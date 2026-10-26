namespace HouseEscape.Gameplay.Vitality
{
    public enum RevivalBeginResult
    {
        Started,
        InvalidParticipant,
        CannotReviveSelf,
        ReviverNotConscious,
        TargetNotUnconscious,
        WindowExpired,
        AlreadyReviving
    }
}
