using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Match;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using UnityEngine;

namespace HouseEscape.Networking.Scoring
{
    /// <summary>
    /// Server-only: when a lights-on phase ends with the match still running, every participant
    /// who is conscious inside the house earns the survival points (GDD 6 "Score").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightsOnSurvivalScoring : MonoBehaviour
    {
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantScoreboard scoreboard;

        private readonly List<ParticipantVitality> _participants = new List<ParticipantVitality>(ParticipantRegistry.MaximumParticipants);
        private MatchTimelineSnapshot _previous;
        private bool _subscribed;

        public void Initialize(
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantScoreboard explicitScoreboard)
        {
            matchTimeline = explicitMatchTimeline;
            vitality = explicitVitality;
            scoreboard = explicitScoreboard;
            ValidateDependencies();
        }

        private void OnEnable()
        {
            ValidateDependencies();
            _previous = matchTimeline.Snapshot;
            matchTimeline.SnapshotChanged += HandleSnapshotChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                matchTimeline.SnapshotChanged -= HandleSnapshotChanged;
                _subscribed = false;
            }
        }

        private void HandleSnapshotChanged(MatchTimelineSnapshot snapshot)
        {
            MatchTimelineSnapshot previous = _previous;
            _previous = snapshot;
            if (!matchTimeline.IsServer
                || !vitality.IsSpawned
                || !scoreboard.IsSpawned
                || !SurvivalRules.IsLightsOnPhaseSurvived(previous, snapshot))
            {
                return;
            }

            vitality.GetVitalities(_participants);
            for (int index = 0; index < _participants.Count; index++)
            {
                if (_participants[index].IsConscious)
                {
                    scoreboard.AddScoreOnServer(_participants[index].ParticipantId, SurvivalRules.LightsOnPhaseSurvivedAward);
                }
            }
        }

        private void ValidateDependencies()
        {
            if (matchTimeline == null || vitality == null || scoreboard == null)
            {
                throw new InvalidOperationException("Survival scoring requires explicit timeline, vitality and scoreboard references.");
            }
        }
    }
}
