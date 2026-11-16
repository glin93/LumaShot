using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LumaShot
{
    public partial class MainUI
    {
        Window trayMenu;

        void UpdateTray()
        {
            if (tray == null)
                return;
            // The icon stays native; its menu uses the same WPF surfaces as the app.
            if (tray.ContextMenuStrip != null)
            {
                tray.ContextMenuStrip.Dispose();
                tray.ContextMenuStrip = null;
            }
        }

        internal void ShowTrayMenu()
        {
            if (trayMenu != null)
            {
                trayMenu.Close();
                return;
            }
            HideQuickHistory();
            var win = Surface("LumaShot · 托盘菜单", true);
            trayMenu = win;
            win.Width = 324;
            win.SizeToContent = SizeToContent.Height;
            var items = new StackPanel();
            var title = T("LumaShot", 13, "#74778A");
            title.FontWeight = FontWeights.SemiBold;
            title.Margin = new Thickness(12, 5, 12, 9);
            items.Children.Add(title);
            Action separator = () =>
                items.Children.Add(
                    new Border
                    {
                        Height = 1,
                        Background = B("#E8E9F0"),
                        Margin = new Thickness(10, 6, 10, 6),
                    }
                );
            Action<string, string, string, Action> add = (label, key, glyph, action) =>
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = Icons.Create(glyph, 17, B("#747186"));
                icon.HorizontalAlignment = HorizontalAlignment.Left;
                row.Children.Add(icon);
                var text = T(label, 13, "#323548");
                text.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                var hint = T(key, 11, "#8B8E9E");
                hint.VerticalAlignment = VerticalAlignment.Center;
                hint.Margin = new Thickness(18, 0, 0, 0);
                Grid.SetColumn(hint, 2);
                row.Children.Add(hint);
                var button = Btn(
                    "",
                    () =>
                    {
                        win.Close();
                        action();
                    }
                );
                button.Content = row;
                button.Style = (Style)Window.FindResource("TrayItem");
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                button.Margin = new Thickness(0, 1, 0, 1);
                System.Windows.Automation.AutomationProperties.SetName(button, label);
                items.Children.Add(button);
            };
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                    separator();
                string action = Preferences.Actions[i];
                add(
                    Preferences.Names[i],
                    Hotkey(action),
                    i < 3 ? "\uE722" : "\uE714",
                    () => RunShortcut(action)
                );
            }
            separator();
            add("快速历史", Hotkey("quickHistory"), "\uE81C", ToggleQuickHistory);
            add("资料库", Hotkey("library"), "\uE8B7", Show);
            add(
                "设置",
                "",
                "\uE713",
                () =>
                {
                    Show();
                    Settings();
                }
            );
            separator();
            add("退出", "", "\uE7E8", Exit);
            var frame = SurfaceFrame(items);
            frame.CornerRadius = new CornerRadius(12);
            frame.Padding = new Thickness(6);
            win.Content = frame;
            var area = PointerWorkArea();
            var cursor = System.Windows.Forms.Cursor.Position;
            double scale = DisplayPlacement.Scale(System.Windows.Forms.Screen.FromPoint(cursor));
            var point = new Point(cursor.X / scale, cursor.Y / scale);
            frame.Measure(new Size(win.Width, double.PositiveInfinity));
            double height = Math.Min(frame.DesiredSize.Height, area.Height - 12);
            win.MaxHeight = area.Height - 12;
            win.Left = Math.Max(
                area.Left + 6,
                Math.Min(point.X - win.Width + 20, area.Right - win.Width - 6)
            );
            win.Top = Math.Max(area.Top + 6, Math.Min(point.Y - height, area.Bottom - height - 6));
            bool closing = false;
            win.Closing += (s, e) => closing = true;
            win.Deactivated += (s, e) =>
            {
                if (!closing)
                    win.Close();
            };
            win.Closed += (s, e) =>
            {
                if (ReferenceEquals(trayMenu, win))
                    trayMenu = null;
            };
            win.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    win.Close();
                    e.Handled = true;
                }
                else if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    var focused = Keyboard.FocusedElement as UIElement;
                    if (focused != null)
                        focused.MoveFocus(
                            new TraversalRequest(
                                e.Key == Key.Down
                                    ? FocusNavigationDirection.Next
                                    : FocusNavigationDirection.Previous
                            )
                        );
                    e.Handled = true;
                }
            };
            KeyboardNavigation.SetTabNavigation(items, KeyboardNavigationMode.Cycle);
            win.Show();
            win.Activate();
            items.Children.OfType<Button>().First().Focus();
        }
    }
}
