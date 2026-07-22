using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed class HotkeySettingsService
{
    private const string OpenPanelSettingsKey = "hotkey.openPanel";
    private const string LegacySettingsKey = "hotkey.binding";
    private const string CopySelectionSettingsKey = "hotkey.copySelection";
    private const string CopyTargetSettingsKey = "copy.target";
    public static HotkeyBinding DefaultCopySelectionBinding { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.K);
    public static CopyLinkTarget DefaultCopyTarget { get; } = CopyLinkTarget.Obsidian;
    private static readonly HotkeyBinding[] OpenPanelFallbackBindings =
    [
        HotkeyBinding.Default,
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.J),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.U),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.L),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.J),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.U)
    ];
    private static readonly HotkeyBinding[] CopySelectionFallbackBindings =
    [
        DefaultCopySelectionBinding,
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.I),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.M),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.K),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.I),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, System.Windows.Forms.Keys.M)
    ];
    private readonly SettingsRepository _settingsRepository;

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

    public static IReadOnlyList<HotkeyBinding> GetOpenPanelFallbackCandidates(HotkeyBinding preferredBinding)
    {
        return BuildFallbackCandidates(preferredBinding, OpenPanelFallbackBindings);
    }

    public static IReadOnlyList<HotkeyBinding> GetCopySelectionFallbackCandidates(HotkeyBinding preferredBinding)
    {
        return BuildFallbackCandidates(preferredBinding, CopySelectionFallbackBindings);
    }

    private static IReadOnlyList<HotkeyBinding> BuildFallbackCandidates(HotkeyBinding preferredBinding, IReadOnlyList<HotkeyBinding> fallbackBindings)
    {
        List<HotkeyBinding> candidates = [preferredBinding];
        foreach (HotkeyBinding candidate in fallbackBindings)
        {
            if (!candidates.Contains(candidate))
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }
}
