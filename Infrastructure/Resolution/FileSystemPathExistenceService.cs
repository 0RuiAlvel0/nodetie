using System.IO;

namespace NodeTie.Infrastructure.Resolution;

public sealed class FileSystemPathExistenceService : IPathExistenceService
{
    public bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }
}
