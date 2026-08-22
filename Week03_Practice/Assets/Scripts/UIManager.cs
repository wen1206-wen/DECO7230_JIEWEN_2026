using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI onScreenText;
    public DebugRotator targetRotator;

    void Update()
    {
        // Update the UI text with the current rotation value
        onScreenText.text = "Rotation: " + targetRotator.currentRotation.ToString("F1");
    }
}