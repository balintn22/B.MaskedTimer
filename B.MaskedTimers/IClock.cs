using System;

namespace B.MaskedTimers;

public interface IClock
{
    DateTime UtcNow { get; }
}
