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
    public static class Phase7Validation
    {
        [InitializeOnLoadMethod]
        private static void WatchSavedValidation() => EditorApplication.update += CheckSavedValidation;

        private static void CheckSavedValidation()
        {
            const string request = ".phase7-work/validate.request";
            if (!System.IO.File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty) return;
            System.IO.File.Delete(request);
            EditorApplication.update -= CheckSavedValidation;
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
                var report = new StringBuilder("Validation of the saved scene reloaded from disk:\n");
                Validate(scene, report);
                foreach (var root in scene.GetRootGameObjects()) CheckReferences(root);
                foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FarmingSimulation" }))
                {
                    var prefab = PrefabUtility.LoadPrefabContents(AssetDatabase.GUIDToAssetPath(path));
                    try { CheckReferences(prefab); }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                report.AppendLine("PASS: no missing scripts or broken serialized object references in the reloaded scene or project-owned prefabs.");
                System.IO.File.WriteAllText(".phase7-work/saved-validation.txt", report.ToString());
                Debug.Log("PHASE7_SAVED_VALIDATION_SUCCESS");
            }
            catch (Exception ex) { System.IO.File.WriteAllText(".phase7-work/saved-validation.txt", "FAILED: " + ex); Debug.LogException(ex); }
        }

        private static void CheckReferences(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + t.name);
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Require(property.objectReferenceValue != null || property.objectReferenceEntityIdValue == default,
                                "Broken reference: " + t.name + "/" + property.propertyPath);
                }
            }
        }

        public static void Validate(Scene scene, StringBuilder report)
        {
            var all = scene.GetRootGameObjects();
            var animals = all.SelectMany(g => g.GetComponentsInChildren<FarmAnimalBehaviour>()).ToArray();
            var pens = all.SelectMany(g => g.GetComponentsInChildren<AnimalEnclosure>()).ToArray();
            Require(animals.Length == 4 && pens.Length == 4, "Expected four animals and four enclosures.");
            Require(animals.Select(a => a.GetComponent<AnimalStats>().Species).Distinct().Count() == 4, "Duplicate species.");
            Require(animals.Select(a => Ref(a,"enclosure")).Distinct().Count() == 4, "Animals share an assigned enclosure.");
            Physics.SyncTransforms();
            foreach (var animal in animals)
            {
                var stats = animal.GetComponent<AnimalStats>();
                var pen = (AnimalEnclosure)Ref(animal, "enclosure");
                var food = (FeedingTrough)Ref(animal, "feedingTrough");
                var water = (WaterTrough)Ref(animal, "waterTrough");
                var cc = animal.GetComponent<CharacterController>();
                Require(food != null && water != null && food.GetComponentInParent<AnimalEnclosure>() == pen
                    && water.GetComponentInParent<AnimalEnclosure>() == pen, "Foreign/missing trough reference: " + stats.Species);
                Require(pen.GetComponentsInChildren<FeedingTrough>().Length == 1 && pen.GetComponentsInChildren<WaterTrough>().Length == 1,
                    "Duplicate troughs in " + stats.Species + " pen.");
                Require(animal.GetComponents<CharacterController>().Length == 1 && animal.GetComponent<Rigidbody>() == null,
                    "Conflicting animal physics.");
                Require(animal.GetComponents<FarmInteractable>().Length == 1 && animal.GetComponent<AnimalCare>() != null,
                    "Animal interaction/care components are invalid.");
                var animator = (Animator)Ref(animal, "animator");
                Require(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion
                    && animator.runtimeAnimatorController.animationClips.All(c => c != null), "Missing Animator references.");
                foreach (var point in new[] { food.FeedingPoint, food.ApproachPoint, water.DrinkingPoint, water.ApproachPoint })
                {
                    Require(point != null && (pen.ClampPosition(point.position)-point.position).sqrMagnitude < .001f,
                        "Out-of-bounds route point: " + stats.Species);
                    var hits = Physics.OverlapCapsule(point.position + Vector3.up*(cc.radius+.04f),
                        point.position + Vector3.up*(cc.height-cc.radius+.04f), cc.radius,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Require(!hits.Any(c => !c.transform.IsChildOf(animal.transform)), "Obstructed use/approach point: " + stats.Species + "/" + point.name);
                }
                ClearPath(animal, animal.transform.position, food.FeedingPoint.position);
                ClearPath(animal, animal.transform.position, water.DrinkingPoint.position);
                ClearPath(animal, food.FeedingPoint.position, water.DrinkingPoint.position);
                // Use actual bounds and 100 independently sampled destinations for each pen.
                var random = new System.Random(7);
                for (int i = 0; i < 100; i++)
                {
                    var p = pen.SampleDestination(random, 0f);
                    Require((pen.ClampPosition(p)-p).sqrMagnitude < .001f, "Sampled destination escaped pen.");
                }
                report.AppendLine(stats.AnimalName + " (" + stats.Species + "): own pen/food/water/care/Animator references valid; route points and starting/between-trough paths clear.");
            }

            // Compare each new pen footprint against previous scenery, including noncolliding props.
            foreach (var config in Phase7Setup.SpeciesList)
            {
                var area = new Bounds(config.penPosition + Vector3.up, new Vector3(8.25f, 2f, 8.25f));
                foreach (var renderer in all.SelectMany(g => g.GetComponentsInChildren<Renderer>()))
                {
                    if (renderer.GetComponentInParent<AnimalEnclosure>() || renderer.GetComponentInParent<FarmAnimalBehaviour>()) continue;
                    if (renderer.name == "Ground" || renderer.name == "Courtyard" || renderer.bounds.max.y < .1f) continue;
                    Require(!area.Intersects(renderer.bounds), config.kind + " enclosure overlaps scenery: " + renderer.name);
                }
                float entryX = config.penPosition.x + (config.westEntrance ? -4f : 4f);
                var entrance = new Vector3(entryX, 1f, config.penPosition.z);
                Require(Physics.OverlapCapsule(entrance-Vector3.up*.6f, entrance+Vector3.up*.6f,.3f).Length == 0,
                    "Player entrance obstructed for " + config.kind);
            }
            report.AppendLine("PASS: new pen footprints clear of existing rendered scenery/plots; all three player entrances clear; 400 sampled destinations remain bounded.");
            ValidateIndependence(report);
            report.AppendLine("Editor method/physics/Animator checks only; no Play Mode gameplay testing.");
        }

        private static void ValidateIndependence(StringBuilder report)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var kinds = new[] { "Sheep", "Cow", "Pig", "Horse" };
                var actors = new FarmAnimalBehaviour[4]; var foods = new FeedingTrough[4]; var waters = new WaterTrough[4];
                for (int i = 0; i < kinds.Length; i++)
                {
                    var pen = Spawn(i == 0 ? "AnimalEnclosure" : kinds[i]+"Enclosure", preview);
                    pen.transform.position = new Vector3(1000 + i*20, 0, 1000);
                    var actor = Spawn(kinds[i], preview);
                    var brain = actor.GetComponent<FarmAnimalBehaviour>(); actors[i] = brain;
                    foods[i] = pen.GetComponentInChildren<FeedingTrough>(); waters[i] = pen.GetComponentInChildren<WaterTrough>();
                    actor.transform.position = pen.transform.position;
                    Phase7Setup.Reference(brain,"enclosure",pen.GetComponent<AnimalEnclosure>());
                    Phase7Setup.Reference(brain,"feedingTrough",foods[i]); Phase7Setup.Reference(brain,"waterTrough",waters[i]);
                    Invoke(actor.GetComponent<AnimalStats>(),"Awake"); Invoke(actor.GetComponent<AnimalCare>(),"Awake");
                    Invoke(brain,"Awake"); Invoke(actor.GetComponent<AnimalInteractable>(),"Awake");
                    var animator = actor.GetComponentInChildren<Animator>(); animator.Rebind(); animator.Update(.01f);
                    foods[i].Interact(); foods[i].Interact(); waters[i].Interact(); waters[i].Interact();
                }
                for (int i = 0; i < actors.Length; i++)
                {
                    var brain = actors[i]; var stats = brain.GetComponent<AnimalStats>(); var care = brain.GetComponent<AnimalCare>();
                    var before = actors.Select(a => new Vector3(a.Stats.Hunger,a.Stats.Thirst,a.Stats.Health)).ToArray();
                    Set(stats,"hunger",70f); Set(stats,"thirst",20f);
                    brain.transform.position = foods[i].FeedingPoint.position;
                    Invoke(brain,"UpdatePriorities"); Invoke(brain,"SeekFood");
                    Require(stats.Behaviour == AnimalBehaviour.Eating, "Food arrival failed: " + kinds[i]);
                    float reduction = (float)Get(brain,"hungerPerPortion");
                    int bites = Mathf.CeilToInt(50f/reduction), cost = (int)Get(brain,"foodPortionsPerBite");
                    for (int j=0;j<bites;j++) { Set(brain,"stateTime",0f); Invoke(brain,"EatPortion"); }
                    Require(stats.Hunger <= 20f && foods[i].CurrentFood == 10-bites*cost && stats.Behaviour == AnimalBehaviour.Idle,
                        "Food consumption/satisfaction failed: " + kinds[i]);
                    Set(stats,"thirst",70f); brain.transform.position = waters[i].DrinkingPoint.position;
                    Invoke(brain,"UpdatePriorities"); Invoke(brain,"SeekWater");
                    Require(stats.Behaviour == AnimalBehaviour.Drinking, "Water arrival failed: " + kinds[i]);
                    int drinks = Mathf.CeilToInt(50f/(float)Get(brain,"thirstPerDrink"));
                    for (int j=0;j<drinks;j++) { Set(brain,"stateTime",0f); Invoke(brain,"DrinkPortion"); }
                    Require(stats.Thirst <= 20f && waters[i].CurrentWater == 10-drinks*(int)Get(brain,"waterPortionsPerSip"),
                        "Water consumption/satisfaction failed: " + kinds[i]);
                    Set(stats,"health",30f);
                    var interaction = brain.GetComponent<AnimalInteractable>();
                    Require(interaction.Prompt == "Press E to Treat " + kinds[i] && interaction.Details.Contains(stats.Species), "Species HUD prompt failed.");
                    interaction.Interact(); interaction.Interact();
                    Require(stats.Health == 65f && care.CooldownRemaining > 0, "Treatment cooldown failed: " + kinds[i]);
                    Set(care,"nextTreatmentTime",Time.time-1f); interaction.Interact();
                    Require(stats.Health == 100f, "Treatment recovery failed.");
                    for (int j=0;j<actors.Length;j++) if (i!=j)
                        Require(before[j] == new Vector3(actors[j].Stats.Hunger,actors[j].Stats.Thirst,actors[j].Stats.Health), "Animal state leaked to another instance.");
                    // Misassign another pen's resources: both must be rejected before consumption.
                    int other=(i+1)%4; int otherFood=foods[other].CurrentFood, otherWater=waters[other].CurrentWater;
                    Phase7Setup.Reference(brain,"feedingTrough",foods[other]); Phase7Setup.Reference(brain,"waterTrough",waters[other]);
                    Set(stats,"hunger",70f); Set(stats,"thirst",70f);
                    Invoke(brain,"EatPortion"); Invoke(brain,"DrinkPortion");
                    Require(stats.Hunger == 70f && stats.Thirst == 70f && foods[other].CurrentFood == otherFood
                        && waters[other].CurrentWater == otherWater, "Cross-enclosure consumption allowed.");
                    Phase7Setup.Reference(brain,"feedingTrough",foods[i]); Phase7Setup.Reference(brain,"waterTrough",waters[i]);
                }
                var f=foods[0]; var w=waters[0];
                Set(f,"currentFood",3); Set(w,"currentWater",3);
                Require(f.TryConsumePortions(2) && !f.TryConsumePortions(2) && f.CurrentFood==1, "Competing food requests overspent.");
                Require(w.TryConsumePortions(2) && !w.TryConsumePortions(2) && w.CurrentWater==1, "Competing water requests overspent.");
                Require(!f.TryConsumePortions(0) && !f.TryConsumePortions(-1) && !f.TryConsumePortions(int.MaxValue)
                    && !w.TryConsumePortions(0) && !w.TryConsumePortions(-1) && !w.TryConsumePortions(int.MaxValue), "Invalid/overflow request accepted.");
                report.AppendLine("PASS: actual shared AI methods on all four species: independent feeding/drinking portions, satisfaction, species HUD, treatment and cooldown/recovery.");
                report.AppendLine("PASS: wrong-enclosure resource rejection; competing multi-portion requests cannot overspend; invalid/overflow requests rejected.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static void ClearPath(FarmAnimalBehaviour animal, Vector3 start, Vector3 target)
        {
            float radius=animal.GetComponent<CharacterController>().radius+.05f;
            Vector3 delta=target-start; delta.y=0;
            Require(!PlayerPhysicsQueries.SphereCast(start+Vector3.up*(radius+.1f),radius,delta.normalized,delta.magnitude,animal.transform,out _),
                "Animal route blocked: " + animal.name);
        }
        private static GameObject Spawn(string kind, Scene scene) => (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Phase7Setup.Root+"Prefabs/"+kind+".prefab"),scene);
        private static UnityEngine.Object Ref(UnityEngine.Object obj,string name) => new SerializedObject(obj).FindProperty(name).objectReferenceValue;
        private static object Invoke(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
        private static object Get(object obj,string name) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        private static void Require(bool valid,string message) { if(!valid) throw new InvalidOperationException(message); }
    }
}
