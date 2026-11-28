using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class PickupSerializedFieldsTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void TheSerializedFieldsKeepTheirNamesSoSceneValuesStayBound()
        {
            var serialized = typeof(Pickup).GetFields(InstanceFields)
                .Where(f => f.IsDefined(typeof(SerializeField), false) || (f.IsPublic && !f.IsDefined(typeof(NonSerializedAttribute), false)))
                .ToDictionary(f => f.Name, f => f.FieldType);

            Assert.That(serialized, Is.EquivalentTo(new[]
            {
                Pair("_contents", typeof(PickupContents)),
                Pair("_collectMode", typeof(PickupCollectMode)),
                Pair("_radius", typeof(float)),
                Pair("_lookAngleDegrees", typeof(float)),
                Pair("_destroyOnCollect", typeof(bool)),
                Pair("_lifetimeSeconds", typeof(float)),
                Pair("_visualPrefab", typeof(GameObject)),
            }));
        }

        [Test]
        public void TheInteractActionStaysNonSerialized()
        {
            var field = typeof(Pickup).GetField("_interactAction", InstanceFields);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.IsDefined(typeof(NonSerializedAttribute), false), Is.True);
        }

        [Test]
        public void TheSerializedFieldsAreDeclaredInPickupCs()
        {
            var source = System.IO.File.ReadAllText("Assets/kits/shooter/Scripts/Runtime/Pickup/Pickup.cs");
            var partials = System.IO.Directory.GetFiles("Assets/kits/shooter/Scripts/Runtime/Pickup", "Pickup.*.cs");

            Assert.That(partials, Is.Not.Empty);
            Assert.That(source.Split(new[] { "[SerializeField" }, StringSplitOptions.None).Length - 1, Is.EqualTo(7));
            foreach (var path in partials)
            {
                StringAssert.DoesNotContain("[SerializeField", System.IO.File.ReadAllText(path), path);
            }
        }

        private static System.Collections.Generic.KeyValuePair<string, Type> Pair(string name, Type type)
        {
            return new System.Collections.Generic.KeyValuePair<string, Type>(name, type);
        }

        // 種類が列挙だった頃のシーンとプレハブは "_kind" に数を持つので、型を数のまま名前も替えずに残す
        [Test]
        public void TheContentsKeepTheOldKindNumberBesideTheKindKey()
        {
            var kindNumber = typeof(PickupContents).GetField("_kind", InstanceFields);
            var kindKey = typeof(PickupContents).GetField("_kindKey", InstanceFields);

            Assert.That(kindNumber.FieldType, Is.EqualTo(typeof(int)));
            Assert.That(kindNumber.IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(kindKey.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(kindKey.IsDefined(typeof(SerializeField), false), Is.True);
        }

        [TestCase("{\"_kind\": 0}", "Weapon")]
        [TestCase("{\"_kind\": 1}", "ReserveAmmo")]
        [TestCase("{\"_kind\": 3}", "Armor")]
        [TestCase("{\"_kind\": 5}", "Item")]
        [TestCase("{}", "Health")]
        [TestCase("{\"_kind\": 0, \"_kindKey\": \"Tag\"}", "Tag")]
        public void AKindSavedAsANumberIsReadInTheOldOrder(string json, string kind)
        {
            Assert.That(JsonUtility.FromJson<PickupContents>(json).Kind, Is.EqualTo(kind));
        }
    }
}
