using UnityEngine;
using System.Collections;

public class CarModelControl : MonoBehaviour 
{
    public int          vehicleType;
    public EntryPoint[] entryPoints;
    public CarMotor     carMotor;
    
    public MeshCollider collider;
    
	// Use this for initialization
	void Start () 
    {
        collider=GetComponent<MeshCollider>();
        carMotor = GetComponent<CarMotor>();
	}
    
    void LateUpdate()
    {
        for(int i =0;i<entryPoints.Length;i++)
        {
            if(entryPoints[i].animator==null & entryPoints[i].hasDoor)
            {
                if(entryPoints[i].exit | entryPoints[i].enter)
                {
                    if((entryPoints[i].startTime + entryPoints[i].curveDuration)<Time.time)
                    {
                        entryPoints[i].enter=false;
                        entryPoints[i].exit=false;
                    }
                    else
                    {
                        AnimationCurve curve = new AnimationCurve();
                        if (entryPoints[i].enter) curve = entryPoints[i].enterCurve;
                        if (entryPoints[i].exit) curve = entryPoints[i].exitCurve;
                        entryPoints[i].doorModel.localRotation=Quaternion.AngleAxis(curve.Evaluate(Time.time-entryPoints[i].startTime),entryPoints[i].hingeAxis);
                    }
                }
            }
        }   
    }
}

[System.Serializable]
public class EntryPoint
{
    public Transform    entryPoint;
    public float        seatHeight;
    public bool         driver;
    public bool         leftSide;
    public bool         hasDoor;
    public Animator     animator;
    public Transform    doorModel;
    public Vector3      hingeAxis=Vector3.up;
    public AnimationCurve   enterCurve;
    public AnimationCurve   exitCurve;
    public bool         enter;
    public bool         exit;
    public float        startTime;
    public float        curveDuration;
}