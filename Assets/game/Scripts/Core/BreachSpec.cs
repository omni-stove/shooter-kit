using System;

namespace Shooter.Core
{
    public sealed class BreachSpec
    {
        // 足を上げてから板に当たるまで。押した瞬間に開くと、蹴った手応えが無い。
        public const float DefaultKickSeconds = 0.6f;

        // 撃たれれば取りやめになるので、敵の前で悠長に仕掛けられない長さにする。
        public const float DefaultPlantSeconds = 2.5f;

        public const float DefaultFuseSeconds = 2f;

        public const float DefaultReach = 1.5f;

        public const float DefaultRoomDepth = 5f;

        public const float DefaultRoomWidth = 4f;

        public const float DefaultRoomHeight = 2.5f;

        public const float DefaultKickStaggerSeconds = 1.5f;

        public const float DefaultChargeStaggerSeconds = 3f;

        // 敵の最大体力 (100) に届かせず、怯ませて撃ち込む間を作るだけにする。
        public const int DefaultChargeDamage = 40;

        // 届く距離 (1.5) と爆発までの秒数で、仕掛けた人がドアから離れれば巻き込まれない広さにする。
        public const float DefaultChargeRadius = 2.5f;

        public const float DefaultKickSoundRadius = 25f;

        public const float DefaultChargeSoundRadius = 60f;

        public const float DefaultSlowMotionScale = 0.35f;

        public const float DefaultSlowMotionSeconds = 1.5f;

        // 空なら品が無くても仕掛けられる。キットの在庫には突入用の爆薬が無いので、既定では品を求めない。
        public const string DefaultChargeItemId = "";

        // 蹴るつもりの短い押しが仕掛けに化けず、仕掛けるつもりの押しで待たされない長さにする。
        public const float DefaultHoldSeconds = 0.35f;

        public static BreachSpec Default { get; } = new BreachSpec();

        public BreachSpec(
            float kickSeconds = DefaultKickSeconds,
            float plantSeconds = DefaultPlantSeconds,
            float fuseSeconds = DefaultFuseSeconds,
            float reach = DefaultReach,
            float roomDepth = DefaultRoomDepth,
            float roomWidth = DefaultRoomWidth,
            float roomHeight = DefaultRoomHeight,
            float kickStaggerSeconds = DefaultKickStaggerSeconds,
            float chargeStaggerSeconds = DefaultChargeStaggerSeconds,
            int chargeDamage = DefaultChargeDamage,
            float chargeRadius = DefaultChargeRadius,
            float kickSoundRadius = DefaultKickSoundRadius,
            float chargeSoundRadius = DefaultChargeSoundRadius,
            float slowMotionScale = DefaultSlowMotionScale,
            float slowMotionSeconds = DefaultSlowMotionSeconds,
            string chargeItemId = DefaultChargeItemId,
            float holdSeconds = DefaultHoldSeconds)
        {
            KickSeconds = SpecGuard.NonNegativeFinite(kickSeconds, nameof(kickSeconds));
            PlantSeconds = SpecGuard.NonNegativeFinite(plantSeconds, nameof(plantSeconds));
            FuseSeconds = SpecGuard.NonNegativeFinite(fuseSeconds, nameof(fuseSeconds));
            Reach = SpecGuard.PositiveFinite(reach, nameof(reach));
            RoomDepth = SpecGuard.PositiveFinite(roomDepth, nameof(roomDepth));
            RoomWidth = SpecGuard.PositiveFinite(roomWidth, nameof(roomWidth));
            RoomHeight = SpecGuard.PositiveFinite(roomHeight, nameof(roomHeight));
            KickStaggerSeconds = SpecGuard.NonNegativeFinite(kickStaggerSeconds, nameof(kickStaggerSeconds));
            ChargeStaggerSeconds = SpecGuard.NonNegativeFinite(chargeStaggerSeconds, nameof(chargeStaggerSeconds));
            ChargeDamage = SpecGuard.NonNegative(chargeDamage, nameof(chargeDamage));
            ChargeRadius = SpecGuard.NonNegativeFinite(chargeRadius, nameof(chargeRadius));
            KickSoundRadius = SpecGuard.NonNegativeFinite(kickSoundRadius, nameof(kickSoundRadius));
            ChargeSoundRadius = SpecGuard.NonNegativeFinite(chargeSoundRadius, nameof(chargeSoundRadius));

            // TimeScaleArbitration が 0 と 1 より大きい倍率を断るので、ここで先に断って項目名を付ける。
            if (!(slowMotionScale > 0f && slowMotionScale <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(slowMotionScale), slowMotionScale, "slowMotionScale must be greater than 0 and at most 1");
            }

            SlowMotionScale = slowMotionScale;
            SlowMotionSeconds = SpecGuard.NonNegativeFinite(slowMotionSeconds, nameof(slowMotionSeconds));
            var itemId = SpecGuard.ReferenceId(chargeItemId);
            ChargeItemId = itemId.Length == 0 ? string.Empty : SpecGuard.LowercaseUuid(itemId, nameof(chargeItemId));
            HoldSeconds = SpecGuard.PositiveFinite(holdSeconds, nameof(holdSeconds));
        }

        public float KickSeconds { get; }

        public float PlantSeconds { get; }

        public float FuseSeconds { get; }

        public float Reach { get; }

        public float RoomDepth { get; }

        public float RoomWidth { get; }

        public float RoomHeight { get; }

        public float KickStaggerSeconds { get; }

        public float ChargeStaggerSeconds { get; }

        public int ChargeDamage { get; }

        public float ChargeRadius { get; }

        public float KickSoundRadius { get; }

        public float ChargeSoundRadius { get; }

        public float SlowMotionScale { get; }

        public float SlowMotionSeconds { get; }

        public string ChargeItemId { get; }

        public float HoldSeconds { get; }

        public bool NeedsChargeItem => ChargeItemId.Length > 0;

        public bool SlowsMotion => SlowMotionSeconds > 0f && SlowMotionScale < 1f;

        public float StaggerSecondsFor(BreachMethod method)
        {
            switch (SpecGuard.Defined(method, nameof(method)))
            {
                case BreachMethod.Kick:
                    return KickStaggerSeconds;
                default:
                    return ChargeStaggerSeconds;
            }
        }

        // 蹴りはよろけ、爆薬は体勢を崩す。種類はアニメの値に出るので、近接と爆発の当たりと同じ見た目に揃える。
        public static StaggerKind StaggerKindFor(BreachMethod method)
        {
            switch (SpecGuard.Defined(method, nameof(method)))
            {
                case BreachMethod.Kick:
                    return StaggerKind.Stumble;
                default:
                    return StaggerKind.Blast;
            }
        }

        public float SoundRadiusFor(BreachMethod method)
        {
            switch (SpecGuard.Defined(method, nameof(method)))
            {
                case BreachMethod.Kick:
                    return KickSoundRadius;
                default:
                    return ChargeSoundRadius;
            }
        }
    }
}
