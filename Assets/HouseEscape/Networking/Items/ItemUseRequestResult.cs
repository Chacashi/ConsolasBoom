namespace HouseEscape.Networking.Items
{
    public enum ItemUseRequestResult
    {
        Used,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        CannotAct,
        InvalidItem,
        NoneLeft,
        NotUsable
    }
}
