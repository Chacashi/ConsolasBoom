namespace HouseEscape.Gameplay.Movement
{
    public readonly struct MovementCommand
    {
        public MovementCommand(uint sequence, MovementInput input)
        {
            Sequence = sequence;
            Input = input;
        }

        public uint Sequence { get; }
        public MovementInput Input { get; }
    }
}
