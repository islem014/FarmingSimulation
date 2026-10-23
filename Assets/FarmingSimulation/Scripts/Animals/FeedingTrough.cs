using System;
using UnityEngine;

namespace FarmingSimulation
{
    // Owns food and its display. Animals must pay for a portion before reducing hunger.
    public sealed class FeedingTrough : FarmInteractable
    {
        [SerializeField, Min(1)] private int capacity = 10;
        [SerializeField, Min(0)] private int currentFood;
        [SerializeField, Min(1)] private int refillAmount = 5;
        [SerializeField, Min(1)] private int unitsPerPortion = 1;
        [SerializeField] private Transform feedingPoint;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private Transform hayVisual;
        public int Capacity => capacity;
        public int CurrentFood => currentFood;
        public bool HasPortion => HasPortions(1);
        public bool HasPortions(int portions) => isActiveAndEnabled && portions > 0
            && portions <= currentFood / unitsPerPortion;
        public Transform FeedingPoint => feedingPoint;
        public Transform ApproachPoint => approachPoint;
        public event Action FoodChanged;
        public override string Prompt => currentFood >= capacity ? "Trough Full" : "Press E to Refill Trough";
        public override string Details => "Food: " + currentFood + "/" + capacity
            + " | Portion: " + unitsPerPortion + " | Refill: +" + refillAmount;

        private void Awake() { ClampSettings(); UpdateHay(); }
        private void OnValidate() { ClampSettings(); }
        private void ClampSettings()
        {
            capacity = Mathf.Max(1, capacity);
            currentFood = Mathf.Clamp(currentFood, 0, capacity);
            refillAmount = Mathf.Max(1, refillAmount);
            unitsPerPortion = Mathf.Clamp(unitsPerPortion, 1, capacity);
        }

        public override void Interact()
        {
            if (!isActiveAndEnabled) return;
            // Cap the addition before adding, so even large Inspector values cannot overflow.
            int added = Mathf.Min(refillAmount, capacity - currentFood);
            if (added <= 0) return;
            currentFood += added;
            PublishChange();
        }

        public bool TryConsumePortion() => TryConsumePortions(1);

        public bool TryConsumePortions(int portions)
        {
            // Check the whole request, then debit before publishing events. No partial/free bites.
            if (!HasPortions(portions)) return false;
            currentFood -= unitsPerPortion * portions;
            PublishChange();
            return true;
        }

        private void PublishChange()
        {
            UpdateHay();
            FoodChanged?.Invoke();
            NotifyFeedbackChanged();
        }

        private void UpdateHay()
        {
            if (hayVisual == null) return;
            hayVisual.gameObject.SetActive(currentFood > 0);
            float height = 0.28f * currentFood / capacity;
            hayVisual.localScale = new Vector3(1.9f, height, 0.48f);
            hayVisual.localPosition = new Vector3(0f, 0.25f + height * 0.5f, 0f);
        }
    }
}
