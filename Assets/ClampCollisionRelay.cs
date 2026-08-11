using UnityEngine;

/// <summary>
/// Forwards physics messages to <see cref="clamp"/> when the Rigidbody lives on a child
/// (Unity sends collisions to the Rigidbody GameObject, not the parent that holds <see cref="clamp"/>).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ClampCollisionRelay : MonoBehaviour
{
    public clamp owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null)
            owner.HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (owner != null)
            owner.HandleCollision(collision);
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.HandleTrigger(other);
    }
}
