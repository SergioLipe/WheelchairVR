using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RenderScaleDebug : MonoBehaviour
{
    void Start()
    {
        DumpRenderInfo("Start");
    }

    void Update()
    {
        if (Time.frameCount % 300 == 0) // a cada ~4 segundos
        {
            DumpRenderInfo("Update");
        }
    }

    void DumpRenderInfo(string context)
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        var urp = rp as UniversalRenderPipelineAsset;
        
        if (urp != null)
        {
            Debug.Log($"[RSDebug:{context}] RP={urp.name} Scale={urp.renderScale} MSAA={urp.msaaSampleCount} HDR={urp.supportsHDR} Hash={urp.GetInstanceID()}");
        }
        
        // Verifica se há algum override de XR
        var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (var d in displays)
        {
            if (d.running)
            {
                Debug.Log($"[RSDebug:{context}] XR Display: scaleOfAllRenderTargets={d.scaleOfAllRenderTargets} scaleOfAllViewports={d.scaleOfAllViewports}");
            }
        }
    }
}