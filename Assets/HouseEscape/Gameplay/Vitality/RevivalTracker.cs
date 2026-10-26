using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// Server-side revival holds (GDD 7). A conscious participant may hold on an unconscious
    /// teammate for <see cref="VitalityRules.RevivalHoldSeconds"/>. A hold is cancelled when
    /// released, when the reviver takes damage or is no longer conscious, when the teammate is no
    /// longer unconscious, or when the teammate's recovery deadline passes first. Several revivers
    /// may hold on the same teammate; the earliest completion wins and the rest are cancelled
    /// because the teammate is then conscious. Range, aim and the first-aid kit are validated by
    /// the caller.
    /// </summary>
    public sealed class RevivalTracker
    {
        private readonly List<RevivalAttempt> _attempts = new List<RevivalAttempt>(ParticipantRegistry.MaximumParticipants);

        public IReadOnlyList<RevivalAttempt> Attempts => _attempts;

        public RevivalBeginResult Begin(ParticipantVitality reviver, ParticipantVitality target, double now)
        {
            if (!reviver.ParticipantId.IsValid || !target.ParticipantId.IsValid)
            {
                return RevivalBeginResult.InvalidParticipant;
            }

            if (reviver.ParticipantId == target.ParticipantId)
            {
                return RevivalBeginResult.CannotReviveSelf;
            }

            if (!reviver.IsConscious)
            {
                return RevivalBeginResult.ReviverNotConscious;
            }

            if (target.State != VitalityState.Unconscious)
            {
                return RevivalBeginResult.TargetNotUnconscious;
            }

            if (now >= target.RecoveryDeadline)
            {
                return RevivalBeginResult.WindowExpired;
            }

            if (TryGet(reviver.ParticipantId, out _))
            {
                return RevivalBeginResult.AlreadyReviving;
            }

            _attempts.Add(new RevivalAttempt(reviver.ParticipantId, target.ParticipantId, now));
            return RevivalBeginResult.Started;
        }

        public bool TryGet(ParticipantId reviverId, out RevivalAttempt attempt)
        {
            for (int index = 0; index < _attempts.Count; index++)
            {
                if (_attempts[index].ReviverId == reviverId)
                {
                    attempt = _attempts[index];
                    return true;
                }
            }

            attempt = default;
            return false;
        }

        /// <summary>Cancels the reviver's hold (release, damage, disconnect). False when none.</summary>
        public bool Cancel(ParticipantId reviverId)
        {
            for (int index = 0; index < _attempts.Count; index++)
            {
                if (_attempts[index].ReviverId == reviverId)
                {
                    _attempts.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves every hold against the current vitality at <paramref name="now"/>. Holds that
        /// are due and finished no later than the teammate's deadline are moved to
        /// <paramref name="due"/> in completion order, at most one per teammate; the caller
        /// completes or rejects each. Holds that can no longer succeed are dropped.
        /// </summary>
        public void Resolve(double now, IReadOnlyList<ParticipantVitality> vitalities, List<RevivalAttempt> due)
        {
            due.Clear();
            _attempts.Sort((left, right) => left.CompletesAt.CompareTo(right.CompletesAt));
            for (int index = _attempts.Count - 1; index >= 0; index--)
            {
                RevivalAttempt attempt = _attempts[index];
                bool reviverConscious = TryFind(vitalities, attempt.ReviverId, out ParticipantVitality reviver) && reviver.IsConscious;
                bool targetUnconscious = TryFind(vitalities, attempt.TargetId, out ParticipantVitality target)
                    && target.State == VitalityState.Unconscious;
                if (!reviverConscious || !targetUnconscious || (now >= target.RecoveryDeadline && attempt.CompletesAt > target.RecoveryDeadline))
                {
                    _attempts.RemoveAt(index);
                    continue;
                }

                // A hold that would finish after the deadline stays visible until the deadline
                // passes, then is dropped above; it never completes.
                if (attempt.CompletesAt <= now && attempt.CompletesAt <= target.RecoveryDeadline)
                {
                    _attempts.RemoveAt(index);
                    due.Insert(0, attempt);
                }
            }

            // At most one completion per teammate: the earliest wins, later ones are dropped.
            for (int index = due.Count - 1; index > 0; index--)
            {
                for (int earlier = 0; earlier < index; earlier++)
                {
                    if (due[earlier].TargetId == due[index].TargetId)
                    {
                        due.RemoveAt(index);
                        break;
                    }
                }
            }
        }

        public void Clear()
        {
            _attempts.Clear();
        }

        private static bool TryFind(IReadOnlyList<ParticipantVitality> vitalities, ParticipantId participantId, out ParticipantVitality vitality)
        {
            for (int index = 0; index < vitalities.Count; index++)
            {
                if (vitalities[index].ParticipantId == participantId)
                {
                    vitality = vitalities[index];
                    return true;
                }
            }

            vitality = default;
            return false;
        }
    }
}
