namespace NodeTie.Infrastructure.Context.Office;

public interface IComActiveObjectService
{
    bool TryGetActiveObject(string progId, out object? activeObject);
}
