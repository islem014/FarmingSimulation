
using UnityEngine;

namespace FarmingSimulation
{
    public class AnimalSleepVisual : MonoBehaviour
    {
        [SerializeField] private AnimalStats animalStats;
        [SerializeField] private Transform visual;
        [SerializeField] private float lowering = 0.25f;
        [SerializeField] private float tilt = 12f;
        [SerializeField] private float speed = 3f;

        private Vector3 originalPosition;
        private Quaternion originalRotation;

        void Start()
        {
            if (animalStats == null || visual == null)
            {
                enabled = false;
                Debug.LogError("AnimalSleepVisual: Missing references.", this);
                return;
            }

            originalPosition = visual.localPosition;
            originalRotation = visual.localRotation;
        }

        void LateUpdate()
        {
            bool sleeping =
                animalStats.Behaviour == AnimalBehaviour.Resting;

            Vector3 targetPosition = originalPosition +
                (sleeping ? Vector3.down * lowering : Vector3.zero);

            Quaternion targetRotation = originalRotation *
                Quaternion.Euler(sleeping ? tilt : 0f, 0f, 0f);

            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);

            visual.localPosition = Vector3.Lerp(
                visual.localPosition, targetPosition, t);

            visual.localRotation = Quaternion.Slerp(
                visual.localRotation, targetRotation, t);
        }
    }
}
