using HouseEscape.Core.Participants;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class ParticipantRegistryTests
    {
        [Test]
        public void RegistersFourParticipantsAcrossConnectionsWithStableDistinctIds()
        {
            var registry = new ParticipantRegistry();

            Assert.That(registry.TryRegister(10, 0, out ParticipantRecord first), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(10, 1, out ParticipantRecord second), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(20, 0, out ParticipantRecord third), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(20, 1, out ParticipantRecord fourth), Is.EqualTo(ParticipantRegistrationResult.Success));

            Assert.That(new[] { first.Id.Value, second.Id.Value, third.Id.Value, fourth.Id.Value }, Is.EqualTo(new ulong[] { 1, 2, 3, 4 }));
            Assert.That(registry.Count, Is.EqualTo(ParticipantRegistry.MaximumParticipants));
        }

        [Test]
        public void RejectsDuplicateSlotInvalidSlotAndFifthParticipant()
        {
            var registry = new ParticipantRegistry();
            Assert.That(registry.TryRegister(7, 0, out ParticipantRecord original), Is.EqualTo(ParticipantRegistrationResult.Success));

            Assert.That(registry.TryRegister(7, 0, out ParticipantRecord duplicate), Is.EqualTo(ParticipantRegistrationResult.LocalSlotAlreadyRegistered));
            Assert.That(duplicate, Is.EqualTo(original));
            Assert.That(registry.TryRegister(7, 4, out _), Is.EqualTo(ParticipantRegistrationResult.InvalidLocalSlot));

            Assert.That(registry.TryRegister(7, 1, out _), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(7, 2, out _), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(7, 3, out _), Is.EqualTo(ParticipantRegistrationResult.Success));
            Assert.That(registry.TryRegister(8, 0, out _), Is.EqualTo(ParticipantRegistrationResult.MatchIsFull));
        }
    }
}
