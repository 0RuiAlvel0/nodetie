namespace NodeTie.Infrastructure.Context;

public interface IForegroundWindowService
{
    bool TryGetForegroundProcessName(out string processName);
}