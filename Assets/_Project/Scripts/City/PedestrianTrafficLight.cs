using UnityEngine;

/// <summary>
/// Controls a pedestrian traffic light system.
/// Switches between Red and Green states and toggles associated hazards on the road.
/// Automatically switches between VR and PC timings based on which wheelchair is active.
/// This script DOES NOT control cars directly.
/// </summary>
public class PedestrianTrafficLight : MonoBehaviour
{
    [Header("=== Mode Detection ===")]
    [Tooltip("Drag the Wheelchair_VR here. If active, uses VR timings. If not, uses PC timings.")]
    public GameObject vrWheelchair;

    [Header("=== Light Visuals ===")]
    [Tooltip("The GameObject representing the Red light glow.")]
    public GameObject redLightObject;

    [Tooltip("The GameObject representing the Green light glow.")]
    public GameObject greenLightObject;

    [Header("=== Hazard Management ===")]
    [Tooltip("List of hazard objects (e.g., triggers on the road) to enable during Red light.")]
    public GameObject[] hazardObjects;

    [Header("=== VR Timings ===")]
    public float vrInitialRed = 10.0f;
    public float vrInitialGreen = 10.0f;
    public float vrNormalRed = 5.0f;
    public float vrNormalGreen = 5.0f;

    [Header("=== PC Timings ===")]
    public float pcInitialRed = 5.0f;
    public float pcInitialGreen = 5.0f;
    public float pcNormalRed = 3.0f;
    public float pcNormalGreen = 3.0f;

    [Header("=== Starting State ===")]
    [Tooltip("If true, the cycle starts with the Green light. Otherwise, starts with Red.")]
    public bool startGreen = false;

    // --- Active Timings (chosen when the game starts) ---
    private float activeInitialRed;
    private float activeInitialGreen;
    private float activeNormalRed;
    private float activeNormalGreen;

    // Internal state tracking
    private float timer;
    private bool isRed;
    
    // Track if it is the first time running each phase
    private bool isFirstRed = true;
    private bool isFirstGreen = true;

    void Start()
    {
        // Check if VR Wheelchair exists and is turned on in the Hierarchy
        if (vrWheelchair != null && vrWheelchair.activeInHierarchy)
        {
            // Apply VR Timings
            activeInitialRed = vrInitialRed;
            activeInitialGreen = vrInitialGreen;
            activeNormalRed = vrNormalRed;
            activeNormalGreen = vrNormalGreen;
        }
        else
        {
            // Apply PC Timings
            activeInitialRed = pcInitialRed;
            activeInitialGreen = pcInitialGreen;
            activeNormalRed = pcNormalRed;
            activeNormalGreen = pcNormalGreen;
        }

        // Initialize the traffic light based on the 'startGreen' toggle
        if (startGreen)
        {
            SetGreenLight();
        }
        else
        {
            SetRedLight();
        }
    }

    void Update()
    {
        // Countdown the active phase timer
        timer -= Time.deltaTime;

        // Switch states when the timer reaches zero
        if (timer <= 0f)
        {
            if (isRed)
            {
                SetGreenLight();
            }
            else
            {
                SetRedLight();
            }
        }
    }

    /// <summary>
    /// Activates the Red light and enables hazards (road is dangerous).
    /// </summary>
    private void SetRedLight()
    {
        isRed = true;

        // Visuals
        if (redLightObject != null) redLightObject.SetActive(true);
        if (greenLightObject != null) greenLightObject.SetActive(false);

        // Logic: Enable hazards because cars might be passing
        ToggleHazards(true);

        // Apply initial duration if it's the first time, otherwise use normal duration
        if (isFirstRed)
        {
            timer = activeInitialRed;
            isFirstRed = false; // Never use the initial time again
        }
        else
        {
            timer = activeNormalRed;
        }
    }

    /// <summary>
    /// Activates the Green light and disables hazards (road is safe).
    /// </summary>
    private void SetGreenLight()
    {
        isRed = false;

        // Visuals
        if (redLightObject != null) redLightObject.SetActive(false);
        if (greenLightObject != null) greenLightObject.SetActive(true);

        // Logic: Disable hazards so the wheelchair can cross
        ToggleHazards(false);

        // Apply initial duration if it's the first time, otherwise use normal duration
        if (isFirstGreen)
        {
            timer = activeInitialGreen;
            isFirstGreen = false; // Never use the initial time again
        }
        else
        {
            timer = activeNormalGreen;
        }
    }

    /// <summary>
    /// Helper method to enable or disable all hazards assigned to this traffic light.
    /// </summary>
    private void ToggleHazards(bool activeState)
    {
        if (hazardObjects == null) return;

        foreach (GameObject hazard in hazardObjects)
        {
            if (hazard != null)
            {
                hazard.SetActive(activeState);
            }
        }
    }
}