using LablabBean.Reactive.ViewModels.Media;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Terminal.Gui;

namespace LablabBean.Console.Views.Media;

/// <summary>
/// Main media player view containing video display and controls
/// </summary>
public class MediaPlayerView : FrameView, IViewFor<MediaPlayerViewModel>
{
    private readonly View _videoDisplayView;
    private readonly MediaControlsView _controlsView;
    private readonly Label _statusLabel;
    private readonly Label _errorLabel;
    private readonly CompositeDisposable _disposables = new();

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (MediaPlayerViewModel?)value;
    }

    public MediaPlayerViewModel? ViewModel { get; set; }

    public View VideoDisplayView => _videoDisplayView;

    public MediaPlayerView()
    {
        Title = "Media Player";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Video display area
        _videoDisplayView = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 5
        };
        Add(_videoDisplayView);

        // Status label
        _statusLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_videoDisplayView),
            Width = Dim.Fill(),
            Height = 1,
            Text = "Ready"
        };
        Add(_statusLabel);

        // Error label
        _errorLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = 1,
            Text = "",
            ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black)
            }
        };
        Add(_errorLabel);

        // Controls view
        _controlsView = new MediaControlsView
        {
            X = 0,
            Y = Pos.Bottom(_errorLabel),
            Width = Dim.Fill(),
            Height = 3
        };
        Add(_controlsView);

        // Keyboard shortcuts
        KeyPress += HandleKeyPress;
    }

    private void HandleKeyPress(object? sender, Key e)
    {
        if (ViewModel == null)
            return;

        switch (e.KeyCode)
        {
            case KeyCode.Space:
                // Toggle play/pause
                if (ViewModel.IsPlaying)
                {
                    if (ViewModel.PauseCommand.CanExecute.FirstAsync().Wait())
                    {
                        ViewModel.PauseCommand.Execute().Subscribe();
                    }
                }
                else
                {
                    if (ViewModel.PlayCommand.CanExecute.FirstAsync().Wait())
                    {
                        ViewModel.PlayCommand.Execute().Subscribe();
                    }
                }
                e.Handled = true;
                break;

            case KeyCode.Esc:
                // Stop playback
                if (ViewModel.StopCommand.CanExecute.FirstAsync().Wait())
                {
                    ViewModel.StopCommand.Execute().Subscribe();
                }
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposables.Dispose();
        }
        base.Dispose(disposing);
    }

    public void BindViewModel(MediaPlayerViewModel viewModel)
    {
        ViewModel = viewModel;
        _controlsView.BindViewModel(viewModel);

        // Subscribe to state changes
        viewModel.WhenAnyValue(x => x.State)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(state =>
            {
                if (state != null)
                {
                    _statusLabel.Text = state.Status switch
                    {
                        PlaybackStatus.Loading => "Loading media...",
                        PlaybackStatus.Playing => $"Playing: {state.CurrentMedia?.Path ?? "Unknown"}",
                        PlaybackStatus.Paused => "Paused",
                        PlaybackStatus.Stopped => "Stopped",
                        PlaybackStatus.Buffering => "Buffering...",
                        PlaybackStatus.Error => "Error occurred",
                        _ => "Ready"
                    };
                }
            })
            .DisposeWith(_disposables);

        viewModel.WhenAnyValue(x => x.ErrorMessage)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(error =>
            {
                _errorLabel.Text = error ?? "";
                _errorLabel.Visible = !string.IsNullOrEmpty(error);
            })
            .DisposeWith(_disposables);

        // Thread-safe UI updates via Application.Invoke
        Observable.Interval(TimeSpan.FromMilliseconds(100))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                Application.Invoke(() =>
                {
                    SetNeedsDraw();
                });
            })
            .DisposeWith(_disposables);
    }

    public async Task LoadAndPlayAsync(string filePath)
    {
        if (ViewModel == null)
            return;

        try
        {
            await ViewModel.LoadMediaCommand.Execute(filePath);
            await ViewModel.PlayCommand.Execute();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to load media: {ex.Message}", "OK");
        }
    }
}
