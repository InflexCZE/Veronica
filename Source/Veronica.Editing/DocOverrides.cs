namespace Veronica.Editing;

// Corrections of what the game itself declares wrongly or leaves out about a member: a unit attribute that names the
// wrong quantity or is missing, or a doc comment that describes another field. Hand-kept; entries join as they are
// confirmed in the game code, and leave when the game is fixed.
public static class DocOverrides
{
    // Unit: a symbol to show instead, or "" to show none. Null leaves the game's own.
    public sealed record Entry(string? Unit = null, string? Summary = null);

    private static readonly Dictionary<string, Entry> Entries = new()
    {
        // [FlowRate] says L/s for every resource; the unit is the resource type's own flow unit.
        ["Keen.Game2.Simulation.WorldObjects.CubeBlocks.ResourceDistribution.Resources.ResourceContainerDefinition.MaxChargingRate"] = new(Unit: "",
            Summary: "Max rate at which the container can be filled, per second, in its resource type's flow unit (kW for Electricity, L/s for gases and water)."),
        ["Keen.Game2.Simulation.WorldObjects.CubeBlocks.ResourceDistribution.Resources.ResourceContainerDefinition.MaxDischargeRate"] = new(Unit: "",
            Summary: "Max rate at which the container can be emptied, per second, in its resource type's flow unit (kW for Electricity, L/s for gases and water)."),
        // [Impulse] on forces and a speed: the thrust is applied as force times the step length.
        ["Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement.ThrusterDefinition.ThrustPower"] = new(Unit: "N"),
        ["Keen.Game2.Simulation.WorldObjects.Characters.JetpackMovementComponentDefinition.MaxThrust"] = new(Unit: "N"),
        ["Keen.Game2.Simulation.WorldObjects.Characters.ContactDamageReceiverDefinition.MinSeparationSpeed"] = new(Unit: "m/s"),
        // [Length] on a plain multiplier.
        ["Keen.Game2.Simulation.WorldObjects.Shared.Visuals.Lights.LightDefinition.FalloffRangeMultiplier"] = new(Unit: ""),
        // No unit attribute; the inventory screens show it in kg, and items' MassPerUnit is [Mass].
        ["Keen.Game2.Simulation.WorldObjects.Items.InventoryDefinition.MaxMass"] = new(Unit: "kg"),
        // Documented as the destruction effect; it is the thrust flame.
        ["Keen.Game2.Client.WorldObjects.CubeBlocks.Effects.ThrusterEffectsComponentDefinition.OnThrustPoweredParticleEffect"] = new(
            Summary: "Looped flame effect shown at each thrust dummy while the thruster is working, driven by how hard it thrusts."),
    };

    // Keyed by the declaring type's full name, or the definition or component it is the object builder of.
    public static Entry? For(Type declaring, Type shown, string member) =>
        Entries.GetValueOrDefault($"{declaring.FullName}.{member}") ?? Entries.GetValueOrDefault($"{shown.FullName}.{member}");
}
