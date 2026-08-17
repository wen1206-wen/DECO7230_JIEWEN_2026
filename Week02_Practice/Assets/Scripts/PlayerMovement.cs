using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    InputAction moveAction;

    void Start()
    {
        // Find the Move action from the project's input actions asset
        moveAction = InputSystem.actions.FindAction("Move");
    }

    void Update()
    {
        // Read the action as a 2D direction (-1 to 1 on each axis)
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Map it onto the ground plane: X stays X, Y becomes Z
        Vector3 movement = new Vector3(input.x, 0f, input.y);

        // Apply movement using Transform
        transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);
    }
}