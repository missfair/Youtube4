using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class DocumentService : IDocumentService
{
    public async Task SaveAsDocxAsync(string content, string outputPath)
    {
        await Task.Run(() =>
        {
            using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Split content by newlines and create paragraphs
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var paragraph = new Paragraph();
                var run = new Run();
                var text = new Text(line) { Space = SpaceProcessingModeValues.Preserve };
                run.Append(text);
                paragraph.Append(run);
                body.Append(paragraph);
            }

            mainPart.Document.Save();
        });
    }
}
