using System.Windows;
using MoodyClone.Models;
using MoodyClone.Services;

namespace MoodyClone.Views;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        LoadMicrophones();
        ApplySettingsToUI();

        FontSizeSlider.ValueChanged += (_, _) =>
            FontSizeDisplay.Text = ((int)FontSizeSlider.Value).ToString();
    }

    private void LoadMicrophones()
    {
        var mics = AudioService.GetMicrophones();
        MicSelector.Items.Clear();
        foreach (var mic in mics)
            MicSelector.Items.Add(mic.Name);

        if (MicSelector.Items.Count > 0)
        {
            int idx = 0;
            if (!string.IsNullOrEmpty(Settings.PreferredMicrophone))
            {
                for (int i = 0; i < MicSelector.Items.Count; i++)
                {
                    if (MicSelector.Items[i].ToString() == Settings.PreferredMicrophone)
                    {
                        idx = i;
                        break;
                    }
                }
            }
            MicSelector.SelectedIndex = idx;
        }
    }

    private void ApplySettingsToUI()
    {
        // Text color
        switch (Settings.TextColor)
        {
            case TextColorOption.Yellow: ColorYellow.IsChecked = true; break;
            case TextColorOption.Green: ColorGreen.IsChecked = true; break;
            default: ColorWhite.IsChecked = true; break;
        }

        // Text alignment
        switch (Settings.TextAlignment)
        {
            case TextAlignmentOption.Left: AlignLeft.IsChecked = true; break;
            case TextAlignmentOption.Right: AlignRight.IsChecked = true; break;
            default: AlignCenter.IsChecked = true; break;
        }

        // Prompter mode
        if (Settings.PrompterMode == PrompterMode.Floating)
            ModeFloating.IsChecked = true;
        else
            ModeNotch.IsChecked = true;

        // Countdown
        switch (Settings.CountdownSeconds)
        {
            case 0: Countdown0.IsChecked = true; break;
            case 5: Countdown5.IsChecked = true; break;
            default: Countdown3.IsChecked = true; break;
        }

        // Font size
        FontSizeSlider.Value = Settings.FontSize;
        FontSizeDisplay.Text = Settings.FontSize.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (MicSelector.SelectedItem != null)
            Settings.PreferredMicrophone = MicSelector.SelectedItem.ToString() ?? "";

        // Color
        if (ColorYellow.IsChecked == true) Settings.TextColor = TextColorOption.Yellow;
        else if (ColorGreen.IsChecked == true) Settings.TextColor = TextColorOption.Green;
        else Settings.TextColor = TextColorOption.White;

        // Alignment
        if (AlignLeft.IsChecked == true) Settings.TextAlignment = TextAlignmentOption.Left;
        else if (AlignRight.IsChecked == true) Settings.TextAlignment = TextAlignmentOption.Right;
        else Settings.TextAlignment = TextAlignmentOption.Center;

        // Mode
        Settings.PrompterMode = ModeFloating.IsChecked == true ? PrompterMode.Floating : PrompterMode.Notch;

        // Countdown
        if (Countdown0.IsChecked == true) Settings.CountdownSeconds = 0;
        else if (Countdown5.IsChecked == true) Settings.CountdownSeconds = 5;
        else Settings.CountdownSeconds = 3;

        // Font
        Settings.FontSize = (int)FontSizeSlider.Value;

        this.DialogResult = true;
        this.Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }
}
