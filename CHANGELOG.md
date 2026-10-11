# Changelog

## 1.1.0 — 2026-10-12

- Automatically follow the selected Mac application’s active BlackHole input stream. Windows opens its microphone only when requested and closes it after input stops.
- Removed F8/F9/F10 hooks and the separate hold-to-talk control; use the speech application’s existing shortcut or button.
- Added native settings for the Windows microphone, the Mac application to follow, and background operation. Refreshed both interfaces with clearer connection and capture states.
- Added manual pause, persistent settings, real TCP control tests, and input-driven BlackHole integration tests that reject output feedback.
- Protocol v3 requires both endpoints to upgrade. The Mac receiver now requires macOS 14.2 or newer.

## 1.0.0 — 2026-10-12

First public release of MicBridge.

- Hold F8 (or select F9/F10) to capture and transmit; release to close the microphone and clear playback.
- Removed connection codes; use an existing trusted TCP tunnel with loopback-only listeners.
- Added Windows microphone selection and device refresh.
- Kept the link alive while idle without capturing audio; bounded queues and explicit stop messages prevent old speech draining after release.
- Added version mismatch, device, occupied port and connection failure feedback.
- Added macOS universal build, Windows build, protocol/queue tests, TCP integration and optional BlackHole audio probe.
- Added MIT license and Chinese setup/troubleshooting documentation.

Both endpoints must be upgraded together from the earlier private prototype.
