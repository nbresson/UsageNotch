using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class HoverControllerTests
{
    private static (HoverController Hover, FakeTimeProvider Time, Func<int> Changes) Build()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var hover = new HoverController(time);
        var count = 0;
        hover.Changed += () => count++;
        return (hover, time, () => count);
    }

    [Fact]
    public void Starts_closed_folded_and_unlocked()
    {
        var (h, _, changes) = Build();
        h.CardVisible.Should().BeFalse();
        h.Unfolded.Should().BeFalse();
        h.Locked.Should().BeFalse();
        changes().Should().Be(0);
    }

    [Fact]
    public void Entering_the_pill_opens_and_unfolds_once()
    {
        var (h, _, changes) = Build();
        h.PointerEnteredPill();
        h.PointerEnteredPill();
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();
        changes().Should().Be(1);
    }

    [Fact]
    public void Leaving_closes_after_250ms_and_folds_after_400ms()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();

        time.Advance(TimeSpan.FromMilliseconds(249));
        h.CardVisible.Should().BeTrue();

        time.Advance(TimeSpan.FromMilliseconds(1));
        h.CardVisible.Should().BeFalse();
        h.Unfolded.Should().BeTrue();

        time.Advance(TimeSpan.FromMilliseconds(150));
        h.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void Crossing_from_the_pill_to_the_card_keeps_it_open()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromMilliseconds(100));
        h.PointerEnteredCard();
        time.Advance(TimeSpan.FromSeconds(2));
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();

        h.PointerLeftCard();
        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_locked_card_survives_the_pointer_leaving_and_closes_after_unlock()
    {
        var (h, time, _) = Build();
        h.ToggleLock();
        h.Locked.Should().BeTrue();
        h.CardVisible.Should().BeTrue();

        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromSeconds(3));
        h.CardVisible.Should().BeTrue();

        h.ToggleLock();
        h.Locked.Should().BeFalse();
        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void Unlocking_while_hovered_keeps_the_card_open()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.ToggleLock();
        h.ToggleLock();
        time.Advance(TimeSpan.FromSeconds(1));
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Peek_opens_for_five_seconds_then_closes()
    {
        var (h, time, _) = Build();
        h.Peek();
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();

        time.Advance(HoverController.PeekDuration);
        h.CardVisible.Should().BeTrue();

        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
        time.Advance(HoverController.FoldDelay);
        h.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void Peek_while_hovered_does_not_close_when_it_ends()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(10));
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void A_second_peek_restarts_the_five_seconds()
    {
        var (h, time, _) = Build();
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(4));
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(4) + HoverController.CloseDelay);
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Repeated_pointer_left_notifications_do_not_postpone_the_close()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromMilliseconds(200));
        h.PointerLeftPill();
        h.PointerLeftCard();
        time.Advance(TimeSpan.FromMilliseconds(50));
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void Dispose_stops_pending_timers()
    {
        var (h, time, changes) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        var before = changes();
        h.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));
        changes().Should().Be(before);
    }
}
