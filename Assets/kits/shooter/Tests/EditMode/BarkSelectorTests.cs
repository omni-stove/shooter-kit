using System;
using NUnit.Framework;
using Shooter.Core;
using Vector3 = System.Numerics.Vector3;

namespace Shooter.Tests.EditMode
{
    // 既定値は kind: Spotted 6・LostTarget 8・Reloading 4・Grenade 3・TakingFire 5・MovingToCover 6・AllyDown 8、
    // 話し手 3・全体 0.75・半径 12・上限 2・話す長さ 2 (秒・m)。
    public sealed class BarkSelectorTests
    {
        private static readonly Vector3 Origin = Vector3.Zero;

        private static readonly Vector3 Far = new Vector3(100f, 0f, 0f);

        [Test]
        public void TheFirstBarkIsAllowed()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void TheSameSpeakerWithinItsCooldownIsSpeakerCooldown()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 1f, Origin), Is.EqualTo(BarkVerdict.SpeakerCooldown));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 2.5f, Origin), Is.EqualTo(BarkVerdict.SpeakerCooldown));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 3f, Origin), Is.EqualTo(BarkVerdict.Allowed), "3 s after the last bark the speaker may talk again");
        }

        [Test]
        public void TheBundledFileGivesTheSameSpeakerCooldown()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(BarkJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/barks.json");
            var selector = new BarkSelector(BarkJson.Parse(asset.text, "Resources/Tuning/barks.json"));

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 1f, Origin), Is.EqualTo(BarkVerdict.SpeakerCooldown));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 3f, Origin), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void TheSameKindWithinItsCooldownIsKindCooldownEvenFromAnotherSpeaker()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Spotted, 5.5f, Far), Is.EqualTo(BarkVerdict.KindCooldown));
            Assert.That(selector.TryBark("b", BarkKind.Spotted, 6f, Far), Is.EqualTo(BarkVerdict.Allowed), "6 s after the last Spotted it may be said again");
        }

        [Test]
        public void EachKindUsesItsOwnCooldown()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Grenade, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Grenade, 2.5f, Far), Is.EqualTo(BarkVerdict.KindCooldown));
            Assert.That(selector.TryBark("b", BarkKind.Grenade, 3f, Far), Is.EqualTo(BarkVerdict.Allowed), "Grenade waits 3 s, not the 6 s of Spotted");
        }

        [Test]
        public void TheKindCooldownIsCheckedBeforeTheSpeakerCooldown()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0.5f, Origin), Is.EqualTo(BarkVerdict.KindCooldown));
        }

        [Test]
        public void AnyBarkWithinTheMinimumIntervalIsGlobalInterval()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.5f, Far), Is.EqualTo(BarkVerdict.GlobalInterval));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.75f, Far), Is.EqualTo(BarkVerdict.Allowed), "0.75 s after any bark another may start");
        }

        [Test]
        public void TooManySpeakersInTheRadiusIsTooManyNearbyUntilOneStops()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.75f, new Vector3(5f, 0f, 0f)), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.CountSpeakingNear(new Vector3(0f, 0f, 5f), 1.5f), Is.EqualTo(2));
            Assert.That(selector.TryBark("c", BarkKind.TakingFire, 1.5f, new Vector3(0f, 0f, 5f)), Is.EqualTo(BarkVerdict.TooManyNearby));

            // a は 0 s から 2 s 話すので、2 s で数から外れる。
            Assert.That(selector.CountSpeakingNear(new Vector3(0f, 0f, 5f), 2f), Is.EqualTo(1));
            Assert.That(selector.TryBark("c", BarkKind.TakingFire, 2f, new Vector3(0f, 0f, 5f)), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void SpeakersOutsideTheRadiusAreNotCounted()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.75f, Origin), Is.EqualTo(BarkVerdict.Allowed));

            Assert.That(selector.CountSpeakingNear(new Vector3(0f, 0f, 12.5f), 1.5f), Is.EqualTo(0));
            Assert.That(selector.TryBark("c", BarkKind.TakingFire, 1.5f, new Vector3(0f, 0f, 12.5f)), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void SpeakersExactlyOnTheRadiusAreCounted()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.75f, Origin), Is.EqualTo(BarkVerdict.Allowed));

            Assert.That(selector.CountSpeakingNear(new Vector3(0f, 0f, 12f), 1.5f), Is.EqualTo(2));
            Assert.That(selector.TryBark("c", BarkKind.TakingFire, 1.5f, new Vector3(0f, 0f, 12f)), Is.EqualTo(BarkVerdict.TooManyNearby));
        }

        [Test]
        public void ABlockedBarkIsNotRecorded()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 1f, Origin), Is.EqualTo(BarkVerdict.SpeakerCooldown));

            Assert.That(selector.TryBark("b", BarkKind.Reloading, 1f, Far), Is.EqualTo(BarkVerdict.Allowed), "the blocked Reloading did not start its kind cooldown");
            Assert.That(selector.TryBark("a", BarkKind.MovingToCover, 3f, Origin), Is.EqualTo(BarkVerdict.Allowed), "the blocked bark did not extend a's cooldown");
        }

        [Test]
        public void TheConfiguredValuesAreUsed()
        {
            var spec = new BarkSpec(speakerCooldownSeconds: 0f, minIntervalSeconds: 0f, maxSpeakersInCrowd: 1, speakSeconds: 1f);
            var selector = new BarkSelector(spec);

            Assert.That(selector.Spec, Is.SameAs(spec));
            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0.5f, Origin), Is.EqualTo(BarkVerdict.TooManyNearby));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 1f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", BarkKind.Grenade, 1f, Far), Is.EqualTo(BarkVerdict.Allowed), "with no speaker cooldown and b moved away, b may talk again at once");
        }

        [Test]
        public void ZeroSpeakSecondsNeverCountsAnyone()
        {
            var selector = new BarkSelector(new BarkSpec(minIntervalSeconds: 0f, maxSpeakersInCrowd: 1, speakSeconds: 0f));

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.CountSpeakingNear(Origin, 0f), Is.EqualTo(0));
            Assert.That(selector.TryBark("b", BarkKind.Reloading, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void ForgetDropsTheSpeakerRecordButKeepsTheKindAndIntervalRecords()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            selector.Forget("a");

            Assert.That(selector.CountSpeakingNear(Origin, 0.5f), Is.EqualTo(0));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 0.5f, Origin), Is.EqualTo(BarkVerdict.GlobalInterval));
            Assert.That(selector.TryBark("a", BarkKind.Spotted, 1f, Origin), Is.EqualTo(BarkVerdict.KindCooldown));
            Assert.That(selector.TryBark("a", BarkKind.Reloading, 1f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.DoesNotThrow(() => selector.Forget("never-spoke"));
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var selector = new BarkSelector(BarkSpec.Default);

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed));
            selector.Clear();

            Assert.That(selector.CountSpeakingNear(Origin, 0.1f), Is.EqualTo(0));
            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0.1f, Origin), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new BarkSelector(null));

            var selector = new BarkSelector(BarkSpec.Default);
            Assert.Throws<ArgumentException>(() => selector.TryBark(null, BarkKind.Spotted, 0f, Origin));
            Assert.Throws<ArgumentException>(() => selector.TryBark(string.Empty, BarkKind.Spotted, 0f, Origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryBark("a", (BarkKind)99, 0f, Origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryBark("a", BarkKind.Spotted, float.NaN, Origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryBark("a", BarkKind.Spotted, float.PositiveInfinity, Origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryBark("a", BarkKind.Spotted, 0f, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.CountSpeakingNear(new Vector3(0f, float.PositiveInfinity, 0f), 0f));
            Assert.Throws<ArgumentException>(() => selector.Forget(null));

            Assert.That(selector.TryBark("a", BarkKind.Spotted, 0f, Origin), Is.EqualTo(BarkVerdict.Allowed), "rejected input recorded nothing");
        }

        [Test]
        public void EachVerdictHasItsLogReason()
        {
            Assert.That(BarkLog.Reason(BarkVerdict.Allowed), Is.EqualTo("allowed"));
            Assert.That(BarkLog.Reason(BarkVerdict.KindCooldown), Is.EqualTo("kind-cooldown"));
            Assert.That(BarkLog.Reason(BarkVerdict.SpeakerCooldown), Is.EqualTo("speaker-cooldown"));
            Assert.That(BarkLog.Reason(BarkVerdict.GlobalInterval), Is.EqualTo("global-interval"));
            Assert.That(BarkLog.Reason(BarkVerdict.TooManyNearby), Is.EqualTo("too-many-nearby"));
            Assert.Throws<ArgumentOutOfRangeException>(() => BarkLog.Reason((BarkVerdict)99));
            Assert.That(
                BarkLog.Skipped("enemy-3", BarkKind.Grenade, BarkVerdict.TooManyNearby),
                Is.EqualTo("[Bark] speaker=enemy-3 kind=Grenade skipped reason=too-many-nearby"));
        }
    }
}
