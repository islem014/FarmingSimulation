using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FarmingSimulation.Editor
{
    public static class Phase8BSetup
    {
        private const string Prefab="Assets/FarmingSimulation/Prefabs/AnimalDashboard.prefab";
        [InitializeOnLoadMethod]
        private static void Watch()=>EditorApplication.update+=Check;
        private static void Check()
        {
            const string request=".phase8b-work/setup.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorSceneManager.GetActiveScene().isDirty){File.WriteAllText(".phase8b-work/status.txt","WAITING: save FarmScene first.");return;}
            File.Delete(request);
            try{Run();File.WriteAllText(".phase8b-work/status.txt","SUCCESS");}
            catch(Exception ex){File.WriteAllText(".phase8b-work/status.txt","FAILED: "+ex);Debug.LogException(ex);}
        }
        private static void Run()
        {
            if(!File.Exists(Prefab))
            {
                var root=new GameObject("Animal Dashboard");
                var dashboard=root.AddComponent<AnimalDashboardController>();dashboard.BuildUI();
                root.GetComponentInChildren<Canvas>().gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);UnityEngine.Object.DestroyImmediate(root);
            }
            var scene=EditorSceneManager.OpenScene("Assets/FarmScene.unity",OpenSceneMode.Single);
            if(!scene.GetRootGameObjects().Any(r=>r.GetComponentInChildren<AnimalDashboardController>(true)!=null))
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
                instance.transform.SetParent(scene.GetRootGameObjects().Single(g=>g.name=="UI").transform,false);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var report=new StringBuilder();
            Phase7Validation.Validate(scene,report);
            var animals=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AnimalStats>()).ToArray();
            Need(animals.Length==4,"Expected existing four animals");
            DashboardTests(animals,report);
            References(scene.GetRootGameObjects());
            var prefab=PrefabUtility.LoadPrefabContents(Prefab);
            try{References(new[]{prefab});}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            report.AppendLine("PASS: scene/dashboard serialized references. Editor tests and UI renders only; no Play Mode gameplay test.");
            File.WriteAllText(".phase8b-work/validation.txt",report.ToString());AssetDatabase.SaveAssets();
            Debug.Log("PHASE8B_SETUP_SUCCESS");
        }
        private static void DashboardTests(AnimalStats[] animals,StringBuilder report)
        {
            var preview=EditorSceneManager.NewPreviewScene();
            GameObject root=null;AnimalDashboardController dash=null;
            var lockBefore=Cursor.lockState;bool visibleBefore=Cursor.visible;float timeScale=Time.timeScale;
            try
            {
                root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),preview);
                dash=root.GetComponent<AnimalDashboardController>();Call(dash,"Awake");Call(dash,"OnEnable");
                Need(dash.AnimalCount==4,"Automatic animal discovery");
                dash.Open();Need(dash.IsOpen&&AnimalDashboardController.GameplayInputBlocked,"Open input gate");
                Need(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Open cursor");
                Need(Time.timeScale==timeScale,"Dashboard must not pause simulation");
                dash.Select(animals[0]);dash.Refresh();
                var detail=(AnimalDashboardView)Get(dash,"details");
                Need(detail.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(animals[0].AnimalName)),"Selected animal name");
                Need(detail.GetComponentsInChildren<Text>().Any(t=>t.text=="Energy  "+Mathf.RoundToInt(animals[0].Energy)+"/100"),"Live energy binding");
                dash.Close(false);Need(!dash.IsOpen&&AnimalDashboardController.GameplayInputBlocked,"Same-frame closing guard");
                Need(Cursor.lockState==lockBefore && Cursor.visible==visibleBefore,"Restore prior free cursor");
                dash.Open();dash.Close(true);Need(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Escape release cursor");
                // A future animal uses the same enable/disable registration, without a hardcoded name.
                var extra=new GameObject("Temporary dashboard test animal");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(extra,preview);
                var stats=extra.AddComponent<AnimalStats>();Set(stats,"animalName","Editor test animal");Call(stats,"Awake");Call(stats,"OnEnable");
                Need(dash.AnimalCount==5,"Dynamic registration");
                Set(stats,"hunger",90f);Set(stats,"thirst",90f);Set(stats,"health",30f);Set(stats,"energy",10f);Call(stats,"OnValidate");
                var alert=AnimalAlertSystem.Evaluate(stats);Need(alert.Count==5&&alert.Severity==2,"Urgent and low welfare alerts");
                dash.Select(stats);dash.Open();dash.Refresh();
                var content=(RectTransform)Get(dash,"content");
                Need(content.GetChild(0).GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Editor test animal")),"Urgent-first sorting");
                Set(stats,"hunger",0f);Set(stats,"thirst",0f);Set(stats,"health",100f);Set(stats,"energy",100f);Call(stats,"OnValidate");
                Need(AnimalAlertSystem.Evaluate(stats).Count==0,"Healthy has no contradictory alerts");
                stats.AdvanceEnergy(1,0);Need((bool)Get(dash,"dirty"),"Stats event invalidates dashboard");
                Call(stats,"OnDisable");UnityEngine.Object.DestroyImmediate(extra);dash.Refresh();
                Need(dash.AnimalCount==4,"Destroyed/disabled selected animal safely removed");
                dash.Select(animals[0]);
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1200)})
                    Render(root,size,report);
                report.AppendLine("PASS: four discovered instances, future instance registration/removal, real selection/value bindings, event refresh, urgent/no-alert classification and sorting, Tab-style open/close gate, Escape cursor, same-frame blocking and unchanged simulation time.");
            }
            finally
            {
                if(dash!=null){Call(dash,"OnDisable");Call(dash,"OnDestroy");}
                EditorSceneManager.ClosePreviewScene(preview);Cursor.lockState=lockBefore;Cursor.visible=visibleBefore;
            }
        }
        private static void Render(GameObject root,Vector2Int size,StringBuilder report)
        {
            var canvas=root.GetComponentInChildren<Canvas>(true);canvas.gameObject.SetActive(true);
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.enabled=false;
            var cameraObject=new GameObject("UI preview camera",typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,root.scene);
            var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.18f,.12f);camera.cullingMask=1<<5;
            camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            var texture=new RenderTexture(size.x,size.y,24);camera.targetTexture=texture;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            canvas.scaleFactor=Mathf.Min(size.x/1600f,size.y/900f);
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(root.GetComponent<AnimalDashboardController>(),"content"));Canvas.ForceUpdateCanvases();
            var details=(AnimalDashboardView)Get(root.GetComponent<AnimalDashboardController>(),"details");
            foreach(var text in details.GetComponentsInChildren<Text>())
                Need(text.preferredHeight<=text.rectTransform.rect.height+2,"Clipped details text at "+size+": "+text.name);
            camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;
            var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
            File.WriteAllBytes(".phase8b-work/dashboard-"+size.x+"x"+size.y+".png",image.EncodeToPNG());RenderTexture.active=old;
            UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraObject);
            report.AppendLine("PASS: UI layout/detail text bounds and editor render at "+size.x+"x"+size.y);
        }
        private static void References(GameObject[] roots)
        {
            foreach(var root in roots)foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script");
                foreach(var c in t.GetComponents<Component>())
                {if(c==null)continue;var p=new SerializedObject(c).GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)Need(p.objectReferenceValue!=null||p.objectReferenceEntityIdValue==default,"Broken reference "+p.propertyPath);}
            }
        }
        private static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
        private static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        private static void Set(object o,string n,object value)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
        private static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
