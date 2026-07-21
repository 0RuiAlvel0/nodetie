using System.Collections.Generic;
using NodeTie.Infrastructure.Context;
using Xunit;

namespace NodeTie.Tests;

public sealed class ActiveFileContextServiceTests
{
    [Fact]
    public void TryGetActiveFile_ReturnsFirstSuccessfulProvider()
    {
        var providers = new IActiveFileContextProvider[]
        {
            new FakeProvider(ok: false, context: null, errorMessage: "Explorer not selected."),
            new FakeProvider(ok: true, context: new ActiveFileContext(@"C:\Docs\Report.docx", "Word"), errorMessage: string.Empty),
            new FakeProvider(ok: true, context: new ActiveFileContext(@"C:\Docs\Backup.docx", "Explorer"), errorMessage: string.Empty)
        };
        var service = new ActiveFileContextService(providers);

        bool ok = service.TryGetActiveFile(out var context, out var message);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(@"C:\Docs\Report.docx", context!.Path);
        Assert.Equal("Word", context.Source);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryGetActiveFile_AggregatesErrorsWhenAllProvidersFail()
    {
        var providers = new IActiveFileContextProvider[]
        {
            new FakeProvider(ok: false, context: null, errorMessage: "Explorer has no selection."),
            new FakeProvider(ok: false, context: null, errorMessage: "Word has no active document.")
        };
        var service = new ActiveFileContextService(providers);

        bool ok = service.TryGetActiveFile(out var context, out var message);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Contains("Explorer has no selection.", message);
        Assert.Contains("Word has no active document.", message);
    }

    [Fact]
    public void TryGetActiveFile_ReturnsConfigurationErrorWhenNoProvidersExist()
    {
        var service = new ActiveFileContextService(new List<IActiveFileContextProvider>());

        bool ok = service.TryGetActiveFile(out var context, out var message);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Equal("No active file context providers are configured.", message);
    }

    private sealed class FakeProvider : IActiveFileContextProvider
    {
        private readonly bool _ok;
        private readonly ActiveFileContext? _context;
        private readonly string _errorMessage;

        public FakeProvider(bool ok, ActiveFileContext? context, string errorMessage)
        {
            _ok = ok;
            _context = context;
            _errorMessage = errorMessage;
        }

        public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
        {
            context = _context;
            errorMessage = _errorMessage;
            return _ok;
        }
    }
}
