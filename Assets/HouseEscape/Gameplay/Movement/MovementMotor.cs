using System;

namespace HouseEscape.Gameplay.Movement
{
    public static class MovementMotor
    {
        public const float WalkSpeed = 3f;
        public const float LookRateDegreesPerSecond = 120f;
        public const float MinimumPitchDegrees = -80f;
        public const float MaximumPitchDegrees = 80f;

        public static MovementMotorState Step(MovementMotorState state, MovementInput input, float deltaTime)
        {
            return Step(state, input, deltaTime, WalkSpeed);
        }

        /// <param name="speed">Metres per second at full stick deflection (see EnergyRules.GetMovementSpeed).</param>
        public static MovementMotorState Step(MovementMotorState state, MovementInput input, float deltaTime, float speed)
        {
            if (!IsFiniteNonNegative(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "Movement speed must be finite and non-negative.");
            }

            if (!input.IsFiniteAndInRange() || !IsFiniteNonNegative(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Movement input and delta time must be finite and in range.");
            }

            float yaw = WrapDegrees(state.YawDegrees + input.LookX * LookRateDegreesPerSecond * deltaTime);
            float pitch = Clamp(
                state.PitchDegrees + input.LookY * LookRateDegreesPerSecond * deltaTime,
                MinimumPitchDegrees,
                MaximumPitchDegrees);

            float moveX = input.MoveX;
            float moveY = input.MoveY;
            float magnitudeSquared = moveX * moveX + moveY * moveY;
            if (magnitudeSquared > 1f)
            {
                float reciprocalMagnitude = 1f / (float)Math.Sqrt(magnitudeSquared);
                moveX *= reciprocalMagnitude;
                moveY *= reciprocalMagnitude;
            }

            double yawRadians = yaw * Math.PI / 180d;
            float sine = (float)Math.Sin(yawRadians);
            float cosine = (float)Math.Cos(yawRadians);
            float distance = speed * deltaTime;
            float worldX = (moveX * cosine + moveY * sine) * distance;
            float worldZ = (-moveX * sine + moveY * cosine) * distance;

            return new MovementMotorState(
                state.X + worldX,
                state.Y,
                state.Z + worldZ,
                yaw,
                pitch);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static float WrapDegrees(float value)
        {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
