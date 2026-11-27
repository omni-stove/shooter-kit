using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BarkJsonTests
    {
        private const string FullJson = @"{
  ""kindCooldownSeconds"": {
    ""spotted"": 1.5,
    ""lostTarget"": 2.5,
    ""reloading"": 3.5,
    ""grenade"": 0.5,
    ""takingFire"": 4.5,
    ""movingToCover"": 5.5,
    ""allyDown"": 9
  },
  ""speakerCooldownSeconds"": 2,
  ""minIntervalSeconds"": 0.25,
  ""crowdRadius"": 20,
  ""maxSpeakersInCrowd"": 3,
  ""hearingDistance"": 45,
  ""grenadeWarningRadius"": 6.5,
  ""speakSeconds"": 2.25
}";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            KitTuning.Reload();
        }

        [Test]
        public void EveryValueIsRead()
        {
            var spec = BarkJson.Parse(FullJson, "barks.json");

            Assert.That(spec.CooldownSecondsFor(BarkKind.Spotted), Is.EqualTo(1.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.LostTarget), Is.EqualTo(2.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Reloading), Is.EqualTo(3.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Grenade), Is.EqualTo(0.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.TakingFire), Is.EqualTo(4.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.MovingToCover), Is.EqualTo(5.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.AllyDown), Is.EqualTo(9f));
            Assert.That(spec.SpeakerCooldownSeconds, Is.EqualTo(2f));
            Assert.That(spec.MinIntervalSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.CrowdRadius, Is.EqualTo(20f));
            Assert.That(spec.MaxSpeakersInCrowd, Is.EqualTo(3));
            Assert.That(spec.HearingDistance, Is.EqualTo(45f));
            Assert.That(spec.GrenadeWarningRadius, Is.EqualTo(6.5f));
            Assert.That(spec.SpeakSeconds, Is.EqualTo(2.25f));
        }

        [Test]
        public void MissingValuesTakeTheDefaults()
        {
            var spec = BarkJson.Parse(
                "{\"kindCooldownSeconds\": {\"grenade\": 1}, \"hearingDistance\": 50, \"maxSpeakersInCrowd\": 1}", "barks.json");

            Assert.That(spec.CooldownSecondsFor(BarkKind.Grenade), Is.EqualTo(1f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Spotted), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.LostTarget), Is.EqualTo(8f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Reloading), Is.EqualTo(4f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.TakingFire), Is.EqualTo(5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.MovingToCover), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.AllyDown), Is.EqualTo(8f));
            Assert.That(spec.HearingDistance, Is.EqualTo(50f));
            Assert.That(spec.MaxSpeakersInCrowd, Is.EqualTo(1));
            Assert.That(spec.SpeakerCooldownSeconds, Is.EqualTo(3f));
            Assert.That(spec.MinIntervalSeconds, Is.EqualTo(0.75f));
            Assert.That(spec.CrowdRadius, Is.EqualTo(12f));
            Assert.That(spec.GrenadeWarningRadius, Is.EqualTo(8f));
            Assert.That(spec.SpeakSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void AnEmptyObjectTakesEveryDefault()
        {
            var spec = BarkJson.Parse("{}", "barks.json");

            Assert.That(spec.CooldownSecondsFor(BarkKind.Spotted), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.LostTarget), Is.EqualTo(8f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Reloading), Is.EqualTo(4f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Grenade), Is.EqualTo(3f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.TakingFire), Is.EqualTo(5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.MovingToCover), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.AllyDown), Is.EqualTo(8f));
            Assert.That(spec.SpeakerCooldownSeconds, Is.EqualTo(3f));
            Assert.That(spec.MinIntervalSeconds, Is.EqualTo(0.75f));
            Assert.That(spec.CrowdRadius, Is.EqualTo(12f));
            Assert.That(spec.MaxSpeakersInCrowd, Is.EqualTo(2));
            Assert.That(spec.HearingDistance, Is.EqualTo(30f));
            Assert.That(spec.GrenadeWarningRadius, Is.EqualTo(8f));
            Assert.That(spec.SpeakSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void AnEmptyKindTableKeepsEveryKindDefault()
        {
            var spec = BarkJson.Parse("{\"kindCooldownSeconds\": {}}", "barks.json");

            foreach (var kind in BarkSpec.Kinds)
            {
                Assert.That(spec.CooldownSecondsFor(kind), Is.EqualTo(BarkSpec.DefaultCooldownSecondsFor(kind)), kind.ToString());
            }
        }

        [Test]
        public void TheDefaultsMatchTheDefaultConstants()
        {
            Assert.That(BarkSpec.DefaultSpottedCooldownSeconds, Is.EqualTo(6f));
            Assert.That(BarkSpec.DefaultLostTargetCooldownSeconds, Is.EqualTo(8f));
            Assert.That(BarkSpec.DefaultReloadingCooldownSeconds, Is.EqualTo(4f));
            Assert.That(BarkSpec.DefaultGrenadeCooldownSeconds, Is.EqualTo(3f));
            Assert.That(BarkSpec.DefaultTakingFireCooldownSeconds, Is.EqualTo(5f));
            Assert.That(BarkSpec.DefaultMovingToCoverCooldownSeconds, Is.EqualTo(6f));
            Assert.That(BarkSpec.DefaultAllyDownCooldownSeconds, Is.EqualTo(8f));
            Assert.That(BarkSpec.DefaultSpeakerCooldownSeconds, Is.EqualTo(3f));
            Assert.That(BarkSpec.DefaultMinIntervalSeconds, Is.EqualTo(0.75f));
            Assert.That(BarkSpec.DefaultCrowdRadius, Is.EqualTo(12f));
            Assert.That(BarkSpec.DefaultMaxSpeakersInCrowd, Is.EqualTo(2));
            Assert.That(BarkSpec.DefaultHearingDistance, Is.EqualTo(30f));
            Assert.That(BarkSpec.DefaultGrenadeWarningRadius, Is.EqualTo(8f));
            Assert.That(BarkSpec.DefaultSpeakSeconds, Is.EqualTo(2f));
            Assert.That(BarkSpec.Kinds, Is.EqualTo(new[]
            {
                BarkKind.Spotted, BarkKind.LostTarget, BarkKind.Reloading, BarkKind.Grenade,
                BarkKind.TakingFire, BarkKind.MovingToCover, BarkKind.AllyDown,
            }));
        }

        [Test]
        public void TheBundledFileHoldsTheShippedValues()
        {
            var asset = Resources.Load<TextAsset>(BarkJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/barks.json");

            var spec = BarkJson.Parse(asset.text, "Resources/Tuning/barks.json");

            Assert.That(spec.CooldownSecondsFor(BarkKind.Spotted), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.LostTarget), Is.EqualTo(8f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Reloading), Is.EqualTo(4f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Grenade), Is.EqualTo(3f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.TakingFire), Is.EqualTo(5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.MovingToCover), Is.EqualTo(6f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.AllyDown), Is.EqualTo(8f));
            Assert.That(spec.SpeakerCooldownSeconds, Is.EqualTo(3f));
            Assert.That(spec.MinIntervalSeconds, Is.EqualTo(0.75f));
            Assert.That(spec.CrowdRadius, Is.EqualTo(12f));
            Assert.That(spec.MaxSpeakersInCrowd, Is.EqualTo(2));
            Assert.That(spec.HearingDistance, Is.EqualTo(30f));
            Assert.That(spec.GrenadeWarningRadius, Is.EqualTo(8f));
            Assert.That(spec.SpeakSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.BarksResourcePath, Is.EqualTo("Tuning/barks"));
            Assert.That(KitTuning.Barks.HearingDistance, Is.EqualTo(30f));
            Assert.That(KitTuning.Barks.CooldownSecondsFor(BarkKind.Grenade), Is.EqualTo(3f));
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Barks";
            content.BarkTuning = Track(new TextAsset("{\"hearingDistance\": 12.5, \"kindCooldownSeconds\": {\"allyDown\": 1}}") { name = "barks" });
            GameContent.Use(content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Barks' (barks)");
            var spec = KitTuning.Barks;

            Assert.That(spec.HearingDistance, Is.EqualTo(12.5f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.AllyDown), Is.EqualTo(1f));
            Assert.That(spec.CooldownSecondsFor(BarkKind.Spotted), Is.EqualTo(6f));
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Broken";
            content.BarkTuning = Track(new TextAsset("{\"hearingDistance\": 0}") { name = "barks" });
            GameContent.Use(content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (barks)");
            LogAssert.Expect(LogType.Error, new Regex(@"(?s)^\[Tuning\] bark JSON .* item 'hearingDistance' is invalid: .*; using the default values instead$"));

            Assert.That(KitTuning.Barks, Is.SameAs(BarkSpec.Default));
        }

        [TestCase("{\"speakerCooldownSeconds\": -1}", "speakerCooldownSeconds")]
        [TestCase("{\"minIntervalSeconds\": -0.5}", "minIntervalSeconds")]
        [TestCase("{\"crowdRadius\": -1}", "crowdRadius")]
        [TestCase("{\"maxSpeakersInCrowd\": 0}", "maxSpeakersInCrowd")]
        [TestCase("{\"hearingDistance\": 0}", "hearingDistance")]
        [TestCase("{\"grenadeWarningRadius\": -2}", "grenadeWarningRadius")]
        [TestCase("{\"speakSeconds\": -1}", "speakSeconds")]
        [TestCase("{\"kindCooldownSeconds\": {\"reloading\": -1}}", "kindCooldownSeconds.reloading")]
        public void AnOutOfRangeValueIsRejectedWithItsItem(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => BarkJson.Parse(json, "barks.json"));

            Assert.That(e.SourceName, Is.EqualTo("barks.json"));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.StartWith("bark JSON 'barks.json' item '" + item + "' is invalid: "));
        }

        [TestCase("{\"hearing\": 10}", "hearing")]
        [TestCase("{\"kindCooldownSeconds\": {\"Spotted\": 1}}", "kindCooldownSeconds.Spotted")]
        [TestCase("{\"maxSpeakersInCrowd\": 1.5}", "maxSpeakersInCrowd")]
        [TestCase("{\"crowdRadius\": \"far\"}", "crowdRadius")]
        [TestCase("{\"kindCooldownSeconds\": 3}", "kindCooldownSeconds")]
        [TestCase("{\"kindCooldownSeconds\": {\"spotted\": null}}", "kindCooldownSeconds.spotted")]
        public void AMalformedItemIsRejectedWithItsItem(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => BarkJson.Parse(json, "barks.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void BrokenTextIsRejected()
        {
            Assert.Throws<TuningJsonException>(() => BarkJson.Parse("{\"hearingDistance\": ", "barks.json"));
            Assert.Throws<ArgumentNullException>(() => BarkJson.Parse(null, "barks.json"));
        }

        [Test]
        public void EveryKindHasItsOwnItemName()
        {
            Assert.That(BarkJson.KindItemNames, Is.EqualTo(new[]
            {
                "spotted", "lostTarget", "reloading", "grenade", "takingFire", "movingToCover", "allyDown",
            }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BarkJson.KindItem((BarkKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => BarkSpec.Default.CooldownSecondsFor((BarkKind)99));
        }

        [Test]
        public void TheLogLinesCarryTheBarkTag()
        {
            Assert.That(
                BarkLog.Spoken("enemy-1", EnemyArchetype.Sniper, BarkKind.Reloading, 30f),
                Is.EqualTo("[Bark] speaker=enemy-1 archetype=Sniper kind=Reloading hearing=30.00"));
            Assert.That(BarkLog.Skipped(null, BarkKind.Spotted, "speaker-cooldown"), Is.EqualTo("[Bark] speaker=- kind=Spotted skipped reason=speaker-cooldown"));
            Assert.That(BarkLog.NoClip(BarkKind.AllyDown), Is.EqualTo("[Bark] kind=AllyDown has no voice clip; nothing is played"));
            Assert.That(
                BarkLog.Loaded(BarkSpec.Default),
                Is.EqualTo("[Bark] spec kinds=Spotted:6.00,LostTarget:8.00,Reloading:4.00,Grenade:3.00,TakingFire:5.00,MovingToCover:6.00,AllyDown:8.00"
                    + " speaker=3.00 interval=0.75 crowdRadius=12.00 crowdMax=2 hearing=30.00 grenadeWarning=8.00 speak=2.00"));
            Assert.Throws<ArgumentNullException>(() => BarkLog.Loaded(null));
        }

        [Test]
        public void ASpokenCueReachesSubscribersUntilTheyAreCleared()
        {
            var heard = new List<BarkCue>();
            Action<BarkCue> listener = heard.Add;
            BarkEvents.Spoken += listener;
            try
            {
                BarkEvents.Raise(new BarkCue("enemy-2", EnemyArchetype.Rifleman, BarkKind.Grenade, new Vector3(1f, 0f, 2f), 25f));

                Assert.That(heard, Has.Count.EqualTo(1));
                Assert.That(heard[0].SpeakerId, Is.EqualTo("enemy-2"));
                Assert.That(heard[0].Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
                Assert.That(heard[0].Kind, Is.EqualTo(BarkKind.Grenade));
                Assert.That(heard[0].Position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
                Assert.That(heard[0].HearingDistance, Is.EqualTo(25f));
                Assert.That(heard[0].IsHeardAt(new Vector3(1f, 0f, 27f)), Is.True);
                Assert.That(heard[0].IsHeardAt(new Vector3(1f, 0f, 27.5f)), Is.False);

                CombatEvents.ClearSubscribers();
                BarkEvents.Raise(new BarkCue("enemy-2", EnemyArchetype.Rifleman, BarkKind.Grenade, Vector3.zero, 25f));

                Assert.That(heard, Has.Count.EqualTo(1), "CombatEvents.ClearSubscribers also clears the bark listeners");
            }
            finally
            {
                BarkEvents.Spoken -= listener;
            }
        }

        [Test]
        public void ACueRejectsAnUnknownKindOrNoHearingDistance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkCue("a", EnemyArchetype.Rifleman, (BarkKind)99, Vector3.zero, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkCue("a", EnemyArchetype.Rifleman, BarkKind.Spotted, Vector3.zero, 0f));
            Assert.That(new BarkCue(null, EnemyArchetype.Rifleman, BarkKind.Spotted, Vector3.zero, 10f).SpeakerId, Is.Empty);
            Assert.That(default(BarkCue).SpeakerId, Is.Empty);
        }

        [Test]
        public void TheCatalogComesFromTheEntryAndGivesEachKindItsClips()
        {
            Assert.That(BarkCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(BarkCatalog.CatalogName)));
            Assert.That(BarkCatalog.ResourcePath, Is.EqualTo("Presentation/BarkCatalog"));
            Assert.That(BarkCatalog.ClipsFor(null, BarkKind.Spotted), Is.Empty);

            var clip = Track(AudioClip.Create("spotted", 1, 1, 8000, false));
            var catalog = Track(ScriptableObject.CreateInstance<BarkCatalog>());
            catalog.SetVoices(new[] { new BarkVoices(BarkKind.Spotted, new[] { clip }) });
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.Barks = catalog;
            GameContent.Use(content);

            Assert.That(BarkCatalog.Load(), Is.SameAs(catalog));
            Assert.That(catalog.ClipsFor(BarkKind.Spotted), Is.EqualTo(new[] { clip }));
            Assert.That(catalog.ClipsFor(BarkKind.AllyDown), Is.Empty);
            Assert.DoesNotThrow(catalog.Validate);

            catalog.SetVoices(new[] { new BarkVoices(BarkKind.Spotted, new[] { clip }), new BarkVoices(BarkKind.Spotted, new AudioClip[] { null }) });
            var e = Assert.Throws<PresentationCatalogException>(catalog.Validate);
            Assert.That(e.Message, Does.Contain("voice #1 (Spotted) uses a kind already used by another entry"));
            Assert.That(e.Message, Does.Contain("voice #1 (Spotted) clip #0 is missing"));
            Assert.That(catalog.ClipsFor(BarkKind.Spotted), Is.EqualTo(new[] { clip }), "the first entry of a kind is used");
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
