using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Vitality
{
    /// <summary>
    /// Per-participant revival line: the hold this participant is performing, or the one being
    /// performed on it. Progress is derived locally from the replicated start time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevivalHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour revivalSource;
        [SerializeField] private TMP_Text revivalText;

        private IRevivalReadSource _source;

        public ParticipantId ParticipantId { get; private set; }
        public string RevivalText =>
            revivalText != null && revivalText.gameObject.activeSelf ? revivalText.text : string.Empty;

        public void Initialize(MonoBehaviour explicitRevivalSource, TMP_Text explicitRevivalText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            revivalSource = explicitRevivalSource;
            revivalText = explicitRevivalText;
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
            // Progress advances between replicated changes only while a hold exists.
            if (revivalText != null && revivalText.gameObject.activeSelf)
            {
                Render();
            }
        }

        private void Bind()
        {
            _source = revivalSource as IRevivalReadSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(RevivalHudView)} requires an explicitly assigned {nameof(IRevivalReadSource)}.",
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
            if (revivalText == null)
            {
                return;
            }

            string value = string.Empty;
            if (_source != null && ParticipantId.IsValid)
            {
                double now = _source.SynchronizedTime;
                if (_source.TryGetRevivalByReviver(ParticipantId, out ParticipantId target, out double startedAt))
                {
                    value = RevivalHudText.FormatReviving(target, RevivalHudText.GetPercent(startedAt, now));
                }
                else if (_source.TryGetRevivalOfTarget(ParticipantId, out ParticipantId reviver, out startedAt))
                {
                    value = RevivalHudText.FormatBeingRevived(reviver, RevivalHudText.GetPercent(startedAt, now));
                }
            }

            bool visible = value.Length > 0;
            if (visible && revivalText.text != value)
            {
                revivalText.text = value;
            }

            if (revivalText.gameObject.activeSelf != visible)
            {
                revivalText.gameObject.SetActive(visible);
            }
        }
    }
}
