namespace OriginLab.DocumentGeneration;

class Program
{
    async static Task Main(string[] args)
    {
        var srcBookPath = args[0];
        if (!Directory.Exists(srcBookPath))
        {
            throw new ArgumentException("Expect book folder exists!", nameof(args));
        }

        var booksXmlPath = Path.Combine(Path.GetDirectoryName(srcBookPath)!, "index", "books");
        if (!Directory.Exists(booksXmlPath))
        {
            throw new ArgumentException("Expect index/books exists!", nameof(args));
        }

        var outputPath = Path.Combine(srcBookPath, "out");
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var transformer = new BookTransformer(booksXmlPath, srcBookPath, outputPath);
        await transformer.TransformAsync();

        File.WriteAllText(Path.Combine(outputPath, "404.html"), """
            <script>
            var currentURL = window.location.href;
            var lowerCaseURL = currentURL.toLowerCase();
            if (currentURL != lowerCaseURL) {
                location.replace(lowerCaseURL);
            }
            </script>
            """);
    }
}