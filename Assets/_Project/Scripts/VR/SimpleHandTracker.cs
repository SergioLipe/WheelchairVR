using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using System.Collections.Generic;

/// <summary>
/// Faz um Transform seguir o pulso de uma mão tracked pelo XR Hands.
/// Coloca este componente no RightHandModel (ou num GameObject pai do visual).
/// </summary>
public class SimpleHandTracker : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Tooltip("Qual mão seguir")]
    public Handedness handedness = Handedness.Right;

    [Tooltip("Esconder o visual quando a mão não está tracked")]
    public bool hideWhenNotTracked = true;

    [Tooltip("Objeto visual a esconder (se vazio, usa este GameObject)")]
    public GameObject visualRoot;

    private XRHandSubsystem handSubsystem;

    void OnEnable()
    {
        TryGetSubsystem();
    }

    void TryGetSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];
    }

    void Update()
    {
        if (handSubsystem == null)
        {
            TryGetSubsystem();
            if (handSubsystem == null) return;
        }

        XRHand hand = (handedness == Handedness.Right)
            ? handSubsystem.rightHand
            : handSubsystem.leftHand;

        bool tracked = hand.isTracked;
        GameObject toHide = visualRoot != null ? visualRoot : gameObject;

        if (!tracked)
        {
            if (hideWhenNotTracked && toHide.activeSelf) toHide.SetActive(false);
            return;
        }

        if (hideWhenNotTracked && !toHide.activeSelf) toHide.SetActive(true);

        // Pose do pulso no espaço do XR Origin
        var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        if (wristJoint.TryGetPose(out Pose pose))
        {
            // Aplicar como local em relação ao XR Origin (pai)
            transform.localPosition = pose.position;
            transform.localRotation = pose.rotation;
        }
    }
}