namespace HouseEscape.Networking.Vitality
{
    public enum RevivalRequestResult
    {
        Started,
        Released,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        CannotAct,
        Busy,
        NoFirstAidKit,
        TargetUnavailable,
        OutOfRange,
        NotAiming,
        Rejected,
        NoRevival
    }
}
