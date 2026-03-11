using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MoodyClone.Helpers;
using MoodyClone.Models;
using MoodyClone.Services;

namespace MoodyClone.Views;

public partial class PrompterWindow : Window
{
    private readonly string _scriptText;
    private readonly AppSettings _settings;
    private readonly AudioService _audioService;
    private readonly SpeechService _speechService;

    // Scroll state
    private double _scrollPosition;
    private double _basePixelsPerSecond = 30.0; // Notch default: slower
    private double _currentSpeedMultiplier = 1.0;
    private bool _isPaused;
    private bool _isHoverPaused;
    private bool _isVoicePaused;
    private bool _isVoiceMode;
    private DateTime _lastFrameTime;

    // Voice scroll target
    private double _voiceTargetOffset = -1;

    // Mode
    private PrompterMode _currentMode;
    private bool _isClosing;

    // Notch dimensions (compact)
    private const double NotchWidth = 460;
    private const double NotchHeight = 160;
    private const double NotchFontSize = 22;

    public PrompterWindow(string scriptText, AppSettings settings)
    {
        InitializeComponent();

        _scriptText = scriptText;
        _settings = settings;
        _currentMode = settings.PrompterMode;
        _currentSpeedMultiplier = GetMultiplierFromStep(settings.ScrollSpeed);

        _audioService = new AudioService();
        _speechService = new SpeechService();

        // Set text
        ScriptText.Text = scriptText;

        // Apply mode first (sets dimensions + position)
        ApplyMode(_currentMode);
        UpdateSpeedButtonStates();

        // Apply text color
        ApplyTextColor(settings.TextColor);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply screen capture invisibility
        ApplyDisplayAffinity();

        // Adjust padding so first line centers on the reading guide
        var halfHeight = TextScroller.ActualHeight / 2;
        TopPadding.Height = halfHeight;
        BottomPadding.Height = halfHeight;

        // Start audio monitoring
        StartAudioMonitoring();

        // Subscribe to rendering for smooth scroll
        _lastFrameTime = DateTime.Now;
        CompositionTarget.Rendering += OnRendering;

        this.Focus();
    }

    // ===================== SCREEN CAPTURE =====================

    private void ApplyDisplayAffinity()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        }
        catch { }
    }

    // ===================== HOVER CONTROLS =====================

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _isHoverPaused = true;
        UpdatePauseVisual();
        FadeControls(true);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _isHoverPaused = false;
        UpdatePauseVisual();
        FadeControls(false);
    }

    private void FadeControls(bool show)
    {
        HoverControls.IsHitTestVisible = show;
        var anim = new DoubleAnimation(show ? 1.0 : 0.0, TimeSpan.FromMilliseconds(150));
        HoverControls.BeginAnimation(OpacityProperty, anim);

        // Also show voice beam when controls are visible
        var beamAnim = new DoubleAnimation(show ? 0.85 : 0.0, TimeSpan.FromMilliseconds(150));
        VoiceBeam.BeginAnimation(OpacityProperty, beamAnim);
    }

    // ===================== SMOOTH SCROLL ENGINE =====================

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_isClosing) return;

        var now = DateTime.Now;
        var deltaSeconds = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;
        if (deltaSeconds > 0.1) deltaSeconds = 0.016;

        UpdateVoiceBeam();

        bool effectivelyPaused = _isPaused || _isHoverPaused || (_isVoiceMode && _isVoicePaused);

        if (_isVoiceMode && _voiceTargetOffset >= 0)
        {
            double diff = _voiceTargetOffset - _scrollPosition;
            if (Math.Abs(diff) > 0.5)
                _scrollPosition += diff * 0.08;
            else
                _scrollPosition = _voiceTargetOffset;
            TextScroller.ScrollToVerticalOffset(_scrollPosition);
        }
        else if (!effectivelyPaused)
        {
            double pxThisFrame = _basePixelsPerSecond * _currentSpeedMultiplier * deltaSeconds;
            _scrollPosition += pxThisFrame;
            var max = TextScroller.ScrollableHeight;
            if (_scrollPosition > max)
            {
                _scrollPosition = max;
                // Show end indicator
                if (EndIndicator.Opacity < 1)
                {
                    var a = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(400));
                    EndIndicator.BeginAnimation(OpacityProperty, a);
                }
            }
            TextScroller.ScrollToVerticalOffset(_scrollPosition);
        }
    }

    // ===================== VOICE BEAM =====================

    private void StartAudioMonitoring()
    {
        var mics = AudioService.GetMicrophones();
        int deviceIndex = 0;
        if (!string.IsNullOrEmpty(_settings.PreferredMicrophone))
        {
            var pref = mics.FirstOrDefault(m => m.Name == _settings.PreferredMicrophone);
            if (pref.Name != null) deviceIndex = pref.Index;
        }
        _audioService.StartMonitoring(deviceIndex);
    }

    private void UpdateVoiceBeam()
    {
        try
        {
            double rms = _audioService.CurrentRms;
            double level = Math.Min(rms / 0.12, 1.0);
            double target = level * (this.ActualWidth * 0.75);
            double cur = VoiceBeam.Width;
            VoiceBeam.Width = double.IsNaN(cur) ? target : cur + (target - cur) * 0.3;
        }
        catch { }
    }

    // ===================== VOICE SCROLLING =====================

    private void VoiceToggle_Click(object sender, RoutedEventArgs e)
    {
        _isVoiceMode = !_isVoiceMode;

        if (_isVoiceMode)
        {
            VoiceToggle.Content = "🎤✓";
            VoiceToggle.Background = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58));
            VoiceToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));

            _speechService.LoadScript(_scriptText);
            _speechService.WordRecognized += OnWordRecognized;
            _speechService.SpeechPaused += OnSpeechPaused;
            _speechService.Start();
            _isVoicePaused = false;
        }
        else
        {
            VoiceToggle.Content = "🎤";
            VoiceToggle.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            VoiceToggle.Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));

            _speechService.WordRecognized -= OnWordRecognized;
            _speechService.SpeechPaused -= OnSpeechPaused;
            _speechService.Stop();
            _voiceTargetOffset = -1;
        }
    }

    private void OnWordRecognized(int wordIndex)
    {
        Dispatcher.Invoke(() =>
        {
            _isVoicePaused = false;
            UpdatePauseVisual();
            if (_speechService.Words.Length > 0)
            {
                double progress = (double)wordIndex / _speechService.Words.Length;
                _voiceTargetOffset = progress * TextScroller.ScrollableHeight;
            }
        });
    }

    private void OnSpeechPaused()
    {
        Dispatcher.Invoke(() =>
        {
            _isVoicePaused = true;
            UpdatePauseVisual();
        });
    }

    // ===================== MODE TOGGLE =====================

    private void ApplyMode(PrompterMode mode)
    {
        _currentMode = mode;

        if (mode == PrompterMode.Notch)
        {
            this.Width = NotchWidth;
            this.Height = NotchHeight;
            MainBorder.CornerRadius = new CornerRadius(0, 0, 18, 18);
            MainBorder.Background = new SolidColorBrush(Colors.Black);
            ScriptText.FontSize = NotchFontSize;
            ScriptText.LineHeight = 30;

            // Center top of primary screen
            var screen = SystemParameters.PrimaryScreenWidth;
            this.Left = (screen - this.Width) / 2;
            this.Top = 0;

            ModeToggle.Content = "🪟";
            ModeToggle.ToolTip = "Switch to floating";
            ResizeGrip.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Floating: remember saved size or default
            this.Width = Math.Max(_settings.PrompterWidth, 500);
            this.Height = Math.Max(_settings.PrompterHeight, 280);
            MainBorder.CornerRadius = new CornerRadius(14);
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x10, 0x10, 0x12));
            ScriptText.FontSize = _settings.FontSize;
            ScriptText.LineHeight = _settings.FontSize * 1.35;

            var wa = SystemParameters.WorkArea;
            this.Left = (wa.Width - this.Width) / 2;
            this.Top = (wa.Height - this.Height) / 2;

            ModeToggle.Content = "📌";
            ModeToggle.ToolTip = "Switch to notch";
            ResizeGrip.Visibility = Visibility.Visible;
        }
    }

    private void ModeToggle_Click(object sender, RoutedEventArgs e)
    {
        var next = _currentMode == PrompterMode.Notch ? PrompterMode.Floating : PrompterMode.Notch;
        ApplyMode(next);
        _settings.PrompterMode = next;
        _settings.Save();
    }

    // ===================== SPEED =====================

    private static double GetMultiplierFromStep(SpeedStep step) => step switch
    {
        SpeedStep.x1 => 1.0,
        SpeedStep.x1_5 => 1.5,
        SpeedStep.x2 => 2.0,
        SpeedStep.x3 => 3.0,
        _ => 1.0
    };

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double mult))
        {
            _currentSpeedMultiplier = mult;
            UpdateSpeedButtonStates();
        }
    }

    private void UpdateSpeedButtonStates()
    {
        var active = (Style)FindResource("ActiveSpeedBtn");
        var normal = (Style)FindResource("OverlayBtn");
        Speed1.Style = _currentSpeedMultiplier == 1.0 ? active : normal;
        Speed15.Style = _currentSpeedMultiplier == 1.5 ? active : normal;
        Speed2.Style = _currentSpeedMultiplier == 2.0 ? active : normal;
        Speed3.Style = _currentSpeedMultiplier == 3.0 ? active : normal;
    }

    private void IncreaseSpeed()
    {
        if (_currentSpeedMultiplier < 1.5) _currentSpeedMultiplier = 1.5;
        else if (_currentSpeedMultiplier < 2.0) _currentSpeedMultiplier = 2.0;
        else _currentSpeedMultiplier = 3.0;
        UpdateSpeedButtonStates();
    }

    private void DecreaseSpeed()
    {
        if (_currentSpeedMultiplier > 2.0) _currentSpeedMultiplier = 2.0;
        else if (_currentSpeedMultiplier > 1.5) _currentSpeedMultiplier = 1.5;
        else _currentSpeedMultiplier = 1.0;
        UpdateSpeedButtonStates();
    }

    // ===================== TEXT COLOR =====================

    private void ApplyTextColor(TextColorOption color)
    {
        ScriptText.Foreground = color switch
        {
            TextColorOption.Yellow => new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x0A)),
            TextColorOption.Green => new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)),
            _ => Brushes.White
        };
    }

    // ===================== PAUSE =====================

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        UpdatePauseVisual();
    }

    private void UpdatePauseVisual()
    {
        bool paused = _isPaused || _isHoverPaused || (_isVoiceMode && _isVoicePaused);
        PauseIndicator.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== KEYBOARD =====================

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                TogglePause();
                e.Handled = true;
                break;
            case Key.Up:
                IncreaseSpeed();
                e.Handled = true;
                break;
            case Key.Down:
                DecreaseSpeed();
                e.Handled = true;
                break;
            case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.Control:
            case Key.Add when Keyboard.Modifiers == ModifierKeys.Control:
                if (ScriptText.FontSize < 96) { ScriptText.FontSize += 2; ScriptText.LineHeight = ScriptText.FontSize * 1.35; }
                e.Handled = true;
                break;
            case Key.OemMinus when Keyboard.Modifiers == ModifierKeys.Control:
            case Key.Subtract when Keyboard.Modifiers == ModifierKeys.Control:
                if (ScriptText.FontSize > 12) { ScriptText.FontSize -= 2; ScriptText.LineHeight = ScriptText.FontSize * 1.35; }
                e.Handled = true;
                break;
            case Key.Escape:
                StopPrompter();
                e.Handled = true;
                break;
        }
    }

    // ===================== DRAG & RESIZE =====================

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentMode == PrompterMode.Floating)
            this.DragMove();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double w = this.Width + e.HorizontalChange;
        double h = this.Height + e.VerticalChange;
        if (w >= 400) this.Width = w;
        if (h >= 200) this.Height = h;
        _settings.PrompterWidth = this.Width;
        _settings.PrompterHeight = this.Height;
    }

    // ===================== STOP =====================

    private void Stop_Click(object sender, RoutedEventArgs e) => StopPrompter();

    private void StopPrompter()
    {
        _isClosing = true;
        CompositionTarget.Rendering -= OnRendering;
        _audioService.Dispose();
        _speechService.Dispose();
        _settings.Save();
        this.Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_isClosing)
        {
            _isClosing = true;
            CompositionTarget.Rendering -= OnRendering;
            _audioService.Dispose();
            _speechService.Dispose();
        }
        base.OnClosed(e);
    }
}
