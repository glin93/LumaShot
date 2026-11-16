using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LumaShot
{
    public class ShortcutBinding
    {
        public uint Modifiers,
            KeyCode;
        public string Text;

        public static ShortcutBinding Parse(string text, string platform)
        {
            if (platform != "windows" && platform != "macos")
                throw new IOException("未知平台。");
            string[] parts = (text ?? "").Split('+').Select(x => x.Trim()).ToArray();
            if (parts.Length == 0 || parts.Any(x => x.Length == 0))
                throw new IOException("请输入有效的快捷键。");
            uint mods = 0;
            var labels = new List<string>();
            foreach (string part in parts.Take(parts.Length - 1))
            {
                string p = part.ToLowerInvariant();
                uint bit =
                    p == "ctrl" || p == "control" ? 2u
                    : p == "alt" || p == "option" ? 1u
                    : p == "shift" ? 4u
                    : p == "win" || p == "cmd" || p == "command" ? 8u
                    : 0u;
                if (bit == 0 || (mods & bit) != 0)
                    throw new IOException("快捷键修饰键无效或重复。");
                if (platform == "windows" && (p == "cmd" || p == "command" || p == "option"))
                    throw new IOException("Windows 使用 Ctrl、Alt、Shift 或 Win。");
                if (platform == "macos" && p == "win")
                    throw new IOException("macOS 使用 Cmd、Ctrl、Option 或 Shift。");
                mods |= bit;
            }
            string last = parts.Last();
            Key key;
            if (last.Length == 1 && char.IsDigit(last[0]))
                key = (Key)Enum.Parse(typeof(Key), "D" + last);
            else if (!Enum.TryParse<Key>(last, true, out key))
                throw new IOException("无法识别按键：" + last);
            if (
                key == Key.None
                || key == Key.System
                || key == Key.ImeProcessed
                || key == Key.DeadCharProcessed
                || key >= Key.LeftShift && key <= Key.RightAlt
                || key == Key.LWin
                || key == Key.RWin
                || key == Key.Escape
                || key == Key.Tab
            )
                throw new IOException("请选择一个完整的组合键。");
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk <= 0 || vk > 254 || (mods & 11) == 0 && !(key >= Key.F1 && key <= Key.F24))
                throw new IOException(
                    "请使用 Ctrl、Alt、Win（Mac 为 Cmd/Option），或功能键 F1–F24。"
                );
            if ((mods & 2) != 0)
                labels.Add("Ctrl");
            if ((mods & 1) != 0)
                labels.Add(platform == "macos" ? "Option" : "Alt");
            if ((mods & 4) != 0)
                labels.Add("Shift");
            if ((mods & 8) != 0)
                labels.Add(platform == "macos" ? "Cmd" : "Win");
            labels.Add(
                key >= Key.D0 && key <= Key.D9
                    ? ((int)key - (int)Key.D0).ToString()
                    : key.ToString()
            );
            return new ShortcutBinding
            {
                Modifiers = mods,
                KeyCode = (uint)vk,
                Text = string.Join("+", labels),
            };
        }
    }

    public class Preferences
    {
        public static readonly string[] Actions =
        {
            "region",
            "window",
            "fullscreen",
            "recordRegion",
            "recordWindow",
            "recordFullscreen",
            "quickHistory",
            "library",
        };
        public static readonly string[] Names =
        {
            "自由截图",
            "窗口截图",
            "全屏截图",
            "自由视频录屏",
            "窗口视频录屏",
            "全屏录屏",
            "快速历史",
            "资料库",
        };
        public static readonly string[] EngineActions =
        {
            "RectangleRegion",
            "ActiveWindow",
            "PrintScreen",
            "ScreenRecorder",
            "ScreenRecorderActiveWindow",
            "ScreenRecorderCustomRegion",
        };
        public Dictionary<string, string> Values = new Dictionary<string, string>();
        readonly string path;

        public Preferences(string root)
        {
            path = Path.Combine(root, "preferences.json");
            if (File.Exists(path))
            {
                var data = Backend
                    .Json()
                    .Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                foreach (var p in data)
                {
                    string[] parts = p.Key.Split('.');
                    if (
                        parts.Length == 3
                        && parts[0] == "hotkeys"
                        && (parts[1] == "windows" || parts[1] == "macos")
                        && Actions.Contains(parts[2])
                    )
                        Values[p.Key] = ShortcutBinding.Parse(p.Value, parts[1]).Text;
                    else if (IsCaptureKey(p.Key, p.Value))
                        Values[p.Key] = p.Value;
                }
            }
        }

        public static bool IsCaptureKey(string key, string value)
        {
            return new[] { "recording.microphone", "recording.systemAudio" }.Contains(key)
                    && (value == "true" || value == "false")
                || new[] { "image", "video" }.Any(k =>
                    key == "capture." + k + ".copy" && (value == "true" || value == "false")
                    || key == "capture." + k + ".open"
                        && new[] { "none", "library", "folder" }.Contains(value)
                );
        }

        public bool Microphone
        {
            get
            {
                string value;
                return Values.TryGetValue("recording.microphone", out value) && value == "true";
            }
        }
        public bool SystemAudio
        {
            get
            {
                string value;
                return !Values.TryGetValue("recording.systemAudio", out value) || value == "true";
            }
        }

        public bool CaptureCopy(string kind)
        {
            string value;
            return Values.TryGetValue("capture." + kind + ".copy", out value)
                ? value == "true"
                : kind == "image";
        }

        public string CaptureOpen(string kind)
        {
            string value;
            return Values.TryGetValue("capture." + kind + ".open", out value) ? value : "none";
        }

        public static string Default(string platform, string action)
        {
            int index = Array.IndexOf(Actions, action);
            if (index < 0)
                throw new IOException("未知快捷键操作。");
            return (platform == "macos" ? "Shift+Cmd+" : "Ctrl+Shift+")
                + new[] { "2", "4", "3", "5", "6", "7", "H", "L" }[index];
        }

        public string Get(string platform, string action)
        {
            string value;
            return Values.TryGetValue("hotkeys." + platform + "." + action, out value)
                ? value
                : Default(platform, action);
        }

        public Dictionary<string, string> ForPlatform(string platform)
        {
            return Actions.ToDictionary(a => a, a => Get(platform, a));
        }

        public static Dictionary<string, string> Validate(
            string platform,
            Dictionary<string, string> values
        )
        {
            var result = Actions.ToDictionary(
                a => a,
                a => ShortcutBinding.Parse(values[a], platform).Text
            );
            if (result.Values.Distinct().Count() != Actions.Length)
                throw new IOException("不同操作不能使用相同快捷键。");
            return result;
        }

        public void Save()
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Backend.Json().Serialize(Values));
            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak");
            else
                File.Move(tmp, path);
        }
    }

    public partial class MainUI
    {
        Preferences preferences;
        string shortcutNotice = "";

        string Hotkey(string action)
        {
            return preferences.Get("windows", action);
        }

        void UnregisterBindings()
        {
            if (handle != IntPtr.Zero)
                for (int i = 1; i <= Preferences.Actions.Length; i++)
                    Native.UnregisterHotKey(handle, i);
        }

        string RegisterBindings(Dictionary<string, string> values)
        {
            UnregisterBindings();
            var failed = new List<string>();
            if (handle == IntPtr.Zero || demo)
                return "";
            for (int i = 0; i < Preferences.Actions.Length; i++)
            {
                try
                {
                    var b = ShortcutBinding.Parse(values[Preferences.Actions[i]], "windows");
                    if (!Native.RegisterHotKey(handle, i + 1, b.Modifiers | 0x4000, b.KeyCode))
                        failed.Add(Preferences.Names[i] + " " + b.Text);
                }
                catch (Exception e)
                {
                    failed.Add(e.Message);
                }
            }
            return failed.Count == 0 ? "" : "快捷键未启用：" + string.Join("；", failed);
        }

        void ApplyBindings()
        {
            shortcutNotice = RegisterBindings(preferences.ForPlatform("windows"));
            UpdateTray();
            Get<Button>("Capture").ToolTip = Hotkey("region");
            Get<Button>("QuickHistory").ToolTip = Hotkey("quickHistory");
            Get<TextBlock>("ShortcutHint").Text = Hotkey("region") + "  截图";
            if (shortcutNotice.Length > 0)
                Say(shortcutNotice);
        }
    }
}
