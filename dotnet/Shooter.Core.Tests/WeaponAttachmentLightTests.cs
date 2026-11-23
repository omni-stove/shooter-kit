using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 付けた物の中からライトを引くのは、サーバーとクライアントが同じ答えを出すための共通の口なので、付かない物を数えないことを確かめる
    public sealed class WeaponAttachmentLightTests
    {
        private const string _rifleId = "7a1c2e3d-4b5f-4a6c-8d7e-9f0a1b2c3d4e";
        private const string _pistolId = "8b2d3f4e-5c6a-4b7d-9e8f-0a1b2c3d4e5f";
        private const string _rifleItemId = "9c3e4a5f-6d7b-4c8e-8f9a-1b2c3d4e5f6a";
        private const string _pistolItemId = "0d4f5b6a-7e8c-4d9f-9a0b-2c3d4e5f6a7b";
        private const string _lightId = "1e5a6c7b-8f9d-4e0a-8b1c-3d4e5f6a7b8c";
        private const string _suppressorId = "2f6b7d8c-9a0e-4f1b-9c2d-4e5f6a7b8c9d";

        // 射程 30 m・広がり 60 度・明るさ 2 の、light の取り付け口に付くライト。取り付け口は小銃にだけある
        private static readonly WeaponLightSpec _light = new WeaponLightSpec(30f, 60f, 2f);

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: _rifleId, attachmentSlots: new[] { "muzzle", "light" }),
                    new ItemSpec(_pistolItemId, "weapon.handgun", weaponId: _pistolId, attachmentSlots: new[] { "muzzle" }),
                    new ItemSpec(_lightId, "item.weaponLight", attachment: new AttachmentSpec("light", light: _light)),
                    new ItemSpec(_suppressorId, "item.suppressor", attachment: new AttachmentSpec("muzzle", shotNoiseRadiusMultiplier: 0.3f)),
                },
                InventoryRule.Default);
        }

        [Test]
        public void TheLightOfAnAttachedLightIsFound()
        {
            var light = WeaponAttachments.LightOf(Catalog(), _rifleId, new[] { _suppressorId, _lightId });

            Assert.That(light, Is.SameAs(_light));
            Assert.That(light.Range, Is.EqualTo(30f));
        }

        [Test]
        public void AWeaponWithoutALightHasNone()
        {
            Assert.That(WeaponAttachments.LightOf(Catalog(), _rifleId, new[] { _suppressorId }), Is.Null);
            Assert.That(WeaponAttachments.LightOf(Catalog(), _rifleId, WeaponAttachments.None), Is.Null);
            Assert.That(WeaponAttachments.LightOf(Catalog(), _rifleId, null), Is.Null);
        }

        // 拳銃には light の取り付け口が無いので、一覧にライトの Id があっても付いていないと数える
        [Test]
        public void ALightThatDoesNotFitTheWeaponIsNotCounted()
        {
            Assert.That(WeaponAttachments.LightOf(Catalog(), _pistolId, new[] { _lightId }), Is.Null);
        }

        [Test]
        public void NoWeaponHasNoLight()
        {
            Assert.That(WeaponAttachments.LightOf(Catalog(), null, new[] { _lightId }), Is.Null);
            Assert.That(WeaponAttachments.LightOf(Catalog(), "", new[] { _lightId }), Is.Null);
        }

        [Test]
        public void TheCatalogIsNeeded()
        {
            Assert.Throws<ArgumentNullException>(() => WeaponAttachments.LightOf(null, _rifleId, new[] { _lightId }));
        }
    }
}
