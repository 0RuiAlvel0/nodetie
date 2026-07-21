using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeTie.Infrastructure.Context;

public sealed class ActiveFileContextService
{
    private readonly IReadOnlyList<IActiveFileContextProvider> _providers;

    public ActiveFileContextService(IEnumerable<IActiveFileContextProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
    {
        context = null;
        errorMessage = string.Empty;

        if (_providers.Count == 0)
        {
            errorMessage = "No active file context providers are configured.";
            return false;
        }

        List<string> providerErrors = [];
        foreach (IActiveFileContextProvider provider in _providers)
        {
            if (provider.TryGetActiveFile(out context, out string providerError) && context is not null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(providerError))
            {
                providerErrors.Add(providerError.Trim());
            }
        }

        // Return provider-specific context to make troubleshooting easier when no source can resolve.
        errorMessage = providerErrors.Count > 0
            ? string.Join(Environment.NewLine, providerErrors.Distinct(StringComparer.Ordinal))
            : "No active file context was found.";
        return false;
    }
}
