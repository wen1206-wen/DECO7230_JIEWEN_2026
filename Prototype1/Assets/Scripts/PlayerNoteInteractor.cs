using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class PlayerNoteInteractor : MonoBehaviour
{
    [Header("XR References")]
    public Transform rayOrigin;
    public Transform holdPoint;
    public TMP_Text instructionText;

    [Header("Interaction")]
    public float interactDistance = 12f;

    // Keep E as a backup for Editor testing
    public KeyCode editorInteractKey = KeyCode.E;

    private StickyNote highlightedNote;
    private StickyNote heldNote;

    private InputDevice rightController;
    private bool previousTriggerState = false;

    void Start()
    {
        GetRightController();
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            GetRightController();
        }

        bool triggerPressed = false;

        if (rightController.isValid)
        {
            rightController.TryGetFeatureValue(
                CommonUsages.triggerButton,
                out triggerPressed
            );
        }

        // Detect the moment the trigger is pressed
        bool triggerDown =
            triggerPressed && !previousTriggerState;

        previousTriggerState = triggerPressed;

        // E key remains available as a computer testing backup
        bool interactPressed =
            triggerDown || Input.GetKeyDown(editorInteractKey);

        if (heldNote == null)
        {
            UpdateHover();

            if (interactPressed && highlightedNote != null)
            {
                PickUpNote(highlightedNote);
            }
        }
        else
        {
            if (instructionText != null)
            {
                instructionText.text =
                    "Point at a category board and press Trigger to place";
            }

            if (interactPressed)
            {
                TryDropNote();
            }
        }
    }

    void GetRightController()
    {
        rightController =
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void UpdateHover()
    {
        if (rayOrigin == null)
        {
            return;
        }

        StickyNote newHighlightedNote = null;

        Ray ray = new Ray(
            rayOrigin.position,
            rayOrigin.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance))
        {
            newHighlightedNote =
                hit.collider.GetComponentInParent<StickyNote>();
        }

        if (newHighlightedNote != highlightedNote)
        {
            if (highlightedNote != null)
            {
                highlightedNote.SetHighlighted(false);
            }

            highlightedNote = newHighlightedNote;

            if (highlightedNote != null)
            {
                highlightedNote.SetHighlighted(true);
            }
        }

        if (instructionText != null)
        {
            if (highlightedNote != null)
            {
                instructionText.text =
                    "Press Trigger to pick up this note";
            }
            else
            {
                instructionText.text =
                    "Point the controller at a sticky note";
            }
        }
    }

    void PickUpNote(StickyNote note)
    {
        if (note.CurrentZone != null)
        {
            note.CurrentZone.RemoveNote(note);
        }

        if (highlightedNote != null)
        {
            highlightedNote.SetHighlighted(false);
        }

        highlightedNote = null;
        heldNote = note;

        heldNote.SetHeld(true);

        heldNote.transform.SetParent(holdPoint);
        heldNote.transform.localPosition = Vector3.zero;
        heldNote.transform.localRotation = Quaternion.identity;
    }

    void TryDropNote()
    {
        if (rayOrigin == null)
        {
            return;
        }

        Ray ray = new Ray(
            rayOrigin.position,
            rayOrigin.forward
        );

        CategoryZone targetZone = null;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance))
        {
            targetZone =
                hit.collider.GetComponentInParent<CategoryZone>();
        }

        if (targetZone != null)
        {
            heldNote.SetHeld(false);
            targetZone.PlaceNote(heldNote);
        }
        else
        {
            heldNote.SetHeld(false);
            heldNote.ReturnHome();
        }

        heldNote = null;
    }
}