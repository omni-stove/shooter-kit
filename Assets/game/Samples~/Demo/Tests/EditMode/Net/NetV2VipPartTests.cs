using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 相手の体は remote で渡し、場に残った NetV2Client に左右されないようにする
    public sealed class NetV2VipPartTests
    {
        private const int _localId = 1;
        private const int _allyId = 2;
        private const int _enemyId = 3;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private readonly Dictionary<int, NetV2RemotePlayer> _remotes = new Dictionary<int, NetV2RemotePlayer>();
        private int _ownId;
        private int _tick;
        private NetV2VipPart _part;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _remotes.Clear();
            _ownId = _localId;
            _tick = 0;
            _part = new NetV2VipPart(() => _ownId, null, id => _remotes.TryGetValue(id, out var remote) ? remote : null, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            var root = GameObject.Find(NetV2VipPart.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefusesAMissingLocalId()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2VipPart(null));
        }

        [Test]
        public void TheStandingVipGetsAPlaceholderMarkerInTheDefendersColourAboveTheBody()
        {
            var ally = Remote(_allyId, new Vector3(4f, 0f, 2f));

            _part.Apply(Snapshot(_allyId));

            Assert.That(_part.Status, Is.Not.Null);
            Assert.That(_part.MarkerCount, Is.EqualTo(1));
            var marker = _part.MarkerOf(_allyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Anchor, Is.SameAs(ally.transform));
            Assert.That(marker.Target, Is.Null, "a net body has no SideMember");
            Assert.That(marker.Side, Is.EqualTo(Side.Bravo));
            Assert.That(marker.ViewerTeam, Is.EqualTo(Team.Bravo));
            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.IsShown, Is.True);
            Assert.That(marker.transform.parent.name, Is.EqualTo(NetV2VipPart.RootName));

            var body = BodyBounds(ally);
            var expectedY = body.max.y + GuardianLeaderMarker.DefaultHeightAboveHead + (GuardianLeaderMarker.DefaultSize * 0.5f);
            Assert.That(marker.transform.position.y, Is.EqualTo(expectedY).Within(0.001f), "above the top of the body, not of the root");
            Assert.That(_logs, Is.EqualTo(new[] { ClientVipLog.MarkerShown(_allyId) }));
        }

        [Test]
        public void TheGivenVisualIsUsedInsteadOfThePlaceholder()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _created.Add(visual);
            _part.Dispose();
            _part = new NetV2VipPart(() => _ownId, visual, id => _remotes.TryGetValue(id, out var remote) ? remote : null, _logs.Add);
            Remote(_allyId, Vector3.zero);

            _part.Apply(Snapshot(_allyId));

            var marker = _part.MarkerOf(_allyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsPlaceholder, Is.False);
            Assert.That(marker.GetComponentInChildren<Collider>() == null, Is.True, "the marker does not block shots");
        }

        [Test]
        public void TheLocalVipHasNoMarkerOverTheOwnHead()
        {
            Remote(_allyId, Vector3.zero);

            _part.Apply(Snapshot(_localId));

            Assert.That(_part.Status.Vip.IsLocalVip, Is.True);
            Assert.That(_part.MarkerCount, Is.Zero, "the HUD tells the local player");
        }

        [Test]
        public void AFallenVipLosesTheMarker()
        {
            Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));

            _part.Apply(Snapshot(_allyId, fallen: true));

            Assert.That(_part.MarkerCount, Is.Zero);
            Assert.That(_logs, Is.EqualTo(new[]
            {
                ClientVipLog.MarkerShown(_allyId),
                ClientVipLog.MarkerRemoved(_allyId),
            }));
        }

        [Test]
        public void TheMarkerMovesToTheNextRoundsVip()
        {
            Remote(_allyId, Vector3.zero);
            var enemy = Remote(_enemyId, new Vector3(3f, 0f, 0f));
            _part.Apply(Snapshot(_allyId));

            _part.Apply(Snapshot(_enemyId, defender: Team.Alpha));

            Assert.That(_part.MarkerOf(_allyId), Is.Null);
            var marker = _part.MarkerOf(_enemyId);
            Assert.That(marker.Anchor, Is.SameAs(enemy.transform));
            Assert.That(marker.Side, Is.EqualTo(Side.Alpha), "the sides have been swapped");
            Assert.That(_part.MarkerCount, Is.EqualTo(1));
        }

        [Test]
        public void TheSameVipOnTheOtherSideGetsTheMarkerInTheNewColour()
        {
            Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));
            var oldMarker = _part.MarkerOf(_allyId);

            _part.Apply(Snapshot(_allyId, defender: Team.Alpha));

            var marker = _part.MarkerOf(_allyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker, Is.Not.SameAs(oldMarker));
            Assert.That(marker.Side, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void ABodyThatAppearsAfterTheSnapshotGetsTheMarkerOnTheNextTick()
        {
            _part.Apply(Snapshot(_allyId));
            Assert.That(_part.MarkerCount, Is.Zero);

            Remote(_allyId, Vector3.zero);
            _part.Tick(0.016f);

            Assert.That(_part.MarkerOf(_allyId), Is.Not.Null);
        }

        [Test]
        public void ARebuiltBodyGetsTheMarkerAgain()
        {
            var first = Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));
            var oldMarker = _part.MarkerOf(_allyId);

            Object.DestroyImmediate(first.gameObject);
            var second = Remote(_allyId, new Vector3(1f, 0f, 0f));
            _part.Tick(0.016f);

            var marker = _part.MarkerOf(_allyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker, Is.Not.SameAs(oldMarker));
            Assert.That(marker.Anchor, Is.SameAs(second.transform));
            Assert.That(oldMarker == null, Is.True, "the marker of the old body is removed");
        }

        [Test]
        public void AConcealedVipDoesNotGiveItsPlaceAwayWithTheMarker()
        {
            var ally = Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));

            ally.SetConcealed(true);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_allyId).IsShown, Is.False);

            ally.SetConcealed(false);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_allyId).IsShown, Is.True);
        }

        [Test]
        public void AnotherModeTakesTheMarkerAway()
        {
            Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));

            var other = Snapshot(_allyId);
            other.Match.Objective = MatchObjective.TeamDeathmatch;
            _part.Apply(other);

            Assert.That(_part.Status, Is.Null);
            Assert.That(_part.MarkerCount, Is.Zero);
        }

        [Test]
        public void TheLocalIdGivenOnAcceptanceIsReadFromTheNextSnapshot()
        {
            Remote(_allyId, Vector3.zero);
            _ownId = 0;
            _part.Apply(Snapshot(_allyId));
            Assert.That(_part.MarkerOf(_allyId), Is.Not.Null);

            _ownId = _allyId;
            _part.Apply(Snapshot(_allyId));

            Assert.That(_part.MarkerCount, Is.Zero, "the local player is now the vip");
        }

        [Test]
        public void DisposeRemovesTheMarkerAndItsRootAndIgnoresLaterSnapshots()
        {
            Remote(_allyId, Vector3.zero);
            _part.Apply(Snapshot(_allyId));
            var marker = _part.MarkerOf(_allyId);

            _part.Dispose();

            Assert.That(marker == null, Is.True);
            Assert.That(GameObject.Find(NetV2VipPart.RootName), Is.Null);
            Assert.That(_part.Handle(new ChatMessage()), Is.False);

            _part.Apply(Snapshot(_allyId));
            _part.Tick(0.016f);
            Assert.That(_part.MarkerCount, Is.Zero);
        }

        private NetV2RemotePlayer Remote(int playerId, Vector3 position)
        {
            var remote = NetV2RemotePlayer.Create(playerId, _stance, MotorSpec.Default, null);
            remote.transform.position = position;
            _created.Add(remote.gameObject);
            _remotes[playerId] = remote;
            return remote;
        }

        private static Bounds BodyBounds(NetV2RemotePlayer remote)
        {
            var renderers = remote.Body.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var bodyRenderer in renderers)
            {
                bounds.Encapsulate(bodyRenderer.bounds);
            }

            return bounds;
        }

        private SnapshotMessage Snapshot(int vip, Team defender = Team.Bravo, bool fallen = false)
        {
            var snapshot = new SnapshotMessage { Tick = ++_tick };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = MatchObjective.Vip;
            snapshot.Match.HasRound = true;
            snapshot.Match.Round = RoundPhase.Live;
            AddPlayer(snapshot, _localId, Team.Bravo);
            AddPlayer(snapshot, _allyId, Team.Bravo);
            AddPlayer(snapshot, _enemyId, Team.Alpha);
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = 0;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Vip;
            item.Team = defender;
            item.HasOwner = true;
            item.OwnerId = vip;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : vip;
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
        }
    }
}
