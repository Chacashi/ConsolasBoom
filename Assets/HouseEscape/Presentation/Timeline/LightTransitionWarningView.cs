using UnityEngine;

namespace HouseEscape.Presentation.Timeline
{
    [DisallowMultipleComponent]
    public sealed class LightTransitionWarningView : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private MatchTimelinePresentationSource timeline;
        [SerializeField] private CanvasGroup visual;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip warningClip;

        private AudioClip _generatedWarningClip;

        public void Initialize(
            MatchTimelinePresentationSource explicitTimeline,
            CanvasGroup explicitVisual,
            AudioSource explicitAudioSource)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            timeline = explicitTimeline;
            visual = explicitVisual;
            audioSource = explicitAudioSource;

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void Awake()
        {
            SetVisual(false);
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
            SetVisual(false);
        }

        private void OnDestroy()
        {
            if (_generatedWarningClip != null)
            {
                Destroy(_generatedWarningClip);
            }
        }

        private void Bind()
        {
            if (timeline == null)
            {
                Debug.LogError($"{nameof(LightTransitionWarningView)} requires an explicitly assigned timeline.", this);
                return;
            }

            timeline.Changed += HandleTimelineChanged;
            timeline.LightTransitionWarningStarted += PlayWarning;
            HandleTimelineChanged(timeline.Current);
        }

        private void Unbind()
        {
            if (timeline != null)
            {
                timeline.Changed -= HandleTimelineChanged;
                timeline.LightTransitionWarningStarted -= PlayWarning;
            }
        }

        private void HandleTimelineChanged(MatchTimelineReadModel readModel)
        {
            SetVisual(readModel.IsAvailable && readModel.IsLightTransitionWarning);
        }

        private void SetVisual(bool visible)
        {
            if (visual != null)
            {
                visual.alpha = visible ? 1f : 0f;
                visual.interactable = false;
                visual.blocksRaycasts = false;
            }
        }

        private void PlayWarning()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = warningClip != null ? warningClip : GetGeneratedWarningClip();
            audioSource.PlayOneShot(clip);
        }

        private AudioClip GetGeneratedWarningClip()
        {
            if (_generatedWarningClip != null)
            {
                return _generatedWarningClip;
            }

            int sampleCount = SampleRate / 4;
            var samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float normalizedTime = (float)index / sampleCount;
                float envelope = 1f - normalizedTime;
                samples[index] = Mathf.Sin(2f * Mathf.PI * 880f * index / SampleRate) * envelope * 0.2f;
            }

            _generatedWarningClip = AudioClip.Create(
                "Generated light transition warning",
                sampleCount,
                1,
                SampleRate,
                false);
            _generatedWarningClip.SetData(samples, 0);
            return _generatedWarningClip;
        }
    }
}
