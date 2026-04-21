using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hanime1Downloader.CSharp.Views;

public partial class MultiSelectDialog : Window
{
    private readonly IReadOnlyList<OptionGroup> _groups;
    private readonly HashSet<string> _selectedKeys;
    private List<string> _visibleKeys = [];
    private bool _preserveScrollPosition;

    public List<string> SelectedKeys => _selectedKeys.ToList();

    public MultiSelectDialog(string title, string prompt, IReadOnlyList<OptionGroup> groups, IEnumerable<string> selectedKeys)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        _groups = groups;
        _selectedKeys = selectedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Loaded += (_, _) =>
        {
            RenderGroups();
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        RenderGroups();
    }

    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && GroupHost.Children.Count > 0)
        {
            if (TryFocusFirstVisibleCheckBox())
            {
                e.Handled = true;
            }
        }
    }

    private bool TryFocusFirstVisibleCheckBox()
    {
        var first = GetAllVisibleCheckBoxes().FirstOrDefault();
        if (first is null) return false;
        first.Focus();
        return true;
    }

    private List<CheckBox> GetAllVisibleCheckBoxes()
    {
        var result = new List<CheckBox>();
        foreach (var container in GroupHost.Children.OfType<Border>())
        {
            if (container.Child is not StackPanel sp) continue;
            var wrap = sp.Children.OfType<WrapPanel>().FirstOrDefault();
            if (wrap is null) continue;
            result.AddRange(wrap.Children.OfType<CheckBox>());
        }
        return result;
    }

    private void CheckBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not CheckBox current) return;
        var all = GetAllVisibleCheckBoxes();
        var idx = all.IndexOf(current);
        if (e.Key == Key.Up)
        {
            if (idx <= 0) SearchBox.Focus();
            else all[idx - 1].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (idx >= 0 && idx < all.Count - 1) all[idx + 1].Focus();
            e.Handled = true;
        }
    }

    private void RefreshGroupsKeepingScroll()
    {
        _preserveScrollPosition = true;
        RenderGroups();
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _selectedKeys.Clear();
        RefreshGroupsKeepingScroll();
    }

    private void SelectVisibleButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var key in _visibleKeys)
        {
            _selectedKeys.Add(key);
        }

        RefreshGroupsKeepingScroll();
    }

    private void ClearVisibleButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var key in _visibleKeys)
        {
            _selectedKeys.Remove(key);
        }

        RefreshGroupsKeepingScroll();
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void SelectGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not IEnumerable<string> keys)
        {
            return;
        }

        foreach (var key in keys)
        {
            _selectedKeys.Add(key);
        }

        RefreshGroupsKeepingScroll();
    }

    private void ClearGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not IEnumerable<string> keys)
        {
            return;
        }

        foreach (var key in keys)
        {
            _selectedKeys.Remove(key);
        }

        RefreshGroupsKeepingScroll();
    }

    private System.Windows.Media.Brush R(string key) =>
        (TryFindResource(key) as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Transparent;

    private void RenderGroups()
    {
        var verticalOffset = _preserveScrollPosition ? GroupScrollViewer.VerticalOffset : 0;
        _preserveScrollPosition = false;
        GroupHost.Children.Clear();
        _visibleKeys = [];
        var keyword = SearchBox.Text.Trim();
        foreach (var group in _groups)
        {
            var visibleOptions = group.Options
                .Where(option => string.IsNullOrWhiteSpace(keyword) || option.Label.Contains(keyword, StringComparison.OrdinalIgnoreCase) || option.SearchKey.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (visibleOptions.Count == 0)
            {
                continue;
            }

            _visibleKeys.AddRange(visibleOptions.Select(option => option.SearchKey));
            var selectedInGroup = visibleOptions.Count(option => _selectedKeys.Contains(option.SearchKey));

            var container = new Border
            {
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(7, 6, 7, 5),
                Background = R("ThemeSurfaceBrush"),
                BorderBrush = R("ThemeBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            var stackPanel = new StackPanel();
            var headerPanel = new DockPanel
            {
                LastChildFill = false
            };

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var selectGroupButton = new Button
            {
                Content = "全选本组",
                Height = 22,
                MinWidth = 62,
                Padding = new Thickness(7, 0, 7, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Tag = visibleOptions.Select(option => option.SearchKey).ToList()
            };
            selectGroupButton.Click += SelectGroupButton_OnClick;
            actionPanel.Children.Add(selectGroupButton);

            var clearGroupButton = new Button
            {
                Content = "清空本组",
                Height = 22,
                MinWidth = 62,
                Padding = new Thickness(7, 0, 7, 0),
                Tag = visibleOptions.Select(option => option.SearchKey).ToList()
            };
            clearGroupButton.Click += ClearGroupButton_OnClick;
            actionPanel.Children.Add(clearGroupButton);

            DockPanel.SetDock(actionPanel, Dock.Right);
            headerPanel.Children.Add(actionPanel);
            headerPanel.Children.Add(new TextBlock
            {
                Text = group.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = R("ThemeTextBrush")
            });
            headerPanel.Children.Add(new Border
            {
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(5, 0, 0, 0),
                Background = R("ThemeSurfaceAltBrush"),
                BorderBrush = R("ThemeBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = selectedInGroup > 0 ? $"已选 {selectedInGroup} / {visibleOptions.Count}" : $"{visibleOptions.Count} 项",
                    Foreground = R("ThemeTextMutedBrush"),
                    FontSize = 10
                }
            });
            stackPanel.Children.Add(headerPanel);

            var wrapPanel = new WrapPanel
            {
                Margin = new Thickness(0, 5, 0, 0)
            };

            foreach (var option in visibleOptions)
            {
                var checkBox = new CheckBox
                {
                    Content = option.Label,
                    Margin = new Thickness(0, 0, 8, 3),
                    MinHeight = 18,
                    FontSize = 11,
                    IsChecked = _selectedKeys.Contains(option.SearchKey),
                    Tag = option.SearchKey,
                    VerticalAlignment = VerticalAlignment.Center
                };
                checkBox.Checked += OptionCheckBox_OnChanged;
                checkBox.Unchecked += OptionCheckBox_OnChanged;
                checkBox.KeyDown += CheckBox_OnKeyDown;
                wrapPanel.Children.Add(checkBox);
            }

            stackPanel.Children.Add(wrapPanel);
            container.Child = stackPanel;
            GroupHost.Children.Add(container);
        }

        if (GroupHost.Children.Count == 0)
        {
            GroupHost.Children.Add(new Border
            {
                Padding = new Thickness(10),
                Background = R("ThemeSurfaceBrush"),
                BorderBrush = R("ThemeBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = "没有匹配的选项，请尝试其他关键词。",
                    Foreground = R("ThemeTextMutedBrush"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        SelectionSummaryText.Text = _selectedKeys.Count == 0
            ? $"当前未选择任何项目，结果 {_visibleKeys.Count} 项"
            : $"总计已选 {_selectedKeys.Count} 项，当前结果 {_visibleKeys.Count} 项";
        SelectVisibleButton.IsEnabled = _visibleKeys.Count > 0;
        ClearVisibleButton.IsEnabled = _visibleKeys.Count > 0;
        if (verticalOffset > 0)
        {
            GroupScrollViewer.ScrollToVerticalOffset(verticalOffset);
        }
    }

    private void OptionCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not string searchKey)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _selectedKeys.Add(searchKey);
            RefreshGroupsKeepingScroll();
            return;
        }

        _selectedKeys.Remove(searchKey);
        RefreshGroupsKeepingScroll();
    }

    public sealed class OptionGroup
    {
        public required string Title { get; init; }
        public required IReadOnlyList<OptionItem> Options { get; init; }
    }

    public sealed class OptionItem
    {
        public required string Label { get; init; }
        public required string SearchKey { get; init; }
    }
}
