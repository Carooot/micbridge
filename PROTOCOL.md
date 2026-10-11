# MicBridge wire protocol v2

Transport: ordered TCP stream, Windows client to Mac listener at `127.0.0.1:18768`. Access control and encryption belong to the surrounding trusted tunnel; this protocol has no credentials.

1. Client sends ASCII `MICBRIDGE2\n`.
2. Server checks version, availability of BlackHole (except isolated tests), and absence of an existing active peer, then returns `OK\n`.
3. Client sends frames with a 2-byte **unsigned little-endian** length/control word:

| Word | Meaning | Payload | Response |
|---|---|---|---|
| `65534` | Start / reset talk segment | none | `STARTED\n` |
| `0` | Stop and clear queued playback | none | `STOPPED\n` |
| `65535` | Keepalive | none | `PONG\n` |
| even `2...1920` | Audio, only after Start | PCM byte count from header | none |

PCM is signed 16-bit little-endian, 48,000 Hz, mono. The Mac duplicates mono samples into both BlackHole channels. A normal audio frame holds 960 samples / 20 ms. TCP may split any header, sample or frame; decoders must retain incomplete input. Lengths outside the table are rejected.

Client pings about once a second. Server closes incomplete handshakes after 3 seconds and established connections without data after 5 seconds. Client treats 5 seconds without server responses as failure. Responses are newline-terminated UTF-8; a client must bound lines to 256 bytes. Errors use `ERROR <message>\n` followed by close.

On key release the client releases WinMM, discards queued unsent audio, and queues STOP. One frame may already be in a socket write. STOP stops/disposes the Mac output queue and clears its ring buffer. Audio arriving without a new START is an error. Session close also clears playback. The receiver allows only one established client.

The test server uses loopback port **18769**, never the production port. `--test-server` skips hardware output; add `--with-audio` to exercise the installed BlackHole device.
