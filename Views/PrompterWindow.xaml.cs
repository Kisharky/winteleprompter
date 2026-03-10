using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    private double _basePixelsPerSecond = 40.0;
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
        ScriptText.FontSize = settings.FontSize;
        ScriptText.LineHeight = settings.FontSize * 1.35;
        ApplyTextColor(settings.TextColor);

        // Set size
        this.Width = settings.PrompterWidth;
        this.Height = settings.PrompterHeight;

        // Apply mode
        ApplyMode(_currentMode);
        UpdateSpeedButtonStates();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply screen capture invisibility
        ApplyDisplayAffinity();

        // Adjust padding so first/last text aligns with center guide
        var textAreaHeight = TextScroller.ActualHeight;
        TopPadding.Height = textAreaHeight / 2;
        BottomPadding.Height = textAreaHeight / 2;

        // Start audio monitoring
        StartAudioMonitoring();

        // Subscribe to rendering for smooth scroll
        _lastFrameTime = DateTime.Now;
        CompositionTarget.Rendering += OnRendering;

        // Hover-to-pause
        TextScroller.MouseEnter += (_, _) => { _isHoverPaused = true; UpdatePauseVisual(); };
        TextScroller.MouseLeave += (_, _) => { _isHoverPaused = false; UpdatePauseVisual(); };

        this.Focus();
    }

    // ===================== SCREEN CAPTURE INVISIBILITY =====================

    private void ApplyDisplayAffinity()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            }
        }
        catch { }
    }

    // ===================== SMOOTH SCROLL ENGINE =====================

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_isClosing) return;

        var now = DateTime.Now;
        var deltaSeconds = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // Clamp delta to avoid huge jumps (e.g., after window un-freeze)
        if (deltaSeconds > 0.1) deltaSeconds = 0.016;

        // Voice beam update
        UpdateVoiceBeam();

        // Determine if scrolling should happen
        bool effectivelyPaused = _isPaused || _isHoverPaused || (_isVoiceMode && _isVoicePaused);

        if (_isVoiceMode && _voiceTargetOffset >= 0)
        {
            // Smooth approach to voice target
            double diff = _voiceTargetOffset - _scrollPosition;
            if (Math.Abs(diff) > 1)
            {
                _scrollPosition += diff * 0.08; // Smooth ease
            }
            else
            {
                _scrollPosition = _voiceTargetOffset;
            }
            TextScroller.ScrollToVerticalOffset(_scrollPosition);
        }
        else if (!effectivelyPaused)
        {
            // Auto-scroll
            double pixelsThisFrame = _basePixelsPerSecond * _currentSpeedMultiplier * deltaSeconds;
            _scrollPosition += pixelsThisFrame;

            // Clamp
            var maxScroll = TextScroller.ScrollableHeight;
            if (_scrollPosition > maxScroll) _scrollPosition = maxScroll;

            TextScroller.ScrollToVerticalOffset(_scrollPosition);
        }
    }

    // ===================== VOICE FEEDBACK BEAM =====================

    private void StartAudioMonitoring()
    {
        var mics = AudioService.GetMicrophones();
        int deviceIndex = 0;

        // Try to use preferred mic
        if (!string.IsNullOrEmpty(_settings.PreferredMicrophone))
        {
            var preferred = mics.FirstOrDefault(m => m.Name == _settings.PreferredMicrophone);
            if (preferred.Name != null) deviceIndex = preferred.Index;
        }

        _audioService.StartMonitoring(deviceIndex);
    }

    private void UpdateVoiceBeam()
    {
        try
        {
            double rms = _audioService.CurrentRms;
            // Scale: RMS typically 0..0.3 for speech
            double normalizedLevel = Math.Min(rms / 0.15, 1.0);
            double targetWidth = normalizedLevel * (this.ActualWidth * 0.8);

            // Smooth interpolation
            double currentWidth = VoiceBeam.Width;
            VoiceBeam.Width = currentWidth + (targetWidth - currentWidth) * 0.3;

            // Brightness
            VoiceBeam.Opacity = 0.4 + normalizedLevel * 0.6;
        }
        catch { }
    }

    // ===================== VOICE SCROLLING =====================

    private void VoiceToggle_Click(object sender, RoutedEventArgs e)
    {
        _isVoiceMode = !_isVoiceMode;

        if (_isVoiceMode)
        {
            VoiceToggle.Content = "🎤 Voice ON";
            VoiceToggle.Background = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58));
            VoiceToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));

            // Start speech recognition
            _speechService.LoadScript(_scriptText);
            _speechService.WordRecognized += OnWordRecognized;
            _speechService.SpeechPaused += OnSpeechPaused;
            _speechService.Start();
            _isVoicePaused = false;
        }
        else
        {
            VoiceToggle.Content = "🎤 Voice";
            VoiceToggle.Background = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            VoiceToggle.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

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

            // Estimate scroll position based on word index
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
            // Notch: top center, rounded bottom corners
            MainBorder.CornerRadius = new CornerRadius(0, 0, 16, 16);
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1C, 0x1C, 0x1E));

            // Position at top center of primary screen
            var screen = SystemParameters.WorkArea;
            this.Left = (screen.Width - this.Width) / 2;
            this.Top = 0;

            ModeToggle.Content = "📌 Notch";
            ResizeGrip.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Floating: rounded all corners, draggable
            MainBorder.CornerRadius = new CornerRadius(12);
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0x00, 0x00));

            ModeToggle.Content = "🪟 Float";
            ResizeGrip.Visibility = Visibility.Visible;
        }
    }

    private void ModeToggle_Click(object sender, RoutedEventArgs e)
    {
        var newMode = _currentMode == PrompterMode.Notch ? PrompterMode.Floating : PrompterMode.Notch;
        ApplyMode(newMode);
        _settings.PrompterMode = newMode;
        _settings.Save();
    }

    // ===================== SPEED CONTROLS =====================

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
        if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, out double mult))
        {
            _currentSpeedMultiplier = mult;
            UpdateSpeedButtonStates();
        }
    }

    private void UpdateSpeedButtonStates()
    {
        var activeStyle = (Style)FindResource("SpeedBtnActive");
        var normalStyle = (Style)FindResource("PrompterBtn");

        Speed1.Style = _currentSpeedMultiplier == 1.0 ? activeStyle : normalStyle;
        Speed15.Style = _currentSpeedMultiplier == 1.5 ? activeStyle : normalStyle;
        Speed2.Style = _currentSpeedMultiplier == 2.0 ? activeStyle : normalStyle;
        Speed3.Style = _currentSpeedMultiplier == 3.0 ? activeStyle : normalStyle;
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
        bool anyPause = _isPaused || _isHoverPaused || (_isVoiceMode && _isVoicePaused);
        PauseIndicator.Visibility = anyPause ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== KEYBOARD SHORTCUTS =====================

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
                if (ScriptText.FontSize < 96)
                {
                    ScriptText.FontSize += 4;
                    ScriptText.LineHeight = ScriptText.FontSize * 1.35;
                    _settings.FontSize = (int)ScriptText.FontSize;
                    _settings.Save();
                }
                e.Handled = true;
                break;

            case Key.OemMinus when Keyboard.Modifiers == ModifierKeys.Control:
            case Key.Subtract when Keyboard.Modifiers == ModifierKeys.Control:
                if (ScriptText.FontSize > 20)
                {
                    ScriptText.FontSize -= 4;
                    ScriptText.LineHeight = ScriptText.FontSize * 1.35;
                    _settings.FontSize = (int)ScriptText.FontSize;
                    _settings.Save();
                }
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
        {
            this.DragMove();
        }
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newWidth = this.Width + e.HorizontalChange;
        double newHeight = this.Height + e.VerticalChange;

        if (newWidth >= 400) this.Width = newWidth;
        if (newHeight >= 200) this.Height = newHeight;

        _settings.PrompterWidth = this.Width;
        _settings.PrompterHeight = this.Height;
    }

    // ===================== STOP =====================

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPrompter();
    }

    private void StopPrompter()
    {
        _isClosing = true;

        // Unsubscribe from rendering
        CompositionTarget.Rendering -= OnRendering;

        // Stop services
        _audioService.Dispose();
        _speechService.Dispose();

        // Save settings
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
