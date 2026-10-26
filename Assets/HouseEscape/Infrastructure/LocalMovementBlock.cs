using System;

namespace HouseEscape.Infrastructure
{
    /// <summary>Hold interactions that block a local participant's walk input while in progress.</summary>
    [Flags]
    public enum LocalMovementBlock
    {
        None = 0,
        Generator = 1,
        Revival = 2,
        Charging = 4,
        Hiding = 8
    }
}
