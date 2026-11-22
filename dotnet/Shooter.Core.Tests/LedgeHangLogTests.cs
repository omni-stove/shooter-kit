using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class LedgeHangLogTests
    {
        private static readonly Vector3 _wall = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _ledgePoint = new Vector3(0.25f, 3f, 1f);
        private static readonly CharacterParkourState _hanging = new CharacterParkourState(ParkourMove.LedgeHang, 0.125f, _wall, _ledgePoint);
        private static readonly CharacterParkourState _shimmying = new CharacterParkourState(ParkourMove.LedgeShimmy, 0.125f, _wall, _ledgePoint);
        private static readonly CharacterParkourState _climbing = new CharacterParkourState(ParkourMove.LedgeClimbUp, 0.125f, _wall, _ledgePoint);

        [Test]
        public void LinesCarryTheLedgeAndTheReason()
        {
            Assert.That(LedgeHangLog.Grab(_ledgePoint, _wall), Is.EqualTo("[LedgeHang] grab ledge=(0.25, 3.00, 1.00) normal=(0.00, 0.00, -1.00)"));
            Assert.That(LedgeHangLog.Edge(_ledgePoint), Is.EqualTo("[LedgeHang] edge ledge=(0.25, 3.00, 1.00)"));
            Assert.That(LedgeHangLog.Climb(_ledgePoint), Is.EqualTo("[LedgeHang] climb ledge=(0.25, 3.00, 1.00)"));
            Assert.That(LedgeHangLog.Top(new Vector3(0.25f, 4f, 1.6f)), Is.EqualTo("[LedgeHang] top position=(0.25, 4.00, 1.60)"));
            Assert.That(
                LedgeHangLog.LetGo(LedgeLetGoReason.Dropped, _ledgePoint), Is.EqualTo("[LedgeHang] letgo reason=Dropped ledge=(0.25, 3.00, 1.00)"));
        }

        [Test]
        public void LettingGoNeedsAReason()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LedgeHangLog.LetGo(LedgeLetGoReason.None, _ledgePoint));
        }

        [Test]
        public void AStepIsLoggedOnlyWhenTheMoveChanges()
        {
            var grabbed = Result(_hanging, LedgeHangMotion.Grabbed);
            var hanging = Result(_hanging, LedgeHangMotion.Hanging);
            var shimmying = Result(_shimmying, LedgeHangMotion.Shimmying);
            var stopped = Result(_hanging, LedgeHangMotion.StoppedAtEdge);
            var climbing = Result(_climbing, LedgeHangMotion.ClimbingUp);
            var top = new LedgeHangResult(
                CharacterParkourState.None, LedgeHangMotion.ClimbedUp, LedgeLetGoReason.None, new Vector3(0.25f, 4f, 1.6f), Vector3.Zero);
            var letGo = new LedgeHangResult(
                new CharacterParkourState(ParkourMove.None, 0f, _wall, _ledgePoint), LedgeHangMotion.LetGo, LedgeLetGoReason.LostLedge, Vector3.Zero, Vector3.Zero);

            Assert.That(LedgeHangLog.ForStep(CharacterParkourState.None, grabbed), Is.EqualTo(LedgeHangLog.Grab(_ledgePoint, _wall)));
            Assert.That(LedgeHangLog.ForStep(_hanging, hanging), Is.Null);
            Assert.That(LedgeHangLog.ForStep(_hanging, shimmying), Is.Null);
            Assert.That(LedgeHangLog.ForStep(_shimmying, stopped), Is.EqualTo(LedgeHangLog.Edge(_ledgePoint)));
            Assert.That(LedgeHangLog.ForStep(_hanging, stopped), Is.Null, "still pushing against the end");
            Assert.That(LedgeHangLog.ForStep(_hanging, climbing), Is.EqualTo(LedgeHangLog.Climb(_ledgePoint)));
            Assert.That(LedgeHangLog.ForStep(_climbing, climbing), Is.Null);
            Assert.That(LedgeHangLog.ForStep(_climbing, top), Is.EqualTo(LedgeHangLog.Top(new Vector3(0.25f, 4f, 1.6f))));
            Assert.That(LedgeHangLog.ForStep(_hanging, letGo), Is.EqualTo("[LedgeHang] letgo reason=LostLedge ledge=(0.25, 3.00, 1.00)"));
            Assert.That(
                LedgeHangLog.ForStep(CharacterParkourState.None, LedgeHangResult.Resting(CharacterParkourState.None, Vector3.Zero, Vector3.Zero)),
                Is.Null);
        }

        private static LedgeHangResult Result(CharacterParkourState state, LedgeHangMotion motion)
        {
            return new LedgeHangResult(state, motion, LedgeLetGoReason.None, Vector3.Zero, Vector3.Zero);
        }
    }
}
