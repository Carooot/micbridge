import Cocoa
let directory = URL(fileURLWithPath: CommandLine.arguments[1])
try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
for size in [16, 24, 32, 48, 64, 128, 256, 512, 1024] {
    let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
    let transform = NSAffineTransform(); transform.scale(by: CGFloat(size) / 256); transform.concat()
    NSColor(srgbRed: 0.08, green: 0.37, blue: 0.33, alpha: 1).setFill()
    NSBezierPath(roundedRect: NSRect(x: 8, y: 8, width: 240, height: 240), xRadius: 54, yRadius: 54).fill()
    NSColor.white.setFill(); NSColor.white.setStroke()
    NSBezierPath(roundedRect: NSRect(x: 103, y: 103, width: 50, height: 100), xRadius: 25, yRadius: 25).fill()
    let bridge = NSBezierPath(); bridge.lineWidth = 14; bridge.lineCapStyle = .round; bridge.lineJoinStyle = .round
    bridge.move(to: NSPoint(x: 78, y: 142)); bridge.line(to: NSPoint(x: 78, y: 117))
    bridge.curve(to: NSPoint(x: 128, y: 67), controlPoint1: NSPoint(x: 78, y: 85), controlPoint2: NSPoint(x: 100, y: 67))
    bridge.curve(to: NSPoint(x: 178, y: 117), controlPoint1: NSPoint(x: 158, y: 67), controlPoint2: NSPoint(x: 178, y: 85))
    bridge.line(to: NSPoint(x: 178, y: 142)); bridge.stroke()
    let stem = NSBezierPath(); stem.lineWidth = 14; stem.lineCapStyle = .round
    stem.move(to: NSPoint(x: 128, y: 66)); stem.line(to: NSPoint(x: 128, y: 45)); stem.move(to: NSPoint(x: 105, y: 45)); stem.line(to: NSPoint(x: 151, y: 45)); stem.stroke()
    NSGraphicsContext.restoreGraphicsState()
    try bitmap.representation(using: .png, properties: [:])!.write(to: directory.appendingPathComponent("icon-\(size).png"))
}
