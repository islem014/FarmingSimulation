using UnityEngine;

namespace FarmingSimulation
{
    [RequireComponent(typeof(FarmAnimalBehaviour), typeof(AnimalStats), typeof(AnimalCare))]
    public class AnimalInteractable : FarmInteractable
    {
        protected FarmAnimalBehaviour sheep;
        protected AnimalStats stats;
        protected AnimalCare care;
        protected bool inspected;
        protected void Awake()
        {
            sheep = GetComponent<FarmAnimalBehaviour>();
            stats = GetComponent<AnimalStats>();
            care = GetComponent<AnimalCare>();
        }
        protected void OnEnable()
        {
            stats.Changed += NotifyFeedbackChanged;
            sheep.FeedbackChanged += NotifyFeedbackChanged;
            if (care != null) care.FeedbackChanged += NotifyFeedbackChanged;
        }
        protected void OnDisable()
        {
            stats.Changed -= NotifyFeedbackChanged;
            sheep.FeedbackChanged -= NotifyFeedbackChanged;
            if (care != null) care.FeedbackChanged -= NotifyFeedbackChanged;
        }
        public override string Prompt => !sheep.isActiveAndEnabled ? "Animal unavailable"
            : care != null && care.ShowingConfirmation ? "Treatment given - keep food and water available"
            : care != null && care.CanTreat ? "Press E to Treat " + stats.Species
            : care != null && care.CooldownRemaining > 0f ? "Treatment cooldown: " + Mathf.CeilToInt(care.CooldownRemaining) + "s"
            : inspected ? sheep.FeedingStatus : "Press E to Inspect " + stats.Species;
        public override string Details => stats.AnimalName + " (" + stats.Species + ")\nHunger " + Mathf.CeilToInt(stats.Hunger)
            + "/100 | Thirst " + Mathf.CeilToInt(stats.Thirst) + "/100 | Health " + Mathf.CeilToInt(stats.Health) + "/100\n"
            + "Energy " + Mathf.CeilToInt(stats.Energy) + "/100 | Welfare " + Mathf.RoundToInt(stats.Welfare) + "/100 (" + stats.WelfareLabel + ")\n"
            + stats.Behaviour + " | " + (stats.Condition == AnimalCondition.NeedsCare ? "Needs Care" : stats.Condition.ToString())
            + " | " + stats.Warning;
        // Shared E action treats only when eligible; inspection never changes food or water needs.
        public override void Interact()
        {
            if (care != null && care.TryTreat()) return;
            inspected = true;
            NotifyFeedbackChanged();
        }
    }
}
