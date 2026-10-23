using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    public static class AnimalVisualRepair
    {
        [InitializeOnLoadMethod]
        private static void Watch() => EditorApplication.update += Check;
        private static void Check()
        {
            if (File.Exists(".phase7-work/visual-repair.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating
                && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorSceneManager.GetActiveScene().isDirty)
            {
                File.Delete(".phase7-work/visual-repair.request");
                try { Repair(); }
                catch (Exception ex) { File.WriteAllText(".phase7-work/visual-repair.txt", "FAILED: " + ex); Debug.LogException(ex); }
            }
            if (!File.Exists(".phase7-work/visual-check.request") || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(".phase7-work/visual-check.request");
            var report = new StringBuilder();
            foreach (var kind in new[] { "Sheep", "Cow", "Pig", "Horse" })
            {
                var root = PrefabUtility.LoadPrefabContents(Phase7Setup.Root + "Prefabs/" + kind + ".prefab");
                try
                {
                    var a = root.GetComponentInChildren<Animator>(); a.Rebind(); a.Update(.01f);
                    foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        report.AppendLine(kind + " renderer=" + r.bounds + " scale=" + r.transform.lossyScale);
                        foreach (bool scale in new[] { false, true })
                        {
                            var m = new Mesh(); r.BakeMesh(m, scale);
                            var b = new Bounds(r.transform.TransformPoint(m.vertices[0]), Vector3.zero);
                            foreach (var v in m.vertices) b.Encapsulate(r.transform.TransformPoint(v));
                            report.AppendLine(" bake " + scale + " raw=" + m.bounds + " world=" + b);
                            UnityEngine.Object.DestroyImmediate(m);
                        }
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(".phase7-work/visual-check.txt", report.ToString());
        }

        private static Bounds Measure(GameObject root)
        {
            bool first = true; Bounds bounds = default;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); r.BakeMesh(mesh, true);
                foreach (var v in mesh.vertices)
                {
                    var p = r.transform.TransformPoint(v);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            return bounds;
        }

        private static void Repair()
        {
            var report = new StringBuilder();
            foreach (var config in Phase7Setup.SpeciesList)
            {
                Bounds finalBounds = default;
                string path = Phase7Setup.Root + "Prefabs/" + config.kind + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var placement = root.transform.Find("Visual Placement");
                    placement.localScale = Vector3.one; placement.localPosition = Vector3.zero;
                    var animator = root.GetComponentInChildren<Animator>(); animator.Rebind(); animator.Update(.01f);
                    var bounds = Measure(root);
                    placement.localScale = Vector3.one * (config.height / bounds.size.y);
                    bounds = Measure(root);
                    placement.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    bounds = Measure(root);
                    if (Mathf.Abs(bounds.size.y-config.height) > .02f || Mathf.Abs(bounds.min.y) > .02f)
                        throw new Exception("Visual size/ground alignment failed: " + config.kind);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine(config.kind + " corrected height=" + bounds.size.y.ToString("F3") + " scale=" + placement.localScale);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // Validate the saved prefab again after Animator initialization, not only during setup.
                root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var a = root.GetComponentInChildren<Animator>(); a.Rebind(); a.Update(.01f);
                    var b = Measure(root);
                    finalBounds = b;
                    if (Mathf.Abs(b.size.y-config.height) > .02f) throw new Exception("Saved animated size invalid: " + config.kind);
                    report.AppendLine("Reloaded animated " + config.kind + " bounds=" + b);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                string penPath = Phase7Setup.Root + "Prefabs/" + config.kind + "Enclosure.prefab";
                var pen = PrefabUtility.LoadPrefabContents(penPath);
                try
                {
                    float inset = new Vector2(finalBounds.extents.x, finalBounds.extents.z).magnitude + .2f;
                    Phase7Setup.Number(pen.GetComponent<AnimalEnclosure>(), "animalInset", Mathf.Max(1.1f, inset));
                    float distance = Mathf.Max(1.35f, finalBounds.extents.z + .65f, config.radius + .7f);
                    var food = pen.GetComponentInChildren<FeedingTrough>();
                    var water = pen.GetComponentInChildren<WaterTrough>();
                    foreach (var point in new[] { food.FeedingPoint, food.ApproachPoint, water.DrinkingPoint, water.ApproachPoint })
                    {
                        var p = point.localPosition;
                        p.z = point == food.FeedingPoint || point == food.ApproachPoint ? -distance : distance;
                        point.localPosition = p; PrefabUtility.RecordPrefabInstancePropertyModifications(point);
                    }
                    PrefabUtility.SaveAsPrefabAsset(pen, penPath);
                    report.AppendLine(config.kind + " safe inset=" + Mathf.Max(1.1f,inset) + " trough stand-off=" + distance);
                }
                finally { PrefabUtility.UnloadPrefabContents(pen); }
            }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/FarmScene.unity", OpenSceneMode.Single);
            Phase7Validation.Validate(scene, report);
            File.WriteAllText(".phase7-work/visual-repair.txt", "SUCCESS\n" + report);
            Debug.Log("PHASE7_VISUAL_REPAIR_SUCCESS");
        }
    }
}
