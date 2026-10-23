using UnityEngine;

namespace FarmingSimulation
{
    // Compatibility wrapper for existing sheep prefabs; all interaction logic is shared.
    [RequireComponent(typeof(SheepBehaviour), typeof(AnimalStats), typeof(AnimalCare))]
    public sealed class SheepInteractable : AnimalInteractable { }
}
