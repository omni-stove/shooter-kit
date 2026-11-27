using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterComponent = Shooter.Runtime.Traversal.Water;

namespace Shooter.Tests.EditMode
{
    public sealed class TraversalFeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        // 立ち 2 m のプレイヤーの胸は足元から 1.3 m (SwimStep.ChestY: 中心 1 m と目 1.6 m の間)。
        private const float ChestAboveFeet = 1.3f;

        private Scene _scene;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [Test]
        public void TheTraversalMovesAreLinedUpInThePlayground()
        {
            Assert.That(
                TraversalFeatureSceneBuilder.Features,
                Is.EqualTo(new[]
                {
                    TraversalFeature.Lean, TraversalFeature.Swim, TraversalFeature.WallRun, TraversalFeature.DoubleJump, TraversalFeature.Parkour,
                }));

            var names = FeatureSceneBuilder.PlaygroundStations().Select(s => s.Fixture.Name).ToArray();
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                Assert.That(names, Has.Member(TraversalFeatureSceneBuilder.Fixture(feature).Name), feature.ToString());
            }
        }

        [Test]
        public void TheTuningIsNamedAfterThePlayground()
        {
            Assert.That(TraversalFeatureSceneBuilder.MotorSettingsPath, Is.EqualTo(DemoSample.Root + "/Movement/Playground.asset"));
            Assert.That(TraversalFeatureSceneBuilder.TraversalTuningPath, Is.EqualTo(DemoSample.Root + "/Tuning/Playground.json"));
            Assert.That(TraversalFeatureSceneBuilder.LegacyAssetPaths, Is.EqualTo(new[]
            {
                DemoSample.Root + "/Movement/Feature_DoubleJump.asset",
                DemoSample.Root + "/Tuning/Feature_WallRun.json",
            }));
        }

        [Test]
        public void TheKitDefaultsKeepTheWallRunAndTheDoubleJumpOffForOtherScenes()
        {
            Assert.That(WallRunTuning.Default.Enabled, Is.False);
            Assert.That(MotorSpec.Default.AirJumps, Is.EqualTo(0));
            Assert.That(TraversalTuning.Current.WallRun.Enabled, Is.False, "the game-wide traversal.json leaves the wall run off");
            Assert.That(MotorSettings.Resolve(null).AirJumps, Is.EqualTo(0), "the game-wide motor settings leave the double jump off");
        }

        [Test]
        public void TheKitDefaultsKeepTheParkourMovesOffForOtherScenes()
        {
            Assert.That(WallKickTuning.Default.Enabled, Is.False);
            Assert.That(LandingRollTuning.Default.Enabled, Is.False);
            Assert.That(WallClimbTuning.Default.Enabled, Is.False);
            Assert.That(LedgeHangTuning.Default.Enabled, Is.False);

            var parkour = TraversalTuning.Current.Parkour;
            Assert.That(parkour.WallKick.Enabled, Is.False, "the game-wide traversal.json leaves the wall kick off");
            Assert.That(parkour.LandingRoll.Enabled, Is.False);
            Assert.That(parkour.WallClimb.Enabled, Is.False);
            Assert.That(parkour.LedgeHang.Enabled, Is.False);
            Assert.That(parkour.Momentum.SameAs(MomentumTuning.Default), Is.True, "the game-wide momentum stays as it was");
        }

        [Test]
        public void PlaygroundTraversalJsonTurnsTheParkourMovesOnAndCarriesMomentum()
        {
            var parkour = TraversalJson.Parse(TraversalFeatureSceneBuilder.TraversalTuningJson(), "test").Parkour;

            Assert.That(parkour.WallKick.Enabled, Is.True);
            Assert.That(parkour.LandingRoll.Enabled, Is.True);
            Assert.That(parkour.WallClimb.Enabled, Is.True);
            Assert.That(parkour.LedgeHang.Enabled, Is.True);
            Assert.That(parkour.Momentum.SlideJumpBoost, Is.EqualTo(2f));
            Assert.That(parkour.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.5f));

            // 入り切りのほかは初期値のままにし、コースの大きさを初期値から決めたとおりに動く
            Assert.That(parkour.WallKick.UpSpeed, Is.EqualTo(WallKickTuning.DefaultUpSpeed));
            Assert.That(parkour.LandingRoll.FallSpeedScale, Is.EqualTo(LandingRollTuning.DefaultFallSpeedScale));
            Assert.That(parkour.WallClimb.MaxHeight, Is.EqualTo(WallClimbTuning.DefaultMaxHeight));
            Assert.That(parkour.LedgeHang.GrabReach, Is.EqualTo(LedgeHangTuning.DefaultGrabReach));
        }

        [Test]
        public void PlaygroundTraversalJsonIsWrittenTheSameUnderACommaDecimalCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            string json;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                json = TraversalFeatureSceneBuilder.TraversalTuningJson();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.That(json, Is.EqualTo(TraversalFeatureSceneBuilder.TraversalTuningJson()));
            Assert.That(TraversalJson.Parse(json, "test").Parkour.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.5f));
        }

        [Test]
        public void ParkourCourseLinesUpSixBoxesSizedFromTheDefaults()
        {
            var station = StationOf(TraversalFeatureSceneBuilder.ParkourCourseName);
            Populate();

            var course = Find(TraversalFeatureSceneBuilder.ParkourCourseName).transform;
            Assert.That(course.childCount, Is.EqualTo(6));
            Assert.That(Vector3.Distance(course.position, station.Offset), Is.LessThan(Epsilon), "moved with the station");

            var vault = course.Find(TraversalFeatureSceneBuilder.VaultBlockName);
            var left = course.Find(TraversalFeatureSceneBuilder.LeftKickWallName);
            var right = course.Find(TraversalFeatureSceneBuilder.RightKickWallName);
            var climb = course.Find(TraversalFeatureSceneBuilder.ClimbWallName);
            var platform = course.Find(TraversalFeatureSceneBuilder.RollPlatformName);
            var ledge = course.Find(TraversalFeatureSceneBuilder.LedgeWallName);

            AssertBox(vault, new Vector3(0f, 0.5f, 0.25f), new Vector3(4f, 1f, 0.5f));
            AssertBox(left, new Vector3(-1.65f, 2.5f, 7.5f), new Vector3(0.5f, 5f, 6f));
            AssertBox(right, new Vector3(1.65f, 2.5f, 7.5f), new Vector3(0.5f, 5f, 6f));
            AssertBox(climb, new Vector3(0f, 1.725f, 15.5f), new Vector3(4f, 3.45f, 2f));
            AssertBox(platform, new Vector3(0f, 2.575f, 18f), new Vector3(4f, 5.15f, 3f));
            AssertBox(ledge, new Vector3(0f, 1.6f, 32.25f), new Vector3(10f, 3.2f, 1.5f));

            var footprint = TraversalFeatureSceneBuilder.Fixture(TraversalFeature.Parkour).Footprint;
            Assert.That(footprint.size.x, Is.EqualTo(10f).Within(Epsilon));
            Assert.That(footprint.size.y, Is.EqualTo(5.15f).Within(Epsilon));
            Assert.That(footprint.size.z, Is.EqualTo(33f).Within(Epsilon));
        }

        [Test]
        public void ParkourCourseFitsEachMoveAtItsDefaultTuning()
        {
            var gravity = MotorSpec.DefaultGravity;

            // 乗り越え: 走って越えられる低く薄い箱
            Assert.That(TraversalFeatureSceneBuilder.VaultBlockHeight, Is.InRange(MotorSpec.DefaultVaultMinHeight, MotorSpec.DefaultVaultMaxHeight));
            Assert.That(TraversalFeatureSceneBuilder.VaultBlockDepth, Is.LessThanOrEqualTo(MotorSpec.DefaultVaultMaxDepth));

            // 壁蹴り: 蹴ってから上がり切るまでに進む幅に体の幅を足すと、向かいの壁に届く
            var kickReach = WallKickTuning.DefaultAwaySpeed * (WallKickTuning.DefaultUpSpeed / gravity);
            Assert.That(TraversalFeatureSceneBuilder.KickWallGap, Is.EqualTo(kickReach + (GameBootstrap.PlayerRadius * 2f)).Within(Epsilon));
            Assert.That(TraversalFeatureSceneBuilder.KickWallGap, Is.LessThan(TraversalFeatureSceneBuilder.CorridorWidth), "narrower than the wall-run corridor");
            var kickRise = WallKickTuning.DefaultUpSpeed * WallKickTuning.DefaultUpSpeed / (2f * gravity);
            Assert.That(
                TraversalFeatureSceneBuilder.KickWallHeight,
                Is.GreaterThan(GameBootstrap.PlayerHeight + MotorSpec.DefaultJumpHeight + kickRise),
                "a kick at the top of a jump stays below the top");

            // 駆け上がり: よじ登りでは届かず、駆け上がれる高さと秒数のうちにふちがよじ登れる所まで下りてくる
            var climbNeeded = TraversalFeatureSceneBuilder.ClimbWallHeight - MotorSpec.DefaultMantleMaxHeight;
            Assert.That(climbNeeded, Is.GreaterThan(0f), "too high to mantle from the floor");
            Assert.That(climbNeeded, Is.LessThan(WallClimbTuning.DefaultMaxHeight));
            Assert.That(climbNeeded, Is.LessThan(WallClimbTuning.DefaultClimbSpeed * WallClimbTuning.DefaultMaxSeconds));
            Assert.That(MotorSpec.DefaultWalkSpeed, Is.GreaterThanOrEqualTo(WallClimbTuning.DefaultMinSpeed), "a walk into the wall is fast enough");
            Assert.That(TraversalFeatureSceneBuilder.ClimbWallDepth, Is.GreaterThanOrEqualTo(GameBootstrap.PlayerRadius * 2f), "room to stand on top");

            // 受け身: 駆け上がった壁の上からよじ登る高台で、床から駆け上がっては届かない
            var step = TraversalFeatureSceneBuilder.RollPlatformHeight - TraversalFeatureSceneBuilder.ClimbWallHeight;
            Assert.That(step, Is.GreaterThan(MotorSpec.DefaultVaultMaxHeight));
            Assert.That(step, Is.LessThanOrEqualTo(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(
                TraversalFeatureSceneBuilder.RollPlatformHeight,
                Is.GreaterThan(MotorSpec.DefaultMantleMaxHeight + WallClimbTuning.DefaultMaxHeight),
                "no wall climb onto it from the floor");

            // 落ちる速さは傷む速さを超え、受け身を取ると下回る
            var fallSpeed = Mathf.Sqrt(2f * gravity * TraversalFeatureSceneBuilder.RollPlatformHeight);
            Assert.That(fallSpeed, Is.GreaterThan(MotorSpec.DefaultSafeFallSpeed), "hurts without a roll");
            Assert.That(fallSpeed * LandingRollTuning.DefaultFallSpeedScale, Is.LessThanOrEqualTo(MotorSpec.DefaultSafeFallSpeed), "safe with a roll");
            Assert.That(fallSpeed, Is.GreaterThanOrEqualTo(LandingRollTuning.DefaultMinFallSpeed), "fast enough to roll");
            var fallSeconds = fallSpeed / gravity;
            var sprintAndRoll = MotorSpec.DefaultSprintSpeed * (fallSeconds + LandingRollTuning.DefaultRollSeconds);
            Assert.That(TraversalFeatureSceneBuilder.RollLandingLength, Is.GreaterThan(sprintAndRoll), "the roll ends before the ledge wall");

            // ぶら下がり: 立ったジャンプの頂点で手の届く範囲に上端があり、よじ登りでは届かない横に長いふち
            var handAtApex = GameBootstrap.PlayerHeight + MotorSpec.DefaultJumpHeight;
            Assert.That(
                TraversalFeatureSceneBuilder.LedgeHeight,
                Is.InRange(handAtApex - LedgeHangTuning.DefaultGrabReach, handAtApex + LedgeHangTuning.DefaultGrabReach));
            Assert.That(TraversalFeatureSceneBuilder.LedgeHeight, Is.GreaterThan(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(TraversalFeatureSceneBuilder.LedgeLength, Is.GreaterThan(TraversalFeatureSceneBuilder.CourseWidth), "long enough to shimmy along");
            Assert.That(TraversalFeatureSceneBuilder.LedgeDepth, Is.GreaterThanOrEqualTo(GameBootstrap.PlayerRadius * 2f), "room to stand after climbing up");
        }

        [Test]
        public void PlaygroundRulesLetThePlayerJumpAndClimbOutOfThePool()
        {
            var rules = GameRulesAssetBuilder.PlaygroundRules();

            Assert.That(rules.Actions.Jump, Is.EqualTo(Jump.On));
            Assert.That(rules.Actions.Mantle, Is.EqualTo(Mantle.On), "climbing out of water is a mantle");
        }

        [Test]
        public void TheFixturesAreNotPickedUpByTheTpsRange()
        {
            var names = TpsRangeSceneBuilder.Fixtures().Select(f => f.GetType().Name).ToArray();

            Assert.That(names, Has.No.Member("TraversalFeatureFixture"));
        }

        [Test]
        public void LeanSceneHasACornerToPeekAroundAndALowWallToCrouchBehind()
        {
            Populate();

            var corner = Find(TraversalFeatureSceneBuilder.LeanCornerName).transform;
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerFrontWallName), Is.Not.Null);
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerSideWallName), Is.Not.Null);
            var low = corner.Find(TraversalFeatureSceneBuilder.LowWallName);
            Assert.That(low.localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.LowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.CornerWallHeight, Is.GreaterThan(GameBootstrap.PlayerHeight), "the corner hides a standing player");
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.LessThan(MotorSpec.DefaultCoverLowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.GreaterThan(PlayerController.DefaultCrouchHeight / 2f), "hides the crouched body");
        }

        [Test]
        public void SwimSceneHasShallowAndDeepWaterAndAPoolWithAHighRim()
        {
            var station = StationOf(TraversalFeatureSceneBuilder.SwimPoolsName);
            Populate();

            var pools = Find(TraversalFeatureSceneBuilder.SwimPoolsName);
            var waters = pools.GetComponentsInChildren<WaterComponent>().ToDictionary(w => w.name);
            Assert.That(waters.Keys, Is.EquivalentTo(new[]
            {
                TraversalFeatureSceneBuilder.ShallowWaterName,
                TraversalFeatureSceneBuilder.DeepWaterName,
                TraversalFeatureSceneBuilder.HighRimWaterName,
            }));

            var shallow = waters[TraversalFeatureSceneBuilder.ShallowWaterName];
            var deep = waters[TraversalFeatureSceneBuilder.DeepWaterName];
            var highRim = waters[TraversalFeatureSceneBuilder.HighRimWaterName];
            Assert.That(shallow.SurfaceHeight, Is.LessThan(ChestAboveFeet), "wading, not swimming");
            Assert.That(deep.SurfaceHeight, Is.GreaterThan(GameBootstrap.PlayerHeight), "over the head");
            Assert.That(highRim.SurfaceHeight, Is.EqualTo(deep.SurfaceHeight));
            foreach (var water in waters.Values)
            {
                Assert.That(water.SurfaceHeight, Is.LessThanOrEqualTo(water.Size.y), water.name);
                Assert.That(water.TryGetBox(out _), Is.True, water.name);
            }

            var deepBottom = TraversalFeatureSceneBuilder.Waters(TraversalFeature.Swim).Single(w => w.Name == TraversalFeatureSceneBuilder.DeepWaterName).Bottom;
            Assert.That(Vector3.Distance(deep.transform.position, deepBottom + station.Offset), Is.LessThan(Epsilon), "moved with the station");

            // 浮いている足元は水面から胸の高さだけ下。縁はジャンプでは届かず、崖のぼりで届く
            var rimAboveFloatingFeet = TraversalFeatureSceneBuilder.HighRimHeight - (TraversalFeatureSceneBuilder.DeepSurfaceHeight - ChestAboveFeet);
            Assert.That(rimAboveFloatingFeet, Is.GreaterThan(MotorSpec.DefaultJumpHeight));
            Assert.That(rimAboveFloatingFeet, Is.LessThanOrEqualTo(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(TraversalFeatureSceneBuilder.HighRimHeight, Is.GreaterThan(MotorSpec.DefaultMantleMaxHeight), "no climbing in from the floor");
            Assert.That(TraversalFeatureSceneBuilder.DividerThickness, Is.GreaterThanOrEqualTo(GameBootstrap.PlayerRadius * 2f), "room to stand on the divider");
            Assert.That(pools.transform.Find(TraversalFeatureSceneBuilder.DividerName).localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.HighRimHeight));
        }

        [Test]
        public void PlaygroundTraversalJsonTurnsTheWallRunOnBesideACorridor()
        {
            var traversal = NewText(TraversalFeatureSceneBuilder.TraversalTuningJson());

            Populate();

            var spec = TraversalJson.Parse(traversal.text, "test");
            Assert.That(spec.WallRun.Enabled, Is.True);
            Assert.That(spec.WallRun.MaxSeconds, Is.EqualTo(WallRunTuning.DefaultMaxSeconds));
            Assert.That(spec.WallRun.MinSpeed, Is.EqualTo(WallRunTuning.DefaultMinSpeed));
            Assert.That(spec.Swim.Speed, Is.EqualTo(SwimTuning.DefaultSpeed));

            var corridor = Find(TraversalFeatureSceneBuilder.WallRunCorridorName).transform;
            var left = corridor.Find(TraversalFeatureSceneBuilder.LeftRunWallName);
            var right = corridor.Find(TraversalFeatureSceneBuilder.RightRunWallName);
            var gap = (right.position.x - (right.localScale.x / 2f)) - (left.position.x + (left.localScale.x / 2f));
            Assert.That(gap, Is.EqualTo(TraversalFeatureSceneBuilder.CorridorWidth).Within(Epsilon));
            Assert.That(gap, Is.GreaterThan(GameBootstrap.PlayerRadius * 2f));
            Assert.That(left.localScale.z, Is.GreaterThanOrEqualTo(MotorSpec.DefaultSprintSpeed * WallRunTuning.DefaultMaxSeconds), "a whole run fits along the wall");
            Assert.That(left.localScale.y, Is.GreaterThan(GameBootstrap.PlayerHeight + MotorSpec.DefaultJumpHeight));
        }

        [Test]
        public void PlaygroundMotorSettingsHaveOneAirJumpAndStepsTooHighForOneJump()
        {
            var settings = NewMotorSettings();
            TraversalFeatureSceneBuilder.ConfigureDoubleJump(settings, null);

            Populate();

            Assert.That(settings.CreateSpec().AirJumps, Is.EqualTo(TraversalFeatureSceneBuilder.DoubleJumpAirJumps));

            var steps = Find(TraversalFeatureSceneBuilder.DoubleJumpStepsName).transform;
            Assert.That(steps.childCount, Is.EqualTo(TraversalFeatureSceneBuilder.StepCount));
            for (var i = 0; i < TraversalFeatureSceneBuilder.StepCount; i++)
            {
                var step = steps.Find(TraversalFeatureSceneBuilder.StepNamePrefix + (i + 1));
                Assert.That(step.localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.StepRise * (i + 1)).Within(Epsilon), step.name);
            }

            var airJumpHeight = MotorSpec.DefaultAirJumpSpeed * MotorSpec.DefaultAirJumpSpeed / (2f * MotorSpec.DefaultGravity);
            Assert.That(TraversalFeatureSceneBuilder.StepRise, Is.GreaterThan(MotorSpec.DefaultJumpHeight), "one jump falls short");
            Assert.That(TraversalFeatureSceneBuilder.StepRise, Is.LessThan(MotorSpec.DefaultJumpHeight + airJumpHeight), "a double jump makes it");
        }

        [Test]
        public void ConfigureDoubleJumpKeepsTheGameWideValuesAndTheName()
        {
            var source = NewMotorSettings();
            source.WalkSpeed = 3f;
            source.JumpHeight = 1.5f;
            var settings = NewMotorSettings();
            settings.name = "Feature_DoubleJump";

            TraversalFeatureSceneBuilder.ConfigureDoubleJump(settings, source);

            Assert.That(settings.name, Is.EqualTo("Feature_DoubleJump"));
            Assert.That(settings.WalkSpeed, Is.EqualTo(3f));
            Assert.That(settings.JumpHeight, Is.EqualTo(1.5f));
            Assert.That(settings.AirJumps, Is.EqualTo(TraversalFeatureSceneBuilder.DoubleJumpAirJumps));
            Assert.That(source.AirJumps, Is.EqualTo(MotorSpec.DefaultAirJumps), "the game-wide settings stay as they are");
        }

        [Test]
        public void ConfigureDoubleJumpRejectsBrokenSourceValues()
        {
            var source = NewMotorSettings();
            source.Gravity = -1f;

            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.ConfigureDoubleJump(NewMotorSettings(), source));
            Assert.Throws<ArgumentNullException>(() => TraversalFeatureSceneBuilder.ConfigureDoubleJump(null, source));
        }

        [Test]
        public void UndefinedFeaturesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Fixture((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Boxes((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Waters((TraversalFeature)99));
        }

        private static void AssertBox(Transform box, Vector3 center, Vector3 size)
        {
            Assert.That(box, Is.Not.Null);
            Assert.That(Vector3.Distance(box.localPosition, center), Is.LessThan(Epsilon), box.name + " center");
            Assert.That(Vector3.Distance(box.localScale, size), Is.LessThan(Epsilon), box.name + " size");
        }

        private static FeatureStation StationOf(string name)
        {
            return FeatureSceneBuilder.PlaygroundStations().Single(s => s.Fixture.Name == name);
        }

        private void Populate()
        {
            var feature = FeatureSceneBuilder.Playground();
            var bootstrap = FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(), feature);
            Assert.That(bootstrap.CreatesFloor, Is.False);
        }

        private WeaponDefinition[] NewWeapons()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, DemoTestWeapons.CarbineSpec());
            return new[] { weapon };
        }

        private GameRulesDefinition NewRules()
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, GameRulesAssetBuilder.PlaygroundRules());
            return definition;
        }

        private MotorSettings NewMotorSettings()
        {
            var settings = ScriptableObject.CreateInstance<MotorSettings>();
            _assets.Add(settings);
            return settings;
        }

        private TextAsset NewText(string text)
        {
            var asset = new TextAsset(text) { name = "Traversal" };
            _assets.Add(asset);
            return asset;
        }

        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail($"'{name}' is not in the scene");
            return null;
        }
    }
}
