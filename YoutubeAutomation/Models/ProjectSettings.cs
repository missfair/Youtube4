using System.IO;
using Newtonsoft.Json;

namespace YoutubeAutomation.Models;

public class ProjectSettings
{
    public string OpenRouterApiKey { get; set; } = "";
    public string GoogleApiKey { get; set; } = "";
    public string FfmpegPath { get; set; } = "";
    public string OutputBasePath { get; set; } = "";

    // Model selections
    public string TopicGenerationModel { get; set; } = "google/gemini-2.5-flash";
    public string ScriptGenerationModel { get; set; } = "google/gemini-2.5-flash";
    public string ImagePromptModel { get; set; } = "google/gemini-2.5-flash";
    public string ImageGenerationModel { get; set; } = "google/gemini-2.0-flash-exp:free";
    public string TtsModel { get; set; } = "gemini-2.5-pro-preview-tts";
    public string TtsVoice { get; set; } = "charon";

    // Video encoding options
    public bool UseGpuEncoding { get; set; } = false; // Use NVIDIA NVENC if available

    // Reference image for style consistency
    public string ReferenceImagePath { get; set; } = "";

    public int LastEpisodeNumber { get; set; } = 1;

    // Stable Diffusion Local settings
    public string StableDiffusionUrl { get; set; } = "http://127.0.0.1:7860";
    public int StableDiffusionSteps { get; set; } = 25;
    public double StableDiffusionCfgScale { get; set; } = 8.5;
    public string StableDiffusionBatPath { get; set; } = ""; // path to run.bat or webui-user.bat
    public string StableDiffusionModelName { get; set; } = ""; // last used model checkpoint

    // Cloud Image Generation (Google Gemini)
    public string CloudImageModel { get; set; } = "gemini-2.5-flash-image";
    public bool UseCloudImageGen { get; set; } = false;

    // Content Category
    public string DefaultCategoryKey { get; set; } = "animal";

    // BGM (Background Music)
    public string BgmFilePath { get; set; } = "";
    public double BgmVolume { get; set; } = 0.25;
    public bool BgmEnabled { get; set; } = false;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeAutomation",
        "settings.json");

    public static ProjectSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<ProjectSettings>(json) ?? new ProjectSettings();
            }
        }
        catch { }
        return new ProjectSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
