using UnityEngine;
using System.Collections;

public class Paraglider : MonoBehaviour 
{
    public Animator     paraglider;
    public CarMotor     carMotor;
	// Use this for initialization
	void Start () 
    {
        carMotor=GetComponent<CarMotor>();
	}
	
	// Update is called once per frame
	void Update () 
    {
        if(carMotor.velocity>5.0f) paraglider.SetBool("Rise",true); else paraglider.SetBool("Rise",false);
	}
}
