using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class MonitorChoicesTests
{
    private static MonitorInfo M(string id, bool primary, int x, int y, int w, int h, double scale) =>
        new(id, primary, new PixelRect(x, y, w, h), new PixelRect(x, y, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 0, 2560, 1440, 1.5);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 2560, 0, 1920, 1080, 1.0);

    [Fact]
    public void Primary_comes_first_then_monitors_from_left_to_right()
    {
        var choices = MonitorChoices.Build([Side, Main], null);

        choices.Select(c => c.Value).Should().Equal("", @"\\.\DISPLAY1", @"\\.\DISPLAY2");
        choices.Select(c => c.Label).Should().Equal(
            "Écran principal",
            "Écran 1 — 2560 × 1440, 150" + FrenchText.Nbsp + "% (principal)",
            "Écran 2 — 1920 × 1080, 100" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void A_monitor_left_of_the_primary_is_numbered_first()
    {
        var left = M(@"\\.\DISPLAY3", false, -1920, 0, 1920, 1080, 1.0);
        MonitorChoices.Ordered([Main, left]).Select(m => m.DeviceId).Should().Equal(@"\\.\DISPLAY3", @"\\.\DISPLAY1");
    }

    [Fact]
    public void An_absent_saved_monitor_stays_listed()
    {
        var choices = MonitorChoices.Build([Main], @"\\.\DISPLAY9");

        choices.Should().HaveCount(3);
        choices[^1].Value.Should().Be(@"\\.\DISPLAY9");
        choices[^1].Label.Should().Be(@"Écran absent (\\.\DISPLAY9) — pilule sur l'écran principal");
    }

    [Fact]
    public void A_saved_monitor_with_another_case_is_not_listed_twice()
    {
        var choices = MonitorChoices.Build([Main, Side], @"\\.\display2");

        choices.Should().HaveCount(3);
        MonitorChoices.KeyFor(choices, @"\\.\display2").Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void Keys_and_device_ids_map_the_primary_choice_to_null()
    {
        var choices = MonitorChoices.Build([Main], null);

        MonitorChoices.KeyFor(choices, null).Should().Be(MonitorChoices.PrimaryKey);
        MonitorChoices.DeviceIdFor(MonitorChoices.PrimaryKey).Should().BeNull();
        MonitorChoices.DeviceIdFor(null).Should().BeNull();
        MonitorChoices.DeviceIdFor(@"\\.\DISPLAY1").Should().Be(@"\\.\DISPLAY1");
    }
}
