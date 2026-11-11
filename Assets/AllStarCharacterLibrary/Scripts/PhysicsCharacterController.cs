
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PhysicsCharacterController : MonoBehaviour 
{
	Animator                        animator;
    public CharacterController      physicsController;
	public bool                     wasAttacking;// we need this so we can take lock the direction we are facing during attacks, mecanim sometimes moves past the target which would flip the character around wildly
	public bool                     stop=true;
	float                           rotateSpeed = 50.0f; //used to smooth out turning

	public Vector3                  movementTargetPosition;
	public Vector3                  attackPos;
	public Vector3                  lookAtPos;
	float                           gravity = 5.0f;
	public CarModelControl[]        cars;
    public CarModelControl          targetCar;
    public bool                     usingCar;
    public bool                     insideVehicle;
    public int                      entryPoint;
	RaycastHit                      hit;
	Ray                             ray;
	
	public bool                     rightButtonDown=false;//we use this to "skip out" of consecutive right mouse down...
	
	// Use this for initialization
	void Start () 
	{	
        physicsController = GetComponent<CharacterController>();
		animator = GetComponentInChildren<Animator>();//need this...
		movementTargetPosition = transform.position;//initializing our movement target as our current position
        lookAtPos=transform.position+transform.forward;
        cars = FindObjectsOfType<CarModelControl>();
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		if ( ! Input.GetKey(KeyCode.LeftAlt))//if we are not using the ALT key(camera control)...
		{
			// LEFT MOUSE BUTTON CLICK	
			if(Input.GetMouseButton(0))//is the left mouse button being clicked?
			{
				ray = Camera.main.ScreenPointToRay (Input.mousePosition);//get a ray that goes from the camera -> "THROUGH" the mouse pointer - > and out into the scene
				if(Physics.Raycast(ray, out hit, 500.0f)) //check to see if that ray hits our "floor"												
				{
					movementTargetPosition = hit.point;//mark it where it hit
                    Vector3 deltaTarget = movementTargetPosition - transform.position;
                    lookAtPos = movementTargetPosition + deltaTarget.normalized*2.0f;
                    stop=false;
				}
			}
		}

        //RIGHT MOUSE BUTTON FOR GETTING IN/OUT
		if ( ! Input.GetKey(KeyCode.LeftAlt)) // if we're changing camera transforms, do not use "USE"
		{
			if(Input.GetMouseButton(1))// are we using the right button?
			{
				if(rightButtonDown != true)// was it previously down? if so we are already using "USE" bailout (we don't want to repeat attacks 800 times a second...just once per press please
				{
					if(!insideVehicle)
                    {
    					ray = Camera.main.ScreenPointToRay (Input.mousePosition);// make a ray based on the camera and mouse pointer
                        for(int i=0;i<cars.Length;i++)
                        {
        					if(cars[i].collider.Raycast(ray, out hit, 500.0f)) 												
        					{
                                targetCar=cars[i];
                                entryPoint = 0;
                                //UnityEngine.Debug.Log (targetCar.name);
                                //UnityEngine.Debug.Log (targetCar.entryPoints.Length);
                                for(int entryIndex=0;entryIndex<cars[i].entryPoints.Length;entryIndex++)
                                {
                                    //UnityEngine.Debug.Log (entryIndex);
                                    if(cars[i].entryPoints[entryIndex].entryPoint!=cars[i].entryPoints[entryPoint].entryPoint)
                                    {
                                        if(Vector3.Distance(transform.position,cars[i].entryPoints[entryIndex].entryPoint.position)<(Vector3.Distance(cars[i].entryPoints[entryPoint].entryPoint.position,transform.position)))
                                        {
                                            entryPoint = entryIndex;
                                        }
                                    }
                                }
                                if(Vector3.Distance(transform.position,targetCar.entryPoints[entryPoint].entryPoint.position)>0.5f)
                                {
                                    movementTargetPosition=targetCar.entryPoints[entryPoint].entryPoint.position;
                                    Vector3 deltaTarget = movementTargetPosition - transform.position;
                                    lookAtPos = movementTargetPosition + deltaTarget.normalized*2.0f;
                                    stop=false;
                                }
                                else
                                {
                                    transform.position=targetCar.entryPoints[entryPoint].entryPoint.position;
                                    transform.rotation=targetCar.entryPoints[entryPoint].entryPoint.rotation;
                                    movementTargetPosition=targetCar.entryPoints[entryPoint].entryPoint.position;
                                    lookAtPos = movementTargetPosition + targetCar.entryPoints[entryPoint].entryPoint.forward;
                                    stop=true;
                                }
                                usingCar=true;
                            }
                        }
                    }
                    else//we are in a vehicle time to get out
                    {
                        animator.SetBool("GetIn", false);
                        if(targetCar.entryPoints[entryPoint].hasDoor)
                        {
                            if(targetCar.entryPoints[entryPoint].animator!=null)
                            {
                                targetCar.entryPoints[entryPoint].animator.SetBool("GetIn",false);
                            }
                        }
                    }
                    rightButtonDown = true;
				}
			}
		}
		
		if (Input.GetMouseButtonUp(1))//ok, we can clear the right mouse button and use it for the next attack
		{
			if (rightButtonDown == true)
			{
				rightButtonDown = false;
			}
		}
        
        if(!insideVehicle)
		{
    		Debug.DrawLine ((movementTargetPosition + transform.up*2), movementTargetPosition);//useful for visuals in editor

    		
    		if(Vector3.Distance(movementTargetPosition,transform.position)>1.0f & !stop)
    		{
    			animator.SetBool("Idling", false);
                lookAtPos.y = transform.position.y;
                
                Quaternion tempRot = transform.rotation;    //save current rotation
                transform.LookAt(lookAtPos);                        
                Quaternion hitRot = transform.rotation;     // store the new rotation
                // now we slerp orientation
                transform.rotation = Quaternion.Slerp(tempRot, hitRot, Time.deltaTime * rotateSpeed);
    		}
    		else
    		{
    			animator.SetBool("Idling", true);
                stop=true;
                if(usingCar)
                {
                    transform.position = targetCar.entryPoints[entryPoint].entryPoint.position;
                    physicsController.enabled=false;
                    lookAtPos = transform.position + targetCar.entryPoints[entryPoint].entryPoint.forward;
                    lookAtPos.y = transform.position.y;
                    transform.LookAt(lookAtPos);
                    movementTargetPosition=transform.position + targetCar.entryPoints[entryPoint].entryPoint.forward*0.08f;
                    transform.position=targetCar.entryPoints[entryPoint].entryPoint.position;
                    transform.parent=targetCar.transform;
                    usingCar=false;
                    insideVehicle = true;
                    
                    animator.SetInteger("VehicleType",targetCar.vehicleType);
                    animator.SetBool("GetIn", true);
                    animator.SetFloat("SeatHeight",targetCar.entryPoints[entryPoint].seatHeight);
                    
                    if(targetCar.entryPoints[entryPoint].leftSide)
                    {
                        animator.SetBool("Left", true);
                    }
                    else
                    {
                        animator.SetBool("Left", false);
                    }
                    
                    if(targetCar.entryPoints[entryPoint].hasDoor)
                    {
                        if(targetCar.entryPoints[entryPoint].animator!=null)
                        {
                            targetCar.entryPoints[entryPoint].animator.SetBool("GetIn",true);
                        }
                        else
                        {
                            targetCar.entryPoints[entryPoint].enter=true;
                            targetCar.entryPoints[entryPoint].startTime=Time.time;
                        }
                    }
                    
                    if(targetCar.entryPoints[entryPoint].driver)
                    {
                        targetCar.carMotor.hasDriver=true;
                        animator.SetBool("Driver", true);
                    }
                    else
                    {
                        animator.SetBool("Driver", false);
                    }
                }
                else//not using car but close to movement target
                {
                    stop=true;
                    lookAtPos=transform.position+transform.forward;
                    transform.LookAt(lookAtPos);
                    movementTargetPosition=transform.position;
                }
    		}
            // Controller dinonaktifkan di frame yang sama saat mulai masuk kendaraan (blok usingCar di atas).
            if (physicsController.enabled)
                physicsController.Move(Vector3.down * gravity * Time.deltaTime);
        }
	}
	
	void OnGUI()
	{
		//string tempString = "LMB=move RMB=attack p=pain abc=deaths 12345678 0=change weapons";
		//GUI.Label (new Rect (10, 5,1000, 20), tempString);
	}
}

