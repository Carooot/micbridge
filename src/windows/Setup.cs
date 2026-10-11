using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace MicBridge
{
    internal sealed class SetupDialog : Form
    {
        public SetupDialog()
        {
            UI.Window(this,"MicBridge · 首次连接",500,540);MinimizeBox=false;ShowInTaskbar=false;
            var root=UI.Column(24);Controls.Add(root);
            root.Controls.Add(UI.Label("让两台电脑连起来",20,true));
            var intro=UI.Label("通过 UU 远程，把这台 Windows 的麦克风送到 Mac。",10);intro.ForeColor=UI.Muted;root.Controls.Add(intro);
            Step(root,"1  在 Mac 安装一次","把整套下载包里的 Mac 文件夹传给 Mac，双击安装器。\n它会一起安装 MicBridge 和 BlackHole，无需 Homebrew。");
            var mac=UI.Button("找到 Mac 安装器");root.Controls.Add(mac);
            var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Mac","MicBridge-Mac-Setup.pkg");
            if(!File.Exists(path)) mac.Text="下载 Mac 安装器";
            mac.Click+=delegate {try {if(File.Exists(path)) Process.Start("explorer.exe","/select,\""+path+"\"");else Process.Start("https://github.com/Carooot/micbridge/releases/latest/download/MicBridge-Mac-Setup.pkg");}catch(Exception e) {MessageBox.Show(this,e.Message,"无法打开安装器位置");}};
            Step(root,"2  开启 UU 端口直连","在 UU 中选中这台 Mac，添加并开启以下 TCP 映射。\n这是首次连接时需要在 UU 内完成的一步。");
            var ports=UI.Label("目标地址     127.0.0.1\nMac 目标端口  18768\nWindows 本地端口  18768",11,true);ports.Padding=new Padding(12);ports.BackColor=UI.Tint;root.Controls.Add(ports);
            Step(root,"3  点击“连接 Mac”","Mac 打开 MicBridge，点击“设为语音输入”。\n连接后照常唤起豆包等语音应用，麦克风自动跟随。");
            var done=UI.Button("知道了");UI.Primary(done);done.Margin=new Padding(0,12,0,0);done.Click+=delegate {Close();};root.Controls.Add(done);AcceptButton=done;UI.Fit(this,root);
        }
        static void Step(TableLayoutPanel root,string title,string text)
        {var heading=UI.Label(title,12,true);heading.Margin=new Padding(0,20,0,7);root.Controls.Add(heading);
            var body=UI.Label(text,10);body.MaximumSize=new Size(445,0);body.ForeColor=UI.Muted;root.Controls.Add(body);}
    }
}
