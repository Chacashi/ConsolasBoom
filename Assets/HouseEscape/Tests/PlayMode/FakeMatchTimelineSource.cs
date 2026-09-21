using System;
using HouseEscape.Core.Timeline;
using UnityEngine;

namespace HouseEscape.Tests.PlayMode
{
    public sealed class FakeMatchTimelineSource : MonoBehaviour, IMatchTimelineSource
    {
        public MatchTimelineSnapshot Snapshot { get; private set; }

        public double SynchronizedTime { get; set; }

        public event Action<MatchTimelineSnapshot> SnapshotChanged;

        public void SetSnapshot(MatchTimelineSnapshot snapshot)
        {
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
    }
}
