using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace LumaShot
{
    // Small original vector set: one stroke, canvas and optical size everywhere.
    internal static class Icons
    {
        static readonly Dictionary<string, string> paths = new Dictionary<string, string>
        {
            {
                "library",
                "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z"
            },
            { "star", "M12,3 L15,9 L22,10 L17,15 L18,22 L12,18 L6,22 L7,15 L2,10 L9,9 Z" },
            { "image", "M3,4 H21 V20 H3 Z M3,16 L8,11 L13,16 L16,13 L21,18 M8,8 L8.01,8" },
            { "video", "M3,5 H16 V19 H3 Z M16,10 L22,6 V18 L16,14" },
            { "trash", "M3,6 H21 M9,6 V3 H15 V6 M5,6 L6,21 H18 L19,6 M10,10 V17 M14,10 V17" },
            {
                "settings",
                "M9,3 H15 L16,6 L19,7 L22,10 V14 L19,17 L16,18 L15,21 H9 L8,18 L5,17 L2,14 V10 L5,7 L8,6 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"
            },
            { "search", "M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 M15,15 L21,21" },
            { "close", "M6,6 L18,18 M6,18 L18,6" },
            { "plus", "M12,5 V19 M5,12 H19" },
            { "down", "M6,9 L12,15 L18,9" },
            { "left", "M15,6 L9,12 L15,18" },
            { "right", "M9,6 L15,12 L9,18" },
            { "pin", "M8,3 H16 M9,3 V9 L18,15 H6 L9,9 M12,15 V22 M15,3 V9" },
            { "edit", "M4,16 L16,4 L20,8 L8,20 L3,21 Z M13,7 L17,11" },
            { "folder", "M3,6 H9 L11,8 H21 V20 H3 Z" },
            { "play", "M7,4 L20,12 L7,20 Z" },
            { "capture", "M3,8 V3 H8 M16,3 H21 V8 M21,16 V21 H16 M8,21 H3 V16 M8,8 H16 V16 H8 Z" },
            {
                "keyboard",
                "M2,5 H22 V19 H2 Z M6,9 H7 M11,9 H12 M16,9 H18 M6,13 H7 M11,13 H12 M16,13 H18 M7,16 H17"
            },
            { "ai", "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z" },
            { "cloud", "M6,18 A5,5 0 1 1 5,8 A7,7 0 0 1 19,8 A5,5 0 0 1 19,18 Z" },
            { "info", "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,11 V17 M12,7 V7.2" },
            { "history", "M3,12 A9,9 0 1 1 6,19 M3,5 V12 H10 M12,7 V12 L16,14" },
            { "exit", "M9,3 H3 V21 H9 M10,12 H22 M17,7 L22,12 L17,17" },
            { "more", "M5,12 H5.2 M12,12 H12.2 M19,12 H19.2" },
            { "minimize", "M5,12 H19" },
            { "maximize", "M5,5 H19 V19 H5 Z" },
        };

        public static string Name(string glyph)
        {
            switch (glyph)
            {
                case "\uE711":
                    return "close";
                case "\uE718":
                    return "pin";
                case "\uE768":
                    return "play";
                case "\uE70F":
                    return "edit";
                case "\uE8B7":
                    return "folder";
                case "\uE76C":
                    return "right";
                case "\uE76B":
                    return "left";
                case "\uE722":
                    return "capture";
                case "\uE714":
                    return "video";
                case "\uE765":
                    return "keyboard";
                case "\uE8F2":
                    return "ai";
                case "\uE753":
                    return "cloud";
                case "\uE946":
                    return "info";
                case "\uE81C":
                    return "history";
                case "\uE713":
                    return "settings";
                case "\uE7E8":
                    return "exit";
                default:
                    return paths.ContainsKey(glyph) ? glyph : "more";
            }
        }

        public static FrameworkElement Create(string name, double size = 18, Brush color = null)
        {
            var path = new Path
            {
                Data = Geometry.Parse(paths[Name(name)]),
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };
            if (color != null)
                path.Stroke = color;
            else
                path.SetBinding(
                    Path.StrokeProperty,
                    new Binding("Foreground")
                    {
                        RelativeSource = new RelativeSource(
                            RelativeSourceMode.FindAncestor,
                            typeof(Button),
                            1
                        ),
                    }
                );
            var canvas = new Canvas { Width = 24, Height = 24 };
            canvas.Children.Add(path);
            return new Viewbox
            {
                Width = size,
                Height = size,
                Child = canvas,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        public static FrameworkElement Label(string icon, string label)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var image = Create(icon);
            image.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Children.Add(image);
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return grid;
        }
    }

    public partial class MainUI
    {
        void ApplyVisualDesign()
        {
            var nav = new[]
            {
                new[] { "NavAll", "library", "全部记录" },
                new[] { "NavStar", "star", "我的收藏" },
                new[] { "NavImage", "image", "截图与动图" },
                new[] { "NavVideo", "video", "屏幕录制" },
                new[] { "NavTrash", "trash", "回收站" },
                new[] { "Settings", "settings", "设置与快捷键" },
                new[] { "Quit", "exit", "退出 LumaShot" },
            };
            foreach (var item in nav)
            {
                var b = Get<Button>(item[0]);
                b.Content = Icons.Label(item[1], item[2]);
                System.Windows.Automation.AutomationProperties.SetName(b, item[2]);
            }
            Get<Button>("Capture").Content = Icons.Label("plus", "新建截图");
            Get<Button>("CaptureMenu").Content = Icons.Create("down", 16);
            System.Windows.Automation.AutomationProperties.SetName(
                Get<Button>("CaptureMenu"),
                "截图与录屏菜单"
            );
            var content = (UIElement)Window.Content;
            Window.Content = null;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid { Background = B("#F7F8FC") };
            header.Children.Add(
                new Border
                {
                    Width = 212,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = B("#1B1D2B"),
                }
            );
            var caption = T("LumaShot", 11, "#9095AD");
            caption.Margin = new Thickness(28, 0, 0, 0);
            caption.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(caption);
            header.Children.Add(CaptionButtons(Window));
            root.Children.Add(header);
            Grid.SetRow(content, 1);
            root.Children.Add(content);
            Window.Content = root;
            ApplyChrome(Window, 36);
        }

        FrameworkElement CaptionButtons(Window win)
        {
            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            foreach (string action in new[] { "minimize", "maximize", "close" })
            {
                string key = action;
                var button = Btn(
                    "",
                    () =>
                    {
                        if (key == "close")
                            win.Close();
                        else if (key == "minimize")
                            SystemCommands.MinimizeWindow(win);
                        else if (win.WindowState == WindowState.Maximized)
                            SystemCommands.RestoreWindow(win);
                        else
                            SystemCommands.MaximizeWindow(win);
                    }
                );
                button.Content = Icons.Create(action, 14);
                button.Width = 44;
                button.Height = 34;
                button.Padding = new Thickness(0);
                button.Margin = new Thickness(0);
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.ToolTip =
                    action == "close" ? "关闭"
                    : action == "minimize" ? "最小化"
                    : "最大化 / 还原";
                System.Windows.Automation.AutomationProperties.SetName(
                    button,
                    (string)button.ToolTip
                );
                WindowChrome.SetIsHitTestVisibleInChrome(button, true);
                controls.Children.Add(button);
            }
            return controls;
        }

        void ApplyChrome(Window win, double captionHeight)
        {
            win.WindowStyle = WindowStyle.None;
            win.ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(
                win,
                new WindowChrome
                {
                    CaptionHeight = captionHeight,
                    ResizeBorderThickness = new Thickness(6),
                    GlassFrameThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(8),
                    UseAeroCaptionButtons = false,
                }
            );
            win.SourceInitialized += (s, e) =>
            {
                HwndSource
                    .FromHwnd(new WindowInteropHelper(win).Handle)
                    .AddHook(
                        (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                            ChromeBounds(win, hwnd, msg, wParam, lParam, ref handled)
                    );
                try
                {
                    int preference = 2;
                    DwmSetWindowAttribute(
                        new WindowInteropHelper(win).Handle,
                        33,
                        ref preference,
                        4
                    );
                }
                catch (EntryPointNotFoundException) { }
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ChromePoint
        {
            public int X,
                Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ChromeRect
        {
            public int Left,
                Top,
                Right,
                Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ChromeMonitor
        {
            public int Size;
            public ChromeRect Monitor,
                Work;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ChromeMinMax
        {
            public ChromePoint Reserved,
                MaxSize,
                MaxPosition,
                MinTrack,
                MaxTrack;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr monitor, ref ChromeMonitor info);

        static IntPtr ChromeBounds(
            Window win,
            IntPtr window,
            int message,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled
        )
        {
            if (message == 0x24)
            {
                var monitor = new ChromeMonitor { Size = Marshal.SizeOf(typeof(ChromeMonitor)) };
                if (GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor))
                {
                    var bounds = (ChromeMinMax)Marshal.PtrToStructure(lParam, typeof(ChromeMinMax));
                    bounds.MaxPosition.X = monitor.Work.Left - monitor.Monitor.Left;
                    bounds.MaxPosition.Y = monitor.Work.Top - monitor.Monitor.Top;
                    bounds.MaxSize.X = monitor.Work.Right - monitor.Work.Left;
                    bounds.MaxSize.Y = monitor.Work.Bottom - monitor.Work.Top;
                    var source = PresentationSource.FromVisual(win);
                    double sx =
                        source != null && source.CompositionTarget != null
                            ? source.CompositionTarget.TransformToDevice.M11
                            : 1;
                    double sy =
                        source != null && source.CompositionTarget != null
                            ? source.CompositionTarget.TransformToDevice.M22
                            : 1;
                    if (win.MinWidth > 0)
                        bounds.MinTrack.X = Math.Max(
                            bounds.MinTrack.X,
                            (int)Math.Ceiling(win.MinWidth * sx)
                        );
                    if (win.MinHeight > 0)
                        bounds.MinTrack.Y = Math.Max(
                            bounds.MinTrack.Y,
                            (int)Math.Ceiling(win.MinHeight * sy)
                        );
                    Marshal.StructureToPtr(bounds, lParam, false);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(
            IntPtr handle,
            int attribute,
            ref int value,
            int size
        );
    }
}
