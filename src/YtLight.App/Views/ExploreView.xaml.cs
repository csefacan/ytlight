using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YtLight.App.ViewModels;
using YtLight.Core.Models;

namespace YtLight.App.Views;

public partial class ExploreView : UserControl
{
    public ExploreView() => InitializeComponent();

    private void OnTrackSingleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox box)
            return;

        var hit = VisualTreeHelper.HitTest(box, e.GetPosition(box));
        if (hit?.VisualHit is DependencyObject dep)
        {
            var item = FindAncestor<ListBoxItem>(dep);
            if (item?.DataContext is Track track)
            {
                box.SelectedItem = track;
                FindMain()?.PlayTrackCommand.Execute(track);
            }
        }
    }

    private void OnTrackRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox box)
            return;

        var hit = VisualTreeHelper.HitTest(box, e.GetPosition(box));
        if (hit?.VisualHit is DependencyObject dep)
        {
            var item = FindAncestor<ListBoxItem>(dep);
            if (item?.DataContext is Track track)
            {
                box.SelectedItem = track;
            }
        }
    }

    private void OnPlayClicked(object sender, RoutedEventArgs e)
    {
        if (FindTrack(sender) is { } track)
            FindMain()?.PlayTrackCommand.Execute(track);
    }

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        if (FindTrack(sender) is { } track)
            FindMain()?.AddToSelectedPlaylistCommand.Execute(track);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scroller = sender as ScrollViewer ?? FindAncestor<ScrollViewer>(sender as DependencyObject);
        if (scroller is null)
            return;

        if (e.Delta > 0)
            scroller.LineUp();
        else
            scroller.LineDown();

        e.Handled = true;
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

    private static Track? FindTrack(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            var menu = menuItem.Parent as ContextMenu;
            if (menu?.PlacementTarget is ListBox box && box.SelectedItem is Track track)
                return track;
        }
        return null;
    }

    private MainViewModel? FindMain()
    {
        var root = Window.GetWindow(this)?.DataContext;
        return root as MainViewModel;
    }
}
