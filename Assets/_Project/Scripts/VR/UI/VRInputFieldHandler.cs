using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Opens the native Meta Quest keyboard when a TMP_InputField is focused/selected.
/// Attach this component to the same GameObject that has the TMP_InputField.
/// 
/// Works in build on Meta Quest. In the Unity Editor, you can type with the PC keyboard
/// directly (TouchScreenKeyboard does not appear in the editor).
/// 
/// HOW TO USE:
/// 1. Attach this script to the GameObject that has your TMP_InputField.
/// 2. The reference auto-detects the input field on the same GameObject.
/// 3. Build and run on the Meta Quest. Click the field with the controller to open the keyboard.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class VRInputFieldHandler : MonoBehaviour, ISelectHandler, IPointerClickHandler
{
    [Tooltip("The input field this handler controls (auto-assigned if left empty)")]
    public TMP_InputField inputField;

    [Tooltip("Placeholder text shown in the keyboard before typing (optional)")]
    public string keyboardPlaceholder = "Digite o ID do paciente";

    [Tooltip("Should the keyboard auto-correct text? (usually false for IDs)")]
    public bool autoCorrect = false;

    [Tooltip("Should the keyboard hide the typed text? (true for passwords)")]
    public bool isSecureField = false;

    [Tooltip("Maximum characters allowed (0 = unlimited)")]
    public int characterLimit = 32;

    private TouchScreenKeyboard keyboard;
    private bool isKeyboardOpen = false;

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }
    }

    /// <summary>
    /// Called automatically when the input field is selected by the EventSystem.
    /// In VR, this triggers when the user clicks the field with the controller ray.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log("[VRInputFieldHandler] OnSelect FOI CHAMADO! Input field: " + (inputField != null ? inputField.name : "NULL"));
    OpenKeyboard();
        
    }

    /// <summary>
    /// Backup trigger: also opens the keyboard on direct click.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isKeyboardOpen)
        {
            OpenKeyboard();
        }
    }

    /// <summary>
    /// Opens the native virtual keyboard of the OS (Meta Quest, Android, iOS, etc.)
    /// </summary>
    public void OpenKeyboard()
    {
        if (inputField == null) return;

        // If keyboard is already open, don't open another one
        if (isKeyboardOpen && keyboard != null && keyboard.active) return;

        keyboard = TouchScreenKeyboard.Open(
            text: inputField.text,
            keyboardType: TouchScreenKeyboardType.Default,
            autocorrection: autoCorrect,
            multiline: false,
            secure: isSecureField,
            alert: false,
            textPlaceholder: keyboardPlaceholder,
            characterLimit: characterLimit
        );

        isKeyboardOpen = true;
        Debug.Log("[VRInputFieldHandler] Native keyboard opened");
    }

    private void Update()
    {
        if (keyboard == null || !isKeyboardOpen) return;

        // Sync the input field with what the user is typing in the keyboard
        if (inputField != null)
        {
            inputField.text = keyboard.text;
        }

        // When the keyboard is closed (Done/Cancel), update the input field one last time
        if (keyboard.status == TouchScreenKeyboard.Status.Done ||
            keyboard.status == TouchScreenKeyboard.Status.Canceled ||
            keyboard.status == TouchScreenKeyboard.Status.LostFocus)
        {
            isKeyboardOpen = false;
            keyboard = null;
            Debug.Log("[VRInputFieldHandler] Native keyboard closed");
        }
    }

    private void OnDisable()
    {
        // Make sure the keyboard is closed if the field is disabled
        if (keyboard != null && keyboard.active)
        {
            keyboard.active = false;
            isKeyboardOpen = false;
            keyboard = null;
        }
    }
}