using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Independently switches between controller and hand on each side.
/// Uses local position to detect controller movement, so wheelchair
/// movement doesn't falsely mark a resting controller as "in use".
/// Priority per side: tracked hand > active controller > hide both.
/// </summary>
public class InputModeSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class HandSide
    {
        [Tooltip("Visual GameObject of the physical controller on this side")]
        public GameObject controllerObject;

        [Tooltip("Transform that receives the Tracked Pose Driver updates (the parent, never disabled)")]
        public Transform controllerTransform;

        [Tooltip("GameObject of the tracked hand on this side")]
        public GameObject handObject;
    }

    [Header("=== Lado Esquerdo ===")]
    public HandSide leftSide;

    [Header("=== Lado Direito ===")]
    public HandSide rightSide;

    [Header("=== Deteção de comando pousado ===")]
    [Tooltip("Seconds without movement before considering the controller resting")]
    public float idleTimeout = 1.0f;

    [Tooltip("Minimum movement (meters) to count as 'in use'")]
    public float movementThreshold = 0.002f;

    [Header("=== Debug ===")]
    [SerializeField] private bool leftHandTracked = false;
    [SerializeField] private bool rightHandTracked = false;
    [SerializeField] private bool leftControllerInUse = true;
    [SerializeField] private bool rightControllerInUse = true;

    private XRHandSubsystem handSubsystem;

    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private float lastLeftMoveTime;
    private float lastRightMoveTime;

    void OnEnable()
    {
        TryGetSubsystem();

        lastLeftMoveTime = Time.time;
        lastRightMoveTime = Time.time;

        if (leftSide.controllerTransform != null)
            lastLeftPos = leftSide.controllerTransform.localPosition;
        if (rightSide.controllerTransform != null)
            lastRightPos = rightSide.controllerTransform.localPosition;
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
            TryGetSubsystem();

        leftHandTracked  = handSubsystem != null && handSubsystem.leftHand.isTracked;
        rightHandTracked = handSubsystem != null && handSubsystem.rightHand.isTracked;

        leftControllerInUse  = IsInUse(leftSide.controllerTransform,  ref lastLeftPos,  ref lastLeftMoveTime);
        rightControllerInUse = IsInUse(rightSide.controllerTransform, ref lastRightPos, ref lastRightMoveTime);

        ApplySide(leftSide,  leftHandTracked,  leftControllerInUse);
        ApplySide(rightSide, rightHandTracked, rightControllerInUse);
    }

    bool IsInUse(Transform t, ref Vector3 lastPos, ref float lastMoveTime)
    {
        if (t == null) return false;

        // Use LOCAL position so wheelchair movement doesn't count as controller movement
        Vector3 currentPos = t.localPosition;
        float moved = Vector3.Distance(currentPos, lastPos);

        if (moved > movementThreshold)
        {
            lastMoveTime = Time.time;
            lastPos = currentPos;
        }

        return (Time.time - lastMoveTime) < idleTimeout;
    }

    void ApplySide(HandSide side, bool handTracked, bool controllerInUse)
    {
        if (side == null) return;

        // Priority: tracked hand wins; otherwise active controller; otherwise hide both
        bool showHand = handTracked;
        bool showCtrl = !handTracked && controllerInUse;

        if (side.controllerObject != null && side.controllerObject.activeSelf != showCtrl)
            side.controllerObject.SetActive(showCtrl);

        if (side.handObject != null && side.handObject.activeSelf != showHand)
            side.handObject.SetActive(showHand);
    }
}