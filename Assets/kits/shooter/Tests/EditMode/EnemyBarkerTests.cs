using System.Collections.Generic;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyBarkerTests
    {
        private const string SpottedLine = "[Bark] speaker=enemy-1 archetype=Rifleman kind=Spotted hearing=30.00";

        private readonly List<BarkCue> _cues = new List<BarkCue>();
        private readonly List<Object> _created = new List<Object>();
        private GameObject _host;
        private EnemyAgent _agent;
        private EnemyBarker _barker;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            BarkBoard.Use(new BarkSpec());
            BarkEvents.Spoken += Record;

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _barker = _host.AddComponent<EnemyBarker>();
        }

        [TearDown]
        public void TearDown()
        {
            BarkEvents.Spoken -= Record;
            _cues.Clear();
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            BarkBoard.Reset();
        }

        [Test]
        public void PatrolTurningToAttackRaisesSpottedOnce()
        {
            _barker.Catalog = EmptyCatalog();
            StartPatrolling();
            Assert.That(_barker.Tick(10f), Is.Null, "the first look only sets what later looks compare against");

            SeeTargetAhead();
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Attack));

            LogAssert.Expect(LogType.Log, SpottedLine);
            var cue = _barker.Tick(10.1f);

            Assert.That(cue.HasValue, Is.True);
            Assert.That(cue.Value.Kind, Is.EqualTo(BarkKind.Spotted));
            Assert.That(cue.Value.SpeakerId, Is.EqualTo("enemy-1"));
            Assert.That(cue.Value.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(cue.Value.HearingDistance, Is.EqualTo(30f));
            Assert.That(cue.Value.Position, Is.EqualTo(_agent.EyePosition));

            Assert.That(_barker.Tick(10.2f), Is.Null, "still attacking is not a new sighting");
            Assert.That(_barker.Tick(20f), Is.Null);

            Assert.That(_cues.Count, Is.EqualTo(1));
            Assert.That(_cues[0].Kind, Is.EqualTo(BarkKind.Spotted));
            Assert.That(_cues[0].SpeakerId, Is.EqualTo("enemy-1"));
        }

        [Test]
        public void ACatalogWithNoVoicesStillRaisesTheCueWithoutPlaying()
        {
            _barker.Catalog = EmptyCatalog();
            StartPatrolling();
            _barker.Tick(10f);
            SeeTargetAhead();

            LogAssert.Expect(LogType.Log, SpottedLine);
            var cue = _barker.Tick(10.1f);

            Assert.That(cue.HasValue, Is.True);
            Assert.That(cue.Value.Kind, Is.EqualTo(BarkKind.Spotted));
            Assert.That(_cues.Count, Is.EqualTo(1));
            Assert.That(_barker.LastClip, Is.Null);
            Assert.That(_host.GetComponent<AudioSource>(), Is.Null, "nothing is played, so no voice is added");
        }

        [Test]
        public void AKindWithAnEmptyClipListStillRaisesTheCueWithoutPlaying()
        {
            var catalog = EmptyCatalog();
            catalog.SetVoices(new[] { new BarkVoices(BarkKind.Spotted, new AudioClip[] { null }) });
            _barker.Catalog = catalog;
            StartPatrolling();
            _barker.Tick(10f);
            SeeTargetAhead();

            LogAssert.Expect(LogType.Log, SpottedLine);
            var cue = _barker.Tick(10.1f);

            Assert.That(cue.HasValue, Is.True);
            Assert.That(_cues.Count, Is.EqualTo(1));
            Assert.That(_barker.LastClip, Is.Null);
            Assert.That(_host.GetComponent<AudioSource>(), Is.Null);
        }

        [Test]
        public void AVoiceOfTheKindIsPlayedFromAnAddedSourceThatReachesTheHearingDistance()
        {
            var clip = AudioClip.Create("spotted", 441, 1, 44100, false);
            _created.Add(clip);
            var catalog = EmptyCatalog();
            catalog.SetVoices(new[] { new BarkVoices(BarkKind.Spotted, new[] { clip }) });
            _barker.Catalog = catalog;
            StartPatrolling();
            _barker.Tick(10f);
            SeeTargetAhead();

            LogAssert.Expect(LogType.Log, SpottedLine);
            _barker.Tick(10.1f);

            Assert.That(_barker.LastClip, Is.SameAs(clip));
            var voice = _host.GetComponent<AudioSource>();
            Assert.That(voice, Is.Not.Null);
            Assert.That(_barker.Voice, Is.SameAs(voice));
            Assert.That(voice.spatialBlend, Is.EqualTo(1f));
            Assert.That(voice.maxDistance, Is.EqualTo(30f));
            Assert.That(voice.playOnAwake, Is.False);
        }

        [Test]
        public void AttachingDuringAFightDoesNotCallOutTheSighting()
        {
            _barker.Catalog = EmptyCatalog();
            StartPatrolling();
            SeeTargetAhead();
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Attack));

            Assert.That(_barker.Tick(10f), Is.Null);
            Assert.That(_barker.Tick(10.1f), Is.Null);
            Assert.That(_cues, Is.Empty);
        }

        [Test]
        public void AnotherSpeakerIsHeldBackByTheSharedInterval()
        {
            _barker.Catalog = EmptyCatalog();
            StartPatrolling();
            _barker.Tick(10f);
            SeeTargetAhead();
            LogAssert.Expect(LogType.Log, SpottedLine);
            _barker.Tick(10.1f);

            Assert.That(
                BarkBoard.Selector.TryBark("enemy-2", BarkKind.Spotted, 12f, System.Numerics.Vector3.Zero),
                Is.EqualTo(BarkVerdict.KindCooldown),
                "another speaker shares the board, so the 6 s Spotted cooldown holds it back");
            Assert.That(
                BarkBoard.Selector.TryBark("enemy-2", BarkKind.Spotted, 16.5f, System.Numerics.Vector3.Zero),
                Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void AnUninitializedAgentIsNotObserved()
        {
            Assert.That(_agent.IsInitialized, Is.False);
            Assert.That(_barker.Tick(10f), Is.Null);
            Assert.That(_cues, Is.Empty);
        }

        [Test]
        public void PickClipSkipsMissingClips()
        {
            var first = AudioClip.Create("first", 441, 1, 44100, false);
            var second = AudioClip.Create("second", 441, 1, 44100, false);
            _created.Add(first);
            _created.Add(second);
            var clips = new[] { null, first, null, second };

            Assert.That(EnemyBarker.PickClip(clips, count => 0), Is.SameAs(first));
            Assert.That(EnemyBarker.PickClip(clips, count => 1), Is.SameAs(second));
            Assert.That(EnemyBarker.PickClip(clips, count => count + 5), Is.SameAs(second), "a roll past the end uses the last clip");
            Assert.That(EnemyBarker.PickClip(new AudioClip[] { null }, count => 0), Is.Null);
            Assert.That(EnemyBarker.PickClip(new AudioClip[0], count => 0), Is.Null);
            Assert.That(EnemyBarker.PickClip(null, count => 0), Is.Null);
        }

        [Test]
        public void WithoutSidesThePlayerIsHostileAndOtherEnemiesAreAllies()
        {
            var player = new GameObject("Player");
            var other = new GameObject("Other enemy");
            _created.Add(player);
            _created.Add(other);
            other.AddComponent<EnemyAgent>();

            Assert.That(EnemyBarker.IsHostile(_host, player), Is.True);
            Assert.That(EnemyBarker.IsAlly(_host, player), Is.False);
            Assert.That(EnemyBarker.IsHostile(_host, other), Is.False);
            Assert.That(EnemyBarker.IsAlly(_host, other), Is.True);
            Assert.That(EnemyBarker.IsHostile(_host, _host), Is.False, "its own grenade is not a threat");
            Assert.That(EnemyBarker.IsAlly(_host, _host), Is.False);
            Assert.That(EnemyBarker.IsHostile(_host, null), Is.False);
        }

        [Test]
        public void WithSidesTheSideDecides()
        {
            var teammate = new GameObject("Teammate");
            var rival = new GameObject("Rival enemy");
            _created.Add(teammate);
            _created.Add(rival);
            _host.AddComponent<SideMember>().Side = new Side(0);
            teammate.AddComponent<SideMember>().Side = new Side(0);
            rival.AddComponent<EnemyAgent>();
            rival.AddComponent<SideMember>().Side = new Side(1);

            Assert.That(EnemyBarker.IsHostile(_host, teammate), Is.False);
            Assert.That(EnemyBarker.IsAlly(_host, teammate), Is.True);
            Assert.That(EnemyBarker.IsHostile(_host, rival), Is.True, "an enemy on another side is hostile");
            Assert.That(EnemyBarker.IsAlly(_host, rival), Is.False);
        }

        private void Record(BarkCue cue)
        {
            _cues.Add(cue);
        }

        private BarkCatalog EmptyCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<BarkCatalog>();
            _created.Add(catalog);
            return catalog;
        }

        private void StartPatrolling()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) });
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Patrol));
        }

        private void SeeTargetAhead()
        {
            _agent.Brain.Tick(0.1f, new EnemyPerception(
                System.Numerics.Vector3.Zero, new System.Numerics.Vector3(0f, 0f, 5f), true, false, null));
        }
    }
}
