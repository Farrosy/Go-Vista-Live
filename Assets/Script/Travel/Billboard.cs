using UnityEngine;

/// Membuat objek selalu menghadap kamera (mis. label melayang di atas TravelPad).
public class Billboard : MonoBehaviour
{
    private Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            else return;
        }
        transform.rotation = cam.rotation;
    }
}
