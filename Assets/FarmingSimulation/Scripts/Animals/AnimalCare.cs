using System;
using UnityEngine;

namespace FarmingSimulation
{
    [RequireComponent(typeof(AnimalStats))]
    public sealed class AnimalCare : MonoBehaviour
    {
        [SerializeField, Min(1)] private float healthRestored = 35f;
        [SerializeField, Min(1)] private float treatmentCooldown = 15f;
        [SerializeField, Min(0.1f)] private float feedbackDuration = 3f;
        private AnimalStats stats;
        private float nextTreatmentTime, confirmationUntil;
        private int lastCooldown = -1;
        private bool lastConfirmation;
        public event Action FeedbackChanged;
        public float CooldownRemaining => Mathf.Max(0f, nextTreatmentTime - Time.time);
        public bool CanTreat => isActiveAndEnabled && stats != null && stats.NeedsTreatment && CooldownRemaining <= 0f;
        public bool ShowingConfirmation => Time.time < confirmationUntil;

        private void Awake() { stats = GetComponent<AnimalStats>(); ClampSettings(); }
        private void OnValidate() => ClampSettings();
        private void ClampSettings()
        {
            healthRestored = Mathf.Max(1f, healthRestored);
            treatmentCooldown = Mathf.Max(1f, treatmentCooldown);
            feedbackDuration = Mathf.Max(0.1f, feedbackDuration);
        }

        public bool TryTreat()
        {
            if (!CanTreat) return false;
            // Set cooldown first: event listeners cannot re-enter and receive a second treatment.
            nextTreatmentTime = Time.time + treatmentCooldown;
            confirmationUntil = Time.time + feedbackDuration;
            stats.RestoreHealth(healthRestored);
            FeedbackChanged?.Invoke();
            return true;
        }

        private void Update()
        {
            int seconds = Mathf.CeilToInt(CooldownRemaining);
            bool confirmation = ShowingConfirmation;
            if (seconds == lastCooldown && confirmation == lastConfirmation) return;
            lastCooldown = seconds; lastConfirmation = confirmation;
            FeedbackChanged?.Invoke();
        }
    }
}
