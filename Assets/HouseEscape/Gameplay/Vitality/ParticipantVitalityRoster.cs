using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// Server-side health and incapacitation state machine for every participant in a match.
    /// Conscious --(health reaches 0)--> Unconscious --(recovery deadline reached)--> Eliminated.
    /// Health never regenerates; unconscious and eliminated participants take no damage.
    /// Records outlive avatars, so the match can tell when every participant is eliminated.
    /// Losing the console connection eliminates immediately from either living state.
    /// Revival (Unconscious --> Conscious with 40 health before the deadline) is driven by
    /// <see cref="RevivalTracker"/>; items restore health and energy of conscious participants.
    /// Energy (GDD 3.3) starts at 100, drains only while conscious in an active match and is
    /// kept across incapacitation.
    /// </summary>
    public sealed class ParticipantVitalityRoster
    {
        private readonly List<ParticipantVitality> _participants =
            new List<ParticipantVitality>(ParticipantRegistry.MaximumParticipants);
        private readonly double _recoveryWindowSeconds;

        public ParticipantVitalityRoster(double recoveryWindowSeconds = VitalityRules.RecoveryWindowSeconds)
        {
            if (double.IsNaN(recoveryWindowSeconds) || double.IsInfinity(recoveryWindowSeconds) || recoveryWindowSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(recoveryWindowSeconds));
            }

            _recoveryWindowSeconds = recoveryWindowSeconds;
        }

        public int Count => _participants.Count;

        public IReadOnlyList<ParticipantVitality> Participants => _participants;

        public int ConsciousCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _participants.Count; index++)
                {
                    if (_participants[index].IsConscious)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool HasUnconsciousParticipants
        {
            get
            {
                for (int index = 0; index < _participants.Count; index++)
                {
                    if (_participants[index].State == VitalityState.Unconscious)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>True once at least one participant exists and every one is permanently eliminated.</summary>
        public bool AreAllEliminated
        {
            get
            {
                if (_participants.Count == 0)
                {
                    return false;
                }

                for (int index = 0; index < _participants.Count; index++)
                {
                    if (_participants[index].IsAlive)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Adds a conscious participant at full health. Returns false for invalid or duplicate IDs.</summary>
        public bool Register(ParticipantId participantId)
        {
            if (!participantId.IsValid || IndexOf(participantId) >= 0)
            {
                return false;
            }

            _participants.Add(new ParticipantVitality(
                participantId,
                VitalityRules.MaximumHealth,
                VitalityState.Conscious,
                0d,
                EnergyRules.StartingEnergy));
            return true;
        }

        public bool TryGet(ParticipantId participantId, out ParticipantVitality vitality)
        {
            int index = IndexOf(participantId);
            vitality = index >= 0 ? _participants[index] : default;
            return index >= 0;
        }

        public VitalityDamageResult ApplyDamage(ParticipantId participantId, int damage, double now)
        {
            int index = IndexOf(participantId);
            if (index < 0 || damage <= 0)
            {
                return VitalityDamageResult.Ignored;
            }

            ParticipantVitality current = _participants[index];
            if (!current.IsConscious)
            {
                return VitalityDamageResult.NotConscious;
            }

            int health = Math.Max(0, current.Health - damage);
            if (health > 0)
            {
                _participants[index] = new ParticipantVitality(participantId, health, VitalityState.Conscious, 0d, current.Energy);
                return VitalityDamageResult.Damaged;
            }

            _participants[index] = new ParticipantVitality(
                participantId,
                0,
                VitalityState.Unconscious,
                now + _recoveryWindowSeconds,
                current.Energy);
            return VitalityDamageResult.KnockedUnconscious;
        }

        /// <summary>
        /// Permanently eliminates every unconscious participant whose deadline is at or before
        /// <paramref name="now"/>, appending each newly eliminated ID to <paramref name="eliminated"/>.
        /// </summary>
        public int EliminateExpired(double now, ICollection<ParticipantId> eliminated)
        {
            int count = 0;
            for (int index = 0; index < _participants.Count; index++)
            {
                ParticipantVitality current = _participants[index];
                if (current.State != VitalityState.Unconscious || now < current.RecoveryDeadline)
                {
                    continue;
                }

                _participants[index] = new ParticipantVitality(current.ParticipantId, 0, VitalityState.Eliminated, 0d, current.Energy);
                eliminated?.Add(current.ParticipantId);
                count++;
            }

            return count;
        }

        /// <summary>True once at least one participant escaped and no conscious participant remains inside.</summary>
        public bool IsEscapeComplete
        {
            get
            {
                bool anyEscaped = false;
                for (int index = 0; index < _participants.Count; index++)
                {
                    if (_participants[index].IsConscious)
                    {
                        return false;
                    }

                    anyEscaped |= _participants[index].State == VitalityState.Escaped;
                }

                return anyEscaped;
            }
        }

        /// <summary>
        /// A conscious participant leaves the house through the front door (GDD 2). Health and
        /// energy are kept for the record. Returns false unless the participant is conscious.
        /// </summary>
        public bool Escape(ParticipantId participantId)
        {
            int index = IndexOf(participantId);
            if (index < 0 || !_participants[index].IsConscious)
            {
                return false;
            }

            ParticipantVitality current = _participants[index];
            _participants[index] = new ParticipantVitality(participantId, current.Health, VitalityState.Escaped, 0d, current.Energy);
            return true;
        }

        /// <summary>
        /// Permanently eliminates a conscious or unconscious participant immediately, for example
        /// when its console's network connection is lost (GDD 7). Returns false when unknown,
        /// already eliminated or escaped.
        /// </summary>
        public bool Eliminate(ParticipantId participantId)
        {
            int index = IndexOf(participantId);
            if (index < 0 || !_participants[index].IsInHouse)
            {
                return false;
            }

            ParticipantVitality eliminated = _participants[index];
            _participants[index] = new ParticipantVitality(participantId, 0, VitalityState.Eliminated, 0d, eliminated.Energy);
            return true;
        }

        /// <summary>
        /// Drains energy for <paramref name="deltaSeconds"/> using <see cref="EnergyRules.GetDrainPerSecond"/>,
        /// clamping at zero. Returns true when the whole (replicated) percentage changed.
        /// </summary>
        public bool DrainEnergy(ParticipantId participantId, double deltaSeconds, bool isRunning, bool isMatchActive)
        {
            int index = IndexOf(participantId);
            if (index < 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0d)
            {
                return false;
            }

            ParticipantVitality current = _participants[index];
            double drain = EnergyRules.GetDrainPerSecond(current.State, current.Health, isRunning, isMatchActive) * deltaSeconds;
            if (drain <= 0d || current.Energy <= 0f)
            {
                return false;
            }

            return ReplaceEnergy(index, (float)(current.Energy - drain));
        }

        /// <summary>
        /// Adds energy to a conscious participant (for example from food), capped at 100.
        /// Returns false for unknown or not-conscious participants and non-positive amounts.
        /// </summary>
        public bool RestoreEnergy(ParticipantId participantId, float amount)
        {
            int index = IndexOf(participantId);
            if (index < 0 || float.IsNaN(amount) || amount <= 0f || !_participants[index].IsConscious)
            {
                return false;
            }

            ReplaceEnergy(index, _participants[index].Energy + amount);
            return true;
        }

        /// <summary>
        /// Returns an unconscious participant to consciousness with <see cref="VitalityRules.RevivedHealth"/>
        /// if its recovery deadline has not passed at <paramref name="revivedAt"/>. Energy is kept.
        /// </summary>
        public bool Revive(ParticipantId participantId, double revivedAt)
        {
            int index = IndexOf(participantId);
            if (index < 0)
            {
                return false;
            }

            ParticipantVitality current = _participants[index];
            if (current.State != VitalityState.Unconscious || revivedAt > current.RecoveryDeadline)
            {
                return false;
            }

            _participants[index] = new ParticipantVitality(
                participantId,
                VitalityRules.RevivedHealth,
                VitalityState.Conscious,
                0d,
                current.Energy);
            return true;
        }

        /// <summary>
        /// Adds health to a conscious participant (for example from food or a first-aid kit),
        /// capped at 100. Returns false for unknown or not-conscious participants and non-positive
        /// amounts.
        /// </summary>
        public bool RestoreHealth(ParticipantId participantId, int amount)
        {
            int index = IndexOf(participantId);
            if (index < 0 || amount <= 0 || !_participants[index].IsConscious)
            {
                return false;
            }

            ParticipantVitality current = _participants[index];
            int health = Math.Min(VitalityRules.MaximumHealth, current.Health + amount);
            _participants[index] = new ParticipantVitality(participantId, health, VitalityState.Conscious, 0d, current.Energy);
            return true;
        }

        /// <summary>Authoritative override of a living participant's energy (clamped to 0-100).</summary>
        public bool SetEnergy(ParticipantId participantId, float energy)
        {
            int index = IndexOf(participantId);
            if (index < 0 || float.IsNaN(energy) || !_participants[index].IsInHouse)
            {
                return false;
            }

            ReplaceEnergy(index, energy);
            return true;
        }

        public void Clear()
        {
            _participants.Clear();
        }

        /// <summary>Returns true when the whole percentage changed.</summary>
        private bool ReplaceEnergy(int index, float energy)
        {
            ParticipantVitality current = _participants[index];
            float clamped = EnergyRules.Clamp(energy);
            _participants[index] = new ParticipantVitality(
                current.ParticipantId,
                current.Health,
                current.State,
                current.RecoveryDeadline,
                clamped);
            return EnergyRules.ToWholeEnergy(clamped) != current.WholeEnergy;
        }

        private int IndexOf(ParticipantId participantId)
        {
            for (int index = 0; index < _participants.Count; index++)
            {
                if (_participants[index].ParticipantId == participantId)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
