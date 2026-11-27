using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BreachPromptTests
    {
        private const string Interact = "Player/Interact";
        private const string ChargeId = "00000000-0000-4000-8000-0000000000b1";
        private const string UnknownChargeId = "00000000-0000-4000-8000-0000000000b2";

        private static readonly string[] _allKeys =
        {
            BreachPrompt.KickOrPlantKey, BreachPrompt.KickKey, BreachPrompt.PlantKey, BreachPrompt.KickNoChargeKey,
            BreachPrompt.NoChargeKey, BreachPrompt.PlantingKey, BreachPrompt.ArmedKey,
        };

        private static readonly ItemCatalog _items = new ItemCatalog(new[] { new ItemSpec(ChargeId, "item.breachCharge") }, InventoryRule.Default);

        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            GameSignals.ResetAll();
        }

        [Test]
        public void ADoorThatCanBeKickedAndChargedShowsBothThePressAndTheHold()
        {
            Assert.That(BreachPrompt.TryDescribe(new BreachHint(true, true, ChargeId, Interact), "E", _items, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(BreachPrompt.KickOrPlantKey, "E")));

            foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
            {
                var line = Localizer.TableOf(language).Get(BreachPrompt.KickOrPlantKey);
                var first = line.IndexOf("{0}", System.StringComparison.Ordinal);
                Assert.That(first, Is.GreaterThanOrEqualTo(0), $"{language} shows the key for the press");
                Assert.That(line.IndexOf("{0}", first + 3, System.StringComparison.Ordinal), Is.GreaterThan(first), $"{language} shows the key for the hold");
            }

            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(text), Is.EqualTo("[E] 蹴破る / [E] 長押し: 爆薬を仕掛ける"));
            Assert.That(Localizer.TableOf(GameLanguage.English).Resolve(text), Is.EqualTo("[E] Kick open / Hold [E] Plant charge"));
        }

        [Test]
        public void ADoorThatCanOnlyBeKickedOrOnlyChargedShowsThatOne()
        {
            Assert.That(BreachPrompt.TryDescribe(new BreachHint(true, false, null, Interact), "E", _items, out var kick), Is.True);
            Assert.That(kick, Is.EqualTo(new LocalizedText(BreachPrompt.KickKey, "E")));

            Assert.That(BreachPrompt.TryDescribe(new BreachHint(false, true, null, Interact), "E", _items, out var plant), Is.True);
            Assert.That(plant, Is.EqualTo(new LocalizedText(BreachPrompt.PlantKey, "E")));
        }

        [Test]
        public void AMissingChargeNamesTheItemFromTheCatalog()
        {
            Assert.That(BreachPrompt.TryDescribe(new BreachHint(true, false, ChargeId, Interact), "E", _items, out var kick), Is.True);
            Assert.That(kick, Is.EqualTo(new LocalizedText(BreachPrompt.KickNoChargeKey, "E", new LocalizedText("item.breachCharge"))));

            Assert.That(BreachPrompt.TryDescribe(new BreachHint(false, false, ChargeId, Interact), "E", _items, out var none), Is.True);
            Assert.That(none, Is.EqualTo(new LocalizedText(BreachPrompt.NoChargeKey, new LocalizedText("item.breachCharge"))));
        }

        [Test]
        public void AChargeMissingFromTheCatalogIsNamedByItsId()
        {
            Assert.That(BreachPrompt.ChargeName(UnknownChargeId, _items), Is.EqualTo(LocalizedText.Literal(UnknownChargeId)));
            Assert.That(BreachPrompt.ChargeName(ChargeId, null), Is.EqualTo(LocalizedText.Literal(ChargeId)));
        }

        [Test]
        public void NothingIsShownWhenTheDoorCanBeNeitherKickedNorCharged()
        {
            Assert.That(BreachPrompt.TryDescribe(new BreachHint(false, false, null, Interact), "E", _items, out var text), Is.False);
            Assert.That(text.IsEmpty, Is.True);
        }

        [Test]
        public void ANullKeyLabelBecomesEmpty()
        {
            Assert.That(BreachPrompt.TryDescribe(new BreachHint(true, false, null, Interact), null, _items, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(BreachPrompt.KickKey, string.Empty)));
        }

        [Test]
        public void PlantingShowsTheFlooredPercentAndArmingShowsTheSecondsRoundedUp()
        {
            Assert.That(BreachPrompt.TryDescribeProgress(new BreachStatus(BreachDoorPhase.Planting, 0.999f, 0.01f), out var planting), Is.True);
            Assert.That(planting, Is.EqualTo(new LocalizedText(BreachPrompt.PlantingKey, 99)));

            Assert.That(BreachPrompt.TryDescribeProgress(new BreachStatus(BreachDoorPhase.Armed, 0.5f, 1.2f), out var armed), Is.True);
            Assert.That(armed, Is.EqualTo(new LocalizedText(BreachPrompt.ArmedKey, 2)));

            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(planting), Is.EqualTo("爆薬を仕掛けている 99%"));
            Assert.That(Localizer.TableOf(GameLanguage.English).Resolve(armed), Is.EqualTo("Charge blows in 2 s"));
        }

        [Test]
        public void NoProgressIsShownWhenNothingIsPlantedOrTheDoorIsAlreadyOpen()
        {
            Assert.That(BreachPrompt.TryDescribeProgress(null, out var none), Is.False);
            Assert.That(none.IsEmpty, Is.True);
            Assert.That(BreachPrompt.TryDescribeProgress(new BreachStatus(BreachDoorPhase.Closed, 0f, 0f), out _), Is.False);
            Assert.That(BreachPrompt.TryDescribeProgress(new BreachStatus(BreachDoorPhase.Opened, 1f, 0f), out _), Is.False);
            Assert.That(BreachPrompt.TryDescribeProgress(new BreachStatus(BreachDoorPhase.Destroyed, 1f, 0f), out _), Is.False);
        }

        [Test]
        public void EveryBreachTextExistsInEnglishAndJapaneseWithTheSamePlaceholders()
        {
            var english = Localizer.TableOf(GameLanguage.English);
            var japanese = Localizer.TableOf(GameLanguage.Japanese);
            foreach (var key in _allKeys)
            {
                Assert.That(english.Contains(key), Is.True, $"English has {key}");
                Assert.That(japanese.Contains(key), Is.True, $"Japanese has {key}");
                foreach (var placeholder in new[] { "{0}", "{1}" })
                {
                    Assert.That(japanese.Get(key).Contains(placeholder), Is.EqualTo(english.Get(key).Contains(placeholder)), $"{key} {placeholder}");
                }
            }

            Assert.That(english.Get(BreachPrompt.KickNoChargeKey), Does.Contain("{1}"));
        }

        [Test]
        public void ThePromptViewDescribesABreachHintAndReadsItsInputAction()
        {
            var hint = new BreachHint(true, false, null, "Player/Use");

            Assert.That(InteractPromptView.TryDescribe(hint, "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(BreachPrompt.KickKey, "E")));
            Assert.That(InteractPromptView.InputActionPathOf(hint), Is.EqualTo("Player/Use"));
        }

        [Test]
        public void TheProgressCoversTheDoorHintAndGivesItBackWhenItEnds()
        {
            var view = BindView();
            var door = new object();
            GameSignals.InteractHint.Publish(new InteractHint(door, new BreachHint(true, true, null, Interact)));
            var hintText = view.Text;
            Assert.That(view.Owner, Is.SameAs(door));

            GameSignals.Breach.Publish(new BreachStatus(BreachDoorPhase.Planting, 0.25f, 1.5f));

            Assert.That(view.IsShowingBreachProgress, Is.True);
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(BreachPrompt.PlantingKey, 25))));

            GameSignals.InteractHint.Publish(new InteractHint(door, new BreachHint(true, false, null, Interact)));
            Assert.That(view.IsShowingBreachProgress, Is.True, "a new door hint waits under the progress");

            GameSignals.Breach.Clear();

            Assert.That(view.IsShowingBreachProgress, Is.False);
            Assert.That(view.Owner, Is.SameAs(door));
            Assert.That(view.Text, Is.Not.EqualTo(hintText), "the latest door hint comes back");
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(BreachPrompt.KickKey, view.HintKeyLabel))));
        }

        [Test]
        public void TheProgressAloneHidesWhenItEnds()
        {
            var view = BindView();

            GameSignals.Breach.Publish(new BreachStatus(BreachDoorPhase.Armed, 0.5f, 3f));
            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(BreachPrompt.ArmedKey, 3))));

            GameSignals.InteractHint.Clear();
            Assert.That(view.IsShowingBreachProgress, Is.True, "clearing an absent door hint leaves the progress");

            GameSignals.Breach.Publish(new BreachStatus(BreachDoorPhase.Destroyed, 1f, 0f));
            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void BindingShowsTheProgressAlreadyPublished()
        {
            GameSignals.Breach.Publish(new BreachStatus(BreachDoorPhase.Planting, 0.5f, 1f));

            var view = BindView();

            Assert.That(view.IsShowingBreachProgress, Is.True);
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(BreachPrompt.PlantingKey, 50))));
        }

        private InteractPromptView BindView()
        {
            _host = new GameObject("Hud");
            var view = _host.AddComponent<InteractPromptView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/kits/shooter/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            view.Bind(layout.CloneTree());
            return view;
        }
    }
}
