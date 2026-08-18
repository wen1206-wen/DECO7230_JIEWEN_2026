using UnityEngine;

public class DebugRotator : MonoBehaviour
{
    public float rotationSpeed = 45f;
    private float currentRotation = 0f;

    void Update()
    {
        // Calculate the rotation value and store it in a variable
        float rotationThisFrame = rotationSpeed * Time.deltaTime;
        currentRotation += rotationThisFrame;
        
        // Apply the rotation using currentRotation as absolute angle
        transform.rotation = Quaternion.Euler(0, currentRotation, 0);
    
        // This is different from last week where we were rotating it by an amount with each call.
        // transform.Rotate(Vector3.up * rotationThisFrame);
        
        // Debug: Print the current rotation value to console
        Debug.Log("Current Rotation: " + currentRotation);
    }
}