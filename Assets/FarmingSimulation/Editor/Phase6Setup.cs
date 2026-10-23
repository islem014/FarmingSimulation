using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    // Explicit, resumable setup. No action without an opt-in request or menu command.
    public static class Phase6Setup
    {
        private const string Root = "Assets/FarmingSimulation/Animals/";
        private const string WaterPath = Root + "Prefabs/WaterTrough.prefab";
        private const string PenPath = Root + "Prefabs/AnimalEnclosure.prefab";
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
            const string request = ".phase6-work/setup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                File.WriteAllText(".phase6-work/setup-status.txt", "WAITING: save the current scene before setup.");
                return;
            }
            File.Delete(request);
            try { Build(); File.WriteAllText(".phase6-work/setup-status.txt", "SUCCESS"); }
            catch (Exception ex)
            {
                File.WriteAllText(".phase6-work/setup-status.txt", "FAILED: " + ex);
                Debug.LogException(ex);
            }
            EditorApplication.update -= CheckRequest;
        }

        [MenuItem("FarmingSimulation/Phase 6/Complete water and care setup")]
        public static void Build()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save the scene first.");
            Directory.CreateDirectory(".phase6-work");
            var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
            var penInstance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AnimalEnclosure>()).Single();
            var sheepInstance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SheepBehaviour>()).Single();
            if (!File.Exists(WaterPath)) CreateWaterPrefab();
            var pen = PrefabUtility.LoadPrefabContents(PenPath);
            try
            {
                var waters = pen.GetComponentsInChildren<WaterTrough>();
                if (waters.Length > 1) throw new InvalidOperationException("Unexpected duplicate water troughs.");
                if (waters.Length == 0)
                {
                    var water = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath), pen.scene);
                    water.transform.SetParent(pen.transform, false);
                    water.transform.localPosition = new Vector3(-1.4f, 0f, -2.3f);
                    PrefabUtility.SaveAsPrefabAsset(pen, PenPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(pen); }

            var sheep = PrefabUtility.LoadPrefabContents(SheepPath);
            try
            {
                if (!sheep.GetComponent<AnimalCare>()) sheep.AddComponent<AnimalCare>();
                // Unity serializes new defaults without replacing the tested existing settings.
                PrefabUtility.SaveAsPrefabAsset(sheep, SheepPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(sheep); }
            var trough = penInstance.GetComponentsInChildren<WaterTrough>().Single();
            SetReference(sheepInstance, "waterTrough", trough);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sheepInstance);
            Phase6Validation.ValidateScene(scene);
            Phase6Validation.ValidateComponents();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(".phase6-work/editor-validation.txt", Phase6Validation.Report);
            Debug.Log("PHASE6_SETUP_SUCCESS: water/care setup saved; editor validation passed.");
        }

        private static void CreateWaterPrefab()
        {
            // Clone our owned wrapper. Nested MarpaStudio model/material references stay untouched.
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/FeedingTrough.prefab"));
            root.name = "Water Trough";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<FeedingTrough>());
                var point = root.transform.Find("Feeding Position");
                point.name = "Drinking Position";
                point.localPosition = new Vector3(0f, 0f, 1.35f);
                var approach = root.transform.Find("Approach Position");
                approach.localPosition = new Vector3(2.1f, 0f, 1.35f);
                var visual = root.transform.Find("Hay Level");
                visual.name = "Water Level";
                var materialPath = Root + "Materials/Trough_Water.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Trough_Water" };
                    material.SetColor("_BaseColor", new Color(0.08f, 0.48f, 0.8f));
                    material.SetFloat("_Smoothness", 0.75f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                visual.GetComponent<Renderer>().sharedMaterial = material;
                visual.gameObject.SetActive(false);
                var water = root.AddComponent<WaterTrough>();
                SetReference(water, "drinkingPoint", point);
                SetReference(water, "approachPoint", approach);
                SetReference(water, "waterVisual", visual);
                PrefabUtility.SaveAsPrefabAsset(root, WaterPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        internal static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
