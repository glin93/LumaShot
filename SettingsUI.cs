using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LumaShot
{
    public partial class MainUI
    {
        Window settingsWindow;
        ContentControl settingsPage;
        Dictionary<string, Button> settingsNavigation;
        readonly Dictionary<string, FrameworkElement> settingsPages =
            new Dictionary<string, FrameworkElement>();

        Window DialogShell(string title, double width, double height, out Grid body)
        {
            var area = PointerWorkArea();
            var win = new Window
            {
                Title = "LumaShot · " + title,
                Owner = Window.IsVisible ? Window : null,
                Width = Math.Min(width, area.Width - 36),
                Height = Math.Min(height, area.Height - 36),
                MinWidth = Math.Min(700, area.Width - 36),
                MinHeight = Math.Min(520, area.Height - 36),
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.CanResize,
                AllowsTransparency = false,
                Background = B("#F6F7FB"),
                FontFamily = Window.FontFamily,
                FontSize = 14,
                Foreground = B("#292B38"),
                Resources = Window.Resources,
                UseLayoutRounding = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Icon = Window.Icon,
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid
            {
                Background = Brushes.Transparent,
                Margin = new Thickness(18, 0, 12, 0),
            };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = T("LumaShot  /  " + title, 12, "#6E7182");
            name.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(name);
            var buttons = CaptionButtons(win);
            Grid.SetColumn(buttons, 1);
            header.Children.Add(buttons);
            root.Children.Add(header);
            body = new Grid();
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            win.Content = root;
            ApplyChrome(win, 46);
            win.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && !e.Handled)
                {
                    win.Close();
                    e.Handled = true;
                }
            };
            return win;
        }

        StackPanel PageHeader(string title, string subtitle)
        {
            var p = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
            var h = T(title, 27, "#272936");
            h.FontWeight = FontWeights.SemiBold;
            p.Children.Add(h);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var t = Help(subtitle);
                t.Margin = new Thickness(0, 8, 0, 0);
                p.Children.Add(t);
            }
            return p;
        }

        Border SettingCard(UIElement content)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = B("#E5E7EF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 12),
                Child = content,
            };
        }

        StackPanel Section(string title, string description)
        {
            var p = new StackPanel();
            var t = T(title, 16, "#303342");
            t.FontWeight = FontWeights.SemiBold;
            p.Children.Add(t);
            if (!string.IsNullOrEmpty(description))
            {
                var d = Help(description);
                d.Margin = new Thickness(0, 6, 0, 12);
                p.Children.Add(d);
            }
            return p;
        }

        FrameworkElement SettingRow(string title, string description, FrameworkElement control)
        {
            var row = new Grid { Margin = new Thickness(0, 12, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var words = new StackPanel
            {
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            words.Children.Add(T(title, 14, "#3D4051"));
            if (!string.IsNullOrEmpty(description))
            {
                var sub = Help(description);
                sub.Margin = new Thickness(0, 3, 0, 0);
                words.Children.Add(sub);
            }
            row.Children.Add(words);
            control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 1);
            row.Children.Add(control);
            return row;
        }

        CheckBox Switch(bool value, string label)
        {
            var box = new CheckBox
            {
                IsChecked = value,
                Style = (Style)Window.FindResource("SettingSwitch"),
            };
            System.Windows.Automation.AutomationProperties.SetName(box, label);
            RoutedEventHandler animate = (sender, args) =>
            {
                if (!box.IsLoaded || !SystemParameters.ClientAreaAnimation)
                    return;
                box.ApplyTemplate();
                var knob = box.Template.FindName("Knob", box) as FrameworkElement;
                if (knob == null)
                    return;
                double end = box.IsChecked == true ? 18 : 0;
                var transform = new TranslateTransform(end, 0);
                knob.RenderTransform = transform;
                transform.BeginAnimation(
                    TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(
                        18 - end,
                        end,
                        TimeSpan.FromMilliseconds(120)
                    )
                    {
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop,
                    }
                );
            };
            box.Checked += animate;
            box.Unchecked += animate;
            return box;
        }

        void Settings()
        {
            OpenSettings("capture");
        }

        void ShortcutSettings()
        {
            OpenSettings("shortcuts");
        }

        void CaptureSettings()
        {
            OpenSettings("capture");
        }

        void OpenSettings(string section, bool show = true)
        {
            if (settingsWindow == null)
            {
                Grid body;
                settingsWindow = DialogShell("设置", 980, 770, out body);
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) });
                body.ColumnDefinitions.Add(new ColumnDefinition());
                var sidebar = new DockPanel { Margin = new Thickness(16, 18, 14, 22) };
                var caption = T("设置", 24, "#292B38");
                caption.FontWeight = FontWeights.SemiBold;
                caption.Margin = new Thickness(14, 0, 0, 26);
                DockPanel.SetDock(caption, Dock.Top);
                sidebar.Children.Add(caption);
                var version = Help("LumaShot 0.1.4");
                version.Margin = new Thickness(14, 15, 0, 0);
                DockPanel.SetDock(version, Dock.Bottom);
                sidebar.Children.Add(version);
                var nav = new StackPanel();
                sidebar.Children.Add(nav);
                settingsNavigation = new Dictionary<string, Button>();
                var keys = new[] { "capture", "shortcuts", "ai", "cloud", "library", "about" };
                var labels = new[]
                {
                    "截图与录屏",
                    "快捷键",
                    "AI 服务",
                    "云同步",
                    "资料库",
                    "关于",
                };
                var glyphs = new[] { "\uE722", "\uE765", "\uE8F2", "\uE753", "\uE8B7", "\uE946" };
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i];
                    var content = new StackPanel { Orientation = Orientation.Horizontal };
                    var icon = Icons.Create(glyphs[i], 18);
                    icon.Margin = new Thickness(0, 0, 12, 0);
                    content.Children.Add(icon);
                    content.Children.Add(
                        new TextBlock
                        {
                            Text = labels[i],
                            VerticalAlignment = VerticalAlignment.Center,
                        }
                    );
                    var button = Btn("", () => SelectSettings(key));
                    button.Content = content;
                    button.Background = Brushes.Transparent;
                    button.BorderThickness = new Thickness(0);
                    button.Padding = new Thickness(14, 13, 8, 13);
                    button.HorizontalContentAlignment = HorizontalAlignment.Left;
                    button.Margin = new Thickness(0, 0, 0, 5);
                    System.Windows.Automation.AutomationProperties.SetName(button, labels[i]);
                    nav.Children.Add(button);
                    settingsNavigation[key] = button;
                }
                body.Children.Add(sidebar);
                settingsPage = new ContentControl
                {
                    Margin = new Thickness(22, 14, 28, 22),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                };
                Grid.SetColumn(settingsPage, 1);
                body.Children.Add(settingsPage);
                settingsWindow.Closed += (s, e) =>
                {
                    settingsWindow = null;
                    settingsPage = null;
                    settingsPages.Clear();
                    ApplyBindings();
                };
            }
            SelectSettings(section);
            if (show)
            {
                settingsWindow.Show();
                settingsWindow.Activate();
            }
        }

        void SelectSettings(string section)
        {
            ApplyBindings();
            foreach (var p in settingsNavigation)
            {
                p.Value.Background = B(p.Key == section ? "#EAE6F8" : "#F6F7FB");
                p.Value.Foreground = B(p.Key == section ? "#7057CF" : "#55596B");
                p.Value.FontWeight = p.Key == section ? FontWeights.SemiBold : FontWeights.Normal;
            }
            FrameworkElement page;
            if (!settingsPages.TryGetValue(section, out page))
            {
                page =
                    section == "capture" ? CapturePage()
                    : section == "shortcuts" ? ShortcutPage()
                    : section == "ai" || section == "cloud"
                        ? ConnectionsPage(settingsWindow, section)
                    : LibrarySettingsPage(section == "about");
                settingsPages[section] = page;
            }
            settingsPage.Content = page;
        }

        FrameworkElement ScrollPage(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 8, 0),
            };
        }

        FrameworkElement CapturePage()
        {
            var page = new StackPanel();
            page.Children.Add(PageHeader("截图与录屏", "选择完成后的操作和录屏声音。"));
            foreach (string item in new[] { "image", "video" })
            {
                string kind = item;
                var card = Section(kind == "image" ? "截图完成后" : "录屏完成后", null);
                var copy = Switch(
                    preferences.CaptureCopy(kind),
                    kind == "image" ? "截图自动复制" : "录屏自动复制"
                );
                copy.Click += (s, e) =>
                {
                    preferences.Values["capture." + kind + ".copy"] =
                        copy.IsChecked == true ? "true" : "false";
                    preferences.Save();
                };
                card.Children.Add(
                    SettingRow(
                        "自动复制",
                        kind == "image" ? "复制图片到剪贴板" : "复制视频文件到剪贴板",
                        copy
                    )
                );
                var target = new ComboBox
                {
                    ItemsSource = new[] { "不打开", "资料库", "所在文件夹" },
                    SelectedIndex =
                        preferences.CaptureOpen(kind) == "library" ? 1
                        : preferences.CaptureOpen(kind) == "folder" ? 2
                        : 0,
                    Width = 155,
                    Height = 38,
                };
                target.SelectionChanged += (s, e) =>
                {
                    preferences.Values["capture." + kind + ".open"] = new[]
                    {
                        "none",
                        "library",
                        "folder",
                    }[target.SelectedIndex];
                    preferences.Save();
                };
                card.Children.Add(SettingRow("自动打开", "完成后显示的位置", target));
                page.Children.Add(SettingCard(card));
            }
            var audio = Section("录屏声音", null);
            var microphone = Switch(preferences.Microphone, "录制麦克风");
            microphone.Click += (s, e) =>
            {
                preferences.Values["recording.microphone"] =
                    microphone.IsChecked == true ? "true" : "false";
                preferences.Save();
            };
            audio.Children.Add(SettingRow("麦克风", "录制你的声音", microphone));
            var system = Switch(preferences.SystemAudio, "录制机内声音");
            system.Click += (s, e) =>
            {
                preferences.Values["recording.systemAudio"] =
                    system.IsChecked == true ? "true" : "false";
                preferences.Save();
            };
            audio.Children.Add(SettingRow("系统声音", "录制电脑正在播放的声音", system));
            page.Children.Add(SettingCard(audio));
            page.Children.Add(Help("更改自动保存"));
            return ScrollPage(page);
        }

        FrameworkElement ShortcutPage()
        {
            var page = new StackPanel();
            page.Children.Add(PageHeader("快捷键", "同平台设备共用一套快捷键，随配置同步。"));
            var platform = new ComboBox
            {
                ItemsSource = new[] { "Windows", "macOS" },
                SelectedIndex = 0,
                Width = 160,
                Height = 38,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 18),
            };
            page.Children.Add(platform);
            var rows = new StackPanel();
            page.Children.Add(SettingCard(rows));
            var message = Help("点击输入框后按下组合键。");
            var inputs = new Dictionary<string, TextBox>();
            var drafts = new Dictionary<string, Dictionary<string, string>>();
            string editing = "windows";
            Action render = () =>
            {
                if (inputs.Count > 0)
                    drafts[editing] = inputs.ToDictionary(p => p.Key, p => p.Value.Text);
                rows.Children.Clear();
                inputs.Clear();
                editing = platform.SelectedIndex == 0 ? "windows" : "macos";
                for (int i = 0; i < Preferences.Actions.Length; i++)
                {
                    string action = Preferences.Actions[i];
                    var box = new TextBox
                    {
                        Text = drafts.ContainsKey(editing)
                            ? drafts[editing][action]
                            : preferences.Get(editing, action),
                        Width = 220,
                        Height = 38,
                        Padding = new Thickness(10, 5, 10, 5),
                    };
                    System.Windows.Automation.AutomationProperties.SetName(
                        box,
                        Preferences.Names[i]
                    );
                    box.GotKeyboardFocus += (s, e) => UnregisterBindings();
                    box.LostKeyboardFocus += (s, e) => ApplyBindings();
                    box.PreviewKeyDown += (s, e) =>
                    {
                        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                        var mods = Keyboard.Modifiers;
                        if (
                            key == Key.Tab
                            || key == Key.Escape
                            || key >= Key.LeftShift && key <= Key.RightAlt
                            || key == Key.LWin
                            || key == Key.RWin
                        )
                            return;
                        if (mods == ModifierKeys.None && !(key >= Key.F1 && key <= Key.F24))
                            return;
                        box.Text =
                            ((mods & ModifierKeys.Control) != 0 ? "Ctrl+" : "")
                            + (
                                (mods & ModifierKeys.Alt) != 0
                                    ? (editing == "macos" ? "Option+" : "Alt+")
                                    : ""
                            )
                            + ((mods & ModifierKeys.Shift) != 0 ? "Shift+" : "")
                            + (
                                (mods & ModifierKeys.Windows) != 0
                                    ? (editing == "macos" ? "Cmd+" : "Win+")
                                    : ""
                            )
                            + (
                                key >= Key.D0 && key <= Key.D9
                                    ? ((int)key - (int)Key.D0).ToString()
                                    : key.ToString()
                            );
                        box.CaretIndex = box.Text.Length;
                        e.Handled = true;
                    };
                    inputs[action] = box;
                    var row = SettingRow(Preferences.Names[i], null, box);
                    row.Margin = new Thickness(0, 2, 0, 4);
                    rows.Children.Add(row);
                }
            };
            platform.SelectionChanged += (s, e) =>
            {
                render();
                message.Text = "点击输入框后按下组合键。";
            };
            render();
            page.Children.Add(message);
            var buttons = new WrapPanel();
            buttons.Children.Add(
                Btn(
                    "保存快捷键",
                    () =>
                    {
                        try
                        {
                            var next = Preferences.Validate(
                                editing,
                                inputs.ToDictionary(p => p.Key, p => p.Value.Text)
                            );
                            if (editing == "windows")
                            {
                                string error = RegisterBindings(next);
                                if (error.Length > 0)
                                {
                                    ApplyBindings();
                                    message.Text = error;
                                    return;
                                }
                            }
                            foreach (var p in next)
                                preferences.Values["hotkeys." + editing + "." + p.Key] = p.Value;
                            preferences.Save();
                            ApplyBindings();
                            message.Text = "快捷键已保存";
                        }
                        catch (Exception ex)
                        {
                            message.Text = ex.Message;
                        }
                    },
                    true
                )
            );
            buttons.Children.Add(
                Btn(
                    "恢复默认",
                    () =>
                    {
                        foreach (string action in Preferences.Actions)
                            inputs[action].Text = Preferences.Default(editing, action);
                    }
                )
            );
            page.Children.Add(buttons);
            return ScrollPage(page);
        }

        FrameworkElement LibrarySettingsPage(bool about)
        {
            var page = new StackPanel();
            page.Children.Add(
                PageHeader(
                    about ? "关于 LumaShot" : "资料库",
                    about ? "截图、录屏与灵感收藏。" : "原图、编辑版本和历史记录保存在本机。"
                )
            );
            if (about)
            {
                var card = Section("LumaShot 0.1.4", "Windows 版");
                card.Children.Add(
                    Btn(
                        "开源项目",
                        () =>
                            Process.Start(
                                new ProcessStartInfo("https://github.com/glin93/LumaShot")
                                {
                                    UseShellExecute = true,
                                }
                            )
                    )
                );
                card.Children.Add(
                    Btn(
                        "开源许可",
                        () =>
                            Process.Start(
                                new ProcessStartInfo(
                                    Path.Combine(
                                        AppDomain.CurrentDomain.BaseDirectory,
                                        "THIRD_PARTY.md"
                                    )
                                )
                                {
                                    UseShellExecute = true,
                                }
                            )
                    )
                );
                page.Children.Add(SettingCard(card));
            }
            else
            {
                var local = Section("本地保存", null);
                var location = Help(lib.Root);
                location.TextWrapping = TextWrapping.Wrap;
                local.Children.Add(location);
                local.Children.Add(
                    Btn(
                        "打开资料库文件夹",
                        () => Process.Start("explorer.exe", "\"" + lib.Root + "\"")
                    )
                );
                page.Children.Add(SettingCard(local));
                var backup = Section("备份与恢复", "恢复时合并历史，保留已有记录。 ");
                backup.Children.Add(Btn("备份资料库…", Backup));
                backup.Children.Add(Btn("从备份恢复…", RestoreBackup));
                page.Children.Add(SettingCard(backup));
                var storage = Section("存储整理", "清理已归档的中转文件和已核对的重复副本。");
                var cleanup = AsyncButton(
                    "整理存储",
                    async () =>
                    {
                        if (engine.Busy || scanning || syncing || maintenance)
                        {
                            Say("请等待当前任务完成。");
                            return;
                        }
                        FlushEdits();
                        maintenance = true;
                        var snapshot = new Library(lib.Root);
                        try
                        {
                            await Task.Run(
                                () => StorageMaintenance.CleanRedundantFiles(snapshot, engine.Inbox)
                            );
                            Say("存储整理完成");
                        }
                        finally
                        {
                            maintenance = false;
                        }
                    }
                );
                storage.Children.Add(cleanup);
                page.Children.Add(SettingCard(storage));
                var behaviour = Section(
                    "后台运行",
                    "关闭资料库窗口后，LumaShot 保留在系统托盘。快捷键继续有效。"
                );
                page.Children.Add(SettingCard(behaviour));
            }
            return ScrollPage(page);
        }
    }
}
