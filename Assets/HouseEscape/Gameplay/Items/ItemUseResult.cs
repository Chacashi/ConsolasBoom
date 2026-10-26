namespace HouseEscape.Gameplay.Items
{
    public enum ItemUseResult : byte
    {
        Used = 0,
        UnknownParticipant = 1,
        InvalidItem = 2,
        NoneLeft = 3,
        NotUsable = 4
    }
}
