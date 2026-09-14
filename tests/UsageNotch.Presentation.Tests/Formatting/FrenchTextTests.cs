using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class FrenchTextTests
{
    // Lundi 14 septembre 2026, 12:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.734, "73")]
    [InlineData(0.736, "74")]
    [InlineData(1.0, "100")]
    [InlineData(1.7, "100")]
    [InlineData(-0.2, "0")]
    public void Percent_is_a_rounded_integer_with_a_non_breaking_space(double fraction, string expected) =>
        FrenchText.Percent(fraction).Should().Be(expected + FrenchText.Nbsp + "%");

    [Fact]
    public void Reset_in_the_past_is_imminent() =>
        FrenchText.ResetCopy(Now.AddSeconds(-5), Now, Utc).Should().Be("Réinitialisation imminente");

    [Theory]
    [InlineData(20, "Réinitialisation dans 1 min")]
    [InlineData(51 * 60, "Réinitialisation dans 51 min")]
    [InlineData(59 * 60 + 40, "Réinitialisation dans 60 min")]
    public void Reset_under_an_hour_is_relative(int seconds, string expected) =>
        FrenchText.ResetCopy(Now.AddSeconds(seconds), Now, Utc).Should().Be(expected);

    [Fact]
    public void Reset_later_today_or_tomorrow_within_24h_shows_the_time() =>
        FrenchText.ResetCopy(Now.AddHours(5).AddMinutes(7), Now, Utc).Should().Be("Réinitialisation à 17:07");

    [Fact]
    public void Reset_beyond_24h_shows_the_abbreviated_day_and_time() =>
        FrenchText.ResetCopy(new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero), Now, Utc)
            .Should().Be("Réinitialisation jeu. 00:00");

    [Fact]
    public void Reset_time_uses_the_given_time_zone()
    {
        var paris = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");
        FrenchText.ResetCopy(Now.AddHours(3), Now, paris).Should().Be("Réinitialisation à 17:00");
    }

    [Theory]
    [InlineData(10, "Mis à jour à l'instant")]
    [InlineData(60, "Mis à jour il y a 1 min")]
    [InlineData(12 * 60, "Mis à jour il y a 12 min")]
    [InlineData(3 * 3600 + 100, "Mis à jour il y a 3 h")]
    [InlineData(50 * 3600, "Mis à jour il y a 2 j")]
    public void Updated_ago_scales_its_unit(int secondsAgo, string expected) =>
        FrenchText.UpdatedAgo(Now.AddSeconds(-secondsAgo), Now).Should().Be(expected);

    [Theory]
    [InlineData(42, "42 s")]
    [InlineData(3 * 60 + 10, "3 min")]
    [InlineData(65 * 60, "1 h 05")]
    [InlineData(0, "0 s")]
    public void Duration_is_compact(int seconds, string expected) =>
        FrenchText.Duration(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
}
