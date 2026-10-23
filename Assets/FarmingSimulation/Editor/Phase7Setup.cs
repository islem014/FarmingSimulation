using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    public static class Phase7Setup
    {
        internal const string Root = "Assets/FarmingSimulation/Animals/";
        internal sealed class Species
        {
            public string kind, name;
            public Vector3 penPosition;
            public float height, radius, speed, hungerRate, thirstRate, cooldown, portionReduction;
            public int portions, seed;
            public bool westEntrance, walks;
        }
        internal static readonly Species[] SpeciesList = {
            new Species { kind="Cow", name="Marguerite", penPosition=new Vector3(-10,0,-3), height=1.5f, radius=.8f,
                speed=.65f, hungerRate=.65f, thirstRate=.7f, cooldown=18, portionReduction=24, portions=2, seed=201, walks=true },
            new Species { kind="Pig", name="Peppa", penPosition=new Vector3(12,0,-25), height=.85f, radius=.6f,
                speed=.6f, hungerRate=1f, thirstRate=.5f, cooldown=12, portionReduction=20, portions=1, seed=302, westEntrance=true },
            new Species { kind="Horse", name="Spirit", penPosition=new Vector3(12,0,1), height=1.8f, radius=.85f,
                speed=1.1f, hungerRate=.7f, thirstRate=.9f, cooldown=20, portionReduction=25, portions=2, seed=403, westEntrance=true, walks=true }
        };
        private static readonly StringBuilder Report = new StringBuilder();
        [InitializeOnLoadMethod]
        private static void Watch() { EditorApplication.update -= Check; EditorApplication.update += Check; }
        private static void Check()
        {
            const string request = ".phase7-work/setup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty)
            { File.WriteAllText(".phase7-work/setup-status.txt", "WAITING: save the current scene first."); return; }
            File.Delete(request);
            try { Build(); File.WriteAllText(".phase7-work/setup-status.txt", "SUCCESS"); }
            catch (Exception ex) { File.WriteAllText(".phase7-work/setup-status.txt", "FAILED: " + ex); Debug.LogException(ex); }
            EditorApplication.update -= Check;
        }

        [MenuItem("FarmingSimulation/Phase 7/Create cow pig and horse")]
        public static void Build()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save the scene first.");
            Report.Clear();
            var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(g => g.name == "Environment");
            var animals = scene.GetRootGameObjects().Single(g => g.name == "Gameplay").transform.Find("Animals");
            // Run existing sheep checks before adding extra enclosures (they deliberately expect one).
            if (environment.GetComponentsInChildren<AnimalEnclosure>().Length == 1) Phase6Validation.ValidateScene(scene);
            Phase6Validation.ValidateComponents();
            Report.AppendLine("Daisy Phase 6 and Phase 5 editor regression checks passed after shared-component extraction.");
            foreach (var config in SpeciesList)
            {
                string actorPath = Root + "Prefabs/" + config.kind + ".prefab";
                string penPath = Root + "Prefabs/" + config.kind + "Enclosure.prefab";
                var actor = File.Exists(actorPath)
                    ? UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(actorPath)) : MakeAnimal(config);
                Bounds body = VisibleBounds(actor);
                if (!File.Exists(actorPath)) PrefabUtility.SaveAsPrefabAsset(actor, actorPath);
                UnityEngine.Object.DestroyImmediate(actor);
                if (!File.Exists(penPath))
                {
                    var pen = MakePen(config, body);
                    PrefabUtility.SaveAsPrefabAsset(pen, penPath);
                    UnityEngine.Object.DestroyImmediate(pen);
                }
                if (animals.GetComponentsInChildren<AnimalStats>().Any(a => a.Species == config.kind)) continue;
                var penInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(penPath), scene);
                penInstance.transform.SetParent(environment.transform, false);
                penInstance.transform.position = config.penPosition;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(actorPath), scene);
                instance.transform.SetParent(animals, false);
                instance.transform.position = config.penPosition + Vector3.up * .05f;
                var brain = instance.GetComponent<FarmAnimalBehaviour>();
                Reference(brain, "enclosure", penInstance.GetComponent<AnimalEnclosure>());
                Reference(brain, "feedingTrough", penInstance.GetComponentInChildren<FeedingTrough>());
                Reference(brain, "waterTrough", penInstance.GetComponentInChildren<WaterTrough>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
            }
            Phase7Validation.Validate(scene, Report);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Root + "Phase7AssetReport.txt", Report.ToString());
            File.WriteAllText(".phase7-work/editor-validation.txt", Report.ToString());
            AssetDatabase.Refresh();
            Debug.Log("PHASE7_SETUP_SUCCESS: four independent animals, enclosures and troughs saved; editor checks passed.");
        }

        private static GameObject MakeAnimal(Species config)
        {
            string modelPath = Root + "Models/" + config.kind + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer.animationType != ModelImporterAnimationType.Generic) throw new Exception("Unexpected rig: " + config.kind);
            var assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            var idle = Clip(assets, config.kind, "Idle", true);
            var activity = Clip(assets, config.kind, config.walks ? "WalkSlow" : "Jump", config.walks);
            var controller = Controller(config, idle, activity);
            var root = new GameObject(config.kind + " - " + config.name);
            var cc = root.AddComponent<CharacterController>();
            cc.radius = config.radius; cc.height = Mathf.Max(config.height, config.radius * 2f + .1f);
            cc.center = Vector3.up * (cc.height / 2); cc.skinWidth = .03f; cc.stepOffset = .15f; cc.slopeLimit = 35f;
            var stats = root.AddComponent<AnimalStats>();
            String(stats, "animalName", config.name); String(stats, "species", config.kind);
            Number(stats, "hungerPerSecond", config.hungerRate); Number(stats, "thirstPerSecond", config.thirstRate);
            var care = root.AddComponent<AnimalCare>(); Number(care, "treatmentCooldown", config.cooldown);
            var brain = root.AddComponent<FarmAnimalBehaviour>();
            Number(brain, "moveSpeed", config.speed); Number(brain, "hungerPerPortion", config.portionReduction);
            Number(brain, "thirstPerDrink", config.portionReduction);
            Integer(brain, "foodPortionsPerBite", config.portions); Integer(brain, "waterPortionsPerSip", config.portions);
            Integer(brain, "randomSeed", config.seed); Boolean(brain, "useMovementAnimation", config.walks);
            Boolean(brain, "playFeedingJump", !config.walks);
            root.AddComponent<AnimalInteractable>();
            var placement = new GameObject("Visual Placement").transform;
            placement.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(placement, false);
            idle.SampleAnimation(model, 0f);
            var bounds = VisibleBounds(model);
            float scale = config.height / bounds.size.y;
            placement.localScale = Vector3.one * scale;
            bounds = VisibleBounds(model);
            placement.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var head = model.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
            var hips = model.GetComponentsInChildren<Transform>().Single(t => t.name == "Hips");
            if (head.position.z <= hips.position.z) throw new Exception("Unexpected forward direction: " + config.kind);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => Material(config.kind, m)).ToArray();
            var animator = model.GetComponent<Animator>();
            // Unity's missing-component placeholder uses its overloaded null comparison.
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Reference(brain, "animator", animator);
            foreach (var clip in new[] { idle, activity })
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.path.Length > 0 && model.transform.Find(binding.path) == null)
                        throw new Exception("Missing animation binding " + config.kind + ": " + binding.path);
            ValidateAnimator(animator, config.walks);
            idle.SampleAnimation(model, 0f);
            Report.AppendLine($"{config.kind}: Generic, +Z forward, baked Idle mesh height {config.height:F2}m; normalization {scale:F6}; collider radius {cc.radius:F2}, height {cc.height:F2}; root motion off.");
            return root;
        }

        private static AnimationClip Clip(UnityEngine.Object[] assets, string kind, string take, bool loop)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/" + kind + "_" + take + ".anim");
            if (existing != null) return existing;
            var source = assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Armature|" + take)
                ?? assets.OfType<AnimationClip>().First(c => c.name == "__preview__Armature|" + take);
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = kind + "_" + take; copy.hideFlags = HideFlags.None; copy.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(copy); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.CreateAsset(copy, Root + "Animations/" + copy.name + ".anim");
            Report.AppendLine(kind + " uses actual " + source.name + ": " + copy.length.ToString("F3") + "s, loop=" + loop);
            return copy;
        }
        private static AnimatorController Controller(Species config, AnimationClip idle, AnimationClip activity)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/" + config.kind + ".controller");
            if (existing != null) return existing;
            var c = AnimatorController.CreateAnimatorControllerAtPath(Root + "Animations/" + config.kind + ".controller");
            var machine = c.layers[0].stateMachine;
            var rest = machine.AddState("Idle"); rest.motion = idle; machine.defaultState = rest;
            var active = machine.AddState(config.walks ? "Walk" : "Jump"); active.motion = activity;
            var enter = rest.AddTransition(active); enter.hasExitTime = false; enter.duration = .15f;
            var leave = active.AddTransition(rest); leave.duration = .15f;
            if (config.walks)
            {
                c.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                enter.AddCondition(AnimatorConditionMode.If, 0, "Moving");
                leave.hasExitTime = false; leave.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            }
            else
            {
                c.AddParameter("Fed", AnimatorControllerParameterType.Trigger);
                enter.AddCondition(AnimatorConditionMode.If, 0, "Fed"); leave.hasExitTime = true; leave.exitTime = 1;
            }
            return c;
        }
        private static Material Material(string kind, Material source)
        {
            string path = Root + "Materials/" + kind + "_" + source.name.Replace('.', '_') + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = kind + "_" + source.name };
            result.SetColor("_BaseColor", source.color); result.SetFloat("_Smoothness", .2f);
            if (source.mainTexture != null) result.SetTexture("_BaseMap", source.mainTexture);
            AssetDatabase.CreateAsset(result, path);
            Report.AppendLine(kind + " material " + source.name + " -> owned URP/Lit; texture=" + (source.mainTexture ? source.mainTexture.name : "none"));
            return result;
        }
        internal static Bounds VisibleBounds(GameObject root)
        {
            bool initialized = false; Bounds bounds = default;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh; bool baked = r is SkinnedMeshRenderer;
                // Compensate renderer scale before TransformPoint applies the hierarchy scale.
                if (r is SkinnedMeshRenderer skinned) { mesh = new Mesh(); skinned.BakeMesh(mesh, true); }
                else { var filter = r.GetComponent<MeshFilter>(); if (filter == null) continue; mesh = filter.sharedMesh; }
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 p = r.transform.TransformPoint(vertex);
                    if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(p);
                }
                if (baked) UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (!initialized) throw new Exception("Model has no visible mesh.");
            return bounds;
        }
        private static void ValidateAnimator(Animator animator, bool walks)
        {
            Vector3 position = animator.transform.localPosition, scale = animator.transform.localScale;
            animator.Rebind(); animator.Update(.01f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Animator did not start Idle.");
            if (walks) animator.SetBool("Moving", true); else animator.SetTrigger("Fed");
            animator.Update(.01f); animator.Update(.3f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(walks ? "Walk" : "Jump")) throw new Exception("Animation transition failed.");
            animator.Update(1f);
            if (walks) animator.SetBool("Moving", false);
            animator.Update(2f); animator.Update(.3f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Animation failed to return Idle.");
            if ((position - animator.transform.localPosition).sqrMagnitude > .0001f || (scale - animator.transform.localScale).sqrMagnitude > .0001f)
                throw new Exception("Animation changed visual root placement/scale.");
        }

        private static GameObject MakePen(Species config, Bounds body)
        {
            var pen = PrefabUtility.LoadPrefabContents(Root + "Prefabs/AnimalEnclosure.prefab");
            var copy = UnityEngine.Object.Instantiate(pen);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, EditorSceneManager.GetActiveScene());
            PrefabUtility.UnloadPrefabContents(pen);
            copy.name = config.kind + " Enclosure";
            if (config.westEntrance)
                foreach (Transform child in copy.transform)
                { var p = child.localPosition; p.x = -p.x; child.localPosition = p; }
            float inset = Mathf.Max(1.1f, new Vector2(body.extents.x, body.extents.z).magnitude + .2f);
            Number(copy.GetComponent<AnimalEnclosure>(), "animalInset", inset);
            float standOff = Mathf.Max(1.35f, body.extents.z + .65f, config.radius + .7f);
            var food = copy.GetComponentInChildren<FeedingTrough>();
            var water = copy.GetComponentInChildren<WaterTrough>();
            food.FeedingPoint.localPosition = new Vector3(0, 0, -standOff);
            water.DrinkingPoint.localPosition = new Vector3(0, 0, standOff);
            float side = config.westEntrance ? -2.1f : 2.1f;
            food.ApproachPoint.localPosition = new Vector3(side, 0, -standOff);
            water.ApproachPoint.localPosition = new Vector3(side, 0, standOff);
            // Preserve nested-instance edits when saving this new owned enclosure prefab.
            foreach (var t in new[] { food.transform, water.transform, food.FeedingPoint, food.ApproachPoint, water.DrinkingPoint, water.ApproachPoint })
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Report.AppendLine($"{config.kind} pen centre {config.penPosition}, 8x8m, inset {inset:F2}, trough stand-off {standOff:F2}, entrance " + (config.westEntrance ? "west" : "east"));
            return copy;
        }
        internal static void Reference(UnityEngine.Object obj, string field, UnityEngine.Object value)
        { var d = new SerializedObject(obj); d.FindProperty(field).objectReferenceValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
        internal static void Number(UnityEngine.Object obj, string field, float value)
        { var d = new SerializedObject(obj); d.FindProperty(field).floatValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Integer(UnityEngine.Object obj, string field, int value)
        { var d = new SerializedObject(obj); d.FindProperty(field).intValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Boolean(UnityEngine.Object obj, string field, bool value)
        { var d = new SerializedObject(obj); d.FindProperty(field).boolValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
        private static void String(UnityEngine.Object obj, string field, string value)
        { var d = new SerializedObject(obj); d.FindProperty(field).stringValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
