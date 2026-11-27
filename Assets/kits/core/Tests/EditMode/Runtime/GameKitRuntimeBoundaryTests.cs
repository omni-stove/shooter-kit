using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace GameKit.Runtime.Tests
{
    public sealed class GameKitRuntimeBoundaryTests
    {
        private static readonly Assembly _runtime = typeof(AppliedInputBindings).Assembly;

        [TestCase(typeof(AppliedInputBindings))]
        [TestCase(typeof(InputBindingOverridesStore))]
        [TestCase(typeof(HeldInput))]
        [TestCase(typeof(InputDeviceKinds))]
        [TestCase(typeof(CloudSaveSync))]
        [TestCase(typeof(ICloudSaveBackend))]
        [TestCase(typeof(FileSaveStorage))]
        public void RuntimeTypeIsPublicInTheGameKitRuntime(System.Type type)
        {
            Assert.That(type.Namespace, Is.EqualTo("GameKit.Runtime"));
            Assert.That(type.Assembly, Is.SameAs(_runtime));
            Assert.That(type.IsPublic, Is.True);
        }

        [Test]
        public void RuntimeReferencesNoGame()
        {
            var references = _runtime.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(_runtime.GetName().Name, Is.EqualTo("GameKit.Runtime"));
            Assert.That(references, Has.None.StartsWith("Shooter."));
            Assert.That(_runtime.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName), Has.None.StartsWith("Shooter."));
        }

        [Test]
        public void SavedBindingsKeepTheirKeyFromTheShooterKit()
        {
            Assert.That(InputBindingOverridesStore.DefaultKey, Is.EqualTo("Shooter.InputBindingOverrides"));
            Assert.That(AppliedInputBindings.LogTag, Is.EqualTo("[Input]"));
        }

        [Test]
        public void SavesKeepTheirPlaceFromTheShooterKit()
        {
            Assert.That(FileSaveStorage.DefaultDirectoryName, Is.EqualTo("saves"));
            Assert.That(FileSaveStorage.FileExtension, Is.EqualTo(".json"));
            Assert.That(FileSaveStorage.LogTag, Is.EqualTo("[Campaign]"));
        }
    }
}
