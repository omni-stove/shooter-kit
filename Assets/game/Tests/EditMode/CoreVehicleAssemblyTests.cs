using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    // dotnet の Shooter.Core は Core/Vehicle も 1 つのアセンブリにまとめてビルドするので、アセンブリの境目は Unity のテストでだけ確かめる
    public sealed class CoreVehicleAssemblyTests
    {
        private const string VehicleAssembly = "Shooter.Core.Vehicle";
        private const string CoreAssembly = "Shooter.Core";

        private static readonly Type[] _vehicleTypes =
        {
            typeof(BailOutSpec),
            typeof(BailOutVerdict),
            typeof(BoatHull),
            typeof(BoatSpec),
            typeof(CarDrive),
            typeof(CarSpec),
            typeof(CountermeasureFlare),
            typeof(CountermeasurePod),
            typeof(CountermeasureSpec),
            typeof(CountermeasureVerdict),
            typeof(HelicopterFlight),
            typeof(HelicopterSpec),
            typeof(HorseGait),
            typeof(HorseSpec),
            typeof(HorseStride),
            typeof(MapVehicle),
            typeof(MapVehicleCountermeasures),
            typeof(MapVehicleHullBox),
            typeof(MapVehicleSeat),
            typeof(MountedGunEnemyBehavior),
            typeof(MountedGunLeaveCause),
            typeof(MountedGunLog),
            typeof(MountedGunOrder),
            typeof(MountedGunShape),
            typeof(NetMapVehicles),
            typeof(VehicleBoardHint),
            typeof(VehicleCameraView),
            typeof(VehicleDamageStage),
            typeof(VehicleDriveCheck),
            typeof(VehicleDriveSpec),
            typeof(VehicleDurability),
            typeof(VehicleDurabilitySpec),
            typeof(VehicleImpactSpec),
            typeof(VehicleLeaveReason),
            typeof(VehicleLog),
            typeof(VehicleMotorInput),
            typeof(VehicleMoveRejection),
            typeof(VehiclePartSpec),
            typeof(VehiclePoseHistory),
            typeof(VehicleRouteFollower),
            typeof(VehicleRouteSpec),
            typeof(VehicleSeatFiring),
            typeof(VehicleSeatPose),
            typeof(VehicleSeatRole),
            typeof(VehicleSeatSpec),
            typeof(VehicleSeatTransition),
            typeof(VehicleSeatTransitionKind),
            typeof(VehicleSeating),
            typeof(VehicleSpec),
            typeof(VehicleSpeedFieldOfViewSpec),
            typeof(VehicleWeaponAim),
            typeof(VehicleWeaponSpec),
        };

        [TestCaseSource(nameof(_vehicleTypes))]
        public void VehicleType_LivesInTheVehicleAssembly_UnderTheCoreNamespace(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo(VehicleAssembly));
            Assert.That(type.Namespace, Is.EqualTo("Shooter.Core"), "the namespace stays so code using these types does not change");
        }

        [Test]
        public void CoreAssembly_NoLongerHoldsTheVehicleTypes()
        {
            var core = typeof(MountedGunUseSpec).Assembly;
            Assert.That(core.GetName().Name, Is.EqualTo(CoreAssembly));
            foreach (var type in _vehicleTypes)
            {
                Assert.That(core.GetType(type.FullName), Is.Null, type.FullName);
            }
        }

        [Test]
        public void MountedGunUseSpec_StaysInCore_ForTheBotSettings()
        {
            Assert.That(typeof(MountedGunUseSpec).Assembly.GetName().Name, Is.EqualTo(CoreAssembly));
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceTheVehicleAssembly()
        {
            var references = typeof(MountedGunUseSpec).Assembly.GetReferencedAssemblies().Select(a => a.Name);
            Assert.That(references, Has.None.EqualTo(VehicleAssembly));
        }

        [Test]
        public void VehicleAssembly_BuildsOnlyOnCoreAndWorld_WithoutTheEngine()
        {
            var references = typeof(MapVehicle).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(
                references.Where(n => n.StartsWith("Shooter.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal),
                Is.EqualTo(new[] { CoreAssembly, "Shooter.Core.World" }));
            Assert.That(references, Has.None.StartsWith("UnityEngine"));
            Assert.That(references, Has.None.StartsWith("UnityEditor"));
        }
    }
}
