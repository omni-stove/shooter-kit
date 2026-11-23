using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では RuntimeInitializeOnLoadMethod も SubtitleView.OnEnable も走らないので、Install と BarkEvents の購読をテストが自分で行う。
    // 字幕の文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/barks.json) から引く。
    public sealed class EnemyBarkInstallerTests
    {
        private const string SpottedLine = "[Bark] speaker=enemy-1 archetype=Rifleman kind=Spotted hearing=30.00";

        private readonly List<BarkCue> _cues = new List<BarkCue>();
        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;
        private SubtitleView _view;

        [SetUp]
        public void SetUp()
        {
            BarkBoard.Use(new BarkSpec());
            BarkEvents.Spoken += Record;

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _created.Add(_rifle);
        }

        [TearDown]
        public void TearDown()
        {
            EnemyBarkInstaller.Uninstall();
            BarkEvents.Spoken -= Record;
            if (_view != null)
            {
                BarkEvents.Spoken -= _view.Show;
            }

            _view = null;
            _cues.Clear();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            BarkBoard.Reset();
        }

        [Test]
        public void InitializingAnEnemyAttachesExactlyOneBarker()
        {
            EnemyBarkInstaller.Install(false);
            Assert.That(EnemyBarkInstaller.IsInstalled, Is.True);
            var agent = CreateAgent("enemy-1");
            Assert.That(agent.GetComponents<EnemyBarker>(), Is.Empty, "nothing is attached before the enemy is initialized");

            Assert.That(agent.TryInitialize(), Is.True);

            var barkers = agent.GetComponents<EnemyBarker>();
            Assert.That(barkers.Length, Is.EqualTo(1));
            Assert.That(barkers[0].Agent, Is.SameAs(agent));

            EnemyBarkInstaller.Install(false);
            Assert.That(EnemyBarkInstaller.AttachTo(agent), Is.SameAs(barkers[0]));
            Assert.That(agent.GetComponents<EnemyBarker>().Length, Is.EqualTo(1), "installing again or attaching again does not add a second barker");
        }

        [Test]
        public void ABarkerAlreadyOnTheEnemyIsKept()
        {
            EnemyBarkInstaller.Install(false);
            var agent = CreateAgent("enemy-1");
            var placed = agent.gameObject.AddComponent<EnemyBarker>();

            Assert.That(agent.TryInitialize(), Is.True);

            var barkers = agent.GetComponents<EnemyBarker>();
            Assert.That(barkers.Length, Is.EqualTo(1));
            Assert.That(barkers[0], Is.SameAs(placed));
        }

        [Test]
        public void InstallingAttachesToEnemiesThatWereAlreadyInitialized()
        {
            var agent = CreateAgent("enemy-1");
            Assert.That(agent.TryInitialize(), Is.True);
            Assert.That(agent.GetComponents<EnemyBarker>(), Is.Empty);

            EnemyBarkInstaller.Install(false);

            Assert.That(agent.GetComponents<EnemyBarker>().Length, Is.EqualTo(1));
        }

        [Test]
        public void ADedicatedServerDoesNotAttachBarkers()
        {
            EnemyBarkInstaller.Install(true);
            Assert.That(EnemyBarkInstaller.IsInstalled, Is.False);
            var agent = CreateAgent("enemy-1");

            Assert.That(agent.TryInitialize(), Is.True);

            Assert.That(agent.GetComponents<EnemyBarker>(), Is.Empty);
        }

        [Test]
        public void AfterUninstallingNewEnemiesGetNoBarker()
        {
            EnemyBarkInstaller.Install(false);
            var first = CreateAgent("enemy-1");
            Assert.That(first.TryInitialize(), Is.True);

            EnemyBarkInstaller.Uninstall();
            Assert.That(EnemyBarkInstaller.IsInstalled, Is.False);
            var second = CreateAgent("enemy-2");
            Assert.That(second.TryInitialize(), Is.True);

            Assert.That(first.GetComponents<EnemyBarker>().Length, Is.EqualTo(1), "a barker already attached is left in place");
            Assert.That(second.GetComponents<EnemyBarker>(), Is.Empty);
        }

        [Test]
        public void AnEnemyTurningFromPatrolToAttackPutsItsSpottedLineInTheSubtitles()
        {
            _view = CreateBoundView();
            BarkEvents.Spoken += _view.Show;
            EnemyBarkInstaller.Install(false);
            var agent = CreateAgent("enemy-1");
            Assert.That(agent.TryInitialize(), Is.True);
            agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) });
            Assert.That(agent.State, Is.EqualTo(EnemyState.Patrol));

            var barker = agent.GetComponent<EnemyBarker>();
            Assert.That(barker, Is.Not.Null);
            barker.Catalog = EmptyCatalog();
            Assert.That(barker.Tick(10f), Is.Null, "the first look only sets what later looks compare against");
            Assert.That(_view.Lines, Is.Empty);

            agent.Brain.Tick(0.1f, new EnemyPerception(
                System.Numerics.Vector3.Zero, new System.Numerics.Vector3(0f, 0f, 5f), true, false, null));
            Assert.That(agent.State, Is.EqualTo(EnemyState.Attack));

            LogAssert.Expect(LogType.Log, SpottedLine);
            barker.Tick(10.1f);

            Assert.That(_cues.Count, Is.EqualTo(1));
            Assert.That(_cues[0].Kind, Is.EqualTo(BarkKind.Spotted));
            Assert.That(_cues[0].SpeakerId, Is.EqualTo("enemy-1"));
            Assert.That(_view.Lines.Count, Is.EqualTo(1));
            Assert.That(Texts(_view), Is.EqualTo(new[] { "敵兵: 見つけたぞ！" }));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        private void Record(BarkCue cue)
        {
            _cues.Add(cue);
        }

        private EnemyAgent CreateAgent(string id)
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = "Enemy " + id;
            _created.Add(host);
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = id;
            return agent;
        }

        private BarkCatalog EmptyCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<BarkCatalog>();
            _created.Add(catalog);
            return catalog;
        }

        private SubtitleView CreateBoundView()
        {
            var gameObject = new GameObject(nameof(SubtitleView));
            _created.Add(gameObject);
            var view = gameObject.AddComponent<SubtitleView>();

            var viewer = new GameObject("viewer");
            _created.Add(viewer);
            viewer.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            view.Viewer = viewer.transform;

            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            view.Bind(layout.CloneTree());
            return view;
        }

        private static string[] Texts(SubtitleView view)
        {
            var labels = view.Element.Query<Label>(className: SubtitleView.LineClass).ToList();
            var texts = new string[labels.Count];
            for (var i = 0; i < labels.Count; i++)
            {
                texts[i] = labels[i].text;
            }

            return texts;
        }
    }
}
