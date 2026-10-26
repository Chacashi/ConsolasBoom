using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Generators;
using HouseEscape.Gameplay.Match;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Generators;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using UnityEngine;

namespace HouseEscape.Networking.Match
{
    /// <summary>
    /// Server-side front door (GDD 2). Once every generator is active, a conscious participant
    /// within the exit radius escapes: its avatar leaves the house, it spectates and earns the
    /// escape points. The match ends in victory once no conscious participant remains inside and at
    /// least one escaped. Holds no replicated state of its own: escapes are vitality records, the
    /// outcome lives on the timeline and the open door is derived from replicated generators.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrontDoorEscape : MonoBehaviour
    {
        [SerializeField] private Transform exitPoint;
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkGeneratorRace generatorRace;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantScoreboard scoreboard;

        private readonly List<NetworkParticipantAvatar> _escaping = new List<NetworkParticipantAvatar>(4);

        public Vector3 ExitPosition => exitPoint != null ? exitPoint.position : transform.position;

        public void Initialize(
            Transform explicitExitPoint,
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkGeneratorRace explicitGeneratorRace,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantScoreboard explicitScoreboard)
        {
            exitPoint = explicitExitPoint;
            matchTimeline = explicitMatchTimeline;
            generatorRace = explicitGeneratorRace;
            avatarSpawner = explicitAvatarSpawner;
            vitality = explicitVitality;
            scoreboard = explicitScoreboard;
            ValidateDependencies();
        }

        private void Awake()
        {
            ValidateDependencies();
        }

        private void Update()
        {
            if (!vitality.IsSpawned
                || !vitality.IsServer
                || !matchTimeline.IsSpawned
                || matchTimeline.Snapshot.IsComplete
                || !generatorRace.IsSpawned)
            {
                return;
            }

            if (EscapeRules.IsDoorOpen(generatorRace.ActiveGeneratorCount, GeneratorRace.TotalGeneratorCount))
            {
                _escaping.Clear();
                IReadOnlyList<NetworkParticipantAvatar> avatars = avatarSpawner.SpawnedAvatars;
                Vector3 exit = ExitPosition;
                for (int index = 0; index < avatars.Count; index++)
                {
                    NetworkParticipantAvatar avatar = avatars[index];
                    if (avatar == null || !avatar.IsSpawned)
                    {
                        continue;
                    }

                    Vector3 offset = avatar.transform.position - exit;
                    offset.y = 0f;
                    if (offset.sqrMagnitude <= EscapeRules.ExitRadiusMetres * EscapeRules.ExitRadiusMetres)
                    {
                        _escaping.Add(avatar);
                    }
                }

                // Escaping despawns the avatar, so the spawner list is not modified while iterated.
                for (int index = 0; index < _escaping.Count; index++)
                {
                    ParticipantId participantId = _escaping[index].ParticipantId;
                    if (vitality.EscapeOnServer(participantId))
                    {
                        scoreboard.AddScoreOnServer(participantId, EscapeRules.EscapeScoreAward);
                    }
                }
            }

            if (vitality.IsEscapeCompleteOnServer)
            {
                matchTimeline.EndMatchOnServer(MatchOutcome.Victory);
            }
        }

        private void ValidateDependencies()
        {
            if (exitPoint == null
                || matchTimeline == null
                || generatorRace == null
                || avatarSpawner == null
                || vitality == null
                || scoreboard == null)
            {
                throw new InvalidOperationException(
                    "The front door requires an explicit exit point, timeline, generator race, avatar spawner, vitality and scoreboard.");
            }
        }
    }
}
