using System.Windows.Forms;
using NodeTie.Infrastructure.Hotkeys;
using Xunit;

namespace NodeTie.Tests;

public sealed class HotkeyBindingTests
{
    [Fact]
    public void TryParse_AcceptsCommonHotkeyString()
    {
        bool ok = HotkeyBinding.TryParse("Ctrl+Shift+L", out var binding);

        Assert.True(ok);
        Assert.Equal(Keys.L, binding.Key);
        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Shift));
    }

    [Fact]
    public void TryParse_RejectsValueWithoutModifier()
    {
        bool ok = HotkeyBinding.TryParse("L", out _);

        Assert.False(ok);
    }

    [Fact]
    public void ToString_RoundTripsWithTryParse()
    {
        var original = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F6);
        string serialized = original.ToString();

        bool parsed = HotkeyBinding.TryParse(serialized, out var roundTripped);

        Assert.True(parsed);
        Assert.Equal(original, roundTripped);
    }
}
