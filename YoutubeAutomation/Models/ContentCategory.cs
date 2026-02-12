namespace YoutubeAutomation.Models;

public class ContentCategory
{
    // Identity
    public string Key { get; init; } = "animal";
    public string DisplayName { get; init; } = "สัตว์โลกแปลก";

    // Topic Generation
    public string TopicRoleDescription { get; init; } = "";
    public string TopicPrefixRule { get; init; } = "";
    public List<string> TopicExamples { get; init; } = new();
    public string TopicBadExample { get; init; } = "";

    // Script
    public string ScriptToneInstruction { get; init; } = "";
    public string ScriptStructureHint { get; init; } = "";

    // TTS
    public string TtsVoiceInstruction { get; init; } = "";

    // Cover Image (PromptTemplates.GetImagePromptGenerationPrompt)
    public string CoverImageStyleDescription { get; init; } = "";
    public string CoverImageTechnique { get; init; } = "";
    public string CoverImageColorPalette { get; init; } = "";

    // SD Local Image (MultiImageViewModel style prefixes)
    public string SdCartoonStylePrefix { get; init; } = "";
    public string SdCartoonNegativePrompt { get; init; } = "";
    public string SdRealisticStylePrefix { get; init; } = "";
    public string SdRealisticNegativePrompt { get; init; } = "";
    public bool DefaultRealisticStyle { get; init; } = false;

    // Cloud Image (OpenRouterService)
    public string CloudImageStyleDescription { get; init; } = "";
    public string CloudImageColorPalette { get; init; } = "";

    // Scene Image Prompt Guidance (category-specific examples for scene image prompts)
    public string ImagePromptSubjectGuidance { get; init; } = "";

    // BGM
    public string DefaultBgmMood { get; init; } = "curious";

    // Mood Analysis (dynamic mood choices per category)
    public Dictionary<string, string> MoodDescriptions { get; init; } = new();

    // YouTube
    public string YoutubeHashtags { get; init; } = "";
    public List<string> TopicStripWords { get; init; } = new() { "ทำไม", "ถึง" };

    public override string ToString() => DisplayName;
}
