using System.Collections.Generic;

namespace AuraTimer;

public sealed class AppSettings
{
    public string BaseTheme { get; set; } = "White";
    public string Accent { get; set; } = "Blue";
    public bool WidgetMode { get; set; }
    public bool PinOnTop { get; set; }

    public double FullWidth { get; set; } = 460;
    public double FullHeight { get; set; } = 720;
    public double WidgetWidth { get; set; } = 380;
    public double WidgetHeight { get; set; } = 640;

    public List<int> WidgetQuickSeconds { get; set; } = new() { 20 * 60, 25 * 60, 30 * 60 };
    public List<SavedTimer> Timers { get; set; } = new();
}

public sealed class SavedTimer
{
    public double DurationSeconds { get; set; }
    public double RemainingSeconds { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool WasRunning { get; set; }
}
