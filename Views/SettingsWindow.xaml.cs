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
        {
            FontSizeDisplay.Text = ((int)FontSizeSlider.Value).ToString();
        };
    }

    private void LoadMicrophones()
    {
        var mics = AudioService.GetMicrophones();
        MicSelector.Items.Clear();
        foreach (var mic in mics)
        {
            MicSelector.Items.Add(mic.Name);
        }

        if (MicSelector.Items.Count > 0)
        {
            // Select preferred or first
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(Settings.PreferredMicrophone))
            {
                for (int i = 0; i < MicSelector.Items.Count; i++)
                {
                    if (MicSelector.Items[i].ToString() == Settings.PreferredMicrophone)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            MicSelector.SelectedIndex = selectedIndex;
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

        // Prompter mode
        if (Settings.PrompterMode == PrompterMode.Floating)
            ModeFloating.IsChecked = true;
        else
            ModeNotch.IsChecked = true;

        // Font size
        FontSizeSlider.Value = Settings.FontSize;
        FontSizeDisplay.Text = Settings.FontSize.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Read from UI
        if (MicSelector.SelectedItem != null)
            Settings.PreferredMicrophone = MicSelector.SelectedItem.ToString() ?? "";

        if (ColorYellow.IsChecked == true) Settings.TextColor = TextColorOption.Yellow;
        else if (ColorGreen.IsChecked == true) Settings.TextColor = TextColorOption.Green;
        else Settings.TextColor = TextColorOption.White;

        Settings.PrompterMode = ModeFloating.IsChecked == true ? PrompterMode.Floating : PrompterMode.Notch;
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
