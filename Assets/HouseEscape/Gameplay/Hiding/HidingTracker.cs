using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Hiding
{
    /// <summary>
    /// Server-side use of hiding places (GDD 3.6). Each hiding place holds one player. Most are
    /// entered instantly; a chest needs a held entry. A hidden player leaves on request or when the
    /// maximum time runs out, then stays vulnerable for one second and cannot hide again until the
    /// ten-second cooldown ends. Protection (a bed seen being entered does not protect) and whether
    /// the hiding made the monster lose the player are decided when the player becomes hidden.
    /// Range, aim, the participant's condition and the monster queries are supplied by the caller.
    /// </summary>
    public sealed class HidingTracker
    {
        private readonly List<HidingStatus> _statuses = new List<HidingStatus>(ParticipantRegistry.MaximumParticipants);

        public IReadOnlyList<HidingStatus> Statuses => _statuses;

        public bool TryGet(ParticipantId participantId, out HidingStatus status)
        {
            int index = IndexOf(participantId);
            status = index >= 0 ? _statuses[index] : default;
            return index >= 0;
        }

        public bool IsSpotOccupied(int spotIndex)
        {
            for (int index = 0; index < _statuses.Count; index++)
            {
                if (_statuses[index].OccupiesSpot && _statuses[index].SpotIndex == spotIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <param name="seenEntering">The monster is awake and detecting the participant now.</param>
        /// <param name="targetedByMonster">The monster's current target is the participant.</param>
        public HidingBeginResult Begin(
            ParticipantId participantId,
            int spotIndex,
            HidingSpotType type,
            double now,
            bool seenEntering,
            bool targetedByMonster)
        {
            if (!participantId.IsValid || spotIndex < 0)
            {
                return HidingBeginResult.InvalidParticipant;
            }

            int index = IndexOf(participantId);
            double cooldownEndsAt = index >= 0 ? _statuses[index].CooldownEndsAt : double.NegativeInfinity;
            if (index >= 0 && _statuses[index].Phase != HidingPhase.None)
            {
                return HidingBeginResult.Busy;
            }

            if (now < cooldownEndsAt)
            {
                return HidingBeginResult.OnCooldown;
            }

            if (IsSpotOccupied(spotIndex))
            {
                return HidingBeginResult.Occupied;
            }

            double hold = HidingRules.GetEnterHoldSeconds(type);
            HidingStatus next = hold > 0d
                ? new HidingStatus(participantId, HidingPhase.Entering, spotIndex, type, now, now + hold, cooldownEndsAt, false, false)
                : Hidden(participantId, spotIndex, type, now, cooldownEndsAt, seenEntering, targetedByMonster);
            Store(index, next);
            return next.Phase == HidingPhase.Hidden ? HidingBeginResult.Hidden : HidingBeginResult.Entering;
        }

        /// <summary>Releasing the button before a held entry completes cancels it.</summary>
        public bool CancelEntering(ParticipantId participantId)
        {
            int index = IndexOf(participantId);
            if (index < 0 || _statuses[index].Phase != HidingPhase.Entering)
            {
                return false;
            }

            _statuses[index] = Idle(_statuses[index], _statuses[index].CooldownEndsAt);
            return true;
        }

        /// <summary>Leaves the hiding place at <paramref name="now"/>; returns the hidden status that ended.</summary>
        public bool Leave(ParticipantId participantId, double now, out HidingStatus left)
        {
            int index = IndexOf(participantId);
            if (index < 0 || _statuses[index].Phase != HidingPhase.Hidden)
            {
                left = default;
                return false;
            }

            left = _statuses[index];
            _statuses[index] = Exiting(left, now);
            return true;
        }

        /// <summary>
        /// Ends any use without vulnerability or award, for example when the participant is
        /// knocked out, eliminated or escapes. The cooldown is kept.
        /// </summary>
        public bool Remove(ParticipantId participantId)
        {
            int index = IndexOf(participantId);
            if (index < 0 || _statuses[index].Phase == HidingPhase.None)
            {
                return false;
            }

            _statuses[index] = Idle(_statuses[index], _statuses[index].CooldownEndsAt);
            return true;
        }

        /// <summary>
        /// Advances phases that end on their own at <paramref name="now"/>: held entries become
        /// hidden (<paramref name="becameHidden"/>), expired hidden players are forced out at their
        /// deadline (<paramref name="forcedOut"/>, the status that ended) and finished
        /// vulnerabilities end (<paramref name="recovered"/>).
        /// </summary>
        public void Advance(
            double now,
            Func<ParticipantId, bool> isSeenByMonster,
            Func<ParticipantId, bool> isTargetedByMonster,
            List<HidingStatus> becameHidden,
            List<HidingStatus> forcedOut,
            List<ParticipantId> recovered)
        {
            becameHidden.Clear();
            forcedOut.Clear();
            recovered.Clear();
            for (int index = 0; index < _statuses.Count; index++)
            {
                HidingStatus status = _statuses[index];
                if (status.Phase == HidingPhase.Entering && now >= status.PhaseEndsAt)
                {
                    status = Hidden(
                        status.ParticipantId,
                        status.SpotIndex,
                        status.SpotType,
                        status.PhaseEndsAt,
                        status.CooldownEndsAt,
                        isSeenByMonster(status.ParticipantId),
                        isTargetedByMonster(status.ParticipantId));
                    becameHidden.Add(status);
                }

                if (status.Phase == HidingPhase.Hidden && now >= status.PhaseEndsAt)
                {
                    forcedOut.Add(status);
                    status = Exiting(status, status.PhaseEndsAt);
                }

                if (status.Phase == HidingPhase.Exiting && now >= status.PhaseEndsAt)
                {
                    recovered.Add(status.ParticipantId);
                    status = Idle(status, status.CooldownEndsAt);
                }

                _statuses[index] = status;
            }
        }

        public void Clear()
        {
            _statuses.Clear();
        }

        private static HidingStatus Hidden(
            ParticipantId participantId,
            int spotIndex,
            HidingSpotType type,
            double hiddenAt,
            double cooldownEndsAt,
            bool seenEntering,
            bool targetedByMonster)
        {
            bool isProtected = HidingRules.Protects(type, seenEntering);
            return new HidingStatus(
                participantId,
                HidingPhase.Hidden,
                spotIndex,
                type,
                hiddenAt,
                hiddenAt + HidingRules.GetMaximumSeconds(type),
                cooldownEndsAt,
                isProtected,
                isProtected && targetedByMonster);
        }

        private static HidingStatus Exiting(HidingStatus left, double leftAt)
        {
            return new HidingStatus(
                left.ParticipantId,
                HidingPhase.Exiting,
                -1,
                left.SpotType,
                leftAt,
                leftAt + HidingRules.ExitVulnerabilitySeconds,
                leftAt + HidingRules.CooldownSeconds,
                false,
                false);
        }

        private static HidingStatus Idle(HidingStatus status, double cooldownEndsAt)
        {
            return new HidingStatus(status.ParticipantId, HidingPhase.None, -1, status.SpotType, 0d, 0d, cooldownEndsAt, false, false);
        }

        private void Store(int index, HidingStatus status)
        {
            if (index >= 0)
            {
                _statuses[index] = status;
            }
            else
            {
                _statuses.Add(status);
            }
        }

        private int IndexOf(ParticipantId participantId)
        {
            for (int index = 0; index < _statuses.Count; index++)
            {
                if (_statuses[index].ParticipantId == participantId)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
