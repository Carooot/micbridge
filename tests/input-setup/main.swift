import CoreAudio
let system = AudioObjectID(kAudioObjectSystemObject)
let input = try blackHoleInputID()
if try audioIDs(system, kAudioHardwarePropertyDefaultInputDevice).first == input {
    let speaker = try audioIDs(system, kAudioHardwarePropertyDefaultOutputDevice)
    try useBlackHoleInput()
    precondition(try! audioIDs(system, kAudioHardwarePropertyDefaultInputDevice).first == input)
    precondition(try! audioIDs(system, kAudioHardwarePropertyDefaultOutputDevice) == speaker)
    print("PASS: input setup is idempotent and preserves the output device")
} else { print("SKIP: preserving the user's existing input selection") }
