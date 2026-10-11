"""Combine MicBridge and the unchanged official BlackHole component; no installation here."""
import hashlib
import pathlib
import plistlib
import shutil
import subprocess
import tempfile
import zipfile
from build import ROOT, VERSION, run

PACKAGE_HASH='57b540f27a3e29c37e310e01bee0fdfab76733087e47f997ef9dccf851400dcf'
SOURCE_HASH='868b54ad05a54c1f83b7559c048054ab51192f66e814899a3e83ba0c69cef005'
SOURCE_COMMIT='e2b22aaaba4e507a097131704bf96dabc004d9cf'

def download(url, path, digest):
    if not path.exists():
        temporary=path.with_suffix(path.suffix+'.download')
        try:
            run('curl','--fail','--location','--silent','--show-error','--connect-timeout','15',
                '--max-time','60','--max-filesize','20000000','--retry','2',url,'--output',temporary)
            data=temporary.read_bytes()
            if len(data)>20_000_000 or hashlib.sha256(data).hexdigest()!=digest:
                raise RuntimeError('Upstream download failed integrity check: '+path.name)
            temporary.replace(path)
        finally:
            if temporary.exists(): temporary.unlink()
    if hashlib.sha256(path.read_bytes()).hexdigest()!=digest:
        raise RuntimeError('Cached download failed integrity check: '+path.name)

if __name__=='__main__':
    cache=ROOT/'build/downloads';cache.mkdir(parents=True,exist_ok=True)
    pkg=cache/'BlackHole2ch-0.7.1.pkg';source=cache/'BlackHole-0.7.1-source.zip'
    download('https://existential.audio/downloads/'+pkg.name,pkg,PACKAGE_HASH)
    download('https://codeload.github.com/ExistentialAudio/BlackHole/zip/'+SOURCE_COMMIT,source,SOURCE_HASH)
    signature=subprocess.check_output(['pkgutil','--check-signature',str(pkg)],text=True)
    if 'Q5C99V536K' not in signature: raise RuntimeError('Unexpected BlackHole signing identity')
    dist=ROOT/'dist';dist.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='installer-',dir=ROOT/'build') as temp:
        work=pathlib.Path(temp);expanded=work/'upstream'
        run('pkgutil','--expand',pkg,expanded)
        run('pkgutil','--flatten',expanded/'BlackHole.pkg',work/'BlackHole.pkg')
        stage=work/'root';stage.mkdir();app=stage/'MicBridge.app'
        shutil.copytree(ROOT/'build/mac/MicBridge.app',app)
        notices=app/'Contents/Resources/BlackHole';notices.mkdir()
        shutil.copy(source,notices/source.name)
        with zipfile.ZipFile(source) as archive:
            license_text=archive.read('BlackHole-'+SOURCE_COMMIT+'/LICENSE')
        (notices/'LICENSE').write_bytes(license_text);shutil.copy(ROOT/'THIRD-PARTY.md',notices/'THIRD-PARTY.md')
        run('codesign','--force','--sign','-','--identifier','io.github.carooot.micbridge',app)
        run('codesign','--verify','--strict',app)
        components=work/'components.plist'
        run('pkgbuild','--analyze','--root',stage,components)
        data=plistlib.loads(components.read_bytes())
        for item in data: item['BundleIsRelocatable']=False
        components.write_bytes(plistlib.dumps(data))
        run('pkgbuild','--root',stage,'--component-plist',components,'--identifier','io.github.carooot.micbridge.pkg',
            '--version',VERSION,'--install-location','/Applications',work/'MicBridge.pkg')
        resources=work/'resources';shutil.copytree(ROOT/'packaging/mac',resources)
        (resources/'LICENSE.txt').write_text((ROOT/'LICENSE').read_text()+'\n\nBlackHole 2ch — GPL-3.0\n\n'+license_text.decode())
        distribution=work/'Distribution'
        distribution.write_text(f'''<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="2">
<title>MicBridge {VERSION} · Mac 整合安装</title>
<welcome file="welcome.html"/><license file="LICENSE.txt"/><conclusion file="conclusion.html"/>
<domains enable_anywhere="false" enable_currentUserHome="false" enable_localSystem="true"/>
<options customize="never" require-scripts="true" hostArchitectures="x86_64,arm64"/>
<volume-check><allowed-os-versions><os-version min="14.2"/></allowed-os-versions></volume-check>
<choices-outline><line choice="receiver"/><line choice="driver"/></choices-outline>
<choice id="receiver" visible="false" title="MicBridge 接收器" start_selected="true"><pkg-ref id="io.github.carooot.micbridge.pkg"/></choice>
<choice id="driver" visible="false" title="BlackHole 2ch（已安装则跳过）" start_selected="!system.files.fileExistsAtPath('/Library/Audio/Plug-Ins/HAL/BlackHole2ch.driver')"><pkg-ref id="audio.existential.BlackHole2ch"/></choice>
<pkg-ref id="io.github.carooot.micbridge.pkg" version="{VERSION}" onConclusion="None">#MicBridge.pkg</pkg-ref>
<pkg-ref id="audio.existential.BlackHole2ch" version="0.7.1" onConclusion="RequireRestart">#BlackHole.pkg</pkg-ref>
</installer-gui-script>''')
        target=dist/'MicBridge-Mac-Setup.pkg'
        run('productbuild','--distribution',distribution,'--resources',resources,'--package-path',work,target)
    shutil.copy(source,dist/source.name)
    for path in [target,dist/source.name]:
        path.with_name(path.name+'.sha256').write_text(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.name+'\n')
    print('Built Mac installer with verified BlackHole and corresponding source')
