using UnityEngine;

namespace FarmingSimulation
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PhysicsCrate : MonoBehaviour
    {
        private bool hasLanded;

        private void OnCollisionEnter(Collision collision)
        {
            if (hasLanded)
                return;
            // A side bump is not a landing: require an upward supporting contact.
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) > 0.5f)
                {
                    hasLanded = true;
                    Debug.Log("Physics demo crate landed on " + collision.gameObject.name + ".", this);
                    break;
                }
            }
        }
    }
}
