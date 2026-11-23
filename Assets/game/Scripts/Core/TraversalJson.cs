using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class TraversalJson
    {
        public const string ResourcePath = "Tuning/traversal";

        public const string SlideItem = "slide";
        public const string ParachuteItem = "parachute";
        public const string ZiplineItem = "zipline";
        public const string SwimItem = "swim";
        public const string WallRunItem = "wallRun";
        public const string WallKickItem = "wallKick";
        public const string LandingRollItem = "landingRoll";
        public const string WallClimbItem = "wallClimb";
        public const string LedgeHangItem = "ledgeHang";
        public const string MomentumItem = "momentum";

        public const string StartSpeedItem = "startSpeed";
        public const string DecelerationItem = "deceleration";
        public const string EndSpeedItem = "endSpeed";
        public const string MinSecondsItem = "minSeconds";
        public const string MaxSecondsItem = "maxSeconds";
        public const string SlopeAccelerationItem = "slopeAcceleration";

        public const string MinOpenHeightItem = "minOpenHeight";
        public const string DescentSpeedItem = "descentSpeed";
        public const string HorizontalSpeedItem = "horizontalSpeed";
        public const string SteerAccelerationItem = "steerAcceleration";

        public const string GrabRadiusItem = "grabRadius";
        public const string HangOffsetItem = "hangOffset";
        public const string RegrabSecondsItem = "regrabSeconds";
        public const string SpreadMoveFractionItem = "spreadMoveFraction";

        public const string SpeedItem = "speed";
        public const string BuoyancyItem = "buoyancy";
        public const string BreathSecondsItem = "breathSeconds";
        public const string DrownDamageItem = "drownDamage";
        public const string DrownIntervalSecondsItem = "drownIntervalSeconds";

        public const string EnabledItem = "enabled";
        public const string MinSpeedItem = "minSpeed";
        public const string GravityScaleItem = "gravityScale";
        public const string JumpOffSpeedItem = "jumpOffSpeed";

        public const string UpSpeedItem = "upSpeed";
        public const string AwaySpeedItem = "awaySpeed";

        public const string WindowSecondsItem = "windowSeconds";
        public const string FallSpeedScaleItem = "fallSpeedScale";
        public const string RollSecondsItem = "rollSeconds";
        public const string MinFallSpeedItem = "minFallSpeed";

        public const string ClimbSpeedItem = "climbSpeed";
        public const string MaxHeightItem = "maxHeight";
        public const string PushOffSpeedItem = "pushOffSpeed";
        public const string MantleAtLedgeItem = "mantleAtLedge";

        public const string GrabReachItem = "grabReach";
        public const string ShimmySpeedItem = "shimmySpeed";
        public const string ClimbUpSpeedItem = "climbUpSpeed";

        public const string SlideJumpBoostItem = "slideJumpBoost";
        public const string VaultExitSpeedKeepItem = "vaultExitSpeedKeep";

        private const string _kind = "traversal JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SlideItem, ParachuteItem, ZiplineItem, SwimItem, WallRunItem, WallKickItem, LandingRollItem, WallClimbItem, LedgeHangItem, MomentumItem,
        };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> SlideItemNames { get; } = new[]
        {
            StartSpeedItem,
            DecelerationItem,
            EndSpeedItem,
            MinSecondsItem,
            MaxSecondsItem,
            SlopeAccelerationItem,
        };

        public static IReadOnlyList<string> ParachuteItemNames { get; } = new[]
        {
            MinOpenHeightItem,
            DescentSpeedItem,
            HorizontalSpeedItem,
            SteerAccelerationItem,
        };

        public static IReadOnlyList<string> ZiplineItemNames { get; } = new[]
        {
            GrabRadiusItem,
            HangOffsetItem,
            RegrabSecondsItem,
            SpreadMoveFractionItem,
        };

        public static IReadOnlyList<string> SwimItemNames { get; } = new[]
        {
            SpeedItem,
            BuoyancyItem,
            BreathSecondsItem,
            DrownDamageItem,
            DrownIntervalSecondsItem,
        };

        public static IReadOnlyList<string> WallRunItemNames { get; } = new[]
        {
            EnabledItem,
            MaxSecondsItem,
            MinSpeedItem,
            GravityScaleItem,
            JumpOffSpeedItem,
        };

        public static IReadOnlyList<string> WallKickItemNames { get; } = new[]
        {
            EnabledItem,
            UpSpeedItem,
            AwaySpeedItem,
        };

        public static IReadOnlyList<string> LandingRollItemNames { get; } = new[]
        {
            EnabledItem,
            WindowSecondsItem,
            FallSpeedScaleItem,
            RollSecondsItem,
            MinFallSpeedItem,
        };

        public static IReadOnlyList<string> WallClimbItemNames { get; } = new[]
        {
            EnabledItem,
            MinSpeedItem,
            ClimbSpeedItem,
            MaxSecondsItem,
            MaxHeightItem,
            PushOffSpeedItem,
            MantleAtLedgeItem,
        };

        public static IReadOnlyList<string> LedgeHangItemNames { get; } = new[]
        {
            EnabledItem,
            GrabReachItem,
            ShimmySpeedItem,
            ClimbUpSpeedItem,
            RegrabSecondsItem,
        };

        public static IReadOnlyList<string> MomentumItemNames { get; } = new[]
        {
            SlideJumpBoostItem,
            VaultExitSpeedKeepItem,
        };

        public static TraversalSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var slide = fields.Object(SlideItem, SlideItemNames);
                var parachute = fields.Object(ParachuteItem, ParachuteItemNames);
                var zipline = fields.Object(ZiplineItem, ZiplineItemNames);
                var swim = fields.Object(SwimItem, SwimItemNames);
                var wallRun = fields.Object(WallRunItem, WallRunItemNames);
                var wallKick = fields.Object(WallKickItem, WallKickItemNames);
                var landingRoll = fields.Object(LandingRollItem, LandingRollItemNames);
                var wallClimb = fields.Object(WallClimbItem, WallClimbItemNames);
                var ledgeHang = fields.Object(LedgeHangItem, LedgeHangItemNames);
                var momentum = fields.Object(MomentumItem, MomentumItemNames);
                return new TraversalSpec(
                    slide == null ? null : ReadSlide(slide),
                    parachute == null ? null : ReadParachute(parachute),
                    zipline == null ? null : ReadZipline(zipline),
                    swim == null ? null : ReadSwim(swim),
                    wallRun == null ? null : ReadWallRun(wallRun),
                    new ParkourTuning(
                        wallKick == null ? null : ReadWallKick(wallKick),
                        landingRoll == null ? null : ReadLandingRoll(landingRoll),
                        wallClimb == null ? null : ReadWallClimb(wallClimb),
                        ledgeHang == null ? null : ReadLedgeHang(ledgeHang),
                        momentum == null ? null : ReadMomentum(momentum)));
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static SlideTuning ReadSlide(ServerDataJson.Fields fields)
        {
            var defaults = SlideTuning.Default;
            var startSpeed = fields.ReadFloat(StartSpeedItem, defaults.StartSpeed);
            var deceleration = fields.ReadFloat(DecelerationItem, defaults.Deceleration);
            var endSpeed = fields.ReadFloat(EndSpeedItem, defaults.EndSpeed);
            var minSeconds = fields.ReadFloat(MinSecondsItem, defaults.MinSeconds);
            var maxSeconds = fields.ReadFloat(MaxSecondsItem, defaults.MaxSeconds);
            var slopeAcceleration = fields.ReadFloat(SlopeAccelerationItem, defaults.SlopeAcceleration);
            try
            {
                return new SlideTuning(startSpeed, deceleration, endSpeed, minSeconds, maxSeconds, slopeAcceleration);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, SlideItemNames, e);
            }
        }

        private static ParachuteTuning ReadParachute(ServerDataJson.Fields fields)
        {
            var defaults = ParachuteTuning.Default;
            var minOpenHeight = fields.ReadFloat(MinOpenHeightItem, defaults.MinOpenHeight);
            var descentSpeed = fields.ReadFloat(DescentSpeedItem, defaults.DescentSpeed);
            var horizontalSpeed = fields.ReadFloat(HorizontalSpeedItem, defaults.HorizontalSpeed);
            var steerAcceleration = fields.ReadFloat(SteerAccelerationItem, defaults.SteerAcceleration);
            try
            {
                return new ParachuteTuning(minOpenHeight, descentSpeed, horizontalSpeed, steerAcceleration);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ParachuteItemNames, e);
            }
        }

        private static ZiplineTuning ReadZipline(ServerDataJson.Fields fields)
        {
            var defaults = ZiplineTuning.Default;
            var grabRadius = fields.ReadFloat(GrabRadiusItem, defaults.GrabRadius);
            var hangOffset = fields.ReadFloat(HangOffsetItem, defaults.HangOffset);
            var regrabSeconds = fields.ReadFloat(RegrabSecondsItem, defaults.RegrabSeconds);
            var spreadMoveFraction = fields.ReadFloat(SpreadMoveFractionItem, defaults.SpreadMoveFraction);
            try
            {
                return new ZiplineTuning(grabRadius, hangOffset, regrabSeconds, spreadMoveFraction);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ZiplineItemNames, e);
            }
        }

        private static SwimTuning ReadSwim(ServerDataJson.Fields fields)
        {
            var defaults = SwimTuning.Default;
            var speed = fields.ReadFloat(SpeedItem, defaults.Speed);
            var buoyancy = fields.ReadFloat(BuoyancyItem, defaults.Buoyancy);
            var breathSeconds = fields.ReadFloat(BreathSecondsItem, defaults.BreathSeconds);
            var drownDamage = fields.ReadInt(DrownDamageItem, defaults.DrownDamage);
            var drownIntervalSeconds = fields.ReadFloat(DrownIntervalSecondsItem, defaults.DrownIntervalSeconds);
            try
            {
                return new SwimTuning(speed, buoyancy, breathSeconds, drownDamage, drownIntervalSeconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, SwimItemNames, e);
            }
        }

        private static WallRunTuning ReadWallRun(ServerDataJson.Fields fields)
        {
            var defaults = WallRunTuning.Default;
            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var maxSeconds = fields.ReadFloat(MaxSecondsItem, defaults.MaxSeconds);
            var minSpeed = fields.ReadFloat(MinSpeedItem, defaults.MinSpeed);
            var gravityScale = fields.ReadFloat(GravityScaleItem, defaults.GravityScale);
            var jumpOffSpeed = fields.ReadFloat(JumpOffSpeedItem, defaults.JumpOffSpeed);
            try
            {
                return new WallRunTuning(enabled, maxSeconds, minSpeed, gravityScale, jumpOffSpeed);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, WallRunItemNames, e);
            }
        }

        private static WallKickTuning ReadWallKick(ServerDataJson.Fields fields)
        {
            var defaults = WallKickTuning.Default;
            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var upSpeed = fields.ReadFloat(UpSpeedItem, defaults.UpSpeed);
            var awaySpeed = fields.ReadFloat(AwaySpeedItem, defaults.AwaySpeed);
            try
            {
                return new WallKickTuning(enabled, upSpeed, awaySpeed);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, WallKickItemNames, e);
            }
        }

        private static LandingRollTuning ReadLandingRoll(ServerDataJson.Fields fields)
        {
            var defaults = LandingRollTuning.Default;
            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var windowSeconds = fields.ReadFloat(WindowSecondsItem, defaults.WindowSeconds);
            var fallSpeedScale = fields.ReadFloat(FallSpeedScaleItem, defaults.FallSpeedScale);
            var rollSeconds = fields.ReadFloat(RollSecondsItem, defaults.RollSeconds);
            var minFallSpeed = fields.ReadFloat(MinFallSpeedItem, defaults.MinFallSpeed);
            try
            {
                return new LandingRollTuning(enabled, windowSeconds, fallSpeedScale, rollSeconds, minFallSpeed);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, LandingRollItemNames, e);
            }
        }

        private static WallClimbTuning ReadWallClimb(ServerDataJson.Fields fields)
        {
            var defaults = WallClimbTuning.Default;
            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var minSpeed = fields.ReadFloat(MinSpeedItem, defaults.MinSpeed);
            var climbSpeed = fields.ReadFloat(ClimbSpeedItem, defaults.ClimbSpeed);
            var maxSeconds = fields.ReadFloat(MaxSecondsItem, defaults.MaxSeconds);
            var maxHeight = fields.ReadFloat(MaxHeightItem, defaults.MaxHeight);
            var pushOffSpeed = fields.ReadFloat(PushOffSpeedItem, defaults.PushOffSpeed);
            var mantleAtLedge = fields.ReadBool(MantleAtLedgeItem, defaults.MantleAtLedge);
            try
            {
                return new WallClimbTuning(enabled, minSpeed, climbSpeed, maxSeconds, maxHeight, pushOffSpeed, mantleAtLedge);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, WallClimbItemNames, e);
            }
        }

        private static LedgeHangTuning ReadLedgeHang(ServerDataJson.Fields fields)
        {
            var defaults = LedgeHangTuning.Default;
            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var grabReach = fields.ReadFloat(GrabReachItem, defaults.GrabReach);
            var shimmySpeed = fields.ReadFloat(ShimmySpeedItem, defaults.ShimmySpeed);
            var climbUpSpeed = fields.ReadFloat(ClimbUpSpeedItem, defaults.ClimbUpSpeed);
            var regrabSeconds = fields.ReadFloat(RegrabSecondsItem, defaults.RegrabSeconds);
            try
            {
                return new LedgeHangTuning(enabled, grabReach, shimmySpeed, climbUpSpeed, regrabSeconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, LedgeHangItemNames, e);
            }
        }

        private static MomentumTuning ReadMomentum(ServerDataJson.Fields fields)
        {
            var defaults = MomentumTuning.Default;
            var slideJumpBoost = fields.ReadFloat(SlideJumpBoostItem, defaults.SlideJumpBoost);
            var vaultExitSpeedKeep = fields.ReadFloat(VaultExitSpeedKeepItem, defaults.VaultExitSpeedKeep);
            try
            {
                return new MomentumTuning(slideJumpBoost, vaultExitSpeedKeep);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, MomentumItemNames, e);
            }
        }

        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> known, ArgumentException e)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    return fields.Rejected(fields.ItemPath(name), e.Message, e);
                }
            }

            return fields.Rejected(fields.Path, e.Message, e);
        }
    }
}
