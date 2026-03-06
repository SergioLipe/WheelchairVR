using UnityEngine;

/// <summary>
/// Controls a pedestrian traffic light system.
/// Switches between Red and Green states and toggles associated hazards on the road.
/// Allows for a unique duration on the very first light cycle.
/// This script DOES NOT control cars directly.
/// </summary>
public class PedestrianTrafficLight : MonoBehaviour
{
    [Header("=== Light Visuals ===")]
    [Tooltip("The GameObject representing the Red light glow.")]
    public GameObject redLightObject;

    [Tooltip("The GameObject representing the Green light glow.")]
    public GameObject greenLightObject;

    [Header("=== Hazard Management ===")]
    [Tooltip("List of hazard objects (e.g., triggers on the road) to enable during Red light.")]
    public GameObject[] hazardObjects;

    [Header("=== Initial Timing Settings ===")]
    [Tooltip("Duration in seconds for the VERY FIRST Red light phase.")]
    public float initialRedDuration = 10.0f;

    [Tooltip("Duration in seconds for the VERY FIRST Green light phase.")]
    public float initialGreenDuration = 10.0f;

    [Header("=== Normal Loop Timing Settings ===")]
    [Tooltip("Duration in seconds for all subsequent Red light phases.")]
    public float redDuration = 5.0f;

    [Tooltip("Duration in seconds for all subsequent Green light phases.")]
    public float greenDuration = 5.0f;

    [Tooltip("If true, the cycle starts with the Green light. Otherwise, starts with Red.")]
    public bool startGreen = false;

    // Internal state tracking
    private float timer;
    private bool isRed;
    
    // Track if it is the first time running each phase
    private bool isFirstRed = true;
    private bool isFirstGreen = true;

    void Start()
    {
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
            timer = initialRedDuration;
            isFirstRed = false; // Never use the initial time again
        }
        else
        {
            timer = redDuration;
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
            timer = initialGreenDuration;
            isFirstGreen = false; // Never use the initial time again
        }
        else
        {
            timer = greenDuration;
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