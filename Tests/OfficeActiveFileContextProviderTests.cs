using System.Collections.Generic;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Context.Office;
using Xunit;

namespace NodeTie.Tests;

public sealed class OfficeActiveFileContextProviderTests
{
    [Fact]
    public void TryGetActiveFile_ReturnsWordContextWhenWordDocumentIsActive()
    {
        var com = new FakeComActiveObjectService(new Dictionary<string, object?>
        {
            ["Word.Application"] = new FakeWordApplication(@"C:\Docs\Spec.docx")
        });
        var foreground = new FakeForegroundWindowService("WINWORD");
        var provider = new OfficeActiveFileContextProvider(com, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(@"C:\Docs\Spec.docx", context!.Path);
        Assert.Equal("Word", context.Source);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryGetActiveFile_FallsBackToExcelWhenWordIsUnavailable()
    {
        var com = new FakeComActiveObjectService(new Dictionary<string, object?>
        {
            ["Excel.Application"] = new FakeExcelApplication(@"C:\Docs\Budget.xlsx")
        });
        var foreground = new FakeForegroundWindowService("EXCEL");
        var provider = new OfficeActiveFileContextProvider(com, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(@"C:\Docs\Budget.xlsx", context!.Path);
        Assert.Equal("Excel", context.Source);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryGetActiveFile_ReturnsErrorWhenNoOfficeContextIsAvailable()
    {
        var com = new FakeComActiveObjectService(new Dictionary<string, object?>());
        var foreground = new FakeForegroundWindowService("WINWORD");
        var provider = new OfficeActiveFileContextProvider(com, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Equal("No active Word document or Excel workbook was found.", message);
    }

    [Fact]
    public void TryGetActiveFile_ReturnsFalseWhenOfficeIsRunningInBackground()
    {
        var com = new FakeComActiveObjectService(new Dictionary<string, object?>
        {
            ["Word.Application"] = new FakeWordApplication(@"C:\Docs\Hidden.docx")
        });
        var foreground = new FakeForegroundWindowService("EXPLORER");
        var provider = new OfficeActiveFileContextProvider(com, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Equal(string.Empty, message);
    }

    private sealed class FakeComActiveObjectService : IComActiveObjectService
    {
        private readonly IReadOnlyDictionary<string, object?> _activeObjects;

        public FakeComActiveObjectService(IReadOnlyDictionary<string, object?> activeObjects)
        {
            _activeObjects = activeObjects;
        }

        public bool TryGetActiveObject(string progId, out object? activeObject)
        {
            if (_activeObjects.TryGetValue(progId, out activeObject))
            {
                return activeObject is not null;
            }

            activeObject = null;
            return false;
        }
    }

    public sealed class FakeWordApplication
    {
        public FakeWordApplication(string fullName)
        {
            ActiveDocument = new FakeOfficeDocument(fullName);
        }

        public FakeOfficeDocument ActiveDocument { get; }
    }

    public sealed class FakeExcelApplication
    {
        public FakeExcelApplication(string fullName)
        {
            ActiveWorkbook = new FakeOfficeDocument(fullName);
        }

        public FakeOfficeDocument ActiveWorkbook { get; }
    }

    public sealed class FakeOfficeDocument
    {
        public FakeOfficeDocument(string fullName)
        {
            FullName = fullName;
        }

        public string FullName { get; }
    }

    private sealed class FakeForegroundWindowService : IForegroundWindowService
    {
        private readonly string _processName;

        public FakeForegroundWindowService(string processName)
        {
            _processName = processName;
        }

        public bool TryGetForegroundProcessName(out string processName)
        {
            processName = _processName;
            return true;
        }
    }
}
