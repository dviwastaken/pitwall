using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using PitWall.Models;
using SkiaSharp;

namespace PitWall.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int MaxPoints = 200;

    private const string ColorBlue = "#3987E5";
    private const string ColorRed = "#E66767";
    private const string ColorOrange = "#D95926";
    private const string ColorAqua = "#199E70";
    private const string ColorYellow = "#C98500";

    private readonly ObservableCollection<double> _throttleValues = [];
    private readonly ObservableCollection<double> _brakeValues = [];
    private readonly ObservableCollection<double> _steeringTorqueValues = [];
    private readonly ObservableCollection<double> _speedValues = [];

    public ObservableCollection<ISeries> ThrottleBrakeSeries { get; }
    public ObservableCollection<ISeries> SteeringSeries { get; }
    public ObservableCollection<ISeries> SpeedSeries { get; }

    public Axis[] TimeAxis { get; } = [new Axis { IsVisible = false }];
    public Axis[] PercentAxis { get; } = [new Axis { MinLimit = 0, MaxLimit = 100, TextSize = 11 }];
    public Axis[] TorqueAxis { get; } = [new Axis { TextSize = 11 }];
    public Axis[] SpeedAxis { get; } = [new Axis { MinLimit = 0, TextSize = 11 }];

    public ObservableCollection<LapListItem> Laps { get; } = [];

    public ObservableCollection<ISeries> ComparisonSeries { get; } = [];
    public Axis[] ComparisonXAxes { get; } = [new Axis { Name = "Tur Mesafesi (%)", MinLimit = 0, MaxLimit = 100 }];
    public Axis[] ComparisonYAxes { get; } = [new Axis { Name = "Hız (km/s)", MinLimit = 0 }];

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }

    private string _statusText = "iRacing bekleniyor...";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    private string _trackName = "—";
    public string TrackName
    {
        get => _trackName;
        set => SetField(ref _trackName, value);
    }

    private string _carName = "—";
    public string CarName
    {
        get => _carName;
        set => SetField(ref _carName, value);
    }

    private string _speedText = "0";
    public string SpeedText { get => _speedText; set => SetField(ref _speedText, value); }

    private string _rpmText = "0";
    public string RpmText { get => _rpmText; set => SetField(ref _rpmText, value); }

    private string _gearText = "N";
    public string GearText { get => _gearText; set => SetField(ref _gearText, value); }

    private string _fuelText = "—";
    public string FuelText { get => _fuelText; set => SetField(ref _fuelText, value); }

    private string _currentLapTimeText = "--:--.---";
    public string CurrentLapTimeText { get => _currentLapTimeText; set => SetField(ref _currentLapTimeText, value); }

    private string _lastLapTimeText = "--:--.---";
    public string LastLapTimeText { get => _lastLapTimeText; set => SetField(ref _lastLapTimeText, value); }

    private string _bestLapTimeText = "--:--.---";
    public string BestLapTimeText { get => _bestLapTimeText; set => SetField(ref _bestLapTimeText, value); }

    private string _comparisonInfoText = "Karşılaştırmak için listeden iki tur seçin.";
    public string ComparisonInfoText { get => _comparisonInfoText; set => SetField(ref _comparisonInfoText, value); }

    public MainViewModel()
    {
        ThrottleBrakeSeries =
        [
            LineOf("Gaz", _throttleValues, ColorBlue),
            LineOf("Fren", _brakeValues, ColorRed),
        ];
        SteeringSeries = [LineOf("Direksiyon Torku (Nm)", _steeringTorqueValues, ColorOrange)];
        SpeedSeries = [LineOf("Hız (km/s)", _speedValues, ColorAqua)];
    }

    private static LineSeries<double> LineOf(string name, ObservableCollection<double> values, string colorHex)
    {
        var color = SKColor.Parse(colorHex);
        return new LineSeries<double>
        {
            Values = values,
            Name = name,
            GeometrySize = 0,
            LineSmoothness = 0.2,
            Fill = new SolidColorPaint(color.WithAlpha(30)),
            Stroke = new SolidColorPaint(color, 2),
        };
    }

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        StatusText = connected ? "Bağlı" : "iRacing bekleniyor...";
        if (!connected)
        {
            TrackName = "—";
            CarName = "—";
        }
    }

    public void SetSession(SessionSummary session)
    {
        TrackName = session.TrackName;
        CarName = session.CarName;
    }

    public void UpdateSnapshot(TelemetrySnapshot snapshot)
    {
        SpeedText = snapshot.SpeedKph.ToString("F0");
        RpmText = snapshot.Rpm.ToString("F0");
        GearText = snapshot.Gear switch { -1 => "R", 0 => "N", _ => snapshot.Gear.ToString() };
        FuelText = (snapshot.FuelLevelPct * 100).ToString("F0") + "%";
        CurrentLapTimeText = FormatLapTime(snapshot.LapCurrentTime);
        LastLapTimeText = FormatLapTime(snapshot.LapLastTime);
        BestLapTimeText = FormatLapTime(snapshot.LapBestTime);

        Push(_throttleValues, snapshot.Throttle * 100);
        Push(_brakeValues, snapshot.Brake * 100);
        Push(_steeringTorqueValues, snapshot.SteeringTorqueNm);
        Push(_speedValues, snapshot.SpeedKph);
    }

    private static void Push(ObservableCollection<double> values, double value)
    {
        values.Add(value);
        while (values.Count > MaxPoints) values.RemoveAt(0);
    }

    public void AddLap(LapListItem lap) => Laps.Insert(0, lap);

    public void LoadComparison(LapListItem lapA, IReadOnlyList<LapSamplePoint> samplesA,
        LapListItem lapB, IReadOnlyList<LapSamplePoint> samplesB)
    {
        ComparisonSeries.Clear();
        ComparisonSeries.Add(PointsOf($"Tur {lapA.LapNumber} ({FormatLapTime(lapA.LapTimeSeconds)})", samplesA, ColorBlue));
        ComparisonSeries.Add(PointsOf($"Tur {lapB.LapNumber} ({FormatLapTime(lapB.LapTimeSeconds)})", samplesB, ColorYellow));

        ComparisonInfoText =
            $"{lapA.TrackName} — Tur {lapA.LapNumber}: {FormatLapTime(lapA.LapTimeSeconds)}  |  " +
            $"Tur {lapB.LapNumber}: {FormatLapTime(lapB.LapTimeSeconds)}  |  " +
            $"Fark: {FormatLapTimeDelta(lapA.LapTimeSeconds - lapB.LapTimeSeconds)}";
    }

    private static LineSeries<ObservablePoint> PointsOf(string name, IReadOnlyList<LapSamplePoint> samples, string colorHex)
    {
        var color = SKColor.Parse(colorHex);
        return new LineSeries<ObservablePoint>
        {
            Values = samples.Select(s => new ObservablePoint(s.LapDistPct * 100, s.SpeedKph)).ToList(),
            Name = name,
            GeometrySize = 0,
            LineSmoothness = 0.2,
            Fill = null,
            Stroke = new SolidColorPaint(color, 2),
        };
    }

    private static string FormatLapTime(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds)) return "--:--.---";
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    private static string FormatLapTimeDelta(double seconds)
    {
        var sign = seconds >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(seconds):F3}s";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
