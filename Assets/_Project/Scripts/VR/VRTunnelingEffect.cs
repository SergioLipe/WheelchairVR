using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a vignette UI image to reduce VR motion sickness.
/// Fades in when the wheelchair moves or rotates, and fades out when stopped.
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
    public float maxDarkness = 0.8f;
    
    [Tooltip("The speed at which the tunneling effect starts triggering")]
    public float speedThreshold = 0.2f;

    private Rigidbody wheelchairRb;

    private void Start()
    {
        if (wheelchairMovement != null)
        {
            wheelchairRb = wheelchairMovement.GetComponent<Rigidbody>();
        }

        // Guarantee the vignette starts completely invisible
        if (vignetteImage != null)
        {
            Color startColor = vignetteImage.color;
            startColor.a = 0f;
            vignetteImage.color = startColor;
        }
    }

    private void Update()
    {
        if (wheelchairMovement == null || vignetteImage == null || wheelchairRb == null) return;

        // Check linear speed
        float currentSpeed = Mathf.Abs(wheelchairMovement.GetCurrentSpeed());
        
        // Check rotational speed (turning is the biggest cause of VR sickness!)
        float turnSpeed = Mathf.Abs(wheelchairRb.angularVelocity.y);

        float targetAlpha = 0f;

        // If moving fast enough OR turning fast enough, activate the tunnel
        if (currentSpeed > speedThreshold || turnSpeed > 0.1f)
        {
            targetAlpha = maxDarkness;
        }

        // Smoothly transition the alpha (fade effect)
        Color currentColor = vignetteImage.color;
        currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * fadeSpeed);
        vignetteImage.color = currentColor;
    }
}