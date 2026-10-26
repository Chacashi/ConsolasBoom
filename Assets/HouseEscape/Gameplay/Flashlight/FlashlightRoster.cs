using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Flashlight
{
    /// <summary>
    /// Server-side flashlights of every participant (GDD 3.2). Every participant starts with a
    /// full battery and the flashlight off. A flashlight cannot be turned on with an empty battery
    /// and turns off when the battery empties. Whether the participant may act is decided by the
    /// caller. Mutators return true when the replicated state (whole battery or on/off) changed.
    /// </summary>
    public sealed class FlashlightRoster
    {
        private readonly List<ParticipantFlashlight> _participants =
            new List<ParticipantFlashlight>(ParticipantRegistry.MaximumParticipants);

        public IReadOnlyList<ParticipantFlashlight> Participants => _participants;

        public bool Register(ParticipantId participantId)
        {
            if (!participantId.IsValid || IndexOf(participantId) >= 0)
            {
                return false;
            }

            _participants.Add(new ParticipantFlashlight(participantId, FlashlightRules.StartingBattery, false));
            return true;
        }

        public bool TryGet(ParticipantId participantId, out ParticipantFlashlight flashlight)
        {
            int index = IndexOf(participantId);
            flashlight = index >= 0 ? _participants[index] : default;
            return index >= 0;
        }

        /// <summary>Turns the flashlight on (only with battery left) or off.</summary>
        public bool SetOn(ParticipantId participantId, bool on)
        {
            int index = IndexOf(participantId);
            if (index < 0)
            {
                return false;
            }

            ParticipantFlashlight current = _participants[index];
            if (current.IsOn == on || (on && current.Battery <= 0f))
            {
                return false;
            }

            _participants[index] = new ParticipantFlashlight(participantId, current.Battery, on);
            return true;
        }

        /// <summary>Drains a lit flashlight for <paramref name="deltaSeconds"/>; it turns off when empty.</summary>
        public bool Drain(ParticipantId participantId, double deltaSeconds)
        {
            int index = IndexOf(participantId);
            if (index < 0 || !_participants[index].IsOn || double.IsNaN(deltaSeconds) || deltaSeconds <= 0d)
            {
                return false;
            }

            ParticipantFlashlight current = _participants[index];
            float battery = FlashlightRules.Clamp((float)(current.Battery - FlashlightRules.DrainPerSecond * deltaSeconds));
            _participants[index] = new ParticipantFlashlight(participantId, battery, battery > 0f);
            return battery <= 0f || FlashlightRules.ToWholeBattery(battery) != current.WholeBattery;
        }

        /// <summary>Recharges at the charging-station rate for <paramref name="deltaSeconds"/>, capped at 100.</summary>
        public bool Recharge(ParticipantId participantId, double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || deltaSeconds <= 0d)
            {
                return false;
            }

            return AddBattery(participantId, (float)(FlashlightRules.RechargePerSecond * deltaSeconds));
        }

        /// <summary>Adds battery (for example from a battery item), capped at 100.</summary>
        public bool AddBattery(ParticipantId participantId, float amount)
        {
            int index = IndexOf(participantId);
            if (index < 0 || float.IsNaN(amount) || amount <= 0f)
            {
                return false;
            }

            ParticipantFlashlight current = _participants[index];
            float battery = FlashlightRules.Clamp(current.Battery + amount);
            _participants[index] = new ParticipantFlashlight(participantId, battery, current.IsOn);
            return FlashlightRules.ToWholeBattery(battery) != current.WholeBattery;
        }

        /// <summary>Authoritative override (clamped), for tests and tooling.</summary>
        public bool SetBattery(ParticipantId participantId, float battery)
        {
            int index = IndexOf(participantId);
            if (index < 0 || float.IsNaN(battery))
            {
                return false;
            }

            ParticipantFlashlight current = _participants[index];
            float clamped = FlashlightRules.Clamp(battery);
            _participants[index] = new ParticipantFlashlight(participantId, clamped, current.IsOn && clamped > 0f);
            return true;
        }

        public void Clear()
        {
            _participants.Clear();
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
