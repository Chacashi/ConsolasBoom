namespace HouseEscape.Gameplay.Movement
{
    public sealed class MovementCommandBuffer
    {
        private bool _hasSequence;
        private uint _lastSequence;
        private double _lastReceivedTime;
        private MovementInput _input;

        public bool TryAccept(MovementCommand command, double receivedTime)
        {
            if (!command.Input.IsFiniteAndInRange()
                || double.IsNaN(receivedTime)
                || double.IsInfinity(receivedTime)
                || (_hasSequence && !IsNewer(command.Sequence, _lastSequence)))
            {
                return false;
            }

            _hasSequence = true;
            _lastSequence = command.Sequence;
            _lastReceivedTime = receivedTime;
            _input = command.Input;
            return true;
        }

        public MovementInput GetInput(double serverTime, double timeoutSeconds)
        {
            if (!_hasSequence
                || timeoutSeconds < 0d
                || double.IsNaN(serverTime)
                || double.IsInfinity(serverTime)
                || serverTime - _lastReceivedTime > timeoutSeconds)
            {
                return MovementInput.Zero;
            }

            return _input;
        }

        public static bool IsNewer(uint candidate, uint previous)
        {
            uint difference = candidate - previous;
            return difference != 0u && difference < 0x80000000u;
        }
    }
}
