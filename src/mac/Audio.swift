import Foundation
import AudioToolbox
import CoreAudio

struct MicError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}
func checked(_ status: OSStatus, _ action: String) throws {
    if status != noErr { throw MicError(message: "\(action)失败（\(status)）") }
}

// shortcut: small locked buffer for one voice stream; use a lock-free buffer if profiling shows audio dropouts.
final class PCMBuffer {
    private let lock = NSLock()
    private var samples = [Int16](repeating: 0, count: 24000)
    private var start = 0, count = 0
    private var odd: UInt8?
    private(set) var peak: Double = 0
    func append(_ data: Data) {
        lock.lock(); defer { lock.unlock() }
        var maximum = 0
        for byte in data {
            if let low = odd {
                let sample = Int16(bitPattern: UInt16(low) | UInt16(byte) << 8)
                if count == samples.count { start = (start + 1) % samples.count; count -= 1 }
                samples[(start + count) % samples.count] = sample; count += 1
                maximum = max(maximum, abs(Int(sample))); odd = nil
            } else { odd = byte }
        }
        peak = Double(maximum) / 32768
    }
    func fill(_ output: UnsafeMutablePointer<Int16>, frames: Int) {
        lock.lock(); defer { lock.unlock() }
        for i in 0..<frames {
            var sample: Int16 = 0
            if count > 0 { sample = samples[start]; start = (start + 1) % samples.count; count -= 1 }
            output[i * 2] = sample; output[i * 2 + 1] = sample
        }
    }
    func clear() {
        lock.lock(); defer { lock.unlock() }
        count = 0; start = 0; odd = nil; peak = 0
    }
}

func blackHoleUID() throws -> String {
    var address = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyDevices,
        mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
    var size: UInt32 = 0
    try checked(AudioObjectGetPropertyDataSize(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size), "读取声卡")
    var devices = [AudioDeviceID](repeating: 0, count: Int(size) / MemoryLayout<AudioDeviceID>.size)
    try checked(AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size, &devices), "读取声卡")
    for device in devices {
        var name: Unmanaged<CFString>?
        var propertySize = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        var property = AudioObjectPropertyAddress(mSelector: kAudioObjectPropertyName,
            mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
        guard AudioObjectGetPropertyData(device, &property, 0, nil, &propertySize, &name) == noErr,
              let name = name?.takeRetainedValue(), (name as String) == "BlackHole 2ch" else { continue }
        property.mSelector = kAudioDevicePropertyDeviceUID
        propertySize = UInt32(MemoryLayout<CFString>.size)
        var uid: Unmanaged<CFString>?
        try checked(AudioObjectGetPropertyData(device, &property, 0, nil, &propertySize, &uid), "读取 BlackHole")
        guard let uid = uid else { throw MicError(message: "BlackHole 没有返回有效的设备标识") }
        return uid.takeRetainedValue() as String
    }
    throw MicError(message: "没有找到 BlackHole 2ch。请安装或确认声卡已加载，再重新打开接收器。")
}

final class AudioOutput {
    let pcm = PCMBuffer()
    private var queue: AudioQueueRef?
    func start() throws {
        if queue != nil { return }
        var device = try blackHoleUID() as CFString
        var format = AudioStreamBasicDescription(mSampleRate: 48000, mFormatID: kAudioFormatLinearPCM,
            mFormatFlags: kLinearPCMFormatFlagIsSignedInteger | kLinearPCMFormatFlagIsPacked,
            mBytesPerPacket: 4, mFramesPerPacket: 1, mBytesPerFrame: 4,
            mChannelsPerFrame: 2, mBitsPerChannel: 16, mReserved: 0)
        try checked(AudioQueueNewOutput(&format, { context, queue, buffer in
            let owner = Unmanaged<AudioOutput>.fromOpaque(context!).takeUnretainedValue()
            owner.pcm.fill(buffer.pointee.mAudioData.assumingMemoryBound(to: Int16.self), frames: 960)
            buffer.pointee.mAudioDataByteSize = 3840
            AudioQueueEnqueueBuffer(queue, buffer, 0, nil)
        }, Unmanaged.passUnretained(self).toOpaque(), nil, nil, 0, &queue), "创建音频通道")
        do {
            try checked(withUnsafePointer(to: &device) {
                AudioQueueSetProperty(queue!, kAudioQueueProperty_CurrentDevice, $0, UInt32(MemoryLayout<CFString>.size))
            }, "接入 BlackHole")
            for _ in 0..<3 {
                var buffer: AudioQueueBufferRef?
                try checked(AudioQueueAllocateBuffer(queue!, 3840, &buffer), "创建音频缓冲")
                memset(buffer!.pointee.mAudioData, 0, 3840); buffer!.pointee.mAudioDataByteSize = 3840
                try checked(AudioQueueEnqueueBuffer(queue!, buffer!, 0, nil), "启动音频缓冲")
            }
            try checked(AudioQueueStart(queue!, nil), "启动 BlackHole 输出")
        } catch { stop(); throw error }
    }
    func stop() {
        if let queue = queue { AudioQueueStop(queue, true); AudioQueueDispose(queue, true) }
        queue = nil; pcm.clear()
    }
    deinit { stop() }
}
