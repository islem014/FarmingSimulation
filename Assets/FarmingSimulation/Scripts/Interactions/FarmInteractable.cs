using System;
using UnityEngine;

namespace FarmingSimulation
{
    // A small common contract for plots and animals; input and HUD remain separate.
    public abstract class FarmInteractable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public abstract string Details { get; }
        public event Action<FarmInteractable> FeedbackChanged;
        public abstract void Interact();
        protected void NotifyFeedbackChanged() => FeedbackChanged?.Invoke(this);
    }
}
