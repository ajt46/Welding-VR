using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectGrab : MonoBehaviour
{
    public string nameofasset;
    public bool isGrabbed;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnGrabbed()
    {
        isGrabbed = true;
    }

    public void OnReleased()
    {
        isGrabbed = false;
    }
}
