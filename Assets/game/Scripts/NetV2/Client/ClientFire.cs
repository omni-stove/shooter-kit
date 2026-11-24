using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public enum FireDecision
    {
        Hold,
        Fire,
        Reload,
    }

    // 撃てるか (弾・間隔・当たり) はサーバーが決めるが、サーバーが捨てると分かっている射撃は送らない。
    public static class ClientFire
    {
        public static FireDecision Decide(SnapshotMessage latest, int playerId, in CharacterMotorState predicted)
        {
            return Decide(latest, playerId, predicted, null);
        }

        // spec が null でも決められるようにするのは、武器の値がまだ引けないときも弾倉の残りで今までどおり決めるため。
        public static FireDecision Decide(SnapshotMessage latest, int playerId, in CharacterMotorState predicted, WeaponSpec spec)
        {
            return Decide(latest, playerId, predicted, spec, false);
        }

        public static FireDecision Decide(
            SnapshotMessage latest, int playerId, in CharacterMotorState predicted, WeaponSpec spec, bool firesVehicleWeapon)
        {
            return Decide(latest, playerId, predicted, spec, firesVehicleWeapon, AkimboHand.Right);
        }

        // 二丁持ちの手は、その手の弾倉とリロードで決める (サーバーの ServerAkimbo.TryFire と同じ)。右手は WeaponSync の弾倉とリロード、左手は Offhand* を見る。
        // 左手は二丁持ちの武器でしか撃てず (サーバーが NotAkimbo で断る)、座席の武器も二丁持ちにならないので、座席の武器を撃つ間も頼まない。
        public static FireDecision Decide(
            SnapshotMessage latest, int playerId, in CharacterMotorState predicted, WeaponSpec spec, bool firesVehicleWeapon, AkimboHand hand)
        {
            RequireHand(hand);
            var left = hand == AkimboHand.Left;
            if (left && (spec == null || !spec.IsAkimbo || firesVehicleWeapon))
            {
                return FireDecision.Hold;
            }

            if (latest == null || !latest.HasOwner)
            {
                return FireDecision.Hold;
            }

            var own = latest.Players.IndexOf(playerId);
            if (own < 0)
            {
                return FireDecision.Hold;
            }

            // しまっている・しまう途中・構える途中はサーバー (ServerHolster) が断るので、射撃もリロードも頼まない。押した射撃は NetV2HolsterPart が構える要求にする。
            // サーバーが断るのは手の武器だけなので、乗り物の武器の席では止めない
            var row = latest.Players[own];
            if (row.Health <= 0 || row.Downed.IsDowned || (row.HolsterPhase != HolsterPhase.Drawn && !firesVehicleWeapon)
                || predicted.IsDodging || predicted.IsVaulting || predicted.IsDiving || predicted.Ladder.IsClimbing
                || predicted.IsHangingOnLedge)
            {
                return FireDecision.Hold;
            }

            var weapon = latest.Owner.Weapon;
            var magazine = left ? weapon.OffhandAmmoInMagazine : weapon.AmmoInMagazine;
            var reloading = left ? weapon.IsOffhandReloading : weapon.IsReloading;

            // 1 発ずつ込める武器は、込めた弾があればサーバーがリロードを止めて撃たせるので、リロード中でも送る。
            var firesWhileReloading = spec != null && spec.Ammo.ReloadStyle == ReloadStyle.PerRound && magazine > 0;
            if (CurrentWeaponId(latest.Owner) == null || (reloading && !firesWhileReloading) || weapon.IsSwitching)
            {
                return FireDecision.Hold;
            }

            // 詰まっている間はサーバー (ServerWeapon) が断るので頼まない。弾倉が空でもリロードを頼まないのは、解除はリロードのキーで始めるため。
            if (weapon.IsJammed)
            {
                return FireDecision.Hold;
            }

            // 熱で撃つ武器は弾倉を持たずリロードもしないので、オーバーヒートして冷め切るまでは頼まずに待つ。
            if (spec != null && spec.Ammo.Kind == AmmoKind.Heat)
            {
                return weapon.IsOverheated || weapon.Heat >= 1f ? FireDecision.Hold : FireDecision.Fire;
            }

            return magazine > 0 ? FireDecision.Fire : FireDecision.Reload;
        }

        // 弾薬の種類はサーバーが選んだ番号を返す。手元で選び直すと、切り替えの要求が届く前の弾がずれた種類で送られるため。
        public static void Fill(
            FireRequestMessage request, OwnerSnapshot owner, string weaponId, in TriggerPull pull, TriggerEdge edge, int? lockTargetId)
        {
            Fill(request, owner, weaponId, pull, edge, lockTargetId, AkimboHand.Right);
        }

        // 要求は使い回すので、撃った手も毎回書き直す。前の左手の射撃の手が次の右手の射撃に残らないように。
        public static void Fill(
            FireRequestMessage request, OwnerSnapshot owner, string weaponId, in TriggerPull pull, TriggerEdge edge, int? lockTargetId, AkimboHand hand)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RequireHand(hand);

            request.WeaponId = weaponId ?? "";
            request.ChargeFraction = Math.Min(1f, Math.Max(0f, pull.ChargeFraction));
            request.AmmoTypeIndex = owner != null ? owner.Weapon.AmmoTypeIndex : (byte)0;
            request.HasLockTarget = lockTargetId.HasValue;
            request.LockTargetId = lockTargetId.HasValue ? ClientLockTargetIds.WireIdOf(lockTargetId.Value) : 0;
            request.LockTargetKind = lockTargetId.HasValue ? ClientLockTargetIds.KindOf(lockTargetId.Value) : LockTargetKind.Player;
            request.Trigger = edge;
            request.Hand = hand;
        }

        private static void RequireHand(AkimboHand hand)
        {
            if (hand != AkimboHand.Right && hand != AkimboHand.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        // 弾薬の種類の番号は 0 が武器そのものの弾、1 からが WeaponSpec.AmmoTypes の番号 + 1 (サーバーの ServerPlayerWeapons.AmmoTypeIndex と同じ)。
        public static RoundSpec CurrentRound(OwnerSnapshot owner, WeaponSpec spec)
        {
            if (owner == null || spec == null)
            {
                return null;
            }

            var index = owner.Weapon.AmmoTypeIndex;
            var types = spec.AmmoTypes;
            return index > 0 && index <= types.Count ? types[index - 1].RoundOr(spec.Round) : spec.Round;
        }

        public static string CurrentWeaponId(OwnerSnapshot owner)
        {
            if (owner == null)
            {
                return null;
            }

            var index = owner.Weapon.EquippedIndex;
            var ids = owner.EquippedWeaponIds;
            return owner.Weapon.WeaponCount > 0 && index < ids.Count && !string.IsNullOrEmpty(ids[index]) ? ids[index] : null;
        }
    }
}
