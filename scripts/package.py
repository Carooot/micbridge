#!/usr/bin/env python3
import argparse
import hashlib
import pathlib
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('target', choices=['mac','windows'])
args = parser.parse_args()
dist = ROOT / 'dist'
dist.mkdir(exist_ok=True)
source = ROOT / 'build' / args.target
if args.target == 'mac':
    files = sorted((source/'MicBridge.app').rglob('*'))
    name = 'MicBridge-macOS-universal.zip'
else:
    files = [source/'MicBridge.exe', source/'MicBridge.exe.config']
    name = 'MicBridge-Windows-x64.zip'
archive = dist/name
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for path in files:
        if path.is_file(): z.write(path, path.relative_to(source))
    for doc in ['README.md','LICENSE','CHANGELOG.md']:
        z.write(ROOT/doc,doc)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    expected = 'MicBridge.app/Contents/MacOS/MicBridge' if args.target == 'mac' else 'MicBridge.exe'
    assert expected in z.namelist()
    if args.target == 'mac': assert (z.getinfo(expected).external_attr >> 16) & 0o111
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
(dist/(name+'.sha256')).write_text(digest+'  '+name+'\n')
print(f'{archive}: {archive.stat().st_size:,} bytes; ZIP checked')
