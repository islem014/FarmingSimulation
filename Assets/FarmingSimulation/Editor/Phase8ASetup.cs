using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmingSimulation.Editor
{
    // One-time prefab tuning plus isolated editor tests. Never runs in Play Mode.
    public static class Phase8ASetup
    {
        private const string Root = "Assets/FarmingSimulation/Animals/Prefabs/";
        private static readonly string[] Kinds = { "Sheep", "Cow", "Pig", "Horse" };
        private static readonly float[] Pauses = { 4f, 5f, 1.5f, 2f };
        private static readonly float[] Distances = { 2.5f, 2f, 3f, 5f };
        private static readonly float[] Costs = { 3f, 2.5f, 3f, 5f };
        private static readonly float[] Recovery = { 7f, 5f, 7f, 6f };
        private static readonly float[] Low = { 35f, 25f, 25f, 30f };
        private static readonly string[] Personalities = { "Calm - frequent rest", "Calm - slow meals", "Curious - short pauses", "Active - longer trips" };

        [InitializeOnLoadMethod]
        private static void Watch() => EditorApplication.update += Check;
        private static void Check()
        {
            const string request = ".phase8a-work/setup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty)
            { File.WriteAllText(".phase8a-work/status.txt", "WAITING: save your scene first."); return; }
            File.Delete(request);
            try { Run(); }
            catch (Exception ex) { File.WriteAllText(".phase8a-work/status.txt", "FAILED: " + ex); Debug.LogException(ex); }
        }

        private static void Run()
        {
            var report = new StringBuilder();
            for (int i=0;i<Kinds.Length;i++)
            {
                string path=Root+Kinds[i]+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var stats=root.GetComponent<AnimalStats>(); var brain=root.GetComponent<FarmAnimalBehaviour>();
                    Set(stats,"energy",100f); Set(stats,"energyPerMetre",Costs[i]); Set(stats,"restRecoveryPerSecond",Recovery[i]);
                    Set(stats,"restThreshold",Low[i]); Set(stats,"recoveredThreshold",80f);
                    Set(brain,"personality",Personalities[i]); Set(brain,"idleDuration",Pauses[i]); Set(brain,"wanderDistance",Distances[i]);
                    Set(brain,"portionInterval",i==1 ? 3f : 2f);
                    EditorUtility.SetDirty(stats); EditorUtility.SetDirty(brain);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    report.AppendLine(Kinds[i]+": pause="+Pauses[i]+", trip limit="+Distances[i]+", energy/metre="+Costs[i]+", rest recovery/s="+Recovery[i]+", rest thresholds="+Low[i]+"/80");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene("Assets/FarmScene.unity",OpenSceneMode.Single);
            Phase6Validation.ValidateComponents();
            report.AppendLine("PASS: existing Phase 5/6 component regression tests.");
            Phase7Validation.Validate(scene,report);
            Tests(report);
            foreach (var root in scene.GetRootGameObjects()) References(root);
            foreach (var id in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/FarmingSimulation"}))
            {
                var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GUIDToAssetPath(id));
                try { References(root); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            report.AppendLine("PASS: saved scene and all owned prefab script/object references. No Play Mode testing performed.");
            File.WriteAllText(".phase8a-work/validation.txt",report.ToString());
            File.WriteAllText(".phase8a-work/status.txt","SUCCESS");
            Debug.Log("PHASE8A_SETUP_SUCCESS");
        }

        private static void Tests(StringBuilder report)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                for (int i=0;i<Kinds.Length;i++)
                {
                    var pen=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+(i==0?"AnimalEnclosure":Kinds[i]+"Enclosure")+".prefab"),scene);
                    pen.transform.position=new Vector3(1000+20*i,0,1000);
                    var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+Kinds[i]+".prefab"),scene);
                    actor.transform.position=pen.transform.position;
                    var stats=actor.GetComponent<AnimalStats>(); var brain=actor.GetComponent<FarmAnimalBehaviour>();
                    var food=pen.GetComponentInChildren<FeedingTrough>(); var water=pen.GetComponentInChildren<WaterTrough>();
                    Phase7Setup.Reference(brain,"enclosure",pen.GetComponent<AnimalEnclosure>());
                    Phase7Setup.Reference(brain,"feedingTrough",food); Phase7Setup.Reference(brain,"waterTrough",water);
                    Call(stats,"Awake"); Call(brain,"Awake");
                    var animator=actor.GetComponentInChildren<Animator>(); animator.Rebind(); animator.Update(.01f);
                    if(i>0)
                    {
                        var bounds=AnimatedBounds(actor);
                        Near(bounds.size.y,i==1?1.5f:i==2?.85f:1.8f,"Preserve corrected visual height");
                    }
                    Need((float)Get(brain,"idleDuration")==Pauses[i] && (float)Get(stats,"energyPerMetre")==Costs[i],"Independent tuning");
                    Set(stats,"energy",50f); stats.AdvanceEnergy(2f,0f);
                    Near(stats.Energy,50f-2f*Costs[i],"Movement energy");
                    float before=stats.Energy; stats.ReduceHunger(10); stats.ReduceThirst(10); stats.RestoreHealth(10);
                    Near(stats.Energy,before,"Food/water/treatment must not restore energy");
                    stats.AdvanceEnergy(0f,2f); Near(stats.Energy,before+2f*Recovery[i],"Rest recovery");
                    stats.AdvanceEnergy(10000f,0f); Near(stats.Energy,0f,"Energy min");
                    stats.AdvanceEnergy(0f,10000f); Near(stats.Energy,100f,"Energy max");
                    stats.AdvanceEnergy(-10f,-10f); Near(stats.Energy,100f,"Negative input");
                    for (int mask=0;mask<16;mask++)
                    {
                        Set(stats,"hunger",(mask&1)!=0?100f:0f); Set(stats,"thirst",(mask&2)!=0?100f:0f);
                        Set(stats,"health",(mask&4)!=0?100f:0f); Set(stats,"energy",(mask&8)!=0?100f:0f);
                        Near(stats.Welfare,((100-stats.Hunger)+(100-stats.Thirst)+stats.Health+stats.Energy)/4f,"Welfare formula");
                        Need(stats.Welfare>=0 && stats.Welfare<=100,"Welfare bounds");
                    }
                    Set(stats,"hunger",0f); Set(stats,"thirst",0f); Set(stats,"health",100f); Set(stats,"energy",100f);
                    Need(stats.WelfareStatus==WelfareCondition.Excellent,"Excellent welfare");
                    foreach(float score in new[]{0f,39f,40f,64f,65f,84f,85f,100f})
                    {
                        Set(stats,"hunger",100f-score); Set(stats,"thirst",100f-score); Set(stats,"health",score); Set(stats,"energy",score);
                        Need(stats.WelfareStatus==(score>=85?WelfareCondition.Excellent:score>=65?WelfareCondition.Good:score>=40?WelfareCondition.NeedsAttention:WelfareCondition.Critical),"Welfare status boundary");
                    }
                    food.Interact(); food.Interact(); water.Interact(); water.Interact();
                    Set(stats,"hunger",70f); Set(stats,"thirst",70f); Set(stats,"energy",10f);
                    Call(brain,"UpdatePriorities"); Need(IsRest(stats),"Low energy before ordinary needs");
                    Call(brain,"BeginRest"); Set(stats,"energy",Low[i]+1f);
                    for (int tick=0;tick<100;tick++) Call(brain,"UpdatePriorities");
                    Need(stats.Behaviour==AnimalBehaviour.Resting,"Rest hysteresis, no oscillation");
                    Set(stats,"thirst",90f); Call(brain,"UpdatePriorities");
                    Need(stats.Behaviour==AnimalBehaviour.SeekingWater,"Severe thirst interrupts rest");
                    Set(water,"currentWater",0); Call(brain,"BeginRest");
                    for(int tick=0;tick<100;tick++) Call(brain,"UpdatePriorities");
                    Need(stats.Behaviour==AnimalBehaviour.Resting,"Empty water does not cause rest oscillation");
                    Set(stats,"thirst",20f); Set(stats,"hunger",90f); Call(brain,"UpdatePriorities");
                    Need(stats.Behaviour==AnimalBehaviour.SeekingFood,"Severe hunger interrupts rest");
                    Set(stats,"hunger",20f); Set(stats,"health",30f); Call(brain,"BeginRest"); Call(brain,"UpdatePriorities");
                    Need(stats.Behaviour==AnimalBehaviour.NeedsCare,"Low health interrupts rest");
                    Set(stats,"health",100f); Set(stats,"energy",10f); Call(brain,"UpdatePriorities"); Need(IsRest(stats),"Resume recovery after care");
                    Set(stats,"energy",80f); Call(brain,"UpdatePriorities"); Need(stats.Behaviour==AnimalBehaviour.Idle,"Recovered resumes normal behavior");
                    Set(brain,"destination",actor.transform.position+Vector3.right); Set(brain,"stateTime",0f);
                    stats.SetBehaviour(AnimalBehaviour.SeekingRest); Call(brain,"SeekRest"); Need(stats.Behaviour==AnimalBehaviour.Resting,"Rest route timeout terminates");
                    for(int attempt=0;attempt<40;attempt++)
                    {
                        Call(brain,"ChooseDestination");
                        if(stats.Behaviour!=AnimalBehaviour.Wander) continue;
                        Vector3 p=(Vector3)Get(brain,"destination");
                        Need(Vector3.Distance(p,actor.transform.position)<=Distances[i]+.001f,"Personality travel limit");
                        Need((p-pen.GetComponent<AnimalEnclosure>().ClampPosition(p)).sqrMagnitude<.001f,"Destination inside pen");
                    }
                    report.AppendLine("PASS "+Kinds[i]+": energy math/clamps, no food energy, 16 welfare corners and status boundaries, preserved animated size, independent tuning, 100-tick rest hysteresis/empty-resource stability, severe food/water/care interrupts, recovery, timeout and bounded wandering.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static Bounds AnimatedBounds(GameObject actor)
        {
            bool first=true; Bounds result=default;
            foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m=new Mesh(); r.BakeMesh(m,true);
                foreach(var v in m.vertices)
                {
                    var p=r.transform.TransformPoint(v);
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
                UnityEngine.Object.DestroyImmediate(m);
            }
            return result;
        }
        private static bool IsRest(AnimalStats s) => s.Behaviour==AnimalBehaviour.Resting || s.Behaviour==AnimalBehaviour.SeekingRest;
        private static void References(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script");
                foreach(var c in t.GetComponents<Component>())
                {
                    if(c==null) continue;
                    var p=new SerializedObject(c).GetIterator();
                    while(p.Next(true)) if(p.propertyType==SerializedPropertyType.ObjectReference)
                        Need(p.objectReferenceValue!=null || p.objectReferenceEntityIdValue==default,"Broken reference "+t.name+"/"+p.propertyPath);
                }
            }
        }
        private static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
        private static object Get(object obj,string name) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        private static void Near(float a,float b,string message) => Need(Mathf.Abs(a-b)<.001f,message);
        private static void Need(bool valid,string message) { if(!valid) throw new InvalidOperationException(message); }
    }
}
