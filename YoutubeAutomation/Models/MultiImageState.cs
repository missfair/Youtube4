using System.IO;
using Newtonsoft.Json;

namespace YoutubeAutomation.Models;

public class MultiImageState
{
    public int EpisodeNumber { get; set; }
    public string TopicText { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public string FinalVideoPath { get; set; } = "";
    public List<bool> StepCompleted { get; set; } = new();
    public List<PartData> Parts { get; set; } = new();
    public string? ReferenceImagePath { get; set; }
    public double DenoisingStrength { get; set; } = 0.6;
    public bool IsRealisticStyle { get; set; }
    public bool UseSceneChaining { get; set; }
    public string SelectedModel { get; set; } = "";
    public DateTime LastSaved { get; set; }

    public class PartData
    {
        public int PartNumber { get; set; }
        public string? AudioPath { get; set; }
        public double AudioDurationSeconds { get; set; }
        public List<SceneRecord> Scenes { get; set; } = new();
    }

    public class SceneRecord
    {
        public int SceneIndex { get; set; }
        public string Text { get; set; } = "";
        public string ImagePrompt { get; set; } = "";
        public string? ImagePath { get; set; }
        public double DurationSeconds { get; set; }
    }

    public static MultiImageState? Load(string outputFolder)
    {
        try
        {
            var path = Path.Combine(outputFolder, "multiimage_state.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<MultiImageState>(json);
            }
        }
        catch { }
        return null;
    }

    public bool Save(string outputFolder)
    {
        try
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            LastSaved = DateTime.Now;
            var path = Path.Combine(outputFolder, "multiimage_state.json");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MultiImageState.Save failed: {ex.Message}");
            return false;
        }
    }

    public bool HasData()
    {
        return Parts.Count > 0 && Parts.Any(p => p.Scenes.Count > 0);
    }
}
