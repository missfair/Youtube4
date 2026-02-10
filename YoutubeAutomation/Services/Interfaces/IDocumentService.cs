namespace YoutubeAutomation.Services.Interfaces;

public interface IDocumentService
{
    Task SaveAsDocxAsync(string content, string outputPath);
}
