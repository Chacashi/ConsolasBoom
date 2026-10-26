using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Participants
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantRegistry : NetworkBehaviour
    {
        private NetworkList<NetworkParticipantRecord> _participants;

        private ParticipantRegistry _serverRegistry;
        private bool _registrationOpen;

        public int Count => _participants?.Count ?? 0;

        public event Action ParticipantsChanged;

        private void Awake()
        {
            _participants = new NetworkList<NetworkParticipantRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            _participants.OnListChanged += HandleListChanged;
            if (IsServer)
            {
                _serverRegistry = new ParticipantRegistry();
                _registrationOpen = true;
                _participants.Clear();
            }

            ParticipantsChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _participants.OnListChanged -= HandleListChanged;
            _serverRegistry = null;
            _registrationOpen = false;
            ParticipantsChanged?.Invoke();
        }

        public override void OnDestroy()
        {
            if (_participants != null)
            {
                base.OnDestroy();
            }
        }

        public void RequestLocalParticipantRegistration(byte localSlot)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("The local-only milestone registers participants on the host.");
            }

            TryRegisterParticipantOnServer(NetworkManager.LocalClientId, localSlot, out _);
        }

        public void CloseRegistrationOnServer()
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only a spawned server registry can close registration.");
            }

            _registrationOpen = false;
        }

        public bool TryGetParticipant(ulong ownerClientId, int localSlot, out ParticipantRecord participant)
        {
            for (int index = 0; index < _participants.Count; index++)
            {
                NetworkParticipantRecord record = _participants[index];
                if (record.OwnerClientId == ownerClientId && record.LocalSlot == localSlot)
                {
                    participant = record.ToCoreRecord();
                    return true;
                }
            }

            participant = default;
            return false;
        }

        public bool TryGetParticipant(ParticipantId participantId, out ParticipantRecord participant)
        {
            for (int index = 0; index < _participants.Count; index++)
            {
                NetworkParticipantRecord record = _participants[index];
                if (record.ParticipantId == participantId.Value)
                {
                    participant = record.ToCoreRecord();
                    return true;
                }
            }

            participant = default;
            return false;
        }

        public bool IsParticipantOwnedBy(ParticipantId participantId, ulong ownerClientId)
        {
            return TryGetParticipant(participantId, out ParticipantRecord participant)
                && participant.OwnerClientId == ownerClientId;
        }

        public IReadOnlyList<ParticipantRecord> GetSnapshot()
        {
            var snapshot = new ParticipantRecord[_participants.Count];
            for (int index = 0; index < _participants.Count; index++)
            {
                snapshot[index] = _participants[index].ToCoreRecord();
            }

            return snapshot;
        }

        public ParticipantRegistrationResult TryRegisterParticipantOnServer(
            ulong ownerClientId,
            int localSlot,
            out ParticipantRecord participant)
        {
            participant = default;
            if (!IsSpawned || !IsServer || _serverRegistry == null)
            {
                throw new InvalidOperationException("Only the spawned server registry can register participants.");
            }

            if (!NetworkManager.ConnectedClients.ContainsKey(ownerClientId))
            {
                return ParticipantRegistrationResult.OwnerConnectionNotConnected;
            }

            if (!_registrationOpen)
            {
                return ParticipantRegistrationResult.RegistrationClosed;
            }

            ParticipantRegistrationResult result = _serverRegistry.TryRegister(ownerClientId, localSlot, out participant);
            if (result == ParticipantRegistrationResult.Success)
            {
                _participants.Add(new NetworkParticipantRecord(participant));
            }

            return result;
        }

        private void HandleListChanged(NetworkListEvent<NetworkParticipantRecord> changeEvent)
        {
            ParticipantsChanged?.Invoke();
        }
    }
}
