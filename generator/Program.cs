
namespace OriginLab.DocumentGeneration;

class Program
{
    static void Main(string[] args)
    {
        var outputPath = args[0];

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        if (Path.GetDirectoryName(outputPath) is not string srcBookPath)
            throw new ArgumentException("Expect output path within source book", nameof(args));

        var languages = (from subPath in Directory.EnumerateDirectories(srcBookPath)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToList();

        if (!languages.Contains("en"))
            throw new FileNotFoundException("Expect en folder exists within source book", Path.Combine(srcBookPath, "en"));

        var transformer = new BookTransformer(srcBookPath, outputPath);

        foreach (var lang in languages)
        {
            transformer.Transform(lang);
        }

        File.WriteAllText(Path.Combine(outputPath, "404.html"), """
            <script>
            var currentURL = window.location.href;
            var lowerCaseURL = currentURL.toLowerCase();
            if (currentURL != lowerCaseURL) {
                location.replace(lowerCaseURL);
            }
            </script>
            """);

        File.WriteAllText(Path.Combine(outputPath, "index.html"), """
            <script>
            location.replace('en');
            </script>
            """);
    }
}