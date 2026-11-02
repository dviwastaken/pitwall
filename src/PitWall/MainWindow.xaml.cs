using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using PitWall.Models;
using PitWall.Services;
using PitWall.ViewModels;

namespace PitWall;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(0x19, 0x9E, 0x70));
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromRgb(0xE6, 0x67, 0x67));

    private readonly MainViewModel _viewModel = new();
    private readonly LapDatabaseService _database = new();
    private readonly TelemetryService _telemetry = new();
    private readonly LapRecorder _lapRecorder;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _lapRecorder = new LapRecorder(_database);

        foreach (var lap in _database.GetLaps())
        {
            _viewModel.Laps.Add(lap);
        }

        _telemetry.Connected += () => Dispatcher.BeginInvoke(() =>
        {
            _viewModel.SetConnected(true);
            StatusDot.Fill = ConnectedBrush;
        });

        _telemetry.Disconnected += () => Dispatcher.BeginInvoke(() =>
        {
            _viewModel.SetConnected(false);
            StatusDot.Fill = DisconnectedBrush;
        });

        _telemetry.SessionInfoReceived += session =>
        {
            _lapRecorder.OnSessionInfo(session);
            Dispatcher.BeginInvoke(() => _viewModel.SetSession(session));
        };

        _telemetry.SnapshotReceived += snapshot =>
        {
            _lapRecorder.OnSnapshot(snapshot);
            Dispatcher.BeginInvoke(() => _viewModel.UpdateSnapshot(snapshot));
        };

        _lapRecorder.LapSaved += lap => Dispatcher.BeginInvoke(() => _viewModel.AddLap(lap));

        _telemetry.Start();
    }

    private void CompareButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = LapsDataGrid.SelectedItems;
        if (selected.Count != 2)
        {
            _viewModel.ComparisonInfoText = "Lütfen listeden tam olarak iki tur seçin (Ctrl+tık).";
            return;
        }

        var lapA = (LapListItem)selected[0]!;
        var lapB = (LapListItem)selected[1]!;

        var samplesA = _database.GetLapSamples(lapA.LapId);
        var samplesB = _database.GetLapSamples(lapB.LapId);

        _viewModel.LoadComparison(lapA, samplesA, lapB, samplesB);
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        _telemetry.Dispose();
    }
}
