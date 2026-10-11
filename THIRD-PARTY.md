# Third-party software in the Mac installer

MicBridge source and its original icon are licensed under MIT (see LICENSE).

The combined installer also installs **BlackHole 2ch 0.7.1**, an independent system audio driver by Existential Audio Inc. and contributors, licensed under **GPL-3.0**. MicBridge does not link or embed BlackHole driver code in its executable. It communicates through macOS audio devices. The driver remains separately installed and independently usable.

- Project: https://github.com/ExistentialAudio/BlackHole
- Official installer: https://existential.audio/downloads/BlackHole2ch-0.7.1.pkg
- Installer SHA-256: `57b540f27a3e29c37e310e01bee0fdfab76733087e47f997ef9dccf851400dcf`
- Corresponding source: tag `v0.7.1`, commit `e2b22aaaba4e507a097131704bf96dabc004d9cf`

The build verifies the official package checksum and Apple signature, then includes its unmodified payload and installation scripts as a separate package component. The GPL license and complete corresponding upstream source archive (including build/install scripts) are included in `MicBridge.app/Contents/Resources/BlackHole/` and as a release download. No additional restriction is placed on the BlackHole component. To rebuild BlackHole, extract that source archive and follow its README and Installer directory. No driver modifications were made.

The combined MicBridge package has not been signed or notarized by Apple. The upstream BlackHole package signature does not sign the combined installer.
