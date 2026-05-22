using UnityEngine;

public class FoveationManager : MonoBehaviour
{
    [Tooltip("0=Off, 1=Low, 2=Medium, 3=High, 4=HighTop")]
    [Range(0, 4)]
    public int foveationLevel = 3;

    void Start()
    {
        Debug.Log("[FoveationManager] Start - configuring foveation via OVRManager");
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
            // Fixed Foveated Rendering via OVRManager (Meta XR Core SDK)
            OVRManager.FixedFoveatedRenderingLevel level = (OVRManager.FixedFoveatedRenderingLevel)foveationLevel;
            OVRManager.fixedFoveatedRenderingLevel = level;
            
            // Confirma o que ficou
            var actual = OVRManager.fixedFoveatedRenderingLevel;
            Debug.Log($"[FoveationManager] Set FFR to {level}, OVRManager reports: {actual}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FoveationManager] Failed: {e.Message}\n{e.StackTrace}");
        }
        #endif
    }
}