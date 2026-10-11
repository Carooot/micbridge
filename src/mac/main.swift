import Cocoa
import Network

if CommandLine.arguments.contains("--check-audio") {
    do {let output = AudioOutput(); try output.start(); output.stop(); print("PASS: BlackHole 2ch output")}
    catch {fputs("\(error.localizedDescription)\n", stderr); exit(1)}
    exit(0)
}

final class Client {
    let connection: NWConnection
    var hello = HelloParser(), frames = FrameParser()
    var ready = false, talking = false, requested = false
    var sentDemand: Bool?
    var lastData = Date()
    let created = Date()
    init(_ connection: NWConnection) { self.connection = connection }
}

final class Receiver: NSObject, NSApplicationDelegate, NSWindowDelegate {
    let output = AudioOutput()
    var listener: NWListener?
    var clients = [ObjectIdentifier: Client]()
    var active: Client?
    var window: NSWindow!
    var statusField = NSTextField(wrappingLabelWithString: "正在启动…")
    var deviceField = NSTextField(wrappingLabelWithString: "")
    var meter = NSLevelIndicator()
    var timer: Timer?
    var lastAudio = Date.distantPast
    var testWithoutAudio = false
    var listenerError: String?
    var pauseButton = NSButton(), settingsPanel: NSPanel?, sourceChoice = NSPopUpButton()
    var backgroundCheck = NSButton(), followingField = NSTextField(wrappingLabelWithString: "")
    var usageError: String?, gate = DemandGate(), paused = false
    var followBundle = "", sourceNames = [String](), lastUsageCheck = Date.distantPast
    var useAutomaticFollow = true
    var isPreview: Bool { CommandLine.arguments.contains("--preview") }


    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        if let saved = UserDefaults.standard.string(forKey: "followingBundle") { followBundle = saved }
        else if (try? audioConsumers().contains { $0.bundle == "com.bytedance.inputmethod.doubaoime" }) == true { followBundle = "com.bytedance.inputmethod.doubaoime" }
        makeWindow(); checkDevice()
        if isPreview { exportPreviews(); NSApp.terminate(nil); return }
        startServer()
        timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in self?.tick() }
        showWindow()
    }
    func startServer(port: UInt16 = 18768) {
        let params = NWParameters.tcp
        params.requiredLocalEndpoint = .hostPort(host: "127.0.0.1", port: NWEndpoint.Port(rawValue: port)!)
        do {
            let server = try NWListener(using: params); listener = server
            server.stateUpdateHandler = { [weak self] state in
                guard let self = self else { return }
                if case .failed(let error) = state {
                    self.listenerError = "端口无法启动：\(error.localizedDescription)。请先退出旧版接收器。"
                    self.statusField.stringValue = self.listenerError!
                }
                if case .ready = state { self.renderStatus() }
            }
            server.newConnectionHandler = { [weak self] in self?.accept($0) }; server.start(queue: .main)
        } catch { listenerError = error.localizedDescription; statusField.stringValue = error.localizedDescription }
    }
    @objc func checkDevice() {
        do { _ = try blackHoleUID(); deviceField.stringValue = "BlackHole 2ch · 已就绪" }
        catch { deviceField.stringValue = error.localizedDescription }
    }
    @objc func showWindow() { window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showWindow(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { UserDefaults.standard.object(forKey: "background") as? Bool == false }
    func applicationWillTerminate(_ notification: Notification) { listener?.cancel(); disconnectAll() }
    @objc func disconnectAll() { for client in Array(clients.values) { disconnect(client) } }
    func accept(_ connection: NWConnection) {
        guard clients.count < 8 else { connection.cancel(); return }
        let client = Client(connection); clients[ObjectIdentifier(client)] = client
        connection.stateUpdateHandler = { [weak self, weak client] state in
            guard let client = client else { return }
            switch state { case .failed, .cancelled: self?.disconnect(client); default: break }
        }
        connection.start(queue: .main); receive(client)
    }
    func receive(_ client: Client) {
        client.connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { [weak self, weak client] data, _, complete, error in
            guard let self = self, let client = client, self.clients[ObjectIdentifier(client)] != nil else { return }
            if let data = data, !data.isEmpty {
                client.lastData = Date()
                do {
                    if client.ready { try self.consume(data, client) }
                    else if let tail = try client.hello.feed(data) {
                        guard self.active == nil else { throw MicError(message: "已有 Windows 连接，请先断开原连接。") }
                        if !self.testWithoutAudio { _ = try blackHoleUID() }
                        self.active = client; client.ready = true
                        client.requested = !self.useAutomaticFollow
                        self.renderStatus()
                        self.reply(client, "OK"); try self.consume(tail, client)
                    }
                } catch { self.reject(client, error.localizedDescription); return }
            }
            if complete || error != nil { self.disconnect(client) } else { self.receive(client) }
        }
    }
    func consume(_ data: Data, _ client: Client) throws {
        for packet in try client.frames.feed(data) {
            switch packet {
            case .ping: reply(client, "PONG")
            case .start:
                output.stop(); if client.requested && !testWithoutAudio { try output.start() }
                client.talking = true; renderStatus()
                reply(client, "STARTED")
            case .stop:
                client.talking = false; output.stop(); meter.doubleValue = 0
                renderStatus(); reply(client, "STOPPED")
            case .audio(let pcm):
                guard client.talking else { throw MicError(message: "音频流尚未开始。") }
                if client.requested { output.pcm.append(pcm); lastAudio = Date() }
            }
        }
    }
    func reply(_ client: Client, _ text: String) {
        client.connection.send(content: Data((text + "\n").utf8), completion: .contentProcessed { [weak self, weak client] error in
            if error != nil, let client = client { self?.disconnect(client) }
        })
    }
    func reject(_ client: Client, _ message: String) {
        client.connection.send(content: Data(("ERROR " + message + "\n").utf8), completion: .contentProcessed { [weak self, weak client] _ in
            if let client = client { self?.disconnect(client) }
        })
    }
    func disconnect(_ client: Client) {
        guard clients.removeValue(forKey: ObjectIdentifier(client)) != nil else { return }
        client.connection.cancel()
        if active === client { active = nil; output.stop(); meter.doubleValue = 0; renderStatus() }
    }
    func tick() {
        for client in Array(clients.values) {
            if Date().timeIntervalSince(client.ready ? client.lastData : client.created) > (client.ready ? 5 : 3) { disconnect(client) }
        }
        if useAutomaticFollow && Date().timeIntervalSince(lastUsageCheck) >= 0.1 {
            lastUsageCheck = Date()
            do {
                let consumers = try audioConsumers()
                let names = consumers.filter { $0.active && (followBundle.isEmpty || $0.bundle == followBundle) }.map { $0.name }
                sourceNames = Array(Set(names)).sorted(); usageError = nil
                let wanted = gate.update(!names.isEmpty, now: ProcessInfo.processInfo.systemUptime) && !paused
                setDemand(wanted)
            } catch { usageError = error.localizedDescription; gate.reset(); setDemand(false) }
            renderStatus()
        }
        meter.doubleValue = active?.talking == true && active?.requested == true && Date().timeIntervalSince(lastAudio) < 0.25 ? min(1, output.pcm.peak * 3) : 0
    }
    func setDemand(_ desired: Bool) {
        guard let client = active else { return }
        client.requested = desired
        if client.sentDemand != desired {
            client.sentDemand = desired
            if !desired { output.stop(); meter.doubleValue = 0 }
            reply(client, desired ? "CAPTURE 1" : "CAPTURE 0")
        }
    }
    @objc func togglePause() { paused.toggle(); gate.reset(); if paused { setDemand(false) }; renderStatus() }
    func renderStatus() {
        pauseButton.title = paused ? "恢复自动联动" : "暂停联动"
        if let error = listenerError ?? usageError { statusField.stringValue = "需要检查"; followingField.stringValue = error; return }
        if active == nil { statusField.stringValue = "等待 Windows 连接"; followingField.stringValue = "在 Windows 打开 MicBridge，点击“连接 Mac”。" }
        else if paused { statusField.stringValue = "联动已暂停"; followingField.stringValue = "Windows 麦克风已关闭，恢复后继续跟随语音输入。" }
        else if active?.talking == true && active?.requested == true { statusField.stringValue = "正在接收声音"; followingField.stringValue = sourceNames.isEmpty ? "语音输入结束后，Windows 会自动关闭麦克风。" : sourceNames.joined(separator: "、") + "正在收音，结束后自动关麦。" }
        else if active?.requested == true { statusField.stringValue = "语音输入已唤起"; followingField.stringValue = "正在等待 Windows 送入声音；若长时间未开始，请检查 Windows 是否暂停。" }
        else { statusField.stringValue = "已连接 · 等待语音输入"; followingField.stringValue = "照常唤起 Mac 的语音输入，Windows 会自动开麦。" }
    }

}
let app = NSApplication.shared
let receiver = Receiver(); app.delegate = receiver
if CommandLine.arguments.contains("--test-server") {
    receiver.testWithoutAudio = !CommandLine.arguments.contains("--with-audio")
    receiver.useAutomaticFollow = CommandLine.arguments.contains("--test-follow")
    receiver.startServer(port: 18769)
    receiver.timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { _ in receiver.tick() }
    RunLoop.main.run(); exit(0)
}
app.run()
