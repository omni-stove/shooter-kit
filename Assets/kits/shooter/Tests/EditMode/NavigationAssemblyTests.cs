using System.Linq;
using GameKit.Navigation;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    public sealed class NavigationAssemblyTests
    {
        private const string NavigationAssembly = "GameKit.Navigation";
        private const string CoreAssembly = "Shooter.Core";

        [Test]
        public void NavigationTypesLiveInTheGameKitNavigationAssembly()
        {
            Assert.That(typeof(MeshNavigation).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavMeshFile).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavLink).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavLinkKind).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavTriangle).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
        }

        [Test]
        public void TheNavigationContractMovesWithTheNavigation()
        {
            Assert.That(typeof(INavigation).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavPath).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavPathPoint).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavPathStatus).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
        }

        [Test]
        public void CoreDependsOnNavigationButNavigationDoesNotDependOnTheGame()
        {
            var navigationReferences = typeof(MeshNavigation).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
            var coreReferences = typeof(MountedGunUseSpec).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

            Assert.That(coreReferences, Does.Contain(NavigationAssembly));
            Assert.That(navigationReferences, Does.Not.Contain(CoreAssembly));
            Assert.That(navigationReferences.Where(name => name.StartsWith("Shooter.")), Is.Empty);
        }

        [Test]
        public void NavigationDoesNotReferenceTheEngine()
        {
            var navigationReferences = typeof(MeshNavigation).Assembly.GetReferencedAssemblies().Select(name => name.Name);

            Assert.That(navigationReferences.Where(name => name.StartsWith("UnityEngine")), Is.Empty);
        }
    }
}
