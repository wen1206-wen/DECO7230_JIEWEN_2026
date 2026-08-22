using UnityEngine;

public class UsefulMath : MonoBehaviour
{
    public Transform debugCube;
    public Transform fallingCube;
    
    public float distanceToFallingCube;

    void Update()
    {
        // Calculate distance between debug cube and falling cube
        distanceToFallingCube = Vector3.Distance(debugCube.position, fallingCube.position);
    }
}