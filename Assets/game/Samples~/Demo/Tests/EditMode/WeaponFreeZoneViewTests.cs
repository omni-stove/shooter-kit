using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/hud.json) から引く。
    public sealed class WeaponFreeZoneViewTests
    {
        private GameObject _host;
        private WeaponFreeZoneView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestWeaponFreeZone");
            _view = _host.AddComponent<WeaponFreeZoneView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Localizer.Apply(GameLanguage.Japanese);
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheViewIsHiddenOutsideAZone()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the weapon-free-warning element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.WeaponFreeZone.Publish(WeaponFreeZoneStatus.Outside);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void WarnOnlyShowsTheWarning()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Text, Is.EqualTo("ここでは武器を出してはいけない 武器をしまえ"));
            Assert.That(Label().text, Is.EqualTo("ここでは武器を出してはいけない 武器をしまえ"));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.HostileClass), Is.False);
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.ForcedClass), Is.False);
        }

        [Test]
        public void WarnThenHostileCountsTheSecondsLeftRoundedUp()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 4.2f));

            Assert.That(_view.Text, Is.EqualTo("武器をしまえ あと 5 秒で敵対される"));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.HostileClass), Is.True);
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.ForcedClass), Is.False);

            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 0.4f));

            Assert.That(_view.Text, Is.EqualTo("武器をしまえ あと 1 秒で敵対される"));
            Assert.That(Label().text, Is.EqualTo("武器をしまえ あと 1 秒で敵対される"));
        }

        [Test]
        public void AWarningReactionWithoutAWarningStaysHidden()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, false, 3f));

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AutoHolsterShowsItsTextWithoutAWarning()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.AutoHolster, false, 0f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo("ここでは武器を出してはいけない 武器をしまう"));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.ForcedClass), Is.True);
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.HostileClass), Is.False);
        }

        [Test]
        public void BlockDrawShowsItsText()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.BlockDraw, false, 0f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo("ここでは武器を構えられない"));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.ForcedClass), Is.True);
        }

        [Test]
        public void HolsteringHidesTheWarningAndDrawingShowsItAgain()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 3f));

            GameSignals.Holster.Publish(new HolsterStatus(HolsterPhase.Holstering, 0.5f, false));

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.HostileClass), Is.False);

            GameSignals.Holster.Publish(new HolsterStatus(HolsterPhase.Holstered, 1f, false));
            Assert.That(_view.IsShown, Is.False);

            GameSignals.Holster.Publish(new HolsterStatus(HolsterPhase.Drawing, 0.8f, true));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo("武器をしまえ あと 3 秒で敵対される"));
        }

        [Test]
        public void NoHolsterSignalCountsAsArmed()
        {
            Assert.That(WeaponFreeZoneText.IsArmed(null), Is.True);
            Assert.That(WeaponFreeZoneText.IsArmed(new HolsterStatus(HolsterPhase.Drawn, 0f, true)), Is.True);
            Assert.That(WeaponFreeZoneText.IsArmed(new HolsterStatus(HolsterPhase.Holstered, 1f, false)), Is.False);
        }

        [Test]
        public void LeavingTheZoneHidesTheView()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.BlockDraw, false, 0f));

            GameSignals.WeaponFreeZone.Publish(null);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(WeaponFreeZoneView.ForcedClass), Is.False);
        }

        [Test]
        public void TheTextFollowsTheLanguage()
        {
            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 2f));

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.Text, Is.EqualTo("Holster your weapon: hostile in 2 s"));
            Assert.That(Label().text, Is.EqualTo("Holster your weapon: hostile in 2 s"));

            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f));
            Assert.That(_view.Text, Is.EqualTo("Weapons are not allowed here. Holster your weapon"));

            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.AutoHolster, false, 0f));
            Assert.That(_view.Text, Is.EqualTo("Weapons are not allowed here. Holstering"));

            GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.BlockDraw, false, 0f));
            Assert.That(_view.Text, Is.EqualTo("Weapons cannot be drawn here"));
        }

        [Test]
        public void AWarningPublishedBeforeBindingIsShownOnBind()
        {
            var other = new GameObject("TestWeaponFreeZoneLate");
            try
            {
                GameSignals.WeaponFreeZone.Publish(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f));
                GameSignals.Holster.Publish(new HolsterStatus(HolsterPhase.Drawn, 0f, true));
                var late = other.AddComponent<WeaponFreeZoneView>();
                late.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(late.IsShown, Is.True);
                Assert.That(late.Text, Is.EqualTo("ここでは武器を出してはいけない 武器をしまえ"));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        private Label Label()
        {
            return _view.Element.Q<Label>(className: WeaponFreeZoneView.TextClass);
        }
    }
}
