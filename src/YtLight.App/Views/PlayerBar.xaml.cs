using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using YtLight.App.ViewModels;

namespace YtLight.App.Views;

public partial class PlayerBar : UserControl
{
    private bool _isDraggingSeek;

    public PlayerBar()
    {
        InitializeComponent();
        Seek.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _isDraggingSeek = true));
        Seek.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            _isDraggingSeek = false;
            if (DataContext is PlayerBarViewModel vm)
                vm.SeekCommand.Execute(Seek.Value);
        }));
    }

    private void OnSeek(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSeek && DataContext is PlayerBarViewModel vm)
            vm.SeekCommand.Execute(Seek.Value);
    }

    private void OnSleepClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu })
        {
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }
    }
}
