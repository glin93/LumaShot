using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LumaShot
{
    public partial class MainUI
    {
        readonly Dictionary<string, TextBox> recordEditors = new Dictionary<string, TextBox>();
        readonly Dictionary<string, Border> cardBorders = new Dictionary<string, Border>();
        readonly Dictionary<string, TextBlock> cardTitles = new Dictionary<string, TextBlock>();
        DispatcherTimer editTimer;
        bool applyingRemote,
            editsPending;
        Action commitTagDraft,
            refreshTagChips;

        // Compare each field with the request snapshot. Focus is not a revision:
        // unchanged fields accept remote edits, while edits made during sync survive.
        internal static void MergeRecord(Shot local, Shot before, Shot remote)
        {
            foreach (var field in typeof(Shot).GetFields())
            {
                if (field.Name == "Id" || field.Name == "SourceKey" || field.Name == "OcrDone")
                    continue;
                if (object.Equals(field.GetValue(local), field.GetValue(before)))
                    field.SetValue(local, field.GetValue(remote));
            }
            local.OcrDone = local.OcrDone || !string.IsNullOrEmpty(local.OcrText);
        }

        void BindEditor(TextBox box, Shot shot, string name)
        {
            recordEditors[name] = box;
            System.Windows.Automation.AutomationProperties.SetName(
                box,
                name == "Title" ? "记录标题"
                    : name == "Tags" ? "标签"
                    : "备注"
            );
            box.TextChanged += (s, e) =>
            {
                if (applyingRemote)
                    return;
                if (name == "Title" && string.IsNullOrWhiteSpace(box.Text))
                    return;
                typeof(Shot).GetField(name).SetValue(shot, box.Text);
                TextBlock title;
                if (name == "Title" && cardTitles.TryGetValue(shot.Id, out title))
                    title.Text = shot.Title;
                editsPending = true;
                if (editTimer == null)
                {
                    editTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    editTimer.Tick += (sender, args) => FlushEdits();
                }
                editTimer.Stop();
                editTimer.Start();
            };
        }

        void FlushEdits()
        {
            if (commitTagDraft != null)
                commitTagDraft();
            if (editTimer != null)
                editTimer.Stop();
            if (!editsPending)
                return;
            lib.Save();
            editsPending = false;
        }

        void RefreshEditors()
        {
            var shot = inspector.Tag as Shot;
            if (shot == null)
                return;
            applyingRemote = true;
            try
            {
                foreach (var entry in recordEditors)
                {
                    string text = (string)typeof(Shot).GetField(entry.Key).GetValue(shot) ?? "";
                    if (entry.Value.Text != text)
                    {
                        int caret = entry.Value.CaretIndex;
                        entry.Value.Text = text;
                        entry.Value.CaretIndex = Math.Min(caret, text.Length);
                    }
                }
            }
            finally
            {
                applyingRemote = false;
            }
            if (refreshTagChips != null)
                refreshTagChips();
        }

        FrameworkElement TagEditor(Shot shot)
        {
            var root = new StackPanel();
            var chips = new WrapPanel();
            root.Children.Add(chips);
            var input = new TextBox
            {
                ToolTip = "输入标签，按回车添加",
                Padding = new Thickness(9, 6, 9, 6),
                MinWidth = 120,
            };
            System.Windows.Automation.AutomationProperties.SetName(input, "添加标签");
            var inputFrame = new Grid();
            inputFrame.Children.Add(input);
            var hint = T("添加标签…", 12, "#9297AA");
            hint.Margin = new Thickness(10, 6, 0, 0);
            hint.IsHitTestVisible = false;
            inputFrame.Children.Add(hint);
            root.Children.Add(inputFrame);
            Action render = null;
            render = () =>
            {
                chips.Children.Clear();
                foreach (
                    string name in (shot.Tags ?? "")
                        .Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .Distinct()
                )
                {
                    string value = name;
                    var chip = Small(
                        name + "  ×",
                        () =>
                        {
                            shot.Tags = string.Join(
                                ",",
                                (shot.Tags ?? "")
                                    .Split(
                                        new[] { ',', '，' },
                                        StringSplitOptions.RemoveEmptyEntries
                                    )
                                    .Select(x => x.Trim())
                                    .Where(x => x != value)
                                    .Distinct()
                            );
                            editsPending = true;
                            FlushEdits();
                            render();
                        }
                    );
                    chip.Background = B("#EEEAF8");
                    chip.BorderThickness = new Thickness(0);
                    chip.Foreground = B("#7057B9");
                    chip.ToolTip = "移除标签 " + name;
                    System.Windows.Automation.AutomationProperties.SetName(
                        chip,
                        "移除标签 " + name
                    );
                    chips.Children.Add(chip);
                }
            };
            Action commit = () =>
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                    return;
                shot.Tags = string.Join(
                    ",",
                    ((shot.Tags ?? "") + "," + input.Text)
                        .Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .Distinct()
                );
                input.Clear();
                editsPending = true;
                render();
            };
            input.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    commit();
                    FlushEdits();
                    e.Handled = true;
                }
            };
            input.LostKeyboardFocus += (s, e) =>
            {
                commit();
                FlushEdits();
            };
            input.TextChanged += (s, e) =>
                hint.Visibility =
                    input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            commitTagDraft = commit;
            refreshTagChips = render;
            render();
            return root;
        }

        void SelectCard(Shot item)
        {
            FlushEdits();
            Shot previous = selected;
            selected = item;
            Border border;
            if (previous != null && cardBorders.TryGetValue(previous.Id, out border))
                UpdateCardStyle(previous, border);
            if (cardBorders.TryGetValue(item.Id, out border))
                UpdateCardStyle(item, border);
            // Batch operations change the action bar, ordinary selection does not.
            if (
                System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
            )
                Refresh();
            else
                Details();
        }

        void UpdateCardStyle(Shot item, Border border)
        {
            bool selectedCard = selected == item || batch.Contains(item.Id);
            border.BorderBrush = B(
                selectedCard ? "#8A72E8"
                : border.IsMouseOver ? "#B7ABDF"
                : "#E5E7F0"
            );
            // Keep geometry stable when moving selection.
            border.Background = B(border.IsMouseOver ? "#FCFBFF" : "#FFFFFF");
        }
    }
}
