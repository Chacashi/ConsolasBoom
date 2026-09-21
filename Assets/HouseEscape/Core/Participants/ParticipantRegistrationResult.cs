namespace HouseEscape.Core.Participants
{
    public enum ParticipantRegistrationResult : byte
    {
        Success = 0,
        InvalidLocalSlot = 1,
        LocalSlotAlreadyRegistered = 2,
        MatchIsFull = 3,
        OwnerConnectionNotConnected = 4,
        RegistrationClosed = 5
    }
}
