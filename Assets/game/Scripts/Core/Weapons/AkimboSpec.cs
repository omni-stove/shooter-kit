namespace Shooter.Core
{
    // 同じ武器を左右の手に 1 丁ずつ持つときの部品。部品を別にするのは、二丁持ちの重さを武器そのものの値と分けて調整するため。
    public sealed class AkimboSpec
    {
        public const float DefaultSpreadMultiplier = 1.5f;

        public const bool DefaultBlocksAds = true;

        public const float DefaultReloadSecondsMultiplier = 1f;

        public const float DefaultMoveSpeedMultiplier = 1f;

        // 0 にしないのは、両手で同じ tick に撃つと 2 発が 1 発に重なって見えるため。
        public const float DefaultOffhandShotDelaySeconds = 0.05f;

        public static readonly AkimboSpec Default = new AkimboSpec();

        public AkimboSpec(
            float spreadMultiplier = DefaultSpreadMultiplier,
            bool blocksAds = DefaultBlocksAds,
            float reloadSecondsMultiplier = DefaultReloadSecondsMultiplier,
            float moveSpeedMultiplier = DefaultMoveSpeedMultiplier,
            float offhandShotDelaySeconds = DefaultOffhandShotDelaySeconds)
        {
            SpreadMultiplier = SpecGuard.NonNegativeFinite(spreadMultiplier, nameof(spreadMultiplier));
            BlocksAds = blocksAds;
            // 0 を受け付けないのは、1 発ずつ込める武器では 1 発の秒数が 0 になり、AmmoSpec が Whole と見分けがつかないとして弾くため。
            ReloadSecondsMultiplier = SpecGuard.PositiveFinite(reloadSecondsMultiplier, nameof(reloadSecondsMultiplier));
            MoveSpeedMultiplier = SpecGuard.NonNegativeFinite(moveSpeedMultiplier, nameof(moveSpeedMultiplier));
            OffhandShotDelaySeconds = SpecGuard.NonNegativeFinite(offhandShotDelaySeconds, nameof(offhandShotDelaySeconds));
        }

        public float SpreadMultiplier { get; }

        public bool BlocksAds { get; }

        // 1 発ずつ込める武器は 1 発の秒数にも掛ける。全体の秒数だけに掛けると、その武器では倍率が効かない。
        public float ReloadSecondsMultiplier { get; }

        // 武器の MoveSpeedMultiplier に重ねて掛ける。武器の値を書き換えないのは、1 丁だけ持つときの速さを変えないため。
        public float MoveSpeedMultiplier { get; }

        public float OffhandShotDelaySeconds { get; }
    }
}
