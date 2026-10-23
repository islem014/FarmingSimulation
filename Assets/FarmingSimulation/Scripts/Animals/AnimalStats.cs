using System;
using UnityEngine;

namespace FarmingSimulation
{
    // Append states so existing serialized values retain their meaning.
    public enum AnimalBehaviour { Idle, Wander, Hungry, Eating, SeekingFood, SeekingWater, Drinking, NeedsCare, Thirsty, SeekingRest, Resting }
    public enum AnimalCondition { Healthy, NeedsCare, Sick }
    public enum WelfareCondition { Critical, NeedsAttention, Good, Excellent }

    public sealed class AnimalStats : MonoBehaviour
    {
        [SerializeField] private string animalName = "Daisy";
        [SerializeField] private string species = "Sheep";
        // Hunger/thirst: 0 = satisfied, 100 = maximum need. Health: 100 = healthy.
        [SerializeField, Range(0, 100)] private float hunger = 45f;
        [SerializeField, Range(0, 100)] private float thirst = 20f;
        [SerializeField, Range(0, 100)] private float health = 100f;
        [Header("Energy — movement costs energy; only rest restores it")]
        [SerializeField, Range(0, 100)] private float energy = 100f;
        [SerializeField, Min(0)] private float energyPerMetre = 3f;
        [SerializeField, Min(0.1f)] private float restRecoveryPerSecond = 6f;
        [SerializeField, Range(0, 99)] private float restThreshold = 25f;
        [SerializeField, Range(1, 100)] private float recoveredThreshold = 80f;
        [Header("Welfare — simplified gameplay score, not a diagnosis")]
        [SerializeField, Range(0, 98)] private float welfareAttentionThreshold = 40f;
        [SerializeField, Range(1, 99)] private float welfareGoodThreshold = 65f;
        [SerializeField, Range(2, 100)] private float welfareExcellentThreshold = 85f;
        [SerializeField, Min(0)] private float hungerPerSecond = 0.8f;
        [SerializeField, Min(0)] private float thirstPerSecond = 0.6f;
        [Header("Health deterioration")]
        [SerializeField, Range(1, 100)] private float severeHungerThreshold = 85f;
        [SerializeField, Range(1, 100)] private float severeThirstThreshold = 85f;
        [SerializeField, Min(0)] private float severeNeedGraceSeconds = 15f;
        [SerializeField, Min(0)] private float healthDamagePerSecond = 1f;
        [SerializeField, Range(1, 99)] private float careHealthThreshold = 70f;
        [SerializeField, Range(0, 98)] private float sickHealthThreshold = 35f;
        [SerializeField] private AnimalBehaviour behaviour = AnimalBehaviour.Idle;
        private float severeHungerTime, severeThirstTime;
        private int displayedHunger = -1, displayedThirst = -1, displayedHealth = -1;
        private int displayedEnergy = -1, displayedWelfare = -1;
        private WelfareCondition displayedWelfareCondition;
        private AnimalCondition displayedCondition;
        private string displayedWarning;
        public string AnimalName => animalName;
        public string Species => species;
        public float Hunger => hunger;
        public float Thirst => thirst;
        public float Health => health;
        public float Energy => energy;
        public bool Tired => energy <= restThreshold;
        public bool Recovered => energy >= recoveredThreshold;
        // Equal weights make this easy to explain; it never causes additional health damage.
        public float Welfare => Mathf.Clamp(((100f-hunger) + (100f-thirst) + health + energy) / 4f, 0f, 100f);
        public WelfareCondition WelfareStatus => Welfare >= welfareExcellentThreshold ? WelfareCondition.Excellent
            : Welfare >= welfareGoodThreshold ? WelfareCondition.Good
            : Welfare >= welfareAttentionThreshold ? WelfareCondition.NeedsAttention : WelfareCondition.Critical;
        public string WelfareLabel => WelfareStatus == WelfareCondition.NeedsAttention ? "Needs Attention" : WelfareStatus.ToString();
        public bool NeedsTreatment => health <= careHealthThreshold;
        public bool SeverelyThirsty => thirst >= severeThirstThreshold;
        public bool SeverelyHungry => hunger >= severeHungerThreshold;
        public AnimalBehaviour Behaviour => behaviour;
        public AnimalCondition Condition => health <= sickHealthThreshold ? AnimalCondition.Sick
            : NeedsTreatment || hunger >= severeHungerThreshold || SeverelyThirsty
                ? AnimalCondition.NeedsCare : AnimalCondition.Healthy;
        public string Warning => hunger >= severeHungerThreshold && SeverelyThirsty ? "Severe hunger and thirst"
            : SeverelyThirsty ? "Severe thirst" : hunger >= severeHungerThreshold ? "Severe hunger"
            : NeedsTreatment ? "Treatment needed" : Tired ? "Low energy - rest needed" : "Needs stable";
        public event Action Changed;
        // Dashboards discover existing instances once, then track enabled/disabled animals.
        public static event Action<AnimalStats, bool> AvailabilityChanged;
        private void OnEnable() => AvailabilityChanged?.Invoke(this, true);
        private void OnDisable() => AvailabilityChanged?.Invoke(this, false);

        private void Awake() { ClampSettings(); PublishIfChanged(true); }
        private void OnValidate() => ClampSettings();
        private void ClampSettings()
        {
            hunger = Mathf.Clamp(hunger, 0f, 100f);
            thirst = Mathf.Clamp(thirst, 0f, 100f);
            health = Mathf.Clamp(health, 0f, 100f);
            energy = Mathf.Clamp(energy, 0f, 100f);
            energyPerMetre = Mathf.Max(0f, energyPerMetre);
            restRecoveryPerSecond = Mathf.Max(.1f, restRecoveryPerSecond);
            restThreshold = Mathf.Clamp(restThreshold, 0f, 99f);
            recoveredThreshold = Mathf.Clamp(recoveredThreshold, restThreshold + 1f, 100f);
            welfareAttentionThreshold = Mathf.Clamp(welfareAttentionThreshold, 0f, 98f);
            welfareGoodThreshold = Mathf.Clamp(welfareGoodThreshold, welfareAttentionThreshold + 1f, 99f);
            welfareExcellentThreshold = Mathf.Clamp(welfareExcellentThreshold, welfareGoodThreshold + 1f, 100f);
            hungerPerSecond = Mathf.Max(0f, hungerPerSecond);
            thirstPerSecond = Mathf.Max(0f, thirstPerSecond);
            severeHungerThreshold = Mathf.Clamp(severeHungerThreshold, 1f, 100f);
            severeThirstThreshold = Mathf.Clamp(severeThirstThreshold, 1f, 100f);
            severeNeedGraceSeconds = Mathf.Max(0f, severeNeedGraceSeconds);
            healthDamagePerSecond = Mathf.Max(0f, healthDamagePerSecond);
            careHealthThreshold = Mathf.Clamp(careHealthThreshold, 1f, 99f);
            sickHealthThreshold = Mathf.Clamp(sickHealthThreshold, 0f, careHealthThreshold - 1f);
        }

        private void Update() => AdvanceNeeds(Time.deltaTime);

        // One owner for all needs. Separate exposure clocks reset when the corresponding need improves.
        private void AdvanceNeeds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            float oldHunger = hunger, oldThirst = thirst;
            hunger = Mathf.Clamp(hunger + hungerPerSecond * seconds, 0f, 100f);
            thirst = Mathf.Clamp(thirst + thirstPerSecond * seconds, 0f, 100f);
            float hungerDamageTime = Exposure(ref severeHungerTime, oldHunger, hunger,
                hungerPerSecond, severeHungerThreshold, seconds);
            float thirstDamageTime = Exposure(ref severeThirstTime, oldThirst, thirst,
                thirstPerSecond, severeThirstThreshold, seconds);
            // Both severe needs use one damage rate, not accidental double damage.
            health = Mathf.Clamp(health - Mathf.Max(hungerDamageTime, thirstDamageTime) * healthDamagePerSecond, 0f, 100f);
            PublishIfChanged();
        }

        private float Exposure(ref float clock, float before, float after, float rate, float threshold, float seconds)
        {
            if (after < threshold) { clock = 0f; return 0f; }
            if (before < threshold) clock = 0f;
            float severeSeconds = before >= threshold ? seconds
                : rate > 0f ? Mathf.Clamp(seconds - (threshold - before) / rate, 0f, seconds) : 0f;
            float old = clock;
            clock += severeSeconds;
            return Mathf.Max(0f, clock - severeNeedGraceSeconds) - Mathf.Max(0f, old - severeNeedGraceSeconds);
        }

        public void ReduceHunger(float amount)
        {
            hunger = Mathf.Clamp(hunger - Mathf.Max(0f, amount), 0f, 100f);
            if (hunger < severeHungerThreshold) severeHungerTime = 0f;
            PublishIfChanged(true);
        }

        public void ReduceThirst(float amount)
        {
            thirst = Mathf.Clamp(thirst - Mathf.Max(0f, amount), 0f, 100f);
            if (thirst < severeThirstThreshold) severeThirstTime = 0f;
            PublishIfChanged(true);
        }

        public void RestoreHealth(float amount)
        {
            health = Mathf.Clamp(health + Mathf.Max(0f, amount), 0f, 100f);
            PublishIfChanged(true);
        }

        // Called once by the AI after CharacterController.Move, using actual planar displacement.
        // Gravity, blocked movement, feeding and drinking do not grant free energy.
        public void AdvanceEnergy(float metresMoved, float secondsResting)
        {
            energy = Mathf.Clamp(energy - Mathf.Max(0f, metresMoved) * energyPerMetre
                + Mathf.Max(0f, secondsResting) * restRecoveryPerSecond, 0f, 100f);
            PublishIfChanged();
        }

        private void PublishIfChanged(bool force = false)
        {
            int h = Mathf.CeilToInt(hunger), t = Mathf.CeilToInt(thirst), life = Mathf.CeilToInt(health);
            int e = Mathf.CeilToInt(energy), w = Mathf.RoundToInt(Welfare);
            if (!force && h == displayedHunger && t == displayedThirst && life == displayedHealth
                && e == displayedEnergy && w == displayedWelfare && WelfareStatus == displayedWelfareCondition
                && Condition == displayedCondition && Warning == displayedWarning) return;
            displayedHunger = h; displayedThirst = t; displayedHealth = life;
            displayedEnergy = e; displayedWelfare = w; displayedWelfareCondition = WelfareStatus;
            displayedCondition = Condition; displayedWarning = Warning;
            Changed?.Invoke();
        }

        public void SetBehaviour(AnimalBehaviour value)
        {
            if (behaviour == value) return;
            behaviour = value;
            Changed?.Invoke();
        }
    }
}

