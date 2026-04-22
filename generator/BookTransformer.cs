using System.Xml.Linq;

namespace OriginLab.DocumentGeneration;

internal class BookTransformer
{
    readonly string BookXmlPath;

    public BookTransformer(string bookXmlPath)
    {
        BookXmlPath = bookXmlPath;
    }

    public void Transform(string language, string targetBookPath, string outputPath)
    {
        var bookXml = XElement.Load(BookXmlPath);
        var pages = bookXml.Descendants("page").ToList();

        var srcDir = Path.Combine(targetBookPath, language);
        var dstDir = Directory.CreateDirectory(Path.Combine(outputPath, language));

        foreach (var page in pages)
        {
            var dir = dstDir.FullName;
            var url = page.Attribute("url")!.Value;
            var sep = url.IndexOf('/');
            if (sep > 0)
            {
                dir = Path.Combine(dir, url[(sep + 1)..]);
                Directory.CreateDirectory(dir);
            }

            var srcFilePath = Path.Combine(srcDir, page.Attribute("file")!.Value);
            var dstFilePath = Path.Combine(dir, "index.html");

            Transform(srcFilePath, dstFilePath);
        }

    }

    void Transform(string src, string dst)
    {
        File.Copy(src, dst);
    }
}
