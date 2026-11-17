using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.WinUI.Notifications;

namespace AuraTimer;

public partial class MainWindow : Window
{
    private readonly List<TimerEntry> _timers = new();
    private AppSettings _settings = new();
    private bool _widgetMode;
    private string _baseTheme = "White";
    private string _accent = "Blue";
    private bool _quickEditMode;
    private bool _loaded;
    private bool _suppressSizeSave;
    private int _ticksSinceSave;

    public MainWindow()
    {
        InitializeComponent();

        _settings = SettingsService.Load();
        _baseTheme = _settings.BaseTheme == "Black" ? "Black" : "White";
        _accent = string.IsNullOrWhiteSpace(_settings.Accent) ? "Blue" : _settings.Accent;
        _widgetMode = _settings.WidgetMode;

        ApplyAppearance();

        Loaded += (_, _) =>
        {
            ApplyWidgetMode(_widgetMode, resize: false);
            RestoreWindowSize();
            RestoreTimers();
            RenderQuickChips();
            _loaded = true;
        };

        Closing += (_, _) => SaveState();
        SizeChanged += (_, _) => SaveWindowSize();
    }

    private void BaseTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            _baseTheme = tag;
            ApplyAppearance();
            SaveState();
        }
    }

    private void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            _accent = tag;
            ApplyAppearance();
            SaveState();
        }
    }

    private void PinToggle_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = PinToggle?.IsChecked == true;
        SaveState();
    }

    private void WidgetMode_Click(object sender, RoutedEventArgs e)
    {
        _widgetMode = !_widgetMode;
        ApplyWidgetMode(_widgetMode, resize: true);
        SaveState();
    }

    private void ApplyWidgetMode(bool widget, bool resize)
    {
        var setupVisibility = widget ? Visibility.Collapsed : Visibility.Visible;
        if (PresetsCard != null) PresetsCard.Visibility = setupVisibility;
        if (CustomCard != null) CustomCard.Visibility = setupVisibility;
        if (QuickStartCard != null) QuickStartCard.Visibility = widget ? Visibility.Visible : Visibility.Collapsed;
        if (WidgetModeButton != null) WidgetModeButton.Content = widget ? "Full mode" : "Widget mode";
        if (!resize) return;

        _suppressSizeSave = true;
        if (widget)
        {
            _settings.FullWidth = Width;
            _settings.FullHeight = Height;
            Width = _settings.WidgetWidth;
            Height = _settings.WidgetHeight;
        }
        else
        {
            _settings.WidgetWidth = Width;
            _settings.WidgetHeight = Height;
            Width = _settings.FullWidth;
            Height = _settings.FullHeight;
        }
        _suppressSizeSave = false;
    }

    private void RestoreWindowSize()
    {
        _suppressSizeSave = true;
        if (_widgetMode)
        {
            if (_settings.WidgetWidth >= MinWidth) Width = _settings.WidgetWidth;
            if (_settings.WidgetHeight >= MinHeight) Height = _settings.WidgetHeight;
        }
        else
        {
            if (_settings.FullWidth >= MinWidth) Width = _settings.FullWidth;
            if (_settings.FullHeight >= MinHeight) Height = _settings.FullHeight;
        }
        _suppressSizeSave = false;
    }

    private void SaveWindowSize()
    {
        if (!_loaded || _suppressSizeSave) return;
        if (_widgetMode)
        {
            _settings.WidgetWidth = Width;
            _settings.WidgetHeight = Height;
        }
        else
        {
            _settings.FullWidth = Width;
            _settings.FullHeight = Height;
        }
        SaveState();
    }

    private void SaveState()
    {
        if (!_loaded) return;
        _settings.BaseTheme = _baseTheme;
        _settings.Accent = _accent;
        _settings.WidgetMode = _widgetMode;
        _settings.PinOnTop = PinToggle?.IsChecked == true;
        if (QuickChipsPanel?.Tag is List<int> list) _settings.WidgetQuickSeconds = new List<int>(list);
        _settings.Timers = _timers.Where(t => !t.IsDisposed).Select(t => new SavedTimer
        {
            DurationSeconds = t.Duration.TotalSeconds,
            RemainingSeconds = t.Remaining.TotalSeconds,
            Label = t.Label,
            IsCompleted = t.IsCompleted,
            WasRunning = t.IsRunning
        }).ToList();
        SettingsService.Save(_settings);
    }

    private void QuickEdit_Click(object sender, RoutedEventArgs e)
    {
        _quickEditMode = !_quickEditMode;
        if (QuickEditPanel != null) QuickEditPanel.Visibility = _quickEditMode ? Visibility.Visible : Visibility.Collapsed;
        if (QuickEditButton != null) QuickEditButton.Content = _quickEditMode ? "Done" : "Edit";
        RenderQuickChips();
    }

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(QuickMinutesBox.Text, out var minutes) || minutes <= 0) return;
        GetQuickSeconds().Add(minutes * 60);
        GetQuickSeconds().Sort();
        RenderQuickChips();
        SaveState();
    }

    private void QuickReset_Click(object sender, RoutedEventArgs e)
    {
        var list = GetQuickSeconds();
        list.Clear();
        list.AddRange(new[] { 20 * 60, 25 * 60, 30 * 60 });
        RenderQuickChips();
        SaveState();
    }

    private List<int> GetQuickSeconds()
    {
        if (QuickChipsPanel?.Tag is List<int> list) return list;
        var created = (_settings.WidgetQuickSeconds?.Count > 0)
            ? new List<int>(_settings.WidgetQuickSeconds)
            : new List<int> { 20 * 60, 25 * 60, 30 * 60 };
        created.Sort();
        if (QuickChipsPanel != null) QuickChipsPanel.Tag = created;
        return created;
    }

    private void RenderQuickChips()
    {
        if (QuickChipsPanel == null) return;
        QuickChipsPanel.Children.Clear();
        foreach (var seconds in GetQuickSeconds())
        {
            var label = FormatDuration(TimeSpan.FromSeconds(seconds));
            if (_quickEditMode) label += "  ✕";
            var chip = new Button
            {
                Content = label,
                Style = (Style)FindResource("ChipButton"),
                Tag = seconds
            };
            chip.Click += (_, _) =>
            {
                if (_quickEditMode)
                {
                    GetQuickSeconds().Remove(seconds);
                    RenderQuickChips();
                    SaveState();
                }
                else
                {
                    AddTimer(TimeSpan.FromSeconds(seconds), FormatDuration(TimeSpan.FromSeconds(seconds)));
                }
            };
            QuickChipsPanel.Children.Add(chip);
        }
    }

    private void RestoreTimers()
    {
        if (_settings.Timers == null) return;
        foreach (var saved in _settings.Timers)
        {
            if (saved.DurationSeconds < 1) continue;
            var duration = TimeSpan.FromSeconds(saved.DurationSeconds);
            var remaining = TimeSpan.FromSeconds(Math.Clamp(saved.RemainingSeconds, 0, saved.DurationSeconds));
            var entry = new TimerEntry(duration, string.IsNullOrWhiteSpace(saved.Label) ? FormatDuration(duration) : saved.Label, remaining, saved.IsCompleted);
            _timers.Add(entry);
            var card = CreateCard(entry);
            entry.Card = card;
            TimersPanel.Children.Add(card);
            if (!entry.IsCompleted && saved.WasRunning)
            {
                entry.Start();
            }
            UpdateCardState(entry);
        }
    }

    private static void SetBrush(string key, string color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush b)
        {
            if (b.IsFrozen) Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            else b.Color = (Color)ColorConverter.ConvertFromString(color);
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }

    private void ApplyAppearance()
    {
        if (_baseTheme == "White")
        {
            SetBrush("BgBrush", "#FFFFFF");
            SetBrush("CardBrush", "#F6F7F9");
            SetBrush("InputBrush", "#FFFFFF");
            SetBrush("BorderBrushColor", "#D9DEE7");
            SetBrush("TextBrush", "#111827");
            SetBrush("SubtleTextBrush", "#6B7280");
            SetBrush("GhostBrush", "#E5E7EB");
            SetBrush("ChipBrush", "#E9EDF3");
            SetBrush("ChipHoverBrush", "#DDE5F0");
            SetBrush("ProgressTrackBrush", "#E5E7EB");
        }
        else
        {
            SetBrush("BgBrush", "#000000");
            SetBrush("CardBrush", "#121212");
            SetBrush("InputBrush", "#0A0A0A");
            SetBrush("BorderBrushColor", "#2A2A2A");
            SetBrush("TextBrush", "#F5F5F5");
            SetBrush("SubtleTextBrush", "#A3A3A3");
            SetBrush("GhostBrush", "#242424");
            SetBrush("ChipBrush", "#1E1E1E");
            SetBrush("ChipHoverBrush", "#2C2C2C");
            SetBrush("ProgressTrackBrush", "#2A2A2A");
        }

        switch (_accent)
        {
            case "Orange":
                SetBrush("AccentBrush", "#F97316"); SetBrush("Accent2Brush", "#FDBA74"); break;
            case "Red":
                SetBrush("AccentBrush", "#EF4444"); SetBrush("Accent2Brush", "#F87171"); break;
            case "Green":
                SetBrush("AccentBrush", "#10B981"); SetBrush("Accent2Brush", "#34D399"); break;
            case "Purple":
                SetBrush("AccentBrush", "#A855F7"); SetBrush("Accent2Brush", "#C084FC"); break;
            case "Teal":
                SetBrush("AccentBrush", "#14B8A6"); SetBrush("Accent2Brush", "#5EEAD4"); break;
            case "Blue":
            default:
                SetBrush("AccentBrush", "#2563EB"); SetBrush("Accent2Brush", "#60A5FA"); break;
        }

        Background = (Brush)Application.Current.Resources["BgBrush"];
        Foreground = (Brush)Application.Current.Resources["TextBrush"];

        if (BlackModeButton != null) BlackModeButton.Opacity = _baseTheme == "Black" ? 1.0 : 0.6;
        if (WhiteModeButton != null) WhiteModeButton.Opacity = _baseTheme == "White" ? 1.0 : 0.6;
        if (AccentPanel != null)
        {
            foreach (var child in AccentPanel.Children)
            {
                if (child is Button b && b.Tag is string tag)
                    b.Opacity = string.Equals(tag, _accent, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.6;
            }
        }

        if (PinToggle != null)
        {
            PinToggle.IsChecked = _settings.PinOnTop;
            Topmost = _settings.PinOnTop;
        }

        // Keep runtime-created timer cards in sync with the new palette.
        foreach (var timer in _timers)
        {
            if (timer.Card != null) timer.Card.Background = (Brush)Application.Current.Resources["CardBrush"];
            if (timer.ProgressBg != null) timer.ProgressBg.Background = (Brush)Application.Current.Resources["ProgressTrackBrush"];
            if (timer.ProgressFg != null) timer.ProgressFg.Background = (Brush)Application.Current.Resources["Accent2Brush"];
            UpdateCardState(timer);
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out var minutes))
        {
            AddTimer(TimeSpan.FromMinutes(minutes), minutes == 60 ? "1 hour" : $"{minutes} min");
        }
    }

    private void AddCustom_Click(object sender, RoutedEventArgs e)
    {
        int h = ParseInt(HoursBox.Text);
        int m = ParseInt(MinutesBox.Text);
        int s = ParseInt(SecondsBox.Text);
        var ts = new TimeSpan(h, m, s);
        if (ts.TotalSeconds < 1)
        {
            MessageBox.Show(this, "Please enter a duration greater than 0.", "Aura Timer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var label = string.IsNullOrWhiteSpace(LabelBox.Text) ? FormatDuration(ts) : LabelBox.Text.Trim();
        AddTimer(ts, label);
        LabelBox.Text = string.Empty;
    }

    private static int ParseInt(string? text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : 0;

    private void AddTimer(TimeSpan duration, string label)
    {
        var entry = new TimerEntry(duration, label);
        _timers.Add(entry);
        var card = CreateCard(entry);
        entry.Card = card;
        TimersPanel.Children.Insert(0, card);
        entry.Start();
        SaveState();
    }

    private Border CreateCard(TimerEntry entry)
    {
        var title = new TextBlock { Text = entry.Label, FontSize = 16, FontWeight = FontWeights.SemiBold };
        var status = new TextBlock { Text = "Running", Foreground = (Brush)FindResource("SubtleTextBrush"), FontSize = 12, Margin = new Thickness(0,2,0,0) };
        var titleStack = new StackPanel();
        titleStack.Children.Add(title);
        titleStack.Children.Add(status);

        var timeText = new TextBlock
        {
            Text = FormatTime(entry.Remaining),
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(titleStack, 0);
        Grid.SetColumn(timeText, 1);
        headerGrid.Children.Add(titleStack);
        headerGrid.Children.Add(timeText);

        var progressBg = new Border { Background = (Brush)Application.Current.Resources["ProgressTrackBrush"], CornerRadius = new CornerRadius(4), Height = 8, Margin = new Thickness(0,12,0,12) };
        var progressFg = new Border { Background = (Brush)Application.Current.Resources["Accent2Brush"], CornerRadius = new CornerRadius(4), Height = 8, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        var progressGrid = new Grid();
        progressGrid.Children.Add(progressBg);
        progressGrid.Children.Add(progressFg);

        var startPause = new Button { Content = "Pause", Style = (Style)FindResource("PrimaryButton"), MinWidth = 90, Margin = new Thickness(0,0,8,0) };
        var reset = new Button { Content = "Reset", Style = (Style)FindResource("GhostButton"), MinWidth = 80, Margin = new Thickness(0,0,8,0) };
        var remove = new Button { Content = "Remove", Style = (Style)FindResource("GhostButton"), MinWidth = 80, Foreground = Brushes.White };
        remove.Background = (Brush)Application.Current.Resources["DangerBrush"];

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(startPause);
        buttons.Children.Add(reset);
        buttons.Children.Add(remove);

        var stack = new StackPanel();
        stack.Children.Add(headerGrid);
        stack.Children.Add(progressGrid);
        stack.Children.Add(buttons);

        var card = new Border
        {
            Background = (Brush)FindResource("CardBrush"),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            Margin = new Thickness(0,0,0,10),
            Child = stack
        };

        entry.TimeText = timeText;
        entry.StatusText = status;
        entry.ProgressFg = progressFg;
        entry.ProgressBg = progressBg;
        entry.StartPauseButton = startPause;

        startPause.Click += (_, _) =>
        {
            if (entry.IsCompleted) { entry.Reset(); entry.Start(); UpdateCardState(entry); SaveState(); return; }
            if (entry.IsRunning) entry.Pause(); else entry.Start();
            UpdateCardState(entry);
            SaveState();
        };
        reset.Click += (_, _) => { entry.Reset(); UpdateCardState(entry); SaveState(); };
        remove.Click += (_, _) =>
        {
            entry.Dispose();
            _timers.Remove(entry);
            TimersPanel.Children.Remove(card);
            SaveState();
        };

        progressBg.SizeChanged += (_, _) => UpdateProgress(entry);
        entry.Tick += () => Dispatcher.Invoke(() => OnEntryTick(entry));
        entry.Completed += () => Dispatcher.Invoke(() => OnEntryCompleted(entry));
        UpdateCardState(entry);
        return card;
    }

    private void OnEntryTick(TimerEntry entry)
    {
        if (entry.TimeText != null) entry.TimeText.Text = FormatTime(entry.Remaining);
        UpdateProgress(entry);

        _ticksSinceSave++;
        if (_ticksSinceSave >= 15)
        {
            _ticksSinceSave = 0;
            SaveState();
        }
    }

    private void OnEntryCompleted(TimerEntry entry)
    {
        if (entry.TimeText != null) entry.TimeText.Text = "00:00";
        if (entry.StatusText != null) { entry.StatusText.Text = "Time's up"; entry.StatusText.Foreground = (Brush)FindResource("SuccessBrush"); }
        if (entry.StartPauseButton != null) entry.StartPauseButton.Content = "Restart";
        UpdateProgress(entry);
        SaveState();
        PlayChime();
        ShowToast(entry.Label);
        ShowInAppAlert(entry.Label);
    }

    private void UpdateCardState(TimerEntry entry)
    {
        if (entry.IsCompleted)
        {
            if (entry.StatusText != null) { entry.StatusText.Text = "Time's up"; entry.StatusText.Foreground = (Brush)FindResource("SuccessBrush"); }
            if (entry.StartPauseButton != null) entry.StartPauseButton.Content = "Restart";
        }
        else if (entry.IsRunning)
        {
            if (entry.StatusText != null) { entry.StatusText.Text = "Running"; entry.StatusText.Foreground = (Brush)FindResource("SubtleTextBrush"); }
            if (entry.StartPauseButton != null) entry.StartPauseButton.Content = "Pause";
        }
        else
        {
            if (entry.StatusText != null) { entry.StatusText.Text = "Paused"; entry.StatusText.Foreground = (Brush)FindResource("SubtleTextBrush"); }
            if (entry.StartPauseButton != null) entry.StartPauseButton.Content = "Start";
        }
        if (entry.TimeText != null) entry.TimeText.Text = FormatTime(entry.Remaining);
        UpdateProgress(entry);
    }

    private void UpdateProgress(TimerEntry entry)
    {
        if (entry.ProgressBg == null || entry.ProgressFg == null) return;
        var total = Math.Max(1, entry.Duration.TotalSeconds);
        var remaining = Math.Max(0, entry.Remaining.TotalSeconds);
        var ratio = 1.0 - (remaining / total);
        if (entry.IsCompleted) ratio = 1.0;
        var w = entry.ProgressBg.ActualWidth;
        if (w <= 0) w = 360; // fallback before layout
        entry.ProgressFg.Width = Math.Max(0, Math.Min(w, w * ratio));
    }

    private static string FormatTime(TimeSpan ts)
    {
        ts = ts < TimeSpan.Zero ? TimeSpan.Zero : ts;
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return $"{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1 && ts.Minutes == 0 && ts.Seconds == 0) return ts.TotalHours == 1 ? "1 hour" : $"{(int)ts.TotalHours} hours";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.Seconds == 0) return $"{ts.Minutes} min";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    private void PlayChime()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "chime.wav");
            if (File.Exists(path))
            {
                var player = new SoundPlayer(path);
                player.Play();
                return;
            }
        }
        catch { }
        try { SystemSounds.Exclamation.Play(); } catch { }
    }

    private void ShowToast(string label)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("Timer complete")
                .AddText(string.IsNullOrWhiteSpace(label) ? "Your countdown has finished." : $"{label} is done.")
                .Show();
        }
        catch { /* toast may be unavailable in some environments */ }
    }

    private void ShowInAppAlert(string label)
    {
        try
        {
            var message = string.IsNullOrWhiteSpace(label) ? "Your countdown has finished." : $"{label} is done.";

            var ok = new Button
            {
                Content = "OK",
                Style = (Style)FindResource("PrimaryButton"),
                MinWidth = 96,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };

            var root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new TextBlock
            {
                Text = "⏰  Timer complete",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var body = new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(ok, 2);
            Grid.SetRow(heading, 0);
            Grid.SetRow(body, 1);
            root.Children.Add(heading);
            root.Children.Add(body);
            root.Children.Add(ok);

            var win = new Window
            {
                Title = "Aura Timer",
                Width = 400,
                MinWidth = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Background = (Brush)Application.Current.Resources["CardBrush"],
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                Content = root
            };

            ok.Click += (_, _) => win.Close();
            win.Show();
            win.Activate();
        }
        catch { }
    }
}

public sealed class TimerEntry : IDisposable
{
    private readonly DispatcherTimer _timer;
    public TimeSpan Duration { get; }
    public TimeSpan Remaining { get; private set; }
    public string Label { get; }
    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsDisposed { get; private set; }

    public Border? Card { get; set; }
    public TextBlock? TimeText { get; set; }
    public TextBlock? StatusText { get; set; }
    public Border? ProgressFg { get; set; }
    public Border? ProgressBg { get; set; }
    public Button? StartPauseButton { get; set; }

    public event Action? Tick;
    public event Action? Completed;

    public TimerEntry(TimeSpan duration, string label, TimeSpan? remaining = null, bool isCompleted = false)
    {
        Duration = duration;
        Remaining = remaining ?? duration;
        if (Remaining < TimeSpan.Zero) Remaining = TimeSpan.Zero;
        if (Remaining > duration) Remaining = duration;
        Label = label;
        IsCompleted = isCompleted;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
    }

    private void OnTick()
    {
        if (!IsRunning || IsCompleted) return;
        Remaining -= TimeSpan.FromSeconds(1);
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            IsCompleted = true;
            IsRunning = false;
            _timer.Stop();
            Tick?.Invoke();
            Completed?.Invoke();
            return;
        }
        Tick?.Invoke();
    }

    public void Start()
    {
        if (IsCompleted) return;
        IsRunning = true;
        _timer.Start();
    }

    public void Pause()
    {
        IsRunning = false;
        _timer.Stop();
    }

    public void Reset()
    {
        IsRunning = false;
        IsCompleted = false;
        Remaining = Duration;
        _timer.Stop();
    }

    public void Dispose()
    {
        IsDisposed = true;
        _timer.Stop();
    }
}
