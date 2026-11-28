using System;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class EconomySlotsTests
    {
        private const int _player = 1;

        // 種類のキーはゲームが決めるので、シューターに無い "Spell"・"Potion" でも買える
        private static readonly BuyItem _fireball = new BuyItem("fireball", "Spell", 400, takesSlot: true, slot: 2);
        private static readonly BuyItem _potion = new BuyItem("potion", "Potion", 100, amount: 3);

        private static Economy Make()
        {
            var economy = new Economy(new EconomyRules(new EconomySpec(startMoney: 1000), new BuyCatalog(new[] { _fireball, _potion })));
            economy.AddPlayer(_player, Side.Alpha);
            economy.OpenBuyTime();
            return economy;
        }

        [Test]
        public void ABuyItemKeepsItsKindKeyAndAmount()
        {
            Assert.That(_fireball.Kind, Is.EqualTo("Spell"));
            Assert.That(_fireball.TakesSlot, Is.True);
            Assert.That(_potion.TakesSlot, Is.False);
            Assert.That(_potion.Amount, Is.EqualTo(3));
        }

        [Test]
        public void ABuyItemChecksItsValues()
        {
            Assert.Throws<ArgumentException>(() => new BuyItem("x", " ", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", "Potion", 1, slot: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", "Potion", 1, amount: -1));
        }

        [Test]
        public void AnItemThatTakesASlotIsKeptInItsSlotAndNotBoughtTwice()
        {
            var economy = Make();

            Assert.That(economy.Buy(_player, "fireball"), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(economy.Buy(_player, "fireball"), Is.EqualTo(PurchaseVerdict.CannotCarry));

            Assert.That(economy.ItemInSlot(_player, 2), Is.EqualTo("fireball"));
            Assert.That(economy.MoneyOf(_player), Is.EqualTo(600));
        }

        [Test]
        public void AnItemWithoutASlotIsNotKeptAndCanBeBoughtAgain()
        {
            var economy = Make();

            Assert.That(economy.Buy(_player, "potion"), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(economy.Buy(_player, "potion"), Is.EqualTo(PurchaseVerdict.Bought));

            Assert.That(economy.SlotItemsOf(_player), Is.Empty);
            Assert.That(economy.MoneyOf(_player), Is.EqualTo(800));
        }

        [Test]
        public void LosingTheSlotItemsEmptiesEverySlot()
        {
            var economy = Make();
            economy.Buy(_player, "fireball");

            Assert.That(economy.LoseSlotItems(_player), Is.True);
            Assert.That(economy.LoseSlotItems(_player), Is.False);

            Assert.That(economy.ItemInSlot(_player, 2), Is.Null);
        }
    }
}
