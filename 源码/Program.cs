using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("音箱静音保活")]
[assembly: AssemblyDescription("Windows 原生托盘全零静音工具")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

namespace SpeakerKeepAlive
{
    public static class StartupSetting
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "SpeakerKeepAlive";
        public static string Command { get { return "\"" + Application.ExecutablePath + "\" --background"; } }
        public static bool Enabled
        {
            get { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key)) { return key != null && string.Equals(key.GetValue(Name) as string, Command, StringComparison.OrdinalIgnoreCase); } }
        }
        public static void Set(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (enabled) key.SetValue(Name, Command, RegistryValueKind.String);
                else key.DeleteValue(Name, false);
            }
        }
    }

    internal static class Program
    {
        internal static readonly string Prefix = @"Local\SpeakerKeepAlive.v1." + WindowsIdentity.GetCurrent().User.Value + ".";
        [STAThread]
        static int Main(string[] args)
        {
            string mode = args.Length == 0 ? "" : args[0].ToLowerInvariant();
            if (mode == "--pause" || mode == "--resume" || mode == "--exit" || mode == "--show")
                return Signal(mode.Substring(2)) ? 0 : 2;
            if (mode != "" && mode != "--background") return 3;
            bool first;
            using (Mutex mutex = new Mutex(true, Prefix + "instance", out first))
            {
                if (!first)
                {
                    if (mode != "--background")
                        for (int i = 0; i < 10 && !Signal("show"); i++) Thread.Sleep(100);
                    return 0;
                }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                    { MessageBox.Show("操作未完成：" + e.Exception.Message, "音箱静音保活", MessageBoxButtons.OK, MessageBoxIcon.Error); };
                    using (TrayApp context = new TrayApp(mode == "--background")) Application.Run(context);
                    return 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法启动：" + ex.Message, "音箱静音保活", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        static bool Signal(string name)
        {
            try { using (EventWaitHandle handle = EventWaitHandle.OpenExisting(Prefix + name)) return handle.Set(); }
            catch (WaitHandleCannotBeOpenedException) { return false; }
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        readonly AudioEngine engine;
        readonly NotifyIcon tray;
        readonly ContextMenu menu;
        readonly MenuItem statusItem, deviceItem, pauseItem, startupItem;
        readonly System.Windows.Forms.Timer timer;
        readonly EventWaitHandle showSignal, pauseSignal, resumeSignal, exitSignal;
        readonly Icon activeIcon, pausedIcon;
        readonly Control dispatcher;
        StatusWindow window;
        AudioDeviceChoice target;
        bool closed;

        internal TrayApp(bool background)
        {
            dispatcher = new Control();
            // Background startup also needs a handle for deferred UI callbacks.
            IntPtr dispatcherHandle = dispatcher.Handle;
            activeIcon = LoadIcon("speaker.ico");
            pausedIcon = LoadIcon("speaker-paused.ico");
            showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Prefix + "show");
            pauseSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Prefix + "pause");
            resumeSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Prefix + "resume");
            exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.Prefix + "exit");
            target = TargetSetting.Load();
            engine = new AudioEngine(target.Id, target.Name);
            statusItem = new MenuItem("正在启动…") { Enabled = false };
            deviceItem = new MenuItem("正在查找播放设备…") { Enabled = false };
            pauseItem = new MenuItem("暂停保活(&P)", delegate { TogglePause(); });
            startupItem = new MenuItem("开机自启(&A)", delegate { ChangeStartup(!ReadStartup()); });
            menu = new ContextMenu(new MenuItem[] {
                statusItem, deviceItem, new MenuItem("-"),
                new MenuItem("运行状态(&S)…", delegate { QueueUi(ShowWindow); }) { DefaultItem = true },
                new MenuItem("选择保活音箱(&D)…", delegate { QueueUi(ChooseTarget); }),
                pauseItem, startupItem, new MenuItem("-"),
                new MenuItem("退出(&X)", delegate { ExitThread(); })
            });
            menu.Popup += delegate { UpdateUi(true); };
            tray = new NotifyIcon { Icon = activeIcon, Text = "音箱静音保活", ContextMenu = menu, Visible = true };
            tray.DoubleClick += delegate { QueueUi(ShowWindow); };
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.SessionEnding += SessionEnding;
            startupItem.Checked = ReadStartup();
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += Tick;
            timer.Start();
            if (!background) ShowWindow();
        }
        static Icon LoadIcon(string resource)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (Icon icon = new Icon(stream)) return (Icon)icon.Clone();
        }
        void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) engine.Rebuild(); }
        void SessionEnding(object sender, SessionEndingEventArgs e) { ExitThread(); }
        void Tick(object sender, EventArgs args)
        {
            if (exitSignal.WaitOne(0)) { ExitThread(); return; }
            if (pauseSignal.WaitOne(0)) engine.SetPaused(true);
            if (resumeSignal.WaitOne(0)) engine.SetPaused(false);
            if (showSignal.WaitOne(0)) QueueUi(ShowWindow);
            UpdateUi(window != null && window.Visible);
        }
        void TogglePause() { engine.SetPaused(!engine.Paused); UpdateUi(); }
        void ChooseTarget()
        {
            try
            {
                using (TargetPicker picker = new TargetPicker(TargetSetting.Devices(target), target.Id))
                {
                    picker.Icon = activeIcon;
                    if (picker.ShowDialog() != DialogResult.OK || picker.Selected == null) return;
                    TargetSetting.Save(picker.Selected);
                    target = picker.Selected;
                    engine.SetTarget(target.Id, target.Name);
                }
            }
            catch (Exception ex) { MessageBox.Show("无法更改保活音箱：" + ex.Message, "音箱静音保活", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            UpdateUi();
        }
        bool ReadStartup()
        {
            try { return StartupSetting.Enabled; }
            catch { return false; }
        }
        void ChangeStartup(bool enabled)
        {
            try { StartupSetting.Set(enabled); }
            catch (Exception ex) { MessageBox.Show("无法更改开机自启：" + ex.Message, "音箱静音保活", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            UpdateUi(true);
        }
        internal static string StateText(AudioSnapshot snapshot)
        {
            switch (snapshot.State)
            {
                case "Running": return "静音流正在运行";
                case "Paused": return "已暂停";
                case "Waiting": return "等待音箱连接";
                case "SelectTarget": return "请选择保活音箱";
                case "Stopped": return "已退出";
                default: return "正在启动";
            }
        }
        void UpdateUi(bool refreshStartup = false)
        {
            AudioSnapshot snapshot = engine.Snapshot;
            string text = StateText(snapshot);
            statusItem.Text = text;
            deviceItem.Text = snapshot.Device.Replace("&", "&&");
            pauseItem.Text = engine.Paused ? "继续保活(&P)" : "暂停保活(&P)";
            if (refreshStartup) startupItem.Checked = ReadStartup();
            tray.Text = "音箱静音保活 · " + text;
            Icon desired = snapshot.State == "Running" ? activeIcon : pausedIcon;
            if (!object.ReferenceEquals(tray.Icon, desired)) tray.Icon = desired;
            if (window != null && !window.IsDisposed && window.Visible) window.RefreshState(snapshot, engine.Paused, startupItem.Checked);
        }
        void QueueUi(Action action)
        {
            if (closed) return;
            // Let the native tray menu finish dismissing before activating a form.
            dispatcher.BeginInvoke((MethodInvoker)delegate { if (!closed) action(); });
        }
        void ShowWindow()
        {
            if (window == null || window.IsDisposed)
                window = new StatusWindow(activeIcon, TogglePause, ChangeStartup, ChooseTarget);
            UpdateUi(true);
            window.RefreshState(engine.Snapshot, engine.Paused, startupItem.Checked);
            if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
            window.Show();
            window.BringToFront();
            window.Activate();
        }
        protected override void ExitThreadCore()
        {
            Cleanup();
            base.ExitThreadCore();
        }
        void Cleanup()
        {
            if (closed) return;
            closed = true;
            dispatcher.Dispose();
            timer.Stop(); timer.Dispose();
            SystemEvents.PowerModeChanged -= PowerChanged;
            SystemEvents.SessionEnding -= SessionEnding;
            engine.Dispose();
            tray.Visible = false; tray.Dispose(); menu.Dispose();
            if (window != null) window.Dispose();
            activeIcon.Dispose(); pausedIcon.Dispose();
            showSignal.Dispose(); pauseSignal.Dispose(); resumeSignal.Dispose(); exitSignal.Dispose();
        }
        protected override void Dispose(bool disposing) { if (disposing) Cleanup(); base.Dispose(disposing); }
    }

    internal sealed class StatusWindow : Form
    {
        readonly Label status, device, duration, detail;
        readonly Button pause;
        readonly CheckBox startup;
        bool updating;
        internal StatusWindow(Icon icon, Action togglePause, Action<bool> changeStartup, Action chooseTarget = null)
        {
            Text = "音箱静音保活"; Icon = icon;
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(460, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            status = MakeLabel("正在启动", 22, 20, 418, 30);
            status.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            Controls.Add(MakeLabel("保活目标音箱", 22, 65, 418, 21));
            device = MakeLabel("正在查找…", 22, 91, 418, 43);
            duration = MakeLabel("", 22, 138, 418, 22);
            detail = MakeLabel("", 22, 169, 418, 46);
            startup = new CheckBox { Text = "登录 Windows 后自动启动", Location = new Point(22, 226), Size = new Size(390, 25), UseVisualStyleBackColor = true };
            startup.CheckedChanged += delegate { if (!updating) changeStartup(startup.Checked); };
            Controls.Add(startup);
            pause = new Button { Text = "暂停保活", Location = new Point(218, 270), Size = new Size(104, 30), UseVisualStyleBackColor = true };
            pause.Click += delegate { togglePause(); };
            Button hide = new Button { Text = "收起到托盘", Location = new Point(333, 270), Size = new Size(104, 30), UseVisualStyleBackColor = true };
            hide.Click += delegate { Hide(); };
            Button choose = new Button { Text = "选择音箱…", Location = new Point(22, 270), Size = new Size(110, 30), UseVisualStyleBackColor = true, Enabled = chooseTarget != null };
            choose.Click += delegate { if (chooseTarget != null) chooseTarget(); };
            Controls.Add(choose); Controls.Add(pause); Controls.Add(hide);
            AcceptButton = hide; CancelButton = hide;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        }
        Label MakeLabel(string text, int x, int y, int width, int height)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), AutoEllipsis = true, UseMnemonic = false };
            Controls.Add(label); return label;
        }
        internal void RefreshState(AudioSnapshot snapshot, bool paused, bool autoStart)
        {
            status.Text = TrayApp.StateText(snapshot);
            device.Text = snapshot.Device;
            TimeSpan elapsed = TimeSpan.FromSeconds(snapshot.RunningSeconds);
            duration.Text = snapshot.State == "Running" ? "本次连续运行：" + (int)elapsed.TotalHours + " 时 " + elapsed.Minutes + " 分 " + elapsed.Seconds + " 秒" : "固定保活所选音箱，不随默认输出切换";
            detail.Text = snapshot.Detail;
            pause.Text = paused ? "继续保活" : "暂停保活";
            updating = true; startup.Checked = autoStart; updating = false;
        }
    }
}
