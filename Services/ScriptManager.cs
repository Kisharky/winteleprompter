using System.IO;
using MoodyClone.Models;
using Newtonsoft.Json;

namespace MoodyClone.Services;

public class ScriptManager
{
    private static readonly string ScriptsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MoodyClone", "scripts");

    private Script? _currentScript;

    public ScriptManager()
    {
        Directory.CreateDirectory(ScriptsDir);
    }

    public Script LoadLastScript()
    {
        try
        {
            var files = Directory.GetFiles(ScriptsDir, "*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .ToArray();

            if (files.Length > 0)
            {
                var json = File.ReadAllText(files[0]);
                _currentScript = JsonConvert.DeserializeObject<Script>(json);
                if (_currentScript != null) return _currentScript;
            }
        }
        catch { }

        _currentScript = new Script();
        return _currentScript;
    }

    public List<Script> LoadScripts()
    {
        var scripts = new List<Script>();
        try
        {
            foreach (var file in Directory.GetFiles(ScriptsDir, "*.json"))
            {
                var json = File.ReadAllText(file);
                var script = JsonConvert.DeserializeObject<Script>(json);
                if (script != null) scripts.Add(script);
            }
        }
        catch { }
        return scripts.OrderByDescending(s => s.LastModified).ToList();
    }

    public void SaveScript(Script script)
    {
        try
        {
            script.LastModified = DateTime.Now;
            var filePath = Path.Combine(ScriptsDir, $"{script.Id}.json");
            var json = JsonConvert.SerializeObject(script, Formatting.Indented);
            File.WriteAllText(filePath, json);
            _currentScript = script;
        }
        catch { }
    }

    public void AutoSave(string content)
    {
        if (_currentScript == null)
            _currentScript = new Script();

        _currentScript.Content = content;
        SaveScript(_currentScript);
    }

    public void DeleteScript(string id)
    {
        try
        {
            var filePath = Path.Combine(ScriptsDir, $"{id}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch { }
    }
}
