"""Inspect actual Installer choices; install only on a disposable GitHub macOS runner."""
import hashlib,os,pathlib,plistlib,subprocess,sys,tempfile,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[1];pkg=root/'dist/MicBridge-Mac-Setup.pkg'
sys.path.insert(0,str(root/'scripts'))
from build import VERSION
driver=pathlib.Path('/Library/Audio/Plug-Ins/HAL/BlackHole2ch.driver')
def selected():
    data=plistlib.loads(subprocess.check_output(['installer','-showChoicesXML','-pkg',str(pkg),'-target','/']))
    result={}
    def walk(rows):
        for row in rows:
            result[row['choiceIdentifier']]=bool(row['choiceIsSelected'])
            walk(row.get('childItems',[]))
    walk(data);return result
before=selected();assert before['receiver'];assert before['driver']==(not driver.exists()),before
with tempfile.TemporaryDirectory() as temp:
    expanded=pathlib.Path(temp)/'package'
    subprocess.run(['pkgutil','--expand-full',str(pkg),str(expanded)],check=True)
    driver_payload=expanded/'BlackHole.pkg/Payload/BlackHole2ch.driver'
    assert (driver_payload/'Contents/MacOS/BlackHole').exists()
    subprocess.run(['codesign','--verify','--strict',str(driver_payload)],check=True)
    app=expanded/'MicBridge.pkg/Payload/MicBridge.app'
    subprocess.run(['codesign','--verify','--strict',str(app)],check=True)
    assert plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleShortVersionString']==VERSION
    source=app/'Contents/Resources/BlackHole/BlackHole-0.7.1-source.zip'
    assert hashlib.sha256(source.read_bytes()).hexdigest()=='868b54ad05a54c1f83b7559c048054ab51192f66e814899a3e83ba0c69cef005'
    assert ET.parse(expanded/'Distribution').find("pkg-ref[@id='audio.existential.BlackHole2ch']").get('onConclusion')=='RequireRestart'
print('PASS: package payload, app signature, source archive, automatic driver selection and restart requirement')
if '--install' in sys.argv:
    assert os.environ.get('GITHUB_ACTIONS')=='true' and os.environ.get('RUNNER_OS')=='macOS','Install test is limited to disposable CI runners'
    subprocess.run(['sudo','installer','-pkg',str(pkg),'-target','/'],check=True)
    assert driver.exists();assert not selected()['driver']
    info=plistlib.loads(pathlib.Path('/Applications/MicBridge.app/Contents/Info.plist').read_bytes())
    assert info['CFBundleShortVersionString']==VERSION
    subprocess.run(['codesign','--verify','--strict',str(driver)],check=True)
    print('PASS: first installation completed; subsequent installation skips existing driver; no reboot performed')
