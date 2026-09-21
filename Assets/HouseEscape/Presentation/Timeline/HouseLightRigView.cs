using UnityEngine;

namespace HouseEscape.Presentation.Timeline
{
    [DisallowMultipleComponent]
    public sealed class HouseLightRigView : MonoBehaviour
    {
        [SerializeField] private MatchTimelinePresentationSource timeline;
        [SerializeField] private Light[] houseLights = new Light[0];

        public void Initialize(
            MatchTimelinePresentationSource explicitTimeline,
            Light[] explicitHouseLights)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            timeline = explicitTimeline;
            houseLights = explicitHouseLights != null ? (Light[])explicitHouseLights.Clone() : new Light[0];
            Apply(false);

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void Awake()
        {
            // The canonical initial state is darkness. Apply it before NGO spawning so the
            // scene cannot render an incorrect lit frame while waiting for its first snapshot.
            Apply(false);
        }

        public void Apply(bool lightsAreOn)
        {
            for (int index = 0; index < houseLights.Length; index++)
            {
                if (houseLights[index] != null)
                {
                    houseLights[index].enabled = lightsAreOn;
                }
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
                Debug.LogError(
                    $"{nameof(HouseLightRigView)} requires an explicitly assigned timeline source.",
                    this);
                return;
            }

            timeline.Changed += HandleTimelineChanged;
            HandleTimelineChanged(timeline.Current);
        }

        private void Unbind()
        {
            if (timeline != null)
            {
                timeline.Changed -= HandleTimelineChanged;
            }
        }

        private void HandleTimelineChanged(MatchTimelineReadModel readModel)
        {
            Apply(readModel.IsAvailable && readModel.LightsAreOn);
        }
    }
}
