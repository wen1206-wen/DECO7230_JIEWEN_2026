using UnityEngine;
using UnityEngine.XR;
using TMPro;
using System.Collections.Generic;

public class PlayerNoteInteractor : MonoBehaviour
{
    [Header("XR References")]
    [Tooltip("Create NoteRayOrigin under the RIGHT controller and drag it here.")]
    public Transform rayOrigin;

    [Tooltip("Point where the note will stay while being held.")]
    public Transform holdPoint;

    [Tooltip("World-space instruction text.")]
    public TMP_Text instructionText;


    [Header("Interaction")]
    [Tooltip("Maximum distance for detecting sticky notes.")]
    public float interactDistance = 12f;

    [Tooltip("Backup keyboard key for testing in Unity Editor.")]
    public KeyCode editorInteractKey = KeyCode.E;


    [Header("Visible Debug Ray")]
    public bool showDebugRay = true;

    [Tooltip("Thickness of the ray visible inside Quest.")]
    public float rayWidth = 0.008f;

    public Color normalRayColor = Color.red;
    public Color hitRayColor = Color.green;
    public Color holdingRayColor = Color.yellow;


    [Header("Hold Settings")]
    [Tooltip("If HoldPoint is not assigned, the note will stay this far in front of RayOrigin.")]
    public float fallbackHoldDistance = 0.35f;


    // Current note that the ray is pointing at
    private StickyNote highlightedNote;

    // Current note being held
    private StickyNote heldNote;

    // Quest right controller
    private InputDevice rightController;

    // Used to detect only the first frame of trigger press
    private bool previousTriggerPressed = false;

    // Visible laser
    private LineRenderer lineRenderer;

    // Information needed when dropping the note
    private Transform originalParent;

    private Collider[] heldColliders;
    private bool[] heldColliderStates;

    // Prevents repeatedly searching for controller every frame
    private float nextControllerSearchTime = 0f;


    void Start()
    {
        SetupDebugRay();
        FindRightController();

        if (instructionText != null)
        {
            instructionText.text =
                "Point at a sticky note.\nPress Trigger to pick it up.";
        }
    }


    void Update()
    {
        // ---------------------------------------------------------
        // 1. Make sure references exist
        // ---------------------------------------------------------

        if (rayOrigin == null)
        {
            SetInstruction(
                "Ray Origin is missing.\nAssign NoteRayOrigin in Inspector."
            );

            HideRay();
            return;
        }


        // ---------------------------------------------------------
        // 2. Reconnect Quest controller if necessary
        // ---------------------------------------------------------

        if (!rightController.isValid &&
            Time.time >= nextControllerSearchTime)
        {
            FindRightController();
            nextControllerSearchTime = Time.time + 1f;
        }


        // ---------------------------------------------------------
        // 3. Read trigger
        // ---------------------------------------------------------

        bool triggerPressed = false;

        if (rightController.isValid)
        {
            bool triggerButton = false;
            float triggerValue = 0f;

            // Try digital trigger
            rightController.TryGetFeatureValue(
                CommonUsages.triggerButton,
                out triggerButton
            );

            // Also try analogue trigger.
            // This makes the script more reliable on Quest/OpenXR.
            rightController.TryGetFeatureValue(
                CommonUsages.trigger,
                out triggerValue
            );

            triggerPressed =
                triggerButton ||
                triggerValue > 0.75f;
        }


        // Keyboard backup when testing in Unity Editor
        bool editorPressed = false;

        #if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            editorPressed =
                UnityEngine.InputSystem.Keyboard.current
                .eKey.wasPressedThisFrame;
        }
        #endif



        // Only trigger once when the button first goes down
        bool triggerDown =
            triggerPressed &&
            !previousTriggerPressed;

        previousTriggerPressed = triggerPressed;


        bool interactPressed =
            triggerDown || editorPressed;


        // ---------------------------------------------------------
        // 4. If holding a note, allow Trigger to drop it
        // ---------------------------------------------------------

        if (heldNote != null)
        {
            UpdateHeldNote();

            DrawRayWhileHolding();

            SetInstruction(
                "Holding: " + heldNote.name +
                "\nPress Trigger to drop."
            );

            if (interactPressed)
            {
                DropHeldNote();
            }

            return;
        }


        // ---------------------------------------------------------
        // 5. Raycast from controller
        // ---------------------------------------------------------

        RaycastHit hit;

        Vector3 rayStart = rayOrigin.position;
        Vector3 rayDirection = rayOrigin.forward;

        bool didHit = Physics.Raycast(
            rayStart,
            rayDirection,
            out hit,
            interactDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );


        StickyNote detectedNote = null;

        Vector3 rayEnd =
            rayStart +
            rayDirection *
            interactDistance;


        if (didHit)
        {
            rayEnd = hit.point;

            // Collider may be on Body or child object,
            // so search upward for StickyNote.
            detectedNote =
                hit.collider.GetComponentInParent<StickyNote>();
        }


        // ---------------------------------------------------------
        // 6. Update current highlighted note
        // ---------------------------------------------------------

        highlightedNote = detectedNote;


        // ---------------------------------------------------------
        // 7. Update visible laser
        // ---------------------------------------------------------

        if (detectedNote != null)
        {
            DrawRay(
                rayStart,
                rayEnd,
                hitRayColor
            );

            SetInstruction(
                "Note detected: " +
                detectedNote.name +
                "\nPress Trigger to pick it up."
            );
        }
        else
        {
            DrawRay(
                rayStart,
                rayEnd,
                normalRayColor
            );

            if (!rightController.isValid)
            {
                SetInstruction(
                    "Right controller not detected.\n" +
                    "Move the controller or press Trigger."
                );
            }
            else
            {
                SetInstruction(
                    "Point at a sticky note.\n" +
                    "Press Trigger to pick it up."
                );
            }
        }


        // ---------------------------------------------------------
        // 8. Pick up note
        // ---------------------------------------------------------

        if (interactPressed &&
            highlightedNote != null)
        {
            PickUpNote(highlightedNote);
        }
    }


    // =============================================================
    // FIND QUEST RIGHT CONTROLLER
    // =============================================================

    void FindRightController()
    {
        List<InputDevice> devices =
            new List<InputDevice>();

        InputDevices.GetDevicesAtXRNode(
            XRNode.RightHand,
            devices
        );

        if (devices.Count > 0)
        {
            rightController = devices[0];

            Debug.Log(
                "[PlayerNoteInteractor] Right controller connected: " +
                rightController.name
            );
        }
        else
        {
            rightController = new InputDevice();

            Debug.LogWarning(
                "[PlayerNoteInteractor] Right controller not found."
            );
        }
    }


    // =============================================================
    // PICK UP
    // =============================================================

    void PickUpNote(StickyNote note)
    {
        if (note == null)
            return;

        heldNote = note;

        originalParent = note.transform.parent;


        // Disable note colliders while holding.
        // Otherwise our own ray may hit the held object.
        heldColliders =
            note.GetComponentsInChildren<Collider>(true);

        heldColliderStates =
            new bool[heldColliders.Length];

        for (int i = 0;
             i < heldColliders.Length;
             i++)
        {
            heldColliderStates[i] =
                heldColliders[i].enabled;

            heldColliders[i].enabled = false;
        }


        if (holdPoint != null)
        {
            note.transform.SetParent(
                holdPoint,
                true
            );

            note.transform.localPosition =
                Vector3.zero;

            note.transform.localRotation =
                Quaternion.identity;
        }
        else
        {
            // Backup in case HoldPoint isn't assigned.
            note.transform.SetParent(
                rayOrigin,
                true
            );

            note.transform.localPosition =
                new Vector3(
                    0f,
                    0f,
                    fallbackHoldDistance
                );

            note.transform.localRotation =
                Quaternion.identity;
        }


        highlightedNote = null;

        Debug.Log(
            "[PlayerNoteInteractor] Picked up: " +
            note.name
        );
    }


    // =============================================================
    // KEEP NOTE AT HOLD POINT
    // =============================================================

    void UpdateHeldNote()
    {
        if (heldNote == null)
            return;


        if (holdPoint != null)
        {
            heldNote.transform.position =
                holdPoint.position;

            heldNote.transform.rotation =
                holdPoint.rotation;
        }
    }


    // =============================================================
    // DROP
    // =============================================================

    void DropHeldNote()
    {
        if (heldNote == null)
            return;


        StickyNote noteToDrop = heldNote;


        // Restore original parent while preserving world position
        noteToDrop.transform.SetParent(
            originalParent,
            true
        );


        // Restore collider states
        if (heldColliders != null &&
            heldColliderStates != null)
        {
            for (int i = 0;
                 i < heldColliders.Length &&
                 i < heldColliderStates.Length;
                 i++)
            {
                if (heldColliders[i] != null)
                {
                    heldColliders[i].enabled =
                        heldColliderStates[i];
                }
            }
        }


        heldNote = null;
        highlightedNote = null;

        heldColliders = null;
        heldColliderStates = null;


        Debug.Log(
            "[PlayerNoteInteractor] Dropped: " +
            noteToDrop.name
        );


        SetInstruction(
            "Point at a sticky note.\n" +
            "Press Trigger to pick it up."
        );
    }


    // =============================================================
    // CREATE VISIBLE QUEST LASER
    // =============================================================

    void SetupDebugRay()
    {
        lineRenderer =
            GetComponent<LineRenderer>();


        if (lineRenderer == null)
        {
            lineRenderer =
                gameObject.AddComponent<LineRenderer>();
        }


        lineRenderer.positionCount = 2;

        lineRenderer.useWorldSpace = true;

        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;

        lineRenderer.numCapVertices = 4;

        lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        lineRenderer.receiveShadows = false;


        // Create simple runtime material
        if (lineRenderer.material == null ||
            lineRenderer.sharedMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit"
                    );
            }

            if (shader != null)
            {
                lineRenderer.material =
                    new Material(shader);
            }
        }


        lineRenderer.enabled =
            showDebugRay;
    }


    // =============================================================
    // NORMAL RAY
    // =============================================================

    void DrawRay(
        Vector3 start,
        Vector3 end,
        Color colour
    )
    {
        if (!showDebugRay ||
            lineRenderer == null)
        {
            return;
        }


        lineRenderer.enabled = true;

        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;

        lineRenderer.startColor = colour;
        lineRenderer.endColor = colour;

        if (lineRenderer.material != null)
        {
            lineRenderer.material.color =
                colour;
        }

        lineRenderer.SetPosition(
            0,
            start
        );

        lineRenderer.SetPosition(
            1,
            end
        );
    }


    // =============================================================
    // RAY WHILE HOLDING
    // =============================================================

    void DrawRayWhileHolding()
    {
        if (rayOrigin == null)
            return;


        Vector3 start =
            rayOrigin.position;

        Vector3 end;

        if (holdPoint != null)
        {
            end = holdPoint.position;
        }
        else
        {
            end =
                start +
                rayOrigin.forward *
                fallbackHoldDistance;
        }


        DrawRay(
            start,
            end,
            holdingRayColor
        );
    }


    // =============================================================
    // HIDE RAY
    // =============================================================

    void HideRay()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }


    // =============================================================
    // INSTRUCTION TEXT
    // =============================================================

    void SetInstruction(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }


    // =============================================================
    // CLEAN UP
    // =============================================================

    void OnDisable()
    {
        // If object gets disabled while a note is held,
        // restore the note so it doesn't remain broken.
        if (heldNote != null)
        {
            DropHeldNote();
        }
    }
}