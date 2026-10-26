namespace HouseEscape.Gameplay.Monster
{
    /// <summary>GDD 4.3 AI states. Values are replicated as a byte; do not reorder.</summary>
    public enum MonsterState : byte
    {
        Sleeping = 0,
        Searching = 1,
        Chasing = 2,
        Attacking = 3,
        LosingInterest = 4
    }
}
