using System.Collections.Generic;

namespace HouseEscape.Gameplay.Items
{
    /// <summary>Approved item values (GDD 3.4) and starting supplies by spawn slot (GDD 7).</summary>
    public static class ItemRules
    {
        public const int TypeCount = 5;
        public const ItemType FirstType = ItemType.Apple;
        public const ItemType LastType = ItemType.FlashlightBattery;

        public static bool IsValid(ItemType type) => type >= FirstType && type <= LastType;

        public static int GetHealthGain(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return 15;
                case ItemType.Sandwich:
                    return 25;
                case ItemType.FirstAidKit:
                    return 40;
                default:
                    return 0;
            }
        }

        public static float GetEnergyGain(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return 10f;
                case ItemType.Sandwich:
                    return 20f;
                case ItemType.EnergyDrink:
                    return 40f;
                default:
                    return 0f;
            }
        }

        /// <summary>Flashlight battery percentage an item adds (GDD 3.4), capped at 100 by the flashlight.</summary>
        public static float GetBatteryGain(ItemType type)
        {
            return type == ItemType.FlashlightBattery ? 50f : 0f;
        }

        /// <summary>Whether the item can be consumed; every valid item type has an effect.</summary>
        public static bool IsUsable(ItemType type) => IsValid(type);

        /// <summary>D-pad selection: the next (+1) or previous (-1) type, wrapping.</summary>
        public static ItemType Cycle(ItemType current, int direction)
        {
            int index = IsValid(current) ? (int)current - (int)FirstType : 0;
            int step = direction < 0 ? -1 : direction > 0 ? 1 : 0;
            index = ((index + step) % TypeCount + TypeCount) % TypeCount;
            return (ItemType)(index + (int)FirstType);
        }

        /// <summary>
        /// Adds the consumable starting items for a zero-based spawn slot (Player 1 is slot 0).
        /// Every player also starts with a flashlight, which is equipment, not an inventory item.
        /// </summary>
        public static void GetStartingItems(int spawnSlot, ICollection<ItemType> results)
        {
            switch (spawnSlot)
            {
                case 0:
                    results.Add(ItemType.Apple);
                    results.Add(ItemType.Apple);
                    break;
                case 1:
                    results.Add(ItemType.FirstAidKit);
                    break;
                case 2:
                    results.Add(ItemType.FlashlightBattery);
                    break;
                case 3:
                    results.Add(ItemType.Sandwich);
                    break;
            }
        }
    }
}
