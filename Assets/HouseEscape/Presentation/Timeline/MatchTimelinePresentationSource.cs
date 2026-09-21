using System;
using HouseEscape.Core.Timeline;
using UnityEngine;

namespace HouseEscape.Presentation.Timeline
{
    [DisallowMultipleComponent]
    public sealed class MatchTimelinePresentationSource : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour timelineSource;

        private IMatchTimelineSource _timelineSource;
        private bool _hasWarnedRevision;
        private uint _lastWarnedRevision;
        private double _lastWarnedMatchStartedAt;

        public MatchTimelineReadModel Current { get; private set; }

        public event Action<MatchTimelineReadModel> Changed;

        public event Action LightTransitionWarningStarted;

        public void Initialize(MonoBehaviour explicitTimelineSource)
        {
            bool sourceChanged = timelineSource != explicitTimelineSource;
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            timelineSource = explicitTimelineSource;
            if (sourceChanged)
            {
                _hasWarnedRevision = false;
            }

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

        private void Update()
        {
            Refresh();
        }

        private void Bind()
        {
            _timelineSource = timelineSource as IMatchTimelineSource;
            if (_timelineSource == null)
            {
                Debug.LogError(
                    $"{nameof(MatchTimelinePresentationSource)} requires an explicitly assigned {nameof(IMatchTimelineSource)}.",
                    this);
                return;
            }

            _timelineSource.SnapshotChanged += HandleSnapshotChanged;
            Refresh();
        }

        private void Unbind()
        {
            if (_timelineSource != null)
            {
                _timelineSource.SnapshotChanged -= HandleSnapshotChanged;
                _timelineSource = null;
            }
        }

        private void HandleSnapshotChanged(MatchTimelineSnapshot snapshot)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_timelineSource == null)
            {
                return;
            }

            MatchTimelineReadModel next = MatchTimelineReadModel.Create(
                _timelineSource.Snapshot,
                _timelineSource.SynchronizedTime);
            bool warningStarted = next.IsLightTransitionWarning
                && (!_hasWarnedRevision
                    || _lastWarnedMatchStartedAt != next.MatchStartedAt
                    || _lastWarnedRevision != next.Revision);

            if (!next.Equals(Current))
            {
                Current = next;
                Changed?.Invoke(Current);
            }

            if (warningStarted)
            {
                _hasWarnedRevision = true;
                _lastWarnedMatchStartedAt = next.MatchStartedAt;
                _lastWarnedRevision = next.Revision;
                LightTransitionWarningStarted?.Invoke();
            }
        }
    }
}
