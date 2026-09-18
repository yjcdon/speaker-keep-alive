using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpeakerKeepAlive
{
    public sealed class AudioDeviceChoice
    {
        public string Id;
        public string Name;
        public bool Connected;
        public override string ToString() { return Name + (Connected ? "" : "（未连接）"); }
    }

    internal static class TargetSetting
    {
        const string Key = @"Software\SpeakerKeepAlive";
        internal static AudioDeviceChoice Load()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key))
            {
                return new AudioDeviceChoice
                {
                    Id = key == null ? "" : key.GetValue("TargetDeviceId", "") as string ?? "",
                    Name = key == null ? "尚未选择保活音箱" : key.GetValue("TargetDeviceName", "保活音箱") as string ?? "保活音箱"
                };
            }
        }
        internal static void Save(AudioDeviceChoice target)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
            {
                key.SetValue("TargetDeviceId", target.Id, RegistryValueKind.String);
                key.SetValue("TargetDeviceName", target.Name, RegistryValueKind.String);
            }
        }
        internal static List<AudioDeviceChoice> Devices(AudioDeviceChoice selected)
        {
            List<AudioDeviceChoice> choices = new List<AudioDeviceChoice>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                enumerator = AudioNative.Enumerator();
                IntPtr raw;
                AudioNative.Check(enumerator.EnumAudioEndpoints(0, 1, out raw));
                try { collection = (IMMDeviceCollection)Marshal.GetObjectForIUnknown(raw); }
                finally { Marshal.Release(raw); }
                uint count;
                AudioNative.Check(collection.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        AudioNative.Check(collection.Item(i, out device));
                        string id;
                        AudioNative.Check(device.GetId(out id));
                        choices.Add(new AudioDeviceChoice { Id = id, Name = AudioNative.DeviceName(device), Connected = true });
                    }
                    catch (COMException) { /* A device can disappear while the picker opens. */ }
                    finally { AudioNative.Release(device); }
                }
            }
            finally { AudioNative.Release(collection); AudioNative.Release(enumerator); }
            if (!string.IsNullOrEmpty(selected.Id) && !choices.Exists(delegate(AudioDeviceChoice x) { return x.Id == selected.Id; }))
                choices.Insert(0, new AudioDeviceChoice { Id = selected.Id, Name = selected.Name, Connected = false });
            return choices;
        }
    }

    internal sealed class TargetPicker : Form
    {
        readonly ComboBox devices;
        internal AudioDeviceChoice Selected { get { return devices.SelectedItem as AudioDeviceChoice; } }
        internal TargetPicker(List<AudioDeviceChoice> choices, string selectedId)
        {
            Text = "选择保活音箱";
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(460, 163); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(new Label { Text = "选定后，切换到耳机也会继续保活这只音箱。", Location = new Point(18, 17), Size = new Size(425, 24) });
            devices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(18, 51), Size = new Size(424, 30), DropDownWidth = 550 };
            foreach (AudioDeviceChoice choice in choices) devices.Items.Add(choice);
            for (int i = 0; i < choices.Count; i++) if (choices[i].Id == selectedId) devices.SelectedIndex = i;
            if (devices.SelectedIndex < 0 && choices.Count > 0) devices.SelectedIndex = 0;
            Controls.Add(devices);
            Button ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(242, 111), Size = new Size(95, 30), Enabled = choices.Count > 0, UseVisualStyleBackColor = true };
            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(347, 111), Size = new Size(95, 30), UseVisualStyleBackColor = true };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
