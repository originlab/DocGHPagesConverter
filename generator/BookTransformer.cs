using System.Xml.Linq;
using OriginLab.DocumentGeneration.Templates;
using Razor.Templating.Core;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer : ContentTransformer
{
    private readonly string BookDirName;
    private readonly (string, string file)[] Pages;

    public BookTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(booksXmlFolder, sourceFolder, outputFolder)
    {
        BookDirName = Path.GetFileName(SourceFolder).ToLowerInvariant();

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));

        Pages = (from p in bookXml.Descendants("page")
                 let url = p.Attribute("url")!.Value
                 let file = p.Attribute("file")!.Value
                 select ((url.Length == BookUrlName.Length ? "" : url[(BookUrlName.Length + 1)..]).ToLowerInvariant(), file)).ToArray();

    }

    protected override string GetBookUrlName() => Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

    public override async Task TransformAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            await TransformAsync(language);
        }
    }

    async Task TransformAsync(string language)
    {
        var srcDir = Path.Combine(SourceFolder, language, BookDirName);

        foreach (var (url, file) in Pages)
        {
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");
            Directory.CreateDirectory(dstDir);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, language);
            }
            else if (language != "en")
            {
                File.WriteAllText(dstFile, $"""
                    <script>
                    location.replace('/{BookUrlName}/{url}')
                    </script>
                    """);
            }
            else
            {
                ReportProblem("en/book.xml", $"Source file not found: {srcFile}");
            }
        }

        var layoutHtml = await RazorTemplateEngine.RenderAsync("/DocumentPage.cshtml", new DocumentPageModel
        {
            RootUrlPrefix = $"/{BookUrlName}",
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BookUrlName,
            BookDirName = BookDirName
        });
        var langDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));
        File.WriteAllText(Path.Combine(langDir.FullName, "layout.html"), layoutHtml);
    }
}
