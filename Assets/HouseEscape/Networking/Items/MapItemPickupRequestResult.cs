namespace HouseEscape.Networking.Items
{
    public enum MapItemPickupRequestResult
    {
        PickedUp,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        CannotAct,
        ItemUnavailable,
        OutOfRange,
        NotAiming
    }
}
