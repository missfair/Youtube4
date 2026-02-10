namespace YoutubeAutomation.Services.Interfaces;

public interface IStableDiffusionService
{
    Task<byte[]> GenerateImageAsync(
        string prompt,
        string negativePrompt = "",
        int width = 768,
        int height = 432,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateImageWithReferenceAsync(
        string prompt,
        string referenceImagePath,
        double denoisingStrength = 0.6,
        string negativePrompt = "",
        int width = 768,
        int height = 432,
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync();

    /// <summary>Tell SD Forge to rescan the models folder for new checkpoints.</summary>
    Task RefreshCheckpointsAsync(CancellationToken cancellationToken = default);

    /// <summary>Get list of available model names from SD Forge.</summary>
    Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Get the currently loaded model name.</summary>
    Task<string> GetCurrentModelAsync(CancellationToken cancellationToken = default);

    /// <summary>Switch to a different model checkpoint.</summary>
    Task SetModelAsync(string modelName, CancellationToken cancellationToken = default);

    Task<bool> LaunchAsync(IProgress<string>? status = null, CancellationToken cancellationToken = default);
    Task StopAsync();
    bool IsProcessRunning { get; }
}
