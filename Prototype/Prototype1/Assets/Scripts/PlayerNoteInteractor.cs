using UnityEngine;
using UnityEngine.XR;
using TMPro;
using System.Collections.Generic;

public class PlayerNoteInteractor : MonoBehaviour
{
    [Header("XR References")]
    [Tooltip("Create NoteRayOrigin under the RIGHT controller and drag it here.")]
    public Transform rayOrigin;

    [Tooltip("Optional. Only used when 'Use Hold Point' is ticked in Hold Settings.")]
    public Transform holdPoint;

    [Tooltip("World-space instruction text.")]
    public TMP_Text instructionText;

    [Tooltip("Head camera used to position the held note. " +
             "Leave empty to use Camera.main (XR Origin > Camera Offset > Main Camera, tagged MainCamera).")]
    public Transform headCamera;


    [Header("Interaction")]
    [Tooltip("Maximum distance for detecting sticky notes and category zones.")]
    public float interactDistance = 12f;

    [Tooltip("Backup keyboard key for testing in Unity Editor.")]
    public KeyCode editorInteractKey = KeyCode.E;

    [Tooltip("Layers the controller ray can hit.")]
    public LayerMask raycastLayers = Physics.DefaultRaycastLayers;

    [Tooltip("Analogue trigger value needed to count as a press.")]
    [Range(0.1f, 1f)]
    public float triggerPressThreshold = 0.75f;

    [Tooltip("Analogue trigger value below which the press is released. " +
             "Lower than the press threshold so a half-held trigger cannot fire twice.")]
    [Range(0.05f, 1f)]
    public float triggerReleaseThreshold = 0.55f;


    [Header("Visible Debug Ray")]
    public bool showDebugRay = true;

    [Tooltip("Thickness of the ray visible inside Quest.")]
    public float rayWidth = 0.008f;

    [Tooltip("Optional. Material for the laser and end dot. Must use a URP shader " +
             "(e.g. Universal Render Pipeline/Unlit). Leave empty to load Resources/NoteLaserMaterial.")]
    public Material laserMaterial;

    [Tooltip("Hide the XR Interaction Toolkit Near-Far Interactor line visuals, " +
             "so only this script's laser is visible. The XRI interactors keep working.")]
    public bool hideXRIControllerLines = true;

    [Tooltip("Laser and end-dot colour when not holding a note (pointing at notes or empty space).")]
    public Color laserColor = Color.white;

    public Color holdingRayColor = Color.yellow;

    [Tooltip("Ray colour while holding a note and pointing at a valid category zone.")]
    public Color validZoneRayColor = Color.green;


    [Header("Ray End Dot")]
    [Tooltip("Show a small sphere where the ray hits a surface. The note under the dot is the one that will be picked up.")]
    public bool showEndDot = true;

    [Tooltip("Dot diameter in metres at close range.")]
    [Range(0.005f, 0.1f)]
    public float endDotSize = 0.02f;

    [Tooltip("Extra diameter per metre of distance, so the dot stays visible far away.")]
    [Range(0f, 0.02f)]
    public float endDotSizePerMetre = 0.004f;


    [Header("Hold Settings")]
    [Tooltip("Only used if no head camera can be found. The note will stay this far in front of RayOrigin.")]
    public float fallbackHoldDistance = 0.35f;

    [Tooltip("Tick to use the HoldPoint transform instead of the camera-relative reading position.")]
    public bool useHoldPoint = false;


    [Header("Reading Position (camera-relative)")]
    [Tooltip("Distance in metres in front of the head camera while reading.")]
    [Range(0.4f, 2f)]
    public float readingDistance = 0.9f;

    [Tooltip("Vertical offset in metres. Slightly negative keeps the note just below eye level.")]
    [Range(-0.5f, 0.5f)]
    public float readingHeightOffset = -0.08f;

    [Tooltip("Keep the note upright and level, ignoring head pitch and roll. Recommended for comfort.")]
    public bool keepNoteUpright = true;

    [Tooltip("How quickly the note follows the head. Higher = tighter. 0 = locked to head (not recommended).")]
    [Range(0f, 30f)]
    public float followSpeed = 8f;

    [Tooltip("Extra rotation applied to the held note. Use (0, 180, 0) if the text appears backwards.")]
    public Vector3 heldRotationOffset = Vector3.zero;

    [Tooltip("Scale multiplier while held. 1 = original size. Lower this if the note covers too much of the view.")]
    [Range(0.25f, 1.5f)]
    public float heldScaleMultiplier = 1f;


    // Fixed instruction messages
    private const string MsgPointAtNote =
        "Point at a sticky note.\nPress Trigger to pick it up.";

    private const string MsgNoController =
        "Right controller not detected.\n" +
        "Move the controller or press Trigger.";

    private const string MsgPointAtZone =
        "Point at a category zone to place this note.";

    private const string MsgPressToPlace =
        "Press Trigger to place note.";


    // Kinds of instruction text that include an object name.
    // Cached so strings are only built when the target changes.
    private enum MessageKind
    {
        None,
        NoteDetected,
        HoldingNoZone,
        HoldingOnZone
    }


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
    private Material rayMaterial;
    private Color currentRayColour;
    private bool hasRayColour = false;

    // Sphere drawn at the end of the ray
    private Transform endDot;
    private Renderer endDotRenderer;
    private Material endDotMaterial;

    // Information needed when releasing the note
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalLocalScale;
    private CategoryZone previousZone;

    private Collider[] heldColliders;
    private bool[] heldColliderStates;

    // Prevents repeatedly searching for controller every frame
    private float nextControllerSearchTime = 0f;

    // Head camera found via Camera.main
    private Transform cachedHeadCamera;

    // Last valid horizontal forward, used when looking straight up/down
    private Vector3 lastReadingForward = Vector3.forward;

    // Reused buffers (no allocations in Update)
    private static readonly List<InputDevice> deviceBuffer =
        new List<InputDevice>();

    private readonly RaycastHit[] hitBuffer =
        new RaycastHit[16];

    // Instruction text cache
    private Object messageSource;
    private MessageKind messageKind = MessageKind.None;
    private string cachedMessage;

    private bool isQuitting = false;


    void Start()
    {
        SetupDebugRay();
        FindRightController();

        if (hideXRIControllerLines)
        {
            HideXRIControllerLines();
        }

        if (instructionText != null)
        {
            instructionText.text = MsgPointAtNote;
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

            UpdateHeldNote();
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

        bool interactPressed = ReadInteractPressed();


        // ---------------------------------------------------------
        // 4. If holding a note, point at a CategoryZone to place it
        // ---------------------------------------------------------

        if (heldNote != null)
        {
            UpdateHeldNote();

            Vector3 holdRayEnd;
            bool holdRayHit;
            CategoryZone targetZone =
                FindTargetZone(out holdRayEnd, out holdRayHit);

            DrawRayWhileHolding(targetZone, holdRayEnd, holdRayHit);

            if (targetZone != null)
            {
                SetInstructionFor(targetZone, MessageKind.HoldingOnZone);

                if (interactPressed)
                {
                    PlaceHeldNote(targetZone);
                }
            }
            else
            {
                // Not pointing at a category: keep holding, never drop in the air.
                SetInstructionFor(heldNote, MessageKind.HoldingNoZone);
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
            raycastLayers,
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

        if (detectedNote != highlightedNote)
        {
            if (highlightedNote != null)
            {
                highlightedNote.SetHighlighted(false);
            }

            highlightedNote = detectedNote;

            if (highlightedNote != null)
            {
                highlightedNote.SetHighlighted(true);
            }
        }


        // ---------------------------------------------------------
        // 7. Update visible laser
        // ---------------------------------------------------------

        // White laser; the end dot sits exactly where the ray hits,
        // so the note under the dot is the one that will be picked up.
        DrawRay(
            rayStart,
            rayEnd,
            laserColor,
            didHit
        );

        if (detectedNote != null)
        {
            SetInstructionFor(detectedNote, MessageKind.NoteDetected);
        }
        else
        {
            if (!rightController.isValid)
            {
                SetInstruction(MsgNoController);
            }
            else
            {
                SetInstruction(MsgPointAtNote);
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
    // READ TRIGGER (controller + editor keyboard backup)
    // =============================================================

    bool ReadInteractPressed()
    {
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

            // Hysteresis: once pressed, the trigger must drop below the
            // release threshold before another press can register.
            float threshold =
                previousTriggerPressed ?
                triggerReleaseThreshold :
                triggerPressThreshold;

            triggerPressed =
                triggerButton ||
                triggerValue > threshold;
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

        return triggerDown || editorPressed;
    }


    // =============================================================
    // FIND QUEST RIGHT CONTROLLER
    // =============================================================

    void FindRightController()
    {
        deviceBuffer.Clear();

        InputDevices.GetDevicesAtXRNode(
            XRNode.RightHand,
            deviceBuffer
        );

        if (deviceBuffer.Count > 0)
        {
            rightController = deviceBuffer[0];

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

        if (highlightedNote != null)
        {
            highlightedNote.SetHighlighted(false);
        }

        highlightedNote = null;

        heldNote = note;

        // Remember everything needed to restore the note later.
        originalParent = note.transform.parent;
        originalPosition = note.transform.position;
        originalRotation = note.transform.rotation;
        originalLocalScale = note.transform.localScale;
        previousZone = note.CurrentZone;


        // If the note was already sorted, take it out of that zone now.
        // This closes the gap in the zone layout and stops the zone
        // from repositioning the note while it is being held.
        if (previousZone != null)
        {
            previousZone.RemoveNote(note);
        }


        // Record collider states BEFORE SetHeld changes the Body collider.
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
        }

        note.SetHeld(true);

        // Disable note colliders while holding.
        // Otherwise our own ray may hit the held object.
        for (int i = 0;
             i < heldColliders.Length;
             i++)
        {
            heldColliders[i].enabled = false;
        }


        // The note is NOT re-parented while held, so its world scale
        // never changes unless heldScaleMultiplier is changed.
        if (!Mathf.Approximately(heldScaleMultiplier, 1f))
        {
            note.transform.localScale =
                originalLocalScale * heldScaleMultiplier;
        }


        Transform head = GetHeadTransform();

        if (head != null)
        {
            Vector3 flatForward = head.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                lastReadingForward = flatForward.normalized;
            }
        }


        // With followSpeed > 0 the note glides from its current position
        // to the reading position, which shows where it came from.
        if (followSpeed <= 0f)
        {
            UpdateHeldNote();
        }


        Debug.Log(
            "[PlayerNoteInteractor] Picked up: " +
            note.name
        );
    }


    // =============================================================
    // KEEP NOTE AT READING POSITION
    // =============================================================

    void UpdateHeldNote()
    {
        if (heldNote == null)
            return;


        Vector3 targetPosition;
        Quaternion targetRotation;

        if (!GetHeldTargetPose(
                out targetPosition,
                out targetRotation))
        {
            return;
        }


        Transform noteTransform = heldNote.transform;

        if (followSpeed <= 0f)
        {
            noteTransform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );
        }
        else
        {
            // Frame-rate independent smoothing.
            float t =
                1f - Mathf.Exp(-followSpeed * Time.deltaTime);

            noteTransform.SetPositionAndRotation(
                Vector3.Lerp(noteTransform.position, targetPosition, t),
                Quaternion.Slerp(noteTransform.rotation, targetRotation, t)
            );
        }
    }


    // =============================================================
    // CALCULATE WHERE THE HELD NOTE SHOULD BE
    // =============================================================

    bool GetHeldTargetPose(
        out Vector3 position,
        out Quaternion rotation
    )
    {
        Quaternion offset =
            Quaternion.Euler(heldRotationOffset);


        // Optional: use a manually placed HoldPoint.
        if (useHoldPoint && holdPoint != null)
        {
            position = holdPoint.position;
            rotation = holdPoint.rotation * offset;
            return true;
        }


        Transform head = GetHeadTransform();


        // Backup if no camera can be found.
        if (head == null)
        {
            if (rayOrigin == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            position =
                rayOrigin.position +
                rayOrigin.forward *
                fallbackHoldDistance;

            rotation = rayOrigin.rotation * offset;
            return true;
        }


        Vector3 forward;
        Vector3 up;

        if (keepNoteUpright)
        {
            forward = head.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                lastReadingForward = forward;
            }
            else
            {
                // Looking straight up or down: keep the last direction.
                forward = lastReadingForward;
            }

            up = Vector3.up;
        }
        else
        {
            forward = head.forward;
            up = head.up;
        }


        position =
            head.position +
            forward * readingDistance +
            up * readingHeightOffset;


        // Note +Z points away from the eyes, matching how notes sit
        // on a CategoryZone (localRotation = identity), so the front
        // face is turned toward the user.
        rotation =
            Quaternion.LookRotation(
                position - head.position,
                up
            ) * offset;

        return true;
    }


    Transform GetHeadTransform()
    {
        if (headCamera != null)
            return headCamera;

        if (cachedHeadCamera == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                cachedHeadCamera = mainCamera.transform;
            }
        }

        return cachedHeadCamera;
    }


    // =============================================================
    // FIND CATEGORY ZONE WHILE HOLDING
    // =============================================================

    CategoryZone FindTargetZone(
        out Vector3 rayEnd,
        out bool hitSurface
    )
    {
        Vector3 start = rayOrigin.position;
        Vector3 direction = rayOrigin.forward;

        rayEnd = start + direction * interactDistance;
        hitSurface = false;


        // Collide with triggers too, in case the zone collider is a trigger.
        int hitCount = Physics.RaycastNonAlloc(
            start,
            direction,
            hitBuffer,
            interactDistance,
            raycastLayers,
            QueryTriggerInteraction.Collide
        );


        CategoryZone nearestZone = null;
        float nearestZoneDistance = float.MaxValue;
        Vector3 nearestZonePoint = rayEnd;

        float nearestBlockDistance = float.MaxValue;
        Vector3 nearestBlockPoint = rayEnd;


        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;

            if (hitCollider == null)
                continue;

            // Ignore the held note itself (its colliders should already be off).
            if (heldNote != null &&
                hitCollider.transform.IsChildOf(heldNote.transform))
                continue;


            // Zone collider may be on a child object, and notes already
            // placed in a zone are children of it, so search upward.
            CategoryZone zone =
                hitCollider.GetComponentInParent<CategoryZone>();

            float distance = hitBuffer[i].distance;

            if (zone != null)
            {
                if (distance < nearestZoneDistance)
                {
                    nearestZone = zone;
                    nearestZoneDistance = distance;
                    nearestZonePoint = hitBuffer[i].point;
                }
            }
            else if (!hitCollider.isTrigger)
            {
                // Solid non-zone object (wall, unsorted note, table...).
                if (distance < nearestBlockDistance)
                {
                    nearestBlockDistance = distance;
                    nearestBlockPoint = hitBuffer[i].point;
                }
            }
        }


        // A zone is only valid if nothing solid is in front of it.
        if (nearestZone != null &&
            nearestZoneDistance <= nearestBlockDistance)
        {
            rayEnd = nearestZonePoint;
            hitSurface = true;
            return nearestZone;
        }


        if (nearestBlockDistance < float.MaxValue)
        {
            rayEnd = nearestBlockPoint;
            hitSurface = true;
        }

        return null;
    }


    // =============================================================
    // PLACE INTO CATEGORY ZONE
    // =============================================================

    void PlaceHeldNote(CategoryZone zone)
    {
        if (heldNote == null || zone == null)
            return;


        StickyNote noteToPlace = heldNote;

        ReleaseHeldState(noteToPlace);

        // CategoryZone handles: removing from the old zone, avoiding
        // duplicates, parenting, and the automatic arrangement.
        zone.PlaceNote(noteToPlace);


        Debug.Log(
            "[PlayerNoteInteractor] Placed " +
            noteToPlace.name +
            " into " +
            zone.name
        );


        SetInstruction(MsgPointAtNote);
    }


    // =============================================================
    // DROP (only used when the interactor is disabled while holding)
    // Restores the note to where it was picked up, never mid-air.
    // =============================================================

    void DropHeldNote()
    {
        if (heldNote == null)
            return;


        StickyNote noteToDrop = heldNote;
        CategoryZone zoneToRestore = previousZone;
        Transform parentToRestore = originalParent;
        Vector3 positionToRestore = originalPosition;
        Quaternion rotationToRestore = originalRotation;

        ReleaseHeldState(noteToDrop);


        if (zoneToRestore != null)
        {
            // Put it back into the category it came from.
            zoneToRestore.PlaceNote(noteToDrop);
        }
        else
        {
            noteToDrop.transform.SetParent(
                parentToRestore != null ? parentToRestore : null,
                true
            );

            noteToDrop.transform.SetPositionAndRotation(
                positionToRestore,
                rotationToRestore
            );
        }


        Debug.Log(
            "[PlayerNoteInteractor] Returned: " +
            noteToDrop.name
        );


        SetInstruction(MsgPointAtNote);
    }


    // =============================================================
    // RESTORE NOTE STATE AFTER HOLDING
    // =============================================================

    void ReleaseHeldState(StickyNote note)
    {
        if (note != null)
        {
            note.transform.localScale = originalLocalScale;

            // Turns the Body collider back on and resets colour.
            note.SetHeld(false);
        }


        // Restore the exact collider states recorded at pickup.
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
        previousZone = null;
        originalParent = null;

        heldColliders = null;
        heldColliderStates = null;
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


        // Use a URP Unlit material. Legacy shaders (Sprites/Default,
        // Default-Line) can render differently in each eye on Quest
        // or show as magenta in URP.
        Material baseMaterial = laserMaterial;

        if (baseMaterial == null)
        {
            baseMaterial =
                Resources.Load<Material>("NoteLaserMaterial");
        }

        if (baseMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                baseMaterial = new Material(shader);
            }

            Debug.LogWarning(
                "[PlayerNoteInteractor] No laser material found. " +
                "Assign Laser Material or keep Assets/Resources/NoteLaserMaterial.mat."
            );
        }

        if (baseMaterial != null)
        {
            // Instance so colour changes never edit the asset.
            lineRenderer.material =
                new Material(baseMaterial);
        }

        rayMaterial = lineRenderer.sharedMaterial;


        lineRenderer.enabled =
            showDebugRay;


        SetupEndDot();
    }


    // =============================================================
    // HIDE XRI CONTROLLER LINES
    // The XR Origin (XR Rig) Starter Assets prefab draws its own
    // curved line from each controller (Near-Far Interactor > LineVisual).
    // It does not follow this script's raycast, so it is turned off.
    // =============================================================

    void HideXRIControllerLines()
    {
        Transform searchRoot =
            rayOrigin != null ? rayOrigin.root : transform.root;

        Transform[] all =
            searchRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];

            if (t.name != "LineVisual" ||
                t.parent == null)
            {
                continue;
            }

            // Matches "Near-Far Interactor" and "Right_NearFarInteractor".
            string parentName =
                t.parent.name.Replace("-", "").Replace(" ", "");

            if (!parentName.Contains("NearFarInteractor"))
            {
                continue;
            }

            if (t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);

                Debug.Log(
                    "[PlayerNoteInteractor] Hid XRI line: " +
                    t.parent.name + "/" + t.name
                );
            }
        }
    }


    // =============================================================
    // CREATE RAY END DOT
    // =============================================================

    void SetupEndDot()
    {
        GameObject dot =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        dot.name = "NoteRayEndDot";


        // The dot must never block the ray it is drawn on.
        Collider dotCollider = dot.GetComponent<Collider>();

        if (dotCollider != null)
        {
            dotCollider.enabled = false;
            Destroy(dotCollider);
        }


        endDotRenderer = dot.GetComponent<Renderer>();

        endDotRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        endDotRenderer.receiveShadows = false;


        // Same unlit shader as the laser, so it shows in the Quest build.
        if (rayMaterial != null)
        {
            endDotMaterial = new Material(rayMaterial);
            endDotRenderer.sharedMaterial = endDotMaterial;
        }


        endDot = dot.transform;
        dot.SetActive(false);
    }


    // =============================================================
    // NORMAL RAY
    // =============================================================

    void DrawRay(
        Vector3 start,
        Vector3 end,
        Color colour,
        bool hitSurface
    )
    {
        if (!showDebugRay ||
            lineRenderer == null)
        {
            SetEndDotVisible(false);
            return;
        }


        lineRenderer.enabled = true;

        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;

        if (!hasRayColour || currentRayColour != colour)
        {
            currentRayColour = colour;
            hasRayColour = true;

            lineRenderer.startColor = colour;
            lineRenderer.endColor = colour;

            if (rayMaterial != null)
            {
                rayMaterial.color =
                    colour;
            }

            if (endDotMaterial != null)
            {
                endDotMaterial.color =
                    colour;
            }
        }

        lineRenderer.SetPosition(
            0,
            start
        );

        lineRenderer.SetPosition(
            1,
            end
        );

        UpdateEndDot(start, end, hitSurface);
    }


    // =============================================================
    // RAY END DOT
    // Only shown when the ray actually hits a surface.
    // =============================================================

    void UpdateEndDot(
        Vector3 start,
        Vector3 end,
        bool hitSurface
    )
    {
        if (!showEndDot ||
            !hitSurface ||
            endDot == null)
        {
            SetEndDotVisible(false);
            return;
        }


        Vector3 toEnd = end - start;
        float distance = toEnd.magnitude;

        float size =
            endDotSize +
            distance * endDotSizePerMetre;


        // Pull the dot back by its radius so it sits on the
        // surface instead of sinking into the note.
        Vector3 dotPosition = end;

        if (distance > 0.0001f)
        {
            dotPosition -=
                (toEnd / distance) * (size * 0.5f);
        }


        endDot.position = dotPosition;
        endDot.localScale = new Vector3(size, size, size);

        SetEndDotVisible(true);
    }


    void SetEndDotVisible(bool visible)
    {
        if (endDot != null &&
            endDot.gameObject.activeSelf != visible)
        {
            endDot.gameObject.SetActive(visible);
        }
    }


    // =============================================================
    // RAY WHILE HOLDING
    // Green on a valid CategoryZone, yellow otherwise.
    // =============================================================

    void DrawRayWhileHolding(
        CategoryZone targetZone,
        Vector3 end,
        bool hitSurface
    )
    {
        if (rayOrigin == null)
            return;


        DrawRay(
            rayOrigin.position,
            end,
            targetZone != null ?
                validZoneRayColor :
                holdingRayColor,
            hitSurface
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

        SetEndDotVisible(false);
    }


    // =============================================================
    // INSTRUCTION TEXT
    // =============================================================

    void SetInstruction(string message)
    {
        if (instructionText != null &&
            instructionText.text != message)
        {
            instructionText.text = message;
        }
    }


    // Builds a message containing an object name only when the
    // object or message type changes, to avoid per-frame strings.
    void SetInstructionFor(
        Object source,
        MessageKind kind
    )
    {
        if (cachedMessage == null ||
            kind != messageKind ||
            source != messageSource)
        {
            messageSource = source;
            messageKind = kind;

            string sourceName =
                source != null ? source.name : "";

            switch (kind)
            {
                case MessageKind.NoteDetected:
                    cachedMessage =
                        "Note detected: " + sourceName +
                        "\nPress Trigger to pick it up.";
                    break;

                case MessageKind.HoldingNoZone:
                    cachedMessage =
                        "Holding: " + sourceName +
                        "\n" + MsgPointAtZone;
                    break;

                case MessageKind.HoldingOnZone:
                    cachedMessage =
                        MsgPressToPlace +
                        "\nCategory: " + sourceName;
                    break;

                default:
                    cachedMessage = MsgPointAtNote;
                    break;
            }
        }

        SetInstruction(cachedMessage);
    }


    // =============================================================
    // CLEAN UP
    // =============================================================

    void OnApplicationQuit()
    {
        isQuitting = true;
    }


    void OnDisable()
    {
        // If object gets disabled while a note is held,
        // put the note back where it came from so it doesn't
        // remain floating or broken.
        if (heldNote != null)
        {
            if (!isQuitting && gameObject.scene.isLoaded)
            {
                DropHeldNote();
            }
            else
            {
                ReleaseHeldState(heldNote);
            }
        }

        if (highlightedNote != null)
        {
            highlightedNote.SetHighlighted(false);
            highlightedNote = null;
        }

        SetEndDotVisible(false);
    }


    void OnDestroy()
    {
        if (endDot != null)
        {
            Destroy(endDot.gameObject);
        }

        if (endDotMaterial != null)
        {
            Destroy(endDotMaterial);
        }
    }
}
