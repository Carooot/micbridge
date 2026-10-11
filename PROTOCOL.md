# MicBridge wire protocol v3

Transport: ordered TCP stream, Windows client to Mac listener at `127.0.0.1:18768`. Access control and encryption belong to the surrounding trusted tunnel; this protocol has no credentials.

1. Client sends ASCII `MICBRIDGE3\n`.
2. Server checks version, availability of BlackHole (except isolated tests), and absence of an existing active peer, then returns `OK\n`.
3. Server sends `CAPTURE 1\n` when the selected application has an active BlackHole **input** stream, or `CAPTURE 0\n` when it stops (after 250 ms debounce) or is paused. The current value is sent after every handshake. Receiver output is excluded.
4. Unless locally paused, the client opens its microphone only on `CAPTURE 1`, releases it on `CAPTURE 0`, and sends frames with a 2-byte **unsigned little-endian** length/control word:

| Word | Meaning | Payload | Response |
|---|---|---|---|
| `65534` | Start / reset talk segment | none | `STARTED\n` |
| `0` | Stop and clear queued playback | none | `STOPPED\n` |
| `65535` | Keepalive | none | `PONG\n` |
| even `2...1920` | Audio, only after Start | PCM byte count from header | none |

PCM is signed 16-bit little-endian, 48,000 Hz, mono. The Mac duplicates mono samples into both BlackHole channels. A normal audio frame holds 960 samples / 20 ms. TCP may split any header, sample or frame; decoders must retain incomplete input. Lengths outside the table are rejected.

Client pings about once a second. Server closes incomplete handshakes after 3 seconds and established connections without data after 5 seconds. Client treats 5 seconds without server responses as failure. Responses are newline-terminated UTF-8; a client must bound lines to 256 bytes. Errors use `ERROR <message>\n` followed by close.

On capture-off or local pause the client releases WinMM, discards queued unsent audio, and queues STOP. One frame may already be in a socket write. STOP stops/disposes the Mac output queue and clears its ring buffer. Audio arriving without a new START is an error. On capture-off the server stops output immediately and discards in-flight PCM until STOP arrives, so network races do not replay stale audio. Session close also clears playback. The receiver allows only one established client.

The test server uses loopback port **18769**, never the production port. `--test-server` skips hardware output; add `--with-audio` to exercise the installed BlackHole device. These manual protocol tests do not send capture requests. Add `--test-follow` to enable the real CoreAudio demand monitor, as exercised by `tests/automatic.py`.
