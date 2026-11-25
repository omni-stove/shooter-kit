using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate が走らないので、Step を呼んで進める。ViewRig が無いので、狙いは本体の正面。
    public sealed class GrappleAimPublisherTests
    {
        private const float _step = 0.02f;
        private const float _wallFaceZ = 10f;

        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));
        private static readonly GameRules _off = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.Off));

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private PlayerController _mover;
        private AbilityHost _host;
        private GrappleAimPublisher _publisher;
        private GrappleAbilityDefinition _definition;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("GrappleAimPublisherTests.Player");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _mover = _body.AddComponent<PlayerController>();
            var look = _body.AddComponent<FirstPersonLook>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _mover.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));

            new AbilityActionInstaller().Install(_body, _on);
            _host = _body.GetComponent<AbilityHost>();
            _publisher = _body.GetComponent<GrappleAimPublisher>();

            _definition = ScriptableObject.CreateInstance<GrappleAbilityDefinition>();
            _definition.name = "Grapple";
            _definition.Id = "grapple";
            _definition.MaxCharges = 1;
            _definition.RechargeSeconds = 10f;
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            _definition.GrabbableLayers = 1 << 0;
            _created.Add(_definition);

            var floor = PlaceBox("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            floor.layer = 0;
            for (var i = 0; i < 10 && !_mover.IsGrounded; i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(_body, null, null, null, _mover, null, _host, null));
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.LocalPlayer.Clear();
            GameSignals.GrappleAim.Clear();
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheInstallerAddsOnePublisherNextToTheHost()
        {
            new AbilityActionInstaller().Install(_body, _on);

            Assert.That(_body.GetComponents<GrappleAimPublisher>().Length, Is.EqualTo(1));
            Assert.That(_publisher, Is.Not.Null);
            Assert.That(_publisher.Host, Is.SameAs(_host));
        }

        [Test]
        public void WithoutAGrappleNothingIsPublished()
        {
            Assert.That(_publisher.Step(), Is.Null);
            Assert.That(GameSignals.GrappleAim.HasValue, Is.False);
        }

        [Test]
        public void AGrabbableWallAheadCanBeGrappled()
        {
            _host.Equip(1, _definition);
            PlaceWall(0);
            Face(Vector3.forward);

            var status = _publisher.Step();

            Assert.That(status.HasValue, Is.True);
            Assert.That(GameSignals.GrappleAim.Value, Is.EqualTo(status));
            var value = status.Value;
            Assert.That(value.Slot, Is.EqualTo(1));
            Assert.That(value.Miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(value.SlotBlock, Is.EqualTo(AbilityUseBlock.None));
            Assert.That(value.CanGrapple, Is.True);
            Assert.That(value.HasSurface, Is.True);
            Assert.That(value.Distance, Is.EqualTo(_wallFaceZ).Within(1e-3f));
            Assert.That(value.Point.Z, Is.EqualTo(_wallFaceZ).Within(1e-3f));
            Assert.That(value.Normal.Z, Is.EqualTo(-1f).Within(1e-3f));
            Assert.That(value.SnappedToEdge, Is.False);
            Assert.That(_host.Slot(1).Charges, Is.EqualTo(1), "a preview spends nothing");
        }

        [Test]
        public void ASurfaceOnAnotherLayerIsHitButNotGrabbable()
        {
            _host.Equip(0, _definition);
            PlaceWall(1);
            Face(Vector3.forward);

            var value = _publisher.Step().Value;

            Assert.That(value.Miss, Is.EqualTo(GrappleMiss.NotGrabbable));
            Assert.That(value.HasSurface, Is.True);
            Assert.That(value.Distance, Is.EqualTo(_wallFaceZ).Within(1e-3f));
            Assert.That(value.IsGrabbable, Is.False);
            Assert.That(value.CanGrapple, Is.False);
        }

        [Test]
        public void NothingInRangeHasNoSurface()
        {
            _host.Equip(0, _definition);
            Face(Vector3.up);

            var value = _publisher.Step().Value;

            Assert.That(value.Miss, Is.EqualTo(GrappleMiss.OutOfRange));
            Assert.That(value.HasSurface, Is.False);
            Assert.That(value.Distance, Is.EqualTo(0f));
        }

        [Test]
        public void WhileOnTheRopeTheAimIsWithdrawnThenShowsTheSpentSlot()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(Vector3.forward);
            Assert.That(_publisher.Step().Value.CanGrapple, Is.True);

            Assert.That(_host.TryUse(0), Is.True);
            Assert.That(_publisher.Step(), Is.Null);
            Assert.That(GameSignals.GrappleAim.HasValue, Is.False);

            _host.Interrupt(0);
            var value = _publisher.Step().Value;

            Assert.That(value.Miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(value.SlotBlock, Is.EqualTo(AbilityUseBlock.NoCharges));
            Assert.That(value.IsGrabbable, Is.True);
            Assert.That(value.CanGrapple, Is.False);
        }

        [Test]
        public void ABodyThatIsNotTheLocalPlayerPublishesNothing()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(Vector3.forward);
            Assert.That(_publisher.Step().HasValue, Is.True);

            GameSignals.LocalPlayer.Clear();

            Assert.That(_publisher.Step(), Is.Null);
            Assert.That(GameSignals.GrappleAim.HasValue, Is.False);
        }

        [Test]
        public void TurningTheRuleOffWithdrawsTheAim()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(Vector3.forward);
            Assert.That(_publisher.Step().HasValue, Is.True);

            _host.Configure(_off);

            Assert.That(_publisher.Step(), Is.Null);
            Assert.That(GameSignals.GrappleAim.HasValue, Is.False);
        }

        [Test]
        public void WithdrawingLeavesAnotherPublishersValue()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(Vector3.forward);
            _publisher.Step();
            var other = new GrappleAimStatus(1, GrappleMiss.TooClose, AbilityUseBlock.None, false, default, default, 0f, false);
            GameSignals.GrappleAim.Publish(other);

            GameSignals.LocalPlayer.Clear();
            _publisher.Step();

            Assert.That(GameSignals.GrappleAim.Value, Is.EqualTo(other));
        }

        private void PlaceWall(int layer)
        {
            var wall = PlaceBox("Wall", new Vector3(0f, 2.5f, _wallFaceZ + 1.5f), new Vector3(6f, 5f, 3f));
            wall.layer = layer;
            Physics.SyncTransforms();
        }

        private GameObject PlaceBox(string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = center;
            box.transform.localScale = size;
            _created.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        private void Face(Vector3 direction)
        {
            _body.transform.rotation = Quaternion.LookRotation(direction.normalized, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            Physics.SyncTransforms();
        }
    }
}
