using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MeleePathTests
    {
        private const string _weaponId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private static readonly Vector3 _origin = Vector3.Zero;
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);
        private static readonly int[] _rightToLeft = { 2, 3, 0 };

        private static MeleeSpec Spec(MeleePath path, float reach = 2f) => new MeleeSpec(50, reach, 90f, 0.6f, 0.1f, 0.2f, 0f, path: path);

        private static Vector3 At(float yawDegrees, float distance, float height = 0f)
        {
            var radians = yawDegrees * Math.PI / 180.0;
            return new Vector3((float)(Math.Sin(radians) * distance), height, (float)(Math.Cos(radians) * distance));
        }

        [Test]
        public void ASpecWithoutAPathKeepsTheSingleStrike()
        {
            Assert.That(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f).Path, Is.Null);
        }

        [TestCase(MeleePathStyle.Horizontal, 30f, 30f, "toDegrees")]
        [TestCase(MeleePathStyle.Horizontal, 190f, 0f, "fromDegrees")]
        [TestCase(MeleePathStyle.Vertical, 60f, -95f, "toDegrees")]
        public void BadPathAnglesAreRefusedByName(MeleePathStyle style, float from, float to, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new MeleePath(style, from, to));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void AThrustNeedsNoAngles()
        {
            Assert.That(new MeleePath(MeleePathStyle.Thrust, 0f, 0f).Style, Is.EqualTo(MeleePathStyle.Thrust));
        }

        [Test]
        public void BadPathSizesAreRefusedByName()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleePath(MeleePathStyle.Horizontal, 45f, -45f, widthDegrees: 0f)).ParamName, Is.EqualTo("widthDegrees"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleePath(MeleePathStyle.Horizontal, 45f, -45f, activeSeconds: -0.1f)).ParamName, Is.EqualTo("activeSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleePath(MeleePathStyle.Horizontal, 45f, -45f, maxTargets: 0)).ParamName, Is.EqualTo("maxTargets"));
        }

        [Test]
        public void TheBladeMustFinishItsPathBeforeTheSwingEnds()
        {
            var path = new MeleePath(MeleePathStyle.Horizontal, 45f, -45f, activeSeconds: 0.5f);

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.6f, 0.1f, 0.2f, 0f, path: path));

            Assert.That(e.ParamName, Is.EqualTo("path"));
        }

        [Test]
        public void AHorizontalSwingCrossesATargetWhenTheBladeReachesItsSide()
        {
            var spec = Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, 30f));
            var right = At(45f, 1.5f);

            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, right, 0f, 0.1f, out _, out _), Is.False, "the blade has not reached the right side yet");
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, right, 0.1f, 0.2f, out var crossing, out var distance), Is.True);
            Assert.That(crossing, Is.EqualTo(0.125f).Within(1e-4f));
            Assert.That(distance, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, right, 0.5f, 1f, out _, out _), Is.False, "a target the blade has passed is not crossed again");
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(-45f, 1.5f), 0.5f, 1f, out crossing, out _), Is.True);
            Assert.That(crossing, Is.EqualTo(0.875f).Within(1e-4f));
        }

        [Test]
        public void AHorizontalSwingMissesWhatIsOutsideItsReachOrAboveItsWidth()
        {
            var spec = Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, 30f));

            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(0f, 2.5f), 0f, 1f, out _, out _), Is.False);
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(0f, 1f, 1.2f), 0f, 1f, out _, out _), Is.False, "50 degrees up is above a 30 degree wide blade");
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(90f, 1f), 0f, 1f, out _, out _), Is.False, "the blade starts at 60 degrees to the right");
        }

        [Test]
        public void AVerticalSwingCrossesTargetsAheadAsItComesDownAndMissesThoseToTheSide()
        {
            var spec = Spec(new MeleePath(MeleePathStyle.Vertical, 60f, -30f, 40f));

            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(0f, 1.5f), 0f, 1f, out var crossing, out _), Is.True);
            Assert.That(crossing, Is.EqualTo(60f / 90f).Within(1e-4f));
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(0f, 1.5f, -0.5f), 0f, 1f, out var lower, out _), Is.True);
            Assert.That(lower, Is.GreaterThan(crossing), "a lower target is crossed later");
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(30f, 1.5f), 0f, 1f, out _, out _), Is.False, "30 degrees to the side is outside a 40 degree wide blade");
        }

        [Test]
        public void AThrustReachesFurtherAsItGoesAndStaysNarrow()
        {
            var spec = Spec(new MeleePath(MeleePathStyle.Thrust, 0f, 0f, 20f));
            var ahead = At(0f, 1f);

            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, ahead, 0f, 0.4f, out _, out _), Is.False);
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, ahead, 0.4f, 0.6f, out var crossing, out _), Is.True);
            Assert.That(crossing, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(MeleeJudge.TryCross(_origin, _forward, spec, At(30f, 1f), 0f, 1f, out _, out _), Is.False);
        }

        [Test]
        public void CrossedTargetsComeInTheOrderTheBladeMeetsThem()
        {
            var spec = Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, 30f));
            var candidates = new List<Vector3> { At(-40f, 1f), At(0f, 3f), At(40f, 1.5f), At(0f, 1f) };
            var crossed = new List<int>();

            MeleeJudge.SelectCrossed(_origin, _forward, spec, candidates, 0f, 1f, crossed);

            Assert.That(crossed, Is.EqualTo(_rightToLeft), "right to left; the one out of reach is left out");
        }

        [Test]
        public void TheSwingReportsHowFarTheBladeMovedEachTick()
        {
            var swing = new MeleeSwing(Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, activeSeconds: 0.2f)));
            swing.TryStart();

            Assert.That(swing.Tick(0.1f), Is.False);
            Assert.That(swing.HasSweep, Is.False, "the blade waits for the impact moment");

            Assert.That(swing.Tick(0.1f), Is.True);
            Assert.That(swing.HasSweep, Is.True);
            Assert.That(swing.SweepFrom, Is.EqualTo(0f));
            Assert.That(swing.SweepTo, Is.EqualTo(0f).Within(1e-4f));

            swing.Tick(0.1f);
            Assert.That(swing.SweepFrom, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(swing.SweepTo, Is.EqualTo(0.5f).Within(1e-4f));

            swing.Tick(0.1f);
            Assert.That(swing.SweepFrom, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(swing.SweepTo, Is.EqualTo(1f));

            swing.Tick(0.1f);
            Assert.That(swing.HasSweep, Is.False, "the blade has finished its path");
        }

        [Test]
        public void ALongTickSweepsTheWholePathEvenIfTheSwingEnds()
        {
            var swing = new MeleeSwing(Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, activeSeconds: 0.2f)));
            swing.TryStart();

            Assert.That(swing.Tick(1f), Is.True);
            Assert.That(swing.HasSweep, Is.True);
            Assert.That(swing.SweepFrom, Is.EqualTo(0f));
            Assert.That(swing.SweepTo, Is.EqualTo(1f));
            Assert.That(swing.IsSwinging, Is.False);
        }

        [Test]
        public void AZeroSecondPathSweepsAtTheImpact()
        {
            var swing = new MeleeSwing(Spec(new MeleePath(MeleePathStyle.Horizontal, 60f, -60f, activeSeconds: 0f)));
            swing.TryStart();

            swing.Tick(0.2f);

            Assert.That(swing.HasSweep, Is.True);
            Assert.That(swing.SweepTo, Is.EqualTo(1f));
        }

        [Test]
        public void ASwingWithoutAPathNeverSweeps()
        {
            var swing = new MeleeSwing(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0f));
            swing.TryStart();

            Assert.That(swing.Tick(0.3f), Is.True);
            Assert.That(swing.HasSweep, Is.False);
        }

        [Test]
        public void TheChargeMovesThePathTowardsTheHeavyPath()
        {
            var light = new MeleeSpec(30, 2f, 90f, 0.6f, 0.1f, 0.2f, 0f, path: new MeleePath(MeleePathStyle.Horizontal, 40f, -40f, 20f, 0.1f, 1));
            var heavy = new MeleeSpec(90, 2f, 90f, 1f, 0.1f, 0.4f, 0f, path: new MeleePath(MeleePathStyle.Horizontal, 80f, -80f, 40f, 0.3f, 3));
            var weapon = new MeleeWeaponSpec(light, heavy);

            var half = weapon.SwingFor(0.5f).Path;

            Assert.That(half.FromDegrees, Is.EqualTo(60f).Within(1e-4f));
            Assert.That(half.ToDegrees, Is.EqualTo(-60f).Within(1e-4f));
            Assert.That(half.WidthDegrees, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(half.ActiveSeconds, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(half.MaxTargets, Is.EqualTo(2));
        }

        [Test]
        public void AChargeBetweenDifferentPathsTakesTheNearerOneAndFitsItIntoTheSwing()
        {
            var light = new MeleeSpec(30, 2f, 90f, 0.3f, 0.1f, 0.1f, 0f);
            var heavy = new MeleeSpec(90, 2f, 90f, 1f, 0.1f, 0.2f, 0f, path: new MeleePath(MeleePathStyle.Vertical, 60f, -30f, activeSeconds: 0.8f));
            var weapon = new MeleeWeaponSpec(light, heavy);

            Assert.That(weapon.SwingFor(0.25f).Path, Is.Null);
            var nearHeavy = weapon.SwingFor(0.6f);
            Assert.That(nearHeavy.Path.Style, Is.EqualTo(MeleePathStyle.Vertical));
            Assert.That(nearHeavy.ImpactSeconds + nearHeavy.Path.ActiveSeconds, Is.LessThanOrEqualTo(nearHeavy.SwingSeconds + 1e-5f));
        }

        [Test]
        public void APathIsReadFromTheSwingAndTheHeavySwingTakesItWhenLeftOut()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _weaponId + "\", \"category\": \"Melee\", \"melee\": {\"swing\": {\"swingSeconds\": 0.6, \"impactSeconds\": 0.2, "
                + "\"path\": {\"style\": \"Vertical\", \"fromDegrees\": 60, \"toDegrees\": -30, \"activeSeconds\": 0.2, \"maxTargets\": 2}}, \"heavySwing\": {\"damage\": 100}}}",
                "melee.json");

            var path = read.Melee.Swing.Path;
            Assert.That(path.Style, Is.EqualTo(MeleePathStyle.Vertical));
            Assert.That(path.FromDegrees, Is.EqualTo(60f));
            Assert.That(path.ToDegrees, Is.EqualTo(-30f));
            Assert.That(path.WidthDegrees, Is.EqualTo(MeleePath.DefaultWidthDegrees));
            Assert.That(path.ActiveSeconds, Is.EqualTo(0.2f));
            Assert.That(path.MaxTargets, Is.EqualTo(2));
            Assert.That(read.Melee.HeavySwing.Path, Is.SameAs(path));
        }

        [Test]
        public void APathIsWrittenBackAsItWasRead()
        {
            var text = WeaponSpecJson.Write(WeaponSpecJson.Parse(
                "{\"id\": \"" + _weaponId + "\", \"category\": \"Melee\", \"melee\": {\"swing\": {\"swingSeconds\": 0.6, \"impactSeconds\": 0.2, "
                + "\"path\": {\"style\": \"Thrust\", \"fromDegrees\": 0, \"toDegrees\": 0, \"widthDegrees\": 15, \"activeSeconds\": 0.1, \"maxTargets\": 1}}}}",
                "melee.json"));

            var again = WeaponSpecJson.Parse(text, "again.json");

            Assert.That(text, Does.Contain("\"path\""));
            Assert.That(again.Melee.Swing.Path.Style, Is.EqualTo(MeleePathStyle.Thrust));
            Assert.That(again.Melee.Swing.Path.WidthDegrees, Is.EqualTo(15f));
            Assert.That(WeaponSpecJson.Write(again), Is.EqualTo(text));
        }

        [TestCase("{\"path\": {\"style\": \"Spin\"}}", "melee.swing.path.style")]
        [TestCase("{\"path\": {\"fromDegrees\": 10, \"toDegrees\": 10}}", "melee.swing.path.toDegrees")]
        [TestCase("{\"path\": {\"maxTargets\": 0}}", "melee.swing.path.maxTargets")]
        [TestCase("{\"path\": {\"activeSeconds\": 5}}", "melee.swing.path")]
        [TestCase("{\"path\": {\"edge\": 1}}", "melee.swing.path.edge")]
        public void ABadPathValueNamesItsItem(string swing, string item)
        {
            var text = "{\"id\": \"" + _weaponId + "\", \"category\": \"Melee\", \"melee\": {\"swing\": " + swing + "}}";

            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }
    }
}
