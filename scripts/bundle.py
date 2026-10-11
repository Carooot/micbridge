"""One download containing both endpoints and the offline Chinese quick start."""
import hashlib,pathlib,zipfile
root=pathlib.Path(__file__).resolve().parents[1]
artifacts=root/'artifacts';out=root/'dist';out.mkdir(exist_ok=True)
windows=artifacts/'MicBridge-Windows-x64.zip';mac=artifacts/'MicBridge-Mac-Setup.pkg'
for path in [windows,mac]:
    expected=path.with_name(path.name+'.sha256').read_text().split()[0]
    assert hashlib.sha256(path.read_bytes()).hexdigest()==expected,path.name
bundle=out/'MicBridge-Setup.zip'
with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    with zipfile.ZipFile(windows) as win:
        for name in ['MicBridge.exe','MicBridge.exe.config']: z.writestr(name,win.read(name))
    z.write(mac,'Mac/'+mac.name)
    for name in ['开始使用.txt','LICENSE','THIRD-PARTY.md']: z.write(root/name,name)
with zipfile.ZipFile(bundle) as z:
    assert z.testzip() is None
    assert set(z.namelist())=={'MicBridge.exe','MicBridge.exe.config','Mac/MicBridge-Mac-Setup.pkg','开始使用.txt','LICENSE','THIRD-PARTY.md'}
bundle.with_name(bundle.name+'.sha256').write_text(hashlib.sha256(bundle.read_bytes()).hexdigest()+'  '+bundle.name+'\n')
print('PASS: combined download includes Windows, Mac installer and quick start')
