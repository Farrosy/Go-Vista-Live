using UnityEngine;
using System.Collections;

public class CarMotor : MonoBehaviour 
{
    public Vector3          angularVelocity;
    public Vector3          centerOfMass;
    public float            velocity;
    public float            RBVelocity;
    public float            jumpDistance;
    Vector3                 jumpStart;
    public bool             wheelsUp;
    public Wheel[]          wheels;
    public Rigidbody        body;
    public float            steerAngle;
    
    public float            maxEngineTorque;
    public float            currentTorque;
    
    public GameObject       rampWheel;
    public Transform[]      ramps;
    
    Vector3                 lastPosition;
    float                   lastTime;
    
    public bool             parked = true;
    public bool             hasDriver = false;

    
	// Use this for initialization
	void Start () 
    {
        Physics.gravity=new Vector3(0.0f,-20.81f,0.0f);
        //Get all the transforms in the scene
        Transform[] tms = FindObjectsOfType<Transform>();
        
        //we will count the tranforms that have the word "ramp" in their name
        int rampCount=0;
        for(int i=0;i<tms.Length;i++)
        {
            if (tms[i].name.ToLower().Contains("ramp"))
            {
                rampCount+=1;
            }
        }
        //create a new array of ramps
        ramps=new Transform[rampCount];
        
        //go back through the list and set each ramp in our ramp array
        rampCount=0;
        for(int i=0;i<tms.Length;i++)
        {
            if (tms[i].name.ToLower().Contains("ramp"))
            {
                ramps[rampCount]=tms[i];
                rampCount+=1;
            }
        }
        
	    lastTime=Time.fixedTime;
        lastPosition = transform.position;
        body.centerOfMass=centerOfMass;
        GameObject go = GameObject.Find("Skidmarks");
        Skidmarks skid=go.GetComponent<Skidmarks>();
        for(int i=0;i<wheels.Length;i++)
        {
            wheels[i].lastPosition = wheels[i].collider.transform.position;
            wheels[i].lastSkid = -1;
            if(skid!=null)
            {
                wheels[i].skidmark=skid;
            }
        }
        
        rampWheel.SetActive(true);
        rampWheel.SetActive(false);
    }
	
	// Update is called once per frame
	void FixedUpdate () 
    {
        if(parked)
        {
            for(int i=0;i<wheels.Length;i++)
            {
                wheels[i].collider.brakeTorque=350000.0f;
            }
        }
        if(!hasDriver) return;
        angularVelocity=body.angularVelocity;
        
        steerAngle=0.0f;
        if(Input.GetKey(KeyCode.LeftArrow)) steerAngle=-20.0f;
        if(Input.GetKey(KeyCode.RightArrow)) steerAngle=20.0f;
        
        float timeElapsed=Time.fixedTime-lastTime;
        float moved = Vector3.Distance(transform.position,lastPosition);
        lastTime=Time.fixedTime;
        lastPosition = transform.position;
        
        velocity = ((1.0f/timeElapsed)*moved)* 2.23693629f;
        RBVelocity =((1.0f/timeElapsed)*body.linearVelocity.magnitude)/50.0f * 2.23693629f;
        
        wheelsUp=true;
	    for(int i=0;i<wheels.Length;i++)
        {
            if (wheels[i].drive) wheels[i].collider.motorTorque = currentTorque;
            if (wheels[i].steer) wheels[i].collider.steerAngle = steerAngle;
            
            if(!parked)
            {
                //GetWorldPose cannot "ref" directly to transforms
                Vector3 tempPos= wheels[i].wheelModel.position;
                Quaternion tempRot= wheels[i].wheelModel.rotation;
                wheels[i].collider.GetWorldPose(out  tempPos,out tempRot);
                wheels[i].wheelModel.position=tempPos;
                wheels[i].wheelModel.rotation=tempRot;
            }
            
            if(wheels[i].skidmark!=null)
            {
                if(wheels[i].collider.isGrounded)
                { 
                    wheelsUp=false;
                    wheels[i].collider.GetGroundHit(out wheels[i].hit);
                    Vector3 wheelVelocityDirection = (wheels[i].lastPosition-wheels[i].collider.transform.position).normalized;
                    if(Mathf.Abs(Vector3.Dot(wheels[i].collider.transform.forward,wheelVelocityDirection))<0.98f)
                    {
                        wheels[i].skidding=true;
                        wheels[i].lastSkid = wheels[i].skidmark.AddSkidMark(wheels[i].hit.point, wheels[i].hit.normal, 0.8f,wheels[i].lastSkid);
    
                    }
                    else
                    {
                        wheels[i].skidding=false;
                        wheels[i].lastSkid = -1;
                    }
                    wheels[i].lastPosition=wheels[i].collider.transform.position;
                }
                else
                {
                    wheels[i].skidding=false;
                    wheels[i].lastSkid = -1;
                }
            }
        }
        if(ramps!=null)
        {
            for(int i=0;i<ramps.Length;i++)
            {
                if(ramps[i]!=null)
                {
                    if(Vector3.Distance(ramps[i].position,transform.position)<20.0f)
                    {
                        //body.AddRelativeTorque(-Vector3.right*100000.0f);
                        rampWheel.SetActive(true);
                    }
                    else
                    {
                        rampWheel.SetActive(false);
                    }
                    
                    if(Vector3.Distance(ramps[i].position,transform.position)<2.0f)
                    {
                        body.AddForceAtPosition(transform.up*100.0f,transform.forward*4.0f);
                    }
                }
            }
        }

        if(!wheelsUp) //wheel on the ground
        {
            
            //angularVelocity.z*=0.002f;
            //body.angularVelocity = angularVelocity;
            
            currentTorque=0.0f;
            if(Input.GetKey(KeyCode.UpArrow))
            {
                body.AddForce(transform.forward*200000.0f);
                currentTorque=20000.0f;
                for(int i=0;i<wheels.Length;i++)
                {
                    wheels[i].collider.brakeTorque=0.0f;
                }
                parked=false;
            }
            if(Input.GetKey(KeyCode.DownArrow))
            {
                body.AddForce(-transform.forward*200000.0f);
                currentTorque=-20000.0f;
                for(int i=0;i<wheels.Length;i++)
                {
                    wheels[i].collider.brakeTorque=0.0f;
                }
                parked=false;
            }
            if(Input.GetKey(KeyCode.B))
            {
                for(int i=0;i<wheels.Length;i++)
                {
                    WheelFrictionCurve tempCurve = wheels[i].collider.sidewaysFriction;
                    tempCurve.stiffness=0.5f;
                    if(!wheels[i].steer) 
                    {
                        wheels[i].collider.sidewaysFriction=tempCurve;
                        wheels[i].collider.brakeTorque=35000.0f;
                    }
                    
                }
            }
            else
            {
                for(int i=0;i<wheels.Length;i++)
                {
                    //WheelFrictionCurve tempCurve = wheels[i].collider.sidewaysFriction;
                    //tempCurve.stiffness=2.0f;
                    //wheels[i].collider.sidewaysFriction=tempCurve;
                    //wheels[i].collider.brakeTorque=0.0f;
                }
            }

            if(steerAngle>1.0f)
            {
               body.AddRelativeTorque(0.0f,0.0f,-2000.0f*(velocity+1.0f));//negative leans toward passenger
               body.AddForce(transform.right*2000.0f*(velocity+1.0f));
            }
            if(steerAngle<-1.0f)
            {
               body.AddRelativeTorque(0.0f,0.0f,2000.0f*(velocity+1.0f));//negative leans toward passenger
               body.AddForce(-transform.right*2000.0f*(velocity+1.0f));
            }
            for(int i=0;i<wheels.Length;i++)
            {
                if(!wheels[i].collider.isGrounded) body.AddForceAtPosition(transform.up*2000.0f,wheels[i].collider.transform.position);
            }
            jumpStart=transform.position;
        }
        else
        {
            if(steerAngle>1.0f)   body.AddRelativeTorque(0.0f,0.0f,-20000.0f);//negative leans toward passenger
            if(steerAngle<-1.0f)   body.AddRelativeTorque(0.0f,0.0f,20000.0f);//negative leans toward passenger
            if(Input.GetKey(KeyCode.Space))
            {
                body.AddForceAtPosition(transform.up*20000.0f,transform.right*4.0f);
            }
            jumpDistance=Vector3.Distance(transform.position,jumpStart);
        }
	}
}
[System.Serializable]
public class Wheel
{
    public WheelCollider    collider;
    public Transform        wheelModel;
    public bool             drive;
    public bool             powerBrake;
    public bool             fullBrake;
    public bool             steer;
    
    public Skidmarks        skidmark;
    public WheelHit         hit;
    public Vector3          lastPosition;
    public int              lastSkid;
    public bool             skidding;
}