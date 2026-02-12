namespace YoutubeAutomation.Services.Interfaces;

public interface IGoogleTtsService
{
    Task<byte[]> GenerateAudioAsync(
        string text,
        string voiceName,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? ttsInstruction = null);

    Task<byte[]> GenerateImageAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        string aspectRatio = "16:9",
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(string apiKey);
}
