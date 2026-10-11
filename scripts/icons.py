"""Regenerate the original microphone mark on macOS; no third-party dependencies."""
import pathlib,shutil,struct,subprocess
root=pathlib.Path(__file__).resolve().parents[1]
subprocess.run(['swift','-module-cache-path',str(root/'build/swift-cache'),str(root/'scripts/icon.swift'),str(root/'build/icon')],check=True)
sizes=[16,24,32,48,64,128,256];frames=[(root/f'build/icon/icon-{s}.png').read_bytes() for s in sizes]
offset=6+16*len(frames);out=bytearray(struct.pack('<HHH',0,1,len(frames)))
for s,b in zip(sizes,frames):
    out+=struct.pack('<BBBBHHII',s%256,s%256,0,0,1,32,len(b),offset);offset+=len(b)
(root/'assets/MicBridge.ico').write_bytes(out+b''.join(frames))
shutil.copy(root/'build/icon/icon-128.png',root/'assets/MicBridge.png')
iconset=root/'build/MicBridge.iconset';iconset.mkdir(exist_ok=True)
for s in [16,32,128,256,512]:
    for scale in [1,2]: shutil.copy(root/f'build/icon/icon-{s*scale}.png',iconset/f'icon_{s}x{s}{"@2x" if scale==2 else ""}.png')
subprocess.run(['iconutil','-c','icns',str(iconset),'-o',str(root/'assets/MicBridge.icns')],check=True)
