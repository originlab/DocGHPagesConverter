using System.Xml.Linq;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Templates;
using Razor.Templating.Core;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer
{
    readonly string SourceFolder;
    readonly string SourceFolderEn;
    readonly string OutputFolder;

    readonly string[] AvailableLanguages;

    readonly string BookUrlName;
    readonly string BookDirName;
    readonly (string url, string file)[] Pages;

    readonly Dictionary<string, (string book, string url)> PageLinks;

    public BookTransformer(string sourceFolder, string outputFolder)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        if (!languages.Contains("en"))
            throw new FileNotFoundException("Expect en folder exists within source book", Path.Combine(sourceFolder, "en"));

        AvailableLanguages = languages;

        BookUrlName = Path.GetFileName(sourceFolder).ToLowerInvariant();
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(sourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));

        Pages = (from p in bookXml.Descendants("page")
                 let url = p.Attribute("url")!.Value
                 let file = p.Attribute("file")!.Value
                 select ((url.Length == BookUrlName.Length ? "" : url[(BookUrlName.Length + 1)..]).ToLowerInvariant(), file)).ToArray();

        PageLinks = Pages.ToDictionary(p => $"{BookDirName}/{p.file}", p => (BookUrlName, p.url), StringComparer.OrdinalIgnoreCase);

        SourceFolder = Path.GetFullPath(sourceFolder);
        SourceFolderEn = Path.Combine(SourceFolder, "en");
        OutputFolder = Path.GetFullPath(outputFolder);
    }

    public async Task TransformAsync()
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
                ReportProblem(srcFile, "Source file not found.");
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

    void Transform(string sourceFile, string destinationFile, string language)
    {
        using var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
        var parser = new HtmlParser();
        var document = parser.ParseDocument(fs);

        Transform(document, language, sourceFile);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    void Transform(IHtmlDocument document, string language, string sourceFile)
    {
        if (document.QuerySelector("h1.firstHeading") is IElement firstHeading)
        {
            document.Title = firstHeading.Text();
        }

        var sourceDir = Path.GetDirectoryName(sourceFile)!;

        foreach (var a in document.Descendants<IHtmlAnchorElement>())
        {
            if (a.GetAttribute("href") is string href && !String.IsNullOrWhiteSpace(href))
            {
                string? hash = null;
                var hashIndex = href.IndexOf('#');
                if (hashIndex > -1)
                {
                    hash = href[hashIndex..];
                    href = href[..hashIndex];
                }

                if (!String.IsNullOrWhiteSpace(href))
                {
                    if (Uri.IsWellFormedUriString(href, UriKind.Relative) && !href.StartsWith('/'))
                    {
                        var fullPath = Path.GetFullPath(href, sourceDir);
                        if (fullPath.StartsWith(SourceFolder)
                            && PageLinks.TryGetValue(fullPath[(SourceFolder.Length + "/en/".Length)..].Replace('\\', '/'), out var link))
                        {
                            if (language == "en")
                            {
                                a.SetAttribute("href", $"/{link.book}/{link.url}{hash}");
                            }
                            else
                            {
                                a.SetAttribute("href", $"/{link.book}/{link.url}/{language}{hash}");
                            }

                            //if (String.IsNullOrWhiteSpace(a.Title) || a.Title.Contains(':'))
                            //{
                            //    a.Title = link.title;
                            //}
                        }
                        else
                        {
                            ReportProblem(sourceFile, $"Mapping unknown for href: {href}");
                        }
                    }
                    else if (!href.StartsWith('#')
                        && !href.StartsWith("mailto:")
                        && !href.StartsWith("javascript:")
                        && !Uri.IsWellFormedUriString(href, UriKind.Absolute))
                    {
                        ReportProblem(sourceFile, $"Unrecognized href: {href}");
                    }
                }
            }
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            if (img.GetAttribute("src") is not string src)
            {
                continue;
            }

            if (src.StartsWith("../images/"))
            {
                img.SetAttribute("loading", "lazy");

                var srcImg = Path.GetFullPath(src, sourceDir);

                var sep = srcImg.AsSpan().IndexOfAny("?#");
                if (sep > -1)
                {
                    srcImg = srcImg[..sep];
                }

                if (File.Exists(srcImg))
                {
                    img.SetAttribute("src", $"/{BookUrlName}/{language}/{src.AsSpan("../".Length)}");
                }
                else
                {
                    var srcImgEn = $"{SourceFolderEn}{srcImg.AsSpan(SourceFolderEn.Length)}";

                    if (!File.Exists(srcImgEn))
                    {
                        ReportProblem(sourceFile, $"Image src not found: {src}");
                    }

                    img.SetAttribute("src", $"/{BookUrlName}/en/{src.AsSpan("../".Length)}");

                    continue;
                }

                var dstImg = Path.Combine(OutputFolder, language, src["../".Length..]);

                sep = dstImg.AsSpan().IndexOfAny("?#");
                if (sep > -1)
                {
                    dstImg = dstImg[..sep];
                }

                var dstImgDir = Path.GetDirectoryName(dstImg)!;
                Directory.CreateDirectory(dstImgDir);

                File.Copy(srcImg, dstImg, overwrite: true);
            }
            else if (!Uri.IsWellFormedUriString(src, UriKind.Absolute))
            {
                ReportProblem(sourceFile, $"Unrecognized src: {src}");
            }
        }

        var script = document.CreateElement<IHtmlScriptElement>();
        script.InnerHtml = $$"""
            window.addEventListener("DOMContentLoaded", async()=>{
                var layout = await fetch("/{{BookUrlName}}/{{language}}/layout.html");
                var parser = new DOMParser();
                var doc = parser.parseFromString(await layout.text(), 'text/html');
                var container = doc.getElementById('doc-static-content-container');
                container.innerHTML = document.body.innerHTML;
                document.replaceChild(
                    document.adoptNode(doc.documentElement),
                    document.documentElement
                );
            })
            """;
        document.Head!.AppendElement(script);
    }

    private void ReportProblem(string sourcePath, string message)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(Path.GetRelativePath(SourceFolder, sourcePath));
        Console.Error.WriteLine(message);
    }
}
