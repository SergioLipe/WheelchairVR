using UnityEngine;
using UnityEditor;
using System.Linq;

public class ColliderOptimizer : EditorWindow
{
    [MenuItem("Tools/Optimization/Collider Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<ColliderOptimizer>("Collider Optimizer");
    }

    private string prefixFilter = "SM_Prop_TrashBag";
    private enum Action { RemoveAllColliders, ReplaceMeshWithBox, ReplaceMeshWithCapsule, KeepOnlyFirstMeshConvex }
    private Action action = Action.ReplaceMeshWithBox;

    void OnGUI()
    {
        GUILayout.Label("Optimize Colliders by Name Prefix", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Type the prefix (e.g. 'SM_Prop_TrashBag') and the action to apply to ALL matching objects in the scene.", MessageType.Info);

        prefixFilter = EditorGUILayout.TextField("Name prefix", prefixFilter);
        action = (Action)EditorGUILayout.EnumPopup("Action", action);

        GUILayout.Space(10);

        if (GUILayout.Button("Apply to Scene", GUILayout.Height(40)))
        {
            ApplyToScene();
        }

        GUILayout.Space(20);
        GUILayout.Label("Quick Presets:", EditorStyles.boldLabel);

        if (GUILayout.Button("Remove colliders from Clouds + Skydome + Flowers"))
        {
            RemoveCollidersByPrefixes(new[] { "SM_Env_Cloud", "SM_Gen_Env_Skydome", "Flowers", "Ocean_" });
        }

        if (GUILayout.Button("Box-collider TrashBags + Mailbox + Benches + Signs"))
        {
            ReplaceWithBoxByPrefixes(new[] { "SM_Prop_TrashBag", "SM_Prop_Mailbox", "SM_Prop_ParkBench", "SM_Prop_Sign" });
        }

        if (GUILayout.Button("Capsule-collider Hydrants + LightPoles"))
        {
            ReplaceWithCapsuleByPrefixes(new[] { "SM_Prop_Hydrant", "SM_Prop_LightPole" });
        }

        if (GUILayout.Button("Convex single-mesh Subway + Sidewalk"))
        {
            ConvexMeshByPrefixes(new[] { "SM_Env_SubwayEntrance", "SM_Env_Sidewalk" });
        }
    }

    void ApplyToScene()
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith(prefixFilter)).ToArray();
        int processed = 0;
        foreach (var go in all)
        {
            switch (action)
            {
                case Action.RemoveAllColliders: RemoveColliders(go); break;
                case Action.ReplaceMeshWithBox: ReplaceMeshWithBox(go); break;
                case Action.ReplaceMeshWithCapsule: ReplaceMeshWithCapsule(go); break;
                case Action.KeepOnlyFirstMeshConvex: KeepOnlyFirstMeshConvex(go); break;
            }
            processed++;
        }
        Debug.Log($"[ColliderOptimizer] Processed {processed} objects with prefix '{prefixFilter}'");
    }

    void RemoveCollidersByPrefixes(string[] prefixes)
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => prefixes.Any(p => g.name.StartsWith(p))).ToArray();
        foreach (var go in all) RemoveColliders(go);
        Debug.Log($"[ColliderOptimizer] Removed colliders from {all.Length} objects.");
    }

    void ReplaceWithBoxByPrefixes(string[] prefixes)
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => prefixes.Any(p => g.name.StartsWith(p))).ToArray();
        foreach (var go in all) ReplaceMeshWithBox(go);
        Debug.Log($"[ColliderOptimizer] Replaced with Box on {all.Length} objects.");
    }

    void ReplaceWithCapsuleByPrefixes(string[] prefixes)
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => prefixes.Any(p => g.name.StartsWith(p))).ToArray();
        foreach (var go in all) ReplaceMeshWithCapsule(go);
        Debug.Log($"[ColliderOptimizer] Replaced with Capsule on {all.Length} objects.");
    }

    void ConvexMeshByPrefixes(string[] prefixes)
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => prefixes.Any(p => g.name.StartsWith(p))).ToArray();
        foreach (var go in all) KeepOnlyFirstMeshConvex(go);
        Debug.Log($"[ColliderOptimizer] Convex'd {all.Length} objects.");
    }

    void RemoveColliders(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) Undo.DestroyObjectImmediate(c);
    }

    void ReplaceMeshWithBox(GameObject go)
    {
        var meshCols = go.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshCols)
        {
            GameObject target = mc.gameObject;
            // Calculate bounds from MeshRenderer or MeshFilter before removing
            Bounds? bounds = null;
            var mr = target.GetComponent<MeshRenderer>();
            if (mr != null) bounds = mr.localBounds;
            else
            {
                var mf = target.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) bounds = mf.sharedMesh.bounds;
            }
            Undo.DestroyObjectImmediate(mc);
            // Only add Box if there isn't one already
            if (target.GetComponent<BoxCollider>() == null)
            {
                var box = Undo.AddComponent<BoxCollider>(target);
                if (bounds.HasValue)
                {
                    box.center = bounds.Value.center;
                    box.size = bounds.Value.size;
                }
            }
        }
    }

    void ReplaceMeshWithCapsule(GameObject go)
    {
        var meshCols = go.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshCols)
        {
            GameObject target = mc.gameObject;
            Bounds? bounds = null;
            var mr = target.GetComponent<MeshRenderer>();
            if (mr != null) bounds = mr.localBounds;
            else
            {
                var mf = target.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) bounds = mf.sharedMesh.bounds;
            }
            Undo.DestroyObjectImmediate(mc);
            if (target.GetComponent<CapsuleCollider>() == null)
            {
                var cap = Undo.AddComponent<CapsuleCollider>(target);
                if (bounds.HasValue)
                {
                    cap.center = bounds.Value.center;
                    cap.height = bounds.Value.size.y;
                    cap.radius = Mathf.Max(bounds.Value.size.x, bounds.Value.size.z) * 0.5f;
                    cap.direction = 1; // Y axis
                }
            }
        }
    }

    void KeepOnlyFirstMeshConvex(GameObject go)
    {
        var meshCols = go.GetComponentsInChildren<MeshCollider>(true);
        // Group by GameObject and keep only the first MeshCollider per object
        var groups = meshCols.GroupBy(mc => mc.gameObject).ToArray();
        foreach (var group in groups)
        {
            var list = group.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                if (i == 0)
                {
                    list[i].convex = true;
                }
                else
                {
                    Undo.DestroyObjectImmediate(list[i]);
                }
            }
        }
    }
}