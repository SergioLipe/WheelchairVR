using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a vignette UI image to reduce VR motion sickness.
/// Fades in when the wheelchair moves or rotates, and fades out when stopped.
/// Works with CharacterController-based movement (no Rigidbody needed).
/// </summary>
public class VRTunnelingEffect : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("Drag the wheelchair MovementVR script here")]
    public MovementVR wheelchairMovement;

    [Tooltip("Drag the Vignette Image (UI) here")]
    public Image vignetteImage;

    [Header("=== Settings ===")]
    [Tooltip("How fast the dark tunnel appears and disappears")]
    public float fadeSpeed = 5f;

    [Tooltip("How dark the edges get. 0 is invisible, 1 is pitch black.")]
    [Range(0f, 1f)]
    public float maxDarkness = 0.6f;

    [Tooltip("The linear speed (m/s) at which the tunnel starts appearing")]
    public float speedThreshold = 0.2f;

    [Tooltip("The rotation speed (degrees/sec) at which the tunnel starts appearing")]
    public float turnSpeedThreshold = 15f;

    [Tooltip("Maximum linear speed for full vignette intensity (m/s)")]
    public float maxSpeedForFullEffect = 2f;

    [Tooltip("Maximum turn speed for full vignette intensity (degrees/sec)")]
    public float maxTurnSpeedForFullEffect = 60f;

    // [OPT] cache de transform e estado anterior
    private Transform wheelchairTransform;
    private float previousYRotation;
    private float currentAlpha = 0f;

    // [OPT] cache da color para evitar boxing
    private Color cachedColor;

    private void Start()
    {
        if (wheelchairMovement != null)
        {
            wheelchairTransform = wheelchairMovement.transform;
            previousYRotation = wheelchairTransform.eulerAngles.y;
        }

        if (vignetteImage != null)
        {
            cachedColor = vignetteImage.color;
            cachedColor.a = 0f;
            vignetteImage.color = cachedColor;
        }
    }

    private void Update()
    {
        if (wheelchairMovement == null || vignetteImage == null || wheelchairTransform == null) return;

        // [FIX] Calcular turn speed manualmente (CharacterController não tem angularVelocity)
        float currentYRotation = wheelchairTransform.eulerAngles.y;
        float deltaY = Mathf.DeltaAngle(previousYRotation, currentYRotation);
        float turnSpeedDegPerSec = Mathf.Abs(deltaY) / Mathf.Max(Time.deltaTime, 0.0001f);
        previousYRotation = currentYRotation;

        // Linear speed
        float currentSpeed = Mathf.Abs(wheelchairMovement.GetCurrentSpeed());

        // [OPT] Cálculo proporcional da intensidade — vignette gradual, não on/off
        float speedFactor = 0f;
        if (currentSpeed > speedThreshold)
        {
            speedFactor = Mathf.InverseLerp(speedThreshold, maxSpeedForFullEffect, currentSpeed);
        }

        float turnFactor = 0f;
        if (turnSpeedDegPerSec > turnSpeedThreshold)
        {
            turnFactor = Mathf.InverseLerp(turnSpeedThreshold, maxTurnSpeedForFullEffect, turnSpeedDegPerSec);
        }

        // Usa o maior dos dois — rotação E movimento somam-se em intensidade
        float intensity = Mathf.Max(speedFactor, turnFactor);
        float targetAlpha = intensity * maxDarkness;

        // [OPT] Smooth alpha transition
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        cachedColor.a = currentAlpha;
        vignetteImage.color = cachedColor;
    }
}