# Changelog

## 1.2.0 — 2026-10-12

- Refreshed the Windows window with a focused connection panel, consistent microphone icon, tray icon, executable metadata and a built-in first-connection guide.
- Added a combined Mac installer for MicBridge and the verified official BlackHole 2ch component. Existing BlackHole installations are skipped; new installations keep the upstream restart requirement.
- Added a Mac button to select BlackHole as the default input, while leaving speaker output unchanged.
- Added a single download containing the Windows app, Mac installer and offline Chinese instructions centered on UU Remote TCP port forwarding.
- Included BlackHole GPL license and corresponding upstream source; added real package installation and repeat-install selection tests on disposable macOS CI.
- Kept protocol v3 compatible with 1.1.0. The automatic capture flow has now been confirmed by the user on Windows → UU → Mac → Doubao.

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
