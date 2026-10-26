using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Monster;
using HouseEscape.Gameplay.Movement;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MonsterBrainTests
    {
        private static readonly ParticipantId A = new ParticipantId(1);
        private static readonly ParticipantId B = new ParticipantId(2);
        private static readonly WorldPoint Origin = new WorldPoint(0f, 0f, 0f);

        private MonsterBrain _brain;

        [SetUp]
        public void SetUp()
        {
            _brain = new MonsterBrain();
        }

        [Test]
        public void StartsSleepingAndDarknessHoldsInPlaceWithoutPerception()
        {
            Assert.That(_brain.State, Is.EqualTo(MonsterState.Sleeping));
            MonsterDecision decision = Tick(false, 0d, Seen(A, 1f));

            Assert.That(decision.State, Is.EqualTo(MonsterState.Sleeping));
            Assert.That(decision.Movement, Is.EqualTo(MonsterMovement.Hold));
            Assert.That(decision.TargetId.IsValid, Is.False);
            Assert.That(decision.HasAttack, Is.False, "A sleeping monster never attacks, even in range.");
            Assert.That(decision.Speed, Is.Zero);
        }

        [Test]
        public void WakingWithoutDetectionSearchesByWandering()
        {
            Tick(false, 0d);
            MonsterDecision decision = Tick(true, 1d, Seen(A, 9f));

            Assert.That(decision.State, Is.EqualTo(MonsterState.Searching));
            Assert.That(decision.Movement, Is.EqualTo(MonsterMovement.Wander));
            Assert.That(decision.TargetId.IsValid, Is.False);
        }

        [Test]
        public void DarknessMidChaseImmediatelyEndsHuntingAndDropsTarget()
        {
            Assert.That(Tick(true, 0d, Seen(A, 5f)).State, Is.EqualTo(MonsterState.Chasing));

            MonsterDecision decision = Tick(false, 0.1d, Seen(A, 5f));
            Assert.That(decision.State, Is.EqualTo(MonsterState.Sleeping));
            Assert.That(decision.Movement, Is.EqualTo(MonsterMovement.Hold));
            Assert.That(_brain.TargetId.IsValid, Is.False);

            Assert.That(Tick(true, 0.2d).State, Is.EqualTo(MonsterState.Searching),
                "Waking again does not resume the dropped target without a new detection.");
        }

        [Test]
        public void DetectionUsesAnEightMetreRadiusInAnyDirection()
        {
            Assert.That(Tick(true, 0d, Candidate(A, new WorldPoint(0f, 0f, -8f), true, true)).TargetId, Is.EqualTo(A),
                "Exactly 8 m behind the monster is detected (360 degrees).");

            _brain = new MonsterBrain();
            Assert.That(Tick(true, 0d, Candidate(A, new WorldPoint(-8.01f, 0f, 0f), true, true)).TargetId.IsValid, Is.False);
        }

        [Test]
        public void ObstructedOrUntargetablePlayersAreNeverDetected()
        {
            Assert.That(Tick(true, 0d, Candidate(A, At(2f), true, false)).State, Is.EqualTo(MonsterState.Searching),
                "No line of sight.");
            Assert.That(Tick(true, 0.1d, Candidate(A, At(1f), false, true)).State, Is.EqualTo(MonsterState.Searching),
                "Hidden (untargetable) players are never valid, even in attack range.");
            Assert.That(Tick(true, 0.2d, Candidate(A, At(1f), false, true)).HasAttack, Is.False);
        }

        [Test]
        public void TargetsNearestAndSwitchesWhenAnotherBecomesNearer()
        {
            MonsterDecision decision = Tick(true, 0d, Seen(A, 6f), Seen(B, 4f));
            Assert.That(decision.TargetId, Is.EqualTo(B));
            Assert.That(decision.State, Is.EqualTo(MonsterState.Chasing));
            Assert.That(decision.Movement, Is.EqualTo(MonsterMovement.Pursue));
            Assert.That(decision.Destination, Is.EqualTo(At(4f)));

            Assert.That(Tick(true, 1d, Seen(A, 4f), Seen(B, 4f)).TargetId, Is.EqualTo(B),
                "An equally near player does not steal the target.");
            Assert.That(Tick(true, 2d, Seen(A, 3f), Seen(B, 4f)).TargetId, Is.EqualTo(A));
        }

        [Test]
        public void LosesInterestFiveSecondsAfterLastDetectionHeadingToLastKnownPosition()
        {
            Tick(true, 0d, Seen(A, 5f));
            MonsterDecision lost = Tick(true, 1d, Candidate(A, At(7f), true, false));
            Assert.That(lost.State, Is.EqualTo(MonsterState.LosingInterest));
            Assert.That(lost.TargetId, Is.EqualTo(A));
            Assert.That(lost.Destination, Is.EqualTo(At(5f)), "Moves to the last detected position.");

            Assert.That(Tick(true, 4.99d, Candidate(A, At(7f), true, false)).TargetId, Is.EqualTo(A));
            MonsterDecision dropped = Tick(true, 5d, Candidate(A, At(7f), true, false));
            Assert.That(dropped.State, Is.EqualTo(MonsterState.Searching));
            Assert.That(dropped.TargetId.IsValid, Is.False);
            Assert.That(_brain.IsExcluded(A, 5d), Is.False, "Losing interest does not exclude the player.");
        }

        [Test]
        public void RedetectionWithinGraceKeepsTargetAndResetsLoseInterestTimer()
        {
            Tick(true, 0d, Seen(A, 5f));
            Tick(true, 4d, Candidate(A, At(5f), true, false));
            Tick(true, 4.5d, Seen(A, 5f));
            Assert.That(Tick(true, 9d, Candidate(A, At(5f), true, false)).TargetId, Is.EqualTo(A));
            Assert.That(Tick(true, 9.5d, Candidate(A, At(5f), true, false)).TargetId.IsValid, Is.False);
        }

        [Test]
        public void PursuitExpiresAfterFifteenSecondsAndExcludesThatPlayerForFiveSeconds()
        {
            Tick(true, 0d, Seen(A, 5f));
            Assert.That(Tick(true, 14.99d, Seen(A, 5f)).TargetId, Is.EqualTo(A));

            MonsterDecision expired = Tick(true, 15d, Seen(A, 5f));
            Assert.That(expired.TargetId.IsValid, Is.False);
            Assert.That(expired.State, Is.EqualTo(MonsterState.Searching));
            Assert.That(_brain.IsExcluded(A, 15d), Is.True);

            Assert.That(Tick(true, 19.99d, Seen(A, 2f)).TargetId.IsValid, Is.False, "Still excluded.");
            Assert.That(Tick(true, 20d, Seen(A, 2f)).TargetId, Is.EqualTo(A), "Exclusion ends after five seconds.");
        }

        [Test]
        public void PursuitExpiryAllowsAnotherDetectedPlayerImmediately()
        {
            Tick(true, 0d, Seen(A, 2f), Seen(B, 6f));
            MonsterDecision decision = Tick(true, 15d, Seen(A, 2f), Seen(B, 6f));
            Assert.That(decision.TargetId, Is.EqualTo(B));
            Assert.That(decision.State, Is.EqualTo(MonsterState.Chasing));

            Assert.That(Tick(true, 17d, Seen(A, 1f), Seen(B, 6f)).TargetId, Is.EqualTo(B),
                "The excluded nearer player cannot steal the target during exclusion.");
        }

        [Test]
        public void PursuitTimerRestartsWhenTargetChangesToADifferentPlayer()
        {
            Tick(true, 0d, Seen(A, 4f), Seen(B, 6f));
            Assert.That(Tick(true, 10d, Seen(A, 6f), Seen(B, 4f)).TargetId, Is.EqualTo(B));
            Assert.That(Tick(true, 24.9d, Seen(A, 6f), Seen(B, 4f)).TargetId, Is.EqualTo(B), "B pursued since t=10.");
            Assert.That(Tick(true, 25d, Seen(A, 6f), Seen(B, 4f)).TargetId, Is.EqualTo(A));
        }

        [Test]
        public void AttacksDetectedTargetInRangeEveryTwoSecondsForThirtyDamage()
        {
            MonsterDecision chasing = Tick(true, 0d, Seen(A, MonsterRules.AttackRangeMetres + 0.01f));
            Assert.That(chasing.State, Is.EqualTo(MonsterState.Chasing));
            Assert.That(chasing.HasAttack, Is.False, "Out of attack range.");

            MonsterDecision first = Tick(true, 0.5d, Seen(A, MonsterRules.AttackRangeMetres));
            Assert.That(first.State, Is.EqualTo(MonsterState.Attacking));
            Assert.That(first.HasAttack, Is.True);
            Assert.That(first.Attack.TargetId, Is.EqualTo(A));
            Assert.That(first.Attack.Damage, Is.EqualTo(30));
            Assert.That(first.Attack.PerformedAt, Is.EqualTo(0.5d));

            Assert.That(Tick(true, 1d, Seen(A, 1f)).HasAttack, Is.False);
            Assert.That(Tick(true, 2.49d, Seen(A, 1f)).HasAttack, Is.False);
            Assert.That(Tick(true, 2.5d, Seen(A, 1f)).HasAttack, Is.True);
            Assert.That(Tick(true, 4.5d, Seen(A, 1f)).HasAttack, Is.True);
        }

        [Test]
        public void ChaseSpeedIsOnePointTwoWalkSpeedPlusFifteenPercentWithFourParticipants()
        {
            Assert.That(MonsterRules.GetChaseSpeed(1), Is.EqualTo(MovementMotor.WalkSpeed * 1.2f).Within(1e-5f));
            Assert.That(MonsterRules.GetChaseSpeed(3), Is.EqualTo(3.6f).Within(1e-5f));
            Assert.That(MonsterRules.GetChaseSpeed(4), Is.EqualTo(3.6f * 1.15f).Within(1e-5f));

            Assert.That(Tick(true, 0d, 3, Seen(A, 5f)).Speed, Is.EqualTo(3.6f).Within(1e-5f));
            Assert.That(Tick(true, 0.1d, 4, Seen(A, 5f)).Speed, Is.EqualTo(4.14f).Within(1e-4f));
        }

        [Test]
        public void TargetLeavingTheMatchIsDroppedImmediately()
        {
            Tick(true, 0d, Seen(A, 5f));
            Assert.That(Tick(true, 0.1d).State, Is.EqualTo(MonsterState.Searching));
        }

        [Test]
        public void EqualDistanceNewCandidatesPreferTheLowerParticipantId()
        {
            Assert.That(Tick(true, 0d, Seen(B, 4f), Candidate(A, new WorldPoint(0f, 0f, 4f), true, true)).TargetId,
                Is.EqualTo(A));
        }

        [Test]
        public void TargetBecomingHiddenMidChaseIsDroppedImmediately()
        {
            Tick(true, 0d, Seen(A, 3f));
            MonsterDecision decision = Tick(true, 0.1d, Candidate(A, At(3f), false, true));
            Assert.That(decision.State, Is.EqualTo(MonsterState.Searching), "No LosingInterest grace for a hidden target.");
            Assert.That(decision.TargetId.IsValid, Is.False);
            Assert.That(_brain.IsExcluded(A, 0.1d), Is.False);

            Assert.That(Tick(true, 0.2d, Candidate(A, At(3f), false, true), Seen(B, 6f)).TargetId, Is.EqualTo(B),
                "Another detected player may be selected at once.");
        }

        [Test]
        public void AttackCooldownIsSharedAcrossTargetSwitches()
        {
            Assert.That(Tick(true, 0d, Seen(A, 1f), Seen(B, 1.4f)).HasAttack, Is.True);
            MonsterDecision switched = Tick(true, 0.5d, Seen(A, 1.45f), Seen(B, 1f));
            Assert.That(switched.TargetId, Is.EqualTo(B));
            Assert.That(switched.State, Is.EqualTo(MonsterState.Attacking));
            Assert.That(switched.HasAttack, Is.False, "Switching targets does not reset the two-second cooldown.");
            MonsterDecision next = Tick(true, 2d, Seen(A, 1.45f), Seen(B, 1f));
            Assert.That(next.HasAttack, Is.True);
            Assert.That(next.Attack.TargetId, Is.EqualTo(B));
        }

        [Test]
        public void FallingAsleepClearsPursuitExclusions()
        {
            Tick(true, 0d, Seen(A, 5f));
            Tick(true, 15d, Seen(A, 5f));
            Assert.That(_brain.IsExcluded(A, 15d), Is.True);

            Tick(false, 16d, Seen(A, 5f));
            Assert.That(_brain.IsExcluded(A, 16d), Is.False);
            Assert.That(Tick(true, 16.1d, Seen(A, 5f)).TargetId, Is.EqualTo(A));
        }

        [Test]
        public void PursuitExpiryAppliesWhileLosingInterest()
        {
            Tick(true, 0d, Seen(A, 5f));
            Tick(true, 11d, Seen(A, 5f));
            Assert.That(Tick(true, 14d, Candidate(A, At(5f), true, false)).State, Is.EqualTo(MonsterState.LosingInterest));

            MonsterDecision expired = Tick(true, 15d, Candidate(A, At(5f), true, false));
            Assert.That(expired.State, Is.EqualTo(MonsterState.Searching));
            Assert.That(_brain.IsExcluded(A, 15d), Is.True, "Pursuit expiry excludes even when the target was not in sight.");
        }

        [Test]
        public void TargetInRangeBehindAWallIsNotAttacked()
        {
            Tick(true, 0d, Seen(A, 3f));
            MonsterDecision decision = Tick(true, 0.5d, Candidate(A, At(1f), true, false));
            Assert.That(decision.State, Is.EqualTo(MonsterState.LosingInterest));
            Assert.That(decision.HasAttack, Is.False);
        }

        [Test]
        public void AttackRangeIsHorizontalAndAttackingStateHasExitHysteresis()
        {
            Assert.That(MonsterRules.IsWithinAttackRange(Origin, new WorldPoint(1.5f, 1f, 0f)), Is.True,
                "Height difference within tolerance does not reduce range.");
            Assert.That(MonsterRules.IsWithinAttackRange(Origin, new WorldPoint(1f, 3f, 0f)), Is.False,
                "A different floor is out of range.");

            Assert.That(Tick(true, 0d, Seen(A, 1.5f)).State, Is.EqualTo(MonsterState.Attacking));
            MonsterDecision edge = Tick(true, 0.5d, Seen(A, 1.6f));
            Assert.That(edge.State, Is.EqualTo(MonsterState.Attacking), "Within hysteresis the state is kept.");
            Assert.That(Tick(true, 2.1d, Seen(A, 1.6f)).HasAttack, Is.False, "Attacks still need the 1.5 m range.");
            Assert.That(Tick(true, 2.2d, Seen(A, 1.5f + MonsterRules.AttackStateExitHysteresisMetres + 0.01f)).State,
                Is.EqualTo(MonsterState.Chasing));
            Assert.That(Tick(true, 2.3d, Seen(A, 1.6f)).State, Is.EqualTo(MonsterState.Chasing),
                "Re-entering needs the real attack range.");
        }

        [Test]
        public void LitMatchAvailabilityRequiresActiveMatchAndLightsBeforeDeadlines()
        {
            var on = new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1200d, 150d, 1u);
            var off = new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1200d, 150d, 2u);
            var ended = new MatchTimelineSnapshot(true, true, LightPhase.On, 0d, 1200d, 150d, 3u);
            Assert.That(MatchTimelineAvailability.IsLitMatchActive(on, 149.9d), Is.True);
            Assert.That(MatchTimelineAvailability.IsLitMatchActive(on, 150d), Is.False);
            Assert.That(MatchTimelineAvailability.IsLitMatchActive(off, 10d), Is.False);
            Assert.That(MatchTimelineAvailability.IsLitMatchActive(ended, 10d), Is.False);
            Assert.That(MatchTimelineAvailability.IsLitMatchActive(default, 0d), Is.False,
                "The monster sleeps before the match starts.");
        }

        private MonsterDecision Tick(bool awake, double now, params MonsterCandidate[] candidates)
        {
            return Tick(awake, now, candidates.Length, candidates);
        }

        private MonsterDecision Tick(bool awake, double now, int activeCount, params MonsterCandidate[] candidates)
        {
            return _brain.Tick(new MonsterPerception(awake, now, Origin, activeCount, new List<MonsterCandidate>(candidates)));
        }

        private static MonsterCandidate Seen(ParticipantId id, float distance)
        {
            return Candidate(id, At(distance), true, true);
        }

        private static MonsterCandidate Candidate(ParticipantId id, WorldPoint position, bool targetable, bool lineOfSight)
        {
            return new MonsterCandidate(id, position, targetable, lineOfSight);
        }

        private static WorldPoint At(float distance) => new WorldPoint(distance, 0f, 0f);
    }
}
