namespace HouseEscape.Gameplay.Movement
{
    public readonly struct MovementMotorState
    {
        public MovementMotorState(float x, float y, float z, float yawDegrees, float pitchDegrees)
        {
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float YawDegrees { get; }
        public float PitchDegrees { get; }
    }
}
