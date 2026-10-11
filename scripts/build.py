#!/usr/bin/env python3
"""Build with native OS tools. No third-party runtime libraries are bundled."""
import argparse
import os
import pathlib
import platform
import plistlib
import shutil
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
VERSION = '1.2.0'

def run(*args):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True)

def mac():
    out = ROOT / 'build/mac'
    bundle = out / 'MicBridge.app'
    binary = bundle / 'Contents/MacOS/MicBridge'
    binary.parent.mkdir(parents=True, exist_ok=True)
    cache = ROOT / 'build/swift-cache'
    sources = sorted((ROOT/'src/mac').glob('*.swift'))
    for arch in ['arm64', 'x86_64']:
        run('swiftc', '-swift-version', '5', '-O', '-target', arch+'-apple-macos14.2',
            '-module-cache-path', cache, *sources, '-o', out / ('MicBridge-'+arch))
    run('lipo', '-create', out/'MicBridge-arm64', out/'MicBridge-x86_64', '-output', binary)
    (bundle/'Contents/Info.plist').write_bytes(plistlib.dumps({
        'CFBundleExecutable':'MicBridge', 'CFBundleIdentifier':'io.github.carooot.micbridge',
        'CFBundleName':'MicBridge', 'CFBundleDisplayName':'MicBridge', 'CFBundlePackageType':'APPL',
        'CFBundleShortVersionString':VERSION, 'CFBundleVersion':'3', 'CFBundleIconFile':'MicBridge',
        'LSMinimumSystemVersion':'14.2', 'NSHighResolutionCapable':True,
    }))
    (bundle/'Contents/Resources').mkdir(exist_ok=True)
    shutil.copy(ROOT/'assets/MicBridge.icns',bundle/'Contents/Resources/MicBridge.icns')
    run('codesign', '--force', '--sign', '-', '--identifier', 'io.github.carooot.micbridge', bundle)
    run('codesign', '--verify', '--strict', bundle)
    run('swiftc', '-swift-version', '5', '-module-cache-path', cache,
        ROOT/'src/mac/Audio.swift', ROOT/'src/mac/AudioUsage.swift', ROOT/'src/mac/Protocol.swift', ROOT/'tests/mac/main.swift', '-o', out/'CoreTests')
    run(out/'CoreTests')
    print(binary)

def windows():
    out = ROOT / 'build/windows'
    out.mkdir(parents=True, exist_ok=True)
    csc = pathlib.Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    refs = ['/r:'+name+'.dll' for name in ['System','System.Core','System.Windows.Forms','System.Drawing']]
    run(csc, '/nologo', '/codepage:65001', '/target:winexe', '/platform:x64', '/optimize+', '/warnaserror+',
        '/out:'+str(out/'MicBridge.exe'), '/win32icon:'+str(ROOT/'assets/MicBridge.ico'),
        '/resource:'+str(ROOT/'assets/MicBridge.ico')+',MicBridge.ico', '/resource:'+str(ROOT/'assets/MicBridge.png')+',MicBridge.png', *refs, *sorted((ROOT/'src/windows').glob('*.cs')))
    (out/'MicBridge.exe.config').write_text('<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>')
    run(csc, '/nologo', '/codepage:65001', '/target:exe', '/out:'+str(out/'CoreTests.exe'), '/r:System.dll', '/r:System.Core.dll',
        ROOT/'src/windows/Core.cs', ROOT/'src/windows/Connection.cs', ROOT/'tests/CoreTests.cs', ROOT/'tests/ConnectionTests.cs')
    run(out/'CoreTests.exe')
    run(out/'MicBridge.exe', '--preview', out/'preview')
    print(out/'MicBridge.exe')

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('target', choices=['mac','windows'], nargs='?', default='mac' if platform.system()=='Darwin' else 'windows')
    args = parser.parse_args()
    (mac if args.target == 'mac' else windows)()
