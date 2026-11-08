using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YtLight.App.ViewModels;
using YtLight.Core.Models;

namespace YtLight.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    private void OnTrackDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView list || list.SelectedItem is not Track track)
            return;
        if (DataContext is LibraryViewModel)
        {
            var main = FindMain();
            main?.PlayTrackCommand.Execute(track);
        }
    }

    private void OnTrackRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView list)
            return;

        var hit = VisualTreeHelper.HitTest(list, e.GetPosition(list));
        if (hit?.VisualHit is DependencyObject dep)
        {
            var item = FindAncestor<ListViewItem>(dep);
            if (item?.DataContext is Track track)
            {
                list.SelectedItem = track;
            }
        }
    }

    private void OnPlayTrack(object sender, RoutedEventArgs e)
    {
        if (TryGetSelectedTrack(sender) is { } track)
            FindMain()?.PlayTrackCommand.Execute(track);
    }

    private void OnAddTrack(object sender, RoutedEventArgs e)
    {
        if (TryGetSelectedTrack(sender) is { } track)
            FindMain()?.AddToSelectedPlaylistCommand.Execute(track);
    }

    private void OnRemoveTrack(object sender, RoutedEventArgs e)
    {
        if (TryGetSelectedTrack(sender) is { } track && DataContext is LibraryViewModel vm)
            vm.RemoveTrackFromCurrentPlaylistCommand.Execute(track);
    }

    private void OnAlbumClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: AlbumSummary album } && DataContext is LibraryViewModel vm)
        {
            vm.LoadAlbumCommand.Execute(album);
        }
    }

    private async void OnCreatePlaylistClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        var input = Prompt("Yeni Çalma Listesi", "Çalma listesi adı:");
        if (!string.IsNullOrWhiteSpace(input))
        {
            await vm.CreatePlaylistCommand.ExecuteAsync(input.Trim());
        }
    }

    private static Track? TryGetSelectedTrack(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            var menu = menuItem.Parent as ContextMenu;
            if (menu?.PlacementTarget is ListView list && list.SelectedItem is Track track)
                return track;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T found)
                return found;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private MainViewModel? FindMain()
    {
        var root = Window.GetWindow(this)?.DataContext;
        return root as MainViewModel;
    }

    private static string? Prompt(string title, string message)
    {
        var win = new Window
        {
            Title = title,
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 28)),
            WindowStyle = WindowStyle.ToolWindow
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock { Text = message, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
        var box = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Oluştur", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "İptal", Width = 80, IsCancel = true };

        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };
        cancel.Click += (_, _) => { win.DialogResult = false; };

        btnPanel.Children.Add(ok);
        btnPanel.Children.Add(cancel);
        panel.Children.Add(label);
        panel.Children.Add(box);
        panel.Children.Add(btnPanel);
        win.Content = panel;

        box.Focus();
        return win.ShowDialog() == true ? result : null;
    }
}
