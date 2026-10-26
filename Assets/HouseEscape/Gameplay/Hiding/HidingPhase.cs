namespace HouseEscape.Gameplay.Hiding
{
    public enum HidingPhase : byte
    {
        /// <summary>Not using a hiding place (possibly still on cooldown).</summary>
        None = 0,

        /// <summary>Holding the button to get into a chest.</summary>
        Entering = 1,

        Hidden = 2,

        /// <summary>The second of vulnerability after leaving.</summary>
        Exiting = 3
    }
}
