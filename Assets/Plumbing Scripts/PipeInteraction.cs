using BNG;
using System.Collections;
using UnityEngine;

public class PipeInteraction : MonoBehaviour
{
    public string nameofasset;
    [Header("Snapping Settings")]
    public float snapSpeed = 5f; // speed of movement
    public float rotationSpeed = 5f; // speed of rotation

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pipe") && nameofasset == other.GetComponent<ObjectGrab>().nameofasset)
        {
            // Remove grabbing and physics
            Destroy(other.GetComponent<GrabbableUnityEvents>());
            Destroy(other.GetComponent<Grabbable>());
            Destroy(other.GetComponent<Rigidbody>());
            Destroy(other.GetComponent<BoxCollider>());
            this.GetComponent<BoxCollider>().enabled = false;
            this.GetComponent<MeshRenderer>().enabled = false;
            // Start coroutine to smoothly move pipe into place
            StartCoroutine(SmoothSnap(other.transform));
        }
    }

    private IEnumerator SmoothSnap(Transform pipe)
    {
        Vector3 startPos = pipe.position;
        Quaternion startRot = pipe.rotation;

        Vector3 targetPos = transform.position;
        Quaternion targetRot = transform.rotation;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * snapSpeed;

            pipe.position = Vector3.Lerp(startPos, targetPos, t);
            pipe.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        // Final align
        pipe.position = targetPos;
        pipe.rotation = targetRot;
    }
}
