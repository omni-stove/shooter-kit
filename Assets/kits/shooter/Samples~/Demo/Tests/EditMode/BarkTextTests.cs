using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/barks.json) から引く。
    // Edit Mode では MonoBehaviour の OnEnable が走らず BarkEvents を購読しないので、SubtitleView.Show を直接呼ぶ。
    public sealed class BarkTextTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            AppliedControlSettings.Apply(_appliedBefore);
        }

        [TestCase(BarkKind.Spotted, "敵兵: 見つけたぞ！")]
        [TestCase(BarkKind.LostTarget, "敵兵: 見失った、探せ！")]
        [TestCase(BarkKind.Reloading, "敵兵: 装填する、援護しろ！")]
        [TestCase(BarkKind.Grenade, "敵兵: 手榴弾だ、伏せろ！")]
        [TestCase(BarkKind.TakingFire, "敵兵: 撃たれてる！")]
        [TestCase(BarkKind.MovingToCover, "敵兵: 遮蔽に移る！")]
        [TestCase(BarkKind.AllyDown, "敵兵: 仲間がやられた！")]
        public void EachKindBecomesItsLine(BarkKind kind, string expected)
        {
            Assert.That(BarkText.Line(Cue("enemy-1", EnemyArchetype.Rifleman, kind, Vector3.zero, 30f)), Is.EqualTo(expected));
        }

        [TestCase(EnemyArchetype.Rifleman, "敵兵: 見つけたぞ！")]
        [TestCase(EnemyArchetype.Shield, "盾兵: 見つけたぞ！")]
        [TestCase(EnemyArchetype.Sniper, "狙撃兵: 見つけたぞ！")]
        [TestCase(EnemyArchetype.Charger, "突撃兵: 見つけたぞ！")]
        [TestCase(EnemyArchetype.Bomber, "爆弾兵: 見つけたぞ！")]
        public void EachArchetypeIsNamedAsTheSpeaker(EnemyArchetype archetype, string expected)
        {
            Assert.That(BarkText.Line(archetype, BarkKind.Spotted), Is.EqualTo(expected));
        }

        [Test]
        public void TheKeysFollowTheKindAndArchetypeNames()
        {
            Assert.That(BarkText.KindKey(BarkKind.Grenade), Is.EqualTo("bark.Grenade"));
            Assert.That(BarkText.SpeakerKey(EnemyArchetype.Sniper), Is.EqualTo("bark.speaker.Sniper"));
            Assert.Throws<ArgumentOutOfRangeException>(() => BarkText.KindKey((BarkKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => BarkText.SpeakerKey((EnemyArchetype)99));
        }

        [Test]
        public void EveryKindAndArchetypeHasTextInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                Assert.That(table.Contains(BarkText.LineKey), Is.True, language + " " + BarkText.LineKey);
                foreach (BarkKind kind in Enum.GetValues(typeof(BarkKind)))
                {
                    Assert.That(table.Contains(BarkText.KindKey(kind)), Is.True, language + " " + kind);
                }

                foreach (EnemyArchetype archetype in Enum.GetValues(typeof(EnemyArchetype)))
                {
                    Assert.That(table.Contains(BarkText.SpeakerKey(archetype)), Is.True, language + " " + archetype);
                }
            }
        }

        [Test]
        public void TheEnglishAndJapaneseFilesHaveTheSameKeys()
        {
            var japanese = Keys(GameLanguage.Japanese);
            var english = Keys(GameLanguage.English);

            Assert.That(english, Is.EquivalentTo(japanese));
            Assert.That(japanese, Has.Count.EqualTo(1 + Enum.GetValues(typeof(BarkKind)).Length + Enum.GetValues(typeof(EnemyArchetype)).Length));
        }

        [Test]
        public void TheEnglishLineReadsAsASubtitle()
        {
            var table = Localizer.TableOf(GameLanguage.English);

            Assert.That(
                table.Format(BarkText.LineKey, table.Get(BarkText.SpeakerKey(EnemyArchetype.Rifleman)), table.Get(BarkText.KindKey(BarkKind.Grenade))),
                Is.EqualTo("Enemy: Grenade! Get down!"));
        }

        [Test]
        public void ABarkWithinHearingBecomesTheSpeakersLine()
        {
            var heard = BarkText.TrySpeech(Cue("enemy-1", EnemyArchetype.Shield, BarkKind.TakingFire, new Vector3(0f, 0f, 30f), 30f), Vector3.zero, out var speech);

            Assert.That(heard, Is.True, "exactly at the hearing distance is still heard");
            Assert.That(speech.Speaker, Is.EqualTo("enemy-1"));
            Assert.That(speech.Text, Is.EqualTo("盾兵: 撃たれてる！"));
        }

        [Test]
        public void ABarkFartherThanItsHearingDistanceIsNotShown()
        {
            var heard = BarkText.TrySpeech(Cue("enemy-1", EnemyArchetype.Rifleman, BarkKind.Spotted, new Vector3(0f, 0f, 30.5f), 30f), Vector3.zero, out var speech);

            Assert.That(heard, Is.False);
            Assert.That(speech.Speaker, Is.Null);
            Assert.That(speech.Text, Is.Null);
        }

        [Test]
        public void ABarkIsShownWhenNoOneIsListening()
        {
            var heard = BarkText.TrySpeech(Cue("enemy-1", EnemyArchetype.Rifleman, BarkKind.Spotted, new Vector3(0f, 0f, 500f), 30f), null, out var speech);

            Assert.That(heard, Is.True);
            Assert.That(speech.Text, Is.EqualTo("敵兵: 見つけたぞ！"));
        }

        [Test]
        public void ABarkWithoutASpeakerIdSharesItsArchetypesLine()
        {
            var speech = BarkText.Speech(Cue(null, EnemyArchetype.Bomber, BarkKind.Grenade, Vector3.zero, 30f));

            Assert.That(speech.Speaker, Is.EqualTo("bark.speaker.Bomber"));
            Assert.That(speech.Text, Is.EqualTo("爆弾兵: 手榴弾だ、伏せろ！"));
        }

        [Test]
        public void TheSubtitlesShowNearBarksAndLeaveOutFarOnes()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithShowSubtitles(false));
            var view = CreateBoundView(new Vector3(0f, 1.6f, 0f));

            view.Show(Cue("enemy-far", EnemyArchetype.Sniper, BarkKind.Spotted, new Vector3(0f, 1.6f, 61f), 60f));
            Assert.That(view.Lines, Is.Empty, "a bark beyond its hearing distance from the camera is not shown");

            view.Show(Cue("enemy-1", EnemyArchetype.Rifleman, BarkKind.Spotted, new Vector3(5f, 0f, 5f), 30f));
            view.Show(Cue("enemy-2", EnemyArchetype.Rifleman, BarkKind.Reloading, new Vector3(-5f, 0f, 5f), 30f));
            Assert.That(Texts(view), Is.EqualTo(new[] { "敵兵: 見つけたぞ！", "敵兵: 装填する、援護しろ！" }), "shown even with sound subtitles off");

            view.Show(Cue("enemy-1", EnemyArchetype.Rifleman, BarkKind.MovingToCover, new Vector3(5f, 0f, 5f), 30f));
            Assert.That(Texts(view), Is.EqualTo(new[] { "敵兵: 遮蔽に移る！", "敵兵: 装填する、援護しろ！" }), "the same soldier stays on one line");
        }

        private static BarkCue Cue(string speakerId, EnemyArchetype archetype, BarkKind kind, Vector3 position, float hearingDistance)
        {
            return new BarkCue(speakerId, archetype, kind, position, hearingDistance);
        }

        private static List<string> Keys(GameLanguage language)
        {
            var path = Localizer.LayerFolderOf(language) + "/barks";
            var asset = Resources.Load<TextAsset>(path);
            Assert.That(asset, Is.Not.Null, "Resources/" + path + ".json ships with the Demo");
            Assert.That(StringTable.TryParse(asset.text, null, out var table, out var error), Is.True, error);
            return table.Keys.ToList();
        }

        private SubtitleView CreateBoundView(Vector3 viewerPosition)
        {
            var gameObject = new GameObject(nameof(SubtitleView));
            _created.Add(gameObject);
            var view = gameObject.AddComponent<SubtitleView>();

            var viewer = new GameObject("viewer");
            _created.Add(viewer);
            viewer.transform.SetPositionAndRotation(viewerPosition, Quaternion.identity);
            view.Viewer = viewer.transform;

            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/kits/shooter/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            view.Bind(layout.CloneTree());
            return view;
        }

        private static string[] Texts(SubtitleView view)
        {
            return view.Element.Query<Label>(className: SubtitleView.LineClass).ToList().Select(label => label.text).ToArray();
        }
    }
}
