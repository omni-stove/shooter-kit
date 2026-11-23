using Shooter.Core;
using Shooter.Editor;

namespace Shooter.Tests.EditMode
{
    // サンプルのシーンの builder は既定の武器を WeaponAssetBuilder の Id で一覧から引く。
    // テストの銃の値 (TestWeapons) はそのままにして Id だけを既定の武器に合わせ、builder が試しの一覧から引けるようにする。
    public static class DemoTestWeapons
    {
        public static WeaponSpec CarbineSpec() => TestWeapons.WithId(TestWeapons.RifleSpec(), WeaponAssetBuilder.CarbineId);

        public static WeaponSpec PistolSpec() => TestWeapons.WithId(TestWeapons.HandgunSpec(), WeaponAssetBuilder.PistolId);
    }
}
