using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ParkourSyncTests
    {
        [Test]
        public void ARowCarriesTheParkourMoveIntoTheStateWithoutItsDetails()
        {
            var row = new PlayerSnapshot { Position = new Vector3(0f, 3f, 0f), ParkourMove = Shooter.Protocol.ParkourMove.LedgeHang };

            var state = SnapshotMotion.ToState(row);

            Assert.That(state.Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.LedgeHang));
            Assert.That(state.Parkour.Elapsed, Is.Zero, "the snapshot has no time");
            Assert.That(state.Parkour.WallNormal, Is.EqualTo(Vector3.Zero), "the snapshot has no wall");
            Assert.That(state.Parkour.LedgePoint, Is.EqualTo(Vector3.Zero), "the snapshot has no ledge");
            Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(SnapshotMotion.ToState(new PlayerSnapshot()).Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void CompleteKeepsThePredictedParkourDetailsWhenTheMoveAgrees()
        {
            var predictedParkour = new CharacterParkourState(Shooter.Core.ParkourMove.LandingRoll, 0.2f, Vector3.Zero, Vector3.Zero, 0.5f);
            var predicted = Plain(new Vector3(0f, 1f, 0f)).WithParkour(predictedParkour);
            var authoritative = SnapshotMotion.ToState(new PlayerSnapshot
            {
                Position = new Vector3(0f, 1.01f, 0f),
                IsGrounded = true,
                ParkourMove = Shooter.Protocol.ParkourMove.LandingRoll,
            });

            var completed = SnapshotMotion.Complete(authoritative, predicted, NetMapWater.None, NetMotorSettings.Default.Stance);

            Assert.That(completed.Parkour, Is.EqualTo(predictedParkour));
            Assert.That(completed.Position, Is.EqualTo(new Vector3(0f, 1.01f, 0f)), "the position stays the server's");
        }

        [Test]
        public void CompleteTakesTheServersMoveWhenThePredictionMissedIt()
        {
            var predicted = Plain(new Vector3(0f, 3f, 0f))
                .WithParkour(new CharacterParkourState(Shooter.Core.ParkourMove.WallKick, 0.1f, Vector3.UnitX, Vector3.Zero));
            var authoritative = SnapshotMotion.ToState(new PlayerSnapshot
            {
                Position = new Vector3(0f, 3f, 0f),
                ParkourMove = Shooter.Protocol.ParkourMove.LedgeHang,
            });

            var completed = SnapshotMotion.Complete(authoritative, predicted, NetMapWater.None, NetMotorSettings.Default.Stance);
            var stopped = SnapshotMotion.Complete(
                SnapshotMotion.ToState(new PlayerSnapshot { Position = new Vector3(0f, 3f, 0f) }), predicted, NetMapWater.None, NetMotorSettings.Default.Stance);

            Assert.That(completed.Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.LedgeHang));
            Assert.That(completed.Parkour.WallNormal, Is.EqualTo(Vector3.Zero), "the predicted wall belongs to another move");
            Assert.That(stopped.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void WithWallRunKeepsTheParkourState()
        {
            var parkour = new CharacterParkourState(Shooter.Core.ParkourMove.WallClimb, 0.3f, -Vector3.UnitZ, Vector3.Zero);
            var state = Plain(Vector3.Zero).WithParkour(parkour);

            var changed = SnapshotMotion.WithWallRun(state, CharacterWallRunState.LeftWall(Vector3.UnitX));

            Assert.That(changed.Parkour, Is.EqualTo(parkour));
        }

        [Test]
        public void ARemotePoseReadsTheParkourMoveAndTheTrackHoldsItBetweenTicks()
        {
            var row = new PlayerSnapshot { Position = Vector3.Zero, ParkourMove = Shooter.Protocol.ParkourMove.WallClimb };
            var first = RemotePose.From(row);
            row.Position = new Vector3(0f, 2f, 0f);
            row.ParkourMove = Shooter.Protocol.ParkourMove.None;
            var second = RemotePose.From(row);

            Assert.That(first.Parkour, Is.EqualTo(Shooter.Core.ParkourMove.WallClimb));
            Assert.That(second.Parkour, Is.EqualTo(Shooter.Core.ParkourMove.None));

            var track = new RemoteTrack();
            track.Record(10, first);
            track.Record(12, second);
            Assert.That(track.TrySample(11f, out var middle), Is.True);
            Assert.That(middle.Position, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(middle.Parkour, Is.EqualTo(Shooter.Core.ParkourMove.WallClimb), "it is not interpolated, so the earlier record holds");
            Assert.That(track.TrySample(12f, out var late), Is.True);
            Assert.That(late.Parkour, Is.EqualTo(Shooter.Core.ParkourMove.None));
        }

        [Test]
        public void ARemotePoseWithoutAParkourMoveDoesNothingAndAnUnknownOneIsDropped()
        {
            Assert.That(new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "").Parkour, Is.EqualTo(Shooter.Core.ParkourMove.None));
            Assert.That(
                new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "", 0f, false, Shooter.Core.WallRunSide.None, false).Parkour,
                Is.EqualTo(Shooter.Core.ParkourMove.None));
            Assert.That(
                new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, "", 0f, false, Shooter.Core.WallRunSide.None, false, (Shooter.Core.ParkourMove)9).Parkour,
                Is.EqualTo(Shooter.Core.ParkourMove.None));
            Assert.That(
                new RemotePose(
                    Vector3.Zero, 0f, PlayerStance.Standing, "", 0f, false, Shooter.Core.WallRunSide.None, false, Shooter.Core.ParkourMove.LedgeClimbUp).Parkour,
                Is.EqualTo(Shooter.Core.ParkourMove.LedgeClimbUp));
        }

        [Test]
        public void TheWorldGivesTheParkourTuningToThePrediction()
        {
            var directory = Path.Combine(Path.GetTempPath(), "shooter-client-parkour-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var stream = File.Create(Path.Combine(directory, "Course" + ShooterMapFile.Extension)))
                {
                    ShooterMapFile.Write(stream, new WorldMap(Array.Empty<WorldCollider>()));
                }

                Assert.That(ClientWorld.TryLoad(directory, "Course", out var world, out var error), Is.True, error);
                var defaults = NetMotorSettings.Default;
                var parkour = new ParkourTuning(new WallKickTuning(true, 7f, 8f), momentum: new MomentumTuning(1f, 0.5f));
                var motor = new NetMotorSettings(
                    defaults.Motor,
                    defaults.Stance,
                    defaults.Mover,
                    defaults.Prone,
                    defaults.Swim,
                    defaults.WallRun,
                    defaults.Parachute,
                    defaults.Slide,
                    defaults.Zipline,
                    parkour);

                Assert.That(world.CreateSimulation(motor, 60, -50f).Parkour, Is.SameAs(parkour));
                Assert.That(world.CreateSimulation(defaults, 60, -50f).Parkour, Is.SameAs(ParkourTuning.Default));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static CharacterMotorState Plain(Vector3 position)
        {
            return new CharacterMotorState(position, Vector3.Zero, false, false);
        }
    }
}
