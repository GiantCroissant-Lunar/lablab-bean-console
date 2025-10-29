using LablabBean.Reactive.ViewModels.Media;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Terminal.Gui;

namespace LablabBean.Console.Views.Media;

/// <summary>
/// Media playback controls view (play, pause, stop, volume, position)
/// </summary>
public class MediaControlsView : View, IViewFor<MediaPlayerViewModel>
{
    private readonly Button _playButton;
    private readonly Button _pauseButton;
    private readonly Button _stopButton;
    private readonly Label _positionLabel;
    private readonly Label _volumeLabel;
    private readonly Slider _volumeSlider;
    private readonly CompositeDisposable _disposables = new();

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (MediaPlayerViewModel?)value;
    }

    public MediaPlayerViewModel? ViewModel { get; set; }

    public MediaControlsView()
    {
        Width = Dim.Fill();
        Height = 3;

        // Play button
        _playButton = new Button
        {
            Text = "▶ Play",
            X = 0,
            Y = 0,
            Width = 12
        };
        Add(_playButton);

        // Pause button
        _pauseButton = new Button
        {
            Text = "⏸ Pause",
            X = Pos.Right(_playButton) + 1,
            Y = 0,
            Width = 12
        };
        Add(_pauseButton);

        // Stop button
        _stopButton = new Button
        {
            Text = "⏹ Stop",
            X = Pos.Right(_pauseButton) + 1,
            Y = 0,
            Width = 12
        };
        Add(_stopButton);

        // Position label
        _positionLabel = new Label
        {
            Text = "00:00 / 00:00",
            X = Pos.Right(_stopButton) + 2,
            Y = 0,
            Width = 20
        };
        Add(_positionLabel);

        // Volume label
        _volumeLabel = new Label
        {
            Text = "🔊 100%",
            X = 0,
            Y = 1,
            Width = 10
        };
        Add(_volumeLabel);

        // Volume slider
        _volumeSlider = new Slider
        {
            X = Pos.Right(_volumeLabel) + 1,
            Y = 1,
            Width = 30,
            Minimum = 0,
            Maximum = 100,
            Value = 100,
            AllowEmpty = false,
            Type = SliderType.Single,
            ShowLegends = false
        };
        Add(_volumeSlider);
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

        // Bind Play command
        _playButton.Clicked += () =>
        {
            if (viewModel.PlayCommand.CanExecute.FirstAsync().Wait())
            {
                viewModel.PlayCommand.Execute().Subscribe();
            }
        };

        // Bind Pause command
        _pauseButton.Clicked += () =>
        {
            if (viewModel.PauseCommand.CanExecute.FirstAsync().Wait())
            {
                viewModel.PauseCommand.Execute().Subscribe();
            }
        };

        // Bind Stop command
        _stopButton.Clicked += () =>
        {
            if (viewModel.StopCommand.CanExecute.FirstAsync().Wait())
            {
                viewModel.StopCommand.Execute().Subscribe();
            }
        };

        // Bind volume slider
        _volumeSlider.ValueChanged += (sender, args) =>
        {
            viewModel.Volume = args.NewValue / 100.0f;
        };

        // Subscribe to ViewModel changes
        viewModel.WhenAnyValue(x => x.Volume)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(vol =>
            {
                var volumePercent = (int)(vol * 100);
                _volumeSlider.Value = volumePercent;

                var icon = vol > 0.66f ? "🔊" :
                          vol > 0.33f ? "🔉" :
                          vol > 0 ? "🔈" : "🔇";
                _volumeLabel.Text = $"{icon} {volumePercent}%";
            })
            .DisposeWith(_disposables);

        viewModel.WhenAnyValue(x => x.Position, x => x.Duration)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(tuple =>
            {
                var (pos, dur) = tuple;
                _positionLabel.Text = $"{viewModel.FormatTimeSpan(pos)} / {viewModel.FormatTimeSpan(dur)}";
            })
            .DisposeWith(_disposables);

        viewModel.WhenAnyValue(x => x.IsPlaying)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(isPlaying =>
            {
                _playButton.Enabled = !isPlaying;
                _pauseButton.Enabled = isPlaying;
            })
            .DisposeWith(_disposables);

        viewModel.WhenAnyValue(x => x.IsStopped)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(isStopped =>
            {
                _stopButton.Enabled = !isStopped;
            })
            .DisposeWith(_disposables);
    }
}
