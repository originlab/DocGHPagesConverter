using System.Xml.Linq;

namespace OriginLab.DocumentGeneration;

class Program
{
    static int Main(string[] args)
    {
        var outputPath = args[0];

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        if (Path.GetDirectoryName(outputPath) is not string targetBookPath)
            return -2;

        var languages = (from subPath in Directory.EnumerateDirectories(targetBookPath)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToList();

        if (!languages.Contains("en"))
            return -3;

        var bookXmlPath = Path.Combine(targetBookPath, "en", "book.xml");

        if (!File.Exists(bookXmlPath))
            return -4;

        var bookXml = XElement.Load(bookXmlPath);
        var pages = bookXml.Descendants("page").ToList();

        foreach (var lang in languages)
        {
            var srcDir = Path.Combine(targetBookPath, lang);
            var dstDir = Directory.CreateDirectory(Path.Combine(outputPath, lang));

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

                File.Copy(srcFilePath, dstFilePath);
            }
        }

        return 0;
    }
}