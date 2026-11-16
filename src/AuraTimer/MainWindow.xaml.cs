using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    private bool _widgetMode;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ThemeCombo != null) ThemeCombo.SelectedIndex = 0; // Midnight (Blue)
        };
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo?.SelectedItem is ComboBoxItem item)
            ApplyTheme(item.Tag?.ToString() ?? "Midnight");
    }

    private void PinToggle_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = PinToggle?.IsChecked == true;
    }

    private void WidgetMode_Click(object sender, RoutedEventArgs e)
    {
        _widgetMode = !_widgetMode;
        var visibility = _widgetMode ? Visibility.Collapsed : Visibility.Visible;
        if (PresetsCard != null) PresetsCard.Visibility = visibility;
        if (CustomCard != null) CustomCard.Visibility = visibility;
        if (WidgetModeButton != null) WidgetModeButton.Content = _widgetMode ? "Full mode" : "Widget mode";
        Width = _widgetMode ? 380 : 460;
        Height = _widgetMode ? 520 : 720;
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

    private void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case "Black":
                ApplyPalette("#000000", "#121212", "#0A0A0A", "#2A2A2A", "#F5F5F5", "#A3A3A3", "#2A2A2A", "#2A2A2A", "#3A3A3A", "#2A2A2A", "#7C5CFF", "#22D3EE");
                break;
            case "Light":
                ApplyPalette("#F3F6FB", "#FFFFFF", "#F8FAFC", "#D5DEEA", "#0F172A", "#64748B", "#E2E8F0", "#E8EEF8", "#DCE6F5", "#E2E8F0", "#6C5CE7", "#0EA5E9");
                break;
            case "Orange":
                ApplyPalette("#140D08", "#1F140C", "#120B07", "#3A2818", "#FFF3E8", "#D6BFA8", "#3A2818", "#3A2818", "#4A3320", "#3A2818", "#F97316", "#FDBA74");
                break;
            case "Red":
                ApplyPalette("#14090B", "#1F1014", "#12080A", "#3A1E25", "#FFECEE", "#D8B0B7", "#3A1E25", "#3A1E25", "#4A2730", "#3A1E25", "#EF4444", "#F87171");
                break;
            case "Green":
                ApplyPalette("#07120D", "#0E1B14", "#06100B", "#1E3528", "#E9FFF4", "#A9D2BC", "#1E3528", "#1E3528", "#294538", "#1E3528", "#10B981", "#34D399");
                break;
            case "Purple":
                ApplyPalette("#100A18", "#171026", "#0D0814", "#30214A", "#F5EEFF", "#C8B3E6", "#30214A", "#30214A", "#3C2A5C", "#30214A", "#A855F7", "#C084FC");
                break;
            case "Midnight":
            default:
                ApplyPalette("#0F172A", "#1E293B", "#0B1220", "#334155", "#E5E7EB", "#94A3B8", "#334155", "#334155", "#475569", "#334155", "#7C5CFF", "#22D3EE");
                break;
        }
        Background = (Brush)Application.Current.Resources["BgBrush"];
        Foreground = (Brush)Application.Current.Resources["TextBrush"];
    }

    private void ApplyPalette(string bg, string card, string input, string border, string text, string subtle, string ghost, string chip, string chipHover, string progress, string accent, string accent2)
    {
        SetBrush("BgBrush", bg);
        SetBrush("CardBrush", card);
        SetBrush("InputBrush", input);
        SetBrush("BorderBrushColor", border);
        SetBrush("TextBrush", text);
        SetBrush("SubtleTextBrush", subtle);
        SetBrush("GhostBrush", ghost);
        SetBrush("ChipBrush", chip);
        SetBrush("ChipHoverBrush", chipHover);
        SetBrush("ProgressTrackBrush", progress);
        SetBrush("AccentBrush", accent);
        SetBrush("Accent2Brush", accent2);
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
            if (entry.IsCompleted) { entry.Reset(); entry.Start(); return; }
            if (entry.IsRunning) entry.Pause(); else entry.Start();
            UpdateCardState(entry);
        };
        reset.Click += (_, _) => { entry.Reset(); UpdateCardState(entry); };
        remove.Click += (_, _) =>
        {
            entry.Dispose();
            _timers.Remove(entry);
            TimersPanel.Children.Remove(card);
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
    }

    private void OnEntryCompleted(TimerEntry entry)
    {
        if (entry.TimeText != null) entry.TimeText.Text = "00:00";
        if (entry.StatusText != null) { entry.StatusText.Text = "Time's up"; entry.StatusText.Foreground = (Brush)FindResource("SuccessBrush"); }
        if (entry.StartPauseButton != null) entry.StartPauseButton.Content = "Restart";
        UpdateProgress(entry);
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
            var win = new Window
            {
                Title = "Aura Timer",
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = true,
                Background = (Brush)FindResource("CardBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = "⏰  Timer complete", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) },
                        new TextBlock { Text = string.IsNullOrWhiteSpace(label) ? "Your countdown has finished." : $"{label} is done.", FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14) },
                        new Button { Content = "OK", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, IsDefault = true }
                    }
                }
            };
            if (win.Content is StackPanel sp && sp.Children[2] is Button ok)
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

    public Border? Card { get; set; }
    public TextBlock? TimeText { get; set; }
    public TextBlock? StatusText { get; set; }
    public Border? ProgressFg { get; set; }
    public Border? ProgressBg { get; set; }
    public Button? StartPauseButton { get; set; }

    public event Action? Tick;
    public event Action? Completed;

    public TimerEntry(TimeSpan duration, string label)
    {
        Duration = duration;
        Remaining = duration;
        Label = label;
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
        _timer.Stop();
    }
}
