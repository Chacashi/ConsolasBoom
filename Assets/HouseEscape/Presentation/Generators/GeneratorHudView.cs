using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Generators;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Generators
{
    /// <summary>
    /// Per-participant generator HUD: global progress and this participant's attempt progress.
    /// Attempt progress is derived locally from the replicated start time and synchronized time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratorHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour raceSource;
        [SerializeField] private TMP_Text generatorCountText;
        [SerializeField] private TMP_Text attemptProgressText;

        private IGeneratorRaceReadSource _source;

        public ParticipantId ParticipantId { get; private set; }
        public string GeneratorCountText => generatorCountText != null ? generatorCountText.text : string.Empty;
        public string AttemptProgressText => attemptProgressText != null && attemptProgressText.gameObject.activeSelf
            ? attemptProgressText.text
            : string.Empty;

        public void Initialize(
            MonoBehaviour explicitRaceSource,
            TMP_Text explicitGeneratorCountText,
            TMP_Text explicitAttemptProgressText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            raceSource = explicitRaceSource;
            generatorCountText = explicitGeneratorCountText;
            attemptProgressText = explicitAttemptProgressText;
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
            if (_source != null && attemptProgressText != null && attemptProgressText.gameObject.activeSelf)
            {
                Render();
            }
        }

        private void Bind()
        {
            _source = raceSource as IGeneratorRaceReadSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(GeneratorHudView)} requires an explicitly assigned {nameof(IGeneratorRaceReadSource)}.",
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
            int activeCount = _source?.ActiveGeneratorCount ?? 0;
            if (generatorCountText != null)
            {
                generatorCountText.text = GeneratorHudText.FormatProgress(activeCount);
            }

            if (attemptProgressText == null)
            {
                return;
            }

            bool attempting = _source != null
                && ParticipantId.IsValid
                && _source.TryGetAttempt(ParticipantId, out int generatorNumber, out double startedAt)
                && SetAttemptText(generatorNumber, startedAt);
            if (attemptProgressText.gameObject.activeSelf != attempting)
            {
                attemptProgressText.gameObject.SetActive(attempting);
            }
        }

        private bool SetAttemptText(int generatorNumber, double startedAt)
        {
            float progress = GeneratorInteractionRules.GetHoldProgress(startedAt, _source.SynchronizedTime);
            attemptProgressText.text = GeneratorHudText.FormatAttempt(generatorNumber, progress);
            return true;
        }
    }
}
