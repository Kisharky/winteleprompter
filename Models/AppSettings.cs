using System.IO;
using Newtonsoft.Json;

namespace MoodyClone.Models;

public enum TextColorOption
{
    White,
    Yellow,
    Green
}

public enum PrompterMode
{
    Notch,
    Floating
}

public enum SpeedStep
{
    x1,
    x1_5,
    x2,
    x3
}

public class AppSettings
{
    public int FontSize { get; set; } = 48;
    public TextColorOption TextColor { get; set; } = TextColorOption.White;
    public PrompterMode PrompterMode { get; set; } = PrompterMode.Notch;
    public string PreferredMicrophone { get; set; } = string.Empty;
    public SpeedStep ScrollSpeed { get; set; } = SpeedStep.x1;
    public int EditorFontSize { get; set; } = 22;
    public double PrompterWidth { get; set; } = 700;
    public double PrompterHeight { get; set; } = 300;

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MoodyClone");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
