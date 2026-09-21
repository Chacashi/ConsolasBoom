using UnityEngine;
using System;
using System.Collections.Generic;
using HouseEscape.Player;

namespace HouseEscape.Items
{
    public enum ItemType
    {
        None,
        Battery,
        HealthPack,
        Key,
        Note,
        Weapon
    }

    [CreateAssetMenu(menuName = "Items/Item Data", fileName = "NewItem")]
    public class ItemData : ScriptableObject
    {
        [Header("Item Info")]
        [SerializeField] private string itemName = "Item";
        [SerializeField] private ItemType itemType = ItemType.None;
        [SerializeField] private Sprite icon;
        [SerializeField] private string description = "";

        [Header("Item Properties")]
        [SerializeField] private float healthRestore = 0f;
        [SerializeField] private float batteryRestore = 0f;
        [SerializeField] private float energyRestore = 0f;
        [SerializeField] private int stackLimit = 1;

        public string ItemName => itemName;
        public ItemType Type => itemType;
        public Sprite Icon => icon;
        public string Description => description;
        public float HealthRestore => healthRestore;
        public float BatteryRestore => batteryRestore;
        public float EnergyRestore => energyRestore;
        public int StackLimit => stackLimit;
    }

    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int count;

        public InventorySlot() { }
        public InventorySlot(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    public class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int maxSlots = 8;
        [SerializeField] private int[] initialItems;

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Flashlight flashlight;
        [SerializeField] private PlayerEnergy playerEnergy;

        private InventorySlot[] _slots;
        private int _lastUsedSlot = -1;

        public event Action<InventorySlot, int> OnItemAdded;
        public event Action<InventorySlot> OnItemUsed;
        public event Action OnInventoryChanged;

        public int SlotCount => _slots.Length;
        public InventorySlot[] Slots => _slots;
        public InventorySlot SelectedSlot => _lastUsedSlot >= 0 && _lastUsedSlot < _slots.Length ? _slots[_lastUsedSlot] : null;

        private void Awake()
        {
            _slots = new InventorySlot[maxSlots];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new InventorySlot();
        }

        private void Start()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            if (flashlight == null)
                flashlight = GetComponent<Flashlight>();
            if (playerEnergy == null)
                playerEnergy = GetComponent<PlayerEnergy>();
        }

        public bool AddItem(ItemData item, int count = 1)
        {
            if (item == null) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].item == null)
                {
                    _slots[i] = new InventorySlot(item, count);
                    OnItemAdded?.Invoke(_slots[i], i);
                    OnInventoryChanged?.Invoke();
                    Debug.Log("[Inventory] Added " + count + "x " + item.ItemName + " to slot " + i);
                    return true;
                }
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].item == item && _slots[i].count + count <= item.StackLimit)
                {
                    _slots[i].count += count;
                    OnItemAdded?.Invoke(_slots[i], i);
                    OnInventoryChanged?.Invoke();
                    Debug.Log("[Inventory] Stacked " + count + "x " + item.ItemName + " to slot " + i);
                    return true;
                }
            }

            Debug.LogWarning("[Inventory] No space for " + item.ItemName);
            return false;
        }

        public bool RemoveItem(int slotIndex, int count = 1)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            if (_slots[slotIndex].item == null) return false;

            if (_slots[slotIndex].count <= count)
            {
                _slots[slotIndex] = new InventorySlot();
                OnInventoryChanged?.Invoke();
            }
            else
            {
                _slots[slotIndex].count -= count;
                OnInventoryChanged?.Invoke();
            }

            Debug.Log("[Inventory] Removed " + count + " from slot " + slotIndex);
            return true;
        }

        public void UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return;
            if (_slots[slotIndex].item == null) return;

            ItemData item = _slots[slotIndex].item;
            _lastUsedSlot = slotIndex;

            switch (item.Type)
            {
                case ItemType.HealthPack:
                    if (playerHealth != null)
                        playerHealth.Heal(item.HealthRestore);
                    break;

                case ItemType.Battery:
                    if (flashlight != null)
                        flashlight.AddBattery(item.BatteryRestore);
                    break;

                case ItemType.Weapon:
                    Debug.Log("[Inventory] Using weapon: " + item.ItemName);
                    break;

                default:
                    Debug.Log("[Inventory] Used " + item.ItemName);
                    break;
            }

            RemoveItem(slotIndex, 1);
            OnItemUsed?.Invoke(_slots[slotIndex]);
            OnInventoryChanged?.Invoke();
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _slots.Length)
            {
                _lastUsedSlot = slotIndex;
            }
        }

        public void NextSlot()
        {
            for (int i = 1; i <= _slots.Length; i++)
            {
                int next = (_lastUsedSlot + i) % _slots.Length;
                if (_slots[next].item != null)
                {
                    _lastUsedSlot = next;
                    return;
                }
            }
        }

        public void PreviousSlot()
        {
            for (int i = 1; i <= _slots.Length; i++)
            {
                int prev = ((_lastUsedSlot - i) + _slots.Length) % _slots.Length;
                if (_slots[prev].item != null)
                {
                    _lastUsedSlot = prev;
                    return;
                }
            }
        }

        public int GetItemCount(ItemType type)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].item != null && _slots[i].item.Type == type)
                    count += _slots[i].count;
            }
            return count;
        }

        public bool HasItem(ItemType type, int count = 1)
        {
            return GetItemCount(type) >= count;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new InventorySlot();
            _lastUsedSlot = -1;
            OnInventoryChanged?.Invoke();
            Debug.Log("[Inventory] Cleared");
        }
    }
}
