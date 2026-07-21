namespace NodeTie.Infrastructure.Resolution;

public interface IStableFileLocator
{
    bool TryLocate(string stableId, out string locatedPath);
}
