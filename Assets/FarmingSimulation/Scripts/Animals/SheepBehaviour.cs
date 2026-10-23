using UnityEngine;

namespace FarmingSimulation
{
    // Compatibility wrapper: preserves Daisy's script GUID and serialized scene references.
    [RequireComponent(typeof(CharacterController), typeof(AnimalStats))]
    public sealed class SheepBehaviour : FarmAnimalBehaviour { }
}
