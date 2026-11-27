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
    public sealed class WeaponLightViewTests
    {
        private GameObject _host;
        private WeaponLightView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestWeaponLight");
            _view = _host.AddComponent<WeaponLightView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/kits/shooter/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
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
        public void TheKeysAreTheHudKeysForOnAndOff()
        {
            Assert.That(WeaponLightText.OnKey, Is.EqualTo("hud.weaponLight.on"));
            Assert.That(WeaponLightText.OffKey, Is.EqualTo("hud.weaponLight.off"));
            Assert.That(WeaponLightText.Message(new WeaponLightStatus(true, true)).Key, Is.EqualTo("hud.weaponLight.on"));
            Assert.That(WeaponLightText.Message(new WeaponLightStatus(true, false)).Key, Is.EqualTo("hud.weaponLight.off"));
        }

        [Test]
        public void AWeaponWithoutALightGivesNoText()
        {
            Assert.That(WeaponLightText.IsShown(null), Is.False);
            Assert.That(WeaponLightText.IsShown(WeaponLightStatus.None), Is.False);
            Assert.That(WeaponLightText.Message(null).IsEmpty, Is.True);
            Assert.That(WeaponLightText.Message(WeaponLightStatus.None).IsEmpty, Is.True);
            Assert.That(WeaponLightText.IsShown(new WeaponLightStatus(true, false)), Is.True);
        }

        [Test]
        public void TheViewIsHiddenWithoutASignal()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the weapon-light element");
            Assert.That(_view.Element.name, Is.EqualTo(WeaponLightView.UxmlName));
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheViewIsHiddenWhileTheWeaponHasNoLight()
        {
            GameSignals.WeaponLight.Publish(WeaponLightStatus.None);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.IsOn, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(Label().text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(WeaponLightView.OnClass), Is.False);
        }

        [Test]
        public void ALightThatIsOffShowsTheOffMark()
        {
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, false));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.IsOn, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Text, Is.EqualTo("ライト 消灯"));
            Assert.That(Label().text, Is.EqualTo("ライト 消灯"));
            Assert.That(_view.Element.ClassListContains(WeaponLightView.OnClass), Is.False);
            Assert.That(Icon(), Is.Not.Null);
        }

        [Test]
        public void TurningTheLightOnShowsTheOnMark()
        {
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, false));

            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, true));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.IsOn, Is.True);
            Assert.That(_view.Text, Is.EqualTo("ライト 点灯"));
            Assert.That(Label().text, Is.EqualTo("ライト 点灯"));
            Assert.That(_view.Element.ClassListContains(WeaponLightView.OnClass), Is.True);

            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, false));

            Assert.That(_view.Text, Is.EqualTo("ライト 消灯"));
            Assert.That(_view.Element.ClassListContains(WeaponLightView.OnClass), Is.False);
        }

        [Test]
        public void SwitchingToAWeaponWithoutALightHidesTheMark()
        {
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, true));

            GameSignals.WeaponLight.Publish(WeaponLightStatus.None);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(WeaponLightView.OnClass), Is.False);

            GameSignals.WeaponLight.Publish(null);
            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void TheTextFollowsTheLanguage()
        {
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, true));

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.Text, Is.EqualTo("Light ON"));
            Assert.That(Label().text, Is.EqualTo("Light ON"));

            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, false));
            Assert.That(_view.Text, Is.EqualTo("Light OFF"));

            Localizer.Apply(GameLanguage.Japanese);
            Assert.That(_view.Text, Is.EqualTo("ライト 消灯"));
        }

        [Test]
        public void AnIconImageReplacesTheDrawnCircle()
        {
            var image = new Texture2D(4, 4);
            try
            {
                Assert.That(Icon().ClassListContains(WeaponLightView.IconImageClass), Is.False);

                _view.IconImage = image;

                Assert.That(Icon().ClassListContains(WeaponLightView.IconImageClass), Is.True);
                Assert.That(Icon().style.backgroundImage.value.texture, Is.SameAs(image));

                _view.IconImage = null;

                Assert.That(Icon().ClassListContains(WeaponLightView.IconImageClass), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void AStatusPublishedBeforeBindingIsShownOnBind()
        {
            var other = new GameObject("TestWeaponLightLate");
            try
            {
                GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, true));
                var late = other.AddComponent<WeaponLightView>();
                late.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(late.IsShown, Is.True);
                Assert.That(late.Text, Is.EqualTo("ライト 点灯"));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void UnbindingStopsFollowingTheSignal()
        {
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, true));
            var element = _view.Element;

            _view.Unbind();
            GameSignals.WeaponLight.Publish(new WeaponLightStatus(true, false));

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(element.ClassListContains(WeaponLightView.OnClass), Is.False);
            Assert.That(element.childCount, Is.EqualTo(0));
        }

        private Label Label()
        {
            return _view.Element.Q<Label>(className: WeaponLightView.TextClass);
        }

        private VisualElement Icon()
        {
            return _view.Element.Q<VisualElement>(className: WeaponLightView.IconClass);
        }
    }
}
