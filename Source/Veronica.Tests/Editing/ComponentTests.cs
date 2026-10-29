using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.UI.PropertyInspector.Visibility;
using Veronica.Editing;
using Xunit;

namespace Keen.VRage.Core.Game.Definitions
{
    // Stands in for the game's prefab root, which turns on the inspector's stricter component visibility.
    [Serialize]
    public partial class PrefabDefinitionObjectBuilder
    {
        public List<object?> ObjectBuilders = [new Veronica.Tests.Editing.PartBuilder()];
    }
}

namespace Veronica.Tests.Editing
{
    [Serialize]
    public partial class PartBuilder
    {
        [ForceShowInInspector]
        public float Shown = 1;

        public float Internal = 2;

        [ForceShowInInspector]
        public Entry Settings = new();
    }

    public sealed class ComponentTests
    {
        private static readonly Guid A = new("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid B = new("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly Guid C = new("bbbbbbbb-c000-0000-0000-000000000003");
        private static readonly Guid D = new("dddddddd-0000-0000-0000-000000000004");

        private static readonly List<ComponentEntry> Components =
        [
            new(A, "ThrusterComponent", ["Thruster"]),
            new(B, "FlameComponent", ["MainFlame"]),
            new(C, "FlameComponent", []),
            new(D, "LightComponent", []),
        ];

        [Fact]
        public void ComponentsGoByTypeThenOwnTagThenGuidStart()
        {
            var names = ComponentNames.Assign(Components);
            Assert.Equal("ThrusterComponent", names[A]);
            Assert.Equal("MainFlame", names[B]);
            Assert.Equal("#bbbbbbbbc", names[C]);
            Assert.Equal("LightComponent", names[D]);
        }

        [Fact]
        public void ATagThatIsAnotherComponentsNameIsNotUsed()
        {
            List<ComponentEntry> components =
            [
                new(A, "LightComponent", []),
                new(B, "FlameComponent", ["LightComponent"]),
                new(C, "FlameComponent", ["Spare"]),
            ];
            var names = ComponentNames.Assign(components);
            Assert.Equal("#bbbbbbbb0", names[B]);
            Assert.Equal("Spare", names[C]);
        }

        [Fact]
        public void ATagThatIsATypeWithoutItsSuffixIsNotUsed()
        {
            List<ComponentEntry> components =
            [
                new(A, "ThrusterComponent", []),
                new(B, "FlameComponent", ["Thruster"]),
                new(C, "FlameComponent", ["Flame"]),
            ];
            var names = ComponentNames.Assign(components);
            Assert.Equal("#bbbbbbbb0", names[B]);
            Assert.Equal("#bbbbbbbbc", names[C]);
            Assert.Equal(A, ComponentNames.Find(components, "Thruster").Id);
        }

        [Theory]
        [InlineData("ThrusterComponent", "aaaaaaaa-0000-0000-0000-000000000001")]
        [InlineData("thruster", "aaaaaaaa-0000-0000-0000-000000000001")]
        [InlineData("Light", "dddddddd-0000-0000-0000-000000000004")]
        [InlineData("mainflame", "bbbbbbbb-0000-0000-0000-000000000002")]
        [InlineData("#bbbbbbbbc", "bbbbbbbb-c000-0000-0000-000000000003")]
        [InlineData("#bbbbbbbb-c000", "bbbbbbbb-c000-0000-0000-000000000003")]
        public void FindTakesNamesTypesTagsAndGuidStarts(string token, string id) =>
            Assert.Equal(new Guid(id), ComponentNames.Find(Components, token).Id);

        [Fact]
        public void ARepeatedTypeAsksForOneOfItsNames()
        {
            var e = Assert.Throws<EditError>(() => ComponentNames.Find(Components, "FlameComponent"));
            Assert.Contains("Components[MainFlame], Components[#bbbbbbbbc]", e.Message);
        }

        [Fact]
        public void AnUnknownComponentListsTheNames()
        {
            var e = Assert.Throws<EditError>(() => ComponentNames.Find(Components, "Wheel"));
            Assert.Contains("#bbbbbbbbc, LightComponent, MainFlame, ThrusterComponent", e.Message);
        }

        [Theory]
        [InlineData("Components[Thruster].Settings.Speed", "Thruster", "Settings.Speed")]
        [InlineData("components.Thruster", "Thruster", "")]
        [InlineData("Components[#1a2b3c4d].List[0]", "#1a2b3c4d", "List[0]")]
        public void ComponentPathsSplit(string path, string name, string inner) =>
            Assert.Equal((name, inner), ComponentNames.Split(path));

        [Fact]
        public void OtherPathsAreNotComponentPaths()
        {
            Assert.Null(ComponentNames.Split("ServerComposite"));
            Assert.Null(ComponentNames.Split("Components"));
            Assert.True(ComponentNames.IsCollection("Components"));
        }

        private static MemberReference Part => new ObjectManipulator(new Keen.VRage.Core.Game.Definitions.PrefabDefinitionObjectBuilder()).Root["ObjectBuilders"].GetListItem(0);

        [Fact]
        public void InAPrefabOnlyForceShownFieldsAreVisible()
        {
            string dump = FieldReader.Read(Part, "", new FakeLinks(), basePath: "Components[Part]");
            Assert.Contains("Components[Part].Shown = 1 (float)", dump);
            Assert.Contains("Components[Part].Settings.Speed = 0 (float)", dump);
            Assert.DoesNotContain("Internal", dump);
        }

        [Fact]
        public void AHiddenPrefabFieldIsNotAField()
        {
            var e = Assert.Throws<EditError>(() => FieldWalker.Walk(Part, "Internal", "Components[Part]"));
            Assert.Contains("'Internal' is not a field of 'Components[Part]'. Fields: Shown, Settings.", e.Message);
        }

        [Fact]
        public void OutsideAPrefabTheSameTypeShowsEverything() =>
            Assert.Contains("Internal = 2", FieldReader.Read(new ObjectManipulator(new PartBuilder()).Root, "", new FakeLinks()));
    }
}
