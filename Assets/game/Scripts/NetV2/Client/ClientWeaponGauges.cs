using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 出すかどうかを武器の値で決めるのは、溜めも熱も無い武器にも WeaponSync は 0 を運ぶので、値だけでは空のゲージが出てしまうため。
    public static class ClientWeaponGauges
    {
        public static void Show(HudModel model, in WeaponSync weapon, WeaponSpec spec)
        {
            Show(model, weapon, spec, null);
        }

        // 手元の溜めを優先するのは、サーバーの溜めは撃った弾のぶんが後から届くだけで、押している間のゲージが動かないため。
        public static void Show(HudModel model, in WeaponSync weapon, WeaponSpec spec, float? localCharge)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (spec == null || weapon.WeaponCount == 0)
            {
                Clear(model);
                return;
            }

            if (spec.Trigger.Charge != null)
            {
                model.SetCharge(Fraction(localCharge ?? weapon.ChargeFraction));
            }
            else
            {
                model.ClearCharge();
            }

            if (spec.Ammo.Kind == AmmoKind.Heat)
            {
                model.SetHeat(Fraction(weapon.Heat));
            }
            else
            {
                model.ClearHeat();
            }

            var types = spec.AmmoTypes;
            if (weapon.AmmoTypeIndex > 0 && weapon.AmmoTypeIndex <= types.Count)
            {
                model.SetAmmoType(types[weapon.AmmoTypeIndex - 1]);
            }
            else
            {
                model.ClearAmmoType();
            }
        }

        public static void Clear(HudModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            model.ClearCharge();
            model.ClearHeat();
            model.ClearAmmoType();
        }

        // 解除の残りは時刻で減り WeaponSync は解除の間ずっと同じ値なので、値が変わらなくてもスナップショットごとに呼ぶ。
        // serverSeconds はサーバーの戦闘の時計 (スナップショットの tick を tick の速さで割った秒) で、JamClearEndsAt もこの時計の時刻で届く。
        public static void ShowJam(HudModel model, in WeaponSync weapon, WeaponSpec spec, double serverSeconds)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (weapon.WeaponCount == 0 || !weapon.IsJammed)
            {
                model.ClearJam();
            }
            else if (weapon.JamClearEndsAt <= 0d)
            {
                model.SetJam();
            }
            else
            {
                model.SetJam(JamClearRemaining(weapon.JamClearEndsAt, spec, serverSeconds));
            }
        }

        // 解除の残りの割合 (1 で始めたところ、0 で解き終わる)。
        // 武器の値が引けない・時刻が壊れているときに 1 にするのは、解除中であることだけは出し、分からない進みを減らして見せないため。
        public static float JamClearRemaining(double clearEndsAt, WeaponSpec spec, double serverSeconds)
        {
            var clearSeconds = spec?.Ammo?.Jam?.ClearSeconds;
            if (!clearSeconds.HasValue || !IsFinite(clearEndsAt) || !IsFinite(serverSeconds))
            {
                return 1f;
            }

            var remaining = (clearEndsAt - serverSeconds) / clearSeconds.Value;
            return (float)Math.Min(1d, Math.Max(0d, remaining));
        }

        // サーバーの戦闘の時計は tick ごとに 1 tick の秒を足すので、スナップショットの tick を tick の速さで割ると同じ時計の秒になる。
        public static double ServerSeconds(int tick, int tickRate)
        {
            return tick / (double)Math.Max(1, tickRate);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static float Fraction(float value)
        {
            return Math.Min(1f, Math.Max(0f, value));
        }
    }
}
