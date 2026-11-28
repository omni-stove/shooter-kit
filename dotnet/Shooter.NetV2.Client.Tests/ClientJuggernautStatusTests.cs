using System;
using System.Numerics;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientJuggernautStatusTests
    {
        private const int _self = 3;
        private const int _other = 4;
        private const int _third = 7;

        private ClientJuggernautStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientJuggernautStatus(_self);
        }

        [Test]
        public void BeforeOneIsChosenTheCountdownIsShownInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(Snapshot(juggernaut: null, secondsUntilChosen: 5));

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<JuggernautStatus>(), "the HUD formatter registered for JuggernautStatus reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Juggernaut));
            var juggernaut = _status.Juggernaut;
            Assert.That(juggernaut.JuggernautId, Is.Null);
            Assert.That(juggernaut.SecondsUntilChosen, Is.EqualTo(5f));
            Assert.That(juggernaut.IsLocalPlayerJuggernaut, Is.False);
            Assert.That(_status.JuggernautId, Is.Null);
            Assert.That(_status.IsStandingJuggernaut(_self), Is.False);
            Assert.That(status.RemainingSeconds, Is.EqualTo(600f));
            Assert.That(status.OwnSide, Is.EqualTo(new Side(_self)));
            Assert.That(status.RespawnSeconds, Is.Null);
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void TheOwnerOfTheJuggernautItemIsTheJuggernaut()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0));

            var juggernaut = _status.Juggernaut;
            Assert.That(juggernaut.JuggernautId, Is.EqualTo(_other));
            Assert.That(juggernaut.SecondsUntilChosen, Is.Zero);
            Assert.That(juggernaut.IsLocalPlayerJuggernaut, Is.False);
            Assert.That(_status.JuggernautId, Is.EqualTo(_other));
            Assert.That(_status.IsStandingJuggernaut(_other), Is.True);
            Assert.That(_status.IsStandingJuggernaut(_self), Is.False);
        }

        [Test]
        public void TheLocalPlayerKnowsWhenTheyAreTheJuggernaut()
        {
            _status.Apply(Snapshot(juggernaut: _self, secondsUntilChosen: 0));

            Assert.That(_status.Juggernaut.IsLocalPlayerJuggernaut, Is.True);
            Assert.That(_status.Juggernaut.JuggernautId, Is.EqualTo(_self));
        }

        [Test]
        public void AStaleCountdownIsDroppedOnceOneIsChosen()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 3));

            Assert.That(_status.Juggernaut.SecondsUntilChosen, Is.Zero);
        }

        [Test]
        public void ADroppedItemMeansTheJuggernautHasFallen()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, fallen: true));

            Assert.That(_status.JuggernautId, Is.EqualTo(_other), "still the juggernaut while down");
            Assert.That(_status.Juggernaut.JuggernautId, Is.EqualTo(_other));
            Assert.That(_status.IsStandingJuggernaut(_other), Is.False, "a fallen juggernaut loses the marker");
        }

        [Test]
        public void EachPlayersPointsAreTheirSideScoresInSideOrder()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, selfPoints: 3, otherPoints: 7, thirdPoints: 1));

            Assert.That(_status.Status.SideScores, Is.EqualTo(new[]
            {
                new SideScore(new Side(_self), 3d),
                new SideScore(new Side(_other), 7d),
                new SideScore(new Side(_third), 1d),
            }));
        }

        [Test]
        public void WithoutAnOwnRowTheLocalPlayerIsUnknown()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, withSelf: false));

            Assert.That(_status.Juggernaut.IsLocalPlayerJuggernaut, Is.Null);
            Assert.That(_status.Status.OwnSide, Is.Null);
            Assert.That(_status.Status.SideScores, Has.Count.EqualTo(2));
        }

        [Test]
        public void TheOwnRespawnSecondsAreShown()
        {
            var snapshot = Snapshot(juggernaut: _other, secondsUntilChosen: 0);
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 4;

            _status.Apply(snapshot);

            Assert.That(_status.Status.RespawnSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, otherPoints: 2));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, otherPoints: 2));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void APointChangesTheStatus()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, otherPoints: 2));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, otherPoints: 3));

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
            Assert.That(_status.Status.SideScores[1].Score, Is.EqualTo(3d));
            Assert.That(shown.SideScores[1].Score, Is.EqualTo(2d), "the old status is not changed");
        }

        [Test]
        public void TakingOverChangesTheStatus()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, otherPoints: 2));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(juggernaut: _self, secondsUntilChosen: 0, selfPoints: 3, otherPoints: 2));

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
            Assert.That(_status.Juggernaut.JuggernautId, Is.EqualTo(_self));
            Assert.That(_status.Juggernaut.IsLocalPlayerJuggernaut, Is.True);
            Assert.That(_status.IsStandingJuggernaut(_other), Is.False);
            Assert.That(_status.IsStandingJuggernaut(_self), Is.True);
            Assert.That(((JuggernautStatus)shown.Detail).JuggernautId, Is.EqualTo(_other), "the old status is not changed");
        }

        [Test]
        public void TheFirstJuggernautItemCountsAndOwnerlessOnesAreSkipped()
        {
            var snapshot = Snapshot(juggernaut: null, secondsUntilChosen: 0);
            ref var ownerless = ref snapshot.ObjectiveItems.Add();
            ownerless.ItemId = 0;
            ownerless.Kind = ObjectiveItemKind.Juggernaut;
            ownerless.State = ObjectiveItemState.Dropped;
            AddJuggernaut(snapshot, 1, _other, fallen: false);
            AddJuggernaut(snapshot, 2, _third, fallen: false);

            _status.Apply(snapshot);

            Assert.That(_status.JuggernautId, Is.EqualTo(_other));
        }

        [Test]
        public void OtherItemsAreNotTheJuggernaut()
        {
            var snapshot = Snapshot(juggernaut: null, secondsUntilChosen: 0);
            ref var leader = ref snapshot.ObjectiveItems.Add();
            leader.ItemId = 0;
            leader.Kind = ObjectiveItemKind.Leader;
            leader.HasOwner = true;
            leader.OwnerId = _other;
            leader.State = ObjectiveItemState.Carried;

            _status.Apply(snapshot);

            Assert.That(_status.JuggernautId, Is.Null);
        }

        [Test]
        public void TheEndShowsThePlayerWithTheMostPoints()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, selfPoints: 15, otherPoints: 4, state: GameModeState.Ended, remaining: 0));

            var status = _status.Status;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(new Side(_self)));
            Assert.That(status.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void ATieAtTheEndHasNoWinner()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0, selfPoints: 4, otherPoints: 4, state: GameModeState.Ended, remaining: 0));

            Assert.That(_status.Status.IsEnded, Is.True);
            Assert.That(_status.Status.Winner, Is.Null);
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0));
            var other = Snapshot(juggernaut: _other, secondsUntilChosen: 0);
            other.Match.Objective = MatchObjective.FreeForAll;

            Assert.That(_status.Apply(other), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Juggernaut, Is.Null);
            Assert.That(_status.JuggernautId, Is.Null);
            Assert.That(_status.IsStandingJuggernaut(_other), Is.False);
            Assert.That(_status.Apply(other), Is.False);
            Assert.That(_status.Clear(), Is.False);
        }

        [Test]
        public void ComingBackToJuggernautMakesANewStatus()
        {
            _status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0));
            _status.Clear();

            Assert.That(_status.Apply(Snapshot(juggernaut: _other, secondsUntilChosen: 0)), Is.True);
            Assert.That(_status.JuggernautId, Is.EqualTo(_other));
        }

        [Test]
        public void ANullSnapshotIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int? juggernaut,
            int secondsUntilChosen,
            bool fallen = false,
            bool withSelf = true,
            int selfPoints = 0,
            int otherPoints = 0,
            int thirdPoints = 0,
            int remaining = 600,
            GameModeState state = GameModeState.Playing)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Juggernaut;
            match.ObjectiveRemainingSeconds = secondsUntilChosen;

            // 陣営の番号の順に並ぶことを確かめるため、行は Id の順に並べない
            AddPlayer(snapshot, _third, thirdPoints);
            if (withSelf)
            {
                AddPlayer(snapshot, _self, selfPoints);
            }

            AddPlayer(snapshot, _other, otherPoints);
            if (juggernaut.HasValue)
            {
                AddJuggernaut(snapshot, 0, juggernaut.Value, fallen);
            }

            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, int points)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = Team.Alpha;
            row.Health = 100;
            row.WeaponId = "";
            row.GunGameTier = points;
        }

        private static void AddJuggernaut(SnapshotMessage snapshot, int itemId, int playerId, bool fallen)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = itemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Juggernaut;
            item.HasOwner = true;
            item.OwnerId = playerId;
            item.Position = Vector3.Zero;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : playerId;
        }
    }
}
