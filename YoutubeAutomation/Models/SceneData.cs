using CommunityToolkit.Mvvm.ComponentModel;

namespace YoutubeAutomation.Models;

public partial class SceneData : ObservableObject
{
    [ObservableProperty] private int sceneIndex;
    [ObservableProperty] private string text = "";
    [ObservableProperty] private string imagePrompt = "";
    [ObservableProperty] private string? imagePath;
    [ObservableProperty] private double durationSeconds;
}

public class ScenePart
{
    public int PartNumber { get; set; }
    public List<SceneData> Scenes { get; set; } = new();
    public string? AudioPath { get; set; }
    public double AudioDurationSeconds { get; set; }

    public string GetFullNarrationText()
        => string.Join("\n\n", Scenes.Select(s => s.Text));

    public void CalculateSceneDurations()
    {
        var totalChars = Scenes.Sum(s => s.Text.Length);
        if (totalChars == 0 || AudioDurationSeconds <= 0) return;

        // First pass: calculate proportional durations with floor
        foreach (var scene in Scenes)
        {
            scene.DurationSeconds = Math.Max(3.0,
                AudioDurationSeconds * ((double)scene.Text.Length / totalChars));
        }

        // Second pass: normalize so sum == AudioDurationSeconds
        var rawSum = Scenes.Sum(s => s.DurationSeconds);
        if (rawSum > 0 && Math.Abs(rawSum - AudioDurationSeconds) > 0.1)
        {
            var ratio = AudioDurationSeconds / rawSum;
            foreach (var scene in Scenes)
            {
                scene.DurationSeconds = Math.Max(3.0, scene.DurationSeconds * ratio);
            }
        }
    }
}
