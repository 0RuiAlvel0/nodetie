using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Linking;

public sealed class LinkRemovalService
{
    private readonly LinkRepository _linkRepository;

    public LinkRemovalService(LinkRepository linkRepository)
    {
        _linkRepository = linkRepository;
    }

    public bool TryRemoveLink(long sourceFileId, long linkedFileId)
    {
        // Removal keeps the link table symmetric by deleting the undirected pair.
        return _linkRepository.RemoveUndirectedLink(sourceFileId, linkedFileId);
    }
}
