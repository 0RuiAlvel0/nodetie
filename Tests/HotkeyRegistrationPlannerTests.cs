using System.Collections.Generic;
using NodeTie.Infrastructure.Hotkeys;
using Xunit;

namespace NodeTie.Tests;

public sealed class HotkeyRegistrationPlannerTests
{
    [Fact]
    public void Resolve_UsesFallbackCandidatesWhenPreferredBindingsConflict()
    {
        HotkeyBinding preferredOpen = HotkeyBinding.Default;
        HotkeyBinding openFallback = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.J);
        HotkeyBinding preferredCopy = HotkeySettingsService.DefaultCopySelectionBinding;
        HotkeyBinding copyFallback = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.I);
        List<HotkeyBinding> registeredOpenCandidates = [];
        List<HotkeyBinding> registeredCopyCandidates = [];

        HotkeyRegistrationPlanResult result = HotkeyRegistrationPlanner.Resolve(
            preferredOpen,
            [preferredOpen, openFallback],
            preferredCopy,
            [preferredCopy, copyFallback],
            binding =>
            {
                registeredOpenCandidates.Add(binding);
                return binding == openFallback;
            },
            () => { },
            binding =>
            {
                registeredCopyCandidates.Add(binding);
                return binding == copyFallback;
            });

        Assert.True(result.Success);
        Assert.Equal(openFallback, result.RegisteredOpenPanelBinding);
        Assert.Equal(copyFallback, result.RegisteredCopySelectionBinding);
        Assert.True(result.UsedOpenPanelFallback);
        Assert.True(result.UsedCopySelectionFallback);
        Assert.Equal([preferredOpen, openFallback], registeredOpenCandidates);
        Assert.Equal([preferredCopy, copyFallback], registeredCopyCandidates);
    }

    [Fact]
    public void Resolve_SkipsDuplicateCopyBindingThatMatchesOpenBinding()
    {
        HotkeyBinding openBinding = HotkeyBinding.Default;
        HotkeyBinding copyFallback = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Forms.Keys.I);
        List<HotkeyBinding> registeredCopyCandidates = [];

        HotkeyRegistrationPlanResult result = HotkeyRegistrationPlanner.Resolve(
            openBinding,
            [openBinding],
            openBinding,
            [openBinding, copyFallback],
            _ => true,
            () => { },
            binding =>
            {
                registeredCopyCandidates.Add(binding);
                return true;
            });

        Assert.True(result.Success);
        Assert.Equal(copyFallback, result.RegisteredCopySelectionBinding);
        Assert.Equal([copyFallback], registeredCopyCandidates);
    }
}