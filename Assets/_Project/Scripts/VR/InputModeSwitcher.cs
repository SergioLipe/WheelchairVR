using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class InputModeSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class HandSide
    {
        public GameObject controllerObject;
        public Transform controllerTransform;
        public GameObject handObject;
    }

    [Header("=== Lados ===")]
    public HandSide leftSide;
    public HandSide rightSide;

    [Header("=== Deteção de comando pousado ===")]
    [Tooltip("Segundos sem movimento até considerar o comando pousado")]
    public float idleTimeout = 1.0f;
    [Tooltip("Movimento mínimo (m) para considerar 'em uso'")]
    public float movementThreshold = 0.002f;

    [Header("=== Debug ===")]
    [SerializeField] private bool leftHandTracked = false;
    [SerializeField] private bool rightHandTracked = false;
    [SerializeField] private bool leftControllerInUse = true;
    [SerializeField] private bool rightControllerInUse = true;

    private XRHandSubsystem handSubsystem;
    private Vector3 lastLeftPos, lastRightPos;
    private float lastLeftMoveTime, lastRightMoveTime;

    void OnEnable()
    {
        TryGetSubsystem();
        lastLeftMoveTime = Time.time;
        lastRightMoveTime = Time.time;
        if (leftSide.controllerTransform != null) lastLeftPos = leftSide.controllerTransform.position;
        if (rightSide.controllerTransform != null) lastRightPos = rightSide.controllerTransform.position;
    }

    void TryGetSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0) handSubsystem = subsystems[0];
    }

    void Update()
    {
        if (handSubsystem == null) TryGetSubsystem();

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
        float moved = Vector3.Distance(t.position, lastPos);
        if (moved > movementThreshold)
        {
            lastMoveTime = Time.time;
            lastPos = t.position;
        }
        return (Time.time - lastMoveTime) < idleTimeout;
    }

    void ApplySide(HandSide side, bool handTracked, bool controllerInUse)
    {
        if (side == null) return;

        // Prioridade: mão tracked > comando em uso > esconder tudo
        bool showHand = handTracked;
        bool showCtrl = !handTracked && controllerInUse;

        if (side.controllerObject != null && side.controllerObject.activeSelf != showCtrl)
            side.controllerObject.SetActive(showCtrl);

        if (side.handObject != null && side.handObject.activeSelf != showHand)
            side.handObject.SetActive(showHand);
    }
}