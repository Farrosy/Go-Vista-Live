using UnityEngine;
using System.Collections;

public class PlayerTracker : MonoBehaviour 
{
    public CamTarget    camTarget;
    public PhysicsCharacterController   playerController;
    
	// Use this for initialization
	void Start () 
    {
	
	}
	
	// Update is called once per frame
	void Update () 
    {
	    if(playerController.insideVehicle)
        {
            camTarget.target=playerController.targetCar.transform;
        }
        else
        {
            camTarget.target=playerController.transform;
        }
	}
}
