using System.IO;
using NodeTie.Infrastructure.Context.Office;
using Xunit;

namespace NodeTie.Tests;

public sealed class OfficePathResolverTests
{
    [Fact]
    public void TryResolvePreferredPath_ReturnsLocalOneDriveCopyWhenDocsLiveUrlMapsToExistingFile()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string oneDriveRoot = Path.Combine(tempRoot, "OneDrive");
        string localFile = Path.Combine(oneDriveRoot, "Documents", "Folder", "Report.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        File.WriteAllText(localFile, "test");

        try
        {
            var resolver = new OfficePathResolver([oneDriveRoot]);
            string url = "https://d.docs.live.net/4B8544510772040F/Documents/Folder/Report.docx";

            bool ok = resolver.TryResolvePreferredPath(url, out string preferredPath);

            Assert.True(ok);
            Assert.Equal(localFile, preferredPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TryResolvePreferredPath_ReturnsOriginalPathWhenAlreadyLocal()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string localFile = Path.Combine(tempRoot, "Report.docx");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(localFile, "test");

        try
        {
            var resolver = new OfficePathResolver([Path.Combine(tempRoot, "OneDrive")]);

            bool ok = resolver.TryResolvePreferredPath(localFile, out string preferredPath);

            Assert.True(ok);
            Assert.Equal(localFile, preferredPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TryResolvePreferredPath_ReturnsFalseWhenNoLocalCopyExists()
    {
        var resolver = new OfficePathResolver([Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())]);

        bool ok = resolver.TryResolvePreferredPath("https://d.docs.live.net/4B8544510772040F/Documents/Missing.docx", out string preferredPath);

        Assert.False(ok);
        Assert.Equal("https://d.docs.live.net/4B8544510772040F/Documents/Missing.docx", preferredPath);
    }
}
