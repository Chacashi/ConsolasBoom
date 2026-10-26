using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Generators;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class GeneratorRaceTests
    {
        private static readonly ParticipantId FirstParticipant = new ParticipantId(1);
        private static readonly ParticipantId SecondParticipant = new ParticipantId(2);

        [Test]
        public void BeginsConcurrentAttemptsWithMonotonicServerIdsAndStartTimestamps()
        {
            var race = new GeneratorRace();

            BeginGeneratorAttemptResult firstResult = race.BeginAttempt(
                1, FirstParticipant, 25d, true, true, out GeneratorAttempt first);
            BeginGeneratorAttemptResult secondResult = race.BeginAttempt(
                1, SecondParticipant, 26d, true, true, out GeneratorAttempt second);

            Assert.That(firstResult, Is.EqualTo(BeginGeneratorAttemptResult.Started));
            Assert.That(secondResult, Is.EqualTo(BeginGeneratorAttemptResult.Started));
            Assert.That(first.Id.Value, Is.EqualTo(1ul));
            Assert.That(second.Id.Value, Is.EqualTo(2ul));
            Assert.That(second.Id.Value, Is.GreaterThan(first.Id.Value));
            Assert.That(first.ParticipantId, Is.EqualTo(FirstParticipant));
            Assert.That(first.StartedAt, Is.EqualTo(25d));
            Assert.That(race.InProgressAttemptCount, Is.EqualTo(2));
        }

        [Test]
        public void RejectsBeginUnlessMatchAndLightsPermitAValidInactiveGenerator()
        {
            var race = new GeneratorRace();

            Assert.That(
                race.BeginAttempt(1, FirstParticipant, 0d, false, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.MatchNotActive));
            Assert.That(
                race.BeginAttempt(1, FirstParticipant, 0d, true, false, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.LightsOff));
            Assert.That(
                race.BeginAttempt(0, FirstParticipant, 0d, true, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.InvalidGenerator));
            Assert.That(
                race.BeginAttempt(7, FirstParticipant, 0d, true, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.InvalidGenerator));
            Assert.That(
                race.BeginAttempt(1, ParticipantId.None, 0d, true, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.InvalidParticipant));
            Assert.That(
                race.BeginAttempt(1, FirstParticipant, double.NaN, true, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.InvalidServerTime));
        }

        [Test]
        public void CompletionRequiresTheFullTenSecondsAtServerTime()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 100d, true, true, out GeneratorAttempt attempt);

            Assert.That(
                race.CompleteAttempt(attempt.Id, FirstParticipant, 109.999d, true, true, out int earlyAward),
                Is.EqualTo(CompleteGeneratorAttemptResult.HoldIncomplete));
            Assert.That(earlyAward, Is.Zero);
            Assert.That(race.IsGeneratorActive(1), Is.False);

            Assert.That(
                race.CompleteAttempt(attempt.Id, FirstParticipant, 110d, true, true, out int award),
                Is.EqualTo(CompleteGeneratorAttemptResult.Completed));
            Assert.That(award, Is.EqualTo(GeneratorRace.ActivationScoreAward));
            Assert.That(race.IsGeneratorActive(1), Is.True);
            Assert.That(race.ActiveGeneratorCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelValidatesParticipantAndAReplacementAttemptStartsFromZeroProgress()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 5d, true, true, out GeneratorAttempt original);

            Assert.That(
                race.CancelAttempt(original.Id, SecondParticipant),
                Is.EqualTo(CancelGeneratorAttemptResult.ParticipantMismatch));
            Assert.That(
                race.CancelAttempt(original.Id, FirstParticipant),
                Is.EqualTo(CancelGeneratorAttemptResult.Canceled));
            Assert.That(
                race.CompleteAttempt(original.Id, FirstParticipant, 15d, true, true, out int canceledAward),
                Is.EqualTo(CompleteGeneratorAttemptResult.AttemptNotActive));
            Assert.That(canceledAward, Is.Zero);

            race.BeginAttempt(1, FirstParticipant, 15d, true, true, out GeneratorAttempt replacement);
            Assert.That(replacement.Id.Value, Is.GreaterThan(original.Id.Value));
            Assert.That(
                race.CompleteAttempt(replacement.Id, FirstParticipant, 20d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.HoldIncomplete));
            Assert.That(
                race.CompleteAttempt(replacement.Id, FirstParticipant, 25d, true, true, out int award),
                Is.EqualTo(CompleteGeneratorAttemptResult.Completed));
            Assert.That(award, Is.EqualTo(200));
        }

        [Test]
        public void LightsOffCancelsAllAttemptsAndResetProgressCannotResume()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 0d, true, true, out GeneratorAttempt first);
            race.BeginAttempt(2, SecondParticipant, 1d, true, true, out GeneratorAttempt second);

            Assert.That(race.ApplyMatchAndLightState(true, false), Is.EqualTo(2));
            Assert.That(race.InProgressAttemptCount, Is.Zero);
            Assert.That(
                race.CompleteAttempt(first.Id, FirstParticipant, 20d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.AttemptNotActive));
            Assert.That(
                race.CompleteAttempt(second.Id, SecondParticipant, 20d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.AttemptNotActive));

            race.BeginAttempt(1, FirstParticipant, 20d, true, true, out GeneratorAttempt restarted);
            Assert.That(
                race.CompleteAttempt(restarted.Id, FirstParticipant, 29.999d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.HoldIncomplete));
        }

        [Test]
        public void MatchEndCancelsAllAttemptsAndRejectsCompletion()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 0d, true, true, out GeneratorAttempt attempt);

            Assert.That(race.ApplyMatchAndLightState(false, true), Is.EqualTo(1));
            Assert.That(
                race.CompleteAttempt(attempt.Id, FirstParticipant, 10d, false, true, out int award),
                Is.EqualTo(CompleteGeneratorAttemptResult.MatchNotActive));
            Assert.That(award, Is.Zero);
            Assert.That(race.IsGeneratorActive(1), Is.False);
        }

        [Test]
        public void FirstCompletionWinsAndLaterCompletionsAwardNothing()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 0d, true, true, out GeneratorAttempt first);
            race.BeginAttempt(1, SecondParticipant, 0d, true, true, out GeneratorAttempt second);

            Assert.That(
                race.CompleteAttempt(first.Id, FirstParticipant, 10d, true, true, out int firstAward),
                Is.EqualTo(CompleteGeneratorAttemptResult.Completed));
            Assert.That(
                race.CompleteAttempt(second.Id, SecondParticipant, 10d, true, true, out int secondAward),
                Is.EqualTo(CompleteGeneratorAttemptResult.GeneratorAlreadyActive));
            Assert.That(firstAward + secondAward, Is.EqualTo(GeneratorRace.ActivationScoreAward));
            Assert.That(race.ActiveGeneratorCount, Is.EqualTo(1));
            Assert.That(race.InProgressAttemptCount, Is.Zero);
            Assert.That(
                race.CompleteAttempt(first.Id, FirstParticipant, 11d, true, true, out int replayAward),
                Is.EqualTo(CompleteGeneratorAttemptResult.GeneratorAlreadyActive));
            Assert.That(replayAward, Is.Zero);
        }

        [Test]
        public void EndedAttemptsAreReleasedSoMemoryStaysBounded()
        {
            var race = new GeneratorRace();
            GeneratorAttempt oldest = default;
            for (int index = 0; index < 1000; index++)
            {
                race.BeginAttempt(1 + index % 6, FirstParticipant, index, true, true, out GeneratorAttempt attempt);
                if (index == 0)
                {
                    oldest = attempt;
                }

                Assert.That(race.CancelAttempt(attempt.Id, FirstParticipant), Is.EqualTo(CancelGeneratorAttemptResult.Canceled));
            }

            Assert.That(race.InProgressAttemptCount, Is.Zero);
            Assert.That(race.RetainedAttemptCount, Is.LessThanOrEqualTo(GeneratorRace.EndedAttemptHistoryCapacity));
            Assert.That(
                race.CompleteAttempt(oldest.Id, FirstParticipant, 5000d, true, true, out int award),
                Is.EqualTo(CompleteGeneratorAttemptResult.AttemptNotActive),
                "An ended attempt older than the history still reports not-active, never a win.");
            Assert.That(award, Is.Zero);
            Assert.That(
                race.CancelAttempt(oldest.Id, FirstParticipant),
                Is.EqualTo(CancelGeneratorAttemptResult.AttemptNotActive));
        }

        [Test]
        public void DueHoldCompletesBeforeLightsOffCancelEvenOnALateFrame()
        {
            var race = new GeneratorRace();
            const double lightsOffAt = 50d;
            race.BeginAttempt(1, FirstParticipant, 39d, true, true, out GeneratorAttempt finishedInTime);
            race.BeginAttempt(2, SecondParticipant, 41d, true, true, out GeneratorAttempt tooLate);
            var completions = new System.Collections.Generic.List<GeneratorCompletion>();

            // The server's frame lands at 52 s, after lights-off at 50 s.
            Assert.That(race.CompleteDueAttempts(52d, lightsOffAt, completions), Is.EqualTo(1));
            race.ApplyMatchAndLightState(true, false);

            Assert.That(completions, Has.Count.EqualTo(1));
            Assert.That(completions[0].Attempt.Id, Is.EqualTo(finishedInTime.Id));
            Assert.That(completions[0].CompletedAt, Is.EqualTo(49d));
            Assert.That(completions[0].ScoreAward, Is.EqualTo(GeneratorRace.ActivationScoreAward));
            Assert.That(race.IsGeneratorActive(1), Is.True);
            Assert.That(race.IsGeneratorActive(2), Is.False, "A hold finishing after lights-off is canceled.");
            Assert.That(race.IsAttemptInProgress(tooLate.Id), Is.False);
        }

        [Test]
        public void DueCompletionNeverRunsAheadOfServerTimeAndEarliestFinisherWins()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, SecondParticipant, 2d, true, true, out GeneratorAttempt later);
            race.BeginAttempt(1, FirstParticipant, 1d, true, true, out GeneratorAttempt earlier);
            var completions = new System.Collections.Generic.List<GeneratorCompletion>();

            Assert.That(race.CompleteDueAttempts(10.5d, double.PositiveInfinity, completions), Is.Zero);
            Assert.That(race.CompleteDueAttempts(12d, double.PositiveInfinity, completions), Is.EqualTo(1));
            Assert.That(completions.Single().Attempt.ParticipantId, Is.EqualTo(FirstParticipant));
            Assert.That(race.IsAttemptInProgress(later.Id), Is.False, "The loser is ended by the activation.");
            Assert.That(race.IsAttemptInProgress(earlier.Id), Is.False);
            Assert.That(race.CompleteDueAttempts(20d, double.PositiveInfinity, completions), Is.Zero);
            Assert.That(race.ActiveGeneratorCount, Is.EqualTo(1));
        }

        [Test]
        public void BeginRateLimiterRejectsRequestsInsideTheMinimumIntervalPerParticipant()
        {
            var limiter = new GeneratorBeginRateLimiter();

            Assert.That(limiter.TryAccept(FirstParticipant, 10d), Is.True);
            Assert.That(limiter.TryAccept(FirstParticipant, 10.1d), Is.False);
            Assert.That(limiter.TryAccept(SecondParticipant, 10.1d), Is.True, "Limits are per participant.");
            Assert.That(
                limiter.TryAccept(FirstParticipant, 10d + GeneratorBeginRateLimiter.MinimumBeginIntervalSeconds),
                Is.True);
            Assert.That(limiter.TryAccept(ParticipantId.None, 100d), Is.False);
            Assert.That(limiter.TryAccept(FirstParticipant, double.NaN), Is.False);
        }

        [Test]
        public void TracksExactlySixGeneratorsGlobally()
        {
            var race = new GeneratorRace();

            for (int generatorNumber = 1; generatorNumber <= GeneratorRace.TotalGeneratorCount; generatorNumber++)
            {
                race.BeginAttempt(
                    generatorNumber,
                    FirstParticipant,
                    generatorNumber,
                    true,
                    true,
                    out GeneratorAttempt attempt);
                Assert.That(
                    race.CompleteAttempt(
                        attempt.Id,
                        FirstParticipant,
                        generatorNumber + GeneratorRace.RequiredHoldSeconds,
                        true,
                        true,
                        out int award),
                    Is.EqualTo(CompleteGeneratorAttemptResult.Completed));
                Assert.That(award, Is.EqualTo(GeneratorRace.ActivationScoreAward));
            }

            Assert.That(race.ActiveGeneratorCount, Is.EqualTo(6));
            Assert.That(
                race.BeginAttempt(6, SecondParticipant, 100d, true, true, out _),
                Is.EqualTo(BeginGeneratorAttemptResult.GeneratorAlreadyActive));
        }

        [Test]
        public void CompletionRejectsWrongParticipantAndInvalidServerTime()
        {
            var race = new GeneratorRace();
            race.BeginAttempt(1, FirstParticipant, 10d, true, true, out GeneratorAttempt attempt);

            Assert.That(
                race.CompleteAttempt(attempt.Id, SecondParticipant, 20d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.ParticipantMismatch));
            Assert.That(
                race.CompleteAttempt(attempt.Id, FirstParticipant, 9d, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.InvalidServerTime));
            Assert.That(
                race.CompleteAttempt(attempt.Id, FirstParticipant, double.PositiveInfinity, true, true, out _),
                Is.EqualTo(CompleteGeneratorAttemptResult.InvalidServerTime));
            Assert.That(race.IsGeneratorActive(1), Is.False);
        }
    }
}
