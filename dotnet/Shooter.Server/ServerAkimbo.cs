using System;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 二丁持ちの武器の左右の手 (AkimboHands) を、サーバーの射撃・リロード・スナップショットの口に合わせて包む。
    // 手は連射の間隔を 0 にした Spec で作る。ServerWeapon と同じく、届く時刻の揺れで正しい射撃を弾かないためで、
    // 手ごとの間隔は ServerLoadout の手ごとの枠 (ShotBudget) が受け持つ。
    internal sealed class ServerAkimbo
    {
        private readonly int _playerId;
        private readonly TextWriter _log;
        private readonly Func<double> _now;
        private readonly GameRules _rules;
        private double _rightReloadStartedAt;
        private double _leftReloadStartedAt;

        public ServerAkimbo(int playerId, WeaponSpec spec, GameRules rules, TextWriter log, Func<double> now, IAmmoPool pool)
        {
            ArgumentNullException.ThrowIfNull(spec);
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _now = now ?? throw new ArgumentNullException(nameof(now));
            if (!spec.IsAkimbo)
            {
                throw new ArgumentException("weapon '" + spec.LogName + "' has no akimbo part", nameof(spec));
            }

            _playerId = playerId;
            Spec = spec;
            Hands = Wire(new AkimboHands(spec.WithFireInterval(0f), rules));
            Hands.UseAmmoPool(pool);
        }

        // 連射の間隔を 0 にする前の、持っている武器の Spec
        public WeaponSpec Spec { get; private set; }

        public AkimboHands Hands { get; private set; }

        public IAmmoPool AmmoPool => Hands.AmmoPool;

        public bool IsReloading => Hands.IsReloading;

        public int StartingReserveAmmo => Hands.Right.StartingReserveAmmo;

        public AmmoTypeSelector AmmoTypes => Hands.Right.AmmoTypes;

        public int ReserveAmmo => Hands.ReserveAmmo;

        public int MaxReserveAmmo => Hands.MaxReserveAmmo;

        public bool HasInfiniteReserve => Hands.HasInfiniteReserve;

        // 撃ち切った手の弾倉が空のまま要求が来たら、ReloadMode.Auto ではその手だけを込め始める (ServerWeapon.TryFire と同じ順に断る)
        public ShotRejection TryFire(AkimboHand hand)
        {
            var weapon = Hands.Hand(hand);

            // リロード中かどうかだけで弾かないのは、1 発ずつ込める弾薬が込めた弾で撃ってリロードを止めるため (ServerWeapon と同じ)
            if (weapon.IsReloading && !weapon.Ammo.CanFire)
            {
                return ShotRejection.Reloading;
            }

            if (weapon.AmmoInMagazine == 0)
            {
                ReloadIfAuto(hand);
                return ShotRejection.OutOfAmmo;
            }

            if (weapon.Ammo is HeatAmmo heat && heat.IsOverheated)
            {
                return ShotRejection.Overheated;
            }

            if (!Hands.TryFire(hand))
            {
                // 連射間隔は 0 なので、リロード中でも空でもオーバーヒート中でもなければ必ず撃てる
                throw new InvalidOperationException("the " + AkimboLog.HandName(hand) + " hand refused a shot with ammo and no reload");
            }

            _log.WriteLine(ServerCombatLog.AkimboFire(_playerId, Spec.Id, hand, Hands.Right.AmmoInMagazine, Hands.Left.AmmoInMagazine));
            if (weapon.AmmoInMagazine == 0)
            {
                ReloadIfAuto(hand);
            }

            return ShotRejection.None;
        }

        // ボットは右の手から撃ち、右の手が撃てないときだけ左の手で撃つ
        public AkimboHand HandForBot()
        {
            return CanFire(Hands.Right) || !CanFire(Hands.Left) ? AkimboHand.Right : AkimboHand.Left;
        }

        // 満タンでない手を両方込める。片方が満タンでも、もう片方を始めれば true
        public bool TryReload()
        {
            var right = StartReload(AkimboHand.Right, false, false);
            var left = StartReload(AkimboHand.Left, false, false);
            return right || left;
        }

        // 撃ち切った手だけを込める。もう片方の手で撃ち続けられるようにするため (ボットと ReloadMode.Auto)
        public bool TryReloadEmpty()
        {
            var right = StartReload(AkimboHand.Right, true, false);
            var left = StartReload(AkimboHand.Left, true, false);
            return right || left;
        }

        public bool CancelReload()
        {
            if (!Hands.IsReloading)
            {
                return false;
            }

            Hands.CancelReload();
            return true;
        }

        // 窓は右の手の分だけを送る (WeaponSync) ので、右の手が込めていれば右、そうでなければ左の手を判定する
        public bool TryActiveReload(out ActiveReloadResult result)
        {
            var weapon = Hands.Right.IsReloading ? Hands.Right : Hands.Left;
            return weapon.TryActiveReload(out result);
        }

        // 左右で込める弾の種類が食い違わないよう、両手をそろえて切り替える。両手は同じ種類から始めて常に一緒に切り替えるので、左の手も同じ種類になる
        public bool TrySwitchAmmoType()
        {
            if (!Hands.Right.TrySwitchAmmoType())
            {
                return false;
            }

            Hands.Left.TrySwitchAmmoType();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            Hands.Tick(deltaSeconds);
        }

        public bool UseAmmoPool(IAmmoPool pool)
        {
            return Hands.UseAmmoPool(pool);
        }

        public int AddReserveAmmo(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            return amount == 0 ? 0 : Hands.AddReserveAmmo(amount);
        }

        // 予備弾の受け皿を使っていなければ、左の手の弾倉を予備弾に戻した数。手放す武器の弾数 (ServerHeldWeapon) は 1 つの弾倉しか持てないため
        public int HeldReserveAmmo => AmmoPool != null ? 0 : AddClamped(Hands.ReserveAmmo, Hands.Left.AmmoInMagazine);

        public int HeldAmmoInMagazine => Hands.Right.AmmoInMagazine;

        // 手放すとき、予備弾の受け皿を使っていれば左の手の弾倉を受け皿に戻し、受け皿から外す。
        // 受け皿を使っていなければ HeldReserveAmmo に入れているので戻さない
        public void Release()
        {
            var pool = AmmoPool;
            if (pool != null)
            {
                pool.Put(Hands.Left.AmmoInMagazine);
            }

            Hands.CancelReload();
            Hands.UseAmmoPool(null);
        }

        public void Refill()
        {
            var pool = Detach();
            Hands = Wire(new AkimboHands(Spec.WithFireInterval(0f), _rules));
            Hands.UseAmmoPool(pool);
        }

        // 落とした武器を拾い直したときの弾数で持たせる。右の手に弾倉を、左の手には予備弾から込める (HeldReserveAmmo の逆)。
        // 予備弾を受け皿から出し入れしていれば、予備弾は受け皿のままにして左の手は空で持たせる
        public void SetAmmo(int ammoInMagazine, int reserveAmmo)
        {
            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            var size = Spec.MagazineSize;
            var pool = AmmoPool;
            var right = Math.Min(ammoInMagazine, size);
            var left = pool == null ? Math.Min(reserveAmmo, size) : 0;
            Rebuild(Spec, right, left, pool == null ? reserveAmmo - left : 0, pool);
        }

        // 弾倉が減って溢れる弾が予備弾の受け皿に入りきらなければ、弾を失わないよう何も変えずに false を返す (ServerWeapon.ChangeSpec と同じ)
        public bool ChangeSpec(WeaponSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (!spec.IsAkimbo)
            {
                throw new ArgumentException("weapon '" + spec.LogName + "' has no akimbo part", nameof(spec));
            }

            var size = spec.MagazineSize;
            var right = Math.Min(Hands.Right.AmmoInMagazine, size);
            var left = Math.Min(Hands.Left.AmmoInMagazine, size);
            var excess = (Hands.Right.AmmoInMagazine - right) + (Hands.Left.AmmoInMagazine - left);
            var pool = AmmoPool;
            if (excess > 0 && pool != null && pool.Room < excess)
            {
                return false;
            }

            var reserve = pool == null ? AddClamped(Hands.ReserveAmmo, excess) : 0;
            if (excess > 0 && pool != null)
            {
                pool.Put(excess);
            }

            Rebuild(spec, right, left, reserve, pool);
            return true;
        }

        public void Fill(ref WeaponSync sync)
        {
            var right = Hands.Right;
            var left = Hands.Left;
            var spec = right.Spec;
            var showsProgress = right.HasActiveReload && right.IsReloading;
            var windowOpen = showsProgress && !right.HasJudgedActiveReload;
            sync.AmmoInMagazine = right.AmmoInMagazine;
            sync.ReserveAmmo = Hands.ReserveAmmo;
            sync.IsReloading = right.IsReloading;
            sync.ReloadStartedAt = showsProgress ? _rightReloadStartedAt : 0d;
            sync.ReloadSeconds = showsProgress ? ReloadDuration(right) : 0f;
            sync.ActiveReloadWindowStart = windowOpen ? spec.ActiveReloadWindowStart : 0f;
            sync.ActiveReloadWindowEnd = windowOpen ? spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength : 0f;
            sync.Heat = right.Ammo.HeatFraction ?? 0f;
            sync.IsOverheated = right.Ammo is HeatAmmo heat && heat.IsOverheated;
            sync.OffhandAmmoInMagazine = left.AmmoInMagazine;
            sync.IsOffhandReloading = left.IsReloading;
            sync.OffhandReloadStartedAt = left.HasActiveReload && left.IsReloading ? _leftReloadStartedAt : 0d;
        }

        private static bool CanFire(Weapon weapon)
        {
            return weapon.AmmoInMagazine > 0 && weapon.Ammo.CanFire;
        }

        // 成功すればその場でリロードが終わるので、判定済みのままリロード中なのは失敗したときだけ (ServerWeapon.ReloadDurationSeconds と同じ)
        private static float ReloadDuration(Weapon weapon)
        {
            var spec = weapon.Spec;
            return weapon.HasJudgedActiveReload ? spec.ReloadSeconds + spec.ActiveReloadFailSeconds : spec.ReloadSeconds;
        }

        private static int AddClamped(int a, int b)
        {
            return (int)Math.Min((long)a + b, int.MaxValue);
        }

        private void ReloadIfAuto(AkimboHand hand)
        {
            if (_rules.ReloadMode == ReloadMode.Auto)
            {
                StartReload(hand, true, true);
            }
        }

        private bool StartReload(AkimboHand hand, bool emptyOnly, bool automatic)
        {
            var started = emptyOnly ? Hands.TryStartReload(hand) : Hands.Hand(hand).TryStartReload();
            if (!started)
            {
                return false;
            }

            if (hand == AkimboHand.Right)
            {
                _rightReloadStartedAt = _now();
            }
            else
            {
                _leftReloadStartedAt = _now();
            }

            _log.WriteLine(ServerCombatLog.AkimboReloadStart(_playerId, Spec.Id, hand, automatic, Hands.AmmoInMagazine(hand), Hands.ReserveAmmo));
            return true;
        }

        private void Rebuild(WeaponSpec spec, int right, int left, int reserve, IAmmoPool pool)
        {
            var ammoType = Hands.Right.AmmoTypes.Index;
            Detach();
            Spec = spec;
            Hands = Wire(new AkimboHands(spec.WithFireInterval(0f), right, left, reserve, _rules));
            Hands.Right.AmmoTypes.TrySelect(ammoType);
            Hands.Left.AmmoTypes.TrySelect(ammoType);
            Hands.UseAmmoPool(pool);
        }

        // 作り直す前の手を受け皿から外し、知らせの購読もやめる。外した受け皿を返す
        private IAmmoPool Detach()
        {
            var pool = Hands.AmmoPool;
            Hands.Reloaded -= HandleReloaded;
            Hands.CancelReload();
            Hands.UseAmmoPool(null);
            return pool;
        }

        private AkimboHands Wire(AkimboHands hands)
        {
            hands.Reloaded += HandleReloaded;
            return hands;
        }

        private void HandleReloaded(AkimboHand hand)
        {
            _log.WriteLine(ServerCombatLog.AkimboReloaded(_playerId, Spec.Id, hand, Hands.AmmoInMagazine(hand), Hands.ReserveAmmo));
        }
    }
}
