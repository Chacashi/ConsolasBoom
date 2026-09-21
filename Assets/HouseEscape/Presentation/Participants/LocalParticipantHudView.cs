using System;
using HouseEscape.Core.Participants;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Participants
{
    [DisallowMultipleComponent]
    public sealed class LocalParticipantHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text participantLabel;
        [SerializeField] private TMP_Text reconnectLabel;

        public ParticipantId ParticipantId { get; private set; }
        public byte LocalSlot { get; private set; }
        public bool IsReconnectionVisible => reconnectLabel != null && reconnectLabel.transform.parent.gameObject.activeSelf;

        public void Initialize(TMP_Text explicitParticipantLabel, TMP_Text explicitReconnectLabel)
        {
            participantLabel = explicitParticipantLabel != null
                ? explicitParticipantLabel
                : throw new ArgumentNullException(nameof(explicitParticipantLabel));
            reconnectLabel = explicitReconnectLabel != null
                ? explicitReconnectLabel
                : throw new ArgumentNullException(nameof(explicitReconnectLabel));
            reconnectLabel.transform.parent.gameObject.SetActive(false);
        }

        public void Bind(ParticipantId participantId, byte localSlot)
        {
            if (!participantId.IsValid)
            {
                throw new ArgumentException("The HUD requires a valid participant ID.", nameof(participantId));
            }

            ParticipantId = participantId;
            Prepare(localSlot);
        }

        public void Prepare(byte localSlot)
        {
            if (localSlot >= ParticipantRegistry.MaximumParticipants)
            {
                throw new ArgumentOutOfRangeException(nameof(localSlot));
            }

            LocalSlot = localSlot;
            participantLabel.text = $"P{localSlot + 1}";
            reconnectLabel.transform.parent.gameObject.SetActive(false);
        }

        public void SetReconnectionRequired(bool required)
        {
            reconnectLabel.text = $"P{LocalSlot + 1}: RECONNECT GAMEPAD";
            reconnectLabel.transform.parent.gameObject.SetActive(required);
        }
    }
}
