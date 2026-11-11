using UnityEngine;
using System.Collections;

public class CarCamera : MonoBehaviour 
{
    public float        cameraHeight;
    public float        cameraDistance;
    public Transform    cameraTarget;
    public float        targetOffset;
    
	// Use this for initialization
	void Start () 
    {
	
	}
	
	// Update is called once per frame
	void LateUpdate () 
    {
        Vector3 tempPos=cameraTarget.position-cameraTarget.forward*cameraDistance;
        tempPos.y=cameraTarget.position.y;
        tempPos.y+=cameraHeight;
        transform.position=tempPos;
        transform.LookAt(cameraTarget.position + cameraTarget.forward * targetOffset);	
    }
}
