using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Hiding;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Hiding
{
    /// <summary>
    /// Per-participant hiding line: entry progress, time left hidden, vulnerability and cooldown,
    /// derived locally from replicated server phase times.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour hidingSource;
        [SerializeField] private TMP_Text hidingText;

        private IHidingReadSource _source;

        public ParticipantId ParticipantId { get; private set; }
        public string HidingText =>
            hidingText != null && hidingText.gameObject.activeSelf ? hidingText.text : string.Empty;

        public void Initialize(MonoBehaviour explicitHidingSource, TMP_Text explicitHidingText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            hidingSource = explicitHidingSource;
            hidingText = explicitHidingText;
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
            // Countdowns advance between replicated changes only while the line is shown.
            if (hidingText != null && hidingText.gameObject.activeSelf)
            {
                Render();
            }
        }

        private void Bind()
        {
            _source = hidingSource as IHidingReadSource;
            if (_source == null)
            {
                Debug.LogError($"{nameof(HidingHudView)} requires an explicitly assigned {nameof(IHidingReadSource)}.", this);
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
            if (hidingText == null)
            {
                return;
            }

            string value = _source != null
                && ParticipantId.IsValid
                && _source.TryGetHiding(ParticipantId, out HidingStatus status)
                    ? HidingHudText.Format(status, _source.SynchronizedTime)
                    : string.Empty;
            bool visible = value.Length > 0;
            if (visible && hidingText.text != value)
            {
                hidingText.text = value;
            }

            if (hidingText.gameObject.activeSelf != visible)
            {
                hidingText.gameObject.SetActive(visible);
            }
        }
    }
}
