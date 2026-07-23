using NodeTie.Infrastructure.Hotkeys;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class HotkeySettingsServiceTests
{
    [Fact]
    public void DefaultCopySelectionBinding_IsCtrlShiftK()
    {
        HotkeyBinding expected = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.K);

        Assert.Equal(expected, HotkeySettingsService.DefaultCopySelectionBinding);
    }

    [Fact]
    public void LoadOrDefault_ReturnsDefaultWhenNoStoredValueExists()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding loaded = service.LoadOrDefault();

        Assert.Equal(HotkeyBinding.Default, loaded);
    }

    [Fact]
    public void Save_ThenLoadOrDefault_ReturnsPersistedBinding()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding binding = new(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, System.Windows.Forms.Keys.F8);
        service.Save(binding);

        HotkeyBinding loaded = service.LoadOrDefault();

        Assert.Equal(binding, loaded);
    }

    [Fact]
    public void LoadCopySelectionOrDefault_ReturnsDefaultWhenNoStoredValueExists()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding loaded = service.LoadCopySelectionOrDefault();

        Assert.Equal(HotkeySettingsService.DefaultCopySelectionBinding, loaded);
    }

    [Fact]
    public void SaveDualBindings_ThenLoadAllOrDefault_ReturnsPersistedValues()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding openPanel = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.L);
        HotkeyBinding copySelection = HotkeySettingsService.DefaultCopySelectionBinding;
        service.Save(openPanel, copySelection);

        (HotkeyBinding loadedOpenPanel, HotkeyBinding loadedCopySelection) = service.LoadAllOrDefault();

        Assert.Equal(openPanel, loadedOpenPanel);
        Assert.Equal(copySelection, loadedCopySelection);
    }

    [Fact]
    public void LoadCopyTargetOrDefault_ReturnsDefaultWhenNoStoredValueExists()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        CopyLinkTarget loaded = service.LoadCopyTargetOrDefault();

        Assert.Equal(HotkeySettingsService.DefaultCopyTarget, loaded);
    }

    [Fact]
    public void LoadRunAtLoginOrDefault_ReturnsProvidedDefaultWhenNoStoredValueExists()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        bool loadedFalseDefault = service.LoadRunAtLoginOrDefault(defaultValue: false);
        bool loadedTrueDefault = service.LoadRunAtLoginOrDefault(defaultValue: true);

        Assert.False(loadedFalseDefault);
        Assert.True(loadedTrueDefault);
    }

    [Fact]
    public void SaveDualBindingsWithCopyTargetAndRunAtLogin_ThenLoadRunAtLoginOrDefault_ReturnsPersistedValue()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding openPanel = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.L);
        HotkeyBinding copySelection = HotkeySettingsService.DefaultCopySelectionBinding;
        service.Save(openPanel, copySelection, CopyLinkTarget.OneNote, runAtLogin: true);

        bool loaded = service.LoadRunAtLoginOrDefault(defaultValue: false);

        Assert.True(loaded);
    }

    [Fact]
    public void SaveDualBindingsWithCopyTarget_ThenLoadAllWithCopyTargetOrDefault_ReturnsPersistedValues()
    {
        using var database = new SqliteTestDatabase();
        var settingsRepository = new SettingsRepository(database.ConnectionFactory);
        var service = new HotkeySettingsService(settingsRepository);

        HotkeyBinding openPanel = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.L);
        HotkeyBinding copySelection = HotkeySettingsService.DefaultCopySelectionBinding;
        service.Save(openPanel, copySelection, CopyLinkTarget.OneNote);

        (HotkeyBinding loadedOpenPanel, HotkeyBinding loadedCopySelection, CopyLinkTarget loadedCopyTarget) = service.LoadAllWithCopyTargetOrDefault();

        Assert.Equal(openPanel, loadedOpenPanel);
        Assert.Equal(copySelection, loadedCopySelection);
        Assert.Equal(CopyLinkTarget.OneNote, loadedCopyTarget);
    }

    [Fact]
    public void GetOpenPanelFallbackCandidates_StartsWithPreferredBinding()
    {
        HotkeyBinding preferred = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.F8);

        IReadOnlyList<HotkeyBinding> candidates = HotkeySettingsService.GetOpenPanelFallbackCandidates(preferred);

        Assert.Equal(preferred, candidates[0]);
        Assert.Contains(HotkeyBinding.Default, candidates);
        Assert.True(candidates.Count >= 6);
    }

    [Fact]
    public void GetCopySelectionFallbackCandidates_DeduplicatesPreferredBinding()
    {
        IReadOnlyList<HotkeyBinding> candidates = HotkeySettingsService.GetCopySelectionFallbackCandidates(HotkeySettingsService.DefaultCopySelectionBinding);

        Assert.Equal(HotkeySettingsService.DefaultCopySelectionBinding, candidates[0]);
        Assert.Equal(candidates.Count, candidates.Distinct().Count());
    }

    [Fact]
    public void GetCopySelectionFallbackCandidates_DoesNotContainNullBinding()
    {
        IReadOnlyList<HotkeyBinding> candidates = HotkeySettingsService.GetCopySelectionFallbackCandidates(HotkeySettingsService.DefaultCopySelectionBinding);

        Assert.DoesNotContain(candidates, static candidate => candidate is null);
    }
}
