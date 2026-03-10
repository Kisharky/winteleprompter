using System.Text.RegularExpressions;
using System.Windows;
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

        // Set up autosave timer (1 second debounce)
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autosaveTimer.Tick += AutosaveTimer_Tick;

        // Load last script
        var lastScript = _scriptManager.LoadLastScript();
        ScriptEditor.Text = lastScript.Content;
        ScriptEditor.FontSize = _settings.EditorFontSize;
        FontSizeLabel.Text = _settings.EditorFontSize.ToString();

        UpdateCounts();
    }

    private void ScriptEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _isDirty = true;
        AutosaveIndicator.Text = "Unsaved";
        AutosaveIndicator.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFF, 0xCC, 0x00));

        // Reset and restart the debounce timer
        _autosaveTimer.Stop();
        _autosaveTimer.Start();

        UpdateCounts();
    }

    private void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();
        if (_isDirty)
        {
            _scriptManager.AutoSave(ScriptEditor.Text);
            _isDirty = false;
            AutosaveIndicator.Text = "Saved";
            AutosaveIndicator.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66));
        }
    }

    private void UpdateCounts()
    {
        var text = ScriptEditor.Text ?? "";
        int words = string.IsNullOrWhiteSpace(text) ? 0 :
            Regex.Split(text.Trim(), @"\s+").Length;
        WordCount.Text = $"{words} word{(words != 1 ? "s" : "")}";
        CharCount.Text = $"{text.Length} character{(text.Length != 1 ? "s" : "")}";
    }

    private void FontIncrease_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptEditor.FontSize < 48)
        {
            ScriptEditor.FontSize += 2;
            FontSizeLabel.Text = ScriptEditor.FontSize.ToString();
            _settings.EditorFontSize = (int)ScriptEditor.FontSize;
            _settings.Save();
        }
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptEditor.FontSize > 12)
        {
            ScriptEditor.FontSize -= 2;
            FontSizeLabel.Text = ScriptEditor.FontSize.ToString();
            _settings.EditorFontSize = (int)ScriptEditor.FontSize;
            _settings.Save();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings);
        settingsWindow.Owner = this;
        if (settingsWindow.ShowDialog() == true)
        {
            _settings = settingsWindow.Settings;
            _settings.Save();
        }
    }

    private void StartPrompter_Click(object sender, RoutedEventArgs e)
    {
        // Force autosave
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

        // Hide editor
        this.Hide();

        // Open prompter
        var prompter = new PrompterWindow(text, _settings);
        prompter.Closed += (_, _) =>
        {
            // Restore editor when prompter closes
            this.Show();
            this.Activate();
            // Reload settings in case they changed
            _settings = AppSettings.Load();
        };
        prompter.Show();
    }
}
