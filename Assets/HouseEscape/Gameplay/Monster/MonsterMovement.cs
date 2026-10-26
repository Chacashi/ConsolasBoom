namespace HouseEscape.Gameplay.Monster
{
    public enum MonsterMovement : byte
    {
        /// <summary>Stay exactly in place (sleeping).</summary>
        Hold = 0,

        /// <summary>Wander between random reachable points chosen by the navigation adapter.</summary>
        Wander = 1,

        /// <summary>Move towards <see cref="MonsterDecision.Destination"/>.</summary>
        Pursue = 2
    }
}
