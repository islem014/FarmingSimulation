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
    // Explicit setup tool. An opt-in request file also allows the assistant to use a licensed editor.
    // Normal imports do nothing unless that one-time request exists. Never runs in Play Mode.
    public static class Phase4Setup
    {
        private const string Root = "Assets/FarmingSimulation/Animals";
        private const string ModelPath = Root + "/Models/Sheep.fbx";
        private const string SheepPath = Root + "/Prefabs/Sheep.prefab";
        private const string EnclosurePath = Root + "/Prefabs/AnimalEnclosure.prefab";
        private static readonly StringBuilder Report = new StringBuilder();
        private static double nextRequestCheck;

        [InitializeOnLoadMethod]
        private static void WatchRequestedSetup()
        {
            EditorApplication.update -= RunRequestedSetup;
            EditorApplication.update += RunRequestedSetup;
        }

        private static void RunRequestedSetup()
        {
            if (EditorApplication.timeSinceStartup < nextRequestCheck) return;
            nextRequestCheck = EditorApplication.timeSinceStartup + 1;
            const string request = ".phase4-work/setup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                File.WriteAllText(".phase4-work/setup-status.txt", "WAITING: current scene has unsaved changes; save it before setup.");
                return;
            }
            File.Delete(request);
            try
            {
                Build();
                File.WriteAllText(".phase4-work/setup-status.txt", "SUCCESS");
            }
            catch (Exception error)
            {
                File.WriteAllText(".phase4-work/setup-status.txt", "FAILED: " + error);
                Debug.LogException(error);
            }
            EditorApplication.update -= RunRequestedSetup;
        }

        [MenuItem("FarmingSimulation/Phase 4/Create missing sheep setup")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(SheepPath) || File.Exists(EnclosurePath))
                throw new InvalidOperationException("Phase 4 prefabs already exist. Setup stopped to avoid duplicates or overwriting your edits.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your current scene before running setup.");
            Report.Clear();
            foreach (string directory in new[] { "Animations", "Materials", "Prefabs" })
                Directory.CreateDirectory(Root + "/" + directory);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(g => g.name == "Environment");
            var gameplay = scene.GetRootGameObjects().Single(g => g.name == "Gameplay");
            if (environment.transform.Find("AnimalEnclosure") || gameplay.transform.Find("Animals"))
                throw new InvalidOperationException("Animal scene groups already exist. Stopping instead of duplicating them.");

            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            Report.AppendLine("Imported rig: " + importer.animationType + "; original model/importer settings unchanged.");
            var subassets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            foreach (var clip in subassets.OfType<AnimationClip>())
                Report.AppendLine($"Available clip: {clip.name}; {clip.length:F3}s; legacy={clip.legacy}");
            foreach (var material in subassets.OfType<Material>())
                Report.AppendLine($"Imported material: {material.name}; color={material.color}; texture={(material.mainTexture ? material.mainTexture.name : "none")}");
            if (importer.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Unexpected rig type: inspect the model before continuing.");

            // Unity retains preview takes even when the explicit clip list omits Idle.
            // Copy the real imported take, not fabricated keyframes, without reimporting the FBX.
            var idle = CopyClip(subassets, "Armature|Idle", "Sheep_Idle.anim", true);
            var jump = CopyClip(subassets, "Armature|Jump", "Sheep_Jump.anim", false);
            var controller = MakeController(idle, jump);
            var sheep = MakeSheep(controller, idle, jump);
            PrefabUtility.SaveAsPrefabAsset(sheep, SheepPath);
            UnityEngine.Object.DestroyImmediate(sheep);
            var enclosure = MakeEnclosure();
            PrefabUtility.SaveAsPrefabAsset(enclosure, EnclosurePath);
            UnityEngine.Object.DestroyImmediate(enclosure);

            var enclosureInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnclosurePath), scene);
            enclosureInstance.transform.SetParent(environment.transform, false);
            enclosureInstance.transform.localPosition = new Vector3(-11f, 0f, -21f);
            var animals = new GameObject("Animals");
            animals.transform.SetParent(gameplay.transform, false);
            var sheepInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SheepPath), scene);
            sheepInstance.transform.SetParent(animals.transform, false);
            sheepInstance.transform.position = new Vector3(-11.5f, 0.05f, -21.5f);
            SetReference(sheepInstance.GetComponent<SheepBehaviour>(), "enclosure", enclosureInstance.GetComponent<AnimalEnclosure>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(sheepInstance.GetComponent<SheepBehaviour>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Report.AppendLine("Scene: one sheep at (-11.5, 0.05, -21.5); enclosure X [-15,-7], Z [-25,-17].");
            Report.AppendLine("Walking boundary inset 1m; entrance on east side, 2m wide, gate held open inward.");
            Report.AppendLine("No walk/eat takes exist. Idle is the placeholder; successful feeding triggers one Jump.");
            File.WriteAllText(Root + "/Phase4AssetReport.txt", Report.ToString());
            AssetDatabase.Refresh();
            Debug.Log("PHASE4_SETUP_SUCCESS\n" + Report);
        }

        private static AnimationClip CopyClip(UnityEngine.Object[] assets, string take, string filename, bool loop)
        {
            var clips = assets.OfType<AnimationClip>();
            var source = clips.FirstOrDefault(c => c.name == take)
                ?? clips.FirstOrDefault(c => c.name == "__preview__" + take);
            if (source == null) throw new InvalidOperationException("Missing source take: " + take);
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = take;
            copy.hideFlags = HideFlags.None;
            copy.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.CreateAsset(copy, Root + "/Animations/" + filename);
            var bindings = AnimationUtility.GetCurveBindings(copy);
            int changing = bindings.Count(b => {
                var curve = AnimationUtility.GetEditorCurve(copy, b);
                return curve.keys.Any(k => Mathf.Abs(k.value - curve.keys[0].value) > 0.0001f);
            });
            Report.AppendLine($"Exported {take}: {copy.length:F3}s; {bindings.Length} curves, {changing} changing; loop={loop}.");
            return copy;
        }

        private static AnimatorController MakeController(AnimationClip idle, AnimationClip jump)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/Animations/Sheep.controller");
            controller.AddParameter("Fed", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle"); idleState.motion = idle;
            var jumpState = machine.AddState("Jump"); jumpState.motion = jump;
            machine.defaultState = idleState;
            var feed = idleState.AddTransition(jumpState);
            feed.hasExitTime = false; feed.duration = 0.1f;
            feed.AddCondition(AnimatorConditionMode.If, 0f, "Fed");
            var back = jumpState.AddTransition(idleState);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f;
            return controller;
        }

        private static GameObject MakeSheep(AnimatorController controller, AnimationClip idle, AnimationClip jump)
        {
            var sheep = new GameObject("Sheep - Daisy");
            var capsule = sheep.AddComponent<CharacterController>();
            capsule.radius = 0.55f; capsule.height = 1.2f;
            capsule.center = new Vector3(0f, 0.6f, 0f);
            capsule.skinWidth = 0.03f; capsule.stepOffset = 0.15f;
            capsule.slopeLimit = 35f; capsule.minMoveDistance = 0f;
            sheep.AddComponent<AnimalStats>();
            var brain = sheep.AddComponent<SheepBehaviour>();
            sheep.AddComponent<SheepInteractable>();
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            // Keep mesh references to the original FBX but own this editable rig hierarchy.
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var visualPlacement = new GameObject("Sheep Visual");
            visualPlacement.transform.SetParent(sheep.transform, false);
            visual.transform.SetParent(visualPlacement.transform, false);
            var bounds = RendererBounds(visual);
            float scale = 1.1f / bounds.size.y;
            // Normalization belongs outside the animated rig so clips cannot overwrite it.
            visualPlacement.transform.localScale *= scale;
            bounds = RendererBounds(visual);
            visualPlacement.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Report.AppendLine("Uniform model scale multiplier: " + scale + "; aligned bind-pose height=1.1m.");
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            SetReference(brain, "animator", animator);
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(source => {
                    if (!materials.TryGetValue(source.name, out var material))
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
                        material = new Material(shader) { name = "Sheep_" + source.name };
                        material.SetColor("_BaseColor", source.color);
                        material.SetFloat("_Smoothness", 0.2f);
                        AssetDatabase.CreateAsset(material, Root + "/Materials/" + material.name + ".mat");
                        materials.Add(source.name, material);
                    }
                    return material;
                }).ToArray();
            foreach (var clip in new[] { idle, jump })
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (!string.IsNullOrEmpty(binding.path) && visual.transform.Find(binding.path) == null)
                        throw new InvalidOperationException("Missing rig binding: " + binding.path);
            Report.AppendLine("All animation binding paths resolve on the Generic rig. No Avatar or root motion required for these transform curves.");
            var head = visual.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
            var hips = visual.GetComponentsInChildren<Transform>().First(t => t.name == "Hips");
            if (sheep.transform.InverseTransformPoint(head.position).z < sheep.transform.InverseTransformPoint(hips.position).z)
                throw new InvalidOperationException("Model does not face +Z: inspect its orientation.");
            TestAnimator(animator);
            idle.SampleAnimation(visual, 0f);
            return sheep;
        }

        private static void TestAnimator(Animator animator)
        {
            Vector3 position = animator.transform.localPosition;
            Vector3 scale = animator.transform.localScale;
            animator.Rebind(); animator.Update(0.1f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Animator did not start Idle.");
            animator.SetTrigger("Fed"); animator.Update(0.01f); animator.Update(0.2f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Jump")) throw new Exception("Fed did not trigger Jump.");
            animator.Update(1.4f); animator.Update(0.2f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Jump did not return to Idle.");
            if ((position - animator.transform.localPosition).sqrMagnitude > 0.0001f ||
                (scale - animator.transform.localScale).sqrMagnitude > 0.0001f)
                throw new Exception("Animation changed the visual root placement/scale.");
            Report.AppendLine("Editor Animator evaluation passed Idle -> Fed -> Jump -> Idle; root placement/scale unchanged. This is not Play Mode.");
        }

        private static GameObject MakeEnclosure()
        {
            var root = new GameObject("AnimalEnclosure");
            root.AddComponent<AnimalEnclosure>();
            string fence = "Assets/MarpaStudio/Built-In/Prefabs/Fence.prefab";
            for (int i = 0; i < 4; i++)
            {
                float p = -3f + 2f * i;
                AddProp(root.transform, fence, "North Fence " + (i+1), new Vector3(p,0,4), 0, 2, 1.5f, 0.24f);
                AddProp(root.transform, fence, "South Fence " + (i+1), new Vector3(p,0,-4), 0, 2, 1.5f, 0.24f);
                AddProp(root.transform, fence, "West Fence " + (i+1), new Vector3(-4,0,p), 90, 2, 1.5f, 0.24f);
            }
            AddProp(root.transform, fence, "East Fence South", new Vector3(4,0,-3),90,2,1.5f,0.24f);
            AddProp(root.transform, fence, "East Fence South Post", new Vector3(4,0,-1.5f),90,1,1.5f,0.24f);
            AddProp(root.transform, fence, "East Fence North Post", new Vector3(4,0,1.5f),90,1,1.5f,0.24f);
            AddProp(root.transform, fence, "East Fence North", new Vector3(4,0,3),90,2,1.5f,0.24f);
            AddProp(root.transform, "Assets/MarpaStudio/Built-In/Prefabs/Gate.prefab", "Gate - held open",
                new Vector3(2.95f,0,1),0,1.85f,1.35f,0.2f);
            AddProp(root.transform, "Assets/MarpaStudio/Built-In/Prefabs/FoodTrough.prefab", "Feeding Trough",
                new Vector3(-1.4f,0,2.3f),0,2.4f,0.55f,0.85f);
            return root;
        }

        private static void AddProp(Transform parent, string prefabPath, string name,
            Vector3 position, float yaw, float width, float height, float depth)
        {
            var wrapper = new GameObject(name);
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            prop.transform.SetParent(wrapper.transform, false);
            Bounds bounds = RendererBounds(prop);
            prop.transform.localScale = Vector3.Scale(prop.transform.localScale,
                new Vector3(width / bounds.size.x, height / bounds.size.y, 1f));
            bounds = RendererBounds(prop);
            prop.transform.position -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
            var collider = wrapper.AddComponent<BoxCollider>();
            collider.size = new Vector3(width,height,depth);
            collider.center = new Vector3(0,height/2f,0);
            wrapper.transform.SetParent(parent,false);
            wrapper.transform.localPosition = position;
            wrapper.transform.localRotation = Quaternion.Euler(0,yaw,0);
        }

        private static Bounds RendererBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Model has no renderers: " + obj.name);
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
