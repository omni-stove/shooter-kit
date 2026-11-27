using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class KitLayoutTests
    {
        private const string _coreKit = "core";
        private const string _corePackage = "com.omnistove.game-kit";

        [Test]
        public void EveryKitFolderIsAPackageNamedAfterIt()
        {
            foreach (var kit in KitFolders())
            {
                var expected = Path.GetFileName(kit) == _coreKit ? _corePackage : _corePackage + "." + Path.GetFileName(kit);
                Assert.That(Member(Package(kit), "name")?.Text, Is.EqualTo(expected), kit);
            }
        }

        [Test]
        public void EveryGenreKitDependsOnTheCoreKitAndTheCoreKitOnNoGenre()
        {
            foreach (var kit in KitFolders())
            {
                var dependencies = (Member(Package(kit), "dependencies")?.Members ?? new List<KeyValuePair<string, JsonNode>>())
                    .Select(member => member.Key)
                    .ToArray();
                if (Path.GetFileName(kit) == _coreKit)
                {
                    Assert.That(dependencies.Where(name => name.StartsWith(_corePackage, StringComparison.Ordinal)), Is.Empty, kit);
                }
                else
                {
                    Assert.That(dependencies, Does.Contain(_corePackage), kit);
                }
            }
        }

        [Test]
        public void KitAssembliesReferenceOnlyTheirOwnKitAndTheCoreKit()
        {
            var kits = KitFolders();
            var owners = new Dictionary<string, string>();
            foreach (var kit in kits)
            {
                foreach (var asmdef in Asmdefs(kit))
                {
                    owners[Member(asmdef.Value, "name").Text] = Path.GetFileName(kit);
                }
            }

            foreach (var kit in kits)
            {
                var own = Path.GetFileName(kit);
                foreach (var asmdef in Asmdefs(kit))
                {
                    var references = Member(asmdef.Value, "references")?.Elements ?? new List<JsonNode>();
                    foreach (var reference in references)
                    {
                        if (!owners.TryGetValue(reference.Text, out var owner))
                        {
                            continue;
                        }

                        Assert.That(owner == own || owner == _coreKit, Is.True, asmdef.Key + " references " + reference.Text + " of the " + owner + " kit");
                    }
                }
            }
        }

        private static string[] KitFolders()
        {
            var kits = KitsRoot();
            if (kits == null)
            {
                Assert.Ignore("Assets/kits exists only in the game-kit repository, not in a project that installs the packages");
            }

            var folders = Directory.GetDirectories(kits).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(folders.Select(Path.GetFileName), Does.Contain(_coreKit));
            return folders;
        }

        // Unity はプロジェクトのルートで、dotnet は bin の下で走らせるので、両方から上へ探す。
        private static string KitsRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
            {
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    var kits = Path.Combine(directory.FullName, "Assets", "kits");
                    if (Directory.Exists(kits))
                    {
                        return kits;
                    }
                }
            }

            return null;
        }

        private static JsonNode Package(string kit)
        {
            var path = Path.Combine(kit, "package.json");
            Assert.That(File.Exists(path), Is.True, path);
            return new JsonParser(File.ReadAllText(path)).ParseDocument();
        }

        private static IEnumerable<KeyValuePair<string, JsonNode>> Asmdefs(string kit)
        {
            return Directory.GetFiles(kit, "*.asmdef", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new KeyValuePair<string, JsonNode>(path, new JsonParser(File.ReadAllText(path)).ParseDocument()));
        }

        private static JsonNode Member(JsonNode node, string name)
        {
            return node.Members.FirstOrDefault(member => member.Key == name).Value;
        }
    }
}
