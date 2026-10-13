"""Run the real Windows forms through scale, minimize, lock notice and restore."""
import os,pathlib,subprocess
root=pathlib.Path(__file__).resolve().parents[1]
out=root/'build/windows';out.mkdir(parents=True,exist_ok=True)
csc=pathlib.Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
subprocess.run([str(csc),'/nologo','/codepage:65001','/target:exe','/main:MicBridge.LayoutTests',
    '/out:'+str(out/'LayoutTests.exe'),'/r:System.dll','/r:System.Core.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',
    '/resource:'+str(root/'assets/MicBridge.ico')+',MicBridge.ico','/resource:'+str(root/'assets/MicBridge.png')+',MicBridge.png',
    *[str(p) for p in sorted((root/'src/windows').glob('*.cs'))],str(root/'tests/LayoutTests.cs')],check=True)
subprocess.run([str(out/'LayoutTests.exe'),str(out/'preview')],check=True)
