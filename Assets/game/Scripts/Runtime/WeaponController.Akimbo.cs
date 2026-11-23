using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 持っている武器の Spec.IsAkimbo のときは、左右の手 (AkimboHands) で撃ち、込める。攻撃のボタンが右手、覗き込みのボタンが左手。
    // 持ち武器の並び (Loadout) の Weapon は持ち替え・セーブ・拾うための入れ物のままにし、二丁持ちの間の弾倉と予備弾は手の組で数える。
    // 並びの Weapon を右手にしないのは、手の Weapon はリロードの秒数に倍率を掛けた別の Spec で作るため。
    public sealed partial class WeaponController
    {
        // 並びを作り直しても同じ武器の組を使えるよう、並びの Weapon ごとに持つ。並びにない武器の組は作り直しのときに捨てる。
        private readonly Dictionary<Weapon, AkimboHands> _akimboHands = new Dictionary<Weapon, AkimboHands>();
        private readonly TriggerDriver _offhandTrigger = new TriggerDriver();
        private AkimboHands _offhandHands;
        private float _offhandDelayRemaining;
        private bool _offhandPending;

        // 二丁持ちでない武器では出さず、Fired だけを出す。撃った手を見ない受け手が今の Fired のまま使えるように。
        public event Action<Weapon, AkimboHand> FiredHand;

        public AkimboHands Akimbo
        {
            get
            {
                var weapon = Weapon;
                return weapon != null && _akimboHands.TryGetValue(weapon, out var hands) ? hands : null;
            }
        }

        public bool IsAkimbo => Akimbo != null;

        public bool IsOffhandShotDelayed => _offhandPending;

        public float AkimboMoveSpeedMultiplier
        {
            get
            {
                var hands = Akimbo;
                return hands != null ? hands.Akimbo.MoveSpeedMultiplier : 1f;
            }
        }

        // 同じフレームに両方を押したら左を遅らせるのは、同じ tick の 2 発が 1 発に重なって見えるため (AkimboSpec.OffhandShotDelaySeconds)。
        public bool PullTriggers(bool rightPressed, bool rightWasPressedThisFrame, bool leftPressed, bool leftWasPressedThisFrame)
        {
            EnsureInitialized();
            var right = PullTrigger(AkimboHand.Right, rightPressed, rightWasPressedThisFrame);
            var hands = Akimbo;
            FollowOffhandHands(hands);
            if (hands == null)
            {
                return right;
            }

            var delay = hands.Akimbo.OffhandShotDelaySeconds;
            if (rightWasPressedThisFrame && leftWasPressedThisFrame && delay > 0f)
            {
                _offhandPending = true;
                _offhandDelayRemaining = delay;
            }

            if (_offhandPending)
            {
                // 遅らせている間に左を渡すと、押しっぱなしで撃つ撃ち方が押した扱いを先に始めてしまう。
                if (_offhandDelayRemaining > 0f)
                {
                    return right;
                }

                _offhandPending = false;
                leftWasPressedThisFrame = true;
            }

            var left = PullTrigger(AkimboHand.Left, leftPressed, leftWasPressedThisFrame);
            return right || left;
        }

        public bool PullTrigger(AkimboHand hand, bool isPressed, bool wasPressedThisFrame)
        {
            EnsureInitialized();
            RequireHand(hand);
            var hands = Akimbo;
            var driver = DriverFor(hand);
            if (hand == AkimboHand.Left && hands == null)
            {
                driver.Bind(null);
                return false;
            }

            var weapon = hands != null ? hands.Hand(hand) : Weapon;
            return PullTrigger(weapon, hands != null ? hand : (AkimboHand?)null, driver, isPressed, wasPressedThisFrame);
        }

        public bool Fire(AkimboHand hand)
        {
            EnsureInitialized();
            RequireHand(hand);
            var hands = Akimbo;
            if (hands == null && hand == AkimboHand.Left)
            {
                return false;
            }

            var weapon = hands != null ? hands.Hand(hand) : Weapon;
            var driver = DriverFor(hand);
            var charge = weapon != null && weapon.Trigger == driver.Model ? driver.ChargeFraction : 0f;
            return FireShot(weapon, hands != null ? hand : (AkimboHand?)null, charge);
        }

        // 弾が残っている手は始めない。ReloadMode.Auto で撃ち切った手だけを込め、もう片方で撃ち続けられるようにするため。
        public bool StartReload(AkimboHand hand)
        {
            EnsureInitialized();
            RequireHand(hand);
            var hands = Akimbo;
            if (hands == null || IsReloadBlocked())
            {
                return false;
            }

            return Started(hands, () => hands.TryStartReload(hand));
        }

        private bool IsReloadBlocked()
        {
            var weapon = Weapon;
            return weapon == null || weapon.Spec.IsShield || weapon.Spec.IsMelee || IsSwitching || IsReloadBlockedByModifier;
        }

        // 満たんでない手を両方始める。片方だけ始められても true。
        private bool StartAkimboReload(AkimboHands hands)
        {
            return Started(hands, hands.TryStartReload);
        }

        private bool Started(AkimboHands hands, Func<bool> start)
        {
            var rightWas = hands.Right.IsReloading;
            var leftWas = hands.Left.IsReloading;
            if (!start())
            {
                return false;
            }

            ReportReloadStarted(hands, AkimboHand.Right, rightWas);
            ReportReloadStarted(hands, AkimboHand.Left, leftWas);
            return true;
        }

        private void ReportReloadStarted(AkimboHands hands, AkimboHand hand, bool wasReloading)
        {
            var weapon = hands.Hand(hand);
            if (wasReloading || !weapon.IsReloading)
            {
                return;
            }

            Debug.Log(AkimboLog.Reload(hand), this);
            ReloadStarted?.Invoke(weapon);
        }

        private bool TryAkimboActiveReload(AkimboHands hands)
        {
            var judged = TryActiveReload(hands.Right);
            return TryActiveReload(hands.Left) || judged;
        }

        private bool TryActiveReload(Weapon weapon)
        {
            if (!weapon.IsReloading || !weapon.TryActiveReload(out var result))
            {
                return false;
            }

            Debug.Log(ActiveReloadLog(result), this);
            ActiveReloadJudged?.Invoke(weapon, result);
            return true;
        }

        private bool IsHeldReloading
        {
            get
            {
                var hands = Akimbo;
                if (hands != null)
                {
                    return hands.IsReloading;
                }

                var weapon = Weapon;
                return weapon != null && weapon.IsReloading;
            }
        }

        private void CancelHeldReload()
        {
            var hands = Akimbo;
            if (hands != null)
            {
                hands.CancelReload();
                return;
            }

            Weapon?.CancelReload();
        }

        private TriggerDriver DriverFor(AkimboHand hand)
        {
            return hand == AkimboHand.Left ? _offhandTrigger : _trigger;
        }

        private static void RequireHand(AkimboHand hand)
        {
            if (hand != AkimboHand.Right && hand != AkimboHand.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        // 持ち替えたら左の引き金の遅らせた押しを捨てる。持ち替え先の武器で、前の武器で押した分を撃たないように。
        private void FollowOffhandHands(AkimboHands hands)
        {
            if (ReferenceEquals(hands, _offhandHands))
            {
                return;
            }

            _offhandHands = hands;
            _offhandPending = false;
            _offhandDelayRemaining = 0f;
        }

        private void TickAkimbo(AkimboHands hands, float deltaSeconds)
        {
            FollowOffhandHands(hands);
            _offhandTrigger.Bind(hands?.Left.Trigger);
            _offhandTrigger.Advance(deltaSeconds);
            _offhandDelayRemaining = Mathf.Max(0f, _offhandDelayRemaining - deltaSeconds);
        }

        private void DropOffhand()
        {
            _offhandTrigger.Drop();
            _offhandPending = false;
            _offhandDelayRemaining = 0f;
        }

        private float AkimboSpreadMultiplier(WeaponSpec spec)
        {
            if (spec.IsAkimbo)
            {
                return spec.Akimbo.SpreadMultiplier;
            }

            // 手の Weapon の Spec は二丁持ちの部品を外してあるので、今の組の手の Spec かで見分ける。左右は同じ Spec を使う。
            var hands = Akimbo;
            return hands != null && ReferenceEquals(spec, hands.Right.Spec) ? hands.Akimbo.SpreadMultiplier : 1f;
        }

        // 並びの作り直しで持ち越す左手の弾倉。右手の弾倉と予備弾は SnapshotLoadoutAmmo で並びの Weapon に渡る。
        private Dictionary<string, int> ReleaseAkimboHands()
        {
            Dictionary<string, int> leftMagazines = null;
            foreach (var hands in _akimboHands.Values)
            {
                leftMagazines ??= new Dictionary<string, int>(StringComparer.Ordinal);
                leftMagazines[hands.Spec.Id] = hands.Left.AmmoInMagazine;
                hands.CancelReload();
                hands.UseAmmoPool(null);
                hands.Reloaded -= OnHandReloaded;
            }

            _akimboHands.Clear();
            _offhandTrigger.Bind(null);
            FollowOffhandHands(null);
            return leftMagazines;
        }

        // 込めた弾を作り直しで戻す (難易度を替える・セーブから戻す) ときは、左手も並びの Weapon の弾倉と同じにする。
        private void BuildAkimboHands(Dictionary<string, int> leftMagazines, GameRules rules, bool resetAmmo)
        {
            if (_loadout == null)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                if (!weapon.Spec.IsAkimbo)
                {
                    continue;
                }

                var right = weapon.AmmoInMagazine;
                var left = right;
                if (!resetAmmo && leftMagazines != null && leftMagazines.TryGetValue(weapon.Spec.Id, out var kept))
                {
                    left = Math.Min(kept, weapon.Spec.MagazineSize);
                }

                var hands = new AkimboHands(weapon.Spec, right, left, weapon.ReserveAmmo, rules ?? GameRules.Default);
                if (weapon.AmmoPool != null)
                {
                    hands.UseAmmoPool(weapon.AmmoPool);
                }

                hands.Reloaded += OnHandReloaded;
                _akimboHands[weapon] = hands;
            }
        }

        private void OnHandReloaded(AkimboHand hand)
        {
            Debug.Log(ReloadedLog, this);
        }

        // 二丁持ちの武器の数は組から読む。並びの Weapon は二丁持ちの間に数えないので古い。
        private WeaponAmmo AmmoOf(Weapon weapon)
        {
            return _akimboHands.TryGetValue(weapon, out var hands)
                ? new WeaponAmmo(weapon.Spec.Id, hands.Right.AmmoInMagazine, hands.ReserveAmmo)
                : WeaponAmmo.Of(weapon);
        }

        private IReadOnlyList<WeaponAmmo> SnapshotLoadoutAmmo()
        {
            if (_akimboHands.Count == 0)
            {
                return _loadout.SnapshotAmmo();
            }

            var result = new WeaponAmmo[_loadout.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = AmmoOf(_loadout.Items[i]);
            }

            return result;
        }

        // 手放す武器に付ける弾は 1 つの弾倉しか運べないので、左手の弾倉の弾は手放す武器の予備弾に足して捨てずに残す。
        private WeaponAmmo LeavingAmmoOf(Weapon weapon)
        {
            if (!_akimboHands.TryGetValue(weapon, out var hands))
            {
                return WeaponAmmo.Leaving(weapon);
            }

            hands.CancelReload();
            var reserve = hands.AmmoPool != null ? 0 : hands.ReserveAmmo;
            return new WeaponAmmo(weapon.Spec.Id, hands.Right.AmmoInMagazine, reserve + hands.Left.AmmoInMagazine);
        }

        private PickupWeaponState PickupStateOf(Weapon weapon)
        {
            return _akimboHands.TryGetValue(weapon, out var hands)
                ? new PickupWeaponState(weapon.Spec.Id, hands.ReserveAmmo, hands.MaxReserveAmmo, hands.HasInfiniteReserve)
                : PickupWeaponState.Of(weapon);
        }

        private int AddReserveAmmoTo(Weapon weapon, int amount)
        {
            return _akimboHands.TryGetValue(weapon, out var hands) ? hands.AddReserveAmmo(amount) : weapon.AddReserveAmmo(amount);
        }

        private void UseAkimboAmmoPool(Weapon weapon, IAmmoPool pool)
        {
            if (_akimboHands.TryGetValue(weapon, out var hands))
            {
                hands.UseAmmoPool(pool);
            }
        }

        // しまった二丁持ちの武器のリロードを止める。並びの Weapon はしまうときに Loadout が止めるが、組は Loadout が知らない。
        private void CancelStowedAkimboReloads()
        {
            var current = Weapon;
            foreach (var pair in _akimboHands)
            {
                if (!ReferenceEquals(pair.Key, current))
                {
                    pair.Value.CancelReload();
                }
            }
        }
    }
}
