using System;
using System.Collections.Generic;

namespace HouseEscape.Gameplay.Items
{
    /// <summary>
    /// Plans the map items of one match (GDD 3.4): a random quantity of each type inside its
    /// approved range, each at a distinct random valid position. Strong healing items take
    /// positions near generators first; every other item takes the remaining positions.
    /// Positions are indexed with the near-generator ones first, then the general ones.
    /// </summary>
    public static class MapItemPlacementPlanner
    {
        public static void Plan(
            Random random,
            int nearGeneratorPositionCount,
            int generalPositionCount,
            List<MapItemPlacement> results)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (nearGeneratorPositionCount < 0 || generalPositionCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nearGeneratorPositionCount));
            }

            if (nearGeneratorPositionCount + generalPositionCount < MapItemRules.MaximumTotalQuantity)
            {
                throw new ArgumentException(
                    $"At least {MapItemRules.MaximumTotalQuantity} valid item positions are required.",
                    nameof(generalPositionCount));
            }

            results.Clear();
            List<int> near = Shuffled(random, 0, nearGeneratorPositionCount);
            List<int> general = Shuffled(random, nearGeneratorPositionCount, generalPositionCount);

            var strong = new List<ItemType>();
            var other = new List<ItemType>();
            for (ItemType type = ItemRules.FirstType; type <= ItemRules.LastType; type++)
            {
                int quantity = random.Next(MapItemRules.GetMinimumQuantity(type), MapItemRules.GetMaximumQuantity(type) + 1);
                List<ItemType> target = MapItemRules.IsStrongHealing(type) ? strong : other;
                for (int index = 0; index < quantity; index++)
                {
                    target.Add(type);
                }
            }

            for (int index = 0; index < strong.Count; index++)
            {
                results.Add(new MapItemPlacement(strong[index], Take(near.Count > 0 ? near : general)));
            }

            // Ordinary items use general positions, then any near-generator position left over.
            for (int index = 0; index < other.Count; index++)
            {
                results.Add(new MapItemPlacement(other[index], Take(general.Count > 0 ? general : near)));
            }
        }

        private static List<int> Shuffled(Random random, int first, int count)
        {
            var values = new List<int>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(first + index);
            }

            for (int index = values.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                int value = values[index];
                values[index] = values[swap];
                values[swap] = value;
            }

            return values;
        }

        private static int Take(List<int> positions)
        {
            int last = positions.Count - 1;
            int value = positions[last];
            positions.RemoveAt(last);
            return value;
        }
    }
}
