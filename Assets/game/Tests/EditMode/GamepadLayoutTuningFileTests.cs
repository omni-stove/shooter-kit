using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    // 上書きはアセットに残るので、プロジェクト全体のアクションの写しに当てて確かめる。
    public sealed class GamepadLayoutTuningFileTests
    {
        private static readonly string[] _bundledLayouts = { "default", "standard", "tactical", "hero", "third-person", "squad" };
        private static readonly string[] _bundledSwaps = { "none", "southpaw", "flipped", "southpaw-flipped" };

        private InputActionAsset _asset;
        private PlayerBindings _bindings;
        private GamepadLayoutSpec _spec;

        [SetUp]
        public void SetUp()
        {
            _asset = ProjectActionsClone.Create();
            _bindings = new PlayerBindings(_asset);
            _spec = Bundled();
        }

        [TearDown]
        public void TearDown()
        {
            _bindings.Dispose();
            ProjectActionsClone.Destroy(_asset);
            KitTuning.Reload();
        }

        [Test]
        public void TheKitShipsALayoutForEachKindOfShooterAndTheCommonStickAndTriggerSwaps()
        {
            Assert.That(_spec.Layouts.Select(l => l.Id), Is.EqualTo(_bundledLayouts));
            Assert.That(_spec.Swaps.Select(s => s.Id), Is.EqualTo(_bundledSwaps));
            Assert.That(_spec.Layouts[0].Bindings, Is.Empty, "the first layout is the Input Actions as they are");
            Assert.That(_spec.Swaps[0].Pairs, Is.Empty, "the first swap changes nothing");
        }

        [Test]
        public void EveryActionALayoutNamesHasAGamepadRow()
        {
            foreach (var layout in _spec.Layouts)
            {
                foreach (var binding in layout.Bindings)
                {
                    Assert.That(
                        PlayerBindings.Slots,
                        Has.Member(new RebindSlot(binding.Action, InputDeviceKind.Gamepad)),
                        layout.Id + " names " + binding.Action);
                }
            }
        }

        [Test]
        public void EveryLayoutAndSwapLeavesNoGamepadButtonOverlappingBeyondKeySharing()
        {
            foreach (var layout in _spec.Layouts)
            {
                foreach (var swap in _spec.Swaps)
                {
                    _bindings.ResetAll();
                    _bindings.ApplyGamepadLayout(layout, swap);

                    foreach (var slot in PlayerBindings.Slots.Where(s => s.Device == InputDeviceKind.Gamepad))
                    {
                        var conflicts = _bindings.FindConflicts(slot).Select(c => c.ActionName);
                        Assert.That(conflicts, Is.Empty, layout.Id + " + " + swap.Id + ": " + slot);
                    }
                }
            }
        }

        [Test]
        public void TheNonDefaultLayoutsPlaceEveryGamepadRowSoAPlayerSeesTheWholeLayout()
        {
            var rows = PlayerBindings.Slots.Where(s => s.Device == InputDeviceKind.Gamepad).Select(s => s.ActionName).ToList();
            foreach (var layout in _spec.Layouts.Skip(1))
            {
                var named = new HashSet<string>(layout.Bindings.Select(b => b.Action));
                Assert.That(named, Is.EquivalentTo(rows), layout.Id);
            }
        }

        [TestCase("standard", "Melee", "<Gamepad>/rightStickPress")]
        [TestCase("standard", "Interact", "<Gamepad>/buttonWest")]
        [TestCase("standard", "Next", "<Gamepad>/buttonNorth")]
        [TestCase("tactical", "Prone", "<Gamepad>/dpad/down")]
        [TestCase("tactical", "HoldBreath", "<Gamepad>/rightShoulder")]
        [TestCase("tactical", "CycleMagnification", "<Gamepad>/dpad/left")]
        [TestCase("hero", "Ability1", "<Gamepad>/leftShoulder")]
        [TestCase("hero", "Ability2", "<Gamepad>/rightShoulder")]
        [TestCase("third-person", "Dodge", "<Gamepad>/buttonEast")]
        [TestCase("third-person", "SwapShoulder", "<Gamepad>/dpad/left")]
        [TestCase("squad", "SquadCommand", "<Gamepad>/rightShoulder")]
        [TestCase("squad", "SwitchStreak", "<Gamepad>/dpad/left")]
        public void ALayoutPutsItsActionsOnTheirButtons(string layoutId, string action, string path)
        {
            _bindings.ApplyGamepadLayout(_spec.FindLayout(layoutId), _spec.FindSwap("none"));

            Assert.That(_bindings.PathOf(new RebindSlot(action, InputDeviceKind.Gamepad)), Is.EqualTo(path));
        }

        [Test]
        public void ALayoutTakesTheActionsItHasNoRoomForOffTheGamepad()
        {
            var cover = new RebindSlot("Cover", InputDeviceKind.Gamepad);

            _bindings.ApplyGamepadLayout(_spec.FindLayout("tactical"), _spec.FindSwap("none"));

            Assert.That(_bindings.PathOf(cover), Is.Empty);
            Assert.That(_bindings.IsOverridden(cover), Is.True, "the default RB is taken off by an override, not by editing the asset");
        }

        [Test]
        public void SouthpawSwapsTheSticksForMoveAndLookAndTheirPresses()
        {
            _bindings.ApplyGamepadLayout(_spec.FindLayout("standard"), _spec.FindSwap("southpaw"));
            var map = _asset.FindActionMap(PlayerBindings.MapName);

            Assert.That(GamepadPathOf(map.FindAction("Move")), Is.EqualTo("<Gamepad>/rightStick"));
            Assert.That(GamepadPathOf(map.FindAction("Look")), Is.EqualTo("<Gamepad>/leftStick"));
            Assert.That(_bindings.PathOf(new RebindSlot("Sprint", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightStickPress"));
            Assert.That(_bindings.PathOf(new RebindSlot("Melee", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/leftStickPress"));
            Assert.That(_bindings.PathOf(new RebindSlot("Attack", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightTrigger"), "the triggers stay");
        }

        [Test]
        public void FlippedSwapsTheTriggersWithTheBumpers()
        {
            _bindings.ApplyGamepadLayout(_spec.FindLayout("standard"), _spec.FindSwap("flipped"));

            Assert.That(_bindings.PathOf(new RebindSlot("Attack", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightShoulder"));
            Assert.That(_bindings.PathOf(new RebindSlot("Aim", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/leftShoulder"));
            Assert.That(_bindings.PathOf(new RebindSlot("Grenade", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/leftTrigger"));
            Assert.That(_bindings.PathOf(new RebindSlot("Cover", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightTrigger"));
        }

        [Test]
        public void ALayoutLeavesTheKeyboardAndPauseAlone()
        {
            var reloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
            _bindings.ApplyOverride(reloadKey, "<Keyboard>/m");

            _bindings.ApplyGamepadLayout(_spec.FindLayout("hero"), _spec.FindSwap("southpaw-flipped"));

            Assert.That(_bindings.PathOf(reloadKey), Is.EqualTo("<Keyboard>/m"));
            Assert.That(GamepadPathOf(_asset.FindActionMap(PlayerBindings.MapName).FindAction("Pause")), Is.EqualTo("<Gamepad>/start"));
        }

        [Test]
        public void TheDefaultLayoutWithNoSwapUndoesAnEarlierLayout()
        {
            _bindings.ApplyGamepadLayout(_spec.FindLayout("tactical"), _spec.FindSwap("southpaw"));

            var changed = _bindings.ApplyGamepadLayout(_spec.FindLayout("default"), _spec.FindSwap("none"));

            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(_bindings.HasOverrides, Is.False);
            Assert.That(_bindings.PathOf(new RebindSlot("Cover", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightShoulder"));
        }

        [Test]
        public void ApplyingTheSameLayoutTwiceChangesNothingTheSecondTime()
        {
            var layout = _spec.FindLayout("standard");
            var swap = _spec.FindSwap("flipped");

            Assert.That(_bindings.ApplyGamepadLayout(layout, swap), Is.GreaterThan(0));
            Assert.That(_bindings.ApplyGamepadLayout(layout, swap), Is.EqualTo(0));
        }

        [Test]
        public void TheAppliedLayoutIsFoundUntilAPlayerChangesOneButton()
        {
            Assert.That(_bindings.TryFindGamepadLayout(_spec, out var layout, out var swap), Is.True);
            Assert.That(layout.Id, Is.EqualTo("default"));
            Assert.That(swap.Id, Is.EqualTo("none"));

            _bindings.ApplyGamepadLayout(_spec.FindLayout("squad"), _spec.FindSwap("southpaw"));
            Assert.That(_bindings.TryFindGamepadLayout(_spec, out layout, out swap), Is.True);
            Assert.That(layout.Id, Is.EqualTo("squad"));
            Assert.That(swap.Id, Is.EqualTo("southpaw"));

            _bindings.ApplyOverride(new RebindSlot("Jump", InputDeviceKind.Gamepad), "<Gamepad>/leftShoulder");
            Assert.That(_bindings.TryFindGamepadLayout(_spec, out layout, out swap), Is.False);
            Assert.That(layout, Is.Null);
            Assert.That(swap, Is.Null);
        }

        [Test]
        public void ALayoutKeepsTheStickDeadzoneOverride()
        {
            var look = _asset.FindActionMap(PlayerBindings.MapName).FindAction("Look");
            var index = GamepadIndexOf(look);
            look.ApplyBindingOverride(index, new InputBinding { overrideProcessors = "stickDeadzone(min=0.2)" });

            _bindings.ApplyGamepadLayout(_spec.FindLayout("standard"), _spec.FindSwap("southpaw"));

            Assert.That(look.bindings[index].effectivePath, Is.EqualTo("<Gamepad>/leftStick"));
            Assert.That(look.bindings[index].overrideProcessors, Is.EqualTo("stickDeadzone(min=0.2)"));
        }

        [Test]
        public void TheLayoutSurvivesASaveAndALoad()
        {
            var store = new InputBindingOverridesStore("Shooter.Tests.GamepadLayout." + System.Guid.NewGuid().ToString("N"));
            var restarted = ProjectActionsClone.Create();
            try
            {
                _bindings.ApplyGamepadLayout(_spec.FindLayout("tactical"), _spec.FindSwap("flipped"));
                store.Save(_asset);

                Assert.That(store.Load(restarted), Is.True);
                using (var reloaded = new PlayerBindings(restarted))
                {
                    Assert.That(reloaded.TryFindGamepadLayout(_spec, out var layout, out var swap), Is.True);
                    Assert.That(layout.Id, Is.EqualTo("tactical"));
                    Assert.That(swap.Id, Is.EqualTo("flipped"));
                    Assert.That(reloaded.PathOf(new RebindSlot("Cover", InputDeviceKind.Gamepad)), Is.Empty, "an unbound row stays unbound after a restart");
                }
            }
            finally
            {
                store.Delete();
                ProjectActionsClone.Destroy(restarted);
            }
        }

        [Test]
        public void ALayoutNamingAnActionThePlayerMapLacksIsSkippedWithAWarning()
        {
            var layout = new GamepadLayout("odd", "k", new[]
            {
                new GamepadLayoutBinding("Fly", "<Gamepad>/buttonSouth"),
                new GamepadLayoutBinding("Chat", "<Gamepad>/buttonNorth"),
            });

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, GamepadLayoutLog.UnknownAction("odd", "Fly"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, GamepadLayoutLog.UnknownAction("odd", "Chat"));
            var changed = _bindings.ApplyGamepadLayout(layout, _spec.FindSwap("none"));

            Assert.That(changed, Is.EqualTo(0));
            Assert.That(_bindings.HasOverrides, Is.False);
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.GamepadLayouts.Layouts.Select(l => l.Id), Is.EqualTo(_bundledLayouts));
            Assert.That(KitTuning.GamepadLayoutsResourcePath, Is.EqualTo("Tuning/gamepad-layouts"));
        }

        private static GamepadLayoutSpec Bundled()
        {
            var asset = Resources.Load<TextAsset>(GamepadLayoutJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/gamepad-layouts.json");
            return GamepadLayoutJson.Parse(asset.text, "Resources/Tuning/gamepad-layouts.json");
        }

        private static int GamepadIndexOf(InputAction action)
        {
            var mask = InputBinding.MaskByGroup(PlayerBindings.GamepadGroup);
            for (var i = 0; i < action.bindings.Count; i++)
            {
                if (!action.bindings[i].isComposite && mask.Matches(action.bindings[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GamepadPathOf(InputAction action)
        {
            return action.bindings[GamepadIndexOf(action)].effectivePath;
        }
    }
}
