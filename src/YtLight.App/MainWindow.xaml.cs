using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YtLight.App.Services;
using YtLight.App.ViewModels;
using YtLight.App.Views;
using YtLight.Core.Abstractions;
using YtLight.Core.Memory;
using YtLight.Core.Models;
using YtLight.Playback;

namespace YtLight.App;

public partial class MainWindow : Window
{
    private readonly ITrayService _tray;
    private readonly MemoryGuardian _memory;
    private readonly ISleepTimer _sleep;
    private Point _dragStart;
    private object? _dragItem;

    public MainWindow(
        MainViewModel viewModel,
        ITrayService tray,
        MemoryGuardian memory,
        ISleepTimer sleep,
        WebViewPlayerBackend playerBackend)
    {
        InitializeComponent();
        DataContext = viewModel;
        _tray = tray;
        _memory = memory;
        _sleep = sleep;

        playerBackend.Attach(PlayerWebView);

        Loaded += async (_, _) => await viewModel.InitializeAsync();
        StateChanged += OnStateChanged;
        Closing += OnClosing;

        _tray.ShowWindowRequested += (_, _) => RestoreFromTray();
        _tray.PlayPauseRequested += async (_, _) => await viewModel.Player.PlayPauseCommand.ExecuteAsync(null);
        _tray.NextRequested += async (_, _) => await viewModel.Player.NextCommand.ExecuteAsync(null);
        _tray.SleepTimerRequested += (_, _) => _sleep.Start(SleepTimerMode.Minutes30);
        _tray.ExitRequested += (_, _) =>
        {
            _forceClose = true;
            Application.Current.Shutdown();
        };
    }

    private bool _forceClose;

    private MainViewModel Vm => (MainViewModel)DataContext;

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        try
        {
            var login = new LoginWindow { Owner = this };
            var result = login.ShowDialog();
            if (result == true && login.CapturedSession is not null)
                await Vm.CompleteLoginAsync(login.CapturedSession);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Giriş penceresi açılamadı: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await Vm.SearchCommand.ExecuteAsync(null);
        if (e.Key == Key.Escape)
            Vm.IsSearchOpen = false;
    }

    private void OnCloseSearch(object sender, MouseButtonEventArgs e) => Vm.IsSearchOpen = false;

    private void OnSearchDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Track track })
            Vm.PlayTrackCommand.Execute(track);
    }

    private void OnSearchAdd(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: ListBox box } } && box.SelectedItem is Track track)
            Vm.AddToSelectedPlaylistCommand.Execute(track);
    }

    private async void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl)
            return;

        if (Vm.SelectedTab == 1)
            await Vm.LoadExploreIfNeededAsync();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
            _memory.OnMinimizedToTray();
        }
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose)
            return;

        // Closing the window should terminate the app. Minimize-to-tray remains
        // available via the window state change behavior when the user minimizes.
        e.Cancel = false;
    }

    private void QueuePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox list)
            return;
        var pos = e.GetPosition(list);
        if (Math.Abs(pos.X - _dragStart.X) < 4 && Math.Abs(pos.Y - _dragStart.Y) < 4 && _dragItem is not null)
            return;
        _dragStart = pos;
        _dragItem = list.SelectedItem;
        if (_dragItem is not null)
            DragDrop.DoDragDrop(list, _dragItem, DragDropEffects.Move);
    }

    private void QueueDragOver(object sender, DragEventArgs e) => e.Effects = DragDropEffects.Move;

    private void QueueDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox list || _dragItem is not QueueItem moved)
            return;
        var point = e.GetPosition(list);
        var target = list.InputHitTest(point) as DependencyObject;
        while (target is not null && target is not ListBoxItem)
            target = System.Windows.Media.VisualTreeHelper.GetParent(target);
        if (target is ListBoxItem item)
        {
            var to = list.ItemContainerGenerator.IndexFromContainer(item);
            var from = list.Items.IndexOf(moved);
            if (from >= 0 && to >= 0)
                Vm.Player.MoveQueue(from, to);
        }
        _dragItem = null;
    }
}
