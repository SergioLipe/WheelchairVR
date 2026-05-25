using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class VRPerformanceManager : MonoBehaviour
{
    [Header("Render Scale")]
    [Range(0.3f, 1.5f)]
    public float renderScale = 0.8f;

    [Header("Foveation")]
    [Range(0, 4)]
    public int foveationLevel = 4;

    [Header("Refresh Rate")]
    public float targetRefreshRate = 72.0f;

    private static VRPerformanceManager instance;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => Invoke(nameof(ApplyAll), 1.0f);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

    void OnApplicationFocus(bool hasFocus) { if (hasFocus) ApplyAll(); }

    public void ApplyAll()
    {
        #if !UNITY_EDITOR && UNITY_ANDROID
        // 1. CRÍTICO: XRSettings.eyeTextureResolutionScale (caminho oficial Unity XR)
        XRSettings.eyeTextureResolutionScale = renderScale;
        XRSettings.renderViewportScale = renderScale;
        Debug.Log($"[VRPerf] XRSettings: eyeTextureResolutionScale={XRSettings.eyeTextureResolutionScale} renderViewportScale={XRSettings.renderViewportScale}");

        // 2. URP renderScale como fallback
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline 
                  as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = renderScale;
            Debug.Log($"[VRPerf] URP renderScale={urp.renderScale}");
        }

        // 3. OVRManager (já vimos que é ignorado mas mantém por segurança)
        if (OVRManager.instance != null)
        {
            OVRManager.instance.minRenderScale = renderScale;
            OVRManager.instance.maxRenderScale = renderScale;
        }

        // 4. Refresh Rate
        try
        {
            OVRManager.display.displayFrequency = targetRefreshRate;
        }
        catch { }

        // 5. Foveation
        try
        {
            OVRManager.useDynamicFoveatedRendering = false;
            OVRManager.fixedFoveatedRenderingLevel = (OVRManager.FixedFoveatedRenderingLevel)foveationLevel;
        }
        catch { }

        Debug.Log($"[VRPerf] APPLIED COMPLETE");
        #endif
    }

    void Update()
    {
        #if !UNITY_EDITOR && UNITY_ANDROID
        if (Time.frameCount % 60 == 0)
        {
            float current = XRSettings.eyeTextureResolutionScale;
            Debug.Log($"[VRPerf:STATUS] XRSettings.eyeTextureResolutionScale={current} eyeTextureWidth={XRSettings.eyeTextureWidth} eyeTextureHeight={XRSettings.eyeTextureHeight}");
            
            // Reaplica se algo sobrescreveu
            if (Mathf.Abs(current - renderScale) > 0.01f)
            {
                Debug.LogWarning($"[VRPerf:OVERRIDE_DETECTED] eyeTextureResolutionScale was {current}, restoring to {renderScale}");
                XRSettings.eyeTextureResolutionScale = renderScale;
                XRSettings.renderViewportScale = renderScale;
            }
        }
        #endif
    }
}