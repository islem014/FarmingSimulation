using System;
using UnityEngine;

namespace FarmingSimulation
{
    public enum PlotState { Empty, Planted, Watered }

    public sealed class FarmPlot : FarmInteractable
    {
        [SerializeField] private Renderer soilRenderer;
        [SerializeField] private Material drySoil;
        [SerializeField] private Material wetSoil;
        [SerializeField] private GameObject seedlingPrefab;
        [SerializeField] private Transform plantingPoint;

        public PlotState State { get; private set; } = PlotState.Empty;
        public event Action<FarmPlot> StateChanged;
        private GameObject seedling;
        public override string Prompt => State == PlotState.Empty ? "Press E to Plant"
            : State == PlotState.Planted ? "Press E to Water" : "Plot Watered";
        public override string Details => name + " | State: " + State;

        private void Awake()
        {
            if (soilRenderer == null || drySoil == null || wetSoil == null
                || seedlingPrefab == null || plantingPoint == null)
            {
                Debug.LogError("FarmPlot needs soil materials, a renderer, a seedling prefab and a planting point.", this);
                enabled = false;
                return;
            }
            soilRenderer.sharedMaterial = drySoil;
        }

        // Called by PlayerInteractor. Every plot owns its own state and seedling.
        public override void Interact()
        {
            if (!isActiveAndEnabled)
                return;

            switch (State)
            {
                case PlotState.Empty:
                    if (seedling != null)
                        return;
                    seedling = Instantiate(seedlingPrefab, plantingPoint.position,
                        plantingPoint.rotation, transform);
                    State = PlotState.Planted;
                    break;
                case PlotState.Planted:
                    // Assign a different material; never edit the shared material asset.
                    soilRenderer.sharedMaterial = wetSoil;
                    State = PlotState.Watered;
                    break;
                case PlotState.Watered:
                    return; // Finished for Suivi 1: repeated presses do nothing.
            }
            StateChanged?.Invoke(this);
            NotifyFeedbackChanged();
        }
    }
}
