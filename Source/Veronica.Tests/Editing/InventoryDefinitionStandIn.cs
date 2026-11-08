using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;

// Same full name as the game's InventoryDefinition, which the tests do not reference, so DocOverrides keys on it.
namespace Keen.Game2.Simulation.WorldObjects.Items;

[Serialize]
public partial class InventoryDefinition
{
    public FixedPoint MaxMass = (FixedPoint)10000;
}
