import Foundation

struct HelloParser {
    static let hello = Data("MICBRIDGE2\n".utf8)
    private var pending = Data()
    mutating func feed(_ data: Data) throws -> Data? {
        pending.append(data)
        let count = min(pending.count, Self.hello.count)
        guard pending.prefix(count) == Self.hello.prefix(count) else {
            throw MicError(message: "协议版本不同，请同时升级 Windows 和 Mac 两端。")
        }
        guard pending.count >= Self.hello.count else { return nil }
        return Data(pending.dropFirst(Self.hello.count))
    }
}

enum Packet: Equatable { case start, stop, ping, audio(Data) }
struct FrameParser {
    private var pending = Data()
    mutating func feed(_ data: Data) throws -> [Packet] {
        pending.append(data)
        var result = [Packet]()
        while pending.count >= 2 {
            let length = Int(pending[0]) | Int(pending[1]) << 8
            if length == 0 || length >= 65534 {
                result.append(length == 0 ? .stop : length == 65534 ? .start : .ping)
                pending = Data(pending.dropFirst(2)); continue
            }
            guard length <= 1920, length % 2 == 0 else { throw MicError(message: "收到无效的音频帧。") }
            guard pending.count >= length + 2 else { break }
            result.append(.audio(Data(pending[2..<(length + 2)])))
            pending = Data(pending.dropFirst(length + 2))
        }
        return result
    }
}
