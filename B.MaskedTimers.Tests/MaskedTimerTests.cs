using B.Intervals;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace B.MaskedTimers.Tests;

[TestClass()]
public class MaskedTimerTests
{
    Mock<IClock> _mockClock = new Mock<IClock>();

    [TestMethod()]
    public void TryCalculateTimeOfNextFireUtc_ShouldReturnExpectedValue_WhenFirstFireIsInFirstInterval()
    {
        _mockClock
            .SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2000, 01, 01, 00, 00, 00));

        var now = _mockClock.Object.UtcNow;

        var start = now + TimeSpan.FromMilliseconds(100);
        var end = start + TimeSpan.FromMilliseconds(200);

        int count = 0;

        var sut = new MaskedTimer(
            _mockClock.Object,
            intervalsUtc: new List<Interval<DateTime>>
            {
                new Interval<DateTime>(start, end),
            },
            frequency: TimeSpan.FromMilliseconds(200),
            action: () => count++);

        var result = sut.TryCalculateTimeOfNextFireUtc(out DateTime nextFireUtc);

        Thread.Sleep(end - _mockClock.Object.UtcNow + TimeSpan.FromMilliseconds(20));

        result.Should().BeTrue();
        nextFireUtc.Should().Be(now + TimeSpan.FromMilliseconds(200));
        count.Should().Be(1);
    }

    [TestMethod()]
    public void TryCalculateTimeOfNextFireUtc_ShouldReturnExpectedValue_WhenFirstFireIsBeforeFirstInterval()
    {
        _mockClock
            .SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2000, 01, 01, 00, 00, 00));

        var now = _mockClock.Object.UtcNow;

        var start = now + TimeSpan.FromMilliseconds(1000);
        var end = start + TimeSpan.FromMilliseconds(100);

        int count = 0;

        var sut = new MaskedTimer(
            _mockClock.Object,
            intervalsUtc: new List<Interval<DateTime>>
            {
                new Interval<DateTime>(start, end),
            },
            frequency: TimeSpan.FromMilliseconds(200),
            action: () => count++);

        var result = sut.TryCalculateTimeOfNextFireUtc(out DateTime nextFireUtc);

        Thread.Sleep(end - _mockClock.Object.UtcNow + TimeSpan.FromMilliseconds(20));

        result.Should().BeTrue();
        nextFireUtc.Should().Be(start);
        count.Should().Be(1);
    }

    [TestMethod()]
    public void TryCalculateTimeOfNextFireUtc_ShouldReturnExpectedValue_WhenFirstFireIsBeforeNthInterval()
    {
        _mockClock
            .SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2000, 01, 01, 00, 00, 00));

        var now = _mockClock.Object.UtcNow;
        int count = 0;
        var intervals = new List<Interval<DateTime>>
            {
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(100), now + TimeSpan.FromMilliseconds(110)),
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(200), now + TimeSpan.FromMilliseconds(240)),
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(300), now + TimeSpan.FromMilliseconds(320)),
            };

        var sut = new MaskedTimer(
            _mockClock.Object,
            intervalsUtc: intervals,
            frequency: TimeSpan.FromMilliseconds(250),
            action: () => count++);

        var result = sut.TryCalculateTimeOfNextFireUtc(out DateTime nextFireUtc);

        Thread.Sleep(intervals.Last().End - _mockClock.Object.UtcNow + TimeSpan.FromMilliseconds(20));

        result.Should().BeTrue();
        nextFireUtc.Should().Be(intervals.Last().Start);
        count.Should().Be(1);
    }

    [TestMethod()]
    public void TryCalculateTimeOfNextFireUtc_ShouldReturnMaxDateTime_WhenFirstFireIsAfterLastInterval()
    {
        _mockClock
            .SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2000, 01, 01, 00, 00, 00));

        var now = _mockClock.Object.UtcNow;
        int count = 0;
        var intervals = new List<Interval<DateTime>>
            {
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(100), now + TimeSpan.FromMilliseconds(110)),
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(200), now + TimeSpan.FromMilliseconds(240)),
                new Interval<DateTime>(now + TimeSpan.FromMilliseconds(300), now + TimeSpan.FromMilliseconds(320)),
            };

        var sut = new MaskedTimer(
            _mockClock.Object,
            intervalsUtc: intervals,
            frequency: TimeSpan.FromMilliseconds(400),
            action: () => count++);

        var result = sut.TryCalculateTimeOfNextFireUtc(out DateTime nextFireUtc);

        Thread.Sleep(intervals.Last().End - _mockClock.Object.UtcNow + TimeSpan.FromMilliseconds(20));

        result.Should().BeFalse();
        nextFireUtc.Should().Be(DateTime.MaxValue);
        count.Should().Be(0);
    }
}