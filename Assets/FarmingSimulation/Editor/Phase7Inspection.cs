using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    public static class Phase7Inspection
    {
        [InitializeOnLoadMethod]
        private static void Watch() { EditorApplication.update -= Check; EditorApplication.update += Check; }
        private static void Check()
        {
            const string request = ".phase7-work/inspect.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Inspect(); }
            catch (Exception ex) { File.WriteAllText(".phase7-work/inspection.txt", ex.ToString()); }
            EditorApplication.update -= Check;
        }
        public static void Inspect()
        {
            var report = new StringBuilder();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string species in new[] { "Sheep", "Cow", "Pig", "Horse" })
                {
                    string path = "Assets/FarmingSimulation/Animals/Models/" + species + ".fbx";
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    report.AppendLine(species + ": rig=" + importer.animationType + ", scale=" + importer.globalScale);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var clip in assets.OfType<AnimationClip>())
                    {
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        report.AppendLine($"  clip {clip.name}: {clip.length:F3}s, {bindings.Length} curves, legacy={clip.legacy}");
                    }
                    foreach (var m in assets.OfType<Material>()) report.AppendLine("  material " + m.name + " " + m.color + " texture=" + (m.mainTexture ? m.mainTexture.name : "none"));
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    report.AppendLine("  bind bounds " + bounds + "; Animator=" + (model.GetComponent<Animator>() != null));
                    foreach (var t in model.GetComponentsInChildren<Transform>().Where(t => t.name == "Head" || t.name == "Hips"))
                        report.AppendLine("  bone " + t.name + " position " + t.position);
                    UnityEngine.Object.DestroyImmediate(model);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FarmScene.unity");
            try
            {
                report.AppendLine("SCENE COLLIDERS (world-space bounds):");
                foreach (var c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>()))
                    report.AppendLine(c.name + ": " + c.bounds);
                report.AppendLine("ENVIRONMENT GROUPS (renderer bounds):");
                var environment = scene.GetRootGameObjects().Single(g => g.name == "Environment");
                foreach (Transform group in environment.transform)
                    foreach (Transform item in group)
                    {
                        var renderers = item.GetComponentsInChildren<Renderer>();
                        if (renderers.Length == 0) continue;
                        var bounds = renderers[0].bounds;
                        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                        report.AppendLine(group.name + "/" + item.name + ": " + bounds);
                    }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText(".phase7-work/inspection.txt", report.ToString());
        }
    }
}
