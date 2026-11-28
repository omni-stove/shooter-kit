using System;
using System.Numerics;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientVipStatusTests
    {
        private const int _self = 3;
        private const int _ally = 4;
        private const int _enemy = 7;

        private ClientVipStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientVipStatus(_self);
        }

        [Test]
        public void TheOwnerOfTheVipItemIsTheVipInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(Snapshot(vip: _ally));

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<VipStatus>(), "the HUD formatter registered for VipStatus reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Vip));
            var vip = _status.Vip;
            Assert.That(vip.VipId, Is.EqualTo(_ally));
            Assert.That(vip.IsLocalVip, Is.False);
            Assert.That(vip.DefenderSide, Is.EqualTo(Side.Bravo));
            Assert.That(vip.ExitFraction, Is.Zero);
            Assert.That(_status.VipId, Is.EqualTo(_ally));
            Assert.That(_status.IsStandingVip(_ally), Is.True);
            Assert.That(_status.IsStandingVip(_self), Is.False);
            Assert.That(_status.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(status.RemainingSeconds, Is.EqualTo(120f));
            Assert.That(status.OwnSide, Is.EqualTo(Side.Bravo));
            Assert.That(status.RespawnSeconds, Is.Null, "nobody comes back within a round");
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void TheLocalPlayerKnowsWhenTheyAreTheVip()
        {
            _status.Apply(Snapshot(vip: _self));

            Assert.That(_status.Vip.IsLocalVip, Is.True);
            Assert.That(_status.Vip.VipId, Is.EqualTo(_self));
        }

        [Test]
        public void TheTeamOfTheVipItemIsTheDefendingSide()
        {
            _status.Apply(Snapshot(vip: _enemy, defender: Team.Alpha));

            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Alpha));
            Assert.That(_status.Status.OwnSide, Is.EqualTo(Side.Bravo), "the local player attacks this round");
            Assert.That(_status.Vip.IsLocalVip, Is.False);
        }

        [Test]
        public void BeforeTheVipIsChosenTheExtractionPointTellsTheDefendingSide()
        {
            _status.Apply(Snapshot(vip: null, defender: Team.Alpha, phase: RoundPhase.Preparing));

            var vip = _status.Vip;
            Assert.That(vip.VipId, Is.Null);
            Assert.That(vip.DefenderSide, Is.EqualTo(Side.Alpha));
            Assert.That(vip.IsLocalVip, Is.False);
            Assert.That(vip.ExitFraction, Is.Zero);
            Assert.That(_status.VipId, Is.Null);
            Assert.That(_status.Phase, Is.EqualTo(RoundPhase.Preparing));
        }

        [Test]
        public void TheVipItemTeamWinsOverTheExtractionPointTeam()
        {
            var snapshot = Snapshot(vip: _ally, defender: Team.Bravo, withExit: false);
            AddExit(snapshot, 5, Team.Alpha);

            _status.Apply(snapshot);

            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void WithoutAnyItemTheLastDefendingSideIsKept()
        {
            _status.Apply(Snapshot(vip: _ally, defender: Team.Alpha));

            _status.Apply(Snapshot(vip: null, withExit: false, phase: RoundPhase.Preparing));

            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void WithoutAnyItemAtAllTheFirstDefenderIsBravoAsOffline()
        {
            _status.Apply(Snapshot(vip: null, withExit: false, phase: RoundPhase.Waiting));

            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void TheExitProgressIsShownWhileTheVipStands()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f));

            Assert.That(_status.Vip.ExitFraction, Is.EqualTo(0.4f));
        }

        [Test]
        public void TheExitProgressIsNotShownOutsideTheLiveRound()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f, phase: RoundPhase.Preparing));

            Assert.That(_status.Vip.ExitFraction, Is.Zero);
        }

        [Test]
        public void AFullExitIsShownAsOne()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 1f));

            Assert.That(_status.Vip.ExitFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ADroppedItemMeansTheVipHasFallen()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f, fallen: true));

            Assert.That(_status.VipId, Is.EqualTo(_ally), "still the vip while down, as offline");
            Assert.That(_status.Vip.VipId, Is.EqualTo(_ally));
            Assert.That(_status.Vip.ExitFraction, Is.Zero, "a fallen vip does not go on escaping");
            Assert.That(_status.IsStandingVip(_ally), Is.False, "a fallen vip loses the marker");
        }

        [Test]
        public void FallingChangesTheStatus()
        {
            _status.Apply(Snapshot(vip: _ally));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(vip: _ally, fallen: true));

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
        }

        [Test]
        public void TheRoundsWonAreTheSideScores()
        {
            _status.Apply(Snapshot(vip: _ally, alphaWins: 2, bravoWins: 1));

            Assert.That(_status.Status.SideScores, Is.EqualTo(new[]
            {
                new SideScore(Side.Alpha, 2d),
                new SideScore(Side.Bravo, 1d),
            }));
        }

        [Test]
        public void WithoutAnOwnRowTheLocalPlayerIsUnknown()
        {
            _status.Apply(Snapshot(vip: _ally, withSelf: false));

            Assert.That(_status.Vip.IsLocalVip, Is.Null);
            Assert.That(_status.Status.OwnSide, Is.Null);
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void ProgressWithinTheSamePercentKeepsTheShownStatus()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.401f));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(vip: _ally, exitFraction: 0.405f));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void ProgressToTheNextPercentChangesTheStatus()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 0.4f));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(vip: _ally, exitFraction: 0.5f));

            Assert.That(changed, Is.True);
            Assert.That(_status.Vip.ExitFraction, Is.EqualTo(0.5f));
            Assert.That(((VipStatus)shown.Detail).ExitFraction, Is.EqualTo(0.4f), "the old status is not changed");
        }

        [Test]
        public void ANewVipChangesTheStatus()
        {
            _status.Apply(Snapshot(vip: _ally));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(vip: _self));

            Assert.That(changed, Is.True);
            Assert.That(_status.Vip.VipId, Is.EqualTo(_self));
            Assert.That(_status.Vip.IsLocalVip, Is.True);
            Assert.That(_status.IsStandingVip(_ally), Is.False);
            Assert.That(((VipStatus)shown.Detail).VipId, Is.EqualTo(_ally), "the old status is not changed");
        }

        [Test]
        public void TheFirstVipItemCountsAndOwnerlessOnesAreSkipped()
        {
            var snapshot = Snapshot(vip: null);
            ref var ownerless = ref snapshot.ObjectiveItems.Add();
            ownerless.ItemId = 10;
            ownerless.Kind = ObjectiveItemKind.Vip;
            ownerless.Team = Team.Alpha;
            ownerless.State = ObjectiveItemState.Dropped;
            AddVip(snapshot, 11, _ally, Team.Bravo, fallen: false);
            AddVip(snapshot, 12, _enemy, Team.Alpha, fallen: false);

            _status.Apply(snapshot);

            Assert.That(_status.VipId, Is.EqualTo(_ally));
            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void OtherItemsAreNotTheVip()
        {
            var snapshot = Snapshot(vip: null);
            ref var juggernaut = ref snapshot.ObjectiveItems.Add();
            juggernaut.ItemId = 10;
            juggernaut.Kind = ObjectiveItemKind.Juggernaut;
            juggernaut.HasOwner = true;
            juggernaut.OwnerId = _ally;
            juggernaut.State = ObjectiveItemState.Carried;

            _status.Apply(snapshot);

            Assert.That(_status.VipId, Is.Null);
        }

        [Test]
        public void TheEndShowsTheWinnerOfTheMatch()
        {
            _status.Apply(Snapshot(vip: _ally, exitFraction: 1f, phase: RoundPhase.Ended, state: GameModeState.Ended, winner: Team.Bravo, bravoWins: 3, remaining: 0));

            var status = _status.Status;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(Side.Bravo));
            Assert.That(status.RemainingSeconds, Is.Zero);
            Assert.That(_status.Vip.ExitFraction, Is.Zero, "the round is over");
        }

        [Test]
        public void AWinnerIsNotShownBeforeTheEnd()
        {
            _status.Apply(Snapshot(vip: _ally, winner: Team.Alpha));

            Assert.That(_status.Status.Winner, Is.Null);
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(vip: _ally, defender: Team.Alpha));
            var other = Snapshot(vip: _ally, defender: Team.Alpha);
            other.Match.Objective = MatchObjective.SearchAndDestroy;

            Assert.That(_status.Apply(other), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Vip, Is.Null);
            Assert.That(_status.VipId, Is.Null);
            Assert.That(_status.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(_status.IsStandingVip(_ally), Is.False);
            Assert.That(_status.Apply(other), Is.False);
            Assert.That(_status.Clear(), Is.False);
        }

        [Test]
        public void ComingBackToVipMakesANewStatusWithoutTheOldDefender()
        {
            _status.Apply(Snapshot(vip: _ally, defender: Team.Alpha));
            _status.Clear();

            Assert.That(_status.Apply(Snapshot(vip: null, withExit: false, phase: RoundPhase.Waiting)), Is.True);
            Assert.That(_status.VipId, Is.Null);
            Assert.That(_status.Vip.DefenderSide, Is.EqualTo(Side.Bravo), "the defender of the old match is forgotten");
        }

        [Test]
        public void ANullSnapshotIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int? vip,
            Team defender = Team.Bravo,
            bool fallen = false,
            float exitFraction = 0f,
            bool withSelf = true,
            bool withExit = true,
            int alphaWins = 0,
            int bravoWins = 0,
            int remaining = 120,
            RoundPhase phase = RoundPhase.Live,
            GameModeState state = GameModeState.Playing,
            Team? winner = null)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Vip;
            match.HasRound = true;
            match.Round = phase;
            match.AlphaPoints = alphaWins;
            match.BravoPoints = bravoWins;
            match.PlantFraction = exitFraction;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;

            if (withSelf)
            {
                AddPlayer(snapshot, _self, Team.Bravo);
            }

            AddPlayer(snapshot, _ally, Team.Bravo);
            AddPlayer(snapshot, _enemy, Team.Alpha);
            if (withExit)
            {
                AddExit(snapshot, 0, defender);
            }

            if (vip.HasValue)
            {
                AddVip(snapshot, 1, vip.Value, defender, fallen);
            }

            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
            row.WeaponId = "";
        }

        private static void AddExit(SnapshotMessage snapshot, int itemId, Team defender)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = itemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.ExtractionPoint;
            item.Team = defender;
            item.Position = new Vector3(10f, 0f, 10f);
            item.State = ObjectiveItemState.Home;
            item.Radius = 3f;
        }

        private static void AddVip(SnapshotMessage snapshot, int itemId, int playerId, Team defender, bool fallen)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = itemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Vip;
            item.Team = defender;
            item.HasOwner = true;
            item.OwnerId = playerId;
            item.Position = Vector3.Zero;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : playerId;
        }
    }
}
