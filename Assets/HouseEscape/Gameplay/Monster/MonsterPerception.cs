using System;
using System.Collections.Generic;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>Server snapshot fed to <see cref="MonsterBrain.Tick"/>.</summary>
    public readonly struct MonsterPerception
    {
        public MonsterPerception(
            bool isAwake,
            double now,
            WorldPoint monsterPosition,
            int activeParticipantCount,
            IReadOnlyList<MonsterCandidate> candidates)
        {
            IsAwake = isAwake;
            Now = now;
            MonsterPosition = monsterPosition;
            ActiveParticipantCount = activeParticipantCount;
            Candidates = candidates ?? Array.Empty<MonsterCandidate>();
        }

        /// <summary>True only while the match is active and the house lights are on.</summary>
        public bool IsAwake { get; }

        /// <summary>Authoritative server time in seconds.</summary>
        public double Now { get; }

        public WorldPoint MonsterPosition { get; }
        /// <summary>Conscious participants (GDD 7: the four-player speed bonus needs four conscious players).</summary>
        public int ActiveParticipantCount { get; }
        public IReadOnlyList<MonsterCandidate> Candidates { get; }
    }
}
