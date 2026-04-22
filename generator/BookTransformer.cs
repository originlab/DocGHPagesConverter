using System.Xml.Linq;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer
{
    readonly string SourceFolder;
    readonly string OutputFolder;

    readonly string BookUrlName;
    readonly string BookDirName;
    readonly (string url, string file)[] Pages;

    readonly Dictionary<string, (string book, string url)> PageLinks;

    public BookTransformer(string sourceFolder, string outputFolder)
    {
        BookUrlName = Path.GetFileName(sourceFolder).ToLowerInvariant();
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(sourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));

        Pages = (from p in bookXml.Descendants("page")
                 let url = p.Attribute("url")!.Value
                 let file = p.Attribute("file")!.Value
                 select ((url.Length == BookUrlName.Length ? "" : url[(BookUrlName.Length + 1)..]).ToLowerInvariant(), file)).ToArray();

        PageLinks = Pages.ToDictionary(p => $"{BookDirName}/{p.file}", p => (BookUrlName, p.url), StringComparer.OrdinalIgnoreCase);

        SourceFolder = Path.GetFullPath(sourceFolder);
        OutputFolder = Path.GetFullPath(outputFolder);
    }

    public void Transform(string language)
    {
        var srcDir = Path.Combine(SourceFolder, language, BookDirName);
        var outDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));

        foreach (var (url, file) in Pages)
        {
            var dstDir = Path.Combine(outDir.FullName, url);
            Directory.CreateDirectory(dstDir);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, language);
            }
            else
            {
                File.WriteAllText(dstFile, $"""
                    <script>
                    location.href.replace('/{BookUrlName}/en/{url}')
                    </script>
                    """);
            }
        }
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
                            a.SetAttribute("href", $"/{link.book}/{language}/{link.url}{hash}");

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
            if (img.GetAttribute("src") is string src)
            {
                if (src.StartsWith("../images/"))
                {
                    img.SetAttribute("src", $"{BookUrlName}/{language}/{src.AsSpan("../".Length)}");

                    var srcImg = Path.GetFullPath(src, sourceDir);
                    var dstImg = Path.Combine(OutputFolder, language, src["../".Length..]);

                    var sep = dstImg.AsSpan().IndexOfAny("?#");
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
        }
    }

    private void ReportProblem(string sourcePath, string message)
    {
    }
}
