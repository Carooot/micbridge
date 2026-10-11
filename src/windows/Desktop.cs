using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MicBridge
{
    internal sealed class Sender : Form
    {
        const int StartMessage=0x8001,StopMessage=0x8002;
        delegate IntPtr KeyboardProc(int code,IntPtr w,IntPtr l);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,KeyboardProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int msg,IntPtr w,IntPtr l);
        readonly ComboBox devices=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=490,AccessibleName="选择麦克风"};
        readonly ComboBox keyChoice=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=120,AccessibleName="按住说话快捷键"};
        readonly Button refresh=new Button {Text="刷新麦克风",AutoSize=true};
        readonly Button connect=new Button {Text="连接 Mac",AutoSize=true};
        readonly Label state=new Label {Text="尚未连接 · 麦克风关闭",AutoSize=true,MaximumSize=new Size(490,90)};
        readonly ProgressBar meter=new ProgressBar {Width=490,Maximum=32768};
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=100};
        readonly KeyboardProc keyboard;readonly IntPtr hook;
        Connection connection;Recorder recorder;
        bool busy,closing,held,blocked;int selectedKey=0x77,peak;long generation;DateTime started;

        public Sender()
        {
            Text="MicBridge · Windows 麦克风";ClientSize=new Size(550,450);StartPosition=FormStartPosition.CenterScreen;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(22),AutoScroll=true};Controls.Add(layout);
            layout.Controls.Add(new Label {Text="按住说话，松开停止",Font=new Font(Font.FontFamily,17,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,12)});
            layout.Controls.Add(new Label {Text="UU 端口直连：本地 18768 → Mac 127.0.0.1:18768",AutoSize=true});
            layout.Controls.Add(new Label {Text="麦克风",AutoSize=true,Margin=new Padding(0,12,0,4)});
            layout.Controls.Add(devices);layout.Controls.Add(refresh);
            layout.Controls.Add(new Label {Text="按住说话快捷键（连接后生效）",AutoSize=true,Margin=new Padding(0,8,0,4)});
            keyChoice.Items.AddRange(new object[]{"F8","F9","F10"});keyChoice.SelectedIndex=0;
            layout.Controls.Add(keyChoice);layout.Controls.Add(connect);layout.Controls.Add(state);layout.Controls.Add(meter);
            layout.Controls.Add(new Label {Text="连接只待命。按住快捷键才打开麦克风，松开即释放。\n可以最小化到后台；Mac 的输入设备选 BlackHole 2ch。",AutoSize=true,Margin=new Padding(0,14,0,0)});
            RefreshDevices();refresh.Click+=delegate {RefreshDevices();};
            keyChoice.SelectedIndexChanged+=delegate {selectedKey=0x77+keyChoice.SelectedIndex;};
            connect.Click+=delegate {ToggleConnection();};
            var menu=new ContextMenuStrip();
            menu.Items.Add("显示窗口",null,delegate {Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add("连接 / 断开 Mac",null,delegate {ToggleConnection();});menu.Items.Add("退出",null,delegate {Close();});
            tray=new NotifyIcon {Icon=SystemIcons.Application,Text="MicBridge · 未连接",Visible=true,ContextMenuStrip=menu};
            tray.DoubleClick+=delegate {Show();WindowState=FormWindowState.Normal;Activate();};
            var handle=Handle;keyboard=Keyboard;hook=SetWindowsHookEx(13,keyboard,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) {state.Text="无法启用快捷键，请退出后重新打开。";connect.Enabled=false;}
            timer.Tick+=delegate {meter.Value=peak;peak=0;if(recorder!=null && (DateTime.UtcNow-started).TotalSeconds>=120) {Release();state.Text="单次最长 2 分钟。松开按键后重新按住继续。";}};timer.Start();
            SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
        }
        void RefreshDevices()
        {
            string old=devices.SelectedItem==null?"":devices.SelectedItem.ToString();
            try {devices.Items.Clear();foreach(var d in Recorder.Devices()) devices.Items.Add(d);devices.SelectedIndex=0;
                for(int i=0;i<devices.Items.Count;i++) if(devices.Items[i].ToString()==old) devices.SelectedIndex=i;}
            catch(Exception e) {state.Text=e.Message;}
        }
        async void ToggleConnection()
        {
            if(busy || closing || hook==IntPtr.Zero) return;
            if(connection!=null) {Disconnect("已断开 · 麦克风关闭");return;}
            busy=true;connect.Enabled=false;state.Text="正在连接 Mac…";long current=++generation;
            try
            {
                var opened=await Task.Run(()=>Connection.Open(message=>PostFailure(current,message)));
                if(closing || generation!=current) {opened.Dispose();return;}
                connection=opened;connection.Run();connect.Text="断开 Mac";keyChoice.Enabled=false;
                state.Text="已连接 · 按住 "+keyChoice.Text+" 说话 · 麦克风关闭";tray.Text="MicBridge · 待命，麦克风关闭";
            }
            catch(Exception e) {Disconnect(e.Message);}
            finally {busy=false;if(!closing) connect.Enabled=true;}
        }
        void PostFailure(long current,string message)
        {
            if(closing || !IsHandleCreated) return;
            try {BeginInvoke((Action)(()=>{if(current==generation) {Disconnect(message);tray.ShowBalloonTip(3500,"MicBridge",message,ToolTipIcon.Warning);}}));}catch(InvalidOperationException) {}
        }
        IntPtr Keyboard(int code,IntPtr w,IntPtr l)
        {
            if(code>=0 && (connection!=null || blocked) && Marshal.ReadInt32(l)==selectedKey)
            {
                int message=w.ToInt32();
                if(message==0x100 || message==0x104) {if(!held && !blocked) {held=true;PostMessage(Handle,StartMessage,IntPtr.Zero,IntPtr.Zero);}return new IntPtr(1);}
                if(message==0x101 || message==0x105) {held=false;blocked=false;PostMessage(Handle,StopMessage,IntPtr.Zero,IntPtr.Zero);return new IntPtr(1);}
            }
            return CallNextHookEx(hook,code,w,l);
        }
        void StartCapture()
        {
            if(connection==null || recorder!=null || !held || blocked) return;
            try
            {
                var device=devices.SelectedItem as Recorder.Device;if(device==null) throw new InvalidOperationException("请选择麦克风。");
                connection.Outbox.Begin();recorder=new Recorder(Handle,device.Id);started=DateTime.UtcNow;
                devices.Enabled=refresh.Enabled=false;state.Text="正在传输 · 松开即停止";tray.Text="MicBridge · 麦克风正在传输";
            }
            catch(Exception e) {Release();state.Text=e.Message;}
        }
        void EndCapture()
        {
            if(recorder!=null) {recorder.Dispose();recorder=null;}
            if(connection!=null) {connection.Outbox.End();state.Text="已连接 · 麦克风关闭";tray.Text="MicBridge · 待命，麦克风关闭";}
            devices.Enabled=refresh.Enabled=true;peak=0;meter.Value=0;
        }
        void Release() {blocked|=held;held=false;EndCapture();}
        void Disconnect(string message)
        {++generation;Release();if(connection!=null) {connection.Dispose();connection=null;}keyChoice.Enabled=!blocked;connect.Text="连接 Mac";state.Text=message;tray.Text="MicBridge · 未连接";}
        void SessionChanged(object sender,SessionSwitchEventArgs e) {if(e.Reason==SessionSwitchReason.SessionLock) PostFailure(generation,"电脑已锁定，连接已断开。");}
        void PowerChanged(object sender,PowerModeChangedEventArgs e) {if(e.Mode==PowerModes.Suspend) PostFailure(generation,"电脑休眠，连接已断开。");}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==StartMessage) {StartCapture();return;}
            if(m.Msg==StopMessage) {EndCapture();if(connection==null) keyChoice.Enabled=true;return;}
            if(m.Msg==0x3C0 && recorder!=null)
            {try {var bytes=recorder.Accept(m.WParam,m.LParam);if(bytes!=null && bytes.Length>0) {peak=Math.Max(peak,Wire.Peak(bytes));connection.Outbox.Add(bytes);}}catch(Exception e) {Disconnect(e.Message);}return;}
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            closing=true;timer.Stop();Disconnect("已退出");SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;
            if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook);tray.Dispose();timer.Dispose();base.OnFormClosing(e);
        }
        [STAThread] static void Main()
        {
            bool created;using(var mutex=new Mutex(true,"Local\\MicBridgeSender",out created))
            {if(!created) {MessageBox.Show("MicBridge 已在运行，请在任务栏右下角找到它。","MicBridge");return;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                try {Application.Run(new Sender());}catch(Exception e) {MessageBox.Show(e.Message,"MicBridge 启动失败");}}
        }
    }
}
