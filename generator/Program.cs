
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

        var transformer = new BookTransformer(bookXmlPath);

        foreach (var lang in languages)
        {
            transformer.Transform(lang, targetBookPath, outputPath);
        }

        return 0;
    }
}