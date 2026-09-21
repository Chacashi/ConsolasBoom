using HouseEscape.Core.Timeline;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Timeline
{
    [DisallowMultipleComponent]
    public sealed class MatchTimelineHudView : MonoBehaviour
    {
        [SerializeField] private MatchTimelinePresentationSource timeline;
        [SerializeField] private TMP_Text inGameTimeText;
        [SerializeField] private TMP_Text matchRemainingText;
        [SerializeField] private TMP_Text lightStateText;
        [SerializeField] private TMP_Text phaseRemainingText;

        public void Initialize(
            MatchTimelinePresentationSource explicitTimeline,
            TMP_Text explicitInGameTimeText,
            TMP_Text explicitMatchRemainingText,
            TMP_Text explicitLightStateText,
            TMP_Text explicitPhaseRemainingText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            timeline = explicitTimeline;
            inGameTimeText = explicitInGameTimeText;
            matchRemainingText = explicitMatchRemainingText;
            lightStateText = explicitLightStateText;
            phaseRemainingText = explicitPhaseRemainingText;

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (timeline == null)
            {
                Debug.LogError($"{nameof(MatchTimelineHudView)} requires an explicitly assigned timeline.", this);
                return;
            }

            timeline.Changed += Render;
            Render(timeline.Current);
        }

        private void Unbind()
        {
            if (timeline != null)
            {
                timeline.Changed -= Render;
            }
        }

        private void Render(MatchTimelineReadModel readModel)
        {
            if (!readModel.IsAvailable)
            {
                SetText(inGameTimeText, "12:00 AM");
                SetText(matchRemainingText, "Match: 20:00");
                SetText(lightStateText, "WAITING FOR HOST");
                SetText(phaseRemainingText, "Next light: --:--");
                return;
            }

            SetText(inGameTimeText, readModel.InGameTime.ToString());
            SetText(matchRemainingText, $"Match: {FormatCountdown(readModel.MatchRemainingSeconds)}");
            SetText(lightStateText, readModel.LightsAreOn ? "LIGHTS ON" : "LIGHTS OFF");
            SetText(
                phaseRemainingText,
                readModel.LightPhaseRemainingSeconds > 0d
                    ? $"Next light: {FormatCountdown(readModel.LightPhaseRemainingSeconds)}"
                    : "Next light: --:--");
        }

        private static string FormatCountdown(double seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
