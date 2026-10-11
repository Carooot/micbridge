import Cocoa

private func label(_ text: String, size: CGFloat = 13, bold: Bool = false) -> NSTextField {
    let field = NSTextField(wrappingLabelWithString: text)
    field.font = .systemFont(ofSize: size, weight: bold ? .semibold : .regular)
    field.preferredMaxLayoutWidth = 444
    return field
}
private func column(spacing: CGFloat = 14) -> NSStackView {
    let view = NSStackView(); view.orientation = .vertical; view.alignment = .leading; view.spacing = spacing
    view.translatesAutoresizingMaskIntoConstraints = false
    return view
}
private func mount(_ stack: NSStackView, in window: NSWindow, width: CGFloat = 492) {
    window.contentView!.addSubview(stack)
    NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24),
        stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 24), stack.widthAnchor.constraint(equalToConstant: width)])
    stack.layoutSubtreeIfNeeded()
    window.setContentSize(NSSize(width: width + 48, height: stack.fittingSize.height + 48))
    window.center()
}
extension Receiver {
    func makeWindow() {
        let menu = NSMenu(), appMenu = NSMenu(), item = NSMenuItem()
        item.submenu = appMenu; menu.addItem(item)
        appMenu.addItem(withTitle: "显示接收窗口", action: #selector(showWindow), keyEquivalent: "0").target = self
        appMenu.addItem(withTitle: "设置…", action: #selector(showSettings), keyEquivalent: ",").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "退出 MicBridge", action: #selector(quit), keyEquivalent: "q").target = self
        NSApp.mainMenu = menu
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 540, height: 430), styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.title = "MicBridge · Mac"; window.delegate = self; window.isReleasedWhenClosed = false
        let root = column(spacing: 22), header = NSStackView()
        let brand = column(spacing: 4); brand.addArrangedSubview(label("MicBridge", size: 24, bold: true))
        let subtitle = label("Mac 接收器"); subtitle.textColor = .secondaryLabelColor; brand.addArrangedSubview(subtitle)
        header.addArrangedSubview(brand); header.addArrangedSubview(NSView())
        header.addArrangedSubview(NSButton(title: "设置…", target: self, action: #selector(showSettings)))
        header.widthAnchor.constraint(equalToConstant: 492).isActive = true; root.addArrangedSubview(header)
        let box = NSBox(); box.boxType = .custom; box.cornerRadius = 12; box.borderWidth = 1
        box.borderColor = .separatorColor; box.fillColor = .controlBackgroundColor; box.contentViewMargins = NSSize(width: 20, height: 22)
        let content = column(spacing: 16); box.contentView = content
        deviceField.font = .systemFont(ofSize: 12, weight: .medium); deviceField.textColor = .secondaryLabelColor
        content.addArrangedSubview(deviceField)
        statusField.font = .systemFont(ofSize: 23, weight: .semibold); statusField.preferredMaxLayoutWidth = 444
        content.addArrangedSubview(statusField)
        followingField.font = .systemFont(ofSize: 13); followingField.textColor = .secondaryLabelColor; followingField.preferredMaxLayoutWidth = 444
        followingField.heightAnchor.constraint(greaterThanOrEqualToConstant: 38).isActive = true
        content.addArrangedSubview(followingField)
        meter.levelIndicatorStyle = .continuousCapacity; meter.minValue = 0; meter.maxValue = 1
        meter.warningValue = 0.9; meter.criticalValue = 0.99; meter.setAccessibilityLabel("收到的麦克风音量")
        meter.widthAnchor.constraint(equalToConstant: 444).isActive = true; content.addArrangedSubview(meter)
        pauseButton = NSButton(title: "暂停联动", target: self, action: #selector(togglePause)); content.addArrangedSubview(pauseButton)
        content.widthAnchor.constraint(equalToConstant: 452).isActive = true
        box.widthAnchor.constraint(equalToConstant: 492).isActive = true
        box.heightAnchor.constraint(equalTo: content.heightAnchor, constant: 44).isActive = true
        root.addArrangedSubview(box)
        let footer = label("语音输入用原来的快捷键，MicBridge 自动跟随。\n麦克风：BlackHole 2ch  ·  UU 端口：18768", size: 12)
        footer.textColor = .secondaryLabelColor; root.addArrangedSubview(footer)
        renderStatus(); mount(root, in: window)
    }
    @objc func showSettings() {
        if let panel = settingsPanel { panel.makeKeyAndOrderFront(nil); return }
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 540, height: 360), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        panel.title = "MicBridge 设置"; panel.isReleasedWhenClosed = false; panel.delegate = self; settingsPanel = panel
        let root = column(spacing: 16)
        root.addArrangedSubview(label("自动联动", size: 21, bold: true))
        root.addArrangedSubview(label("仅在所选应用读取 BlackHole 麦克风时，通知 Windows 开麦。"))
        root.addArrangedSubview(label("跟随哪个应用", bold: true))
        sourceChoice.removeAllItems(); sourceChoice.addItem(withTitle: "所有使用 BlackHole 的应用")
        sourceChoice.lastItem?.representedObject = ""
        let consumers = (try? audioConsumers()) ?? []
        var seen = Set<String>()
        for consumer in consumers.sorted(by: { $0.name.localizedStandardCompare($1.name) == .orderedAscending }) {
            guard !consumer.bundle.isEmpty, !consumer.bundle.hasPrefix("com.apple."), seen.insert(consumer.bundle).inserted else { continue }
            sourceChoice.addItem(withTitle: consumer.name); sourceChoice.lastItem?.representedObject = consumer.bundle
        }
        if !followBundle.isEmpty && !seen.contains(followBundle) { sourceChoice.addItem(withTitle: followBundle); sourceChoice.lastItem?.representedObject = followBundle }
        sourceChoice.select(sourceChoice.itemArray.first { ($0.representedObject as? String) == followBundle })
        sourceChoice.widthAnchor.constraint(equalToConstant: 492).isActive = true
        sourceChoice.target = self; sourceChoice.action = #selector(changeSource); root.addArrangedSubview(sourceChoice)
        let hint = label("若应用未出现在列表中，先打开它再重新打开设置。应用需要把输入设备设为 BlackHole 2ch。", size: 12)
        hint.textColor = .secondaryLabelColor; root.addArrangedSubview(hint)
        backgroundCheck = NSButton(checkboxWithTitle: "关闭窗口后继续在后台待命", target: self, action: #selector(changeBackground))
        backgroundCheck.state = UserDefaults.standard.object(forKey: "background") as? Bool == false ? .off : .on; root.addArrangedSubview(backgroundCheck)
        let network = label("UU 端口直连  ·  TCP\nWindows 本地 18768 → Mac 127.0.0.1:18768", size: 12)
        network.textColor = .secondaryLabelColor; root.addArrangedSubview(network)
        let buttons = NSStackView()
        buttons.addArrangedSubview(NSButton(title: "重新检查声卡", target: self, action: #selector(checkDevice)))
        buttons.addArrangedSubview(NSButton(title: "完成", target: self, action: #selector(closeSettings))); root.addArrangedSubview(buttons)
        mount(root, in: panel); panel.makeKeyAndOrderFront(nil)
    }
    @objc func changeSource() {
        followBundle = sourceChoice.selectedItem?.representedObject as? String ?? ""
        UserDefaults.standard.set(followBundle, forKey: "followingBundle"); gate.reset(); setDemand(false); lastUsageCheck = .distantPast
    }
    @objc func changeBackground() { UserDefaults.standard.set(backgroundCheck.state == .on, forKey: "background") }
    @objc func closeSettings() { settingsPanel?.close(); settingsPanel = nil }
    func windowWillClose(_ notification: Notification) { if notification.object as? NSWindow === settingsPanel { settingsPanel = nil } }
    func exportPreviews() {
        guard let index = CommandLine.arguments.firstIndex(of: "--preview"), index + 1 < CommandLine.arguments.count else { return }
        let directory = URL(fileURLWithPath: CommandLine.arguments[index + 1])
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            func save(_ window: NSWindow, _ name: String) throws {
                window.displayIfNeeded(); let view = window.contentView!
                guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw MicError(message: "无法生成界面预览") }
                view.cacheDisplay(in: view.bounds, to: bitmap)
                let image = NSImage(size: view.bounds.size)
                let foreground = NSImage(size: view.bounds.size); foreground.addRepresentation(bitmap)
                image.lockFocus(); NSColor.windowBackgroundColor.setFill(); view.bounds.fill()
                foreground.draw(in: view.bounds, from: .zero, operation: .sourceOver, fraction: 1); image.unlockFocus()
                guard let data = image.tiffRepresentation, let png = NSBitmapImageRep(data: data)?.representation(using: .png, properties: [:]) else { throw MicError(message: "无法保存界面预览") }
                try png.write(to: directory.appendingPathComponent(name))
            }
            try save(window, "Mac-main.png"); showSettings(); try save(settingsPanel!, "Mac-settings.png")
        } catch { fputs("\(error)\n", stderr); exit(1) }
    }
}
