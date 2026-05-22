using UnityEngine;
using UnityEngine.SceneManagement;

public class FoveationManager : MonoBehaviour
{
    [Header("Foveation Settings")]
    [Tooltip("0=Off, 1=Low, 2=Medium, 3=High, 4=HighTop")]
    [Range(0, 4)]
    public int foveationLevel = 4;  // 4 = HighTop, mais agressiva

    private static FoveationManager instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        Debug.Log("[FoveationManager] Start - applying foveation");
        ApplyFoveation();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[FoveationManager] Scene '{scene.name}' loaded - reapplying foveation");
        ApplyFoveation();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyFoveation();
    }

    public void ApplyFoveation()
    {
        #if !UNITY_EDITOR && UNITY_ANDROID
        try
        {
            OVRManager.FixedFoveatedRenderingLevel level = (OVRManager.FixedFoveatedRenderingLevel)foveationLevel;
            OVRManager.fixedFoveatedRenderingLevel = level;
            var actual = OVRManager.fixedFoveatedRenderingLevel;
            Debug.Log($"[FoveationManager] Set FFR to {level}, OVRManager reports: {actual}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FoveationManager] Failed: {e.Message}");
        }
        #endif
    }
}