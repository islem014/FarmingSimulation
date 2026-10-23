using UnityEngine;

namespace FarmingSimulation
{
    // A small read-only IMGUI overlay: no UI package setup or input capture is needed.
    public sealed class FarmHUD : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        private FarmInteractable target;
        private string prompt;
        private string stateText;
        private GUIStyle textStyle;

        private void OnEnable()
        {
            if (interactor == null)
            {
                Debug.LogError("Assign PlayerInteractor to FarmHUD.", this);
                return;
            }
            interactor.TargetChanged += SetTarget;
            SetTarget(interactor.CurrentTarget);
        }

        private void SetTarget(FarmInteractable item)
        {
            if (target != null)
                target.FeedbackChanged -= RefreshText;
            target = item;
            if (target != null)
            {
                target.FeedbackChanged += RefreshText;
                RefreshText(target);
            }
            else
            {
                prompt = string.Empty;
                stateText = string.Empty;
            }
        }

        // The target event updates the HUD immediately for either a plot or an animal.
        private void RefreshText(FarmInteractable item)
        {
            prompt = item.Prompt;
            stateText = item.Details;
        }

        private void OnGUI()
        {
            if (AnimalDashboardController.GameplayInputBlocked || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
                return;
            Color previousColor = GUI.color;
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - 5, y - 2, 10, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 2, y - 5, 4, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 4, y - 1, 8, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 4, 2, 8), Texture2D.whiteTexture);

            if (target != null && target.isActiveAndEnabled)
            {
                if (textStyle == null)
                    textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
                textStyle.normal.textColor = Color.white;
                float width = Mathf.Min(520f, Screen.width - 16f);
                float textWidth = width - 16f;
                float promptHeight = textStyle.CalcHeight(new GUIContent(prompt), textWidth) + 4f;
                float detailsHeight = textStyle.CalcHeight(new GUIContent(stateText), textWidth) + 4f;
                Rect panel = new Rect(x - width / 2f, y + 24f, width, promptHeight + detailsHeight + 8f);
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, textWidth, promptHeight), prompt, textStyle);
                GUI.Label(new Rect(panel.x + 8f, panel.y + 4f + promptHeight, textWidth, detailsHeight), stateText, textStyle);
            }
            GUI.color = previousColor;
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.TargetChanged -= SetTarget;
            SetTarget(null);
        }
    }
}
