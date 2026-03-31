using UnityEngine;
using System.Collections.Generic;

public class WheelchairViewFix : MonoBehaviour
{
    [Header("=== Configuration ===")]
    public Camera mainCamera;

    [Header("=== Body Renderers (any type) ===")]
    [Tooltip("Drag here: Beta_Surface, tshirt, pants, shoes - accepts any Renderer")]
    public List<Renderer> bodyRenderers = new List<Renderer>();

    [Header("=== Bones to Hide ===")]
    public List<Transform> bonesToHide = new List<Transform>();

    [Range(0.0001f, 0.01f)]
    public float hiddenScale = 0.001f;

    [Header("=== Neck Plug ===")]
    [Tooltip("Transform where the neck plug will be created (drag mixamorig:Neck here)")]
    public Transform neckBone;
    [Tooltip("Color of the plug - match skin or make it dark")]
    public Color plugColor = new Color(0.2f, 0.15f, 0.12f, 1f);
    [Tooltip("Size of the neck plug")]
    public float plugRadius = 0.08f;

    private Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
    private GameObject neckPlug;

    void Awake()
    {
        foreach (var bone in bonesToHide)
        {
            if (bone != null)
                originalScales[bone] = bone.localScale;
        }
    }

    void OnEnable()
    {
        SetupCamera();
        ApplyDoubleSidedRendering();
        HideBones();
        CreateNeckPlug();
    }

    void OnDisable()
    {
        RestoreBones();
        DestroyNeckPlug();
    }

    private void SetupCamera()
    {
        if (mainCamera == null)
            mainCamera = GetComponentInChildren<Camera>();

        if (mainCamera != null)
            mainCamera.nearClipPlane = 0.01f;
    }

    private void ApplyDoubleSidedRendering()
    {
        foreach (var rend in bodyRenderers)
        {
            if (rend == null) continue;

            // Works with both MeshRenderer and SkinnedMeshRenderer
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < rend.materials.Length; i++)
            {
                newMats[i] = new Material(rend.materials[i]);
                newMats[i].SetInt("_Cull", 0);
            }
            rend.materials = newMats;
        }
    }

    private void HideBones()
    {
        foreach (var bone in bonesToHide)
        {
            if (bone != null)
                bone.localScale = Vector3.one * hiddenScale;
        }
    }

    private void RestoreBones()
    {
        foreach (var bone in bonesToHide)
        {
            if (bone != null && originalScales.ContainsKey(bone))
                bone.localScale = originalScales[bone];
        }
    }

    private void CreateNeckPlug()
    {
        if (neckBone == null) return;

        // Create a sphere that plugs the neck hole
        neckPlug = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        neckPlug.name = "NeckPlug";
        neckPlug.transform.SetParent(neckBone);
        neckPlug.transform.localPosition = Vector3.zero;
        neckPlug.transform.localScale = Vector3.one * plugRadius;

        // Remove collider (we don't need physics on this)
        var col = neckPlug.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Apply a simple unlit dark material
        var rend = neckPlug.GetComponent<Renderer>();
        Material plugMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        plugMat.color = plugColor;
        plugMat.SetInt("_Cull", 0);
        rend.material = plugMat;
    }

    private void DestroyNeckPlug()
    {
        if (neckPlug != null)
            Destroy(neckPlug);
    }
}