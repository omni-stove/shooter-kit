using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HolsterStateTests
    {
        [Test]
        public void ItStartsDrawnAndArmed()
        {
            var state = new HolsterState();

            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(state.Lowering, Is.Zero);
            Assert.That(state.IsMoving, Is.False);
            Assert.That(state.RemainingSeconds, Is.Zero);
            Assert.That(state.IsArmed(HolsterSpec.Default), Is.True);
        }

        [Test]
        public void HolsteringLowersTheWeaponOverTheGivenSecondsThenSettles()
        {
            var state = new HolsterState();

            Assert.That(state.BeginHolster(0.5f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(state.RemainingSeconds, Is.EqualTo(0.5f));

            Assert.That(state.Tick(0.25f), Is.False);
            Assert.That(state.Lowering, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(state.RemainingSeconds, Is.EqualTo(0.25f).Within(1e-5f));

            Assert.That(state.Tick(0.25f), Is.True, "settling is reported on the tick it happens");
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(state.Lowering, Is.EqualTo(1f));
            Assert.That(state.RemainingSeconds, Is.Zero);
            Assert.That(state.Tick(1f), Is.False, "nothing more to settle");
        }

        [Test]
        public void DrawingRaisesTheWeaponOverTheGivenSecondsThenSettles()
        {
            var state = new HolsterState();
            state.BeginHolster(0f);

            Assert.That(state.BeginDraw(0.4f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(state.Lowering, Is.EqualTo(1f));
            Assert.That(state.RemainingSeconds, Is.EqualTo(0.4f));

            Assert.That(state.Tick(0.1f), Is.False);
            Assert.That(state.Lowering, Is.EqualTo(0.75f).Within(1e-5f));

            Assert.That(state.Tick(0.5f), Is.True, "a long tick settles without going past");
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(state.Lowering, Is.Zero);
        }

        [Test]
        public void ZeroSecondsSettlesAtOnce()
        {
            var state = new HolsterState();

            Assert.That(state.BeginHolster(0f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(state.Lowering, Is.EqualTo(1f));

            Assert.That(state.BeginDraw(0f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(state.Lowering, Is.Zero);
        }

        [Test]
        public void DrawingPartWayThroughHolsteringGoesBackFromTheCurrentLowering()
        {
            var state = new HolsterState();
            state.BeginHolster(1f);
            state.Tick(0.4f);

            Assert.That(state.BeginDraw(0.5f), Is.True);

            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(state.Lowering, Is.EqualTo(0.4f).Within(1e-5f), "it does not jump to fully lowered");
            Assert.That(state.RemainingSeconds, Is.EqualTo(0.5f * 0.4f).Within(1e-5f), "draw seconds × lowering");

            Assert.That(state.Tick(0.1f), Is.False);
            Assert.That(state.Lowering, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(state.Tick(0.1f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn));
        }

        [Test]
        public void HolsteringPartWayThroughDrawingGoesOnFromTheCurrentLowering()
        {
            var state = new HolsterState();
            state.BeginHolster(0f);
            state.BeginDraw(1f);
            state.Tick(0.25f);

            Assert.That(state.BeginHolster(2f), Is.True);

            Assert.That(state.Lowering, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(state.RemainingSeconds, Is.EqualTo(2f * 0.25f).Within(1e-5f));
        }

        [Test]
        public void DrawingRightAfterStartingToHolsterIsDrawnAtOnce()
        {
            var state = new HolsterState();
            state.BeginHolster(1f);

            Assert.That(state.BeginDraw(0.5f), Is.True);

            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn), "nothing was lowered yet, so nothing is left to raise");
            Assert.That(state.Lowering, Is.Zero);
        }

        [Test]
        public void BeginningTheSameWayAgainChangesNothing()
        {
            var state = new HolsterState();

            Assert.That(state.BeginDraw(0.5f), Is.False, "already drawn");

            state.BeginHolster(1f);
            state.Tick(0.5f);
            Assert.That(state.BeginHolster(0.1f), Is.False, "already holstering");
            Assert.That(state.RemainingSeconds, Is.EqualTo(0.5f).Within(1e-5f), "the first holster time is kept");

            state.Tick(1f);
            Assert.That(state.BeginHolster(1f), Is.False, "already holstered");

            state.BeginDraw(1f);
            Assert.That(state.BeginDraw(0.1f), Is.False, "already drawing");
        }

        [Test]
        public void ResetToDrawnPutsTheWeaponBackUpFromAnyPhase()
        {
            var state = new HolsterState();
            state.BeginHolster(1f);
            state.Tick(0.5f);

            state.ResetToDrawn();

            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(state.Lowering, Is.Zero);
            Assert.That(state.RemainingSeconds, Is.Zero);
            Assert.That(state.Tick(1f), Is.False);
        }

        [TestCase(HolsterPhase.Drawn, true)]
        [TestCase(HolsterPhase.Holstering, false)]
        [TestCase(HolsterPhase.Holstered, false)]
        [TestCase(HolsterPhase.Drawing, true)]
        public void TheDefaultSpecCountsDrawnAndDrawingAsArmed(HolsterPhase phase, bool armed)
        {
            var state = Into(phase);

            Assert.That(state.Phase, Is.EqualTo(phase));
            Assert.That(state.IsArmed(HolsterSpec.Default), Is.EqualTo(armed));
            Assert.That(state.IsArmed(HolsterSpec.Default, heldByOthers: true), Is.False, "by default, being made to holster by a ladder is not armed");
        }

        [Test]
        public void TheSpecDecidesWhichPhasesAreArmedAndWhetherBeingHeldByOthersCounts()
        {
            var onlyDrawn = new HolsterSpec(armedPhases: new[] { HolsterPhase.Drawn }, armedWhileHeldByOthers: true);

            Assert.That(Into(HolsterPhase.Drawn).IsArmed(onlyDrawn, heldByOthers: true), Is.True);
            Assert.That(Into(HolsterPhase.Drawing).IsArmed(onlyDrawn), Is.False);
            Assert.That(Into(HolsterPhase.Holstered).IsArmed(onlyDrawn, heldByOthers: true), Is.False, "being held by others does not arm a holstered weapon");
            Assert.That(Into(HolsterPhase.Drawn).IsArmed(new HolsterSpec(armedPhases: Array.Empty<HolsterPhase>())), Is.False);
        }

        [Test]
        public void TheStatusCarriesThePhaseTheLoweringAndWhetherItIsArmed()
        {
            var state = new HolsterState();
            state.BeginHolster(1f);
            state.Tick(0.5f);

            var status = state.Status(HolsterSpec.Default);

            Assert.That(status.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(status.Lowering, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(status.IsArmed, Is.False);
            Assert.That(new HolsterState().Status(HolsterSpec.Default), Is.EqualTo(new HolsterStatus(HolsterPhase.Drawn, 0f, true)));
        }

        [Test]
        public void BadSecondsAndAMissingSpecAreRejected()
        {
            var state = new HolsterState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginHolster(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginHolster(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginHolster(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-0.1f));
            Assert.Throws<ArgumentNullException>(() => state.IsArmed(null));
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Drawn), "a rejected call changes nothing");

            state.BeginHolster(0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginDraw(-1f));
            Assert.That(state.Phase, Is.EqualTo(HolsterPhase.Holstered));
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterSpec(moveSpeedMultiplier: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterSpec(moveSpeedMultiplier: float.NaN));
            Assert.Throws<ArgumentException>(() => new HolsterSpec(armedPhases: new[] { HolsterPhase.Drawn, HolsterPhase.Drawn }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterSpec(armedPhases: new[] { (HolsterPhase)9 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => HolsterSpec.Default.IsArmedPhase((HolsterPhase)9));
        }

        [Test]
        public void TheSpecKeepsItsOwnCopyOfTheArmedPhases()
        {
            var phases = new[] { HolsterPhase.Holstered };
            var spec = new HolsterSpec(armedPhases: phases);
            phases[0] = HolsterPhase.Drawn;

            Assert.That(spec.ArmedPhases, Is.EqualTo(new[] { HolsterPhase.Holstered }));
            Assert.That(spec.IsArmedPhase(HolsterPhase.Holstered), Is.True);
            Assert.That(spec.IsArmedPhase(HolsterPhase.Drawn), Is.False);
        }

        [Test]
        public void TheDefaultSpecChangesNothing()
        {
            Assert.That(HolsterSpec.Default.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(HolsterSpec.Default.ArmedPhases, Is.EqualTo(new[] { HolsterPhase.Drawn, HolsterPhase.Drawing }));
            Assert.That(HolsterSpec.Default.ArmedWhileHeldByOthers, Is.False);
        }

        [Test]
        public void TheStatusRejectsAnUnknownPhaseAndALoweringOutsideZeroToOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterStatus((HolsterPhase)9, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterStatus(HolsterPhase.Drawn, -0.1f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterStatus(HolsterPhase.Drawn, 1.1f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HolsterStatus(HolsterPhase.Drawn, float.NaN, true));
            Assert.That(new HolsterStatus(HolsterPhase.Drawing, 0.5f, true) == new HolsterStatus(HolsterPhase.Drawing, 0.5f, true), Is.True);
            Assert.That(new HolsterStatus(HolsterPhase.Drawing, 0.5f, true) != new HolsterStatus(HolsterPhase.Drawing, 0.5f, false), Is.True);
        }

        [Test]
        public void TheLogLinesStartWithTheTag()
        {
            Assert.That(HolsterLog.Began("p1", HolsterPhase.Holstering, 0.5f), Is.EqualTo("[Holster] holder=p1 began=Holstering seconds=0.50"));
            Assert.That(HolsterLog.Settled(null, HolsterPhase.Holstered), Is.EqualTo("[Holster] holder=- settled=Holstered"));
            Assert.That(HolsterLog.Reset("p1", "died"), Is.EqualTo("[Holster] holder=p1 reset=Drawn reason=died"));
            Assert.That(HolsterLog.NoInput("Player/Holster"), Is.EqualTo("[Holster] input action 'Player/Holster' is missing; the holster key is not read"));
            Assert.That(
                HolsterLog.Loaded(HolsterSpec.Default),
                Is.EqualTo("[Holster] spec moveSpeedMultiplier=1.00 armed=Drawn,Drawing armedWhileHeldByOthers=false"));
            Assert.That(
                HolsterLog.Loaded(new HolsterSpec(0.8f, Array.Empty<HolsterPhase>(), true)),
                Is.EqualTo("[Holster] spec moveSpeedMultiplier=0.80 armed=- armedWhileHeldByOthers=true"));
            Assert.Throws<ArgumentNullException>(() => HolsterLog.Loaded(null));
        }

        private static HolsterState Into(HolsterPhase phase)
        {
            var state = new HolsterState();
            switch (phase)
            {
                case HolsterPhase.Holstering:
                    state.BeginHolster(1f);
                    state.Tick(0.5f);
                    break;
                case HolsterPhase.Holstered:
                    state.BeginHolster(0f);
                    break;
                case HolsterPhase.Drawing:
                    state.BeginHolster(0f);
                    state.BeginDraw(1f);
                    state.Tick(0.5f);
                    break;
            }

            return state;
        }
    }
}
