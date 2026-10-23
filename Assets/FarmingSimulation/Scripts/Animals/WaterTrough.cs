using System;
using UnityEngine;

namespace FarmingSimulation
{
    // Uses the same interaction contract as the food trough; stores water only, never animal thirst.
    public sealed class WaterTrough : FarmInteractable
    {
        [SerializeField, Min(1)] private int capacity = 10;
        [SerializeField, Min(0)] private int currentWater;
        [SerializeField, Min(1)] private int refillAmount = 5;
        [SerializeField, Min(1)] private int unitsPerDrink = 1;
        [SerializeField] private Transform drinkingPoint;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private Transform waterVisual;
        public int Capacity => capacity;
        public int CurrentWater => currentWater;
        public bool HasPortion => HasPortions(1);
        public bool HasPortions(int portions) => isActiveAndEnabled && portions > 0
            && portions <= currentWater / unitsPerDrink;
        public Transform DrinkingPoint => drinkingPoint;
        public Transform ApproachPoint => approachPoint;
        public event Action WaterChanged;
        public override string Prompt => currentWater >= capacity ? "Water Trough Full" : "Press E to Refill Water";
        public override string Details => "Water: " + currentWater + "/" + capacity
            + " | Drink: " + unitsPerDrink + " | Refill: +" + refillAmount;

        private void Awake() { ClampSettings(); UpdateWaterVisual(); }
        private void OnValidate() => ClampSettings();
        private void ClampSettings()
        {
            capacity = Mathf.Max(1, capacity);
            currentWater = Mathf.Clamp(currentWater, 0, capacity);
            refillAmount = Mathf.Max(1, refillAmount);
            unitsPerDrink = Mathf.Clamp(unitsPerDrink, 1, capacity);
        }

        public override void Interact()
        {
            if (!isActiveAndEnabled) return;
            int added = Mathf.Min(refillAmount, capacity - currentWater);
            if (added <= 0) return;
            currentWater += added;
            PublishChange();
        }

        public bool TryConsumePortion() => TryConsumePortions(1);

        public bool TryConsumePortions(int portions)
        {
            if (!HasPortions(portions)) return false;
            currentWater -= unitsPerDrink * portions;
            PublishChange();
            return true;
        }

        private void PublishChange()
        {
            UpdateWaterVisual();
            WaterChanged?.Invoke();
            NotifyFeedbackChanged();
        }

        private void UpdateWaterVisual()
        {
            if (waterVisual == null) return;
            waterVisual.gameObject.SetActive(currentWater > 0);
            float height = 0.28f * currentWater / capacity;
            waterVisual.localScale = new Vector3(1.9f, height, 0.48f);
            waterVisual.localPosition = new Vector3(0f, 0.25f + height * 0.5f, 0f);
        }
    }
}
