using HouseEscape.Gameplay.Items;

namespace HouseEscape.Presentation.Items
{
    public static class ItemHudText
    {
        public static string GetName(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return "Apple";
                case ItemType.Sandwich:
                    return "Sandwich";
                case ItemType.EnergyDrink:
                    return "Energy drink";
                case ItemType.FirstAidKit:
                    return "First-aid kit";
                case ItemType.FlashlightBattery:
                    return "Flashlight battery";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The selected type and its carried count, with D-pad hints.</summary>
        public static string FormatSelection(ItemType type, int count)
        {
            return ItemRules.IsValid(type) ? $"< {GetName(type)} x{(count < 0 ? 0 : count)} >" : string.Empty;
        }
    }
}
