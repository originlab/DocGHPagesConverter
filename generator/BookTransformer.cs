using System.Xml.Linq;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer
{
    readonly string SourceBookPath;
    readonly string OutputPath;
    readonly (string url, string file)[] Pages;

    public BookTransformer(string srcBookPath, string outputPath)
    {
        var bookXml = XElement.Load(Path.Combine(srcBookPath, "en", "book.xml"));
        Pages = (from p in bookXml.Descendants("page")
                 select (p.Attribute("url")!.Value, p.Attribute("file")!.Value)).ToArray();

        SourceBookPath = srcBookPath;
        OutputPath = outputPath;
    }

    public void Transform(string language)
    {
        var srcDir = Path.Combine(SourceBookPath, language);
        var dstDir = Directory.CreateDirectory(Path.Combine(OutputPath, language));

        foreach (var page in Pages)
        {
            var dir = dstDir.FullName;
            var url = page.url;
            var sep = url.IndexOf('/');
            if (sep > 0)
            {
                dir = Path.Combine(dir, url[(sep + 1)..].ToLower());
                Directory.CreateDirectory(dir);
            }

            var srcFilePath = Path.Combine(srcDir, page.file);
            var dstFilePath = Path.Combine(dir, "index.html");

            Transform(srcFilePath, dstFilePath);
        }

    }

    void Transform(string src, string dst)
    {
        File.Copy(src, dst);
    }
}
