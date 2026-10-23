using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    // Opt-in editor migration: never runs in Play Mode or over an unsaved scene.
    public static class Phase5Setup
    {
        private const string Root = "Assets/FarmingSimulation/Animals/";
        private const string TroughPath = Root + "Prefabs/FeedingTrough.prefab";
        private const string EnclosurePath = Root + "Prefabs/AnimalEnclosure.prefab";
        private const string SheepPath = Root + "Prefabs/Sheep.prefab";
        private static double nextCheck;

        [InitializeOnLoadMethod]
        private static void Watch()
        {
            EditorApplication.update -= CheckRequest;
            EditorApplication.update += CheckRequest;
        }
        private static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            const string request = ".phase5-work/setup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                File.WriteAllText(".phase5-work/setup-status.txt", "WAITING: Save the current scene first.");
                return;
            }
            File.Delete(request);
            try
            {
                Build();
                File.WriteAllText(".phase5-work/setup-status.txt", "SUCCESS");
            }
            catch (Exception ex)
            {
                File.WriteAllText(".phase5-work/setup-status.txt", "FAILED: " + ex);
                Debug.LogException(ex);
            }
            EditorApplication.update -= CheckRequest;
        }

        [MenuItem("FarmingSimulation/Phase 5/Create feeding trough setup")]
        public static void Build()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save the current scene first.");
            if (File.Exists(TroughPath)) throw new InvalidOperationException("Phase 5 trough already exists; refusing duplicate migration.");
            var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
            var enclosureInstance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AnimalEnclosure>()).Single();
            var sheepInstance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SheepBehaviour>()).Single();

            var enclosureRoot = PrefabUtility.LoadPrefabContents(EnclosurePath);
            try
            {
                var old = enclosureRoot.transform.Find("Feeding Trough");
                if (old == null) throw new InvalidOperationException("Expected Phase 4 trough wrapper is missing.");
                Vector3 position = old.localPosition;
                Quaternion rotation = old.localRotation;
                var troughRoot = UnityEngine.Object.Instantiate(old.gameObject);
                troughRoot.name = "Feeding Trough";
                troughRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                try
                {
                    var trough = troughRoot.AddComponent<FeedingTrough>();
                    var feeding = Child(troughRoot.transform, "Feeding Position", new Vector3(0, 0, -1.35f));
                    var approach = Child(troughRoot.transform, "Approach Position", new Vector3(2.1f, 0, -1.35f));
                    var hay = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hay.name = "Hay Level";
                    UnityEngine.Object.DestroyImmediate(hay.GetComponent<Collider>());
                    hay.transform.SetParent(troughRoot.transform, false);
                    hay.transform.localPosition = new Vector3(0, 0.39f, 0);
                    hay.transform.localScale = new Vector3(1.9f, 0.28f, 0.48f);
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.name = "Trough_Hay";
                    material.SetColor("_BaseColor", new Color(0.72f, 0.52f, 0.12f));
                    material.SetFloat("_Smoothness", 0.05f);
                    AssetDatabase.CreateAsset(material, Root + "Materials/Trough_Hay.mat");
                    hay.GetComponent<Renderer>().sharedMaterial = material;
                    hay.SetActive(false); // Empty at the start; runtime scales it with food quantity.
                    SetReference(trough, "feedingPoint", feeding);
                    SetReference(trough, "approachPoint", approach);
                    SetReference(trough, "hayVisual", hay.transform);
                    PrefabUtility.SaveAsPrefabAsset(troughRoot, TroughPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(troughRoot); }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TroughPath), enclosureRoot.scene);
                replacement.transform.SetParent(enclosureRoot.transform, false);
                replacement.transform.localPosition = position;
                replacement.transform.localRotation = rotation;
                PrefabUtility.SaveAsPrefabAsset(enclosureRoot, EnclosurePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(enclosureRoot); }

            // Re-save only our sheep prefab to serialize new settings and discard retired direct-feed fields.
            var sheepRoot = PrefabUtility.LoadPrefabContents(SheepPath);
            try
            {
                var data = new SerializedObject(sheepRoot.GetComponent<SheepBehaviour>());
                data.FindProperty("satisfiedThreshold").floatValue = 20f;
                data.FindProperty("portionInterval").floatValue = 2f;
                data.FindProperty("hungerPerPortion").floatValue = 20f;
                data.FindProperty("foodRetryInterval").floatValue = 3f;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(sheepRoot, SheepPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(sheepRoot); }

            var sceneTrough = enclosureInstance.GetComponentInChildren<FeedingTrough>();
            if (sceneTrough == null) throw new InvalidOperationException("Enclosure instance did not receive the trough prefab update.");
            SetReference(sheepInstance, "feedingTrough", sceneTrough);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sheepInstance);
            ValidatePlacement(sheepInstance, enclosureInstance, sceneTrough);
            ValidateFoodLimits();
            ValidateMealLogic();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(".phase5-work/editor-validation.txt",
                "PASS: one existing sheep and enclosure; trough assigned.\n"
                + "PASS: feeding/approach positions within bounds; feeding position unobstructed; start route clear.\n"
                + "PASS: empty consumption rejected, refill clamped, ten portions consumed, no negative food, hay states/events.\n"
                + "PASS: actual AI methods on preview instances: empty wait, seek/eat, portion payment, satisfaction, interrupted meal, inspection only.\n"
                + "Editor checks only; no Play Mode gameplay test.\n"
                + "Trough: " + sceneTrough.transform.position + "; feeding point: " + sceneTrough.FeedingPoint.position);
            Debug.Log("PHASE5_SETUP_SUCCESS: assets, scene wiring and editor validation completed.");
        }

        private static void ValidatePlacement(SheepBehaviour sheep, AnimalEnclosure enclosure, FeedingTrough trough)
        {
            Physics.SyncTransforms();
            Vector3 goal = trough.FeedingPoint.position;
            foreach (var point in new[] { goal, trough.ApproachPoint.position })
                Require((enclosure.ClampPosition(point) - point).sqrMagnitude < 0.001f, "Feeding route lies outside enclosure.");
            // Capsule matching the sheep, lifted slightly so the ground is not a false obstruction.
            var overlaps = Physics.OverlapCapsule(goal + Vector3.up * 0.6f, goal + Vector3.up * 0.65f, 0.55f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Require(!overlaps.Any(c => !c.transform.IsChildOf(sheep.transform)), "Feeding position is obstructed.");
            Vector3 start = sheep.transform.position;
            Vector3 offset = goal - start; offset.y = 0;
            Require(!PlayerPhysicsQueries.SphereCast(start + Vector3.up * 0.7f, 0.6f,
                offset.normalized, offset.magnitude, sheep.transform, out _), "Starting sheep route is obstructed.");
        }

        private static void ValidateFoodLimits()
        {
            // Exercise actual component methods in an isolated preview scene; never change game food.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TroughPath), preview);
                var trough = root.GetComponent<FeedingTrough>();
                var hay = root.transform.Find("Hay Level");
                int changes = 0;
                trough.FoodChanged += () => changes++;
                Require(!trough.TryConsumePortion() && trough.CurrentFood == 0, "Empty trough allowed consumption.");
                trough.Interact();
                Require(trough.CurrentFood == 5 && hay.gameObject.activeSelf, "Partial refill failed.");
                float partialHeight = hay.localScale.y;
                for (int i = 0; i < 20; i++) trough.Interact();
                Require(trough.CurrentFood == 10 && changes == 2 && hay.localScale.y > partialHeight, "Capacity clamp or full visual failed.");
                for (int i = 0; i < 10; i++) Require(trough.TryConsumePortion(), "Valid portion rejected.");
                Require(trough.CurrentFood == 0 && !trough.TryConsumePortion() && !hay.gameObject.activeSelf
                    && changes == 12, "Empty limit, event count or hay visibility failed.");
                var data = new SerializedObject(trough);
                data.FindProperty("unitsPerPortion").intValue = 3;
                data.ApplyModifiedPropertiesWithoutUndo();
                trough.Interact();
                Require(trough.TryConsumePortion() && trough.CurrentFood == 2 && !trough.TryConsumePortion(),
                    "Incomplete portions must not be consumed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void ValidateMealLogic()
        {
            // Controlled method-level checks, not a simulated Play Mode or navigation test.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var pen = new GameObject("Test enclosure");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pen, preview);
                pen.transform.position = new Vector3(1000, 0, 1000);
                var bounds = pen.AddComponent<AnimalEnclosure>();
                var troughObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TroughPath), preview);
                troughObject.transform.position = pen.transform.position + new Vector3(0, 0, 2);
                var trough = troughObject.GetComponent<FeedingTrough>();
                var animal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SheepPath), preview);
                animal.transform.position = trough.FeedingPoint.position;
                var brain = animal.GetComponent<SheepBehaviour>();
                var stats = animal.GetComponent<AnimalStats>();
                SetReference(brain, "enclosure", bounds);
                SetReference(brain, "feedingTrough", trough);
                Invoke(brain, "Awake");
                var animator = animal.GetComponentInChildren<Animator>();
                animator.Rebind(); animator.Update(0.01f);
                SetNumber(stats, "hunger", 70f);
                SetPrivate(brain, "wantsFood", true);
                for (int i = 0; i < 10; i++) Invoke(brain, "PlanFoodRoute");
                Require(stats.Behaviour == AnimalBehaviour.Hungry && stats.Hunger == 70f,
                    "Empty trough changed hunger or allowed another state.");
                trough.Interact(); // Five portions.
                Invoke(brain, "PlanFoodRoute");
                Require(stats.Behaviour == AnimalBehaviour.SeekingFood, "Food did not allow seeking.");
                Invoke(brain, "SeekFood");
                Require(stats.Behaviour == AnimalBehaviour.Eating, "Arrival did not begin eating.");
                for (int i = 0; i < 3; i++)
                {
                    SetPrivate(brain, "stateTime", 0f);
                    Invoke(brain, "EatPortion");
                }
                Require(stats.Hunger == 10f && trough.CurrentFood == 2 && stats.Behaviour == AnimalBehaviour.Idle,
                    "Meal did not pay exactly three portions and stop satisfied.");
                // A meal with just one portion must stop hungry, without a free second portion.
                SetNumber(stats, "hunger", 70f);
                var food = new SerializedObject(trough);
                food.FindProperty("currentFood").intValue = 1; food.ApplyModifiedPropertiesWithoutUndo();
                SetPrivate(brain, "wantsFood", true);
                Invoke(brain, "PlanFoodRoute"); Invoke(brain, "SeekFood");
                SetPrivate(brain, "stateTime", 0f); Invoke(brain, "EatPortion");
                Require(stats.Hunger == 50f && trough.CurrentFood == 0 && stats.Behaviour == AnimalBehaviour.Hungry,
                    "Exhausted meal did not stop Hungry.");
                for (int i = 0; i < 10; i++) Invoke(brain, "PlanFoodRoute");
                Require(stats.Hunger == 50f && stats.Behaviour == AnimalBehaviour.Hungry, "Empty wait oscillated or fed for free.");
                var interaction = animal.GetComponent<SheepInteractable>();
                Invoke(interaction, "Awake"); interaction.Interact();
                Require(stats.Hunger == 50f && trough.CurrentFood == 0, "Inspection fed the sheep directly.");
                trough.Interact();
                Invoke(brain, "PlanFoodRoute"); Invoke(brain, "SeekFood");
                for (int i = 0; i < 2; i++) { SetPrivate(brain, "stateTime", 0f); Invoke(brain, "EatPortion"); }
                Require(stats.Hunger == 10f && stats.Behaviour == AnimalBehaviour.Idle && trough.CurrentFood == 3,
                    "Refilled trough did not resume the interrupted meal below the hungry threshold.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static object Invoke(object target, string method) => target.GetType().GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
        private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        private static void SetNumber(UnityEngine.Object target, string name, float value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position;
            return child;
        }
        private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
