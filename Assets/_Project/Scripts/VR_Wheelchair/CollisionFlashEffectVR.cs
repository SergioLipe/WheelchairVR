using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Modern visual collision feedback system tailored for VR.
/// Uses WorldSpace Canvas attached to the Main Camera.
/// Note: Camera shake has been explicitly removed as it causes severe motion sickness in VR.
/// </summary>
public class CollisionFlashEffectVR : MonoBehaviour
{
    [Header("=== General Configuration ===")]
    [Tooltip("Enable feedback system")]
    public bool feedbackActive = true;

    [Tooltip("Canvas reference (will be created automatically if null)")]
    public Canvas canvas;

    [Tooltip("Distance from the VR Camera to place the HUD (meters)")]
    public float hudDistance = 0.5f;

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

    // Animation state
    private Coroutine[] arrowCoroutines = new Coroutine[4];
    private Coroutine mainEffectCoroutine;
    private Transform vrCameraTransform;

    // Cooldown management
    private Dictionary<CollisionType, float> lastEffectTime = new Dictionary<CollisionType, float>();
    private float lastGlobalEffectTime = 0f;
    private Dictionary<CollisionType, int> effectCounter = new Dictionary<CollisionType, int>();

    // Cache
    private Dictionary<CollisionType, Sprite> cachedGradientSprites = new Dictionary<CollisionType, Sprite>();

    void Start()
    {
        if (Camera.main != null)
        {
            vrCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Main Camera not found! Make sure your VR camera is tagged as 'MainCamera'.");
        }

        SetupUI();
        InitializeCooldowns();
        PrecomputeSprites();
    }

    void OnDestroy()
    {
        StopAllEffects();
        ClearCache();
    }

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
    /// Creates a WorldSpace Canvas attached to the VR Camera
    /// </summary>
    private void CreateCanvasIfNeeded()
    {
        if (canvas != null) return;

        GameObject canvasObj = new GameObject("VRCollisionFeedbackCanvas");
        
        if (vrCameraTransform != null)
        {
            canvasObj.transform.SetParent(vrCameraTransform, false);
            // Place it in front of the player's face
            canvasObj.transform.localPosition = new Vector3(0, 0, hudDistance);
            canvasObj.transform.localRotation = Quaternion.identity;
        }

        canvas = canvasObj.AddComponent<Canvas>();
        // CRITICAL FOR VR: Must be WorldSpace
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920, 1080);
        // Scale it way down so 1920 pixels fits within ~1 meter in the VR world
        rect.localScale = new Vector3(0.0006f, 0.0006f, 0.0006f); 
    }

    private void CreateEffectPanel()
    {
        effectPanel = new GameObject("EffectPanel");
        effectPanel.transform.SetParent(canvas.transform, false);

        RectTransform rectEffect = effectPanel.AddComponent<RectTransform>();
        rectEffect.anchorMin = Vector2.zero;
        rectEffect.anchorMax = Vector2.one;
        rectEffect.sizeDelta = Vector2.zero;
        rectEffect.localPosition = Vector3.zero;

        effectImage = effectPanel.AddComponent<Image>();
        effectImage.color = new Color(1, 1, 1, 0);
        effectImage.raycastTarget = false;
    }

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
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.Euler(0, 0, rotations[i]);

            arrowImages[i] = arrows[i].AddComponent<Image>();
            arrowImages[i].color = new Color(1, 1, 1, 0);
            arrowImages[i].raycastTarget = false;
            arrowImages[i].sprite = arrowSprite;
        }
    }

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

    public void FrontFlash() => ActivateFeedback(CollisionType.Front);
    public void BackFlash() => ActivateFeedback(CollisionType.Back);
    public void LeftSideFlash() => ActivateFeedback(CollisionType.LeftSide);
    public void RightSideFlash() => ActivateFeedback(CollisionType.RightSide);

    private bool IsEffectAllowed(CollisionType type)
    {
        float currentTime = Time.time;

        if (currentTime - lastGlobalEffectTime < globalCooldown) return false;

        if (lastEffectTime.ContainsKey(type))
        {
            float timeSinceLastEffect = currentTime - lastEffectTime[type];
            if (timeSinceLastEffect < effectCooldown) return false;
        }

        return true;
    }

    void ActivateFeedback(CollisionType type)
    {
        if (!feedbackActive || type == CollisionType.None) return;

        if (!IsEffectAllowed(type))
        {
            effectCounter[type]++;
            if (effectCounter[type] > 5)
            {
                effectCounter[type] = 0; 
            }
            return; 
        }

        // Changed to MovementVR
        if (type == CollisionType.Front || type == CollisionType.Back)
        {
            MovementVR moveVR = GetComponent<MovementVR>();
            if (moveVR != null && moveVR.hardCollisionSound != null)
            {
                moveVR.PlaySound(moveVR.hardCollisionSound);
            }
        }

        float currentTime = Time.time;
        lastEffectTime[type] = currentTime;
        lastGlobalEffectTime = currentTime;
        effectCounter[type] = 0; 

        if (mainEffectCoroutine != null)
        {
            StopCoroutine(mainEffectCoroutine);
            mainEffectCoroutine = null;
        }
        mainEffectCoroutine = StartCoroutine(AnimateMainEffect(type));

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
    }

    IEnumerator AnimateMainEffect(CollisionType type)
    {
        Color color = GetColorForType(type);
        
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

        effectImage.color = new Color(color.r, color.g, color.b, 0);
        mainEffectCoroutine = null;
    }

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

                Color arrowColor = color;
                arrowColor.a = intensity * 1.5f;
                arrowImages[arrowIndex].color = arrowColor;

                float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
                arrows[arrowIndex].transform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        arrowImages[arrowIndex].color = new Color(color.r, color.g, color.b, 0);
        arrows[arrowIndex].transform.localScale = Vector3.one;
        arrowCoroutines[arrowIndex] = null;
    }

    private Color GetColorForType(CollisionType type)
    {
        return (type == CollisionType.Front || type == CollisionType.Back) ? impactColor : slideColor;
    }

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

    public void StopAllEffects()
    {
        if (mainEffectCoroutine != null)
        {
            StopCoroutine(mainEffectCoroutine);
            mainEffectCoroutine = null;
        }

        for (int i = 0; i < arrowCoroutines.Length; i++)
        {
            if (arrowCoroutines[i] != null)
            {
                StopCoroutine(arrowCoroutines[i]);
                arrowCoroutines[i] = null;
            }
        }

        if (effectImage != null)
            effectImage.color = new Color(1, 1, 1, 0);

        for (int i = 0; i < arrowImages.Length; i++)
        {
            if (arrowImages[i] != null)
            {
                arrowImages[i].color = new Color(1, 1, 1, 0);
                if (arrows[i] != null)
                    arrows[i].transform.localScale = Vector3.one;
            }
        }
    }

    public bool IsOnCooldown(CollisionType type)
    {
        return !IsEffectAllowed(type);
    }

    public void ResetCooldowns()
    {
        InitializeCooldowns();
        lastGlobalEffectTime = 0f;
    }
}