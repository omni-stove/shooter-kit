using System.Linq;
using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 本物の設定とプロジェクト全体のアクションを壊さないよう、保存先はテストごとの別のキー、アクションは写しにする。
    public sealed class SettingsViewBindingsTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);
        private static readonly RebindSlot JumpPad = new RebindSlot("Jump", InputDeviceKind.Gamepad);

        private ControlSettings _appliedBefore;
        private GameLanguage _languageBefore;
        private float _volumeBefore;
        private ControlSettingsStore _store;
        private InputBindingOverridesStore _bindingStore;
        private InputActionAsset _asset;
        private InputActionAsset _restarted;
        private SettingsView _view;
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            _languageBefore = Localizer.Language;

            // 案内の文を日本語で確かめるので、この機械で保存した言語に依らないようにする
            Localizer.Apply(GameLanguage.Japanese);
            var suffix = Guid.NewGuid().ToString("N");
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewBindings.Settings." + suffix);
            _bindingStore = new InputBindingOverridesStore("Shooter.Tests.SettingsViewBindings.Bindings." + suffix);
            _asset = ProjectActionsClone.Create();
            _view = new SettingsView(_store, _bindingStore, _asset);
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_bindingStore.Key);
            PlayerPrefs.Save();
            ProjectActionsClone.Destroy(_asset);
            ProjectActionsClone.Destroy(_restarted);
            _asset = null;
            _restarted = null;
            AppliedControlSettings.Apply(_appliedBefore);
            Localizer.Apply(_languageBefore);
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void OpenBuildsAButtonForEverySlotShowingItsKey()
        {
            _view.Open(_container);

            Assert.That(_view.Bindings, Is.Not.Null);
            Assert.That(_view.Bindings.Asset, Is.SameAs(_asset));
            foreach (var slot in PlayerBindings.Slots)
            {
                var button = _container.Q<Button>(SettingsView.RebindButtonName(slot));
                Assert.That(button, Is.Not.Null, slot.ToString());
                Assert.That(button.text, Is.EqualTo(SettingsView.BindingText(_view.Bindings, slot)), slot.ToString());
                Assert.That(button.text, Is.Not.Empty, slot.ToString());
            }

            Assert.That(_container.Q<Button>(SettingsView.BindingsResetButtonName).enabledSelf, Is.True);
            Assert.That(_container.Q<Label>(SettingsView.RebindNoticeName), Is.Not.Null);
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False, "nothing to tell yet");
            Assert.That(
                SettingsView.RebindButtonName(ReloadKey),
                Is.Not.EqualTo(SettingsView.RebindButtonName(ReloadPad)),
                "each device has its own button");
        }

        [Test]
        public void ChangingReloadFromRToFTakesEffectAtOnceAndIsSavedOnClose()
        {
            _view.Open(_container);
            var reload = _asset.FindAction(InputActionPaths.Reload);

            var result = _view.SetBinding(ReloadKey, "<Keyboard>/m");

            Assert.That(result.Changed, Is.True);
            Assert.That(result.Conflicts, Is.Empty);
            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/m"));
            Assert.That(reload.bindings, Has.None.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/r"));
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"), "the gamepad is separate");
            Assert.That(
                _container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).text,
                Is.EqualTo(_view.Bindings.DisplayOf(ReloadKey)));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).ClassListContains("settings__bind--overridden"), Is.True);
            Assert.That(_view.HasUnsavedChanges, Is.True);
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False, "nothing is written until the view closes");

            Assert.That(_view.Close(), Is.True);

            // 起動し直したときと同じく、上書きの無いアセットに読み込む。
            _restarted = ProjectActionsClone.Create();
            Assert.That(new InputBindingOverridesStore(_bindingStore.Key).Load(_restarted), Is.True);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/m"));
            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "the other settings did not change");
        }

        [Test]
        public void TheSavedBindingIsShownWhenOpenedAgain()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/m");
            _view.Close();

            _view.Open(_container);

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/m"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void AnOverlapIsShownAndCanBeUndone()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/m");

            var result = _view.SetBinding(ReloadKey, "<Keyboard>/g");

            Assert.That(result.Conflicts.Count(), Is.EqualTo(1));
            Assert.That(result.Conflicts[0].ActionName, Is.EqualTo("Grenade"));
            Assert.That(_view.PendingConflict, Is.SameAs(result));
            var notice = _container.Q<Label>(SettingsView.RebindNoticeName);
            Assert.That(IsDisplayed(notice.parent), Is.True);
            Assert.That(notice.text, Does.Contain(InputActionLabels.LabelOf("Grenade")));
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindUndoButtonName)), Is.True);
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindKeepButtonName)), Is.True);
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindCancelButtonName)), Is.False);
            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/g"), "it takes effect until undone");

            Assert.That(_view.UndoRebind(), Is.True);

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/m"), "back to the binding before the overlap");
            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(IsDisplayed(notice.parent), Is.False);
            Assert.That(_view.UndoRebind(), Is.False, "nothing more to undo");
        }

        [Test]
        public void UndoingTheFirstChangeGoesBackToTheDefault()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/g");

            _view.UndoRebind();

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.Bindings.IsOverridden(ReloadKey), Is.False);

            _view.Close();
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False, "no override is left to save");
        }

        [Test]
        public void AnOverlapCanBeKept()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadPad, "<Gamepad>/buttonSouth");

            Assert.That(_view.PendingConflict.Conflicts[0].ActionName, Is.EqualTo("Jump"));
            Assert.That(_view.KeepRebind(), Is.True);

            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False);
        }

        [Test]
        public void ResetBindingsGoesBackToRAndIsSavedOnClose()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/m");
            _view.SetBinding(ReloadPad, "<Gamepad>/buttonNorth");
            _view.Close();
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.True);

            _view.Open(_container);
            _view.ResetBindingsToDefaults();

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).ClassListContains("settings__bind--overridden"), Is.False);
            Assert.That(_view.HasUnsavedChanges, Is.True);

            Assert.That(_view.Close(), Is.True);

            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False);
            _restarted = ProjectActionsClone.Create();
            _bindingStore.Load(_restarted);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
        }

        [Test]
        public void ResetBindingsLeavesTheOtherSettingsAlone()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MouseSensitivity, 250));
            _view.Open(_container);

            _view.ResetBindingsToDefaults();

            Assert.That(AppliedControlSettings.Current.Get(ControlValue.MouseSensitivity), Is.EqualTo(250));
            Assert.That(_view.HasUnsavedChanges, Is.False, "there were no overrides to reset");
        }

        [Test]
        public void WaitingForAnInputShowsItAndCancellingChangesNothing()
        {
            _view.Open(_container);
            var button = _container.Q<Button>(SettingsView.RebindButtonName(ReloadKey));

            Assert.That(_view.StartRebind(ReloadKey), Is.True);

            Assert.That(_view.Bindings.IsRebinding, Is.True);
            Assert.That(button.text, Is.EqualTo(SettingsView.WaitingText));
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindCancelButtonName)), Is.True);
            Assert.That(_container.Q<Label>(SettingsView.RebindNoticeName).text, Does.Contain("Esc"));
            Assert.That(_view.StartRebind(ReloadPad), Is.False, "one at a time");

            _view.CancelRebind();

            Assert.That(_view.Bindings.IsRebinding, Is.False);
            Assert.That(button.text, Is.EqualTo(_view.Bindings.DisplayOf(ReloadKey)));
            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False);
        }

        [Test]
        public void ClosingWhileWaitingStopsWaiting()
        {
            _view.Open(_container);
            var bindings = _view.Bindings;
            _view.StartRebind(ReloadPad);

            Assert.That(_view.Close(), Is.False, "nothing changed");

            Assert.That(bindings.IsRebinding, Is.False);
            Assert.That(PlayerBindings.IsCapturingInput, Is.False);
            Assert.That(bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
        }

        [Test]
        public void ClosingWithAnUndecidedOverlapKeepsIt()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/g");

            Assert.That(_view.Close(), Is.True);

            _restarted = ProjectActionsClone.Create();
            _bindingStore.Load(_restarted);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/g"));
        }

        [Test]
        public void RebindingNeedsAnOpenViewWithAPlayerMap()
        {
            Assert.Throws<InvalidOperationException>(() => _view.StartRebind(ReloadKey));
            Assert.Throws<InvalidOperationException>(() => _view.SetBinding(ReloadKey, "<Keyboard>/m"));
            Assert.Throws<InvalidOperationException>(() => _view.ResetBindingsToDefaults());
            Assert.That(_view.UndoRebind(), Is.False);
            Assert.That(_view.KeepRebind(), Is.False);

            var noPlayerMap = ScriptableObject.CreateInstance<InputActionAsset>();
            noPlayerMap.AddActionMap("UI");
            var view = new SettingsView(_store, _bindingStore, noPlayerMap);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Settings\] .*has no 'Player' action map"));
                view.Open(new VisualElement());

                Assert.That(view.IsOpen, Is.True, "the other settings still work");
                Assert.That(view.Bindings, Is.Null);
                Assert.That(view.Root.Q<Button>(SettingsView.BindingsResetButtonName).enabledSelf, Is.False);
                Assert.Throws<InvalidOperationException>(() => view.StartRebind(ReloadKey));
            }
            finally
            {
                view.Close();
                Object.DestroyImmediate(noPlayerMap);
            }
        }

        [Test]
        public void TheOverlapNoticeNamesEachActionOnce()
        {
            var conflicts = new[]
            {
                new BindingConflict("Move", "up", 2),
                new BindingConflict("Move", "up", 3),
                new BindingConflict("Interact", null, 0),
            };

            Assert.That(SettingsView.FormatConflict("W", conflicts), Is.EqualTo("W は「前へ移動」「調べる・起こす」にも割り当てられています。"));
            Assert.That(SettingsView.FormatConflict("W", Array.Empty<BindingConflict>()), Is.Empty);
        }

        [Test]
        public void AGamepadRowWithNoButtonSaysNone()
        {
            _view.Open(_container);

            var melee = new RebindSlot("Melee", InputDeviceKind.Gamepad);

            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(melee)).text, Is.EqualTo("なし"));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(ReloadPad)).text, Is.EqualTo(_view.Bindings.DisplayOf(ReloadPad)));
        }

        [Test]
        public void TheTemplateButtonsNameTheDefaultTemplateWhenNothingIsChanged()
        {
            _view.Open(_container);

            Assert.That(_container.Q<Button>(SettingsView.GamepadLayoutButtonName).text, Is.EqualTo("既定"));
            Assert.That(_container.Q<Button>(SettingsView.GamepadSwapButtonName).text, Is.EqualTo("そのまま"));
            Assert.That(_container.Q<Button>(SettingsView.GamepadLayoutButtonName).enabledSelf, Is.True);
        }

        [Test]
        public void TheLayoutButtonAppliesTheNextTemplateAtOnceAndItIsSavedOnClose()
        {
            _view.Open(_container);
            var melee = new RebindSlot("Melee", InputDeviceKind.Gamepad);

            Assert.That(_view.NextGamepadLayout(), Is.True);

            Assert.That(_container.Q<Button>(SettingsView.GamepadLayoutButtonName).text, Is.EqualTo("標準 (FPS)"));
            Assert.That(_container.Q<Button>(SettingsView.GamepadSwapButtonName).text, Is.EqualTo("そのまま"));
            Assert.That(_view.Bindings.PathOf(melee), Is.EqualTo("<Gamepad>/rightStickPress"));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(melee)).text, Is.EqualTo(_view.Bindings.DisplayOf(melee)));
            Assert.That(_view.HasUnsavedChanges, Is.True);
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False, "nothing is written until the view closes");

            Assert.That(_view.Close(), Is.True);

            _restarted = ProjectActionsClone.Create();
            Assert.That(new InputBindingOverridesStore(_bindingStore.Key).Load(_restarted), Is.True);
            using (var reloaded = new PlayerBindings(_restarted))
            {
                Assert.That(reloaded.TryFindGamepadLayout(KitTuning.GamepadLayouts, out var layout, out var swap), Is.True);
                Assert.That(layout.Id, Is.EqualTo("standard"));
                Assert.That(swap.Id, Is.EqualTo("none"));
            }
        }

        [Test]
        public void TheSwapButtonKeepsTheLayout()
        {
            _view.Open(_container);
            _view.NextGamepadLayout();

            Assert.That(_view.NextGamepadSwap(), Is.True);

            Assert.That(_view.TryGetGamepadLayout(out var layout, out var swap), Is.True);
            Assert.That(layout.Id, Is.EqualTo("standard"));
            Assert.That(swap.Id, Is.EqualTo("southpaw"));
            Assert.That(_container.Q<Button>(SettingsView.GamepadSwapButtonName).text, Is.EqualTo("左右スティック入れ替え"));
            Assert.That(_view.Bindings.PathOf(new RebindSlot("Sprint", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/rightStickPress"));
        }

        [Test]
        public void TheLayoutButtonGoesRoundBackToTheDefault()
        {
            _view.Open(_container);
            var count = _view.GamepadLayouts.Layouts.Count;
            Assert.That(count, Is.EqualTo(6));

            for (var i = 0; i < count; i++)
            {
                _view.NextGamepadLayout();
            }

            Assert.That(_view.TryGetGamepadLayout(out var layout, out _), Is.True);
            Assert.That(layout.Id, Is.EqualTo("default"));
            Assert.That(_view.Bindings.HasOverrides, Is.False);
        }

        [Test]
        public void ChangingOneButtonShowsCustomAndTheSwapButtonStartsAgainFromTheLastTemplate()
        {
            _view.Open(_container);
            _view.NextGamepadLayout();
            _view.SetBinding(JumpPad, "<Gamepad>/dpad/down");

            Assert.That(_view.TryGetGamepadLayout(out _, out _), Is.False);
            Assert.That(_container.Q<Button>(SettingsView.GamepadLayoutButtonName).text, Is.EqualTo("カスタム"));
            Assert.That(_container.Q<Button>(SettingsView.GamepadSwapButtonName).text, Is.EqualTo("カスタム"));
            Assert.That(_view.PendingConflict, Is.Not.Null, "Dodge is on the d-pad down in the standard layout");

            _view.NextGamepadSwap();

            Assert.That(_view.TryGetGamepadLayout(out var layout, out var swap), Is.True);
            Assert.That(layout.Id, Is.EqualTo("standard"), "the layout the player started from");
            Assert.That(swap.Id, Is.EqualTo("none"), "a custom set starts again from the first swap");
            Assert.That(_view.PendingConflict, Is.Null, "applying a template settles the overlap");
            Assert.That(_view.Bindings.PathOf(JumpPad), Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void ResettingTheBindingsGoesBackToTheDefaultTemplate()
        {
            _view.Open(_container);
            _view.NextGamepadLayout();

            _view.ResetBindingsToDefaults();

            Assert.That(_container.Q<Button>(SettingsView.GamepadLayoutButtonName).text, Is.EqualTo("既定"));
        }

        [Test]
        public void EveryBundledTemplateNameIsInEveryLanguage()
        {
            var keys = KitTuning.GamepadLayouts.Layouts.Select(l => l.NameKey)
                .Concat(KitTuning.GamepadLayouts.Swaps.Select(s => s.NameKey))
                .Concat(new[] { SettingsView.UnboundTextKey, SettingsView.CustomLayoutTextKey })
                .ToList();
            Assert.That(keys, Has.Count.EqualTo(12));

            foreach (var language in Localizer.AvailableLanguages())
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }

        private static bool IsDisplayed(VisualElement element)
        {
            return element != null && element.style.display.value != DisplayStyle.None;
        }
    }
}
