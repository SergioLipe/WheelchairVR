using UnityEngine;
using TMPro;

/// <summary>
/// Professional VR Dashboard UI Controller
/// Manages the Left Tablet (Stats: Time, Collisions, Slides) 
/// and the Right Tablet (Mode, replaced dynamically by Emergency Brake).
/// Uses TextMeshPro Rich Text for a clean, modern, and readable layout.
/// </summary>
public class VRDashboardUI : MonoBehaviour
{
    [Header("=== Core References ===")]
    public MovementVR wheelchairController;
    public CollisionSystemVR collisionSystem;

    [Header("=== Countdown Reference ===")]
    [Tooltip("Drag the object with the VRCountdownUI script here")]
    public VRCountdownUI countdownScript;

    [Header("=== Left Dashboard (Stats) ===")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI collisionsText;
    public TextMeshProUGUI slidesText;

    [Header("=== Right Dashboard (Mode & Brake) ===")]
    public TextMeshProUGUI modeText;

    // --- Custom Timer Variables ---
    private float timeElapsed = 0f;
    private bool isTimerRunning = false;

    private void OnEnable()
    {
        // Start listening to the countdown script
        if (countdownScript != null)
        {
            countdownScript.OnCountdownFinished += StartTimer;
        }
        else
        {
            // Fallback: If no countdown script is assigned, start immediately
            isTimerRunning = true;
        }
    }

    private void OnDisable()
    {
        // Stop listening to prevent errors
        if (countdownScript != null)
        {
            countdownScript.OnCountdownFinished -= StartTimer;
        }
    }

    // Function called by the event when countdown finishes
    private void StartTimer()
    {
        isTimerRunning = true;
    }

    void Update()
    {
        // Prevent errors if references are not assigned in the Inspector
        if (wheelchairController == null || collisionSystem == null) return;

        UpdateTimerDisplay();
        UpdateStatsDisplay();
        UpdateModeDisplay();
    }

    /// <summary>
    /// Updates the digital clock showing time since the level started.
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (timeText == null) return;
        
        // Only increment the timer if the countdown is finished
        if (isTimerRunning)
        {
            timeElapsed += Time.deltaTime;
        }

        int minutes = Mathf.FloorToInt(timeElapsed / 60f);
        int seconds = Mathf.FloorToInt(timeElapsed % 60f);
        
        // Formats time as "00:00" and makes it bigger and bold
        timeText.text = $"<size=130%><b>{minutes:00}:{seconds:00}</b></size>";
    }

    /// <summary>
    /// Updates the collision and slide counters with custom colors.
    /// </summary>
    private void UpdateStatsDisplay()
    {
        if (collisionsText != null) 
        {
            // Uses Rich Text (Hex color) to make the number stand out
            collisionsText.text = $"Colisões: <color=#FF4D4D><b>{collisionSystem.TotalCollisions}</b></color>";
        }

        if (slidesText != null) 
        {
            slidesText.text = $"Deslizes: <color=#FFB84D><b>{collisionSystem.TotalSlides}</b></color>";
        }
    }

    /// <summary>
    /// Handles the right tablet logic. Shows the driving mode, 
    /// but completely overrides it with a warning if the brake is held.
    /// </summary>
    private void UpdateModeDisplay()
    {
        if (modeText == null) return;

        // 1. Check if the emergency brake is active first
        if (wheelchairController.IsEmergencyBraking())
        {
            modeText.text = "<size=150%><b>TRAVÃO</b></size>";
            modeText.color = new Color(1f, 0.2f, 0.2f, 1f); // Strong Red
            return; // Stops the function here so the mode text doesn't overwrite it
        }

        // 2. If no brake is applied, show the current speed mode
        string modeString = "";
        Color modeColor = Color.white;

        switch (wheelchairController.currentMode)
        {
            case MovementVR.SpeedMode.Slow:
                modeString = "<size=150%><b>INTERIOR</b></size>";
                modeColor = new Color(1f, 0.9f, 0.5f, 1f); // Yellowish
                break;
            case MovementVR.SpeedMode.Normal:
                modeString = "<size=150%><b>EXTERIOR</b></size>";
                modeColor = new Color(0.6f, 1f, 0.7f, 1f); // Greenish
                break;
            case MovementVR.SpeedMode.Off:
                modeString = "<size=150%><b>DESLIGADO</b></size>";
                modeColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Gray
                break;
        }

        modeText.text = modeString;
        modeText.color = modeColor;
    }
}