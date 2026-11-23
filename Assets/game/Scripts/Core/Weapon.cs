using System;

namespace Shooter.Core
{
    // 弾倉が空になっても自動ではリロードしない (呼び出し側が TryStartReload を呼ぶ)。
    public sealed class Weapon
    {
        public event Action Fired;

        public event Action Reloaded;

        public event Action ReserveAmmoAdded;

        public event Action AmmoTypeSwitched;

        // 予備弾を持ち物から出し入れする武器で、持ち物が変わったとき (弾薬を拾った・捨てた・同じ弾薬の別の武器が使った) に呼ぶ
        public event Action ReserveAmmoChanged;

        // 撃てた 1 発が詰まらせたときに、Fired のあとで呼ぶ
        public event Action Jammed;

        public event Action JamClearStarted;

        // 解除し終えてまた撃てるようになったとき
        public event Action JamCleared;

        private readonly IJamRoll _jamRoll;
        private float _cooldownRemaining;

        public WeaponSpec Spec { get; }

        public ITriggerModel Trigger { get; }

        public IAmmoModel Ammo { get; }

        public AmmoTypeSelector AmmoTypes { get; }

        // 近接武器でなければ null。溜めは撃ち方 (Trigger) と別に持つ。近接武器は撃たないので、撃ち方の溜めを使うと弾を撃つ処理が走る。
        public MeleeCharge MeleeCharge { get; }

        public int AmmoInMagazine => Ammo.AmmoInMagazine;
        public int ReserveAmmo => Ammo.ReserveAmmo;
        public bool IsReloading => Ammo.IsReloading;

        public bool HasInfiniteReserve => Ammo.HasInfiniteReserve;

        public bool HasActiveReload => Ammo.HasActiveReload;

        public bool HasJudgedActiveReload => Ammo.HasJudgedActiveReload;

        public float ReloadProgress => Ammo.ReloadProgress;

        public Weapon(WeaponSpec spec)
            : this(spec, GameRules.Default)
        {
        }

        // jamRoll を省くと武器の Id から決めた種で作る。詰まらない武器・ルールでは使わない。
        public Weapon(WeaponSpec spec, GameRules rules, IJamRoll jamRoll = null)
            : this(
                spec,
                AmmoModels.For((spec ?? throw new ArgumentNullException(nameof(spec))).Ammo, rules ?? throw new ArgumentNullException(nameof(rules))),
                rules,
                jamRoll)
        {
        }

        public Weapon(WeaponSpec spec, int ammoInMagazine, int reserveAmmo)
            : this(spec, ammoInMagazine, reserveAmmo, GameRules.Default)
        {
        }

        // reserveAmmo はセーブの値をそのまま使い、GameRules.ReserveAmmoMultiplier は掛けない (作ったときに掛けた値がセーブされているため)。
        public Weapon(WeaponSpec spec, int ammoInMagazine, int reserveAmmo, GameRules rules, IJamRoll jamRoll = null)
            : this(
                spec,
                AmmoModels.For(
                    (spec ?? throw new ArgumentNullException(nameof(spec))).Ammo,
                    ammoInMagazine,
                    reserveAmmo,
                    rules ?? throw new ArgumentNullException(nameof(rules))),
                rules,
                jamRoll)
        {
        }

        private Weapon(WeaponSpec spec, IAmmoModel ammo, GameRules rules, IJamRoll jamRoll)
        {
            Spec = spec;
            Trigger = TriggerModels.For(spec);
            Ammo = ammo;
            AmmoTypes = new AmmoTypeSelector(spec);
            MeleeCharge = spec.IsMelee ? new MeleeCharge(spec.Melee) : null;
            Ammo.Reloaded += () => Reloaded?.Invoke();
            var jam = spec.Ammo?.Jam;
            if (jam != null && rules.Actions.Jam == WeaponJam.On)
            {
                Jam = new WeaponJamState(jam);
                _jamRoll = jamRoll ?? JamRoll.ForWeapon(spec.Id);
            }
        }

        // ammo.jam が無いかルールの Jam が Off なら null。
        public WeaponJamState Jam { get; }

        public bool IsJammed => Jam != null && Jam.IsJammed;

        public bool IsClearingJam => Jam != null && Jam.IsClearing;

        public float JamClearRemainingSeconds => Jam?.ClearRemainingSeconds ?? 0f;

        public int MaxReserveAmmo => Ammo.MaxReserveAmmo;

        // HasInfiniteReserve なら拾う意味が無いので 0。
        public int ReserveAmmoRoom => Ammo.ReserveAmmoRoom;

        public int StartingReserveAmmo => Ammo.StartingReserveAmmo;

        public IAmmoPool AmmoPool { get; private set; }

        // 予備弾を pool から出し入れするようにする。null で外す。予備弾が無限か熱で撃つ武器は pool を使わず false
        public bool UseAmmoPool(IAmmoPool pool)
        {
            if (AmmoPool != null)
            {
                AmmoPool.Changed -= OnAmmoPoolChanged;
            }

            AmmoPool = Ammo.UsePool(pool) ? pool : null;
            if (AmmoPool != null)
            {
                AmmoPool.Changed += OnAmmoPoolChanged;
            }

            return AmmoPool != null;
        }

        public int AddReserveAmmo(int amount)
        {
            var added = Ammo.AddReserveAmmo(amount);
            if (added > 0)
            {
                ReserveAmmoAdded?.Invoke();
            }

            return added;
        }

        public bool TryFire()
        {
            if (IsJammed || !Ammo.CanFire || _cooldownRemaining > 0f)
            {
                return false;
            }

            Ammo.Consume();
            _cooldownRemaining = Trigger.MinShotSpacingSeconds;
            Fired?.Invoke();
            if (Jam != null && Jam.RegisterShot(_jamRoll.Draw()))
            {
                Jammed?.Invoke();
            }

            return true;
        }

        public bool TryStartClearJam()
        {
            if (Jam == null || !Jam.TryStartClear())
            {
                return false;
            }

            JamClearStarted?.Invoke();
            return true;
        }

        // 持ち替えなどで解除をやめる。詰まったまま残り、次は最初から解除し直す
        public bool CancelClearJam()
        {
            return Jam != null && Jam.CancelClear();
        }

        public static int ScaleReserveAmmo(int reserveAmmo, float multiplier)
        {
            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (!(multiplier >= 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be a non-negative finite number");
            }

            // 0 のときは積が 0 のままなので、下の decimal 変換 (倍率が巨大だと OverflowException) に進めない。
            if (reserveAmmo == 0 || multiplier == 0f)
            {
                return 0;
            }

            if ((double)reserveAmmo * multiplier >= int.MaxValue)
            {
                return int.MaxValue;
            }

            // decimal で掛けるのは、0.7f のような倍率が 2 進では 0.69999... になり、ちょうど .5 になる積が切り捨てられるのを避けるため。
            // float の最短往復表記 ("R") を経由するのは、(decimal)float の丸め桁数が実行環境で変わっても設定に書いた 0.7 のまま掛けるため。
            var decimalMultiplier = decimal.Parse(
                multiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
            var scaled = Math.Round(reserveAmmo * decimalMultiplier, MidpointRounding.AwayFromZero);
            return (int)Math.Min(Math.Max(scaled, 0m), int.MaxValue);
        }

        public bool TryStartReload()
        {
            return !IsJammed && Ammo.TryStartReload();
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            return Ammo.TryActiveReload(out result);
        }

        // 弾倉を空にするのは、弾倉を種類で共通にしたまま、込めてあった弾の種類を覚えずに切り替え後の弾をすべて新しい種類にするため。
        public bool TrySwitchAmmoType()
        {
            if (!AmmoTypes.TrySwitch())
            {
                return false;
            }

            Ammo.Unload();
            AmmoTypeSwitched?.Invoke();
            return true;
        }

        public void CancelReload()
        {
            Ammo.CancelReload();
        }

        private void OnAmmoPoolChanged()
        {
            ReserveAmmoChanged?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
            Ammo.Tick(deltaSeconds);
            if (Jam != null && Jam.Tick(deltaSeconds))
            {
                JamCleared?.Invoke();
            }
        }
    }
}
