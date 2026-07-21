namespace NodeTie.Infrastructure.Context;

public interface IActiveFileContextProvider
{
    bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage);
}
