namespace NodeTie.Infrastructure.Resolution;

public interface IFileIdentityService
{
    bool TryGetIdentity(string path, out FileIdentity identity);
}
