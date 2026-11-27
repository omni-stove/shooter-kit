using System;
using GameKit.Core;

namespace Shooter.Core
{
    // 二丁持ちの武器の左右の手。弾倉・撃ち方・クールダウンは手ごとの Weapon に持たせ、予備弾は左右で 1 つを分け合う。
    // 手ごとに Weapon を作るのは、弾倉の数え方・リロードの進め方・撃ち方を 1 丁の武器と同じ処理のまま使うため。
    public sealed class AkimboHands
    {
        private readonly SharedReserve _reserve;

        public AkimboHands(WeaponSpec spec)
            : this(spec, GameRules.Default)
        {
        }

        public AkimboHands(WeaponSpec spec, GameRules rules)
        {
            Spec = RequireAkimbo(spec);
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var handSpec = HandSpec(spec);
            Right = new Weapon(handSpec, rules);
            Left = new Weapon(handSpec, rules);
            _reserve = Share(Right.ReserveAmmo);
            Wire();
        }

        // reserveAmmo はセーブの値をそのまま使い、GameRules.ReserveAmmoMultiplier は掛けない (Weapon と同じく、作ったときに掛けた値がセーブされているため)。
        // 手ごとの詰まりの判定は、渡さなければ左右で別の種から引く (同じ種だと左右で同じ順番目の弾で詰まるため)。
        public AkimboHands(WeaponSpec spec, int rightAmmoInMagazine, int leftAmmoInMagazine, int reserveAmmo, GameRules rules, IJamRoll rightJamRoll = null, IJamRoll leftJamRoll = null)
        {
            Spec = RequireAkimbo(spec);
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var handSpec = HandSpec(spec);
            Right = new Weapon(handSpec, rightAmmoInMagazine, reserveAmmo, rules, rightJamRoll);

            // 左の手に予備弾を持たせないのは、右の手と同じ数を持たせると分け合う予備弾が 2 倍に見えるため。
            Left = new Weapon(handSpec, leftAmmoInMagazine, 0, rules, leftJamRoll ?? JamRoll.ForWeapon(spec.Id + ":left"));
            _reserve = Share(reserveAmmo);
            Wire();
        }

        public event Action<AkimboHand> Fired;

        public event Action<AkimboHand> Reloaded;

        // 左右で同じ予備弾を使うので、右の手の知らせだけを流す。両手から流すと 1 度の変化が 2 度届く。
        public event Action ReserveAmmoChanged;

        // 二丁持ちの部品を持つ元の武器。手ごとの Weapon.Spec は、二丁持ちの部品を外してリロードの秒数に倍率を掛けたもの。
        public WeaponSpec Spec { get; }

        public AkimboSpec Akimbo => Spec.Akimbo;

        public Weapon Right { get; }

        public Weapon Left { get; }

        public int MagazineSize => Right.Spec.MagazineSize;

        public float ReloadSeconds => Right.Spec.ReloadSeconds;

        public int ReserveAmmo => Right.ReserveAmmo;

        public int MaxReserveAmmo => Right.MaxReserveAmmo;

        public int ReserveAmmoRoom => Right.ReserveAmmoRoom;

        public bool HasInfiniteReserve => Right.HasInfiniteReserve;

        public bool IsReloading => Right.IsReloading || Left.IsReloading;

        // 持ち物の弾薬を使っていなければ null。左右の内で分け合っている予備弾は返さない。
        public IAmmoPool AmmoPool { get; private set; }

        public Weapon Hand(AkimboHand hand)
        {
            switch (hand)
            {
                case AkimboHand.Right:
                    return Right;
                case AkimboHand.Left:
                    return Left;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        public int AmmoInMagazine(AkimboHand hand)
        {
            return Hand(hand).AmmoInMagazine;
        }

        public bool IsHandReloading(AkimboHand hand)
        {
            return Hand(hand).IsReloading;
        }

        public float ReloadProgress(AkimboHand hand)
        {
            return Hand(hand).ReloadProgress;
        }

        public bool TryFire(AkimboHand hand)
        {
            if (!Hand(hand).TryFire())
            {
                return false;
            }

            Fired?.Invoke(hand);
            return true;
        }

        // 片方が満タンでも、もう片方を始められれば true。
        public bool TryStartReload()
        {
            var right = Right.TryStartReload();
            var left = Left.TryStartReload();
            return right || left;
        }

        // 弾が残っている手を始めないのは、撃ち切った手だけを込め直し、もう片方の手で撃ち続けられるようにするため。
        public bool TryStartReload(AkimboHand hand)
        {
            var weapon = Hand(hand);
            return weapon.AmmoInMagazine == 0 && weapon.TryStartReload();
        }

        public void CancelReload()
        {
            Right.CancelReload();
            Left.CancelReload();
        }

        public int AddReserveAmmo(int amount)
        {
            return Right.AddReserveAmmo(amount);
        }

        // 予備弾を pool から出し入れするようにする。null で外す (外した後の予備弾は Weapon.UseAmmoPool と同じく 0)。
        // 予備弾が無限か熱で撃つ武器は数えないので false。
        public bool UseAmmoPool(IAmmoPool pool)
        {
            if (_reserve == null)
            {
                return false;
            }

            if (pool == null)
            {
                AmmoPool = null;
                Attach(_reserve);
                return false;
            }

            // 左右の内の予備弾を移してから両手を付け替える。両手の Weapon は予備弾を内に移し終えて 0 なので、付け替えでは数えない。
            if (AmmoPool == null)
            {
                var missing = _reserve.Count - pool.Count;
                if (missing > 0)
                {
                    pool.Put(missing);
                }
            }

            _reserve.Empty();
            AmmoPool = pool;
            Attach(pool);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            Right.Tick(deltaSeconds);
            Left.Tick(deltaSeconds);
        }

        private static WeaponSpec RequireAkimbo(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!spec.IsAkimbo)
            {
                throw new ArgumentException("weapon '" + spec.LogName + "' has no akimbo part", nameof(spec));
            }

            return spec;
        }

        // 手の Spec から二丁持ちの部品を外すのは、掛けたあとの秒数に WeaponSpec が倍率をもう一度掛けて確かめ、JSON で通った武器を弾いてしまうため。
        private static WeaponSpec HandSpec(WeaponSpec spec)
        {
            var multiplier = spec.Akimbo.ReloadSecondsMultiplier;
            var single = spec.WithoutAkimbo();
            if (multiplier == 1f)
            {
                return single;
            }

            var ammo = spec.Ammo;
            return single.WithAmmo(new AmmoSpec(
                ammo.MagazineSize,
                ammo.ReserveAmmo,
                ammo.ReloadSeconds * multiplier,
                ammo.ActiveReloadWindowStart,
                ammo.ActiveReloadWindowLength,
                ammo.ActiveReloadFailSeconds,
                ammo.ReloadStyle,
                ammo.ReloadSecondsPerRound * multiplier,
                ammo.Kind,
                ammo.Heat));
        }

        // 予備弾が無限か熱で撃つ武器は予備弾を数えないので、分け合う予備弾を作らない。
        private SharedReserve Share(int count)
        {
            if (Right.HasInfiniteReserve)
            {
                return null;
            }

            var reserve = new SharedReserve(Spec.Id, count, Math.Max(count, Right.MaxReserveAmmo));
            if (!Right.UseAmmoPool(reserve))
            {
                return null;
            }

            Left.UseAmmoPool(reserve);
            return reserve;
        }

        private void Attach(IAmmoPool pool)
        {
            Right.UseAmmoPool(pool);
            Left.UseAmmoPool(pool);
        }

        private void Wire()
        {
            Right.Reloaded += () => Reloaded?.Invoke(AkimboHand.Right);
            Left.Reloaded += () => Reloaded?.Invoke(AkimboHand.Left);
            Right.ReserveAmmoChanged += () => ReserveAmmoChanged?.Invoke();
        }

        private sealed class SharedReserve : IAmmoPool
        {
            private readonly int _max;

            public SharedReserve(string id, int count, int max)
            {
                Id = id;
                Count = count;
                _max = max;
            }

            public event Action Changed;

            public string Id { get; }

            public int Count { get; private set; }

            public int Room => Math.Max(0, _max - Count);

            public int Take(int count)
            {
                SpecGuard.NonNegative(count, nameof(count));
                var taken = Math.Min(count, Count);
                if (taken > 0)
                {
                    Count -= taken;
                    Changed?.Invoke();
                }

                return taken;
            }

            public int Put(int count)
            {
                SpecGuard.NonNegative(count, nameof(count));
                var put = Math.Min(count, Room);
                if (put > 0)
                {
                    Count += put;
                    Changed?.Invoke();
                }

                return put;
            }

            public void Empty()
            {
                if (Count == 0)
                {
                    return;
                }

                Count = 0;
                Changed?.Invoke();
            }
        }
    }
}
