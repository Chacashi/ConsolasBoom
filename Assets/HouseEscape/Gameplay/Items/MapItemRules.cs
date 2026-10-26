namespace HouseEscape.Gameplay.Items
{
    /// <summary>Approved map item values (GDD 3.4 spawn, pickup and score rules).</summary>
    public static class MapItemRules
    {
        /// <summary>Points for finding a map item. Starting items award nothing.</summary>
        public const int PickupScoreAward = 25;

        /// <summary>
        /// Placeholder tuning: radius of the aim volume around a map item's aim point that the
        /// participant's view ray must pass through.
        /// </summary>
        public const float AimRadiusMetres = 0.45f;

        /// <summary>Technical server tolerance for view replication skew, as for generators.</summary>
        public const float ServerAimRadiusMetres = AimRadiusMetres + 0.25f;

        public static int GetMinimumQuantity(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return 8;
                case ItemType.Sandwich:
                    return 5;
                case ItemType.EnergyDrink:
                    return 3;
                case ItemType.FirstAidKit:
                    return 2;
                case ItemType.FlashlightBattery:
                    return 4;
                default:
                    return 0;
            }
        }

        public static int GetMaximumQuantity(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return 10;
                case ItemType.Sandwich:
                    return 6;
                case ItemType.EnergyDrink:
                    return 4;
                case ItemType.FirstAidKit:
                    return 3;
                case ItemType.FlashlightBattery:
                    return 5;
                default:
                    return 0;
            }
        }

        /// <summary>The largest number of map items a match can spawn.</summary>
        public static int MaximumTotalQuantity
        {
            get
            {
                int total = 0;
                for (ItemType type = ItemRules.FirstType; type <= ItemRules.LastType; type++)
                {
                    total += GetMaximumQuantity(type);
                }

                return total;
            }
        }

        /// <summary>"Stronger healing items appear near generators to reinforce risk and reward."</summary>
        public static bool IsStrongHealing(ItemType type)
        {
            return type == ItemType.FirstAidKit || type == ItemType.Sandwich;
        }
    }
}
