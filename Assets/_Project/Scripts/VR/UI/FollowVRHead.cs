using UnityEngine;

public class FollowVRHead : MonoBehaviour
{
    public Transform vrCamera;
    
    void LateUpdate()
    {
        if (vrCamera == null) return;
        transform.position = vrCamera.position;
        transform.rotation = vrCamera.rotation;
    }
}