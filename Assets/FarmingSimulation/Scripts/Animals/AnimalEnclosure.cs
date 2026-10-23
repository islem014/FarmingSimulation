using UnityEngine;

namespace FarmingSimulation
{
    // Axis-aligned walking area; the scene fence is the physical boundary.
    public sealed class AnimalEnclosure : MonoBehaviour
    {
        [SerializeField] private Vector2 halfSize = new Vector2(4f, 4f);
        [SerializeField, Min(0.6f)] private float animalInset = 1f;

        public Vector3 ClampPosition(Vector3 position)
        {
            Vector3 center = transform.position;
            float x = Mathf.Max(0f, halfSize.x - animalInset);
            float z = Mathf.Max(0f, halfSize.y - animalInset);
            return new Vector3(Mathf.Clamp(position.x, center.x - x, center.x + x),
                position.y, Mathf.Clamp(position.z, center.z - z, center.z + z));
        }

        public Vector3 SampleDestination(System.Random random, float y)
        {
            Vector3 p = transform.position;
            p.x += ((float)random.NextDouble() * 2f - 1f) * Mathf.Max(0f, halfSize.x - animalInset);
            p.z += ((float)random.NextDouble() * 2f - 1f) * Mathf.Max(0f, halfSize.y - animalInset);
            p.y = y;
            return p;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f,
                new Vector3(halfSize.x * 2f, 1f, halfSize.y * 2f));
        }
    }
}
