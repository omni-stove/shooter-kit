using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GamepadLayoutJsonTests
    {
        private static readonly string[] _expectedItemNames = { "layouts", "swaps" };
        private static readonly string[] _expectedLayoutItemNames = { "id", "nameKey", "bindings" };
        private static readonly string[] _expectedSwapItemNames = { "id", "nameKey", "pairs" };
        private static readonly string[] _expectedBindingItemNames = { "action", "path" };
        private static readonly string[] _expectedPairItemNames = { "a", "b" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = GamepadLayoutJson.Parse(
                "{\"layouts\": [{\"id\": \"standard\", \"nameKey\": \"gamepadLayout.standard\", \"bindings\": ["
                + "{\"action\": \"Melee\", \"path\": \"<Gamepad>/rightStickPress\"}, {\"action\": \"Prone\", \"path\": \"\"}]}],"
                + " \"swaps\": [{\"id\": \"southpaw\", \"nameKey\": \"gamepadSwap.southpaw\", \"pairs\": ["
                + "{\"a\": \"<Gamepad>/leftStick\", \"b\": \"<Gamepad>/rightStick\"}]}]}",
                "gamepad-layouts.json");

            Assert.That(spec.Layouts.Count, Is.EqualTo(1));
            var layout = spec.Layouts[0];
            Assert.That(layout.Id, Is.EqualTo("standard"));
            Assert.That(layout.NameKey, Is.EqualTo("gamepadLayout.standard"));
            Assert.That(layout.Bindings.Count, Is.EqualTo(2));
            Assert.That(layout.TryGetPath("melee", out var melee), Is.True, "action names are looked up regardless of case, as Input System does");
            Assert.That(melee, Is.EqualTo("<Gamepad>/rightStickPress"));
            Assert.That(layout.TryGetPath("Prone", out var prone), Is.True);
            Assert.That(prone, Is.EqualTo(string.Empty), "an empty path takes the action off the gamepad");
            Assert.That(layout.Bindings[1].IsUnbound, Is.True);
            Assert.That(layout.TryGetPath("Jump", out _), Is.False);

            Assert.That(spec.Swaps.Count, Is.EqualTo(1));
            Assert.That(spec.Swaps[0].Id, Is.EqualTo("southpaw"));
            Assert.That(spec.Swaps[0].NameKey, Is.EqualTo("gamepadSwap.southpaw"));
            Assert.That(spec.Swaps[0].Pairs.Count, Is.EqualTo(1));
            Assert.That(spec.FindLayout("standard"), Is.SameAs(layout));
            Assert.That(spec.FindSwap("southpaw"), Is.SameAs(spec.Swaps[0]));
            Assert.That(spec.FindLayout("missing"), Is.Null);
        }

        [Test]
        public void LeftOutListsKeepTheKitDefaultAndSwapNothing()
        {
            var spec = GamepadLayoutJson.Parse("{}", "gamepad-layouts.json");

            Assert.That(spec.Layouts.Count, Is.EqualTo(1));
            Assert.That(spec.Layouts[0].Id, Is.EqualTo(GamepadLayoutSpec.DefaultLayoutId));
            Assert.That(spec.Layouts[0].NameKey, Is.EqualTo("gamepadLayout.default"));
            Assert.That(spec.Layouts[0].Bindings, Is.Empty);
            Assert.That(spec.Swaps.Count, Is.EqualTo(1));
            Assert.That(spec.Swaps[0].Id, Is.EqualTo(GamepadLayoutSpec.DefaultSwapId));
            Assert.That(spec.Swaps[0].NameKey, Is.EqualTo("gamepadSwap.none"));
            Assert.That(spec.Swaps[0].Pairs, Is.Empty);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(GamepadLayoutJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(GamepadLayoutJson.LayoutItemNames, Is.EqualTo(_expectedLayoutItemNames));
            Assert.That(GamepadLayoutJson.SwapItemNames, Is.EqualTo(_expectedSwapItemNames));
            Assert.That(GamepadLayoutJson.BindingItemNames, Is.EqualTo(_expectedBindingItemNames));
            Assert.That(GamepadLayoutJson.PairItemNames, Is.EqualTo(_expectedPairItemNames));
            Assert.That(GamepadLayoutJson.ResourcePath, Is.EqualTo("Tuning/gamepad-layouts"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var keyboard = Reject("{\"layouts\": [{\"id\": \"a\", \"nameKey\": \"k\", \"bindings\": [{\"action\": \"Jump\", \"path\": \"<Keyboard>/space\"}]}]}");
            var twice = Reject("{\"layouts\": [{\"id\": \"a\", \"nameKey\": \"k\", \"bindings\": ["
                + "{\"action\": \"Jump\", \"path\": \"<Gamepad>/buttonSouth\"}, {\"action\": \"jump\", \"path\": \"\"}]}]}");
            var noPath = Reject("{\"layouts\": [{\"id\": \"a\", \"nameKey\": \"k\", \"bindings\": [{\"action\": \"Jump\"}]}]}");
            var sameId = Reject("{\"layouts\": [{\"id\": \"a\", \"nameKey\": \"k\"}, {\"id\": \"a\", \"nameKey\": \"k\"}]}");
            var noLayouts = Reject("{\"layouts\": []}");
            var noName = Reject("{\"swaps\": [{\"id\": \"s\", \"pairs\": []}]}");
            var self = Reject("{\"swaps\": [{\"id\": \"s\", \"nameKey\": \"k\", \"pairs\": [{\"a\": \"<Gamepad>/leftStick\", \"b\": \"<Gamepad>/leftStick\"}]}]}");
            var reused = Reject("{\"swaps\": [{\"id\": \"s\", \"nameKey\": \"k\", \"pairs\": ["
                + "{\"a\": \"<Gamepad>/leftStick\", \"b\": \"<Gamepad>/rightStick\"}, {\"a\": \"<Gamepad>/rightStick\", \"b\": \"<Gamepad>/dpad\"}]}]}");
            var unknown = Reject("{\"layouts\": [{\"id\": \"a\", \"nameKey\": \"k\", \"hold\": true}]}");

            Assert.That(keyboard.Item, Is.EqualTo("layouts[0].bindings[0].path"));
            Assert.That(keyboard.Message, Does.Contain("<Keyboard>/space"));
            Assert.That(twice.Item, Is.EqualTo("layouts[0].bindings"));
            Assert.That(twice.Message, Does.Contain("'jump'"));
            Assert.That(noPath.Item, Is.EqualTo("layouts[0].bindings[0].path"));
            Assert.That(sameId.Item, Is.EqualTo("layouts"));
            Assert.That(noLayouts.Item, Is.EqualTo("layouts"));
            Assert.That(noName.Item, Is.EqualTo("swaps[0].nameKey"));
            Assert.That(self.Item, Is.EqualTo("swaps[0].pairs[0].b"));
            Assert.That(reused.Item, Is.EqualTo("swaps[0].pairs"));
            Assert.That(reused.Message, Does.Contain("<Gamepad>/rightStick"));
            Assert.That(unknown.Item, Is.EqualTo("layouts[0].hold"));
        }

        [Test]
        public void ASwapExchangesBothWaysAndCarriesStickDirections()
        {
            var swap = new GamepadSwap("southpaw", "gamepadSwap.southpaw", new[]
            {
                new GamepadSwapPair("<Gamepad>/leftStick", "<Gamepad>/rightStick"),
                new GamepadSwapPair("<Gamepad>/leftStickPress", "<Gamepad>/rightStickPress"),
            });

            Assert.That(swap.Apply("<Gamepad>/leftStick"), Is.EqualTo("<Gamepad>/rightStick"));
            Assert.That(swap.Apply("<Gamepad>/rightStick"), Is.EqualTo("<Gamepad>/leftStick"));
            Assert.That(swap.Apply("<Gamepad>/leftStick/up"), Is.EqualTo("<Gamepad>/rightStick/up"));
            Assert.That(swap.Apply("<Gamepad>/leftStickPress"), Is.EqualTo("<Gamepad>/rightStickPress"), "leftStickPress is its own control, not a part of leftStick");
            Assert.That(swap.Apply("<Gamepad>/rightStickPress"), Is.EqualTo("<Gamepad>/leftStickPress"));
            Assert.That(swap.Apply("<Gamepad>/buttonSouth"), Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(swap.Apply(string.Empty), Is.EqualTo(string.Empty), "an unbound action stays unbound");
        }

        [Test]
        public void PathForPlacesTheLayoutThenSwaps()
        {
            var layout = new GamepadLayout("l", "k", new[]
            {
                new GamepadLayoutBinding("Grenade", "<Gamepad>/rightShoulder"),
                new GamepadLayoutBinding("Prone", ""),
            });
            var flipped = new GamepadSwap("flipped", "k", new[] { new GamepadSwapPair("<Gamepad>/rightTrigger", "<Gamepad>/rightShoulder") });

            Assert.That(GamepadLayoutSpec.PathFor(layout, flipped, "Grenade", "<Gamepad>/leftShoulder"), Is.EqualTo("<Gamepad>/rightTrigger"));
            Assert.That(GamepadLayoutSpec.PathFor(layout, flipped, "Attack", "<Gamepad>/rightTrigger"), Is.EqualTo("<Gamepad>/rightShoulder"), "an action the layout leaves out keeps its default, then swaps");
            Assert.That(GamepadLayoutSpec.PathFor(layout, flipped, "Prone", null), Is.EqualTo(string.Empty));
            Assert.That(GamepadLayoutSpec.PathFor(layout, flipped, "Jump", null), Is.EqualTo(string.Empty));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => GamepadLayoutJson.Parse(json, "gamepad-layouts.json"));
        }
    }
}
