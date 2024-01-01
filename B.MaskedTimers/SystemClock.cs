using System;

namespace B.MaskedTimers;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
