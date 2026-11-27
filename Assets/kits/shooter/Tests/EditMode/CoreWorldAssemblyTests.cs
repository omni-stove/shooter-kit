using System;
using System.Linq;
using System.Reflection;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    // dotnet の GameKit.Core は Core/World も 1 つのアセンブリにまとめてビルドするので、アセンブリの境目は Unity のテストでだけ確かめる
    public sealed class CoreWorldAssemblyTests
    {
        private const string WorldAssembly = "GameKit.World";

        [Test]
        public void WorldTypes_LiveInTheGameKitWorldAssembly()
        {
            var types = new[]
            {
                typeof(WorldHit),
                typeof(IWorldCollision),
                typeof(BvhWorldCollision),
                typeof(CapsuleMover),
                typeof(CapsuleMoverSpec),
                typeof(CapsuleContacts),
                typeof(MapColliderGroups),
                typeof(ShooterMapFile),
                typeof(WorldMap),
                typeof(WorldCollider),
                typeof(WorldShapeKind),
                typeof(WorldGeometry),
            };

            foreach (var type in types)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(WorldAssembly), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("GameKit.World"), type.Name);
            }

            Assert.That(typeof(MapVehicle).Assembly.GetName().Name, Is.EqualTo("Shooter.Core.Vehicle"));
        }

        [Test]
        public void WorldAssembly_ReferencesNeitherTheGameNorTheEngine()
        {
            var references = typeof(IWorldCollision).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(references, Has.None.StartsWith("Shooter."));
            Assert.That(references, Has.None.StartsWith("GameKit."));
            Assert.That(references, Has.None.StartsWith("UnityEngine"));
            Assert.That(references, Has.None.StartsWith("UnityEditor"));
        }

        [Test]
        public void WorldAssembly_SharesItsInternalsWithNoOne()
        {
            var friends = typeof(IWorldCollision).Assembly
                .GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
                .Select(a => a.AssemblyName)
                .ToArray();

            Assert.That(friends, Is.Empty);
        }

        [Test]
        public void WorldGeometry_IsPublicForTheVehicleBoxes()
        {
            Assert.That(typeof(WorldGeometry).IsPublic, Is.True);
            Assert.That(Type.GetType("GameKit.World.WorldGeometry, Shooter.Core"), Is.Null);
            Assert.That(Type.GetType("GameKit.World.WorldGeometry, Shooter.Core.Vehicle"), Is.Null);
        }

        [Test]
        public void CoreAssembly_BuildsOnTheGameKitWorld()
        {
            var references = typeof(MountedGunUseSpec).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(references, Does.Contain(WorldAssembly));
            Assert.That(references, Has.None.EqualTo("Shooter.Core.World"));
        }
    }
}
