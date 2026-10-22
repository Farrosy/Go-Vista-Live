using UnityEngine;

public class CubeScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0f); 
        transform. Translate (movement * Time.deltaTime * 8f, Space. World);
        
        transform. forward = Vector3. Slerp (transform.forward, movement, Time.deltaTime * 8f);
    }
}
