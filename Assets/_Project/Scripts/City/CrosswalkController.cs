using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages player waiting areas and car stopping areas at crosswalks.
/// Optimized: uses NonAlloc, throttling, and reused buffers.
/// </summary>
public class CrosswalkController : MonoBehaviour
{
    [Header("=== Zone Setup ===")]
    public BoxCollider[] playerZones;
    public BoxCollider[] carZones;

    [Header("=== Advanced Stopping Logic ===")]
    public float crosswalkEntryMargin = 0f;
    public float timeToWaitBeforeStopping = 3f;

    [Header("=== Optimization ===")]
    [Tooltip("Layers that contain player and cars. Filtering = huge perf gain")]
    public LayerMask detectionLayerMask = ~0;

    [Tooltip("How often to update (per second). Lower = better performance")]
    [Range(2, 30)]
    public int updatesPerSecond = 10;

    // [OPT] Pre-allocated buffer (zero garbage)
    private static readonly Collider[] s_OverlapBuffer = new Collider[16];

    // [OPT] Reused lists (não cria novas todos os frames)
    private readonly List<CarCityMovement> yieldingCars = new List<CarCityMovement>(16);
    private readonly List<CarCityMovement> carsCurrentlyInZone = new List<CarCityMovement>(16);

    private float playerWaitTimer = 0f;
    private float lastUpdateTime = 0f;
    private float updateInterval;

    void Start()
    {
        updateInterval = 1f / updatesPerSecond;
    }

    void Update()
    {
        if (playerZones == null || carZones == null || playerZones.Length == 0 || carZones.Length == 0)
        {
            return;
        }

        // [OPT] Throttle — não checa cada frame
        if (Time.time - lastUpdateTime < updateInterval) return;
        float dt = Time.time - lastUpdateTime;
        lastUpdateTime = Time.time;

        bool playerDetected = CheckForPlayer();

        if (playerDetected)
        {
            playerWaitTimer += dt;
        }
        else
        {
            playerWaitTimer = 0f;
        }

        bool shouldStopCars = playerWaitTimer >= timeToWaitBeforeStopping;

        ControlCars(shouldStopCars);
    }

    private bool CheckForPlayer()
    {
        for (int i = 0; i < playerZones.Length; i++)
        {
            BoxCollider pZone = playerZones[i];
            if (pZone == null) continue;

            // [OPT] cache transform calls
            Transform pZoneTr = pZone.transform;
            Vector3 boxCenter = pZoneTr.TransformPoint(pZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(pZone.size, pZoneTr.lossyScale) * 0.5f;

            // [OPT] OverlapBoxNonAlloc — zero garbage
            int hitCount = Physics.OverlapBoxNonAlloc(
                boxCenter, boxHalfExtents, s_OverlapBuffer,
                pZoneTr.rotation, detectionLayerMask);

            for (int j = 0; j < hitCount; j++)
            {
                Collider hit = s_OverlapBuffer[j];
                if (hit.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void ControlCars(bool shouldStop)
    {
        // [OPT] Clear reused list (não cria nova)
        carsCurrentlyInZone.Clear();

        for (int i = 0; i < carZones.Length; i++)
        {
            BoxCollider cZone = carZones[i];
            if (cZone == null) continue;

            Transform cZoneTr = cZone.transform;
            Vector3 boxCenter = cZoneTr.TransformPoint(cZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(cZone.size, cZoneTr.lossyScale) * 0.5f;

            int hitCount = Physics.OverlapBoxNonAlloc(
                boxCenter, boxHalfExtents, s_OverlapBuffer,
                cZoneTr.rotation, detectionLayerMask);

            for (int j = 0; j < hitCount; j++)
            {
                Collider hit = s_OverlapBuffer[j];

                // [OPT] GetComponent first (faster than GetComponentInParent)
                CarCityMovement car = hit.GetComponent<CarCityMovement>();
                if (car == null) car = hit.GetComponentInParent<CarCityMovement>();
                if (car == null) continue;

                // [OPT] Contains numa lista pequena é rápido, mantemos
                if (carsCurrentlyInZone.Contains(car)) continue;

                carsCurrentlyInZone.Add(car);

                bool forceStop = shouldStop;

                if (forceStop)
                {
                    // Check if car already inside crosswalk
                    for (int k = 0; k < playerZones.Length; k++)
                    {
                        BoxCollider pZone = playerZones[k];
                        if (pZone == null) continue;

                        Transform pZoneTr = pZone.transform;
                        Vector3 localPos = pZoneTr.InverseTransformPoint(car.transform.position);
                        Vector3 extents = pZone.size * 0.5f;

                        float checkX = Mathf.Max(0, extents.x - crosswalkEntryMargin);
                        float checkZ = Mathf.Max(0, extents.z - crosswalkEntryMargin);

                        if (Mathf.Abs(localPos.x) <= checkX &&
                            Mathf.Abs(localPos.y) <= extents.y + 2f &&
                            Mathf.Abs(localPos.z) <= checkZ)
                        {
                            forceStop = false;
                            break;
                        }
                    }
                }

                car.isYielding = forceStop;

                if (forceStop && !yieldingCars.Contains(car))
                {
                    yieldingCars.Add(car);
                }
            }
        }

        // Free cars that left the zone
        for (int i = yieldingCars.Count - 1; i >= 0; i--)
        {
            CarCityMovement pastCar = yieldingCars[i];

            if (pastCar == null)
            {
                yieldingCars.RemoveAt(i);
                continue;
            }

            if (!carsCurrentlyInZone.Contains(pastCar))
            {
                pastCar.isYielding = false;
                yieldingCars.RemoveAt(i);
            }
            else if (!pastCar.isYielding)
            {
                yieldingCars.RemoveAt(i);
            }
        }
    }
}