using HouseEscape.Gameplay.Generators;
using HouseEscape.Gameplay.Match;
using UnityEngine;

namespace HouseEscape.Presentation.Generators
{
    /// <summary>Shows the front door closed until every generator is active, then open (GDD 2).</summary>
    [DisallowMultipleComponent]
    public sealed class FrontDoorView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour generatorSource;
        [SerializeField] private GameObject closedIndicator;
        [SerializeField] private GameObject openIndicator;

        private IGeneratorRaceReadSource _source;

        public bool IsShownOpen => openIndicator != null && openIndicator.activeSelf;

        public void Initialize(MonoBehaviour explicitGeneratorSource, GameObject explicitClosedIndicator, GameObject explicitOpenIndicator)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            generatorSource = explicitGeneratorSource;
            closedIndicator = explicitClosedIndicator;
            openIndicator = explicitOpenIndicator;
            Render();

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
            _source = generatorSource as IGeneratorRaceReadSource;
            if (_source == null)
            {
                Debug.LogError($"{nameof(FrontDoorView)} requires an explicitly assigned {nameof(IGeneratorRaceReadSource)}.", this);
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
            bool open = _source != null && EscapeRules.IsDoorOpen(_source.ActiveGeneratorCount, GeneratorRace.TotalGeneratorCount);
            if (closedIndicator != null && closedIndicator.activeSelf == open)
            {
                closedIndicator.SetActive(!open);
            }

            if (openIndicator != null && openIndicator.activeSelf != open)
            {
                openIndicator.SetActive(open);
            }
        }
    }
}
