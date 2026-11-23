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
    public sealed class SecurityAlarmViewTests
    {
        private GameObject _host;
        private SecurityAlarmView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestSecurityAlarm");
            _view = _host.AddComponent<SecurityAlarmView>();
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
        public void TheViewIsHiddenWhileNoAlarmIsPublished()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the security-alarm element");
            Assert.That(_view.Element.name, Is.EqualTo(SecurityAlarmView.UxmlName));
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ARingingAlarmShowsTheTextAndTheSecondsLeft()
        {
            GameSignals.StealthAlarm.Publish(Ringing(10f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Text, Is.EqualTo("警報 カメラに見つかった あと 10 秒"));
            Assert.That(Label().text, Is.EqualTo("警報 カメラに見つかった あと 10 秒"));
        }

        [Test]
        public void TheSecondsLeftAreRoundedUp()
        {
            GameSignals.StealthAlarm.Publish(Ringing(4.2f));
            Assert.That(_view.Text, Is.EqualTo("警報 カメラに見つかった あと 5 秒"));

            GameSignals.StealthAlarm.Publish(Ringing(0.3f));
            Assert.That(_view.Text, Is.EqualTo("警報 カメラに見つかった あと 1 秒"));
            Assert.That(Label().text, Is.EqualTo("警報 カメラに見つかった あと 1 秒"));

            GameSignals.StealthAlarm.Publish(Ringing(0f));
            Assert.That(_view.IsShown, Is.True, "a ringing alarm stays up until it is silenced");
            Assert.That(_view.Text, Is.EqualTo("警報 カメラに見つかった あと 0 秒"));
        }

        [Test]
        public void ClearingTheAlarmHidesTheView()
        {
            GameSignals.StealthAlarm.Publish(Ringing(10f));

            GameSignals.StealthAlarm.Publish(null);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(Label().text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ASilentAlarmIsNotShown()
        {
            GameSignals.StealthAlarm.Publish(Ringing(10f));

            GameSignals.StealthAlarm.Publish(SecurityAlarmStatus.Silent);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheTextFollowsTheLanguage()
        {
            GameSignals.StealthAlarm.Publish(Ringing(7.5f));

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.Text, Is.EqualTo("Alarm! Spotted by a camera: 8 s left"));
            Assert.That(Label().text, Is.EqualTo("Alarm! Spotted by a camera: 8 s left"));
        }

        [Test]
        public void AnAlarmPublishedBeforeBindingIsShownOnBind()
        {
            var other = new GameObject("TestSecurityAlarmLate");
            try
            {
                GameSignals.StealthAlarm.Publish(Ringing(3f));
                var late = other.AddComponent<SecurityAlarmView>();
                late.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(late.IsShown, Is.True);
                Assert.That(late.Text, Is.EqualTo("警報 カメラに見つかった あと 3 秒"));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AnUnboundViewStopsFollowingTheSignal()
        {
            _view.Unbind();

            GameSignals.StealthAlarm.Publish(Ringing(10f));

            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void TheMessageCarriesTheDisplayedSeconds()
        {
            Assert.That(SecurityAlarmText.IsShown(null), Is.False);
            Assert.That(SecurityAlarmText.IsShown(SecurityAlarmStatus.Silent), Is.False);
            Assert.That(SecurityAlarmText.IsShown(Ringing(1f)), Is.True);
            Assert.That(SecurityAlarmText.Message(Ringing(2.1f)), Is.EqualTo(new LocalizedText(SecurityAlarmText.RingingKey, 3)));
            Assert.That(SecurityAlarmText.Message(SecurityAlarmStatus.Silent).IsEmpty, Is.True);
        }

        private static SecurityAlarmStatus Ringing(float secondsLeft)
        {
            return new SecurityAlarmStatus(true, secondsLeft, new System.Numerics.Vector3(1f, 0f, 2f), "cam-a");
        }

        private Label Label()
        {
            return _view.Element.Q<Label>(className: SecurityAlarmView.TextClass);
        }
    }
}
