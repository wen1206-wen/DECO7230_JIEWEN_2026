using System.Collections.Generic;
using UnityEngine;

public class CategoryZone : MonoBehaviour
{
    public int columns = 2;
    public float spacingX = 1.05f;
    public float spacingY = 0.72f;
    public float startY = 0.65f;
    public float surfaceOffset = -0.16f;

    private readonly List<StickyNote> placedNotes = new List<StickyNote>();


    public void PlaceNote(StickyNote note)
    {
        if (note == null)
        {
            return;
        }

        // Moving between categories: remove from the old zone first.
        if (note.CurrentZone != null && note.CurrentZone != this)
        {
            note.CurrentZone.RemoveNote(note);
        }

        // Never add the same note twice.
        if (!placedNotes.Contains(note))
        {
            placedNotes.Add(note);
        }

        note.CurrentZone = this;

        // Keep world scale while re-parenting.
        note.transform.SetParent(transform, true);

        RepositionNotes();
    }


    public void RemoveNote(StickyNote note)
    {
        if (note == null)
        {
            return;
        }

        placedNotes.Remove(note);

        if (note.CurrentZone == this)
        {
            note.CurrentZone = null;
        }

        RepositionNotes();
    }


    void RepositionNotes()
    {
        // Drop any notes that were destroyed while in this zone.
        for (int i = placedNotes.Count - 1; i >= 0; i--)
        {
            if (placedNotes[i] == null)
            {
                placedNotes.RemoveAt(i);
            }
        }

        // Guard against columns being set to 0 in the Inspector.
        int safeColumns = Mathf.Max(1, columns);

        for (int i = 0; i < placedNotes.Count; i++)
        {
            int row = i / safeColumns;
            int column = i % safeColumns;

            float x = (column - (safeColumns - 1) * 0.5f) * spacingX;
            float y = startY - row * spacingY;

            StickyNote note = placedNotes[i];

            note.transform.localPosition = new Vector3(x, y, surfaceOffset);
            note.transform.localRotation = Quaternion.identity;
        }
    }
}
