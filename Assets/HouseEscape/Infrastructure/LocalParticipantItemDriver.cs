using System;
using HouseEscape.Gameplay.Items;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Items;
using HouseEscape.Presentation.Items;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Routes one local participant's paired-gamepad D-pad left/right into the locally selected
    /// item type and the west face button into a use-item intention (GDD 3.4). The server repeats
    /// every check; incapacitated participants do not send intentions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantItemDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkParticipantInventory inventory;
        [SerializeField] private ItemHudView itemHud;

        private ItemType _selected = ItemRules.FirstType;
        private bool _subscribed;

        public LocalParticipantContext Context => context;
        public ItemType SelectedItem => _selected;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkParticipantInventory explicitInventory,
            ItemHudView explicitItemHud)
        {
            Unsubscribe();
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            inventory = explicitInventory != null
                ? explicitInventory
                : throw new ArgumentNullException(nameof(explicitInventory));
            itemHud = explicitItemHud != null
                ? explicitItemHud
                : throw new ArgumentNullException(nameof(explicitItemHud));
            if (avatarDriver.Context != context)
            {
                throw new ArgumentException("The avatar driver must belong to the same participant context.", nameof(explicitAvatarDriver));
            }

            if (Application.isPlaying)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            if (context != null && avatarDriver != null && inventory != null && itemHud != null)
            {
                Subscribe();
            }
        }

        private void Update()
        {
            // Without an avatar (before the match, or once eliminated) there is nothing to use.
            if (itemHud != null && avatarDriver != null)
            {
                itemHud.SetHidden(avatarDriver.BoundAvatar == null);
            }
        }

        private void HandleItemSelectionPerformed(LocalParticipantContext changedContext, int direction)
        {
            if (changedContext != context || avatarDriver.BoundAvatar == null)
            {
                return;
            }

            _selected = ItemRules.Cycle(_selected, direction);
            itemHud.SetSelectedItem(_selected);
        }

        private void HandleUseItemPerformed(LocalParticipantContext changedContext)
        {
            if (changedContext != context
                || !inventory.IsSpawned
                || !context.ParticipantId.IsValid
                || avatarDriver.BoundAvatar == null
                || avatarDriver.IsIncapacitated)
            {
                return;
            }

            inventory.RequestUseItem(context.ParticipantId, _selected);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            itemHud.SetSelectedItem(_selected);
            context.ItemSelectionPerformed += HandleItemSelectionPerformed;
            context.UseItemPerformed += HandleUseItemPerformed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && context != null)
            {
                context.ItemSelectionPerformed -= HandleItemSelectionPerformed;
                context.UseItemPerformed -= HandleUseItemPerformed;
            }

            _subscribed = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
