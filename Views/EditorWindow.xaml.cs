using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MoodyClone.Models;
using MoodyClone.Services;

namespace MoodyClone.Views;

public partial class EditorWindow : Window
{
    private readonly ScriptManager _scriptManager = new();
    private readonly DispatcherTimer _autosaveTimer;
    private AppSettings _settings;
    private bool _isDirty;

    public EditorWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autosaveTimer.Tick += AutosaveTimer_Tick;

        var lastScript = _scriptManager.LoadLastScript();
        ScriptEditor.Text = lastScript.Content;
        ScriptEditor.FontSize = _settings.EditorFontSize;

        UpdateCounts();
        UpdatePlaceholder();
    }

    private void ScriptEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _isDirty = true;
        SaveIndicator.Text = "Unsaved";
        SaveIndicator.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));

        _autosaveTimer.Stop();
        _autosaveTimer.Start();

        UpdateCounts();
        UpdatePlaceholder();
    }

    private void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();
        if (_isDirty)
        {
            _scriptManager.AutoSave(ScriptEditor.Text);
            _isDirty = false;
            SaveIndicator.Text = "Saved";
            SaveIndicator.Foreground = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        }
    }

    private void UpdateCounts()
    {
        var text = ScriptEditor.Text ?? "";
        int words = string.IsNullOrWhiteSpace(text) ? 0 : Regex.Split(text.Trim(), @"\s+").Length;
        int chars = text.Length;
        WordCount.Text = $"{words} word{(words != 1 ? "s" : "")} · {chars} character{(chars != 1 ? "s" : "")}";
    }

    private void UpdatePlaceholder()
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(ScriptEditor.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FontIncrease_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptEditor.FontSize < 48)
        {
            ScriptEditor.FontSize += 2;
            _settings.EditorFontSize = (int)ScriptEditor.FontSize;
            _settings.Save();
        }
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptEditor.FontSize > 12)
        {
            ScriptEditor.FontSize -= 2;
            _settings.EditorFontSize = (int)ScriptEditor.FontSize;
            _settings.Save();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings);
        win.Owner = this;
        if (win.ShowDialog() == true)
        {
            _settings = win.Settings;
            _settings.Save();
        }
    }

    private void StartPrompter_Click(object sender, RoutedEventArgs e)
    {
        _autosaveTimer.Stop();
        _scriptManager.AutoSave(ScriptEditor.Text);
        _isDirty = false;

        var text = ScriptEditor.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Please enter a script before starting the prompter.",
                "No Script", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        this.Hide();

        var prompter = new PrompterWindow(text, _settings);
        prompter.Closed += (_, _) =>
        {
            this.Show();
            this.Activate();
            _settings = AppSettings.Load();
        };
        prompter.Show();
    }
}
