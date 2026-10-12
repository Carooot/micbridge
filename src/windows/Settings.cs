using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

namespace MicBridge
{
    internal static class UI
    {
        public static readonly string Version=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        public static readonly Color Accent=Color.FromArgb(21,94,83),Muted=Color.FromArgb(100,111,116),Tint=Color.FromArgb(233,242,238);
        public static readonly Icon AppIcon=new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("MicBridge.ico"),32,32);
        public static readonly Image Logo=Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("MicBridge.png"));
        public static void Window(Form form,string title,int width,int height)
        {form.Text=title+" · v"+Version;form.Icon=AppIcon;form.Font=new Font("Microsoft YaHei UI",10);form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;
            form.ClientSize=new Size(width,height);form.BackColor=Color.FromArgb(245,247,246);form.ForeColor=Color.FromArgb(28,39,35);
            form.FormBorderStyle=FormBorderStyle.FixedDialog;form.MaximizeBox=false;form.StartPosition=FormStartPosition.CenterScreen;}
        public static Label Label(string text,int size,bool bold=false)
        {return new Label {Text=text,AutoSize=true,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,5)};}
        public static Button Button(string text)
        {var button=new Button {Text=text,AutoSize=true,MinimumSize=new Size(86,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(235,239,237),ForeColor=Accent,Padding=new Padding(12,4,12,4),Margin=new Padding(0),Cursor=Cursors.Hand};
            button.FlatAppearance.BorderSize=0;return button;}
        public static void Primary(Button button) {button.BackColor=Accent;button.ForeColor=Color.White;}
        public static TableLayoutPanel Column(int padding)
        {return new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new Padding(padding),Margin=new Padding(0)};}
        public static void Fit(Form form,Control content)
        {content.SizeChanged+=delegate {if(content.Height>0) form.ClientSize=new Size(form.ClientSize.Width,content.Height);};
            form.Shown+=delegate {form.ClientSize=new Size(form.ClientSize.Width,content.Height);};}
        public static void Snapshot(Form form,string path)
        {using(var bitmap=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
    }
    internal sealed class SettingsDialog : Form
    {
        readonly ComboBox devices=new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="麦克风"};
        readonly CheckBox background=new CheckBox {Text="关闭窗口后，在任务栏后台待命",AutoSize=true};
        readonly Label error=UI.Label("",9);
        public Preferences Value;
        public SettingsDialog(Preferences current)
        {
            UI.Window(this,"MicBridge 设置",500,430);MinimizeBox=false;ShowInTaskbar=false;
            var root=UI.Column(24);Controls.Add(root);
            root.Controls.Add(UI.Label("设置",20,true));root.Controls.Add(UI.Label("只需选好麦克风，语音开关交给 Mac。",10));
            var label=UI.Label("麦克风",11,true);label.Margin=new Padding(0,20,0,8);root.Controls.Add(label);root.Controls.Add(devices);
            var refresh=UI.Button("刷新设备");refresh.Margin=new Padding(0,8,0,16);root.Controls.Add(refresh);
            Action<string> load=name=>{try {devices.Items.Clear();foreach(var d in Recorder.Devices()) devices.Items.Add(d);devices.SelectedIndex=0;
                for(int i=0;i<devices.Items.Count;i++) if(devices.Items[i].ToString()==name) devices.SelectedIndex=i;
                if(!string.IsNullOrEmpty(name) && devices.SelectedIndex==0 && name!=devices.Text) error.Text="原麦克风未找到，保存后将使用默认设备。";}
                catch(Exception e) {error.Text=e.Message;}};
            load(current.Device);refresh.Click+=delegate {load(devices.Text);};
            background.Checked=current.Background;root.Controls.Add(background);
            var info=UI.Label("自动跟随 Mac 的实际收音状态，不占用任何快捷键。\n联动应用可在 Mac 端设置。UU 端口仍为 18768。",10);info.MaximumSize=new Size(445,0);info.ForeColor=UI.Muted;info.Margin=new Padding(0,18,0,12);root.Controls.Add(info);
            var version=UI.Label("版本 "+UI.Version+"\n运行位置："+Application.ExecutablePath,9);version.MaximumSize=new Size(445,0);version.ForeColor=UI.Muted;root.Controls.Add(version);
            error.ForeColor=Color.FromArgb(170,70,30);error.MaximumSize=new Size(445,0);root.Controls.Add(error);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,12,0,0)};
            var save=UI.Button("保存设置");var cancel=UI.Button("取消");UI.Primary(save);cancel.Margin=new Padding(0,0,10,0);
            save.Click+=delegate {if(devices.SelectedIndex<0) {error.Text="请先选择一个麦克风。";return;}Value=new Preferences {Device=devices.SelectedIndex==0?"":devices.Text,Background=background.Checked};DialogResult=DialogResult.OK;};
            cancel.Click+=delegate {DialogResult=DialogResult.Cancel;};buttons.Controls.Add(save);buttons.Controls.Add(cancel);root.Controls.Add(buttons);AcceptButton=save;CancelButton=cancel;UI.Fit(this,root);
        }
    }
}
