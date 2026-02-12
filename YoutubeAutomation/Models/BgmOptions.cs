namespace YoutubeAutomation.Models;

public record BgmOptions(
    string FilePath,
    double Volume = 0.25,
    double FadeInSeconds = 3.0,
    double FadeOutSeconds = 5.0
);
