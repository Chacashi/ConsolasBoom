using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Generators
{
    public enum BeginGeneratorAttemptResult
    {
        Started,
        InvalidGenerator,
        InvalidParticipant,
        InvalidServerTime,
        MatchNotActive,
        LightsOff,
        GeneratorAlreadyActive,
        AttemptIdsExhausted
    }

    public enum CancelGeneratorAttemptResult
    {
        Canceled,
        InvalidAttempt,
        ParticipantMismatch,
        AttemptNotActive
    }

    public enum CompleteGeneratorAttemptResult
    {
        Completed,
        InvalidAttempt,
        ParticipantMismatch,
        InvalidServerTime,
        MatchNotActive,
        LightsOff,
        AttemptNotActive,
        GeneratorAlreadyActive,
        HoldIncomplete
    }

    public readonly struct GeneratorAttemptId : IEquatable<GeneratorAttemptId>
    {
        public static readonly GeneratorAttemptId None = default;

        internal GeneratorAttemptId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public bool Equals(GeneratorAttemptId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GeneratorAttemptId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsValid ? Value.ToString() : "None";

        public static bool operator ==(GeneratorAttemptId left, GeneratorAttemptId right) => left.Equals(right);
        public static bool operator !=(GeneratorAttemptId left, GeneratorAttemptId right) => !left.Equals(right);
    }

    public readonly struct GeneratorAttempt
    {
        internal GeneratorAttempt(
            GeneratorAttemptId id,
            int generatorNumber,
            ParticipantId participantId,
            double startedAt)
        {
            Id = id;
            GeneratorNumber = generatorNumber;
            ParticipantId = participantId;
            StartedAt = startedAt;
        }

        public GeneratorAttemptId Id { get; }
        public int GeneratorNumber { get; }
        public ParticipantId ParticipantId { get; }
        public double StartedAt { get; }
        public double CompletesAt => StartedAt + GeneratorRace.RequiredHoldSeconds;
    }

    public readonly struct GeneratorCompletion
    {
        internal GeneratorCompletion(GeneratorAttempt attempt, int scoreAward)
        {
            Attempt = attempt;
            ScoreAward = scoreAward;
        }

        public GeneratorAttempt Attempt { get; }
        public int ScoreAward { get; }
        public double CompletedAt => Attempt.CompletesAt;
    }

    /// <summary>
    /// Owns the authoritative, pure generator race state. A server adapter must validate
    /// participant registration and connection ownership, target/range, and RPC provenance
    /// before forwarding an intention to this class.
    /// </summary>
    /// <remarks>
    /// Not thread-safe: it is driven only from the server's main thread. Only in-progress
    /// attempts are retained, plus a fixed-size history of recently ended attempts so late
    /// cancel/complete calls still receive a precise result. Older ended IDs report
    /// <see cref="CompleteGeneratorAttemptResult.AttemptNotActive"/>.
    /// </remarks>
    public sealed class GeneratorRace
    {
        public const int TotalGeneratorCount = 6;
        public const double RequiredHoldSeconds = 10d;
        public const int ActivationScoreAward = 200;
        public const int EndedAttemptHistoryCapacity = 32;

        private readonly bool[] _activeGenerators = new bool[TotalGeneratorCount];
        private readonly Dictionary<GeneratorAttemptId, GeneratorAttempt> _inProgress =
            new Dictionary<GeneratorAttemptId, GeneratorAttempt>();
        private readonly GeneratorAttempt[] _endedHistory = new GeneratorAttempt[EndedAttemptHistoryCapacity];
        private readonly List<GeneratorAttemptId> _scratchIds = new List<GeneratorAttemptId>(4);

        private ulong _nextAttemptId = 1;
        private bool _attemptIdsExhausted;
        private int _activeGeneratorCount;
        private int _endedHistoryNext;
        private int _endedHistoryCount;

        public int ActiveGeneratorCount => _activeGeneratorCount;

        public int InProgressAttemptCount => _inProgress.Count;

        /// <summary>Number of attempts retained in memory (in progress plus bounded history).</summary>
        public int RetainedAttemptCount => _inProgress.Count + _endedHistoryCount;

        public bool IsGeneratorActive(int generatorNumber)
        {
            if (!IsValidGeneratorNumber(generatorNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(generatorNumber));
            }

            return _activeGenerators[generatorNumber - 1];
        }

        public bool IsAttemptInProgress(GeneratorAttemptId attemptId)
        {
            return _inProgress.ContainsKey(attemptId);
        }

        public BeginGeneratorAttemptResult BeginAttempt(
            int generatorNumber,
            ParticipantId participantId,
            double serverTime,
            bool isMatchActive,
            bool areLightsOn,
            out GeneratorAttempt attempt)
        {
            attempt = default;
            CancelUnavailableAttempts(isMatchActive, areLightsOn);

            if (!isMatchActive)
            {
                return BeginGeneratorAttemptResult.MatchNotActive;
            }

            if (!areLightsOn)
            {
                return BeginGeneratorAttemptResult.LightsOff;
            }

            if (!IsValidGeneratorNumber(generatorNumber))
            {
                return BeginGeneratorAttemptResult.InvalidGenerator;
            }

            if (!participantId.IsValid)
            {
                return BeginGeneratorAttemptResult.InvalidParticipant;
            }

            if (!IsFiniteNonNegative(serverTime))
            {
                return BeginGeneratorAttemptResult.InvalidServerTime;
            }

            if (_activeGenerators[generatorNumber - 1])
            {
                return BeginGeneratorAttemptResult.GeneratorAlreadyActive;
            }

            if (_attemptIdsExhausted)
            {
                return BeginGeneratorAttemptResult.AttemptIdsExhausted;
            }

            var attemptId = new GeneratorAttemptId(_nextAttemptId);
            if (_nextAttemptId == ulong.MaxValue)
            {
                _attemptIdsExhausted = true;
            }
            else
            {
                _nextAttemptId++;
            }

            attempt = new GeneratorAttempt(attemptId, generatorNumber, participantId, serverTime);
            _inProgress.Add(attemptId, attempt);
            return BeginGeneratorAttemptResult.Started;
        }

        public CancelGeneratorAttemptResult CancelAttempt(
            GeneratorAttemptId attemptId,
            ParticipantId participantId)
        {
            if (!attemptId.IsValid)
            {
                return CancelGeneratorAttemptResult.InvalidAttempt;
            }

            if (_inProgress.TryGetValue(attemptId, out GeneratorAttempt attempt))
            {
                if (attempt.ParticipantId != participantId)
                {
                    return CancelGeneratorAttemptResult.ParticipantMismatch;
                }

                EndAttempt(attemptId);
                return CancelGeneratorAttemptResult.Canceled;
            }

            if (TryGetEndedAttempt(attemptId, out GeneratorAttempt ended))
            {
                return ended.ParticipantId != participantId
                    ? CancelGeneratorAttemptResult.ParticipantMismatch
                    : CancelGeneratorAttemptResult.AttemptNotActive;
            }

            return WasIssued(attemptId)
                ? CancelGeneratorAttemptResult.AttemptNotActive
                : CancelGeneratorAttemptResult.InvalidAttempt;
        }

        public CompleteGeneratorAttemptResult CompleteAttempt(
            GeneratorAttemptId attemptId,
            ParticipantId participantId,
            double serverTime,
            bool isMatchActive,
            bool areLightsOn,
            out int scoreAward)
        {
            scoreAward = 0;
            CancelUnavailableAttempts(isMatchActive, areLightsOn);

            if (!isMatchActive)
            {
                return CompleteGeneratorAttemptResult.MatchNotActive;
            }

            if (!areLightsOn)
            {
                return CompleteGeneratorAttemptResult.LightsOff;
            }

            if (!attemptId.IsValid)
            {
                return CompleteGeneratorAttemptResult.InvalidAttempt;
            }

            bool inProgress = _inProgress.TryGetValue(attemptId, out GeneratorAttempt attempt);
            if (!inProgress && !TryGetEndedAttempt(attemptId, out attempt))
            {
                return WasIssued(attemptId)
                    ? CompleteGeneratorAttemptResult.AttemptNotActive
                    : CompleteGeneratorAttemptResult.InvalidAttempt;
            }

            if (attempt.ParticipantId != participantId)
            {
                return CompleteGeneratorAttemptResult.ParticipantMismatch;
            }

            if (!IsFiniteNonNegative(serverTime) || serverTime < attempt.StartedAt)
            {
                return CompleteGeneratorAttemptResult.InvalidServerTime;
            }

            if (_activeGenerators[attempt.GeneratorNumber - 1])
            {
                if (inProgress)
                {
                    EndAttempt(attemptId);
                }

                return CompleteGeneratorAttemptResult.GeneratorAlreadyActive;
            }

            if (!inProgress)
            {
                return CompleteGeneratorAttemptResult.AttemptNotActive;
            }

            if (serverTime - attempt.StartedAt < RequiredHoldSeconds)
            {
                return CompleteGeneratorAttemptResult.HoldIncomplete;
            }

            Activate(attempt.GeneratorNumber);
            scoreAward = ActivationScoreAward;
            return CompleteGeneratorAttemptResult.Completed;
        }

        /// <summary>
        /// Completes every in-progress attempt whose full hold finished at or before both
        /// <paramref name="serverTime"/> and <paramref name="availableUntil"/> (the end of the
        /// lights-on window it ran in). Attempts are resolved earliest-finishing first, ties by
        /// attempt ID, so the first valid finisher wins each generator. Call this before
        /// canceling for lights-off or match end so a hold that finished in time is never lost
        /// to a late frame.
        /// </summary>
        public int CompleteDueAttempts(
            double serverTime,
            double availableUntil,
            ICollection<GeneratorCompletion> completions)
        {
            if (double.IsNaN(serverTime) || double.IsNaN(availableUntil))
            {
                return 0;
            }

            double cutoff = Math.Min(serverTime, availableUntil);
            int completed = 0;
            while (TryGetEarliestDueAttempt(cutoff, out GeneratorAttempt attempt))
            {
                if (_activeGenerators[attempt.GeneratorNumber - 1])
                {
                    EndAttempt(attempt.Id);
                    continue;
                }

                Activate(attempt.GeneratorNumber);
                completions?.Add(new GeneratorCompletion(attempt, ActivationScoreAward));
                completed++;
            }

            return completed;
        }

        public int ApplyMatchAndLightState(bool isMatchActive, bool areLightsOn)
        {
            return CancelUnavailableAttempts(isMatchActive, areLightsOn);
        }

        private bool TryGetEarliestDueAttempt(double cutoff, out GeneratorAttempt earliest)
        {
            earliest = default;
            bool found = false;
            foreach (GeneratorAttempt attempt in _inProgress.Values)
            {
                if (attempt.CompletesAt > cutoff)
                {
                    continue;
                }

                if (!found
                    || attempt.StartedAt < earliest.StartedAt
                    || (attempt.StartedAt.Equals(earliest.StartedAt) && attempt.Id.Value < earliest.Id.Value))
                {
                    earliest = attempt;
                    found = true;
                }
            }

            return found;
        }

        private void Activate(int generatorNumber)
        {
            _activeGenerators[generatorNumber - 1] = true;
            _activeGeneratorCount++;
            _scratchIds.Clear();
            foreach (GeneratorAttempt attempt in _inProgress.Values)
            {
                if (attempt.GeneratorNumber == generatorNumber)
                {
                    _scratchIds.Add(attempt.Id);
                }
            }

            for (int index = 0; index < _scratchIds.Count; index++)
            {
                EndAttempt(_scratchIds[index]);
            }
        }

        private int CancelUnavailableAttempts(bool isMatchActive, bool areLightsOn)
        {
            if (isMatchActive && areLightsOn)
            {
                return 0;
            }

            _scratchIds.Clear();
            _scratchIds.AddRange(_inProgress.Keys);
            for (int index = 0; index < _scratchIds.Count; index++)
            {
                EndAttempt(_scratchIds[index]);
            }

            return _scratchIds.Count;
        }

        private void EndAttempt(GeneratorAttemptId attemptId)
        {
            if (!_inProgress.TryGetValue(attemptId, out GeneratorAttempt attempt))
            {
                return;
            }

            _inProgress.Remove(attemptId);
            _endedHistory[_endedHistoryNext] = attempt;
            _endedHistoryNext = (_endedHistoryNext + 1) % EndedAttemptHistoryCapacity;
            if (_endedHistoryCount < EndedAttemptHistoryCapacity)
            {
                _endedHistoryCount++;
            }
        }

        private bool TryGetEndedAttempt(GeneratorAttemptId attemptId, out GeneratorAttempt attempt)
        {
            for (int index = 0; index < _endedHistoryCount; index++)
            {
                if (_endedHistory[index].Id == attemptId)
                {
                    attempt = _endedHistory[index];
                    return true;
                }
            }

            attempt = default;
            return false;
        }

        private bool WasIssued(GeneratorAttemptId attemptId)
        {
            return attemptId.IsValid && (_attemptIdsExhausted || attemptId.Value < _nextAttemptId);
        }

        private static bool IsValidGeneratorNumber(int generatorNumber)
        {
            return generatorNumber >= 1 && generatorNumber <= TotalGeneratorCount;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }
}
