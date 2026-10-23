using UnityEngine;
using UnityEngine.UI;

namespace FarmingSimulation
{
    // Shared uGUI construction and reusable value bars for cards and selected-animal details.
    public static class DashboardUI
    {
        public static readonly Color Ink=new Color(.18f,.23f,.17f), Green=new Color(.24f,.43f,.29f), Amber=new Color(.68f,.36f,.08f), Red=new Color(.70f,.20f,.15f);
        public static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
        }
        public static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        { var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;return image; }
        public static Text Label(string name,Transform parent,Vector2 min,Vector2 max,int size,Color color,string text="")
        {
            var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize=size;t.color=color;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        public static Button Button(string name,Transform parent,Vector2 min,Vector2 max,string caption)
        {
            var image=Panel(name,parent,min,max,Green);var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var label=Label("Label",image.transform,Vector2.zero,Vector2.one,20,Color.white,caption);label.alignment=TextAnchor.MiddleCenter;return b;
        }
    }

    public sealed class AnimalDashboardView : MonoBehaviour
    {
        [SerializeField] private Text title, status, warnings;
        [SerializeField] private Text[] values;
        [SerializeField] private Image[] fills;
        [SerializeField] private Image background;
        public void Build(bool detail)
        {
            background=gameObject.GetComponent<Image>();
            title=DashboardUI.Label("Animal",transform,new Vector2(.04f,.80f),new Vector2(.96f,.97f),detail?30:24,DashboardUI.Ink);
            status=DashboardUI.Label("Status",transform,new Vector2(.04f,.69f),new Vector2(.96f,.81f),detail?20:17,DashboardUI.Ink);
            values=new Text[5];fills=new Image[5];
            string[] labels={"Hunger","Thirst","Health","Energy","Welfare"};
            for(int i=0;i<5;i++)
            {
                float top=.68f-i*.095f;
                values[i]=DashboardUI.Label(labels[i],transform,new Vector2(.04f,top-.075f),new Vector2(.49f,top),detail?21:17,DashboardUI.Ink);
                var track=DashboardUI.Panel(labels[i]+" Track",transform,new Vector2(.51f,top-.055f),new Vector2(.96f,top-.015f),new Color(.86f,.87f,.81f));track.raycastTarget=false;
                fills[i]=DashboardUI.Panel("Fill",track.transform,Vector2.zero,Vector2.one,DashboardUI.Green);fills[i].raycastTarget=false;
            }
            warnings=DashboardUI.Label("Care alerts",transform,new Vector2(.04f,.025f),new Vector2(.96f,.20f),detail?19:16,DashboardUI.Ink);
        }
        public void Show(AnimalStats animal,AnimalAlertSystem.Summary alert,bool selected,bool detail=false)
        {
            if(animal==null){title.text="No animal selected";status.text="Select an animal from the list.";warnings.text="No active animals found.";foreach(var v in values)v.text="";foreach(var f in fills)f.rectTransform.anchorMax=new Vector2(0,1);return;}
            title.text=animal.AnimalName+"  /  "+animal.Species;
            status.text=AnimalAlertSystem.BehaviourLabel(animal.Behaviour)+"  |  "+animal.WelfareLabel;
            float[] amounts={animal.Hunger,animal.Thirst,animal.Health,animal.Energy,animal.Welfare};
            string[] names={"Hunger","Thirst","Health","Energy","Welfare"};
            for(int i=0;i<5;i++)
            {
                values[i].text=names[i]+"  "+Mathf.RoundToInt(amounts[i])+"/100";
                fills[i].rectTransform.anchorMax=new Vector2(Mathf.Clamp01(amounts[i]/100f),1);
                float quality=i<2?100f-amounts[i]:amounts[i];
                fills[i].color=quality<35?DashboardUI.Red:quality<65?DashboardUI.Amber:DashboardUI.Green;
            }
            warnings.text=detail?alert.Message+"\nCare condition: "+(animal.Condition==AnimalCondition.NeedsCare?"Needs Care":animal.Condition.ToString())
                :alert.Count==0?"No care alerts":(alert.Severity==2?"URGENT":"ATTENTION")+"  /  "+alert.Count+" alerts - select for details";
            warnings.color=alert.Severity==2?DashboardUI.Red:alert.Severity==1?DashboardUI.Amber:DashboardUI.Green;
            if(background!=null)background.color=selected?new Color(.88f,.94f,.84f):new Color(1f,.99f,.95f);
        }
    }
}
