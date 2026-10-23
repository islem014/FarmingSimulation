using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FarmingSimulation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class AnimalDashboardController : MonoBehaviour
    {
        [SerializeField] private GameObject screen;
        [SerializeField] private RectTransform content;
        [SerializeField] private AnimalDashboardView details;
        [SerializeField] private Text summary;
        [SerializeField] private Button closeButton;
        private sealed class Entry
        {
            public AnimalStats Animal; public AnimalDashboardView View; public AnimalAlertSystem.Summary Alert;
        }
        private readonly List<Entry> entries=new List<Entry>();
        private InputAction toggle,escape;
        private AnimalStats selected;
        private bool dirty=true,open,oldVisible;
        private CursorLockMode oldLock;
        private float nextRefresh;
        private GameObject previousSelection;
        private static AnimalDashboardController owner;
        private static int closedFrame=int.MinValue;
        public static bool GameplayInputBlocked => owner!=null || Time.frameCount==closedFrame;
        public bool IsOpen => open;
        public int AnimalCount => entries.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession(){owner=null;closedFrame=int.MinValue;}

        private void Awake()
        {
            BuildUI();screen.SetActive(false);
            toggle=new InputAction("Animal dashboard",InputActionType.Button,"<Keyboard>/tab");
            escape=new InputAction("Close dashboard",InputActionType.Button,"<Keyboard>/escape");
            closeButton.onClick.AddListener(CloseFromButton);
            // Respect an existing EventSystem; otherwise create one with New Input System UI actions.
            if(FindAnyObjectByType<EventSystem>()==null)
            {
                var events=new GameObject("Dashboard EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        private void OnEnable()
        {
            toggle?.Enable();escape?.Enable();
            AnimalStats.AvailabilityChanged+=Availability;
            foreach(var animal in FindObjectsByType<AnimalStats>())
                if(animal.isActiveAndEnabled)Availability(animal,true);
        }
        private void OnDisable()
        {
            Close(false);
            toggle?.Disable();escape?.Disable();AnimalStats.AvailabilityChanged-=Availability;
            foreach(var entry in entries)
            {
                if(entry.Animal!=null)entry.Animal.Changed-=MarkDirty;
                if(entry.View!=null)RemoveView(entry.View.gameObject);
            }
            entries.Clear(); selected=null;
        }
        private void OnDestroy(){toggle?.Dispose();escape?.Dispose();}
        private void Availability(AnimalStats animal,bool available)
        {
            if(animal==null)return;
            var entry=entries.Find(e=>e.Animal==animal);
            if(available && entry==null)
            {
                entries.Add(new Entry{Animal=animal}); animal.Changed+=MarkDirty;
            }
            else if(!available && entry!=null)
            {
                animal.Changed-=MarkDirty;if(entry.View!=null)RemoveView(entry.View.gameObject);entries.Remove(entry);
                if(selected==animal)selected=null;
            }
            dirty=true;
        }
        private void MarkDirty()=>dirty=true;
        private void Update()
        {
            if(Application.isFocused)
            {
                if(open && escape.WasPressedThisFrame())Close(true);
                else if(toggle.WasPressedThisFrame()){if(open)Close(false);else Open();}
            }
            if(open && dirty && Time.unscaledTime>=nextRefresh){Refresh();nextRefresh=Time.unscaledTime+.2f;}
        }
        public void Open()
        {
            if(open || (owner!=null && owner!=this))return;
            oldLock=Cursor.lockState;oldVisible=Cursor.visible;
            previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            owner=this;open=true;screen.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Refresh(); if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
        private void CloseFromButton()=>Close(false);
        public void Close(bool releaseCursor)
        {
            if(!open)return;
            open=false;if(owner==this)owner=null;closedFrame=Time.frameCount;
            if(screen!=null)screen.SetActive(false);
            Cursor.lockState=releaseCursor || !Application.isFocused?CursorLockMode.None:oldLock;
            Cursor.visible=releaseCursor || !Application.isFocused?true:oldVisible;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        public void Select(AnimalStats animal){selected=animal;dirty=true;Refresh();}
        public void Refresh()
        {
            dirty=false;
            // Defensive cleanup handles destroyed objects even if an external script skipped lifecycle events.
            for(int i=entries.Count-1;i>=0;i--)if(entries[i].Animal==null)
            {if(entries[i].View!=null)RemoveView(entries[i].View.gameObject);entries.RemoveAt(i);}
            foreach(var entry in entries)entry.Alert=AnimalAlertSystem.Evaluate(entry.Animal);
            entries.Sort((a,b)=>{int severity=b.Alert.Severity.CompareTo(a.Alert.Severity);return severity!=0?severity:string.Compare(a.Animal.AnimalName,b.Animal.AnimalName,StringComparison.Ordinal);});
            if(selected==null && entries.Count>0)selected=entries[0].Animal;
            int alerts=0,care=0;
            for(int i=0;i<entries.Count;i++)
            {
                var entry=entries[i];alerts+=entry.Alert.Count;if(entry.Alert.Count>0)care++;
                if(entry.View==null)
                {
                    var card=DashboardUI.Panel("Animal card",content,Vector2.zero,Vector2.one,Color.white);
                    var layout=card.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=240;
                    entry.View=card.gameObject.AddComponent<AnimalDashboardView>();entry.View.Build(false);
                    var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;
                    AnimalStats captured=entry.Animal;button.onClick.AddListener(()=>Select(captured));
                }
                entry.View.transform.SetSiblingIndex(i);entry.View.Show(entry.Animal,entry.Alert,selected==entry.Animal);
            }
            summary.text=entries.Count+" animals   /   "+care+" need care   /   "+alerts+" alerts   -   urgent first";
            details.Show(selected,AnimalAlertSystem.Evaluate(selected),false,true);
        }

        // Also called by the editor setup to save a fully inspectable uGUI hierarchy in the prefab.
        public void BuildUI()
        {
            if(screen!=null)return;
            screen=new GameObject("Animal Dashboard Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            screen.transform.SetParent(transform,false);var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=screen.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var shade=DashboardUI.Panel("Dim background",screen.transform,Vector2.zero,Vector2.one,new Color(.08f,.12f,.08f,.82f));
            var panel=DashboardUI.Panel("Management panel",shade.transform,new Vector2(.035f,.045f),new Vector2(.965f,.955f),new Color(.96f,.94f,.87f));
            DashboardUI.Label("Heading",panel.transform,new Vector2(.03f,.91f),new Vector2(.75f,.98f),32,DashboardUI.Ink,"ANIMAL CARE");
            summary=DashboardUI.Label("Live summary",panel.transform,new Vector2(.03f,.84f),new Vector2(.80f,.91f),20,DashboardUI.Ink);
            closeButton=DashboardUI.Button("Close",panel.transform,new Vector2(.83f,.88f),new Vector2(.97f,.965f),"Close  [Tab]");
            var scrollRoot=DashboardUI.Rect("Animal list",panel.transform,new Vector2(.025f,.105f),new Vector2(.55f,.825f));
            var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=DashboardUI.Rect("Viewport",scrollRoot,Vector2.zero,Vector2.one);viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage=viewport.gameObject.AddComponent<Image>();viewportImage.color=new Color(.90f,.90f,.84f,.5f);
            content=DashboardUI.Rect("Cards",viewport,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.padding=new RectOffset(8,16,8,8);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;
            var track=DashboardUI.Panel("Scroll track",scrollRoot,new Vector2(.985f,0),Vector2.one,new Color(.82f,.83f,.76f));
            var handle=DashboardUI.Panel("Handle",track.transform,Vector2.zero,Vector2.one,DashboardUI.Green);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;
            var detailPanel=DashboardUI.Panel("Selected animal",panel.transform,new Vector2(.575f,.105f),new Vector2(.975f,.825f),Color.white);
            details=detailPanel.gameObject.AddComponent<AnimalDashboardView>();details.Build(true);
            DashboardUI.Label("Footer",panel.transform,new Vector2(.03f,.015f),new Vector2(.97f,.09f),18,DashboardUI.Ink,
                "Live simulation continues. Visit troughs and animals to provide care.\nHunger / thirst: lower is better. Welfare is a gameplay indicator, not a diagnosis.  Esc: close and release cursor.");
        }
        private static void RemoveView(GameObject view)
        { if(Application.isPlaying)Destroy(view);else DestroyImmediate(view); }
    }
}
