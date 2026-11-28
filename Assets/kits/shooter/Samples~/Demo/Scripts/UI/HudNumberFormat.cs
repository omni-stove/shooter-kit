using System;
using System.Globalization;
using GameKit.Core;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 書式を言語や画面ごとに変えられるよう、HudModel は数で持ち、文字列にするのは UI のここで行う。
    public static class HudNumberFormat
    {
        public const string NoHealth = "--";

        public const string NoAmmo = "-- / --";

        public const string NoMagazine = "--";

        public const string NoTime = "--:--";

        public const string AmmoSeparator = " / ";

        // 二丁持ちの左手と右手の弾倉の間に置く。予備弾は左右で分け合うので AmmoSeparator の後に 1 つだけ出す。
        public const string AkimboSeparator = " | ";

        // 無限のときの予備の数は減らないので、数を出すと有限に見えてしまうため、数の代わりにこれを出す。
        public const string InfiniteReserve = "∞";

        // 弾倉の数はリロードが終わるまで変わらず撃てるように見えてしまうため、リロード中は数の代わりにこれを出す。
        public static LocalizedText Reloading => new LocalizedText("hud.reloading");

        public static string Health(int? current)
        {
            if (!current.HasValue)
            {
                return NoHealth;
            }

            if (current.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            return current.Value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Ammo(int? magazine, int? reserve, bool reloading, bool infiniteReserve, bool showsReserve)
        {
            if (!magazine.HasValue)
            {
                return showsReserve ? NoAmmo : NoMagazine;
            }

            if (magazine.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            var reserveCount = reserve ?? 0;
            if (reserveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            var magazineText = Magazine(magazine.Value, reloading);
            if (!showsReserve)
            {
                return magazineText;
            }

            var reserveText = infiniteReserve ? InfiniteReserve : reserveCount.ToString(CultureInfo.InvariantCulture);
            return magazineText + AmmoSeparator + reserveText;
        }

        // 二丁持ちの弾数。画面の左右と同じ並びにするため、左手の弾倉を先に出す (例: "8 | 5 / 32")。
        // offhandMagazine が null (二丁持ちでない) か右手の弾倉が無ければ、Ammo(magazine, ...) と同じ書き方にする。
        public static string Ammo(int? magazine, int? offhandMagazine, int? reserve, bool reloading, bool offhandReloading, bool infiniteReserve, bool showsReserve)
        {
            if (offhandMagazine.HasValue && offhandMagazine.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offhandMagazine), offhandMagazine, "offhandMagazine must not be negative");
            }

            var right = Ammo(magazine, reserve, reloading, infiniteReserve, showsReserve);
            if (!offhandMagazine.HasValue || !magazine.HasValue)
            {
                return right;
            }

            return Magazine(offhandMagazine.Value, offhandReloading) + AkimboSeparator + right;
        }

        private static string Magazine(int magazine, bool reloading)
        {
            return reloading ? GameText.Resolve(Reloading) : magazine.ToString(CultureInfo.InvariantCulture);
        }

        public static string Time(int? seconds)
        {
            if (!seconds.HasValue)
            {
                return NoTime;
            }

            if (seconds.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            var minutes = seconds.Value / 60;
            var rest = seconds.Value % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string RemainingTime(float remainingSeconds)
        {
            return Time(HudFormat.DisplayedSeconds(remainingSeconds));
        }

        public static string Health(HudModel model)
        {
            return Health(model.CurrentHealth);
        }

        public static string Ammo(HudModel model)
        {
            return Ammo(model.MagazineAmmo, model.OffhandMagazineAmmo, model.ReserveAmmo, model.IsReloading, model.IsOffhandReloading, model.IsReserveInfinite, model.ShowsReserveAmmo);
        }

        public static string Time(HudModel model)
        {
            return Time(model.RemainingSeconds);
        }
    }
}
