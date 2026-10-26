using System;
using HouseEscape.Gameplay.Movement;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MovementRulesTests
    {
        [TestCase(float.NaN, 0f, 0f, 0f)]
        [TestCase(float.PositiveInfinity, 0f, 0f, 0f)]
        [TestCase(1.01f, 0f, 0f, 0f)]
        [TestCase(0f, -1.01f, 0f, 0f)]
        [TestCase(0f, 0f, 2f, 0f)]
        [TestCase(0f, 0f, 0f, float.NegativeInfinity)]
        public void RejectsNonFiniteOrOutOfRangeAxes(float moveX, float moveY, float lookX, float lookY)
        {
            var buffer = new MovementCommandBuffer();
            Assert.That(buffer.TryAccept(new MovementCommand(1, new MovementInput(moveX, moveY, lookX, lookY)), 0d), Is.False);
        }

        [Test]
        public void RejectsDuplicateAndStaleSequencesIncludingAcrossWrap()
        {
            var buffer = new MovementCommandBuffer();
            Assert.That(buffer.TryAccept(new MovementCommand(uint.MaxValue, MovementInput.Zero), 0d), Is.True);
            Assert.That(buffer.TryAccept(new MovementCommand(uint.MaxValue, MovementInput.Zero), 0d), Is.False);
            Assert.That(buffer.TryAccept(new MovementCommand(uint.MaxValue - 1, MovementInput.Zero), 0d), Is.False);
            Assert.That(buffer.TryAccept(new MovementCommand(0, MovementInput.Zero), 0d), Is.True);
        }

        [Test]
        public void NormalizesDiagonalMovementToWalkSpeed()
        {
            MovementMotorState state = MovementMotor.Step(default, new MovementInput(1f, 1f, 0f, 0f), 1f);
            Assert.That(Math.Sqrt(state.X * state.X + state.Z * state.Z), Is.EqualTo(3d).Within(0.0001d));
        }

        [Test]
        public void AppliesMovementRelativeToYawAndKeepsOriginalY()
        {
            var initial = new MovementMotorState(2f, 4.5f, 3f, 90f, 0f);
            MovementMotorState state = MovementMotor.Step(initial, new MovementInput(0f, 1f, 0f, 0f), 1f);
            Assert.That(state.X, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(state.Z, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(state.Y, Is.EqualTo(4.5f));
        }

        [Test]
        public void UsesApprovedLookRateStandardVerticalDirectionAndPitchClamp()
        {
            MovementMotorState up = MovementMotor.Step(default, new MovementInput(0f, 0f, 1f, 1f), 1f);
            Assert.That(up.YawDegrees, Is.EqualTo(120f));
            Assert.That(up.PitchDegrees, Is.EqualTo(80f));

            MovementMotorState down = MovementMotor.Step(up, new MovementInput(0f, 0f, 0f, -1f), 2f);
            Assert.That(down.PitchDegrees, Is.EqualTo(-80f));
        }

        [Test]
        public void ReturnsZeroInputAfterStaleTimeout()
        {
            var input = new MovementInput(0.5f, -0.25f, 1f, -1f);
            var buffer = new MovementCommandBuffer();
            buffer.TryAccept(new MovementCommand(1, input), 10d);

            Assert.That(buffer.GetInput(10.25d, 0.25d), Is.EqualTo(input));
            Assert.That(buffer.GetInput(10.2501d, 0.25d), Is.EqualTo(MovementInput.Zero));
        }

        [Test]
        public void ZeroInputDoesNotMoveOrRotate()
        {
            var initial = new MovementMotorState(1f, 2f, 3f, 45f, 10f);
            MovementMotorState state = MovementMotor.Step(initial, MovementInput.Zero, 4f);
            Assert.That(state.X, Is.EqualTo(initial.X));
            Assert.That(state.Y, Is.EqualTo(initial.Y));
            Assert.That(state.Z, Is.EqualTo(initial.Z));
            Assert.That(state.YawDegrees, Is.EqualTo(initial.YawDegrees));
            Assert.That(state.PitchDegrees, Is.EqualTo(initial.PitchDegrees));
        }
    }
}
