using UnityEngine;

namespace FarmingSimulation
{
    // Ignore only this player's own colliders, not walls or other scene objects.
    // All-hit queries avoid losing a wall hit when the player's capsule is in front of it.
    public static class PlayerPhysicsQueries
    {
        public static bool Raycast(Ray ray, float distance, Transform player, out RaycastHit nearest)
        {
            return Closest(Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), player, out nearest);
        }

        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction,
            float distance, Transform player, out RaycastHit nearest)
        {
            return Closest(Physics.SphereCastAll(origin, radius, direction, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), player, out nearest);
        }

        private static bool Closest(RaycastHit[] hits, Transform player, out RaycastHit nearest)
        {
            nearest = default;
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player) || hit.distance >= closestDistance)
                    continue;
                nearest = hit;
                closestDistance = hit.distance;
                found = true;
            }
            return found;
        }
    }
}
