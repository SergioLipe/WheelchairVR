using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Modern visual collision feedback system with spam prevention.
/// Uses pre-cached UI Canvas gradients, smooth animations, and directional effects
/// to ensure zero latency and perfect audio-visual synchronization.
/// </summary>
public class CollisionFlashEffect : MonoBehaviour
{
    [Header("=== General Configuration ===")]
    [Tooltip("Enable feedback system")]
    public bool feedbackActive = true;

    [Tooltip("Canvas reference (will be created automatically if null)")]
    public Canvas canvas;

    [Header("=== Spam Prevention ===")]
    [Tooltip("Minimum time between effects for same direction (seconds)")]
    [Range(0.1f, 2f)]
    public float effectCooldown = 1f;

    [Tooltip("Global cooldown between any effects (seconds)")]
    [Range(0f, 1f)]
    public float globalCooldown = 1f;

    [Header("=== Visual Configuration ===")]
    [Tooltip("Effect duration (seconds)")]
    [Range(0.1f, 2f)]
    public float effectDuration = 0.5f;

    [Tooltip("Maximum effect intensity (0-1)")]
    [Range(0f, 1f)]
    public float maxIntensity = 0.7f;

    [Tooltip("Use radial gradient (more modern)")]
    public bool useRadialGradient = true;

    [Header("=== Colors ===")]
    [Tooltip("Color for front/rear collisions")]
    public Color impactColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("Color for side slides")]
    public Color slideColor = new Color(1f, 0.8f, 0f, 1f);

    [Header("=== Animation ===")]
    [Tooltip("Effect animation curve")]
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Number of pulses")]
    [Range(1, 3)]
    public int pulseCount = 1;

    [Header("=== Camera Shake (Optional) ===")]
    [Tooltip("Enable camera shake")]
    public bool cameraShakeActive = true;

    [Tooltip("Shake intensity")]
    [Range(0f, 1f)]
    public float shakeIntensity = 0.15f;

    [Tooltip("Shake duration")]
    [Range(0.05f, 0.5f)]
    public float shakeDuration = 0.2f;

    [Header("=== Extra Effects ===")]
    [Tooltip("Show directional arrows")]
    public bool showArrows = true;

    [Tooltip("Arrow size")]
    [Range(50f, 200f)]
    public float arrowSize = 100f;

    [Header("=== Debug Info ===")]
    [SerializeField] private bool showDebugInfo = false;

    public enum CollisionType
    {
        None,
        Front,
        Back,
        LeftSide,
        RightSide
    }

    // UI components
    private GameObject effectPanel;
    private Image effectImage;
    private GameObject[] arrows = new GameObject[4];
    private Image[] arrowImages = new Image[4];

    // Animation state - one coroutine per arrow for independent control
    private Coroutine[] arrowCoroutines = new Coroutine[4];
    private Coroutine mainEffectCoroutine;
    private Transform cameraTransform;
    private Vector3 originalCameraPosition;

    // Cooldown management
    private Dictionary<CollisionType, float> lastEffectTime = new Dictionary<CollisionType, float>();
    private float lastGlobalEffectTime = 0f;
    private Dictionary<CollisionType, int> effectCounter = new Dictionary<CollisionType, int>();

    // Cache for pre-generated gradient textures to avoid CPU/GC spikes during collisions
    private Dictionary<CollisionType, Sprite> cachedGradientSprites = new Dictionary<CollisionType, Sprite>();

    void Start()
    {
        SetupUI();
        InitializeCamera();
        InitializeCooldowns();
        PrecomputeSprites(); // Generate textures ONLY ONCE at startup to prevent lag
    }

    void OnDestroy()
    {
        StopAllEffects();
        ClearCache();
    }

    /// <summary>
    /// Pre-generates the 512x512 gradient textures at startup.
    /// This prevents massive CPU spikes and Garbage Collection stuttering during gameplay.
    /// </summary>
    private void PrecomputeSprites()
    {
        CollisionType[] types = { CollisionType.Front, CollisionType.Back, CollisionType.LeftSide, CollisionType.RightSide };
        
        foreach (CollisionType type in types)
        {
            Texture2D tex = CreateGradientTexture(type);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            cachedGradientSprites[type] = sprite;
        }
    }

    /// <summary>
    /// Cleans up cached textures from memory when the script is destroyed.
    /// </summary>
    private void ClearCache()
    {
        foreach (Sprite cachedSprite in cachedGradientSprites.Values)
        {
            if (cachedSprite != null)
            {
                if (cachedSprite.texture != null) Destroy(cachedSprite.texture);
                Destroy(cachedSprite);
            }
        }
        cachedGradientSprites.Clear();
    }

    /// <summary>
    /// Initialize cooldown tracking
    /// </summary>
    private void InitializeCooldowns()
    {
        lastEffectTime[CollisionType.Front] = 0f;
        lastEffectTime[CollisionType.Back] = 0f;
        lastEffectTime[CollisionType.LeftSide] = 0f;
        lastEffectTime[CollisionType.RightSide] = 0f;

        effectCounter[CollisionType.Front] = 0;
        effectCounter[CollisionType.Back] = 0;
        effectCounter[CollisionType.LeftSide] = 0;
        effectCounter[CollisionType.RightSide] = 0;
    }

    /// <summary>
    /// Initializes camera reference for shake effect
    /// </summary>
    private void InitializeCamera()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            originalCameraPosition = cameraTransform.localPosition;
        }
    }

    /// <summary>
    /// Sets up Canvas and UI elements
    /// </summary>
    void SetupUI()
    {
        CreateCanvasIfNeeded();
        CreateEffectPanel();

        if (showArrows)
        {
            CreateArrows();
        }
    }

    /// <summary>
    /// Creates Canvas if it doesn't exist
    /// </summary>
    private void CreateCanvasIfNeeded()
    {
        if (canvas != null) return;

        GameObject canvasObj = new GameObject("CollisionFeedbackCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// Creates main effect panel
    /// </summary>
    private void CreateEffectPanel()
    {
        effectPanel = new GameObject("EffectPanel");
        effectPanel.transform.SetParent(canvas.transform, false);

        RectTransform rectEffect = effectPanel.AddComponent<RectTransform>();
        rectEffect.anchorMin = Vector2.zero;
        rectEffect.anchorMax = Vector2.one;
        rectEffect.sizeDelta = Vector2.zero;

        effectImage = effectPanel.AddComponent<Image>();
        effectImage.color = new Color(1, 1, 1, 0);
        effectImage.raycastTarget = false;
    }

    /// <summary>
    /// Creates directional arrows
    /// </summary>
    void CreateArrows()
    {
        string[] names = { "FrontArrow", "BackArrow", "LeftArrow", "RightArrow" };
        Vector2[] positions = {
            new Vector2(0.5f, 0.85f),
            new Vector2(0.5f, 0.15f),
            new Vector2(0.15f, 0.5f),
            new Vector2(0.85f, 0.5f)
        };
        float[] rotations = { 0f, 180f, 90f, -90f };

        Sprite arrowSprite = CreateArrowSprite();

        for (int i = 0; i < 4; i++)
        {
            arrows[i] = new GameObject(names[i]);
            arrows[i].transform.SetParent(canvas.transform, false);

            RectTransform rect = arrows[i].AddComponent<RectTransform>();
            rect.anchorMin = positions[i];
            rect.anchorMax = positions[i];
            rect.sizeDelta = new Vector2(arrowSize, arrowSize);
            rect.localRotation = Quaternion.Euler(0, 0, rotations[i]);

            arrowImages[i] = arrows[i].AddComponent<Image>();
            arrowImages[i].color = new Color(1, 1, 1, 0);
            arrowImages[i].raycastTarget = false;
            arrowImages[i].sprite = arrowSprite;
        }
    }

    /// <summary>
    /// Creates arrow sprite procedurally
    /// </summary>
    Sprite CreateArrowSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normY = (float)y / size;
                float centerX = size / 2f;
                float width = (1f - normY) * size / 2f;

                if (x >= centerX - width && x <= centerX + width)
                {
                    pixels[y * size + x] = Color.white;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ===== PUBLIC METHODS =====

    public void FrontFlash() => ActivateFeedback(CollisionType.Front);
    public void BackFlash() => ActivateFeedback(CollisionType.Back);
    public void LeftSideFlash() => ActivateFeedback(CollisionType.LeftSide);
    public void RightSideFlash() => ActivateFeedback(CollisionType.RightSide);

    /// <summary>
    /// Check if effect is allowed based on cooldowns
    /// </summary>
    private bool IsEffectAllowed(CollisionType type)
    {
        float currentTime = Time.time;

        // Check global cooldown
        if (currentTime - lastGlobalEffectTime < globalCooldown)
        {
            if (showDebugInfo) Debug.Log($"Global cooldown active. Wait {globalCooldown - (currentTime - lastGlobalEffectTime):F2}s");
            return false;
        }

        // Check specific direction cooldown
        if (lastEffectTime.ContainsKey(type))
        {
            float timeSinceLastEffect = currentTime - lastEffectTime[type];
            if (timeSinceLastEffect < effectCooldown)
            {
                if (showDebugInfo) Debug.Log($"{type} cooldown active. Wait {effectCooldown - timeSinceLastEffect:F2}s");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Activates visual feedback and plays the collision sound in perfect sync.
    /// Sound will ONLY play if the visual flash is also triggered.
    /// Hard collision sounds are exclusively played for Front and Back impacts.
    /// </summary>
    void ActivateFeedback(CollisionType type)
    {
        // 1. Basic validation
        if (!feedbackActive || type == CollisionType.None) return;

        // 2. Cooldown & Spam Check
        // If the effect is on cooldown, we block BOTH the visual flash and the audio.
        if (!IsEffectAllowed(type))
        {
            effectCounter[type]++;

            if (effectCounter[type] > 5)
            {
                if (showDebugInfo) Debug.LogWarning($"Excessive collision spam detected for {type}");
                effectCounter[type] = 0; 
            }

            return; // Blocks execution: NO flash and NO sound
        }

        // ==========================================
        // 3. PERFECT AUDIO & VISUAL SYNC
        // ==========================================
        // We only want the hard collision sound for direct impacts (Front/Back).
        // Slide sounds are handled separately by MovementPC.
        if (type == CollisionType.Front || type == CollisionType.Back)
        {
            MovementPC movePC = GetComponent<MovementPC>();
            if (movePC != null && movePC.hardCollisionSound != null)
            {
                movePC.PlaySound(movePC.hardCollisionSound);
            }


        }
        // ==========================================

        // 4. Update cooldown timers
        float currentTime = Time.time;
        lastEffectTime[type] = currentTime;
        lastGlobalEffectTime = currentTime;
        effectCounter[type] = 0; 

        // 5. Restart Main Screen Effect
        if (mainEffectCoroutine != null)
        {
            StopCoroutine(mainEffectCoroutine);
            mainEffectCoroutine = null;
        }
        mainEffectCoroutine = StartCoroutine(AnimateMainEffect(type));

        // 6. Handle Directional Arrows Animation
        int arrowIndex = GetArrowIndex(type);
        if (arrowIndex >= 0 && showArrows)
        {
            if (arrowCoroutines[arrowIndex] != null)
            {
                StopCoroutine(arrowCoroutines[arrowIndex]);
                arrowCoroutines[arrowIndex] = null;
            }

            Color color = GetColorForType(type);
            arrowCoroutines[arrowIndex] = StartCoroutine(AnimateArrow(arrowIndex, color));
        }

        // 7. Camera Shake
        if (cameraShakeActive && cameraTransform != null)
        {
            if (currentTime - lastGlobalEffectTime > shakeDuration)
            {
                StartCoroutine(CameraShake());
            }
        }
    }

    /// <summary>
    /// Animates the main screen effect using pre-cached sprites.
    /// This is highly optimized and will not cause Garbage Collection spikes.
    /// </summary>
    IEnumerator AnimateMainEffect(CollisionType type)
    {
        Color color = GetColorForType(type);
        
        // Apply the pre-calculated sprite from the dictionary
        if (cachedGradientSprites.ContainsKey(type))
        {
            effectImage.sprite = cachedGradientSprites[type];
        }

        float durationPerPulse = effectDuration / pulseCount;
        float elapsedTime = 0f;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            float pulseStartTime = elapsedTime;

            while (elapsedTime - pulseStartTime < durationPerPulse)
            {
                elapsedTime += Time.deltaTime;
                float progress = (elapsedTime - pulseStartTime) / durationPerPulse;
                float intensity = animationCurve.Evaluate(progress) * maxIntensity;

                Color currentColor = color;
                currentColor.a = intensity;
                effectImage.color = currentColor;

                yield return null;
            }
        }

        // Clean up main effect visibility (Reset alpha to 0)
        effectImage.color = new Color(color.r, color.g, color.b, 0);
        mainEffectCoroutine = null;
    }

    /// <summary>
    /// Animates a specific arrow independently
    /// </summary>
    IEnumerator AnimateArrow(int arrowIndex, Color color)
    {
        if (arrowIndex < 0 || arrowIndex >= 4 || arrowImages[arrowIndex] == null)
            yield break;

        float durationPerPulse = effectDuration / pulseCount;
        float elapsedTime = 0f;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            float pulseStartTime = elapsedTime;

            while (elapsedTime - pulseStartTime < durationPerPulse)
            {
                elapsedTime += Time.deltaTime;
                float progress = (elapsedTime - pulseStartTime) / durationPerPulse;
                float intensity = animationCurve.Evaluate(progress) * maxIntensity;

                // Update arrow visual
                Color arrowColor = color;
                arrowColor.a = intensity * 1.5f;
                arrowImages[arrowIndex].color = arrowColor;

                float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
                arrows[arrowIndex].transform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        // Clean up this specific arrow
        arrowImages[arrowIndex].color = new Color(color.r, color.g, color.b, 0);
        arrows[arrowIndex].transform.localScale = Vector3.one;
        arrowCoroutines[arrowIndex] = null;
    }

    /// <summary>
    /// Returns color based on collision type
    /// </summary>
    private Color GetColorForType(CollisionType type)
    {
        return (type == CollisionType.Front || type == CollisionType.Back) ? impactColor : slideColor;
    }

    /// <summary>
    /// Returns arrow index for collision type
    /// </summary>
    private int GetArrowIndex(CollisionType type)
    {
        if (!showArrows) return -1;

        switch (type)
        {
            case CollisionType.Front: return 0;
            case CollisionType.Back: return 1;
            case CollisionType.LeftSide: return 2;
            case CollisionType.RightSide: return 3;
            default: return -1;
        }
    }

    /// <summary>
    /// Creates directional gradient texture
    /// </summary>
    Texture2D CreateGradientTexture(CollisionType type)
    {
        int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = CalculateGradientAlpha(type, x, y, size);
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Calculates gradient alpha for pixel
    /// </summary>
    private float CalculateGradientAlpha(CollisionType type, int x, int y, int size)
    {
        if (useRadialGradient)
        {
            return CalculateRadialGradient(type, x, y, size);
        }
        else
        {
            return CalculateLinearGradient(type, x, y, size);
        }
    }

    /// <summary>
    /// Calculates radial gradient alpha
    /// </summary>
    private float CalculateRadialGradient(CollisionType type, int x, int y, int size)
    {
        float distX = Mathf.Abs(x - size / 2f) / (size / 2f);
        float distY = Mathf.Abs(y - size / 2f) / (size / 2f);

        switch (type)
        {
            case CollisionType.Front:
                return 1f - (distX * 0.7f + (1f - (float)y / size) * 0.3f);
            case CollisionType.Back:
                return 1f - (distX * 0.7f + ((float)y / size) * 0.3f);
            case CollisionType.LeftSide:
                return 1f - (distY * 0.7f + (1f - (float)x / size) * 0.3f);
            case CollisionType.RightSide:
                return 1f - (distY * 0.7f + ((float)x / size) * 0.3f);
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Calculates linear gradient alpha
    /// </summary>
    private float CalculateLinearGradient(CollisionType type, int x, int y, int size)
    {
        switch (type)
        {
            case CollisionType.Front:
                return 1f - (float)y / size;
            case CollisionType.Back:
                return (float)y / size;
            case CollisionType.LeftSide:
                return 1f - (float)x / size;
            case CollisionType.RightSide:
                return (float)x / size;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Camera shake effect
    /// </summary>
    IEnumerator CameraShake()
    {
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            float intensity = Mathf.Lerp(shakeIntensity, 0f, elapsedTime / shakeDuration);

            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f) * intensity,
                Random.Range(-1f, 1f) * intensity,
                0
            );

            cameraTransform.localPosition = originalCameraPosition + offset;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cameraTransform.localPosition = originalCameraPosition;
    }

    /// <summary>
    /// Stop all effects
    /// </summary>
    public void StopAllEffects()
    {
        // Stop main effect
        if (mainEffectCoroutine != null)
        {
            StopCoroutine(mainEffectCoroutine);
            mainEffectCoroutine = null;
        }

        // Stop all arrow coroutines
        for (int i = 0; i < arrowCoroutines.Length; i++)
        {
            if (arrowCoroutines[i] != null)
            {
                StopCoroutine(arrowCoroutines[i]);
                arrowCoroutines[i] = null;
            }
        }

        // Reset main effect visual
        if (effectImage != null)
            effectImage.color = new Color(1, 1, 1, 0);

        // Reset all arrow visuals
        for (int i = 0; i < arrowImages.Length; i++)
        {
            if (arrowImages[i] != null)
            {
                arrowImages[i].color = new Color(1, 1, 1, 0);
                if (arrows[i] != null)
                    arrows[i].transform.localScale = Vector3.one;
            }
        }

        // Reset camera
        if (cameraTransform != null)
            cameraTransform.localPosition = originalCameraPosition;
    }

    /// <summary>
    /// Get cooldown status for UI/debug
    /// </summary>
    public bool IsOnCooldown(CollisionType type)
    {
        return !IsEffectAllowed(type);
    }

    /// <summary>
    /// Reset all cooldowns
    /// </summary>
    public void ResetCooldowns()
    {
        InitializeCooldowns();
        lastGlobalEffectTime = 0f;
    }
}