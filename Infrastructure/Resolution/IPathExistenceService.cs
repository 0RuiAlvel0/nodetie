namespace NodeTie.Infrastructure.Resolution;

public interface IPathExistenceService
{
    bool Exists(string path);
}
