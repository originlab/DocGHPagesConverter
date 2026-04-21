
namespace OriginLab.DocumentGeneration;

internal class FileTransformer
{
    public void Transform(string src, string dst)
    {
        File.Copy(src, dst);
    }
}
