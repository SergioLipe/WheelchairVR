using UnityEngine;
using UnityEngine.UI;

public class VRSeatEnforcer : MonoBehaviour
{
    [Header("Core References")]
    public Transform headCamera;
    public Transform seatCenter; 
    public Image fadeImage;

    [Header("Front Boundaries")]
    [Tooltip("How far forward the player can lean")]
    public float maxFrontDistance = 0.4f;
    [Tooltip("How far left/right the player can lean when leaning FORWARD")]
    public float maxSideDistanceFront = 0.3f;
    
    [Header("Back & Shoulder Boundaries")]
    [Tooltip("Leaning straight back into the chair (very strict backrest limit)")]
    public float maxBackCenter = 0.05f;
    
    [Tooltip("Leaning backward while leaning to the side (looking over the shoulder)")]
    public float maxBackShoulder = 0.25f;

    [Tooltip("How far left/right you can lean when looking over your shoulder")]
    public float maxSideDistanceBack = 0.35f;
    
    [Header("Height Boundary")]
    public float maxHeight = 0.15f;

    [Header("Fade Settings")]
    public float fadeSpeed = 15f; 
    public float fadeSharpness = 8f; 

    private void Update()
    {
        if (headCamera == null || fadeImage == null || seatCenter == null) return;

        Vector3 localHeadPos = seatCenter.InverseTransformPoint(headCamera.position);

        float frontExcess = 0f;
        float backExcess = 0f;
        float sideExcess = 0f;
        
        if (localHeadPos.z > 0) 
        {
            // --- LEANING FORWARD ---
            frontExcess = Mathf.Max(0, localHeadPos.z - maxFrontDistance);
            sideExcess = Mathf.Max(0, Mathf.Abs(localHeadPos.x) - maxSideDistanceFront);
        }
        else 
        {
            // --- LEANING BACKWARD ---
            // Calculate how much they are leaning to the side (0 = straight back, 1 = fully over the shoulder)
            float sideRatio = Mathf.Clamp01(Mathf.Abs(localHeadPos.x) / maxSideDistanceBack);
            
            // Blend the back limit: strict in the middle, loose on the sides
            float currentBackLimit = Mathf.Lerp(maxBackCenter, maxBackShoulder, sideRatio);
            
            backExcess = Mathf.Max(0, Mathf.Abs(localHeadPos.z) - currentBackLimit);
            sideExcess = Mathf.Max(0, Mathf.Abs(localHeadPos.x) - maxSideDistanceBack);
        }

        // --- HEIGHT ---
        float heightExcess = 0f;
        if (localHeadPos.y > 0)
        {
            heightExcess = Mathf.Max(0, localHeadPos.y - maxHeight);
        }

        // Find the largest rule broken and apply fade
        float maxExcess = Mathf.Max(frontExcess, backExcess, sideExcess, heightExcess);
        float targetAlpha = Mathf.Clamp01(maxExcess * fadeSharpness);

        Color currentColor = fadeImage.color;
        currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * fadeSpeed);
        fadeImage.color = currentColor;
    }
}