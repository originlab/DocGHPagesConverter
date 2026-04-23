namespace OriginLab.DocumentGeneration;

class Program
{
    async static Task Main(string[] args)
    {
        var outputPath = args[0];

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        if (Path.GetDirectoryName(outputPath) is not string srcBookPath)
            throw new ArgumentException("Expect output path within source book", nameof(args));

        var transformer = new BookTransformer(srcBookPath, outputPath);
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