using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed class HotkeySettingsService
{
    private const string OpenPanelSettingsKey = "hotkey.openPanel";
    private const string LegacySettingsKey = "hotkey.binding";
    private const string CopySelectionSettingsKey = "hotkey.copySelection";
    private const string CopyTargetSettingsKey = "copy.target";
    private readonly SettingsRepository _settingsRepository;

    public static HotkeyBinding DefaultCopySelectionBinding { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.K);
    public static CopyLinkTarget DefaultCopyTarget { get; } = CopyLinkTarget.Obsidian;

    public HotkeySettingsService(SettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public HotkeyBinding LoadOrDefault()
    {
        return LoadOpenPanelOrDefault();
    }

    public HotkeyBinding LoadOpenPanelOrDefault()
    {
        string? stored = _settingsRepository.GetValue(OpenPanelSettingsKey)
            ?? _settingsRepository.GetValue(LegacySettingsKey);
        if (HotkeyBinding.TryParse(stored, out var binding))
        {
            return binding;
        }

        return HotkeyBinding.Default;
    }

    public HotkeyBinding LoadCopySelectionOrDefault()
    {
        string? stored = _settingsRepository.GetValue(CopySelectionSettingsKey);
        if (HotkeyBinding.TryParse(stored, out var binding))
        {
            return binding;
        }

        return DefaultCopySelectionBinding;
    }

    public (HotkeyBinding OpenPanel, HotkeyBinding CopySelection) LoadAllOrDefault()
    {
        return (LoadOpenPanelOrDefault(), LoadCopySelectionOrDefault());
    }

    public (HotkeyBinding OpenPanel, HotkeyBinding CopySelection, CopyLinkTarget CopyTarget) LoadAllWithCopyTargetOrDefault()
    {
        return (LoadOpenPanelOrDefault(), LoadCopySelectionOrDefault(), LoadCopyTargetOrDefault());
    }

    public CopyLinkTarget LoadCopyTargetOrDefault()
    {
        string? stored = _settingsRepository.GetValue(CopyTargetSettingsKey);
        if (System.Enum.TryParse<CopyLinkTarget>(stored, ignoreCase: true, out var target))
        {
            return target;
        }

        return DefaultCopyTarget;
    }

    public void Save(HotkeyBinding binding)
    {
        Save(binding, LoadCopySelectionOrDefault());
    }

    public void Save(HotkeyBinding openPanelBinding, HotkeyBinding copySelectionBinding)
    {
        Save(openPanelBinding, copySelectionBinding, LoadCopyTargetOrDefault());
    }

    public void Save(HotkeyBinding openPanelBinding, HotkeyBinding copySelectionBinding, CopyLinkTarget copyTarget)
    {
        _settingsRepository.SetValue(OpenPanelSettingsKey, openPanelBinding.ToString());
        _settingsRepository.SetValue(CopySelectionSettingsKey, copySelectionBinding.ToString());
        _settingsRepository.SetValue(CopyTargetSettingsKey, copyTarget.ToString());
    }
}
