using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR Tunneling Effect (Vignette) — anti-motion-sickness.
/// Compatível com CharacterController (sem Rigidbody).
/// Gera textura de vignette procedural automaticamente (não precisa de asset).
/// Calibrado para velocidades baixas (cadeira de rodas eléctrica).
/// </summary>
[DisallowMultipleComponent]
public class VRTunnelingEffect : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("Drag the wheelchair MovementVR script here")]
    public MovementVR wheelchairMovement;

    [Tooltip("Drag the Vignette Image (UI) here")]
    public Image vignetteImage;

    [Header("=== Vignette Strength ===")]
    [Tooltip("Máxima opacidade do vignette. 1.0 = praticamente preto nas bordas")]
    [Range(0f, 1f)]
    public float maxDarkness = 0.95f;

    [Tooltip("Opacidade mínima sempre visível (mesmo parado). 0 = invisível, 0.05 = ligeira moldura")]
    [Range(0f, 0.3f)]
    public float idleDarkness = 0.0f;

    [Header("=== Speed Triggers ===")]
    [Tooltip("Velocidade mínima (m/s) para começar a aparecer")]
    public float speedThreshold = 0.1f;

    [Tooltip("Velocidade onde o vignette está no máximo (m/s). Para cadeira: ~1.0 normal, ~1.5 com folga")]
    public float maxSpeedForFullEffect = 1.0f;

    [Header("=== Rotation Triggers ===")]
    [Tooltip("Velocidade rotacional mínima (°/s) para começar a aparecer")]
    public float turnSpeedThreshold = 8f;

    [Tooltip("Rotação onde o vignette está no máximo (°/s)")]
    public float maxTurnSpeedForFullEffect = 35f;

    [Header("=== Smoothness ===")]
    [Tooltip("Velocidade de fade-in/out. Maior = mais rápido")]
    public float fadeSpeed = 8f;

    [Tooltip("Curva de intensidade. <1 = sobe rápido no início (mais protector). >1 = sobe devagar")]
    [Range(0.3f, 2f)]
    public float intensityCurve = 0.7f;

    [Tooltip("Combina velocidade e rotação somando-as (mais agressivo) em vez de usar só o maior")]
    public bool combineMotionAndRotation = true;

    [Header("=== Vignette Texture Generation ===")]
    [Tooltip("Resolução da textura procedural (256 é suficiente)")]
    public int vignetteResolution = 256;

    [Tooltip("Raio do círculo central limpo (0=tudo escuro, 1=quase nada de escuro). Menor = vignette mais forte")]
    [Range(0.1f, 0.9f)]
    public float vignetteRadius = 0.35f;

    [Tooltip("Suavidade da transição (0=corte abrupto, 1=transição muito longa)")]
    [Range(0.05f, 1f)]
    public float vignetteSmoothness = 0.45f;

    [Header("=== Debug ===")]
    [Tooltip("Mostra valores em runtime no Inspector")]
    public bool showDebug = false;

    // --- Runtime ---
    private Transform wheelchairTransform;
    private float previousYRotation;
    private float currentAlpha = 0f;
    private Color cachedColor;

    // --- Debug (read-only no Inspector durante Play) ---
    [SerializeField, HideInInspector] private float debugSpeed;
    [SerializeField, HideInInspector] private float debugTurn;
    [SerializeField, HideInInspector] private float debugIntensity;

    private void Start()
    {
        

        if (wheelchairMovement != null)
        {
            wheelchairTransform = wheelchairMovement.transform;
            previousYRotation = wheelchairTransform.eulerAngles.y;
        }
        else
        {
            Debug.LogError("[VRTunnelingEffect] wheelchairMovement não está atribuído!");
        }

        if (vignetteImage != null)
        {
            cachedColor = Color.black;
            cachedColor.a = idleDarkness;
            vignetteImage.color = cachedColor;
        }
        else
        {
            Debug.LogError("[VRTunnelingEffect] vignetteImage não está atribuído!");
        }
    }

    private void Update()
    {
        if (wheelchairMovement == null || vignetteImage == null || wheelchairTransform == null) return;

        // --- Calcular rotation speed (°/s) manualmente ---
        float currentYRotation = wheelchairTransform.eulerAngles.y;
        float deltaY = Mathf.DeltaAngle(previousYRotation, currentYRotation);
        float turnSpeedDegPerSec = Mathf.Abs(deltaY) / Mathf.Max(Time.deltaTime, 0.0001f);
        previousYRotation = currentYRotation;

        // --- Linear speed (m/s) ---
        float currentSpeed = Mathf.Abs(wheelchairMovement.GetCurrentSpeed());

        // --- Speed factor com curva exponencial ---
        float speedFactor = 0f;
        if (currentSpeed > speedThreshold)
        {
            speedFactor = Mathf.InverseLerp(speedThreshold, maxSpeedForFullEffect, currentSpeed);
            speedFactor = Mathf.Pow(speedFactor, intensityCurve);
        }

        // --- Turn factor com curva exponencial ---
        float turnFactor = 0f;
        if (turnSpeedDegPerSec > turnSpeedThreshold)
        {
            turnFactor = Mathf.InverseLerp(turnSpeedThreshold, maxTurnSpeedForFullEffect, turnSpeedDegPerSec);
            turnFactor = Mathf.Pow(turnFactor, intensityCurve);
        }

        // --- Combinar speed + turn ---
        float intensity;
        if (combineMotionAndRotation)
        {
            // Magnitude vectorial: andar+rodar simultaneamente = vignette mais forte
            intensity = Mathf.Sqrt(speedFactor * speedFactor + turnFactor * turnFactor);
            intensity = Mathf.Clamp01(intensity);
        }
        else
        {
            intensity = Mathf.Max(speedFactor, turnFactor);
        }

        // --- Aplicar com idle baseline ---
        float targetAlpha = Mathf.Lerp(idleDarkness, maxDarkness, intensity);

        // --- Suavização ---
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        cachedColor.a = currentAlpha;
        vignetteImage.color = cachedColor;

        // --- Debug ---
        if (showDebug)
        {
            debugSpeed = currentSpeed;
            debugTurn = turnSpeedDegPerSec;
            debugIntensity = intensity;
        }
    }

    /// <summary>
    /// Gera uma textura de vignette procedural com gradient radial.
    /// Não precisas de importar nenhum asset.
    /// </summary>
    private void GenerateVignetteTexture()
    {
        if (vignetteImage == null) return;

        int res = vignetteResolution;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "VignetteProceduralTex"
        };

        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float maxDist = res * 0.5f;

        Color[] pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                // dist=0 no centro, dist≈1 nos cantos

                // SmoothStep: transição suave entre raio interior e exterior
                float alpha = Mathf.SmoothStep(
                    vignetteRadius,
                    vignetteRadius + vignetteSmoothness,
                    dist
                );

                pixels[y * res + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = "VignetteProceduralSprite";

        vignetteImage.sprite = sprite;
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.preserveAspect = false;

        // Cor base: preto puro com alfa controlado pelo script
        vignetteImage.color = new Color(0f, 0f, 0f, idleDarkness);
    }

    /// <summary>
    /// Permite regenerar a textura em runtime se mudares os parâmetros no Inspector
    /// </summary>
    [ContextMenu("Regenerate Vignette Texture")]
    public void RegenerateTexture()
    {
        GenerateVignetteTexture();
    }

    private void OnValidate()
    {
        // Não pode regenerar textura no OnValidate (fora de Play mode dá erro silencioso)
        // Mas garante que valores fazem sentido
        if (speedThreshold >= maxSpeedForFullEffect)
            speedThreshold = maxSpeedForFullEffect * 0.5f;

        if (turnSpeedThreshold >= maxTurnSpeedForFullEffect)
            turnSpeedThreshold = maxTurnSpeedForFullEffect * 0.5f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Quando o utilizador tira o headset, força vignette transparente
        if (!hasFocus && vignetteImage != null)
        {
            cachedColor.a = 0f;
            vignetteImage.color = cachedColor;
            currentAlpha = 0f;
        }
    }
}