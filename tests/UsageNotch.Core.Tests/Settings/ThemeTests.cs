using FluentAssertions;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Settings;

public class ThemeTests
{
    [Fact]
    public void The_codenotch_preset_gives_each_ring_its_own_colour()
    {
        var t = Theme.Codenotch;
        t.RingSession.Should().Be("#28E07B");
        t.RingWeeklyAll.Should().Be("#57C7FF");
        t.RingWeeklyScoped.Should().Be("#C792EA");
    }

    [Fact]
    public void The_monochrome_preset_separates_the_rings_by_lightness()
    {
        var t = Theme.Monochrome;
        new[] { t.RingSession, t.RingWeeklyAll, t.RingWeeklyScoped }
            .Should().Equal("#E0E0E0", "#A0A0A0", "#707070");
    }

    [Fact]
    public void Every_preset_keeps_its_three_ring_colours_distinct()
    {
        foreach (var t in new[] { Theme.Codenotch, Theme.Monochrome })
        {
            new[] { t.RingSession, t.RingWeeklyAll, t.RingWeeklyScoped }
                .Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void A_theme_missing_its_ring_colours_falls_back_to_codenotch()
    {
        var partial = Theme.Monochrome with { RingSession = "", RingWeeklyAll = null!, RingWeeklyScoped = "" };

        var clamped = partial.Clamp();

        clamped.RingSession.Should().Be(Theme.Codenotch.RingSession);
        clamped.RingWeeklyAll.Should().Be(Theme.Codenotch.RingWeeklyAll);
        clamped.RingWeeklyScoped.Should().Be(Theme.Codenotch.RingWeeklyScoped);
    }

    [Fact]
    public void The_system_accent_preset_tints_the_session_ring_only()
    {
        var t = Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, "#0078D4");

        t.RingSession.Should().Be("#0078D4");
        t.RingWeeklyAll.Should().Be(Theme.Codenotch.RingWeeklyAll);
        t.RingWeeklyScoped.Should().Be(Theme.Codenotch.RingWeeklyScoped);
    }

    [Fact]
    public void The_codenotch_preset_carries_the_provider_brand_colour()
    {
        Theme.Codenotch.LogoDone.Should().Be("#D97757");
    }

    [Fact]
    public void The_monochrome_preset_keeps_the_finished_logo_grey()
    {
        Theme.Monochrome.LogoDone.Should().Be("#B0B0B0");
    }

    [Fact]
    public void Every_preset_keeps_its_four_activity_colours_distinct()
    {
        foreach (var t in new[] { Theme.Codenotch, Theme.Monochrome })
        {
            new[] { t.RingTrack, t.Running, t.Attention, t.LogoDone }.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void A_theme_missing_its_brand_colour_falls_back_to_codenotch()
    {
        var partial = Theme.Monochrome with { LogoDone = "" };

        partial.Clamp().LogoDone.Should().Be(Theme.Codenotch.LogoDone);
    }
}
