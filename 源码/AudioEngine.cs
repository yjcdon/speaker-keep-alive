using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpeakerKeepAlive
{
    public sealed class AudioSnapshot
    {
        public string State = "Starting";
        public string Device = "尚未选择保活音箱";
        public string DeviceId = "";
        public string Detail = "";
        public long SilentFrames;
        public long SilentPackets;
        public long StreamStarts;
        public long Errors;
        public int SampleRate;
        public int Channels;
        public double RunningSeconds;
        public AudioSnapshot Copy() { return (AudioSnapshot)MemberwiseClone(); }
    }

    public sealed class AudioEngine : IDisposable
    {
        readonly object sync = new object();
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly AutoResetEvent changed = new AutoResetEvent(false);
        readonly AutoResetEvent ready = new AutoResetEvent(false);
        readonly AudioSnapshot state = new AudioSnapshot();
        readonly Thread worker;
        volatile bool paused;
        volatile bool disposed;
        long generation;
        string targetId;
        string targetName;

        public AudioEngine(string deviceId = "", string deviceName = "尚未选择保活音箱")
        {
            targetId = deviceId ?? "";
            targetName = deviceName ?? "保活音箱";
            state.Device = targetName;
            state.DeviceId = targetId;
            worker = new Thread(Run) { IsBackground = true, Name = "静音音频" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        public AudioSnapshot Snapshot { get { lock (sync) { return state.Copy(); } } }
        public bool Paused { get { return paused; } }
        public void SetTarget(string deviceId, string deviceName)
        {
            lock (sync)
            {
                targetId = deviceId ?? "";
                targetName = deviceName ?? "保活音箱";
                if (paused || state.State == "SelectTarget") { state.Device = targetName; state.DeviceId = targetId; }
                Interlocked.Increment(ref generation);
            }
            if (!disposed) changed.Set();
        }
        public void SetPaused(bool value) { paused = value; Rebuild(); }
        public void Rebuild() { if (!disposed) { Interlocked.Increment(ref generation); changed.Set(); } }
        void SetState(string value, string detail)
        {
            lock (sync) { state.State = value; state.Detail = detail; state.RunningSeconds = 0; }
        }
        void Run()
        {
            while (!stop.WaitOne(0))
            {
                if (paused)
                {
                    SetState("Paused", "已暂停，不再发送静音音频。");
                    WaitHandle.WaitAny(new WaitHandle[] { stop, changed }, 2000);
                    continue;
                }
                bool selected;
                lock (sync) { selected = !string.IsNullOrEmpty(targetId); }
                if (!selected)
                {
                    SetState("SelectTarget", "请从托盘菜单选择需要保活的音箱。");
                    WaitHandle.WaitAny(new WaitHandle[] { stop, changed }, 2000);
                    continue;
                }
                try { Stream(); }
                catch (Exception ex)
                {
                    if (stop.WaitOne(0)) break;
                    lock (sync) { state.Errors++; state.Device = targetName; state.DeviceId = targetId; }
                    SetState("Waiting", FriendlyError(ex));
                    WaitHandle.WaitAny(new WaitHandle[] { stop, changed }, 2000);
                }
            }
            SetState("Stopped", "已退出。");
        }
        static string FriendlyError(Exception ex)
        {
            uint code = unchecked((uint)ex.HResult);
            string message;
            if (code == 0x80070490 || code == 0x88890004) message = "等待所选音箱连接，将自动重试。";
            else if (code == 0x8889000A) message = "音频设备被其他程序独占，将自动重试。";
            else if (code == 0x88890010) message = "Windows 音频服务不可用，将自动重试。";
            else message = "音频暂时不可用，将自动重试。";
            return message + " (0x" + code.ToString("X8") + ")";
        }
        void Stream()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioClient client = null;
            IAudioRenderClient render = null;
            IntPtr format = IntPtr.Zero;
            bool started = false;
            long currentGeneration;
            string selectedId;
            lock (sync)
            {
                currentGeneration = Interlocked.Read(ref generation);
                selectedId = targetId;
                state.Device = targetName;
                state.DeviceId = targetId;
            }
            try
            {
                SetState("Starting", "正在连接所选保活音箱…");
                enumerator = AudioNative.Enumerator();
                // Always open the chosen endpoint. Never fall back to the default output.
                AudioNative.Check(enumerator.GetDevice(selectedId, out device));
                uint active;
                AudioNative.Check(device.GetState(out active));
                if (active != 1) throw new COMException("Target is disconnected", unchecked((int)0x88890004));
                string id;
                AudioNative.Check(device.GetId(out id));
                string name = AudioNative.DeviceName(device);
                object result;
                Guid iid = AudioNative.AudioClientId;
                AudioNative.Check(device.Activate(ref iid, 23, IntPtr.Zero, out result));
                client = (IAudioClient)result;
                AudioNative.Check(client.GetMixFormat(out format));
                int sampleRate = Marshal.ReadInt32(format, 4);
                int channels = (ushort)Marshal.ReadInt16(format, 2);
                Guid session = AudioNative.SessionId;
                // Shared, event-driven, no persistent app volume/mute preferences.
                AudioNative.Check(client.Initialize(0, 0x00040000 | 0x00080000, 2000000, 0, format, ref session));
                uint size;
                AudioNative.Check(client.GetBufferSize(out size));
                AudioNative.Check(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()));
                iid = AudioNative.RenderClientId;
                AudioNative.Check(client.GetService(ref iid, out result));
                render = (IAudioRenderClient)result;
                WriteSilence(render, size);
                AudioNative.Check(client.Start());
                started = true;
                Stopwatch elapsed = Stopwatch.StartNew();
                double lastDeviceCheck = -2;
                double lastWake = 0;
                lock (sync)
                {
                    state.State = "Running";
                    state.Detail = "正在发送全零静音；是否防止音箱关机需要实测。";
                    state.Device = name; state.DeviceId = id;
                    state.SampleRate = sampleRate; state.Channels = channels;
                    state.StreamStarts++;
                }
                WaitHandle[] signals = { stop, changed, ready };
                while (!stop.WaitOne(0) && !paused && currentGeneration == Interlocked.Read(ref generation))
                {
                    int signal = WaitHandle.WaitAny(signals, 250);
                    if (signal == 0 || paused || currentGeneration != Interlocked.Read(ref generation)) break;
                    double now = elapsed.Elapsed.TotalSeconds;
                    // Reopen after a long scheduling gap (including suspend/resume).
                    if (now - lastWake > 3) break;
                    lastWake = now;
                    uint padding;
                    AudioNative.Check(client.GetCurrentPadding(out padding));
                    if (padding > size) throw new InvalidOperationException("Invalid audio padding");
                    if (padding < size) WriteSilence(render, size - padding);
                    if (now - lastDeviceCheck >= 1)
                    {
                        lastDeviceCheck = now;
                        lock (sync) { state.RunningSeconds = now; }
                        AudioNative.Check(device.GetState(out active));
                        if (active != 1) throw new COMException("Target is disconnected", unchecked((int)0x88890004));
                    }
                }
            }
            finally
            {
                if (started && client != null) client.Stop();
                AudioNative.Release(render);
                AudioNative.Release(client);
                AudioNative.Release(device);
                AudioNative.Release(enumerator);
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
            }
        }
        void WriteSilence(IAudioRenderClient render, uint frames)
        {
            if (frames == 0) return;
            IntPtr buffer;
            AudioNative.Check(render.GetBuffer(frames, out buffer));
            // This is the only output path. Windows fills every frame with silence.
            // No PCM tone generator, volume setter, or non-silent ReleaseBuffer exists.
            AudioNative.Check(render.ReleaseBuffer(frames, AudioNative.Silent));
            lock (sync) { state.SilentFrames += frames; state.SilentPackets++; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; stop.Set(); changed.Set();
            // Do not dispose wait handles still in use if a faulty driver blocks COM.
            if (worker.Join(3000)) { ready.Dispose(); changed.Dispose(); stop.Dispose(); }
        }
    }
}
