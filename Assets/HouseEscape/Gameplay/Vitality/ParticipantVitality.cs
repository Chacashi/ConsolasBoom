using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// One participant's authoritative condition. <see cref="RecoveryDeadline"/> is the server
    /// time at which an unconscious participant becomes permanently eliminated; it is zero in
    /// every other state. <see cref="Energy"/> is 0-100 (GDD 3.3); on clients it is the
    /// replicated whole percentage.
    /// </summary>
    public readonly struct ParticipantVitality : IEquatable<ParticipantVitality>
    {
        public ParticipantVitality(
            ParticipantId participantId,
            int health,
            VitalityState state,
            double recoveryDeadline,
            float energy)
        {
            ParticipantId = participantId;
            Health = health;
            State = state;
            RecoveryDeadline = recoveryDeadline;
            Energy = energy;
        }

        public ParticipantId ParticipantId { get; }
        public int Health { get; }
        public VitalityState State { get; }
        public double RecoveryDeadline { get; }
        public float Energy { get; }

        /// <summary>Whole energy percentage used for brackets and the HUD.</summary>
        public int WholeEnergy => EnergyRules.ToWholeEnergy(Energy);

        public bool IsConscious => State == VitalityState.Conscious;

        /// <summary>Not permanently eliminated: conscious, unconscious (still revivable) or escaped.</summary>
        public bool IsAlive => State != VitalityState.Eliminated;

        /// <summary>Still inside the house with an avatar: conscious or unconscious.</summary>
        public bool IsInHouse => State == VitalityState.Conscious || State == VitalityState.Unconscious;

        /// <summary>Whole seconds left before elimination, for presentation; zero unless unconscious.</summary>
        public int GetRecoverySecondsRemaining(double now)
        {
            if (State != VitalityState.Unconscious)
            {
                return 0;
            }

            double remaining = RecoveryDeadline - now;
            return remaining <= 0d ? 0 : (int)Math.Ceiling(remaining);
        }

        public bool Equals(ParticipantVitality other)
        {
            return ParticipantId == other.ParticipantId
                && Health == other.Health
                && State == other.State
                && RecoveryDeadline.Equals(other.RecoveryDeadline)
                && Energy.Equals(other.Energy);
        }

        public override bool Equals(object obj) => obj is ParticipantVitality other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Health;
                hashCode = (hashCode * 397) ^ (int)State;
                hashCode = (hashCode * 397) ^ RecoveryDeadline.GetHashCode();
                return (hashCode * 397) ^ Energy.GetHashCode();
            }
        }
    }
}
