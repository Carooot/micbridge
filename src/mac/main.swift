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
    var ready = false, talking = false
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

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        makeWindow(); checkDevice(); startServer()
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
                if case .ready = state { self.statusField.stringValue = "等待 Windows 连接 · 127.0.0.1:\(port)" }
            }
            server.newConnectionHandler = { [weak self] in self?.accept($0) }; server.start(queue: .main)
        } catch { listenerError = error.localizedDescription; statusField.stringValue = error.localizedDescription }
    }
    func makeWindow() {
        let menu = NSMenu(), appMenu = NSMenu(), item = NSMenuItem()
        item.submenu = appMenu; menu.addItem(item)
        appMenu.addItem(withTitle: "显示接收窗口", action: #selector(showWindow), keyEquivalent: "0").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "退出 MicBridge", action: #selector(quit), keyEquivalent: "q").target = self
        NSApp.mainMenu = menu
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 590, height: 375), styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.title = "MicBridge · Mac 麦克风"; window.center(); window.delegate = self; window.isReleasedWhenClosed = false
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 16
        stack.translatesAutoresizingMaskIntoConstraints = false; window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 26),
            stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -26),
            stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 26)])
        let title = NSTextField(labelWithString: "Windows 说话，Mac 接收")
        title.font = .boldSystemFont(ofSize: 22); stack.addArrangedSubview(title)
        stack.addArrangedSubview(NSTextField(wrappingLabelWithString: "UU 端口直连：TCP · 目标 127.0.0.1:18768 · 本地 18768\nWindows 点击“连接 Mac”，按住 F8 说话，松开停止。无需连接码。"))
        deviceField.textColor = .secondaryLabelColor; stack.addArrangedSubview(deviceField)
        statusField.font = .systemFont(ofSize: 15, weight: .medium); stack.addArrangedSubview(statusField)
        meter.levelIndicatorStyle = .continuousCapacity; meter.minValue = 0; meter.maxValue = 1
        meter.warningValue = 0.85; meter.criticalValue = 0.97
        meter.widthAnchor.constraint(equalToConstant: 530).isActive = true
        meter.setAccessibilityLabel("收到的麦克风音量"); stack.addArrangedSubview(meter)
        let hint = NSTextField(wrappingLabelWithString: "在 Mac 语音 App 中选择“BlackHole 2ch”作为麦克风。\n关闭窗口后继续待命；退出应用则断开连接。")
        hint.textColor = .secondaryLabelColor; stack.addArrangedSubview(hint)
        let buttons = NSStackView()
        buttons.addArrangedSubview(NSButton(title: "重新检查声卡", target: self, action: #selector(checkDevice)))
        buttons.addArrangedSubview(NSButton(title: "断开 Windows", target: self, action: #selector(disconnectAll)))
        stack.addArrangedSubview(buttons)
    }
    @objc func checkDevice() {
        do { _ = try blackHoleUID(); deviceField.stringValue = "BlackHole 2ch 已就绪 · 48 kHz 音频" }
        catch { deviceField.stringValue = error.localizedDescription }
    }
    @objc func showWindow() { window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showWindow(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
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
                        self.statusField.stringValue = "已连接 · Windows 麦克风待命"
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
                output.stop(); if !testWithoutAudio { try output.start() }
                client.talking = true; statusField.stringValue = "正在接收 · 松开 Windows 按键后停止"
                reply(client, "STARTED")
            case .stop:
                client.talking = false; output.stop(); meter.doubleValue = 0
                statusField.stringValue = "已连接 · Windows 麦克风关闭"; reply(client, "STOPPED")
            case .audio(let pcm):
                guard client.talking else { throw MicError(message: "未按下说话键，拒绝音频。") }
                output.pcm.append(pcm); lastAudio = Date()
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
        if active === client { active = nil; output.stop(); meter.doubleValue = 0; statusField.stringValue = listenerError ?? "连接已断开，等待 Windows 连接" }
    }
    func tick() {
        for client in Array(clients.values) {
            if Date().timeIntervalSince(client.ready ? client.lastData : client.created) > (client.ready ? 5 : 3) { disconnect(client) }
        }
        meter.doubleValue = active?.talking == true && Date().timeIntervalSince(lastAudio) < 0.25 ? min(1, output.pcm.peak * 3) : 0
    }
}
let app = NSApplication.shared
let receiver = Receiver(); app.delegate = receiver
if CommandLine.arguments.contains("--test-server") {
    receiver.testWithoutAudio = !CommandLine.arguments.contains("--with-audio")
    receiver.startServer(port: 18769)
    receiver.timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { _ in receiver.tick() }
    RunLoop.main.run(); exit(0)
}
app.run()
