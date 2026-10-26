namespace HouseEscape.Networking.Hiding
{
    public enum HidingRequestResult
    {
        Hidden,
        Entering,
        Left,
        Cancelled,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        CannotAct,
        Busy,
        OnCooldown,
        Occupied,
        InvalidSpot,
        OutOfRange,
        NotAiming,
        NotHidden
    }
}
