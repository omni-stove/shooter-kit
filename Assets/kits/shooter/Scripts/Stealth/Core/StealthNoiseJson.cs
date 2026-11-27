using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class StealthNoiseJson
    {
        public const string FootstepNoiseRadiusItem = "footstepNoiseRadius";
        public const string WalkStrideItem = "walkStride";
        public const string SprintStrideItem = "sprintStride";
        public const string CrouchStrideItem = "crouchStride";
        public const string WalkVolumeItem = "walkVolume";
        public const string SprintVolumeItem = "sprintVolume";
        public const string CrouchVolumeItem = "crouchVolume";
        public const string MinLandingFallSpeedItem = "minLandingFallSpeed";
        public const string HardLandingFallSpeedItem = "hardLandingFallSpeed";
        public const string MinLandingVolumeItem = "minLandingVolume";
        public const string ArmedGainFactorItem = "armedGainFactor";
        public const string LitGainFactorItem = "litGainFactor";
        public const string SecurityCameraItem = "securityCamera";
        public const string AlarmItem = "alarm";

        public const string SweepDegreesItem = "sweepDegrees";
        public const string SweepDegreesPerSecondItem = "sweepDegreesPerSecond";
        public const string EdgePauseSecondsItem = "edgePauseSeconds";
        public const string ViewDistanceItem = "viewDistance";
        public const string FieldOfViewDegreesItem = "fieldOfViewDegrees";
        public const string AwarenessItem = "awareness";
        public const string DisableHoldSecondsItem = "disableHoldSeconds";
        public const string DisabledSecondsItem = "disabledSeconds";
        public const string MaxHealthItem = "maxHealth";
        public const string DisableRadiusItem = "disableRadius";

        public const string DurationSecondsItem = "durationSeconds";
        public const string RadiusItem = "radius";

        public const string GainPerSecondItem = "gainPerSecond";
        public const string FullGainDistanceItem = "fullGainDistance";
        public const string FarGainFactorItem = "farGainFactor";
        public const string PeripheralGainFactorItem = "peripheralGainFactor";
        public const string CrouchGainFactorItem = "crouchGainFactor";
        public const string ProneGainFactorItem = "proneGainFactor";
        public const string StillGainFactorItem = "stillGainFactor";
        public const string FullMotionSpeedItem = "fullMotionSpeed";
        public const string DecayDelaySecondsItem = "decayDelaySeconds";
        public const string DecayPerSecondItem = "decayPerSecond";
        public const string NoticeThresholdItem = "noticeThreshold";
        public const string AlertThresholdItem = "alertThreshold";
        public const string AlarmRadiusItem = "alarmRadius";
        public const string EngagedHoldThresholdItem = "engagedHoldThreshold";
        public const string GunshotGainItem = "gunshotGain";
        public const string FootstepGainItem = "footstepGain";
        public const string InvestigateLevelItem = "investigateLevel";

        private const string _kind = "stealth JSON";

        // 項目名は StealthNoiseSpec と AwarenessSpec のコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName からそのまま示せるようにする
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FootstepNoiseRadiusItem,
            WalkStrideItem,
            SprintStrideItem,
            CrouchStrideItem,
            WalkVolumeItem,
            SprintVolumeItem,
            CrouchVolumeItem,
            MinLandingFallSpeedItem,
            HardLandingFallSpeedItem,
            MinLandingVolumeItem,
            ArmedGainFactorItem,
            LitGainFactorItem,
        };

        // ItemNames に入れないのは、ItemNames が ParamName を項目名へ戻す一覧で、入れ子の項目はその中の一覧で戻すため。
        public static IReadOnlyList<string> TopLevelItemNames { get; } = Concat(ItemNames, SecurityCameraItem, AlarmItem);

        // 項目名は SecurityCameraSpec のコンストラクターの引数名にそろえる。
        public static IReadOnlyList<string> CameraItemNames { get; } = new[]
        {
            SweepDegreesItem,
            SweepDegreesPerSecondItem,
            EdgePauseSecondsItem,
            ViewDistanceItem,
            FieldOfViewDegreesItem,
            AwarenessItem,
            DisableHoldSecondsItem,
            DisabledSecondsItem,
            MaxHealthItem,
            DisableRadiusItem,
        };

        // 視野を入れないのは、カメラの視野 (securityCamera.fieldOfViewDegrees) をそのまま気づきの視野にするため。
        public static IReadOnlyList<string> CameraAwarenessItemNames { get; } = new[]
        {
            GainPerSecondItem,
            FullGainDistanceItem,
            FarGainFactorItem,
            PeripheralGainFactorItem,
            CrouchGainFactorItem,
            ProneGainFactorItem,
            StillGainFactorItem,
            FullMotionSpeedItem,
            DecayDelaySecondsItem,
            DecayPerSecondItem,
            NoticeThresholdItem,
            AlertThresholdItem,
            AlarmRadiusItem,
            EngagedHoldThresholdItem,
            GunshotGainItem,
            FootstepGainItem,
            InvestigateLevelItem,
            ArmedGainFactorItem,
            LitGainFactorItem,
        };

        public static IReadOnlyList<string> AlarmItemNames { get; } = new[]
        {
            DurationSecondsItem,
            RadiusItem,
        };

        public static StealthNoiseSpec Parse(string text, string sourceName)
        {
            return ParseTuning(text, sourceName).Noise;
        }

        public static StealthTuningSpec ParseTuning(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);

            // KitTuning は TuningJsonException だけを受けて既定値で続けるので、ServerDataJson の例外をここで包み直す
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, TopLevelItemNames);
                var defaults = StealthNoiseSpec.Default;
                var radius = fields.ReadFloat(FootstepNoiseRadiusItem, defaults.FootstepNoiseRadius);
                var walkStride = fields.ReadFloat(WalkStrideItem, defaults.WalkStride);
                var sprintStride = fields.ReadFloat(SprintStrideItem, defaults.SprintStride);
                var crouchStride = fields.ReadFloat(CrouchStrideItem, defaults.CrouchStride);
                var walkVolume = fields.ReadFloat(WalkVolumeItem, defaults.WalkVolume);
                var sprintVolume = fields.ReadFloat(SprintVolumeItem, defaults.SprintVolume);
                var crouchVolume = fields.ReadFloat(CrouchVolumeItem, defaults.CrouchVolume);
                var minLandingFallSpeed = fields.ReadFloat(MinLandingFallSpeedItem, defaults.MinLandingFallSpeed);
                var hardLandingFallSpeed = fields.ReadFloat(HardLandingFallSpeedItem, defaults.HardLandingFallSpeed);
                var minLandingVolume = fields.ReadFloat(MinLandingVolumeItem, defaults.MinLandingVolume);
                var armedNode = fields.Find(ArmedGainFactorItem);
                float? armedGainFactor = armedNode == null ? (float?)null : fields.ToFloat(ArmedGainFactorItem, armedNode);
                var litNode = fields.Find(LitGainFactorItem);
                float? litGainFactor = litNode == null ? (float?)null : fields.ToFloat(LitGainFactorItem, litNode);
                var camera = ReadCamera(fields.Object(SecurityCameraItem, CameraItemNames));
                var alarm = ReadAlarm(fields.Object(AlarmItem, AlarmItemNames));
                try
                {
                    var noise = new StealthNoiseSpec(
                        radius,
                        walkStride,
                        sprintStride,
                        crouchStride,
                        walkVolume,
                        sprintVolume,
                        crouchVolume,
                        minLandingFallSpeed,
                        hardLandingFallSpeed,
                        minLandingVolume);
                    return new StealthTuningSpec(noise, armedGainFactor, litGainFactor, camera, alarm);
                }
                catch (ArgumentException e)
                {
                    var item = e.ParamName != null && Contains(ItemNames, e.ParamName) ? e.ParamName : null;
                    throw fields.Rejected(item, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static SecurityCameraSpec ReadCamera(ServerDataJson.Fields camera)
        {
            if (camera == null)
            {
                return null;
            }

            var defaults = SecurityCameraSpec.Default;
            var sweepDegrees = camera.ReadFloat(SweepDegreesItem, defaults.SweepDegrees);
            var sweepDegreesPerSecond = camera.ReadFloat(SweepDegreesPerSecondItem, defaults.SweepDegreesPerSecond);
            var edgePauseSeconds = camera.ReadFloat(EdgePauseSecondsItem, defaults.EdgePauseSeconds);
            var viewDistance = camera.ReadFloat(ViewDistanceItem, defaults.ViewDistance);
            var fieldOfViewDegrees = camera.ReadFloat(FieldOfViewDegreesItem, defaults.FieldOfViewDegrees);
            var disableHoldSeconds = camera.ReadFloat(DisableHoldSecondsItem, defaults.DisableHoldSeconds);
            var disabledSeconds = camera.ReadFloat(DisabledSecondsItem, defaults.DisabledSeconds);
            var maxHealth = camera.ReadInt(MaxHealthItem, defaults.MaxHealth);
            var disableRadius = camera.ReadFloat(DisableRadiusItem, defaults.DisableRadius);
            var awarenessFields = camera.Object(AwarenessItem, CameraAwarenessItemNames);

            AwarenessSpec awareness;
            try
            {
                awareness = ReadCameraAwareness(awarenessFields, fieldOfViewDegrees);
            }
            catch (ArgumentException e)
            {
                // 視野はカメラの項目から渡すので、気づきの値の検査で断られてもカメラの項目名で示す。
                if (string.Equals(e.ParamName, FieldOfViewDegreesItem, StringComparison.Ordinal))
                {
                    throw camera.Rejected(camera.ItemPath(FieldOfViewDegreesItem), e.Message, e);
                }

                var item = awarenessFields != null && e.ParamName != null && Contains(CameraAwarenessItemNames, e.ParamName)
                    ? awarenessFields.ItemPath(e.ParamName)
                    : camera.ItemPath(AwarenessItem);
                throw camera.Rejected(item, e.Message, e);
            }

            try
            {
                return new SecurityCameraSpec(
                    sweepDegrees,
                    sweepDegreesPerSecond,
                    edgePauseSeconds,
                    viewDistance,
                    fieldOfViewDegrees,
                    awareness,
                    disableHoldSeconds,
                    disabledSeconds,
                    maxHealth,
                    disableRadius);
            }
            catch (ArgumentException e)
            {
                var item = e.ParamName != null && Contains(CameraItemNames, e.ParamName) ? camera.ItemPath(e.ParamName) : camera.Path;
                throw camera.Rejected(item, e.Message, e);
            }
        }

        private static AwarenessSpec ReadCameraAwareness(ServerDataJson.Fields awareness, float fieldOfViewDegrees)
        {
            if (awareness == null)
            {
                return new AwarenessSpec(fieldOfViewDegrees: fieldOfViewDegrees);
            }

            return new AwarenessSpec(
                fieldOfViewDegrees: fieldOfViewDegrees,
                gainPerSecond: awareness.ReadFloat(GainPerSecondItem, AwarenessSpec.DefaultGainPerSecond),
                fullGainDistance: awareness.ReadFloat(FullGainDistanceItem, AwarenessSpec.DefaultFullGainDistance),
                farGainFactor: awareness.ReadFloat(FarGainFactorItem, AwarenessSpec.DefaultFarGainFactor),
                peripheralGainFactor: awareness.ReadFloat(PeripheralGainFactorItem, AwarenessSpec.DefaultPeripheralGainFactor),
                crouchGainFactor: awareness.ReadFloat(CrouchGainFactorItem, AwarenessSpec.DefaultCrouchGainFactor),
                proneGainFactor: awareness.ReadFloat(ProneGainFactorItem, AwarenessSpec.DefaultProneGainFactor),
                stillGainFactor: awareness.ReadFloat(StillGainFactorItem, AwarenessSpec.DefaultStillGainFactor),
                fullMotionSpeed: awareness.ReadFloat(FullMotionSpeedItem, AwarenessSpec.DefaultFullMotionSpeed),
                decayDelaySeconds: awareness.ReadFloat(DecayDelaySecondsItem, AwarenessSpec.DefaultDecayDelaySeconds),
                decayPerSecond: awareness.ReadFloat(DecayPerSecondItem, AwarenessSpec.DefaultDecayPerSecond),
                noticeThreshold: awareness.ReadFloat(NoticeThresholdItem, AwarenessSpec.DefaultNoticeThreshold),
                alertThreshold: awareness.ReadFloat(AlertThresholdItem, AwarenessSpec.DefaultAlertThreshold),
                alarmRadius: awareness.ReadFloat(AlarmRadiusItem, AwarenessSpec.DefaultAlarmRadius),
                engagedHoldThreshold: awareness.ReadFloat(EngagedHoldThresholdItem, AwarenessSpec.DefaultEngagedHoldThreshold),
                gunshotGain: awareness.ReadFloat(GunshotGainItem, AwarenessSpec.DefaultGunshotGain),
                footstepGain: awareness.ReadFloat(FootstepGainItem, AwarenessSpec.DefaultFootstepGain),
                investigateLevel: awareness.ReadFloat(InvestigateLevelItem, AwarenessSpec.DefaultInvestigateLevel),
                armedGainFactor: awareness.ReadFloat(ArmedGainFactorItem, AwarenessSpec.DefaultArmedGainFactor),
                litGainFactor: awareness.ReadFloat(LitGainFactorItem, AwarenessSpec.DefaultLitGainFactor));
        }

        private static SecurityAlarmSpec ReadAlarm(ServerDataJson.Fields alarm)
        {
            if (alarm == null)
            {
                return null;
            }

            var defaults = SecurityAlarmSpec.Default;
            var durationSeconds = alarm.ReadFloat(DurationSecondsItem, defaults.DurationSeconds);
            var radius = alarm.ReadFloat(RadiusItem, defaults.Radius);
            try
            {
                return new SecurityAlarmSpec(durationSeconds, radius);
            }
            catch (ArgumentException e)
            {
                var item = e.ParamName != null && Contains(AlarmItemNames, e.ParamName) ? alarm.ItemPath(e.ParamName) : alarm.Path;
                throw alarm.Rejected(item, e.Message, e);
            }
        }

        private static string[] Concat(IReadOnlyList<string> names, params string[] more)
        {
            var all = new string[names.Count + more.Length];
            for (var i = 0; i < names.Count; i++)
            {
                all[i] = names[i];
            }

            more.CopyTo(all, names.Count);
            return all;
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
