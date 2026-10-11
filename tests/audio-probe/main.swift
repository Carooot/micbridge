import Foundation
import AudioToolbox

final class Probe {
    let lock = NSLock()
    var peak = 0, active = 0, crossings = 0, frames = 0, latePeak = 0
    var previous: Int16 = 0
    var negative = false
    func feed(_ buffer: AudioQueueBufferRef) {
        lock.lock(); defer { lock.unlock() }
        let samples = buffer.pointee.mAudioData.assumingMemoryBound(to: Int16.self)
        for frame in 0..<(Int(buffer.pointee.mAudioDataByteSize) / 4) {
            let sample = samples[frame * 2]
            frames += 1
            if frames > 144000 { latePeak = max(latePeak, abs(Int(sample))) }
            peak = max(peak, abs(Int(sample)))
            if abs(Int(sample)) > 100 { active += 1 }
            if sample < -500 { negative = true }
            if negative && sample > 500 { crossings += 1; negative = false }
            previous = sample
        }
    }
}
let probe = Probe()
var queue: AudioQueueRef?
do {
    var format = AudioStreamBasicDescription(mSampleRate: 48000, mFormatID: kAudioFormatLinearPCM,
        mFormatFlags: kLinearPCMFormatFlagIsSignedInteger | kLinearPCMFormatFlagIsPacked,
        mBytesPerPacket: 4, mFramesPerPacket: 1, mBytesPerFrame: 4,
        mChannelsPerFrame: 2, mBitsPerChannel: 16, mReserved: 0)
    try checked(AudioQueueNewInput(&format, { _, queue, buffer, _, _, _ in
        probe.feed(buffer)
        AudioQueueEnqueueBuffer(queue, buffer, 0, nil)
    }, nil, nil, nil, 0, &queue), "创建虚拟输入测试")
    var device = try blackHoleUID() as CFString
    try checked(withUnsafePointer(to: &device) {
        AudioQueueSetProperty(queue!, kAudioQueueProperty_CurrentDevice, $0, UInt32(MemoryLayout<CFString>.size))
    }, "选择 BlackHole 输入")
    for _ in 0..<3 {
        var buffer: AudioQueueBufferRef?
        try checked(AudioQueueAllocateBuffer(queue!, 3840, &buffer), "创建测试缓冲")
        try checked(AudioQueueEnqueueBuffer(queue!, buffer!, 0, nil), "启动测试缓冲")
    }
    try checked(AudioQueueStart(queue!, nil), "开始虚拟输入测试")
    print("READY"); fflush(stdout)
    Thread.sleep(forTimeInterval: 4)
    AudioQueueStop(queue!, true); AudioQueueDispose(queue!, true); queue = nil
    let frequency = Double(probe.crossings) * 48000 / Double(max(1, probe.active))
    guard probe.active > 48000, (2500...4000).contains(probe.peak), (900...1100).contains(frequency), probe.latePeak < 50
    else { throw MicError(message: "未检测到预期的测试音或静音：peak=\(probe.peak), samples=\(probe.active), estimate=\(Int(frequency))Hz, latePeak=\(probe.latePeak)") }
    print("PASS: TCP 997 Hz tone received through BlackHole input; peak=\(probe.peak), activeSamples=\(probe.active), post-release peak=\(probe.latePeak)")
} catch {
    if let queue = queue { AudioQueueStop(queue, true); AudioQueueDispose(queue, true) }
    fputs("\(error.localizedDescription)\n", stderr); exit(1)
}
