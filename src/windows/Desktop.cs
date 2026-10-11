using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MicBridge
{
    internal sealed class Sender : Form
    {
        readonly Button connect=UI.Button("连接 Mac"),pause=UI.Button("暂停联动"),settings=UI.Button("设置");
        readonly Label headline=UI.Label("准备连接",25,true),detail=UI.Label("连接后，只在 Mac 的语音应用收音时打开麦克风。",11),
            status=UI.Label("未连接",10,true),deviceLabel=UI.Label("",10),elapsed=UI.Label("麦克风关闭",10),notice=UI.Label("",10);
        readonly ProgressBar meter=new ProgressBar {Dock=DockStyle.Fill,Maximum=32768,Height=8};
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=100};
        readonly CaptureDemand demand=new CaptureDemand();
        readonly string preferenceFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MicBridge","settings.ini");
        Preferences preferences=new Preferences();
        Connection connection;Recorder recorder;
        bool busy,closing,exiting,editing;int peak;long generation;DateTime started;

        public Sender()
        {
            UI.Window(this,"MicBridge · Windows",520,460);
            var root=UI.Column(22);Controls.Add(root);
            var header=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=new Padding(0,0,0,24)};
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var brand=UI.Column(0);brand.Controls.Add(UI.Label("MicBridge",20,true));brand.Controls.Add(UI.Label("Windows 麦克风",10));
            header.Controls.Add(brand,0,0);header.Controls.Add(settings,1,0);root.Controls.Add(header);
            var card=UI.Column(22);card.BackColor=Color.White;card.Margin=new Padding(0,0,0,18);
            status.ForeColor=UI.Accent;card.Controls.Add(status);headline.Margin=new Padding(0,14,0,8);card.Controls.Add(headline);
            detail.MaximumSize=new Size(426,0);detail.ForeColor=UI.Muted;card.Controls.Add(detail);
            meter.Margin=new Padding(0,24,0,10);card.Controls.Add(meter);card.Controls.Add(elapsed);
            pause.Dock=DockStyle.Top;pause.Margin=new Padding(0,18,0,0);card.Controls.Add(pause);root.Controls.Add(card);
            var footer=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            deviceLabel.MaximumSize=new Size(320,0);footer.Controls.Add(deviceLabel,0,0);footer.Controls.Add(connect,1,0);root.Controls.Add(footer);
            notice.ForeColor=Color.FromArgb(170,70,30);notice.MaximumSize=new Size(470,0);notice.Margin=new Padding(0,12,0,0);root.Controls.Add(notice);
            try {if(File.Exists(preferenceFile)) preferences=Preferences.Decode(File.ReadAllText(preferenceFile));}
            catch(IOException) {notice.Text="设置未能读取，本次使用默认麦克风。";}catch(UnauthorizedAccessException) {notice.Text="设置未能读取，本次使用默认麦克风。";}
            connect.Click+=delegate {ToggleConnection();};pause.Click+=delegate {demand.Pause(!demand.Paused);ApplyCapture();};settings.Click+=delegate {OpenSettings();};
            var menu=new ContextMenuStrip();menu.Items.Add("显示窗口",null,delegate {ShowWindow();});
            menu.Items.Add("暂停 / 恢复联动",null,delegate {if(connection!=null) {demand.Pause(!demand.Paused);ApplyCapture();}});
            menu.Items.Add("退出",null,delegate {exiting=true;Close();});
            tray=new NotifyIcon {Icon=SystemIcons.Application,Text="MicBridge · 未连接",Visible=true,ContextMenuStrip=menu};
            tray.DoubleClick+=delegate {ShowWindow();};
            timer.Tick+=delegate {meter.Value=Math.Min(32768,peak);peak=0;if(recorder!=null) elapsed.Text="正在传输  "+(DateTime.UtcNow-started).ToString(@"mm\:ss");};timer.Start();
            SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
            Render();UI.Fit(this,root);
        }
        void ShowWindow() {Show();WindowState=FormWindowState.Normal;Activate();}
        void Render()
        {
            connect.Text=connection==null?"连接 Mac":"断开连接";connect.Enabled=!busy;pause.Enabled=connection!=null;
            pause.Text=demand.Paused?"恢复自动联动":"暂停联动";
            deviceLabel.Text=string.IsNullOrEmpty(preferences.Device)?"麦克风：跟随 Windows 默认设备":"麦克风："+preferences.Device;
            status.Text=connection==null?(busy?"连接中":"未连接"):(demand.Paused?"已暂停":"已连接 Mac");
            headline.Text=recorder!=null?"正在传输声音":connection==null?"准备连接":demand.Paused?"麦克风已暂停":"等待 Mac 语音输入";
            detail.Text=recorder!=null?"继续使用 Mac 的语音快捷键。语音应用结束收音后，会自动关闭麦克风。":connection==null?"连接后，只在 Mac 的语音应用收音时打开麦克风。":demand.Paused?"已关闭麦克风。恢复后，继续跟随 Mac 的收音状态。":"照常唤起豆包或其他语音应用，无需额外按键。";
            if(recorder==null) elapsed.Text="麦克风关闭";
            tray.Text=recorder!=null?"MicBridge · 正在传输声音":"MicBridge · 麦克风关闭";
        }
        async void ToggleConnection()
        {
            if(busy || closing || editing) return;
            if(connection!=null) {Disconnect("");return;}
            busy=true;notice.Text="";Render();long current=++generation;
            try
            {
                var opened=await Task.Run(()=>Connection.Open(message=>Post(current,()=>Disconnect(message)),active=>Post(current,()=>{demand.Request(active);ApplyCapture();})));
                if(closing || generation!=current) {opened.Dispose();return;}
                connection=opened;demand.Reset();connection.Run();
            }
            catch(Exception e) {Disconnect(e.Message);}
            finally {busy=false;if(!closing) Render();}
        }
        void Post(long current,Action action)
        {
            if(closing || !IsHandleCreated) return;
            try {BeginInvoke((Action)(()=>{if(!closing && current==generation) action();}));}catch(InvalidOperationException) {}
        }
        void ApplyCapture()
        {
            if(connection!=null && demand.Active && recorder==null)
            {
                try
                {
                    var devices=Recorder.Devices();var selected=devices[0];
                    if(!string.IsNullOrEmpty(preferences.Device))
                    {selected=devices.Find(d=>d.Name==preferences.Device);if(selected==null) throw new IOException("所选麦克风已断开，请在设置中重新选择。");}
                    connection.Outbox.Begin();recorder=new Recorder(Handle,selected.Id);started=DateTime.UtcNow;notice.Text="";
                }
                catch(Exception e) {Disconnect(e.Message);return;}
            }
            else if(!demand.Active || connection==null) EndCapture();
            Render();
        }
        void EndCapture()
        {
            if(recorder!=null) {recorder.Dispose();recorder=null;if(connection!=null) connection.Outbox.End();}
            peak=0;meter.Value=0;
        }
        void Disconnect(string message)
        {++generation;demand.Reset();EndCapture();if(connection!=null) {connection.Dispose();connection=null;}notice.Text=message;Render();}
        void OpenSettings()
        {
            if(editing || busy) return;editing=true;bool wasPaused=demand.Paused;demand.Pause(true);ApplyCapture();
            try
            {
                using(var dialog=new SettingsDialog(preferences))
                {
                    if(dialog.ShowDialog(this)==DialogResult.OK)
                    {
                        var next=dialog.Value;Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile));string temp=preferenceFile+".tmp";
                        File.WriteAllText(temp,next.Encode());if(File.Exists(preferenceFile)) File.Replace(temp,preferenceFile,null);else File.Move(temp,preferenceFile);
                        preferences=next;notice.Text="设置已保存。";
                    }
                }
            }
            catch(Exception e) {notice.Text="设置未保存："+e.Message;}
            finally {editing=false;demand.Pause(wasPaused);ApplyCapture();}
        }
        void SessionChanged(object sender,SessionSwitchEventArgs e) {if(e.Reason==SessionSwitchReason.SessionLock) Post(generation,()=>Disconnect("电脑已锁定，连接已断开。"));}
        void PowerChanged(object sender,PowerModeChangedEventArgs e) {if(e.Mode==PowerModes.Suspend) Post(generation,()=>Disconnect("电脑休眠，连接已断开。"));}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x3C0 && recorder!=null)
            {try {var bytes=recorder.Accept(m.WParam,m.LParam);if(bytes!=null && bytes.Length>0) {peak=Math.Max(peak,Wire.Peak(bytes));connection.Outbox.Add(bytes);}}catch(Exception e) {Disconnect(e.Message);}return;}
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(!exiting && e.CloseReason==CloseReason.UserClosing && preferences.Background) {e.Cancel=true;Hide();return;}
            closing=true;timer.Stop();Disconnect("");SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;
            tray.Dispose();timer.Dispose();base.OnFormClosing(e);
        }
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2 && args[0]=="--preview")
            {
                Directory.CreateDirectory(args[1]);
                using(var form=new Sender()) {form.Show();Application.DoEvents();UI.Snapshot(form,Path.Combine(args[1],"Windows-main.png"));
                    using(var dialog=new SettingsDialog(new Preferences())) {dialog.Show();Application.DoEvents();UI.Snapshot(dialog,Path.Combine(args[1],"Windows-settings.png"));}
                    form.exiting=true;form.Close();}return;
            }
            bool created;using(var mutex=new Mutex(true,"Local\\MicBridgeSender",out created))
            {if(!created) {MessageBox.Show("MicBridge 已在运行，请在任务栏右下角找到它。","MicBridge");return;}
                try {Application.Run(new Sender());}catch(Exception e) {MessageBox.Show(e.Message,"MicBridge 启动失败");}}
        }
    }
}
