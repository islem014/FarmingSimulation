using System.Collections.Generic;
using UnityEngine;

namespace FarmingSimulation
{
    // Read-only presentation of existing thresholds. Never changes animal needs.
    public static class AnimalAlertSystem
    {
        public struct Summary { public int Count, Severity; public string Message; }
        public static Summary Evaluate(AnimalStats animal)
        {
            if (animal == null) return default;
            var brain=animal.GetComponent<FarmAnimalBehaviour>();
            var notes=new List<string>(5); int severity=0;
            if(animal.SeverelyHungry || (brain!=null && animal.Hunger>=brain.HungryThreshold))
            { notes.Add(animal.SeverelyHungry?"URGENT: Needs Food":"Needs Food"); severity=animal.SeverelyHungry?2:1; }
            if(animal.SeverelyThirsty || (brain!=null && animal.Thirst>=brain.ThirstyThreshold))
            { notes.Add(animal.SeverelyThirsty?"URGENT: Needs Water":"Needs Water"); severity=Mathf.Max(severity,animal.SeverelyThirsty?2:1); }
            if(animal.NeedsTreatment) { notes.Add("URGENT: Needs Treatment"); severity=2; }
            if(animal.Tired) { notes.Add("Needs Rest"); severity=Mathf.Max(severity,1); }
            if(animal.WelfareStatus<=WelfareCondition.NeedsAttention)
            { notes.Add(animal.WelfareStatus==WelfareCondition.Critical?"URGENT: Critical welfare":"Welfare Needs Attention"); severity=Mathf.Max(severity,animal.WelfareStatus==WelfareCondition.Critical?2:1); }
            return new Summary { Count=notes.Count, Severity=severity, Message=notes.Count==0?"No care alerts":string.Join("  |  ",notes) };
        }
        public static string BehaviourLabel(AnimalBehaviour state)
        {
            switch(state)
            {
                case AnimalBehaviour.SeekingFood:return "Seeking food";
                case AnimalBehaviour.SeekingWater:return "Seeking water";
                case AnimalBehaviour.SeekingRest:return "Seeking rest";
                case AnimalBehaviour.NeedsCare:return "Needs care";
                case AnimalBehaviour.Wander:return "Wandering";
                default:return state.ToString();
            }
        }
    }
}
