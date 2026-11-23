using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update・OnEnable は Edit Mode では走らないので、購読は Listen で、時間は Tick で手で進め、伝える先は ListenersOverride で渡す。
    public sealed class SecurityAlarmTests
    {
        private static readonly Vector3 _spot = new Vector3(0f, 0f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;
        private SecurityAlarm _alarm;

        [SetUp]
        public void SetUp()
        {
            SecurityAlarmBus.ResetOnPlay();
            GameSignals.StealthAlarm.Publish(null);
            GameContent.Use(null);
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _alarm = Track(new GameObject("Alarm")).AddComponent<SecurityAlarm>();
            _alarm.Initialize(new SecurityAlarmSpec(4f, 10f));
            _alarm.ListenersOverride = new List<EnemyStealth>();
        }

        [TearDown]
        public void TearDown()
        {
            _alarm.StopListening();
            SecurityAlarmBus.ResetOnPlay();
            GameSignals.StealthAlarm.Publish(null);
            GameContent.Use(null);
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheBusRingsTheAlarmIntoTheSignalAndItFallsSilentAfterTheSpecSeconds()
        {
            var heard = new List<SecurityAlarmStatus?>();
            GameSignals.StealthAlarm.Changed += heard.Add;
            try
            {
                _alarm.Listen();
                LogAssert.Expect(LogType.Log, SecurityAlarmLog.Started("cam-a", new System.Numerics.Vector3(1f, 0f, 2f), 4f, 10f));

                SecurityAlarmBus.Raise(new Vector3(1f, 0f, 2f), " cam-a ");

                Assert.That(_alarm.Ringing, Is.True);
                Assert.That(
                    GameSignals.StealthAlarm.Value,
                    Is.EqualTo((SecurityAlarmStatus?)new SecurityAlarmStatus(true, 4f, new System.Numerics.Vector3(1f, 0f, 2f), "cam-a")));

                _alarm.Tick(1.5f);
                Assert.That(GameSignals.StealthAlarm.Value.Value.SecondsLeft, Is.EqualTo(2.5f), "the countdown reaches the HUD");

                LogAssert.Expect(LogType.Log, SecurityAlarmLog.Stopped("cam-a", new System.Numerics.Vector3(1f, 0f, 2f)));
                _alarm.Tick(2.5f);

                Assert.That(_alarm.Ringing, Is.False);
                Assert.That(GameSignals.StealthAlarm.Value, Is.Null);
                Assert.That(GameSignals.StealthAlarm.HasValue, Is.False);
                Assert.That(heard, Has.Count.EqualTo(3));
                Assert.That(heard[2], Is.Null, "the last thing published is the silence");

                _alarm.Tick(1f);
                Assert.That(heard, Has.Count.EqualTo(3), "a silent alarm publishes nothing more");
            }
            finally
            {
                GameSignals.StealthAlarm.Changed -= heard.Add;
            }
        }

        [Test]
        public void RaisingWhileRingingRefillsTheSecondsAndMovesTheSpot()
        {
            _alarm.Listen();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            SecurityAlarmBus.Raise(_spot, "cam-a");
            _alarm.Tick(3f);

            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Extended("cam-b", new System.Numerics.Vector3(5f, 0f, 0f), 4f));
            SecurityAlarmBus.Raise(new Vector3(5f, 0f, 0f), "cam-b");

            Assert.That(
                GameSignals.StealthAlarm.Value,
                Is.EqualTo((SecurityAlarmStatus?)new SecurityAlarmStatus(true, 4f, new System.Numerics.Vector3(5f, 0f, 0f), "cam-b")));
        }

        [Test]
        public void OnlyEnemiesWithinTheRadiusComeToInvestigate()
        {
            var near = CreateEnemy("enemy-near", new Vector3(6f, 0f, 0f), Stealth.On);
            var edge = CreateEnemy("enemy-edge", new Vector3(0f, 0f, 10f), Stealth.On);
            var far = CreateEnemy("enemy-far", new Vector3(10.5f, 0f, 0f), Stealth.On);
            _alarm.ListenersOverride = new[] { near, null, far, edge };
            _alarm.Listen();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-near stage=Alerted prev=Unaware "));
            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Received("enemy-near", "cam-a", _spot.ToNumerics(), HearingOutcome.Investigating, Stealth.On));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-edge stage=Alerted prev=Unaware "));
            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Received("enemy-edge", "cam-a", _spot.ToNumerics(), HearingOutcome.Investigating, Stealth.On));

            SecurityAlarmBus.Raise(_spot, "cam-a");

            Assert.That(near.Stage, Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(near.Level, Is.EqualTo(AwarenessSpec.DefaultInvestigateLevel));
            Assert.That(near.Awareness.LastSeen, Is.EqualTo(_spot.ToNumerics()));
            Assert.That(near.Awareness.RaisedByAlarm, Is.False);
            Assert.That(edge.Stage, Is.EqualTo(AwarenessStage.Alerted), "exactly on the radius counts");
            Assert.That(far.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(far.Awareness.LastSeen, Is.Null);
        }

        [Test]
        public void RaiseCountsTheEnemiesSentToInvestigateOnlyWhenTheAlarmStarts()
        {
            var near = CreateEnemy("enemy-near", new Vector3(3f, 0f, 0f), Stealth.On);
            var listeners = new[] { near };
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-near stage=Alerted "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-near security-alarm "));

            Assert.That(_alarm.Raise(_spot, "cam-a", listeners), Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-near stage=Unaware prev=Alerted "));
            near.Awareness.Reset();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a extended "));
            Assert.That(_alarm.Raise(_spot, "cam-a", listeners), Is.Zero, "an alarm already ringing does not call again");
            Assert.That(near.Stage, Is.EqualTo(AwarenessStage.Unaware));
        }

        [Test]
        public void TheDeadAreNotTold()
        {
            var dead = CreateEnemy("enemy-dead", new Vector3(2f, 0f, 0f), Stealth.On);
            dead.GetComponent<Damageable>().TakeDamage(10);
            Assert.That(dead.IsDead, Is.True);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));

            Assert.That(_alarm.Raise(_spot, "cam-a", new[] { dead }), Is.Zero);

            Assert.That(dead.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(dead.Awareness.LastSeen, Is.Null);
            Assert.That(dead.ReceiveSecurityAlarm(Vector3.zero, "cam-a"), Is.EqualTo(HearingOutcome.NotHeard));
        }

        [Test]
        public void WithStealthOffTheEnemyStillSearchesTheSpotWithoutTheGauge()
        {
            var off = CreateEnemy("enemy-off", new Vector3(2f, 0f, 0f), Stealth.Off);
            var spot = new Vector3(1f, 0f, -1f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Received("enemy-off", "cam-a", spot.ToNumerics(), HearingOutcome.Investigating, Stealth.Off));

            Assert.That(_alarm.Raise(spot, "cam-a", new[] { off }), Is.EqualTo(1));

            Assert.That(off.Stage, Is.EqualTo(AwarenessStage.Unaware), "the gauge is not used with stealth off");
            Assert.That(off.Awareness.LastSeen, Is.Null);
            Assert.That(off.GetComponent<EnemyAgent>().Perceive().NoisePosition, Is.EqualTo(spot.ToNumerics()), "the brain searches where the camera saw the player");
        }

        [Test]
        public void AnEngagedEnemyKeepsFightingAndIsNotCounted()
        {
            var engaged = CreateEnemy("enemy-engaged", new Vector3(2f, 0f, 0f), Stealth.On);
            var target = new Vector3(4f, 0f, 4f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-engaged stage=Engaged "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-engaged alarmed by=unknown "));
            engaged.ReceiveAlarm(null, target);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Received("enemy-engaged", "cam-a", _spot.ToNumerics(), HearingOutcome.Engaged, Stealth.On));

            Assert.That(_alarm.Raise(_spot, "cam-a", new[] { engaged }), Is.Zero);

            Assert.That(engaged.Stage, Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(engaged.Awareness.LastSeen, Is.EqualTo(target.ToNumerics()));
        }

        [Test]
        public void StoppingListeningSilencesTheAlarmAndTheBusNoLongerReachesIt()
        {
            var published = 0;
            void Count(SecurityAlarmStatus? _) => published++;
            GameSignals.StealthAlarm.Changed += Count;
            try
            {
                _alarm.Listen();
                _alarm.Listen();
                Assert.That(_alarm.IsListening, Is.True);
                LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
                SecurityAlarmBus.Raise(_spot, "cam-a");
                Assert.That(published, Is.EqualTo(1), "listening twice still hears each alarm once");
            }
            finally
            {
                GameSignals.StealthAlarm.Changed -= Count;
            }

            LogAssert.Expect(LogType.Log, SecurityAlarmLog.Stopped("cam-a", _spot.ToNumerics()));
            _alarm.StopListening();

            Assert.That(_alarm.IsListening, Is.False);
            Assert.That(_alarm.Ringing, Is.False);
            Assert.That(GameSignals.StealthAlarm.Value, Is.Null);

            SecurityAlarmBus.Raise(_spot, "cam-a");
            Assert.That(_alarm.Ringing, Is.False);
            Assert.That(GameSignals.StealthAlarm.Value, Is.Null);
        }

        [Test]
        public void SilencingLeavesASignalPublishedByAnotherAloneAndIgnoresAnUnknownSpot()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            _alarm.Raise(_spot, "cam-a", new EnemyStealth[0]);
            var other = new SecurityAlarmStatus(true, 9f, new System.Numerics.Vector3(7f, 0f, 7f), "cam-other");
            GameSignals.StealthAlarm.Publish(other);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a stopped "));
            _alarm.Silence();

            Assert.That(GameSignals.StealthAlarm.Value, Is.EqualTo((SecurityAlarmStatus?)other));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => _alarm.Raise(new Vector3(float.NaN, 0f, 0f), "cam-a", new EnemyStealth[0]));
            Assert.Throws<System.ArgumentNullException>(() => _alarm.Raise(_spot, "cam-a", null));
            Assert.That(_alarm.Ringing, Is.False);
        }

        [Test]
        public void TheSpecComesFromTheStealthTuningWhenNotGiven()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Alarmed";
            var text = Track(new TextAsset("{ \"alarm\": { \"durationSeconds\": 7, \"radius\": 12 } }") { name = "AlarmedStealth" });
            content.StealthTuning = text;
            GameContent.Use(content);
            var alarm = Track(new GameObject("TunedAlarm")).AddComponent<SecurityAlarm>();

            alarm.EnsureInitialized();

            Assert.That(alarm.State.Spec.DurationSeconds, Is.EqualTo(7f));
            Assert.That(alarm.State.Spec.Radius, Is.EqualTo(12f));
            Assert.Throws<System.InvalidOperationException>(() => alarm.Initialize(SecurityAlarmSpec.Default));
            Assert.Throws<System.ArgumentNullException>(() => Track(new GameObject("Other")).AddComponent<SecurityAlarm>().Initialize(null));
        }

        [Test]
        public void TheCatalogSoundPlaysWhenTheAlarmStartsAndNothingIsAddedWhenItIsEmpty()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            _alarm.Raise(_spot, "cam-a", new EnemyStealth[0]);
            Assert.That(_alarm.ResolveClip(), Is.Null);
            Assert.That(_alarm.Source, Is.Null, "no sound, no AudioSource");
            Assert.That(_alarm.GetComponent<AudioSource>(), Is.Null);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a stopped "));
            _alarm.Silence();

            var catalog = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());
            var clip = Track(AudioClip.Create("Alarm", 441, 1, 44100, false));
            catalog.AlarmClip = clip;
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.SecurityCamera = catalog;
            GameContent.Use(content);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityAlarm\] camera=cam-a raised "));
            _alarm.Raise(_spot, "cam-a", new EnemyStealth[0]);

            Assert.That(_alarm.Source, Is.Not.Null);
            Assert.That(_alarm.Source.clip, Is.SameAs(clip));
            Assert.That(_alarm.Source.spatialBlend, Is.EqualTo(0f));
            Assert.That(_alarm.Source.playOnAwake, Is.False);

            var own = Track(AudioClip.Create("OwnAlarm", 441, 1, 44100, false));
            _alarm.AlarmClip = own;
            Assert.That(_alarm.ResolveClip(), Is.SameAs(own), "the alarm's own clip wins over the catalog");
        }

        [Test]
        public void TheEnemyLineUsesTheStealthTag()
        {
            Assert.That(SecurityAlarmLog.EnemyTag, Is.EqualTo(EnemyStealth.LogTag));
            Assert.That(SecurityAlarm.LogTag, Is.EqualTo(SecurityAlarmLog.Tag));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _alarm.Volume = 1.5f);
            _alarm.Volume = 0.5f;
            Assert.That(_alarm.Volume, Is.EqualTo(0.5f));
        }

        private EnemyStealth CreateEnemy(string id, Vector3 position, Stealth rule)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = id;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = id;
            host.GetComponent<Damageable>().Initialize(id, 10);
            var stealth = host.AddComponent<EnemyStealth>();
            Assert.That(stealth.TryInitialize(), Is.True);
            stealth.Awareness.Rule = rule;

            // 足元は NavMeshAgent の baseOffset だけ根より下にあるので、足元が position に来るように根を置く。
            var feetToRoot = host.transform.position - stealth.GroundPosition;
            host.transform.position = position + feetToRoot;
            Assert.That(stealth.GroundPosition, Is.EqualTo(position));
            return stealth;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
