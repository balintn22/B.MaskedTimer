using B.Atomics;
using B.Intervals;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using YAGuard;

namespace B.MaskedTimers;

// Note: all Datetimes are UTC

/// <summary>
/// Implements a timer with masking intervals.
/// The timer will only fire in the specified intervals.
/// </summary>
public class MaskedTimer : IDisposable
{
    private readonly IClock _clock;

    private List<Interval<DateTime>> _intervalsUtc { get; set; }

    private TimeSpan _frequency { get; set; }

    private Action _action;

    private Timer _timer;

    private int _actionExecuting = 0; // 0 means not executing, 1 means executing.

    /// <summary>
    /// When set, action calls may be skipped if the previous action has not yet concluded
    /// by the time the timer fires.
    /// When cleared, missed action calls resulting from the above scenario are catered for,
    /// by making up for the missed action calls when the currently running one concludes.
    /// Defaults to true, allowing skipped ation calls.
    /// </summary>
    public bool AllowSkip { get; set; } = true;

    /// <summary>
    /// Contains the time of missed action calls, as a result of the preceding action having not
    /// concluded by the timer fired.
    /// </summary>
    private ConcurrentQueue<DateTime> _missedActionCallTimesUtc { get; set; } = new ConcurrentQueue<DateTime>();

    /// <summary>
    /// DI-ready constructor.
    /// </summary>
    /// <param name="clock">Specifies the IClock to use.</param>
    /// <param name="intervalsUtc">
    /// Specifies the allowed intervals, in which the timer will fire.
    /// Intervals should not overlap.
    /// </param>
    /// <param name="frequency">
    /// Specifies the frequency in terms of the time span between two timer fires.
    /// When one timer action is completed, the timer is re-trimmed to fire after this interval.
    /// </param>
    /// <param name="action">Action to execute when the timer fires.</param>
    /// <exception cref="ArgumentException">If intervals overlap.</exception>
    public MaskedTimer(
        IClock clock,
        List<Interval<DateTime>> intervalsUtc,
        TimeSpan frequency,
        Action action)
    {
        Guard.AgainstNullOrEmptyCollection(intervalsUtc);

        if (intervalsUtc.Any(i1 => intervalsUtc.Any(i2 => i1 != i2 && i1.Intersects(i2))))
            throw new ArgumentException("Intervals should not overlap.");

        _clock = clock;

        _intervalsUtc = intervalsUtc.OrderBy(iv => iv.Start).ToList();
        _frequency = frequency;
        _action = action;

        _timer = new Timer();
        _timer.AutoReset = false;
        _timer.Elapsed += _timer_Elapsed!;
        StartTimerTillNextFire();
    }

    /// <summary>
    /// Non-DI ctor
    /// </summary>
    /// <param name="intervalsUtc">
    /// Specifies the allowed intervals, in which the timer will fire.
    /// Intervals should not overlap.
    /// </param>
    /// <param name="frequency">
    /// Specifies the frequency in terms of the time span between two timer fires.
    /// When one timer action is completed, the timer is re-trimmed to fire after this interval.
    /// </param>
    /// <param name="action">Action to execute when the timer fires.</param>
    /// <exception cref="ArgumentException">If intervals overlap.</exception>
    public MaskedTimer(
        List<Interval<DateTime>> intervalsUtc,
        TimeSpan frequency,
        Action action)
        : this(new SystemClock(), intervalsUtc, frequency, action)
    {
    }

    public void Dispose()
    {
        _timer.Elapsed -= _timer_Elapsed!;
    }

    private void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        _timer.Enabled = false;
        
        try
        {
            if (Atomic.TestAndSet(ref _actionExecuting, replaceWithValue: 1, testForValue: 0))
            {   // Action is not executing.
                StartTimerTillNextFire();
                _action();
                _actionExecuting = 0;
            }
            else
            {   // Action is executing as a result of another timer fire event.
                StartTimerTillNextFire();
                _missedActionCallTimesUtc.Enqueue(e.SignalTime);
            }
        }
        finally { StartTimerTillNextFire(); }
    }

    /// <summary>
    /// Calculates the time of the next fire, trims the timer and starts it
    /// to fire at that time.
    /// </summary>
    private void StartTimerTillNextFire()
    {
        if (TryCalculateTimeToNextFire(out TimeSpan timeTillNextFire))
        {
            _timer.Interval = timeTillNextFire.TotalMilliseconds;
            _timer.Start();
        }
    }

    public bool TryCalculateTimeToNextFire(out TimeSpan timeTillNextFire)
    {
        if (!TryCalculateTimeOfNextFireUtc(out DateTime nextFire))
        {
            timeTillNextFire = TimeSpan.MaxValue;
            return false;
        }

        timeTillNextFire = nextFire - _clock.UtcNow;
        return true;
    }

    public bool TryCalculateTimeOfNextFireUtc(out DateTime nextFireUtc)
    {
        var nextFire = _clock.UtcNow + _frequency;
        bool nextFireIsInInterval = _intervalsUtc.Any(iv => iv.Contains(nextFire));

        if (nextFireIsInInterval)
        {
            nextFireUtc = nextFire;
            return true;
        }

        if (nextFire < _intervalsUtc[0].Start)
        {
            // nextFireUtc is before the first interval.
            nextFireUtc = _intervalsUtc[0].Start;
            return true;
        }

        for (int i = 0; i < _intervalsUtc.Count - 1; i++)
        {
            var gap = new Interval<DateTime>(_intervalsUtc[i].End, _intervalsUtc[i + 1].Start);
            if (gap.Contains(nextFire))
            {
                // Next fire time is before interval i+1. Return the start of interval i + 1.
                nextFireUtc = _intervalsUtc[i + 1].Start;
                return true;
            }
        }

        // Next fire time is after the last interval
        nextFireUtc = DateTime.MaxValue;
        return false;
    }
}
