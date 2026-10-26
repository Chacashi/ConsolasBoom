namespace HouseEscape.Gameplay.Items
{
    /// <summary>One planned map item: its type and the index of the valid spawn position it uses.</summary>
    public readonly struct MapItemPlacement
    {
        public MapItemPlacement(ItemType type, int positionIndex)
        {
            Type = type;
            PositionIndex = positionIndex;
        }

        public ItemType Type { get; }
        public int PositionIndex { get; }
    }
}
