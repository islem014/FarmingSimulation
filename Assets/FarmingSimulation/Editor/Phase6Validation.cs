using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmingSimulation.Editor
{
    // Editor-only checks on preview instances. No saved gameplay values are changed by the tests.
    public static class Phase6Validation
    {
        private const string Root = "Assets/FarmingSimulation/Animals/Prefabs/";
        private static readonly StringBuilder Results = new StringBuilder();
        public static string Report => Results.ToString();

        public static void ValidateScene(Scene scene)
        {
            Results.Clear();
            var all = scene.GetRootGameObjects();
            var sheep = all.SelectMany(g => g.GetComponentsInChildren<SheepBehaviour>()).Single();
            var pen = all.SelectMany(g => g.GetComponentsInChildren<AnimalEnclosure>()).Single();
            var water = pen.GetComponentsInChildren<WaterTrough>().Single();
            var food = pen.GetComponentsInChildren<FeedingTrough>().Single();
            Require(sheep.GetComponent<AnimalCare>() != null, "AnimalCare is missing.");
            Require(Reference(sheep, "waterTrough") == water && Reference(sheep, "feedingTrough") == food
                && Reference(sheep, "enclosure") == pen, "Sheep scene references do not match its pen.");
            var animator = sheep.GetComponentInChildren<Animator>();
            Require(Reference(sheep, "animator") == animator && animator.runtimeAnimatorController != null
                && !animator.applyRootMotion, "Animator reference/root motion changed.");
            Require(animator.runtimeAnimatorController.animationClips.All(c => c != null), "Missing animation clip.");
            Require(water.GetComponent<BoxCollider>() != null && water.GetComponent<FeedingTrough>() == null,
                "Water trough must have one water interaction and a physical collider.");
            Physics.SyncTransforms();
            foreach (var point in new[] { water.DrinkingPoint, water.ApproachPoint, food.FeedingPoint, food.ApproachPoint })
            {
                Require(point != null && (pen.ClampPosition(point.position) - point.position).sqrMagnitude < 0.001f,
                    "Trough route point outside walking bounds.");
                var overlaps = Physics.OverlapCapsule(point.position + Vector3.up * 0.6f,
                    point.position + Vector3.up * 0.65f, 0.55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Require(!overlaps.Any(c => !c.transform.IsChildOf(sheep.transform)), "Route point obstructed: " + point.name);
            }
            CheckPath(sheep, sheep.transform.position, water.DrinkingPoint.position);
            CheckPath(sheep, sheep.transform.position, food.FeedingPoint.position);
            CheckPath(sheep, water.DrinkingPoint.position, food.FeedingPoint.position);
            Results.AppendLine("PASS: one sheep, food trough and water trough; care, Animator and scene references assigned.");
            Results.AppendLine("PASS: all four approach/use points inside bounds and unobstructed; start routes and route between troughs clear.");
            Results.AppendLine("Water position: " + water.transform.position + "; drinking position: " + water.DrinkingPoint.position);
        }

        private static void CheckPath(SheepBehaviour sheep, Vector3 start, Vector3 goal)
        {
            var delta = goal - start; delta.y = 0f;
            Require(!PlayerPhysicsQueries.SphereCast(start + Vector3.up * 0.7f, 0.6f,
                delta.normalized, delta.magnitude, sheep.transform, out _), "Expected route is obstructed.");
        }

        public static void ValidateComponents()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                CheckNeedsAndTreatment(preview);
                CheckWaterAndAI(preview);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            // Reuse the existing actual food-component and meal checks, rather than duplicating them.
            foreach (var name in new[] { "ValidateFoodLimits", "ValidateMealLogic" })
                typeof(Phase5Setup).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Results.AppendLine("PASS: Phase 5 food capacity/visual/event and meal/interruption regression checks.");
            Results.AppendLine("Editor method/physics checks only. These do not constitute Play Mode gameplay testing.");
        }

        private static void CheckNeedsAndTreatment(Scene preview)
        {
            var root = new GameObject("Needs test");
            SceneManager.MoveGameObjectToScene(root, preview);
            var stats = root.AddComponent<AnimalStats>();
            Invoke(stats, "Awake");
            Set(stats, "hungerPerSecond", 0f); Set(stats, "thirstPerSecond", 0f);
            Set(stats, "hunger", 20f); Set(stats, "thirst", 20f);
            Invoke(stats, "AdvanceNeeds", 120f);
            Require(stats.Health == 100f && stats.Condition == AnimalCondition.Healthy, "Normal needs damaged health.");
            Set(stats, "thirst", 90f);
            Invoke(stats, "AdvanceNeeds", 14f);
            Require(stats.Health == 100f, "Damage ignored the grace period.");
            Invoke(stats, "AdvanceNeeds", 2f);
            Require(Mathf.Approximately(stats.Health, 99f), "Damage did not start after the grace period.");
            stats.ReduceThirst(30f);
            Invoke(stats, "AdvanceNeeds", 30f);
            Require(stats.Health == 99f, "Hydrated animal kept taking damage.");
            Set(stats, "thirst", 90f);
            Invoke(stats, "AdvanceNeeds", 15f);
            Require(stats.Health == 99f, "Grace timer did not reset after hydration.");
            Set(stats, "hunger", 90f); Set(stats, "health", 100f);
            Set(stats, "severeHungerTime", 0f); Set(stats, "severeThirstTime", 0f);
            Invoke(stats, "AdvanceNeeds", 20f);
            Require(stats.Health == 95f, "Two severe needs incorrectly doubled damage.");
            Invoke(stats, "AdvanceNeeds", 1000f);
            Require(stats.Health == 0f && stats.Condition == AnimalCondition.Sick, "Health lower limit/condition failed.");
            stats.ReduceHunger(1000f); stats.ReduceThirst(1000f); stats.RestoreHealth(1000f);
            Require(stats.Hunger == 0f && stats.Thirst == 0f && stats.Health == 100f, "Need/health clamp failed.");
            Set(stats, "hungerPerSecond", 5f); Set(stats, "thirstPerSecond", 5f);
            Invoke(stats, "AdvanceNeeds", 100f);
            Require(stats.Hunger == 100f && stats.Thirst == 100f, "Needs upper limit failed.");

            var care = root.AddComponent<AnimalCare>(); Invoke(care, "Awake");
            Set(stats, "health", 30f);
            Require(care.TryTreat() && stats.Health == 65f && care.ShowingConfirmation, "Treatment/confirmation failed.");
            Require(!care.TryTreat() && stats.Health == 65f && care.CooldownRemaining > 0f, "Cooldown allowed repeated treatment.");
            Set(care, "nextTreatmentTime", Time.time - 1f);
            Require(care.TryTreat() && stats.Health == 100f, "Cooldown expiry/recovery failed.");
            Set(care, "nextTreatmentTime", Time.time - 1f);
            Require(!care.TryTreat(), "Unnecessary treatment was allowed.");
            Set(stats, "health", 0f);
            Require(care.TryTreat() && stats.Health == 35f, "Zero-health animal cannot recover.");
            Results.AppendLine("PASS: needs/health clamps, grace delay/reset, no damage for normal needs, combined damage rate, conditions.");
            Results.AppendLine("PASS: treatment amount, confirmation, cooldown rejection/expiry, unnecessary-treatment rejection, recovery from zero.");
        }

        private static void CheckWaterAndAI(Scene preview)
        {
            var penObject = new GameObject("AI test pen");
            SceneManager.MoveGameObjectToScene(penObject, preview);
            penObject.transform.position = new Vector3(1000f, 0f, 1000f);
            var pen = penObject.AddComponent<AnimalEnclosure>();
            var waterRoot = Spawn("WaterTrough.prefab", preview, penObject.transform.position + new Vector3(0, 0, -2.3f));
            var water = waterRoot.GetComponent<WaterTrough>(); Invoke(water, "Awake");
            var level = waterRoot.transform.Find("Water Level");
            int events = 0; water.WaterChanged += () => events++;
            Require(!water.TryConsumePortion() && water.CurrentWater == 0 && !level.gameObject.activeSelf,
                "Empty trough consumed water or showed water.");
            water.Interact();
            float halfHeight = level.localScale.y;
            Require(water.CurrentWater == 5 && level.gameObject.activeSelf, "Partial refill failed.");
            for (int i = 0; i < 20; i++) water.Interact();
            Require(water.CurrentWater == 10 && events == 2 && level.localScale.y > halfHeight, "Full refill limit/visual/events failed.");
            for (int i = 0; i < 10; i++) Require(water.TryConsumePortion(), "Valid water portion rejected.");
            Require(!water.TryConsumePortion() && water.CurrentWater == 0 && !level.gameObject.activeSelf, "Water lower limit failed.");
            Set(water, "unitsPerDrink", 3); water.Interact();
            Require(water.TryConsumePortion() && water.CurrentWater == 2 && !water.TryConsumePortion(), "Partial water portion incorrectly accepted.");
            Set(water, "unitsPerDrink", 1); Set(water, "currentWater", 0);

            var food = Spawn("FeedingTrough.prefab", preview, penObject.transform.position + new Vector3(0, 0, 2.3f)).GetComponent<FeedingTrough>();
            var animal = Spawn("Sheep.prefab", preview, water.DrinkingPoint.position);
            var brain = animal.GetComponent<SheepBehaviour>();
            var stats = animal.GetComponent<AnimalStats>();
            var care = animal.GetComponent<AnimalCare>();
            Phase6Setup.SetReference(brain, "enclosure", pen);
            Phase6Setup.SetReference(brain, "feedingTrough", food);
            Phase6Setup.SetReference(brain, "waterTrough", water);
            Invoke(stats, "Awake"); Invoke(care, "Awake"); Invoke(brain, "Awake");
            var animator = animal.GetComponentInChildren<Animator>(); animator.Rebind(); animator.Update(0.01f);
            Set(stats, "hunger", 20f); Set(stats, "thirst", 70f);
            for (int i = 0; i < 30; i++) Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.Thirsty && stats.Thirst == 70f, "Empty water oscillated or hydrated for free.");
            water.Interact(); Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.SeekingWater, "Did not seek stocked water.");
            Invoke(brain, "SeekWater");
            Require(stats.Behaviour == AnimalBehaviour.Drinking, "Arrival did not start Drinking.");
            for (int i = 0; i < 3; i++) { Set(brain, "stateTime", 0f); Invoke(brain, "DrinkPortion"); }
            Require(stats.Thirst == 10f && water.CurrentWater == 2 && stats.Behaviour == AnimalBehaviour.Idle,
                "Drinking failed to pay portions and finish hydrated.");

            Set(stats, "thirst", 70f); Set(water, "currentWater", 1); Set(brain, "nextWaterAttempt", -1f);
            Invoke(brain, "UpdatePriorities"); Invoke(brain, "SeekWater");
            Set(brain, "stateTime", 0f); Invoke(brain, "DrinkPortion");
            Require(stats.Thirst == 50f && water.CurrentWater == 0 && stats.Behaviour == AnimalBehaviour.Thirsty,
                "Empty water did not interrupt drinking.");
            for (int i = 0; i < 10; i++) Invoke(brain, "UpdatePriorities");
            Require(stats.Thirst == 50f, "Empty waiting reduced thirst.");
            water.Interact(); Set(brain, "nextWaterAttempt", -1f);
            Invoke(brain, "UpdatePriorities"); Invoke(brain, "SeekWater");
            for (int i = 0; i < 2; i++) { Set(brain, "stateTime", 0f); Invoke(brain, "DrinkPortion"); }
            Require(stats.Thirst == 10f && water.CurrentWater == 3, "Unfinished drink did not resume below thirsty threshold.");

            food.Interact(); Set(stats, "hunger", 70f); Set(stats, "thirst", 70f);
            Set(brain, "nextWaterAttempt", -1f); Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.SeekingWater, "Waiting tie did not prefer water.");
            stats.SetBehaviour(AnimalBehaviour.Eating);
            Set(brain, "destination", food.FeedingPoint.position);
            Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.Eating, "Normal thirst interrupted a meal.");
            Set(stats, "thirst", 90f);
            Vector3 validPoint = water.DrinkingPoint.localPosition;
            water.DrinkingPoint.localPosition = new Vector3(100, 0, 100);
            Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.Eating && (Vector3)Get(brain, "destination") == food.FeedingPoint.position,
                "Failed water route destroyed the ongoing food route.");
            water.DrinkingPoint.localPosition = validPoint;
            Set(brain, "nextWaterAttempt", -1f); Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.SeekingWater, "Severe thirst did not interrupt food for reachable water.");

            stats.SetBehaviour(AnimalBehaviour.Idle); Set(stats, "health", 0f);
            Set(stats, "thirst", 70f); Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.SeekingWater, "Low health blocked hydration.");
            Set(stats, "hunger", 20f); Set(stats, "thirst", 20f); stats.SetBehaviour(AnimalBehaviour.Idle);
            Invoke(brain, "UpdatePriorities");
            Require(stats.Behaviour == AnimalBehaviour.NeedsCare, "Low-health animal did not rest in NeedsCare.");
            var interaction = animal.GetComponent<SheepInteractable>(); Invoke(interaction, "Awake");
            Require(interaction.Prompt == "Press E to Treat Sheep", "Treatment prompt missing.");
            interaction.Interact(); interaction.Interact();
            Require(stats.Health == 35f, "Shared E treatment ignored cooldown.");
            for (int i = 0; i < 2; i++) { Set(care, "nextTreatmentTime", Time.time - 1f); interaction.Interact(); }
            Invoke(brain, "UpdatePriorities");
            Require(stats.Health == 100f && stats.Behaviour == AnimalBehaviour.Idle, "Care recovery failed to resume normal AI.");
            Results.AppendLine("PASS: water empty/partial/full visuals, capacity/events, complete portions and no negative water.");
            Results.AppendLine("PASS: drinking, empty interruption/resumption, need hysteresis, water tie priority, stable meals, severe-thirst preemption.");
            Results.AppendLine("PASS: failed water route preserves food destination; zero-health hydration, treatment routing and NeedsCare recovery.");
        }

        private static GameObject Spawn(string file, Scene scene, Vector3 position)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + file), scene);
            obj.transform.position = position; return obj;
        }
        private static UnityEngine.Object Reference(UnityEngine.Object obj, string field) =>
            new SerializedObject(obj).FindProperty(field).objectReferenceValue;
        private static object Invoke(object obj, string name, params object[] args) => obj.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
        private static void Set(object obj, string name, object value) => obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static object Get(object obj, string name) => obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
