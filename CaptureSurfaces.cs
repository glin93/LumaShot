using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using F = System.Windows.Forms;

namespace LumaShot
{
    public partial class MainUI
    {
        Window captureRail,
            quickHistory;
        StackPanel railItems,
            quickItems;
        ScrollViewer railScroll,
            quickScroll;
        TextBox quickSearch;
        TextBlock quickCount;
        readonly List<string> pendingCards = new List<string>();
        readonly CapturePreviewLifetime previewLifetime = new CapturePreviewLifetime();
        readonly System.Diagnostics.Stopwatch previewClock =
            System.Diagnostics.Stopwatch.StartNew();
        DispatcherTimer previewExpiryTimer;
        bool surfacesSuspended,
            capturing;
        Rect railArea;
        List<Shot> quickResults = new List<Shot>();
        int quickSelected;
        string quickSelectionId;
        F.Screen railScreen;
        DateTime latestAutomaticCopy = DateTime.MinValue;

        internal static void FitCaptureCards(
            List<string> cards,
            Func<string, double> height,
            double available
        )
        {
            double total = 12 + cards.Sum(height);
            while (cards.Count > 1 && total > available)
            {
                total -= height(cards[0]);
                cards.RemoveAt(0);
            }
        }

        Rect PointerWorkArea()
        {
            return DisplayPlacement.WorkArea(F.Screen.FromPoint(F.Cursor.Position));
        }

        Window Surface(string title, bool activate)
        {
            return new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = demo,
                Topmost = true,
                ShowActivated = activate,
                WindowStartupLocation = WindowStartupLocation.Manual,
                FontFamily = Window.FontFamily,
                FontSize = 13,
                Foreground = B("#303247"),
                UseLayoutRounding = true,
                Resources = Window.Resources,
            };
        }

        Border SurfaceFrame(UIElement content)
        {
            return new Border
            {
                Background = B("#FCFCFF"),
                CornerRadius = new CornerRadius(20),
                BorderBrush = B("#E9E5EF"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(7),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(32, 23, 61),
                    Opacity = .17,
                    BlurRadius = 14,
                    ShadowDepth = 3,
                },
                Child = content,
            };
        }

        Button SurfaceButton(string label, string tip, Action action)
        {
            var b = Small(label, action);
            b.ToolTip = tip;
            b.Padding = new Thickness(9, 5, 9, 5);
            b.FontSize = 11;
            return b;
        }

        void EnsureRail()
        {
            if (captureRail != null)
                return;
            captureRail = Surface("LumaShot · 最近截图", false);
            captureRail.Width = 246;
            railItems = new StackPanel();
            railScroll = new ScrollViewer
            {
                Content = railItems,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 4, 0, 8),
            };
            captureRail.Content = railScroll;
            previewExpiryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            previewExpiryTimer.Tick += (s, e) =>
            {
                if (
                    pendingCards.RemoveAll(id =>
                        previewLifetime.IsExpired(id, previewClock.Elapsed.TotalSeconds)
                    ) > 0
                )
                    RenderRail();
            };
            captureRail.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    pendingCards.Clear();
                    RenderRail();
                    e.Handled = true;
                }
            };
            captureRail.Closing += (s, e) =>
            {
                if (!quitting)
                {
                    e.Cancel = true;
                    pendingCards.Clear();
                    RenderRail();
                }
            };
        }

        void AddCaptureCard(Shot shot)
        {
            if (shot == null || shot.Deleted)
                return;
            EnsureRail();
            pendingCards.RemoveAll(id =>
                previewLifetime.IsExpired(id, previewClock.Elapsed.TotalSeconds)
            );
            previewLifetime.Retain(pendingCards);
            pendingCards.Remove(shot.Id);
            pendingCards.Add(shot.Id);
            previewLifetime.Add(shot.Id, previewClock.Elapsed.TotalSeconds);
            if (railArea.IsEmpty || railArea.Width == 0)
            {
                railScreen = F.Screen.FromPoint(F.Cursor.Position);
                railArea = DisplayPlacement.WorkArea(railScreen);
            }
            RenderRail();
        }

        void DismissCaptureCard(string id)
        {
            pendingCards.Remove(id);
            RenderRail();
        }

        void RenderRail()
        {
            if (captureRail == null)
                return;
            pendingCards.RemoveAll(id =>
                previewLifetime.IsExpired(id, previewClock.Elapsed.TotalSeconds)
            );
            pendingCards.RemoveAll(id => !lib.Items.Any(x => x.Id == id && !x.Deleted));
            FitCaptureCards(
                pendingCards,
                id => CaptureTileHeight(lib.Items.First(x => x.Id == id)) + 18,
                railArea.Height - 24
            );
            previewLifetime.Retain(pendingCards);
            foreach (FrameworkElement card in railItems.Children)
                if (card.ContextMenu != null)
                    card.ContextMenu.IsOpen = false;
            railItems.Children.Clear();
            foreach (string id in pendingCards)
            {
                var shot = lib.Items.First(x => x.Id == id);
                railItems.Children.Add(FloatingCard(shot, false));
            }
            if (pendingCards.Count == 0)
            {
                previewExpiryTimer.Stop();
                captureRail.Hide();
                return;
            }
            previewExpiryTimer.Start();
            double height = Math.Min(
                railArea.Height - 24,
                12
                    + pendingCards.Sum(id =>
                        CaptureTileHeight(lib.Items.First(x => x.Id == id)) + 18
                    )
            );
            captureRail.Height = Math.Max(140, height);
            captureRail.Left = railArea.Left + 6;
            captureRail.Top = railArea.Bottom - captureRail.Height - 12;
            if (!surfacesSuspended)
            {
                captureRail.Show();
                if (railScreen != null)
                    DisplayPlacement.Position(
                        captureRail,
                        railScreen,
                        railArea.Left + 6,
                        railArea.Bottom - height - 12,
                        246,
                        height
                    );
            }
            railScroll.ScrollToEnd();
        }

        FrameworkElement FloatingCard(Shot shot, bool horizontal)
        {
            if (!horizontal)
                return CaptureTile(shot);
            var tile = CaptureTile(shot, true);
            var wrap = new StackPanel
            {
                Width = 246,
                ToolTip = shot.Title,
                ContextMenu = tile.ContextMenu,
            };
            wrap.Children.Add(tile);
            var caption = T(shot.Title, 11, "#77727F");
            caption.TextTrimming = TextTrimming.CharacterEllipsis;
            caption.Margin = new Thickness(12, 0, 12, 2);
            caption.HorizontalAlignment = HorizontalAlignment.Stretch;
            wrap.Children.Add(caption);
            return wrap;
        }

        double CaptureTileHeight(Shot shot)
        {
            return Math.Max(
                140,
                Math.Min(200, 220.0 * Math.Max(1, shot.Height) / Math.Max(1, shot.Width))
            );
        }

        Button TileButton(string label, string tooltip, Action action, bool icon)
        {
            var button = new Button
            {
                Content = label,
                ToolTip = tooltip,
                Width = icon ? 28 : 82,
                Height = icon ? 28 : 34,
                FontSize = icon ? 13 : 14,
                FontWeight = icon ? FontWeights.Normal : FontWeights.SemiBold,
                Foreground = B("#F8F9FF"),
                Background = B("#CA252838"),
                BorderBrush = B("#38FFFFFF"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            if (icon)
                button.Content = Icons.Create(label, 15);
            var frame = new FrameworkElementFactory(typeof(Border));
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
            frame.SetBinding(
                Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent,
                }
            );
            frame.SetBinding(
                Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush")
                {
                    RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent,
                }
            );
            frame.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(
                FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Center
            );
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            frame.AppendChild(content);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = frame };
            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Button.BackgroundProperty, B("#EC48405F")));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Button.BackgroundProperty, B("#FF7561DF")));
            template.Triggers.Add(pressed);
            button.Template = template;
            System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
            button.Click += (s, e) =>
            {
                e.Handled = true;
                Safe(action);
            };
            return button;
        }

        FrameworkElement CaptureTile(Shot shot, bool inHistory = false)
        {
            double height = inHistory ? 152 : CaptureTileHeight(shot);
            var outer = new Border
            {
                Width = 220,
                Height = height,
                Margin = new Thickness(10, 9, 16, 9),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(inHistory && shot.Id == quickSelectionId ? 2 : 0),
                BorderBrush = B("#8D7ACD"),
                Background = B("#F5F4F2"),
                Focusable = true,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 0, 0),
                    Opacity = .23,
                    BlurRadius = 16,
                    ShadowDepth = 4,
                },
            };
            double inset = inHistory && shot.Id == quickSelectionId ? 4 : 0;
            var grid = new Grid
            {
                Clip = new RectangleGeometry(new Rect(0, 0, 220 - inset, height - inset), 12, 12),
            };
            var preview = new Image { Stretch = Stretch.Uniform };
            try
            {
                preview.Source = Images.Load(lib.Full(shot.Thumb), 480);
            }
            catch { }
            grid.Children.Add(preview);
            var overlay = new Grid { Background = B("#B81B1D2B"), Visibility = Visibility.Hidden };
            grid.Children.Add(overlay);
            Action reveal = () =>
            {
                overlay.Visibility = Visibility.Visible;
                preview.Effect = new BlurEffect { Radius = 4 };
            };
            Action hide = () =>
            {
                if (
                    !outer.IsKeyboardFocusWithin
                    && (outer.ContextMenu == null || !outer.ContextMenu.IsOpen)
                )
                {
                    overlay.Visibility = Visibility.Hidden;
                    preview.Effect = null;
                }
            };
            outer.MouseEnter += (s, e) => reveal();
            outer.MouseLeave += (s, e) => hide();
            outer.GotKeyboardFocus += (s, e) => reveal();
            outer.LostKeyboardFocus += (s, e) =>
            {
                if (!outer.IsMouseOver)
                    hide();
            };
            var dismiss = TileButton(
                "\uE711",
                "关闭缩略图",
                () => DismissCaptureCard(shot.Id),
                true
            );
            dismiss.HorizontalAlignment = HorizontalAlignment.Left;
            dismiss.VerticalAlignment = VerticalAlignment.Top;
            dismiss.Margin = new Thickness(9);
            dismiss.Visibility = inHistory ? Visibility.Collapsed : Visibility.Visible;
            overlay.Children.Add(dismiss);
            var edit = TileButton(
                shot.Kind == "video" ? "\uE768" : "\uE70F",
                shot.Kind == "video" ? "播放" : "编辑",
                () =>
                {
                    if (inHistory)
                        HideQuickHistory();
                    selected = FindShot(shot.Id);
                    if (selected != null)
                        Edit(false);
                },
                true
            );
            edit.HorizontalAlignment = HorizontalAlignment.Left;
            edit.VerticalAlignment = VerticalAlignment.Bottom;
            edit.Margin = new Thickness(9);
            overlay.Children.Add(edit);
            var center = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            Button copy = null;
            copy = TileButton(
                "复制",
                "复制图片或文件",
                async () =>
                {
                    try
                    {
                        await CaptureClipboard.Copy(lib, shot);
                        copy.Content = "已复制";
                        Say("已复制");
                        if (inHistory)
                            HideQuickHistory();
                    }
                    catch (Exception ex)
                    {
                        copy.Content = "重试";
                        copy.ToolTip = ex.Message;
                        Say(ex.Message);
                    }
                },
                false
            );
            copy.Width = 104;
            copy.Height = 30;
            row.Children.Add(copy);
            center.Children.Add(row);
            if (aiAvailable)
            {
                var ask = TileButton(
                    "问 AI",
                    "询问 AI",
                    () =>
                    {
                        HideQuickHistory();
                        selected = FindShot(shot.Id);
                        if (selected != null)
                            Ask();
                    },
                    false
                );
                ask.Width = 104;
                ask.Height = 28;
                ask.FontSize = 12;
                ask.Margin = new Thickness(0, 6, 0, 0);
                center.Children.Add(ask);
            }
            overlay.Children.Add(center);
            var moreMenu = new ContextMenu();
            Add(
                moreMenu,
                "另存为…",
                () =>
                {
                    if (inHistory)
                        HideQuickHistory();
                    selected = FindShot(shot.Id);
                    if (selected != null)
                        Export();
                }
            );
            if (shot.Kind != "video")
                Add(
                    moreMenu,
                    "置顶贴图",
                    () =>
                    {
                        if (inHistory)
                            HideQuickHistory();
                        selected = FindShot(shot.Id);
                        if (selected != null)
                            Pin();
                    }
                );
            Add(moreMenu, "打开所在文件夹", () => OpenLocation(shot));
            Button more = null;
            more = TileButton(
                "more",
                "更多操作",
                () =>
                {
                    moreMenu.PlacementTarget = outer;
                    moreMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                    moreMenu.HorizontalOffset = -12;
                    moreMenu.IsOpen = true;
                },
                true
            );
            more.HorizontalAlignment = HorizontalAlignment.Right;
            more.VerticalAlignment = VerticalAlignment.Top;
            more.Margin = new Thickness(9);
            overlay.Children.Add(more);

            outer.Child = grid;
            Point start = new Point();
            bool drag = false,
                armed = false;
            outer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                DependencyObject p = e.OriginalSource as DependencyObject;
                while (p != null && p != outer)
                {
                    if (p is Button)
                    {
                        armed = false;
                        return;
                    }
                    p = VisualTreeHelper.GetParent(p);
                }
                if (overlay.Visibility != Visibility.Visible)
                {
                    reveal();
                    armed = false;
                    e.Handled = true;
                    return;
                }
                armed = true;
                drag = false;
                start = e.GetPosition(outer);
            };
            outer.MouseMove += (s, e) =>
            {
                if (!armed || drag || e.LeftButton != MouseButtonState.Pressed)
                    return;
                var p = e.GetPosition(outer);
                if (
                    Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance
                )
                    return;
                drag = true;
                DragDrop.DoDragDrop(
                    outer,
                    new DataObject(DataFormats.FileDrop, new[] { lib.Full(shot.File) }),
                    DragDropEffects.Copy
                );
            };
            outer.MouseLeftButtonUp += (s, e) =>
            {
                if (armed && !drag)
                {
                    if (inHistory)
                        HideQuickHistory();
                    Preview(lib.Full(shot.File), shot.Title);
                }
                armed = false;
            };
            var menu = moreMenu;
            Add(menu, "快速历史", ToggleQuickHistory);
            Add(menu, "在资料库查看", () => OpenInLibrary(shot));
            Add(
                menu,
                "关闭全部缩略图",
                () =>
                {
                    pendingCards.Clear();
                    RenderRail();
                }
            );
            outer.ContextMenu = menu;
            return outer;
        }

        Shot FindShot(string id)
        {
            return lib.Items.FirstOrDefault(x => x.Id == id && !x.Deleted);
        }

        async Task CopyShot(Shot shot)
        {
            try
            {
                await CaptureClipboard.Copy(lib, shot);
                Say("已复制");
            }
            catch (Exception e)
            {
                Say(e.Message);
            }
        }

        void OpenInLibrary(Shot shot)
        {
            HideQuickHistory();
            Show();
            search.Text = "";
            Get<ComboBox>("DateFilter").SelectedIndex = 0;
            Navigate("all");
            selected = FindShot(shot.Id);
            var records = Filtered();
            int at = records.FindIndex(x => x.Id == shot.Id);
            shown = (Math.Max(0, at) / 48 + 1) * 48;
            Refresh();
        }

        void OpenLocation(Shot shot)
        {
            System.Diagnostics.Process.Start(
                "explorer.exe",
                "/select,\"" + lib.Full(shot.File) + "\""
            );
        }

        void EnsureQuickHistory()
        {
            if (quickHistory != null)
                return;
            quickHistory = Surface("LumaShot · 快速历史", true);
            quickHistory.Height = 290;
            var dock = new DockPanel();
            var header = new DockPanel { Margin = new Thickness(3, 0, 3, 12) };
            var close = TileButton("close", "关闭", HideQuickHistory, true);
            close.Background = Brushes.Transparent;
            close.Foreground = B("#60657A");
            close.BorderBrush = Brushes.Transparent;
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var library = SurfaceButton(
                "资料库",
                "打开资料库",
                () =>
                {
                    HideQuickHistory();
                    Show();
                }
            );
            DockPanel.SetDock(library, Dock.Right);
            header.Children.Add(library);
            var next = TileButton(
                "\uE76C",
                "下一组",
                () => quickScroll.ScrollToHorizontalOffset(quickScroll.HorizontalOffset + 480),
                true
            );
            next.Background = Brushes.Transparent;
            next.Foreground = B("#60657A");
            next.BorderBrush = Brushes.Transparent;
            next.Margin = new Thickness(8, 0, 8, 0);
            DockPanel.SetDock(next, Dock.Right);
            header.Children.Add(next);
            var previous = TileButton(
                "\uE76B",
                "上一组",
                () => quickScroll.ScrollToHorizontalOffset(quickScroll.HorizontalOffset - 480),
                true
            );
            previous.Background = Brushes.Transparent;
            previous.Foreground = B("#60657A");
            previous.BorderBrush = Brushes.Transparent;
            DockPanel.SetDock(previous, Dock.Right);
            header.Children.Add(previous);
            quickSearch = new TextBox
            {
                Width = 250,
                Height = 33,
                FontSize = 12,
                ToolTip = "搜索截图",
                Margin = new Thickness(10, 0, 12, 0),
            };
            quickSearch.Padding = new Thickness(32, 5, 10, 5);
            var searchFrame = new Grid();
            searchFrame.Children.Add(quickSearch);
            var searchIcon = Icons.Create("search", 15, B("#9397AA"));
            searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
            searchIcon.Margin = new Thickness(20, 0, 0, 0);
            searchIcon.IsHitTestVisible = false;
            searchFrame.Children.Add(searchIcon);
            var placeholder = T("搜索截图…", 12, "#9397AA");
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.Margin = new Thickness(43, 0, 0, 0);
            placeholder.IsHitTestVisible = false;
            searchFrame.Children.Add(placeholder);
            quickSearch.TextChanged += (s, e) =>
                placeholder.Visibility =
                    quickSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            System.Windows.Automation.AutomationProperties.SetName(quickSearch, "搜索截图");
            DockPanel.SetDock(searchFrame, Dock.Right);
            header.Children.Add(searchFrame);
            quickCount = T("快速历史", 14, "#4C465D");
            quickCount.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(quickCount);
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);
            quickItems = new StackPanel { Orientation = Orientation.Horizontal };
            quickScroll = new ScrollViewer
            {
                Content = quickItems,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 0, 7),
            };
            quickScroll.PreviewMouseWheel += (s, e) =>
            {
                quickScroll.ScrollToHorizontalOffset(quickScroll.HorizontalOffset - e.Delta * 2);
                e.Handled = true;
            };
            var scrollFrame = new Grid();
            scrollFrame.Children.Add(quickScroll);
            var fade = new Border
            {
                Width = 26,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsHitTestVisible = false,
                Background = new LinearGradientBrush(
                    Color.FromArgb(0, 252, 252, 255),
                    Color.FromRgb(252, 252, 255),
                    0
                ),
            };
            scrollFrame.Children.Add(fade);
            quickScroll.ScrollChanged += (s, e) =>
                fade.Visibility =
                    quickScroll.ScrollableWidth - quickScroll.HorizontalOffset > 1
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            dock.Children.Add(scrollFrame);
            quickHistory.Content = SurfaceFrame(dock);
            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            debounce.Tick += (s, e) =>
            {
                debounce.Stop();
                quickSelectionId = null;
                RenderQuickHistory();
                quickScroll.ScrollToLeftEnd();
            };
            quickSearch.TextChanged += (s, e) =>
            {
                debounce.Stop();
                debounce.Start();
            };
            quickHistory.Deactivated += (s, e) =>
            {
                if (
                    !capturing
                    && !quickItems
                        .Children.OfType<FrameworkElement>()
                        .Any(x => x.ContextMenu != null && x.ContextMenu.IsOpen)
                )
                    HideQuickHistory();
            };
            quickHistory.Closing += (s, e) =>
            {
                if (!quitting)
                {
                    e.Cancel = true;
                    quickHistory.Hide();
                }
            };
            quickHistory.PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    HideQuickHistory();
                    e.Handled = true;
                    return;
                }
                if (Keyboard.FocusedElement is Button || quickResults.Count == 0)
                    return;
                if (e.Key == Key.Enter)
                {
                    await CopyShot(quickResults[quickSelected]);
                    HideQuickHistory();
                    e.Handled = true;
                }
                else if (
                    (e.Key == Key.Left || e.Key == Key.Right)
                    && !(Keyboard.FocusedElement == quickSearch && quickSearch.Text.Length > 0)
                )
                {
                    quickSelected = Math.Max(
                        0,
                        Math.Min(
                            quickResults.Count - 1,
                            quickSelected + (e.Key == Key.Right ? 1 : -1)
                        )
                    );
                    quickSelectionId = quickResults[quickSelected].Id;
                    RenderQuickHistory();
                    ((FrameworkElement)quickItems.Children[quickSelected]).BringIntoView();
                    e.Handled = true;
                }
                else if (e.Key == Key.Space && Keyboard.FocusedElement != quickSearch)
                {
                    var shot = quickResults[quickSelected];
                    HideQuickHistory();
                    Preview(lib.Full(shot.File), shot.Title);
                    e.Handled = true;
                }
            };
        }

        void RenderQuickHistory()
        {
            if (quickHistory == null)
                return;
            quickResults = lib.Query("all", quickSearch.Text, false).Take(160).ToList();
            quickSelected = Math.Max(0, quickResults.FindIndex(x => x.Id == quickSelectionId));
            quickSelectionId = quickResults.Count == 0 ? null : quickResults[quickSelected].Id;
            quickItems.Children.Clear();
            quickCount.Text = "快速历史  ·  " + quickResults.Count;
            foreach (var shot in quickResults)
                quickItems.Children.Add(FloatingCard(shot, true));
            if (quickResults.Count == 0)
                quickItems.Children.Add(
                    new TextBlock
                    {
                        Text = "没有匹配的截图",
                        Margin = new Thickness(30, 50, 30, 30),
                        Foreground = B("#9993A7"),
                    }
                );
        }

        void ToggleQuickHistory()
        {
            if (capturing || engine.Busy)
                return;
            EnsureQuickHistory();
            if (quickHistory.IsVisible)
            {
                HideQuickHistory();
                return;
            }
            var targetScreen = F.Screen.FromPoint(F.Cursor.Position);
            var area = DisplayPlacement.WorkArea(targetScreen);
            quickHistory.Width = Math.Min(1080, Math.Max(360, area.Width - 40));
            quickHistory.Left = area.Left + (area.Width - quickHistory.Width) / 2;
            quickHistory.Top = area.Top + 10;
            quickSearch.Text = "";
            quickSelectionId = null;
            RenderQuickHistory();
            quickHistory.Show();
            DisplayPlacement.Position(
                quickHistory,
                targetScreen,
                area.Left + (area.Width - quickHistory.Width) / 2,
                area.Top + 10,
                quickHistory.Width,
                quickHistory.Height
            );
            quickHistory.Activate();
            quickSearch.Focus();
            quickScroll.ScrollToLeftEnd();
        }

        void HideQuickHistory()
        {
            if (quickHistory != null)
                quickHistory.Hide();
        }

        void SuspendSurfaces()
        {
            surfacesSuspended = true;
            if (captureRail != null)
                captureRail.Hide();
        }

        async Task PrepareCaptureSurfaces()
        {
            bool hadPreview = captureRail != null && captureRail.IsVisible;
            SuspendSurfaces();
            if (hadPreview)
            {
                int result = await Task.Run(() => Native.DwmFlush());
                if (result < 0)
                    await Task.Delay(32);
            }
        }

        void ResumeSurfaces()
        {
            surfacesSuspended = false;
            RenderRail();
        }

        void CloseSurfaces()
        {
            if (previewExpiryTimer != null)
                previewExpiryTimer.Stop();
            if (captureRail != null)
                captureRail.Close();
            if (quickHistory != null)
                quickHistory.Close();
        }

        async Task AfterCapture(Shot shot)
        {
            AddCaptureCard(shot);
            string kind = shot.Kind == "image" ? "image" : "video";
            if (preferences.CaptureCopy(kind) && shot.Date >= latestAutomaticCopy)
            {
                latestAutomaticCopy = shot.Date;
                await CopyShot(shot);
            }
            string open = preferences.CaptureOpen(kind);
            if (open == "library")
                OpenInLibrary(shot);
            else if (open == "folder")
                OpenLocation(shot);
        }

        public void PreviewCaptureSurfaces()
        {
            railScreen = F.Screen.FromPoint(F.Cursor.Position);
            railArea = DisplayPlacement.WorkArea(railScreen);
            foreach (var shot in lib.Items.Where(x => !x.Deleted).OrderBy(x => x.Date).Take(12))
                AddCaptureCard(shot);
            ToggleQuickHistory();
        }
    }

    public static class CaptureClipboard
    {
        static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        static long generation;

        public static async Task Set(Action write)
        {
            long request = Interlocked.Increment(ref generation);
            await gate.WaitAsync();
            try
            {
                Exception error = null;
                bool written = false;
                for (int i = 0; i < 10; i++)
                {
                    if (request != Interlocked.Read(ref generation))
                        return;
                    try
                    {
                        if (!written)
                        {
                            write();
                            written = true;
                        }
                        else
                        {
                            Clipboard.Flush();
                            return;
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException e)
                    {
                        error = e;
                    }
                    await Task.Delay(100);
                }
                throw new IOException("剪贴板暂时被占用，请重试。", error);
            }
            finally
            {
                gate.Release();
            }
        }

        public static Task CopyText(string text)
        {
            var data = new DataObject(DataFormats.UnicodeText, text);
            return Set(() => Clipboard.SetDataObject(data, false));
        }

        public static async Task Copy(Library lib, Shot shot)
        {
            if (shot.Kind == "image")
                await Images.Copy(Images.Load(lib.Full(shot.File)));
            else
            {
                var paths = new System.Collections.Specialized.StringCollection();
                paths.Add(lib.Full(shot.File));
                var data = new DataObject();
                data.SetFileDropList(paths);
                await Set(() => Clipboard.SetDataObject(data, false));
            }
        }
    }
}
