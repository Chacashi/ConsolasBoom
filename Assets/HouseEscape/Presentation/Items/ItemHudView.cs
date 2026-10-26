using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Items;
using TMPro;
using UnityEngine;

namespace HouseEscape.Presentation.Items
{
    /// <summary>
    /// Per-participant HUD line for the locally selected item type and its replicated count
    /// (GDD 3.4). Selection is local presentation state owned by the participant's input driver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemHudView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inventorySource;
        [SerializeField] private TMP_Text selectionText;

        private IItemInventoryReadSource _source;
        private ItemType _selected = ItemRules.FirstType;
        private bool _hidden;

        public ParticipantId ParticipantId { get; private set; }
        public ItemType SelectedItem => _selected;
        public string SelectionText =>
            selectionText != null && selectionText.gameObject.activeSelf ? selectionText.text : string.Empty;

        public void Initialize(MonoBehaviour explicitInventorySource, TMP_Text explicitSelectionText)
        {
            if (isActiveAndEnabled)
            {
                Unbind();
            }

            inventorySource = explicitInventorySource;
            selectionText = explicitSelectionText;
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

        public void SetSelectedItem(ItemType type)
        {
            if (_selected == type)
            {
                return;
            }

            _selected = type;
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
            _source = inventorySource as IItemInventoryReadSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(ItemHudView)} requires an explicitly assigned {nameof(IItemInventoryReadSource)}.",
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
            if (selectionText == null)
            {
                return;
            }

            ParticipantItems items = default;
            bool known = !_hidden
                && _source != null
                && ParticipantId.IsValid
                && _source.TryGetItems(ParticipantId, out items);
            if (known)
            {
                string value = ItemHudText.FormatSelection(_selected, items.GetCount(_selected));
                if (selectionText.text != value)
                {
                    selectionText.text = value;
                }
            }

            if (selectionText.gameObject.activeSelf != known)
            {
                selectionText.gameObject.SetActive(known);
            }
        }
    }
}
