namespace HouseEscape.Networking.Flashlight
{
    public enum FlashlightRequestResult
    {
        Changed,
        Unchanged,
        Released,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        CannotAct,
        EmptyBattery,
        Busy,
        InvalidStation,
        OutOfRange,
        NotAiming,
        NotCharging
    }
}
