using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Flashlight;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Flashlight
{
    /// <summary>Per-participant flashlight battery HUD line (GDD 8), from replicated state only.</summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour flashlightSource;
        [SerializeField] private TMP_Text batteryText;

        private IFlashlightReadSource _source;
        private bool _hidden;

        public ParticipantId ParticipantId { get; private set; }
        public string BatteryText =>
            batteryText != null && batteryText.gameObject.activeSelf ? batteryText.text : string.Empty;

        public void Initialize(MonoBehaviour explicitFlashlightSource, TMP_Text explicitBatteryText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            flashlightSource = explicitFlashlightSource;
            batteryText = explicitBatteryText;
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

        /// <summary>Hides the line, for example once the participant is eliminated.</summary>
        public void SetHidden(bool hidden)
        {
            if (_hidden == hidden)
            {
                return;
            }

            _hidden = hidden;
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

        private void Bind()
        {
            _source = flashlightSource as IFlashlightReadSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(FlashlightHudView)} requires an explicitly assigned {nameof(IFlashlightReadSource)}.",
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
            if (batteryText == null)
            {
                return;
            }

            ParticipantFlashlight flashlight = default;
            bool known = !_hidden
                && _source != null
                && ParticipantId.IsValid
                && _source.TryGetFlashlight(ParticipantId, out flashlight);
            if (known)
            {
                string value = FlashlightHudText.Format(flashlight.WholeBattery, flashlight.IsOn, _source.IsCharging(ParticipantId));
                if (batteryText.text != value)
                {
                    batteryText.text = value;
                }
            }

            if (batteryText.gameObject.activeSelf != known)
            {
                batteryText.gameObject.SetActive(known);
            }
        }
    }
}
