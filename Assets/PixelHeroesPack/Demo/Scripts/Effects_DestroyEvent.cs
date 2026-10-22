using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_DestroyEvent : MonoBehaviour
{
    public bool DestroyOnEvent = true;

    public void destroyEvent()
    {
        if (DestroyOnEvent)
            Destroy(gameObject);
    }
}
