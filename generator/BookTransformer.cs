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
                 let url = p.Attribute("url")!.Value.ToLower()
                 let file = p.Attribute("file")!.Value
                 select (url, file)).ToArray();

        SourceBookPath = srcBookPath;
        OutputPath = outputPath;
    }

    public void Transform(string language)
    {
        var srcDir = Path.Combine(SourceBookPath, language);
        var outDir = Directory.CreateDirectory(Path.Combine(OutputPath, language));

        foreach (var page in Pages)
        {
            var dstDir = outDir.FullName;
            var url = page.url;
            var sep = url.IndexOf('/');
            if (sep > 0)
            {
                dstDir = Path.Combine(dstDir, url[(sep + 1)..]);
                Directory.CreateDirectory(dstDir);
            }

            var srcFilePath = Path.Combine(srcDir, page.file);
            var dstFilePath = Path.Combine(dstDir, "index.html");

            Transform(srcFilePath, dstFilePath);
        }

    }

    void Transform(string src, string dst)
    {
        File.Copy(src, dst);
    }
}
