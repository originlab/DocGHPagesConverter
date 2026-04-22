using System.Xml.Linq;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer
{
    readonly string SourcePath;
    readonly string OutputPath;

    readonly string BookUrlName;
    readonly string BookChmName;
    readonly (string url, string file)[] Pages;

    readonly Dictionary<string, (string book, string url)> PageLinks;

    public BookTransformer(string sourcePath, string outputPath)
    {
        var bookXml = XElement.Load(Path.Combine(sourcePath, "en", "book.xml"));

        BookUrlName = Path.GetFileName(sourcePath).ToLowerInvariant();
        BookChmName = bookXml.Attribute("dir")!.Value;

        Pages = (from p in bookXml.Descendants("page")
                 let url = p.Attribute("url")!.Value
                 let file = p.Attribute("file")!.Value
                 select ((url.Length == BookUrlName.Length ? "" : url[(BookUrlName.Length + 1)..]).ToLowerInvariant(), file)).ToArray();

        PageLinks = Pages.ToDictionary(p => p.file, p => (BookUrlName, p.url), StringComparer.OrdinalIgnoreCase);

        SourcePath = sourcePath;
        OutputPath = outputPath;
    }

    public void Transform(string language)
    {
        var srcDir = Path.Combine(SourcePath, language);
        var outDir = Directory.CreateDirectory(Path.Combine(OutputPath, language));

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

    void Transform(string srcFile, string dstFile, string language)
    {
        using var fs = new FileStream(srcFile, FileMode.Open, FileAccess.Read);
        var parser = new HtmlParser();
        var document = parser.ParseDocument(fs);

        Transform(document, language, srcFile);

        using var sw = new StreamWriter(dstFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    void Transform(IHtmlDocument document, string language, string sourcePath)
    {
        if (document.QuerySelector("h1.firstHeading") is IElement firstHeading)
        {
            document.Title = firstHeading.Text();
        }

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
                    if (Uri.IsWellFormedUriString(href, UriKind.Relative))
                    {
                        if (href.StartsWith($"../../{BookChmName}/"))
                        {
                            if (PageLinks.TryGetValue(href[$"../../{BookChmName}/".Length..], out var link))
                            {
                                a.SetAttribute("href", $"/{link.book}/{language}/{link.url}{hash}");

                                //if (String.IsNullOrWhiteSpace(a.Title) || a.Title.Contains(':'))
                                //{
                                //    a.Title = link.title;
                                //}
                            }
                            else
                            {
                                ReportProblem(sourcePath, $"Mapping unknown for href: {href}");
                            }
                        }
                    }
                    else if (!href.StartsWith('#')
                        && !href.StartsWith("mailto:")
                        && !href.StartsWith("javascript:")
                        && !Uri.IsWellFormedUriString(href, UriKind.Absolute))
                    {
                        ReportProblem(sourcePath, $"Unrecognized href: {href}");
                    }
                }
            }
        }
    }

    private void ReportProblem(string sourcePath, string message)
    {
    }
}
