using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Generators
{
    /// <summary>
    /// Technical anti-spam guard for begin intentions: a participant may submit at most one
    /// begin request per <see cref="MinimumBeginIntervalSeconds"/> of server time. Callers must
    /// validate participant ownership first so memory stays bounded by registered participants.
    /// </summary>
    public sealed class GeneratorBeginRateLimiter
    {
        public const double MinimumBeginIntervalSeconds = 0.25d;

        private readonly Dictionary<ParticipantId, double> _lastRequestTimes =
            new Dictionary<ParticipantId, double>(4);

        public bool TryAccept(ParticipantId participantId, double serverTime)
        {
            if (!participantId.IsValid || double.IsNaN(serverTime) || double.IsInfinity(serverTime))
            {
                return false;
            }

            if (_lastRequestTimes.TryGetValue(participantId, out double lastRequestTime)
                && serverTime >= lastRequestTime
                && serverTime - lastRequestTime < MinimumBeginIntervalSeconds)
            {
                return false;
            }

            _lastRequestTimes[participantId] = serverTime;
            return true;
        }

        public void Clear()
        {
            _lastRequestTimes.Clear();
        }
    }
}
