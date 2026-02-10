using System.IO;
using Newtonsoft.Json;

namespace YoutubeAutomation.Models;

public class ProjectState
{
    public int EpisodeNumber { get; set; } = 1;
    public string TopicSubject { get; set; } = "";
    public List<string> ScriptParts { get; set; } = new() { "", "", "" };
    public string CoverImagePrompt { get; set; } = "";
    public string CoverImagePath { get; set; } = "";
    public List<string> AudioPaths { get; set; } = new() { "", "", "" };
    public string FinalVideoPath { get; set; } = "";
    public int CurrentStepIndex { get; set; } = 0;
    public DateTime LastSaved { get; set; } = DateTime.Now;

    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeAutomation",
        "current_project.json");

    public static ProjectState? Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var json = File.ReadAllText(StatePath);
                return JsonConvert.DeserializeObject<ProjectState>(json);
            }
        }
        catch { }
        return null;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            LastSaved = DateTime.Now;
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(StatePath, json);
        }
        catch { }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
        }
        catch { }
    }

    public bool HasData()
    {
        return !string.IsNullOrWhiteSpace(TopicSubject) ||
               ScriptParts.Any(s => !string.IsNullOrWhiteSpace(s)) ||
               AudioPaths.Any(s => !string.IsNullOrWhiteSpace(s)) ||
               !string.IsNullOrWhiteSpace(CoverImagePath);
    }
}
