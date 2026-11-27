using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class InteractPromptViewTests
    {
        private GameObject _host;
        private InteractPromptView _view;

        private const string Interact = "Player/Interact";

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("Hud");
            _view = _host.AddComponent<InteractPromptView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheLatestOwnerReplacesTheTextAndOnlyItCanHideIt()
        {
            var first = new object();
            var second = new object();

            _view.Show(first, LocalizedText.Literal("first"));
            _view.Show(second, LocalizedText.Literal("second"));
            _view.Hide(first);

            Assert.That(_view.IsShown, Is.True, "another owner's hide leaves the prompt alone");
            Assert.That(_view.Text, Is.EqualTo("second"));
            Assert.That(_view.Owner, Is.SameAs(second));

            _view.Hide(second);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
        }

        [Test]
        public void BlankTextHidesTheOwnersPrompt()
        {
            var owner = new object();
            _view.Show(owner, LocalizedText.Literal("resupply"));

            _view.Show(owner, LocalizedText.Literal("  "));

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void TheCratePromptTextsExistInEveryLanguage()
        {
            Assert.That(Localizer.Resolve(AmmoCratePrompt.Describe(new AmmoCrateHint(true, Interact), "E")), Does.Contain("E"));
            Assert.That(AmmoCratePrompt.Describe(new AmmoCrateHint(false, Interact), "E"), Is.EqualTo(new LocalizedText(AmmoCratePrompt.FullKey)));
            Assert.That(Localizer.Resolve(new LocalizedText(AmmoCratePrompt.FullKey)), Is.Not.EqualTo(AmmoCratePrompt.FullKey));
            Assert.That(AmmoCratePrompt.Describe(new AmmoCrateHint(true, Interact), null), Is.EqualTo(new LocalizedText(AmmoCratePrompt.PromptKey, string.Empty)));
        }

        [Test]
        public void TheVehicleBoardPromptTextsExistInEveryLanguage()
        {
            Assert.That(VehicleBoardPrompt.Describe(new VehicleBoardHint(VehicleSeatRole.Driver, Interact), "E"), Is.EqualTo(new LocalizedText(VehicleBoardPrompt.DriverKey, "E")));
            Assert.That(VehicleBoardPrompt.Describe(new VehicleBoardHint(VehicleSeatRole.Passenger, Interact), null), Is.EqualTo(new LocalizedText(VehicleBoardPrompt.PassengerKey, string.Empty)));
            foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[] { VehicleBoardPrompt.DriverKey, VehicleBoardPrompt.PassengerKey, VehicleBoardPrompt.GunnerKey })
                {
                    Assert.That(table.Contains(key), Is.True, $"{language} has {key}");
                    Assert.That(table.Get(key), Does.Contain("{0}"), $"{language} {key} shows the key");
                }
            }
        }

        [Test]
        public void TheSecurityCameraPromptAsksToHoldForTheTunedSeconds()
        {
            Assert.That(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, Interact), "E"), Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisableKey, "E", "1.5")));
            Assert.That(SecurityCameraPrompt.Describe(new SecurityCameraHint(2f, Interact), null), Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisableKey, string.Empty, "2.0")));
            Assert.That(SecurityCameraPrompt.HoldSeconds(1.04f), Is.EqualTo("1.1"), "rounded up so the hold is never shown shorter than it is");

            Assert.That(Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, Interact), "E")), Is.EqualTo("[E] を 1.5 秒長押しでカメラを止める"));
            Localizer.Apply(GameLanguage.English);
            try
            {
                Assert.That(Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, Interact), "E")), Is.EqualTo("Hold [E] for 1.5 s to disable the camera"));
            }
            finally
            {
                Localizer.Apply(GameLanguage.Japanese);
            }

            foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
            {
                var table = Localizer.TableOf(language);
                Assert.That(table.Contains(SecurityCameraPrompt.DisableKey), Is.True, $"{language} has {SecurityCameraPrompt.DisableKey}");
                Assert.That(table.Get(SecurityCameraPrompt.DisableKey), Does.Contain("{0}").And.Contain("{1}"), $"{language} shows the key and the hold seconds");
            }
        }

        [Test]
        public void TheSecurityCameraPromptShowsTheHoldUnderWayAndAStoppedCamera()
        {
            Assert.That(
                SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0.5f, true, Interact), "E"),
                Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisablingKey, 50)));
            Assert.That(
                SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0f, false, Interact), "E"),
                Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisabledKey)));
            Assert.That(
                SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0.5f, false, Interact), "E"),
                Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisabledKey)),
                "a stopped camera says so even mid-hold");
            Assert.That(SecurityCameraPrompt.ProgressPercent(0.999f), Is.EqualTo(99), "never 100% before the hold ends");
            Assert.That(SecurityCameraPrompt.ProgressPercent(1f), Is.EqualTo(100));

            Assert.That(Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0.5f, true, Interact), "E")), Is.EqualTo("カメラを止めている 50%"));
            Assert.That(Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0f, false, Interact), "E")), Is.EqualTo("カメラは止まっている"));
            Localizer.Apply(GameLanguage.English);
            try
            {
                Assert.That(
                    Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0.5f, true, Interact), "E")),
                    Is.EqualTo("Disabling the camera... 50%"));
                Assert.That(
                    Localizer.Resolve(SecurityCameraPrompt.Describe(new SecurityCameraHint(1.5f, 0f, false, Interact), "E")),
                    Is.EqualTo("The camera is disabled"));
            }
            finally
            {
                Localizer.Apply(GameLanguage.Japanese);
            }
        }

        [Test]
        public void ABoundViewShowsThePublishedSecurityCameraHint()
        {
            var root = BindToLayout();
            var source = new object();

            GameSignals.InteractHint.Publish(new InteractHint(source, new SecurityCameraHint(1.5f, Interact)));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Owner, Is.SameAs(source));
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(SecurityCameraPrompt.DisableKey, _view.HintKeyLabel, "1.5"))));
            Assert.That(root.Q<Label>(InteractPromptView.UxmlName).text, Is.EqualTo(_view.Text));

            GameSignals.InteractHint.Clear();

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void EachHintDetailHasItsOwnText()
        {
            Assert.That(InteractPromptView.TryDescribe(new AmmoCrateHint(true, Interact), "E", out var crate), Is.True);
            Assert.That(crate, Is.EqualTo(new LocalizedText(AmmoCratePrompt.PromptKey, "E")));

            Assert.That(InteractPromptView.TryDescribe(new PickupHint(PickupHintAction.Health, null, null, 0, Interact), "E", out var pickup), Is.True);
            Assert.That(pickup, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, "E")));

            Assert.That(InteractPromptView.TryDescribe(new VehicleBoardHint(VehicleSeatRole.Gunner, Interact), "E", out var board), Is.True);
            Assert.That(board, Is.EqualTo(new LocalizedText(VehicleBoardPrompt.GunnerKey, "E")));

            Assert.That(InteractPromptView.TryDescribe(new SecurityCameraHint(1.5f, Interact), "E", out var camera), Is.True);
            Assert.That(camera, Is.EqualTo(new LocalizedText(SecurityCameraPrompt.DisableKey, "E", "1.5")));

            Assert.That(InteractPromptView.TryDescribe("unknown", "E", out var unknown), Is.False);
            Assert.That(unknown.IsEmpty, Is.True);

            Assert.That(InteractPromptView.InputActionPathOf(new PickupHint(PickupHintAction.Health, null, null, 0, Interact)), Is.EqualTo(Interact));
            Assert.That(InteractPromptView.InputActionPathOf(new AmmoCrateHint(false, "Player/Use")), Is.EqualTo("Player/Use"));
            Assert.That(InteractPromptView.InputActionPathOf(new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Use")), Is.EqualTo("Player/Use"));
            Assert.That(InteractPromptView.InputActionPathOf(new SecurityCameraHint(1.5f, "Player/Use")), Is.EqualTo("Player/Use"));
            Assert.That(InteractPromptView.InputActionPathOf(null), Is.Empty);
        }

        [Test]
        public void ABoundViewShowsThePublishedHintAndHidesItWhenCleared()
        {
            var root = BindToLayout();
            var source = new object();
            var detail = new PickupHint(PickupHintAction.Health, null, null, 0, Interact);

            GameSignals.InteractHint.Publish(new InteractHint(source, detail));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Owner, Is.SameAs(source));
            Assert.That(_view.HintKeyLabel, Is.Not.Null);
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(PickupPrompt.HealthKey, _view.HintKeyLabel))));
            var label = root.Q<Label>(InteractPromptView.UxmlName);
            Assert.That(label.text, Is.EqualTo(_view.Text));
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.InteractHint.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.HintKeyLabel, Is.Null);
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingShowsTheHintAlreadyPublished()
        {
            var source = new object();
            GameSignals.InteractHint.Publish(new InteractHint(source, new AmmoCrateHint(false, Interact)));

            BindToLayout();

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(AmmoCratePrompt.FullKey))));
        }

        [Test]
        public void AHintTheViewCannotDescribeIsNotShown()
        {
            BindToLayout();

            GameSignals.InteractHint.Publish(new InteractHint(new object(), "unknown"));

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void AnUnboundViewStopsFollowingTheSignal()
        {
            BindToLayout();
            _view.Unbind();

            GameSignals.InteractHint.Publish(new InteractHint(new object(), new AmmoCrateHint(false, Interact)));

            Assert.That(_view.IsShown, Is.False);
        }

        private VisualElement BindToLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/kits/shooter/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            var root = layout.CloneTree();
            _view.Bind(root);
            return root;
        }
    }
}
