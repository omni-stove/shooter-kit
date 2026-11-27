using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameKit.Navigation;
using GameKit.Net;
using GameKit.World;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class GameKitBoundaryTests
    {
        private static readonly Type[] _kitTypes =
        {
            typeof(JsonNode),
            typeof(ServerDataJson),
            typeof(TimeScaleArbitration),
            typeof(IWorldCollision),
            typeof(WorldGeometry),
            typeof(INavigation),
            typeof(MeshNavigation),
            typeof(UdpMatchTransport),
            typeof(RebindSlot),
            typeof(CloudSaveMerge),
        };

        [TestCase(typeof(JsonNode), "GameKit.Core")]
        [TestCase(typeof(JsonParser), "GameKit.Core")]
        [TestCase(typeof(JsonWriter), "GameKit.Core")]
        [TestCase(typeof(SaveJson), "GameKit.Core")]
        [TestCase(typeof(ServerDataJson), "GameKit.Core")]
        [TestCase(typeof(SpecJsonReader), "GameKit.Core")]
        [TestCase(typeof(SpecGuard), "GameKit.Core")]
        [TestCase(typeof(SpecLists), "GameKit.Core")]
        [TestCase(typeof(TuningJsonException), "GameKit.Core")]
        [TestCase(typeof(TimeScaleArbitration), "GameKit.Core")]
        [TestCase(typeof(InputDeviceKind), "GameKit.Core")]
        [TestCase(typeof(ControlValueRange), "GameKit.Core")]
        [TestCase(typeof(IWorldCollision), "GameKit.World")]
        [TestCase(typeof(BvhWorldCollision), "GameKit.World")]
        [TestCase(typeof(WorldGeometry), "GameKit.World")]
        [TestCase(typeof(CapsuleMover), "GameKit.World")]
        [TestCase(typeof(INavigation), "GameKit.Navigation")]
        [TestCase(typeof(NavPath), "GameKit.Navigation")]
        [TestCase(typeof(MeshNavigation), "GameKit.Navigation")]
        [TestCase(typeof(NavMeshFile), "GameKit.Navigation")]
        [TestCase(typeof(RebindSlot), "GameKit.Core")]
        [TestCase(typeof(RebindResult), "GameKit.Core")]
        [TestCase(typeof(BindingConflict), "GameKit.Core")]
        [TestCase(typeof(IMatchTransport), "GameKit.Net")]
        [TestCase(typeof(UdpMatchTransport), "GameKit.Net")]
        [TestCase(typeof(UdpTransportOptions), "GameKit.Net")]
        [TestCase(typeof(MessageReader), "GameKit.Net")]
        [TestCase(typeof(MessageWriter), "GameKit.Net")]
        [TestCase(typeof(IMessage), "GameKit.Net")]
        [TestCase(typeof(TransportEvent), "GameKit.Net")]
        [TestCase(typeof(ISaveStorage), "GameKit.Core")]
        [TestCase(typeof(AccountLog), "GameKit.Core")]
        [TestCase(typeof(AccountLoginResult), "GameKit.Core")]
        [TestCase(typeof(AccountStatus), "GameKit.Core")]
        [TestCase(typeof(CloudSaveAction), "GameKit.Core")]
        [TestCase(typeof(CloudSaveEnvelope), "GameKit.Core")]
        [TestCase(typeof(CloudSaveLog), "GameKit.Core")]
        [TestCase(typeof(CloudSaveMerge), "GameKit.Core")]
        [TestCase(typeof(CloudSaveStamp), "GameKit.Core")]
        [TestCase(typeof(CloudSaveStamps), "GameKit.Core")]
        [TestCase(typeof(CosmeticCatalog), "GameKit.Core")]
        [TestCase(typeof(CosmeticJson), "GameKit.Core")]
        [TestCase(typeof(CosmeticLog), "GameKit.Core")]
        [TestCase(typeof(LeaderboardJson), "GameKit.Core")]
        [TestCase(typeof(LeaderboardSpec), "GameKit.Core")]
        [TestCase(typeof(LeaderboardStatus), "GameKit.Core")]
        [TestCase(typeof(PlayerStat), "GameKit.Core")]
        [TestCase(typeof(ReplayFile), "GameKit.Core")]
        [TestCase(typeof(ReplayReader), "GameKit.Core")]
        [TestCase(typeof(ReplayWriter), "GameKit.Core")]
        [TestCase(typeof(ReplaySpec), "GameKit.Core")]
        [TestCase(typeof(ReplayJson), "GameKit.Core")]
        [TestCase(typeof(Playlist), "GameKit.Core")]
        [TestCase(typeof(PlaylistCursor), "GameKit.Core")]
        [TestCase(typeof(PlaylistJson), "GameKit.Core")]
        [TestCase(typeof(PlaylistLog), "GameKit.Core")]
        [TestCase(typeof(ChatGate), "GameKit.Core")]
        [TestCase(typeof(ChatLine), "GameKit.Core")]
        [TestCase(typeof(ChatChannel), "GameKit.Core")]
        [TestCase(typeof(ChatWordFilter), "GameKit.Core")]
        [TestCase(typeof(ChatWordFilterJson), "GameKit.Core")]
        public void KitTypeLivesInItsGameKitNamespace(Type type, string expected)
        {
            Assert.That(type.Namespace, Is.EqualTo(expected));
        }

        [TestCase(typeof(JsonNode))]
        [TestCase(typeof(JsonParser))]
        [TestCase(typeof(JsonWriter))]
        [TestCase(typeof(SaveJson))]
        [TestCase(typeof(ServerDataJson))]
        [TestCase(typeof(SpecJsonReader))]
        [TestCase(typeof(SpecGuard))]
        [TestCase(typeof(SpecLists))]
        [TestCase(typeof(UdpMatchTransport))]
        [TestCase(typeof(MessageReader))]
        [TestCase(typeof(RebindSlot))]
        [TestCase(typeof(ISaveStorage))]
        [TestCase(typeof(CloudSaveMerge))]
        [TestCase(typeof(CloudSaveEnvelope))]
        [TestCase(typeof(CosmeticJson))]
        [TestCase(typeof(LeaderboardJson))]
        [TestCase(typeof(ReplayFile))]
        [TestCase(typeof(ReplayReader))]
        [TestCase(typeof(ReplayWriter))]
        [TestCase(typeof(PlaylistCursor))]
        [TestCase(typeof(PlaylistLog))]
        [TestCase(typeof(ChatGate))]
        [TestCase(typeof(ChatWordFilter))]
        [TestCase(typeof(WorldGeometry))]
        public void KitHelperIsPublicForEveryGame(Type type)
        {
            Assert.That(type.IsPublic, Is.True);
        }

        [Test]
        public void KitAssembliesReferenceNoGameAndNoEngine()
        {
            foreach (var assembly in _kitTypes.Select(t => t.Assembly).Distinct())
            {
                var references = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

                Assert.That(references, Has.None.StartsWith("Shooter."), assembly.GetName().Name);
                Assert.That(references, Has.None.StartsWith("UnityEngine"), assembly.GetName().Name);
                Assert.That(references, Has.None.StartsWith("UnityEditor"), assembly.GetName().Name);
            }
        }

        [Test]
        public void KitAssembliesShareTheirInternalsWithNoGame()
        {
            foreach (var assembly in _kitTypes.Select(t => t.Assembly).Distinct())
            {
                var friends = assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).ToArray();

                Assert.That(friends, Has.None.StartsWith("Shooter."), assembly.GetName().Name);
            }
        }

        [Test]
        public void KitTypesDeclareNoShooterNamespace()
        {
            foreach (var assembly in _kitTypes.Select(t => t.Assembly).Distinct())
            {
                var namespaces = assembly.GetTypes().Select(t => t.Namespace).Where(n => n != null).Distinct().ToArray();

                Assert.That(namespaces, Has.None.StartsWith("Shooter"), assembly.GetName().Name);
            }
        }

        [Test]
        public void ReplayFileTextStaysInsideTheKit()
        {
            var text = typeof(ReplayFile).GetMethod("Text", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(text, Is.Not.Null);
            Assert.That(text.IsAssembly, Is.True);
        }

        [Test]
        public void ReplaySpecKeepsTheShooterKitDefaultFreeSpeed()
        {
            Assert.That(ReplaySpec.DefaultFreeSpeed, Is.EqualTo(6f));
        }
    }
}
