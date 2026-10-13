using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MicBridge
{
    static class LayoutTests
    {
        static int failures;
        static void Check(Form form,string stage,int expectedWidth,string output)
        {
            Application.DoEvents();
            Console.WriteLine(stage+": client="+form.ClientSize+" state="+form.WindowState+" restore="+form.RestoreBounds);
            if(form.WindowState==FormWindowState.Normal && form.ClientSize.Width<expectedWidth-2)
            {failures++;Console.WriteLine("FAIL: window width collapsed; expected at least "+expectedWidth);}
            if(form.WindowState==FormWindowState.Normal) UI.Snapshot(form,Path.Combine(output,stage+".png"));
        }
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string output=args[0];Directory.CreateDirectory(output);
            foreach(float scale in new float[]{1,1.25f,1.5f,2})
            foreach(string kind in new string[]{"main","settings","setup"})
            {
                using(Form form=kind=="main"?(Form)new Sender():kind=="settings"?(Form)new SettingsDialog(new Preferences()):new SetupDialog())
                {
                    form.Show();Application.DoEvents();
                    form.Scale(new SizeF(scale,scale));Application.DoEvents();
                    int width=(int)Math.Round((kind=="main"?464:500)*scale);
                    string prefix="layout-"+kind+"-"+(int)(scale*100);
                    Check(form,prefix+"-open",width,output);
                    for(int round=0;round<3;round++)
                    {
                        form.WindowState=FormWindowState.Minimized;Application.DoEvents();
                        if(form is Sender) typeof(Sender).GetMethod("SessionChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{null,new SessionSwitchEventArgs(SessionSwitchReason.SessionLock)});
                        Application.DoEvents();form.WindowState=FormWindowState.Normal;
                        Check(form,prefix+"-restore-"+round,width,output);
                    }
                    form.Hide();Application.DoEvents();form.Show();
                    Check(form,prefix+"-tray",width,output);
                    if(form is Sender) typeof(Sender).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                    form.Close();
                }
            }
            Console.WriteLine(failures==0?"PASS: window width survives scaling, lock notice and restore":"FAIL: "+failures+" collapsed windows");
            return failures==0?0:1;
        }
    }
}
