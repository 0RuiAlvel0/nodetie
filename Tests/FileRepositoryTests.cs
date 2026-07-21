using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class FileRepositoryTests
{
    [Fact]
    public void UpsertByStableId_UpdatesCurrentPathAndKeepsAliasHistory()
    {
        using var testDatabase = new SqliteTestDatabase();
        var repository = new FileRepository(testDatabase.ConnectionFactory);

        long id = repository.UpsertByStableId("volumeA:3001", @"C:\Docs\OldName.txt", "OldName.txt");
        long updatedId = repository.UpsertByStableId("volumeA:3001", @"C:\Docs\NewName.txt", "NewName.txt");

        Assert.Equal(id, updatedId);

        var updatedRecord = repository.GetById(id);
        Assert.NotNull(updatedRecord);
        Assert.Equal(@"C:\Docs\NewName.txt", updatedRecord!.CurrentPath);
        Assert.Equal("NewName.txt", updatedRecord.DisplayName);

        var searchMatches = repository.SearchByNameOrPath("OldName", maxResults: 10);
        Assert.Contains(searchMatches, record => record.Id == id);
    }

    [Fact]
    public void GetByKnownPath_ReturnsRecordForCurrentPath()
    {
        using var testDatabase = new SqliteTestDatabase();
        var repository = new FileRepository(testDatabase.ConnectionFactory);

        long id = repository.UpsertByStableId("volumeA:3002", @"C:\Docs\Current.txt", "Current.txt");

        FileRecord? record = repository.GetByKnownPath(@"C:\Docs\Current.txt");

        Assert.NotNull(record);
        Assert.Equal(id, record!.Id);
    }

    [Fact]
    public void GetByKnownPath_ReturnsRecordForAliasPath()
    {
        using var testDatabase = new SqliteTestDatabase();
        var repository = new FileRepository(testDatabase.ConnectionFactory);

        long id = repository.UpsertByStableId("volumeA:3003", @"C:\Docs\OldAlias.txt", "OldAlias.txt");
        repository.UpsertByStableId("volumeA:3003", @"C:\Docs\NewAlias.txt", "NewAlias.txt");

        FileRecord? record = repository.GetByKnownPath(@"C:\Docs\OldAlias.txt");

        Assert.NotNull(record);
        Assert.Equal(id, record!.Id);
        Assert.Equal(@"C:\Docs\NewAlias.txt", record.CurrentPath);
    }
}
