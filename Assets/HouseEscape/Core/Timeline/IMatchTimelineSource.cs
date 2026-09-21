using System;

namespace HouseEscape.Core.Timeline
{
    public interface IMatchTimelineSource
    {
        MatchTimelineSnapshot Snapshot { get; }

        double SynchronizedTime { get; }

        event Action<MatchTimelineSnapshot> SnapshotChanged;
    }
}
