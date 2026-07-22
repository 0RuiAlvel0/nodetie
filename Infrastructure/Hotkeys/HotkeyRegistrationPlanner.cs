using System;
using System.Collections.Generic;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed record HotkeyRegistrationPlanResult(
    bool Success,
    HotkeyBinding? RegisteredOpenPanelBinding,
    HotkeyBinding? RegisteredCopySelectionBinding,
    bool UsedOpenPanelFallback,
    bool UsedCopySelectionFallback);

public static class HotkeyRegistrationPlanner
{
    public static HotkeyRegistrationPlanResult Resolve(
        HotkeyBinding preferredOpenPanelBinding,
        IReadOnlyList<HotkeyBinding> openPanelCandidates,
        HotkeyBinding preferredCopySelectionBinding,
        IReadOnlyList<HotkeyBinding> copySelectionCandidates,
        Func<HotkeyBinding, bool> tryRegisterOpenPanel,
        Action unregisterOpenPanel,
        Func<HotkeyBinding, bool> tryRegisterCopySelection)
    {
        foreach (HotkeyBinding openPanelCandidate in openPanelCandidates)
        {
            if (!tryRegisterOpenPanel(openPanelCandidate))
            {
                continue;
            }

            foreach (HotkeyBinding copySelectionCandidate in copySelectionCandidates)
            {
                if (copySelectionCandidate == openPanelCandidate)
                {
                    continue;
                }

                if (!tryRegisterCopySelection(copySelectionCandidate))
                {
                    continue;
                }

                return new HotkeyRegistrationPlanResult(
                    Success: true,
                    RegisteredOpenPanelBinding: openPanelCandidate,
                    RegisteredCopySelectionBinding: copySelectionCandidate,
                    UsedOpenPanelFallback: openPanelCandidate != preferredOpenPanelBinding,
                    UsedCopySelectionFallback: copySelectionCandidate != preferredCopySelectionBinding);
            }

            unregisterOpenPanel();
        }

        return new HotkeyRegistrationPlanResult(
            Success: false,
            RegisteredOpenPanelBinding: null,
            RegisteredCopySelectionBinding: null,
            UsedOpenPanelFallback: false,
            UsedCopySelectionFallback: false);
    }
}