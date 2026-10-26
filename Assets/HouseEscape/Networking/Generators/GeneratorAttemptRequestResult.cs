namespace HouseEscape.Networking.Generators
{
    public enum GeneratorAttemptRequestResult
    {
        Started,
        Released,
        NotServer,
        InvalidParticipant,
        SenderDoesNotOwnParticipant,
        AvatarUnavailable,
        BeginTooSoon,
        AlreadyAttempting,
        InvalidGenerator,
        MatchNotActive,
        LightsOff,
        GeneratorAlreadyActive,
        OutOfRange,
        NotAiming,
        NoAttempt,
        GeneratorMismatch,
        Rejected,
        ParticipantIncapacitated
    }
}
