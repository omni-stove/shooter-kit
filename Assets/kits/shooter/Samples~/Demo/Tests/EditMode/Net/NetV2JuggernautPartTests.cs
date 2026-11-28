using System;
using System.Collections.Generic;
using GameKit.Core;
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
    public sealed class NetV2JuggernautPartTests
    {
        private const int _localId = 1;
        private const int _otherId = 2;
        private const int _thirdId = 3;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private readonly Dictionary<int, NetV2RemotePlayer> _remotes = new Dictionary<int, NetV2RemotePlayer>();
        private int _ownId;
        private int _tick;
        private NetV2JuggernautPart _part;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _remotes.Clear();
            _ownId = _localId;
            _tick = 0;
            _part = new NetV2JuggernautPart(() => _ownId, null, id => _remotes.TryGetValue(id, out var remote) ? remote : null, _logs.Add);
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
            var root = GameObject.Find(NetV2JuggernautPart.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefusesAMissingLocalId()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2JuggernautPart(null));
        }

        [Test]
        public void TheStandingJuggernautGetsAPlaceholderMarkerAboveTheBody()
        {
            var other = Remote(_otherId, new Vector3(4f, 0f, 2f));

            _part.Apply(Snapshot(_otherId));

            Assert.That(_part.Status, Is.Not.Null);
            Assert.That(_part.MarkerCount, Is.EqualTo(1));
            var marker = _part.MarkerOf(_otherId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Anchor, Is.SameAs(other.transform));
            Assert.That(marker.Target, Is.Null, "a net body has no SideMember");
            Assert.That(marker.Side, Is.EqualTo(new Side(_otherId)));
            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.IsShown, Is.True);
            Assert.That(marker.transform.parent.name, Is.EqualTo(NetV2JuggernautPart.RootName));

            var body = BodyBounds(other);
            var expectedY = body.max.y + GuardianLeaderMarker.DefaultHeightAboveHead + (GuardianLeaderMarker.DefaultSize * 0.5f);
            Assert.That(marker.transform.position.y, Is.EqualTo(expectedY).Within(0.001f), "above the top of the body, not of the root");
            Assert.That(_logs, Is.EqualTo(new[] { ClientJuggernautLog.MarkerShown(_otherId) }));
        }

        [Test]
        public void TheGivenVisualIsUsedInsteadOfThePlaceholder()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _created.Add(visual);
            _part.Dispose();
            _part = new NetV2JuggernautPart(() => _ownId, visual, id => _remotes.TryGetValue(id, out var remote) ? remote : null, _logs.Add);
            Remote(_otherId, Vector3.zero);

            _part.Apply(Snapshot(_otherId));

            var marker = _part.MarkerOf(_otherId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsPlaceholder, Is.False);
            Assert.That(marker.GetComponentInChildren<Collider>() == null, Is.True, "the marker does not block shots");
        }

        [Test]
        public void TheLocalJuggernautHasNoMarkerOverTheOwnHead()
        {
            Remote(_otherId, Vector3.zero);

            _part.Apply(Snapshot(_localId));

            Assert.That(_part.Status.Juggernaut.IsLocalPlayerJuggernaut, Is.True);
            Assert.That(_part.MarkerCount, Is.Zero, "the HUD tells the local player");
        }

        [Test]
        public void AFallenJuggernautLosesTheMarker()
        {
            Remote(_otherId, Vector3.zero);
            _part.Apply(Snapshot(_otherId));

            _part.Apply(Snapshot(_otherId, fallen: true));

            Assert.That(_part.MarkerCount, Is.Zero);
            Assert.That(_logs, Is.EqualTo(new[]
            {
                ClientJuggernautLog.MarkerShown(_otherId),
                ClientJuggernautLog.MarkerRemoved(_otherId),
            }));
        }

        [Test]
        public void TheMarkerMovesToTheNewJuggernaut()
        {
            Remote(_otherId, Vector3.zero);
            var third = Remote(_thirdId, new Vector3(3f, 0f, 0f));
            _part.Apply(Snapshot(_otherId));

            _part.Apply(Snapshot(_thirdId));

            Assert.That(_part.MarkerOf(_otherId), Is.Null);
            Assert.That(_part.MarkerOf(_thirdId).Anchor, Is.SameAs(third.transform));
            Assert.That(_part.MarkerCount, Is.EqualTo(1));
        }

        [Test]
        public void ABodyThatAppearsAfterTheSnapshotGetsTheMarkerOnTheNextTick()
        {
            _part.Apply(Snapshot(_otherId));
            Assert.That(_part.MarkerCount, Is.Zero);

            Remote(_otherId, Vector3.zero);
            _part.Tick(0.016f);

            Assert.That(_part.MarkerOf(_otherId), Is.Not.Null);
        }

        [Test]
        public void ARebuiltBodyGetsTheMarkerAgain()
        {
            var first = Remote(_otherId, Vector3.zero);
            _part.Apply(Snapshot(_otherId));
            var oldMarker = _part.MarkerOf(_otherId);

            Object.DestroyImmediate(first.gameObject);
            var second = Remote(_otherId, new Vector3(1f, 0f, 0f));
            _part.Tick(0.016f);

            var marker = _part.MarkerOf(_otherId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker, Is.Not.SameAs(oldMarker));
            Assert.That(marker.Anchor, Is.SameAs(second.transform));
            Assert.That(oldMarker == null, Is.True, "the marker of the old body is removed");
        }

        [Test]
        public void AConcealedJuggernautDoesNotGiveItsPlaceAwayWithTheMarker()
        {
            var other = Remote(_otherId, Vector3.zero);
            _part.Apply(Snapshot(_otherId));

            other.SetConcealed(true);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_otherId).IsShown, Is.False);

            other.SetConcealed(false);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_otherId).IsShown, Is.True);
        }

        [Test]
        public void AnotherModeTakesTheMarkerAway()
        {
            Remote(_otherId, Vector3.zero);
            _part.Apply(Snapshot(_otherId));

            var other = Snapshot(_otherId);
            other.Match.Objective = MatchObjective.FreeForAll;
            _part.Apply(other);

            Assert.That(_part.Status, Is.Null);
            Assert.That(_part.MarkerCount, Is.Zero);
        }

        [Test]
        public void TheLocalIdGivenOnAcceptanceIsReadFromTheNextSnapshot()
        {
            Remote(_otherId, Vector3.zero);
            _ownId = 0;
            _part.Apply(Snapshot(_otherId));
            Assert.That(_part.MarkerOf(_otherId), Is.Not.Null);

            _ownId = _otherId;
            _part.Apply(Snapshot(_otherId));

            Assert.That(_part.MarkerCount, Is.Zero, "the local player is now the juggernaut");
        }

        [Test]
        public void DisposeRemovesTheMarkerAndItsRootAndIgnoresLaterSnapshots()
        {
            Remote(_otherId, Vector3.zero);
            _part.Apply(Snapshot(_otherId));
            var marker = _part.MarkerOf(_otherId);

            _part.Dispose();

            Assert.That(marker == null, Is.True);
            Assert.That(GameObject.Find(NetV2JuggernautPart.RootName), Is.Null);
            Assert.That(_part.Handle(new ChatMessage()), Is.False);

            _part.Apply(Snapshot(_otherId));
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

        private SnapshotMessage Snapshot(int juggernaut, bool fallen = false)
        {
            var snapshot = new SnapshotMessage { Tick = ++_tick };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = MatchObjective.Juggernaut;
            AddPlayer(snapshot, _localId);
            AddPlayer(snapshot, _otherId);
            AddPlayer(snapshot, _thirdId);
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = 0;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Juggernaut;
            item.HasOwner = true;
            item.OwnerId = juggernaut;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : juggernaut;
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = Team.Alpha;
            row.Health = 100;
        }
    }
}
