import Foundation

var hello = HelloParser()
precondition(try! hello.feed(Data("MIC".utf8)) == nil)
precondition(try! hello.feed(Data("BRIDGE2\n".utf8) + Data([254, 255])) == Data([254, 255]))
var wrong = HelloParser()
do { _ = try wrong.feed(Data("UUMIC1 12345678\n".utf8)); fatalError("accepted old protocol") } catch {}
let stream = Data([254,255, 4,0, 0xff,0x7f,0,0x80, 0,0, 255,255])
for cut in 0...stream.count {
    var parser = FrameParser()
    let a = try! parser.feed(Data(stream.prefix(cut)))
    let b = try! parser.feed(Data(stream.dropFirst(cut)))
    precondition(a + b == [.start, .audio(Data([255,127,0,128])), .stop, .ping])
}
var bytes = FrameParser(), packets = [Packet]()
for byte in stream { packets += try! bytes.feed(Data([byte])) }
precondition(packets.count == 4)
for invalid in [Data([1,0]),Data([130,7]),Data([253,255])] {
    var parser = FrameParser(); do {_ = try parser.feed(invalid); fatalError("accepted invalid length")} catch {}
}
let pcm = PCMBuffer()
pcm.append(Data([0xff])); pcm.append(Data([0x7f,0,0x80]))
var stereo = [Int16](repeating: 4, count: 6)
pcm.fill(&stereo, frames: 3); precondition(stereo == [32767,32767,-32768,-32768,0,0])
pcm.append(Data(repeating: 1, count: 60000))
var large = [Int16](repeating: -1, count: 48002)
pcm.fill(&large, frames: 24001); precondition(large[47999] == 257 && large[48000] == 0)
pcm.append(Data([1])); pcm.clear(); pcm.fill(&stereo, frames: 3); precondition(stereo.allSatisfy { $0 == 0 })
print("PASS: version mismatch, every frame split, PCM boundaries, overflow and release silence")
