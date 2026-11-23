using System.Numerics;
using Shooter.Core;

namespace Shooter.NetV2
{
    public readonly struct RemotePose
    {
        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId)
            : this(position, yaw, stance, weaponId, 0f, false, WallRunSide.None)
        {
        }

        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId, float lean, bool swimming, WallRunSide wallRun)
            : this(position, yaw, stance, weaponId, lean, swimming, wallRun, false)
        {
        }

        public RemotePose(
            Vector3 position, float yaw, PlayerStance stance, string weaponId, float lean, bool swimming, WallRunSide wallRun, bool parachuting)
            : this(position, yaw, stance, weaponId, lean, swimming, wallRun, parachuting, ParkourMove.None)
        {
        }

        public RemotePose(
            Vector3 position,
            float yaw,
            PlayerStance stance,
            string weaponId,
            float lean,
            bool swimming,
            WallRunSide wallRun,
            bool parachuting,
            ParkourMove parkour)
            : this(position, yaw, stance, weaponId, lean, swimming, wallRun, parachuting, parkour, 0f)
        {
        }

        public RemotePose(
            Vector3 position,
            float yaw,
            PlayerStance stance,
            string weaponId,
            float lean,
            bool swimming,
            WallRunSide wallRun,
            bool parachuting,
            ParkourMove parkour,
            float holsterLowering)
            : this(position, yaw, stance, weaponId, lean, swimming, wallRun, parachuting, parkour, holsterLowering, false)
        {
        }

        public RemotePose(
            Vector3 position,
            float yaw,
            PlayerStance stance,
            string weaponId,
            float lean,
            bool swimming,
            WallRunSide wallRun,
            bool parachuting,
            ParkourMove parkour,
            float holsterLowering,
            bool weaponLightOn)
        {
            Position = position;
            Yaw = yaw;
            Stance = stance;
            WeaponId = weaponId ?? "";

            // 見た目の傾け (PlayerMockPose.TraversalLean) は範囲の外と NaN で投げるので、届いた値はここで収める
            Lean = float.IsNaN(lean) ? 0f : (lean < -1f ? -1f : (lean > 1f ? 1f : lean));
            Swimming = swimming;
            WallRun = wallRun <= WallRunSide.Right ? wallRun : WallRunSide.None;
            Parachuting = parachuting;

            // 壁走りの向きと同じく、見た目が知らない動きの番号で迷わないよう、範囲の外は何もしていないことにする
            Parkour = parkour <= ParkourMove.LedgeClimbUp ? parkour : ParkourMove.None;

            // リーンと同じく、見た目の下げ (PlayerMockPose の Lowered) が範囲の外で迷わないよう、届いた値はここで収める
            HolsterLowering = float.IsNaN(holsterLowering) ? 0f : (holsterLowering < 0f ? 0f : (holsterLowering > 1f ? 1f : holsterLowering));
            WeaponLightOn = weaponLightOn;
        }

        public Vector3 Position { get; }

        public float Yaw { get; }

        public PlayerStance Stance { get; }

        public bool IsCrouched => Stance == PlayerStance.Crouching;

        public bool IsProne => Stance == PlayerStance.Prone;

        public string WeaponId { get; }

        public float Lean { get; }

        public bool Swimming { get; }

        public WallRunSide WallRun { get; }

        public bool Parachuting { get; }

        public ParkourMove Parkour { get; }

        // 体の銃をどれだけ下げるか。0 が構えた状態、1 がしまい切った状態 (Loadout.SwitchLowering と同じ向き)
        public float HolsterLowering { get; }

        // しまい切った体は銃を持たないので、オフラインの WeaponController.Holster と同じく銃を隠す
        public bool IsWeaponHidden => HolsterLowering >= 1f;

        // 体に持たせて見せる武器。しまい切っていれば空。WeaponId はしまっていても持っている武器のまま残す
        public string ShownWeaponId => IsWeaponHidden ? "" : WeaponId;

        // 点灯の条件 (ライトが付いているか・しまっているか) はサーバー (ServerWeaponLight) が確かめて送るので、ここは届いた値をそのまま持つ
        public bool WeaponLightOn { get; }

        // 銃を隠している間に光だけが残らないよう、見せる光は銃が見えている時に限る
        public bool ShowsWeaponLight => WeaponLightOn && !IsWeaponHidden && WeaponId.Length > 0;

        // 伏せとしゃがみはサーバーが同時に立てないので、伏せを先に見る。
        public static RemotePose From(in Shooter.Protocol.PlayerSnapshot row)
        {
            var stance = row.IsProne ? PlayerStance.Prone : row.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing;
            return new RemotePose(
                row.Position,
                row.Yaw,
                stance,
                row.WeaponId,
                row.Lean,
                row.Swimming,
                (WallRunSide)(byte)row.WallRunSide,
                row.IsParachuting,
                (ParkourMove)(byte)row.ParkourMove,
                row.HolsterLowering,
                row.WeaponLightOn);
        }
    }
}
