using System;
using HouseEscape.Core.Participants;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Scoring
{
    /// <summary>
    /// Server-authoritative per-participant score (GDD 6 "Score"), replicated to every peer.
    /// Gameplay adapters award points through <see cref="AddScoreOnServer"/> once per scoring
    /// event; nothing else writes it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantScoreboard : NetworkBehaviour
    {
        private NetworkList<NetworkParticipantScore> _scores;

        public event Action Changed;

        private void Awake()
        {
            _scores = new NetworkList<NetworkParticipantScore>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            _scores.OnListChanged += HandleScoresChanged;
            if (IsServer)
            {
                _scores.Clear();
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _scores.OnListChanged -= HandleScoresChanged;
            Changed?.Invoke();
        }

        public int GetScore(ParticipantId participantId)
        {
            if (_scores != null && participantId.IsValid)
            {
                for (int index = 0; index < _scores.Count; index++)
                {
                    if (_scores[index].ParticipantId == participantId.Value)
                    {
                        return _scores[index].Score;
                    }
                }
            }

            return 0;
        }

        public void AddScoreOnServer(ParticipantId participantId, int award)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server scoreboard can award points.");
            }

            if (award <= 0 || !participantId.IsValid)
            {
                return;
            }

            for (int index = 0; index < _scores.Count; index++)
            {
                NetworkParticipantScore score = _scores[index];
                if (score.ParticipantId == participantId.Value)
                {
                    _scores[index] = new NetworkParticipantScore(score.ParticipantId, score.Score + award);
                    return;
                }
            }

            _scores.Add(new NetworkParticipantScore(participantId.Value, award));
        }

        private void HandleScoresChanged(NetworkListEvent<NetworkParticipantScore> changeEvent)
        {
            Changed?.Invoke();
        }
    }
}
