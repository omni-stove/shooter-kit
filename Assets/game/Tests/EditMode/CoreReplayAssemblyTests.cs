using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Shooter.Core;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // dotnet の Shooter.Core は Core/Replay も 1 つのアセンブリにまとめてビルドするので、アセンブリの境目は Unity のテストでだけ確かめる
    public sealed class CoreReplayAssemblyTests
    {
        private const string ReplayAssembly = "Shooter.Core.Replay";

        [Test]
        public void ReplayTypes_LiveInTheReplayAssembly()
        {
            var types = new[]
            {
                typeof(ReplayEntry),
                typeof(ReplayFile),
                typeof(ReplayHeader),
                typeof(ReplayJson),
                typeof(ReplayLog),
                typeof(ReplayReader),
                typeof(ReplayRecord),
                typeof(ReplayRecordKind),
                typeof(ReplaySpec),
                typeof(ReplayWriter),
            };

            foreach (var type in types)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(ReplayAssembly), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("Shooter.Core"), type.Name);
            }
        }

        [Test]
        public void ReplayAssembly_ReferencesOnlyCoreAndNotTheEngine()
        {
            var references = typeof(ReplayFile).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(references.Where(n => n.StartsWith("Shooter.")), Is.SubsetOf(new[] { "Shooter.Core", "Shooter.Core.World" }));
            Assert.That(references, Has.None.StartsWith("UnityEngine"));
            Assert.That(references, Has.None.StartsWith("UnityEditor"));
        }

        [Test]
        public void ReplayAssembly_SharesItsInternalsOnlyWithCoreTests()
        {
            var friends = typeof(ReplayFile).Assembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(a => a.AssemblyName)
                .ToArray();

            Assert.That(friends, Is.EquivalentTo(new[] { "Shooter.Core.Tests" }));
        }

        [Test]
        public void ReplayFileText_StaysInternal()
        {
            var text = typeof(ReplayFile).GetMethod("Text", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(text, Is.Not.Null);
            Assert.That(text.IsAssembly, Is.True);
        }
    }
}
