import Cocoa
import CoreAudio

struct AudioConsumer {
    let bundle: String
    let name: String
    let active: Bool
}
func audioIDs(_ object: AudioObjectID, _ selector: AudioObjectPropertySelector, scope: AudioObjectPropertyScope = kAudioObjectPropertyScopeGlobal) throws -> [UInt32] {
    var address = AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
    var size: UInt32 = 0
    try checked(AudioObjectGetPropertyDataSize(object, &address, 0, nil, &size), "读取收音状态")
    guard size % 4 == 0, size < 1_000_000 else { throw MicError(message: "收音状态数据无效") }
    if size == 0 { return [] }
    var values = [UInt32](repeating: 0, count: Int(size) / 4)
    try checked(AudioObjectGetPropertyData(object, &address, 0, nil, &size, &values), "读取收音状态")
    return Array(values.prefix(Int(size) / 4))
}
func audioString(_ object: AudioObjectID, _ selector: AudioObjectPropertySelector) throws -> String {
    var address = AudioObjectPropertyAddress(mSelector: selector, mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
    var size = UInt32(MemoryLayout<CFString?>.size), value: Unmanaged<CFString>?
    try checked(AudioObjectGetPropertyData(object, &address, 0, nil, &size, &value), "读取音频应用")
    return value?.takeRetainedValue() as String? ?? ""
}
func audioConsumers() throws -> [AudioConsumer] {
    guard #available(macOS 14.2, *) else { throw MicError(message: "自动联动需要 macOS 14.2 或更新版本") }
    let devices = try audioIDs(AudioObjectID(kAudioObjectSystemObject), kAudioHardwarePropertyDevices)
    guard let input = devices.first(where: { (try? audioString($0, kAudioObjectPropertyName)) == "BlackHole 2ch" }) else {
        throw MicError(message: "未找到 BlackHole 2ch，请先安装虚拟声卡")
    }
    let processes = try audioIDs(AudioObjectID(kAudioObjectSystemObject), kAudioHardwarePropertyProcessObjectList)
    var result = [AudioConsumer]()
    for process in processes {
        // Processes can exit between queries. Only an explicit active input stream can request the microphone.
        guard let pid = try? audioIDs(process, kAudioProcessPropertyPID).first, pid != UInt32(ProcessInfo.processInfo.processIdentifier),
              let bundle = try? audioString(process, kAudioProcessPropertyBundleID) else { continue }
        let running = (try? audioIDs(process, kAudioProcessPropertyIsRunningInput).first) == 1
        let inputs = (try? audioIDs(process, kAudioProcessPropertyDevices, scope: kAudioObjectPropertyScopeInput)) ?? []
        let name = bundle == "com.bytedance.inputmethod.doubaoime" ? "豆包输入法" : NSRunningApplication(processIdentifier: pid_t(pid))?.localizedName ?? bundle
        result.append(AudioConsumer(bundle: bundle, name: name.isEmpty ? "语音应用" : name, active: running && inputs.contains(input)))
    }
    return result
}
struct DemandGate {
    private var lastInput: TimeInterval?
    mutating func update(_ input: Bool, now: TimeInterval) -> Bool {
        if input { lastInput = now; return true }
        return lastInput.map { now - $0 < 0.25 } ?? false
    }
    mutating func reset() { lastInput = nil }
}
