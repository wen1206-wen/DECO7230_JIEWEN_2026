using UnityEngine;

public class StickyNote : MonoBehaviour
{
    public Color hoverColor = Color.white;
    public Color heldColor = new Color(1f, 0.85f, 0.2f);

    [Tooltip("Disable any Collider on the note root so only the Body collider " +
             "(which matches the visible note) is hit by the controller ray.")]
    public bool useBodyColliderOnly = true;

    private Renderer noteRenderer;
    private Collider noteCollider;
    private Material runtimeMaterial;
    private Color baseColor;

    private Transform homeParent;
    private Vector3 homePosition;
    private Quaternion homeRotation;

    private bool isHeld = false;

    public CategoryZone CurrentZone { get; set; }

    public bool IsHeld
    {
        get { return isHeld; }
    }


    void Awake()
    {
        // Record the home pose first so ReturnHome() works
        // even if the Body child is missing.
        homeParent = transform.parent;
        homePosition = transform.position;
        homeRotation = transform.rotation;

        Transform body = transform.Find("Body");

        if (body == null)
        {
            Debug.LogError(
                name + " does not contain a child named Body."
            );
            return;
        }

        noteRenderer = body.GetComponent<Renderer>();
        noteCollider = body.GetComponent<Collider>();


        // A larger collider on the root sticks out in front of the note and
        // overlaps neighbouring notes, so the ray selects the wrong note.
        if (useBodyColliderOnly && noteCollider != null)
        {
            Collider[] rootColliders = GetComponents<Collider>();

            for (int i = 0; i < rootColliders.Length; i++)
            {
                if (rootColliders[i].enabled)
                {
                    rootColliders[i].enabled = false;

                    Debug.Log(
                        "[StickyNote] Disabled root collider on " + name +
                        ". Using Body collider for pointing."
                    );
                }
            }
        }

        if (noteRenderer != null)
        {
            runtimeMaterial = noteRenderer.material;
            baseColor = runtimeMaterial.color;
        }
    }


    public void SetHighlighted(bool highlighted)
    {
        if (isHeld || runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.color =
            highlighted ? hoverColor : baseColor;
    }


    public void SetHeld(bool held)
    {
        isHeld = held;

        if (noteCollider != null)
        {
            noteCollider.enabled = !held;
        }

        if (runtimeMaterial != null)
        {
            runtimeMaterial.color =
                held ? heldColor : baseColor;
        }
    }


    public void ReturnHome()
    {
        // Remove from the zone's list as well, so the zone
        // does not keep a stale entry for this note.
        if (CurrentZone != null)
        {
            CurrentZone.RemoveNote(this);
        }

        CurrentZone = null;

        transform.SetParent(homeParent);
        transform.position = homePosition;
        transform.rotation = homeRotation;

        isHeld = false;

        if (noteCollider != null)
        {
            noteCollider.enabled = true;
        }

        if (runtimeMaterial != null)
        {
            runtimeMaterial.color = baseColor;
        }
    }
}
