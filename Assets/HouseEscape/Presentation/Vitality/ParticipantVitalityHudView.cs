using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Vitality
{
    /// <summary>
    /// Per-participant health/energy/condition HUD. The unconscious countdown is derived locally from
    /// the replicated server-time deadline and synchronized time; nothing is replicated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParticipantVitalityHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour vitalitySource;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text outcomeText;

        private IParticipantVitalityReadSource _source;
        private ParticipantId _spectated;
        private int _renderedCountdownSeconds = -1;

        public ParticipantId ParticipantId { get; private set; }
        public string HealthText => ReadVisible(healthText);
        public string EnergyText => ReadVisible(energyText);
        public string StatusText => ReadVisible(statusText);
        public string OutcomeText => ReadVisible(outcomeText);

        public void Initialize(
            MonoBehaviour explicitVitalitySource,
            TMP_Text explicitHealthText,
            TMP_Text explicitEnergyText,
            TMP_Text explicitStatusText,
            TMP_Text explicitOutcomeText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            vitalitySource = explicitVitalitySource;
            healthText = explicitHealthText;
            energyText = explicitEnergyText;
            statusText = explicitStatusText;
            outcomeText = explicitOutcomeText;
            Render();

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        public void BindParticipant(ParticipantId participantId)
        {
            ParticipantId = participantId;
            Render();
        }

        /// <summary>Local presentation: which teammate this participant's view currently follows.</summary>
        public void SetSpectatedParticipant(ParticipantId spectated)
        {
            if (_spectated == spectated)
            {
                return;
            }

            _spectated = spectated;
            Render();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            // Only the whole-second countdown changes between replicated updates.
            if (_source != null
                && _source.TryGetVitality(ParticipantId, out ParticipantVitality vitality)
                && vitality.State == VitalityState.Unconscious
                && vitality.GetRecoverySecondsRemaining(_source.SynchronizedTime) != _renderedCountdownSeconds)
            {
                Render();
            }
        }

        private void Bind()
        {
            _source = vitalitySource as IParticipantVitalityReadSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(ParticipantVitalityHudView)} requires an explicitly assigned {nameof(IParticipantVitalityReadSource)}.",
                    this);
                return;
            }

            _source.Changed += Render;
            Render();
        }

        private void Unbind()
        {
            if (_source != null)
            {
                _source.Changed -= Render;
                _source = null;
            }
        }

        private void Render()
        {
            ParticipantVitality vitality = default;
            bool known = _source != null
                && ParticipantId.IsValid
                && _source.TryGetVitality(ParticipantId, out vitality);

            _renderedCountdownSeconds = known && vitality.State == VitalityState.Unconscious
                ? vitality.GetRecoverySecondsRemaining(_source.SynchronizedTime)
                : -1;
            SetText(healthText, known ? VitalityHudText.FormatHealth(vitality.Health) : string.Empty);
            SetText(energyText, known && vitality.IsInHouse ? VitalityHudText.FormatEnergy(vitality.WholeEnergy) : string.Empty);
            SetText(statusText, known ? VitalityHudText.FormatStatus(vitality, _source.SynchronizedTime, _spectated) : string.Empty);
            SetText(outcomeText, _source != null ? VitalityHudText.FormatOutcome(_source.Outcome) : string.Empty);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            bool visible = !string.IsNullOrEmpty(value);
            if (visible && text.text != value)
            {
                text.text = value;
            }

            if (text.gameObject.activeSelf != visible)
            {
                text.gameObject.SetActive(visible);
            }
        }

        private static string ReadVisible(TMP_Text text)
        {
            return text != null && text.gameObject.activeSelf ? text.text : string.Empty;
        }
    }
}
