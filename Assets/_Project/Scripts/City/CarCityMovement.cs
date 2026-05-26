using UnityEngine;

/// <summary>
/// Controls city traffic with straight-line driving and curve-turning at intersections.
/// Features: Stop Zones, NonStop Zones, Player Detection, Failsafes, Auto-Teleport Recovery,
/// Stationary Failsafe, External Trigger-based Turning, and Two-Clip Engine Audio.
/// Optimized for Meta Quest 3 VR performance.
/// </summary>
public class CarCityMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    public float speed = 10f;
    public bool canMove = true;

    [Header("=== Zone Settings ===")]
    public string targetStopZoneTag = "StopZone";
    public string neverStopZoneTag = "NonStopZone";

    [Header("=== Collision Sensor Settings ===")]
    public float sensorFrontOffset = 1.4f;
    public float frontSensorLength = 4.5f;
    public float obliqueSensorLength = 4f;
    public float obliqueSensorAngle = 25f;

    [Header("=== Sensor Optimization ===")]
    [Tooltip("Layers the sensors should detect. Set to only relevant layers (Cars, Player) for big performance gains!")]
    public LayerMask sensorLayerMask = ~0;

    [Tooltip("Skip sensor checks every N frames. 1 = every frame, 2 = every other frame, 3 = every 3rd frame.")]
    [Range(1, 5)]
    public int sensorUpdateFrequency = 2;

    [Header("=== Player Detection Settings ===")]
    [Tooltip("Tempo (segundos) que o carro espera antes de arrancar depois de deixar de detectar o Player")]
    public float playerResumeDelay = 1.5f;

    [Header("=== Stuck Failsafe Settings ===")]
    public float maxWaitTime = 5f;

    [Header("=== Teleport Recovery Settings ===")]
    public float maxTimeOffMagnet = 5f;
    public float maxStationaryTime = 15f;

    [HideInInspector]
    public float timeOffMagnet = 0f;

    [Header("=== Audio Settings ===")]
    public AudioClip idleSound;
    public AudioClip movingSound;
    public float engineVolume = 0.3f;

    [Header("=== Debug ===")]
    [Tooltip("Draw sensor rays in Scene view (Editor only — auto-disabled in builds)")]
    public bool drawDebugRays = false;

    // Private audio
    private AudioSource engineAudio;
    private bool wasMoving = false;

    // Internal State
    private bool isInStopZone = false;
    private bool isInNeverStopZone = false;
    private float stuckTimer = 0f;
    private bool ignoreObliqueCars = false;
    private float recoveryTimer = 0f;

    // Player detection timing
    private bool playerDetectedThisFrame = false;
    private bool playerDetectedLastFrame = false;
    private float playerClearTimer = 0f;

    [HideInInspector]
    public bool isYielding = false;

    // Stationary Failsafe
    private float stationaryTimer = 0f;
    private Vector3 lastPosition;

    // Turning Logic
    [HideInInspector] public bool isTurning = false;
    private float degreesTurned = 0f;
    private float currentTargetAngle = 0f;
    private float turnDirection = 1f;
    private float currentTurnSpeed = 120f;
    private float currentSpeedDuringTurn = 3f;

    // [OPT] Cache de transform
    private Transform myTransform;

    // [OPT] Pre-allocated buffer para Physics queries (zero garbage!)
    private static readonly RaycastHit[] s_RaycastBuffer = new RaycastHit[8];

    // [OPT] Cache do instance ID (não muda nunca)
    private int cachedInstanceID;

    // [OPT] Cache dos resultados dos sensores (não recalcula todos os frames)
    private bool cachedCenterBlocked = false;
    private bool cachedObliqueBlocked = false;
    private int frameCounter = 0;

    // [OPT] Cache do offset vertical do sensor (não muda nunca)
    private static readonly Vector3 SENSOR_HEIGHT_OFFSET = new Vector3(0f, 0.5f, 0f);

    void Awake()
    {
        myTransform = transform;
        cachedInstanceID = gameObject.GetInstanceID();
    }

    void Start()
    {
        lastPosition = myTransform.position;

        // Auto-generate AudioSource
        if (idleSound != null && movingSound != null)
        {
            engineAudio = gameObject.AddComponent<AudioSource>();
            engineAudio.loop = true;
            engineAudio.spatialBlend = 1f;
            engineAudio.rolloffMode = AudioRolloffMode.Linear;
            engineAudio.minDistance = 2f;
            engineAudio.maxDistance = 15f;
            engineAudio.volume = engineVolume;
            engineAudio.clip = idleSound;
            engineAudio.Play();
            wasMoving = false;
        }
        else
        {
            Debug.LogWarning($"[Traffic System] Missing Idle or Moving sound on {gameObject.name}!");
        }
    }

    void Update()
    {
        // [OPT] cache deltaTime once
        float dt = Time.deltaTime;

        // [OPT] Throttle sensor checks (carros não precisam checar cada frame)
        // Cada carro tem um offset diferente baseado no instance ID — evita todos a checar no mesmo frame
        frameCounter++;
        bool shouldCheckSensors = (frameCounter % sensorUpdateFrequency) == (cachedInstanceID % sensorUpdateFrequency);

        if (shouldCheckSensors)
        {
            CheckSensors(out cachedCenterBlocked, out cachedObliqueBlocked);
        }

        bool centerBlocked = cachedCenterBlocked;
        bool obliqueBlocked = cachedObliqueBlocked;

        // ==== Player detection delay logic ====
        if (shouldCheckSensors)
        {
            if (playerDetectedThisFrame)
            {
                // Está a ver o Player AGORA
                playerClearTimer = 0f;
                playerDetectedLastFrame = true;
            }
            else if (playerDetectedLastFrame)
            {
                // Deixou de ver o Player — começa a contar
                playerClearTimer += dt * sensorUpdateFrequency; // compensa skip de frames
                if (playerClearTimer >= playerResumeDelay)
                {
                    // Cooldown completo — limpa flag
                    playerDetectedLastFrame = false;
                    playerClearTimer = 0f;
                }
            }
        }

        // 2. Check legal movement
        bool wantsToMove = (canMove || !isInStopZone) && !isYielding;

        // 3. NON-STOP ZONE OVERRIDE
        if (isInNeverStopZone)
        {
            wantsToMove = !isYielding;
        }

        // 4. TELEPORT FAILSAFE TIMER (Off Magnet)
        if (isInStopZone || !canMove || isYielding)
        {
            timeOffMagnet = 0f;
        }
        else
        {
            timeOffMagnet += dt;
            if (timeOffMagnet >= maxTimeOffMagnet)
            {
                TeleportToNextSpawn();
                return;
            }
        }

        // 5. STATIONARY FAILSAFE TIMER (uses sqrMagnitude — faster than Distance)
        Vector3 currentPos = myTransform.position;
        Vector3 posDelta = currentPos - lastPosition;
        bool isPhysicallyStopped = posDelta.sqrMagnitude < 0.0001f; // 0.01^2

        if (isPhysicallyStopped)
        {
            bool isLegallyWaiting = (isInStopZone && !canMove) || isYielding;
            if (!isLegallyWaiting)
            {
                stationaryTimer += dt;
                if (stationaryTimer >= maxStationaryTime)
                {
                    Debug.Log($"[Traffic System] Car blocked for {maxStationaryTime}s! Teleporting...");
                    TeleportToNextSpawn();
                    return;
                }
            }
            else
            {
                stationaryTimer = 0f;
            }
        }
        else
        {
            stationaryTimer = 0f;
        }

        lastPosition = currentPos;

        // 6. SAFETY SENSOR & OBSTACLES LOGIC
        if (centerBlocked)
        {
            stuckTimer = 0f;
            ignoreObliqueCars = false;
            recoveryTimer = 0f;
        }
        else if (obliqueBlocked && !ignoreObliqueCars)
        {
            if (wantsToMove)
            {
                stuckTimer += dt;
                if (stuckTimer >= maxWaitTime)
                {
                    ignoreObliqueCars = true;
                    recoveryTimer = 0f;
                }
            }
        }
        else if (!centerBlocked && !obliqueBlocked)
        {
            stuckTimer = 0f;
            ignoreObliqueCars = false;
            recoveryTimer = 0f;
        }

        // 7. SENSOR RECOVERY
        if (ignoreObliqueCars && wantsToMove)
        {
            recoveryTimer += dt;
            if (recoveryTimer >= 2.0f)
            {
                ignoreObliqueCars = false;
                stuckTimer = 0f;
                recoveryTimer = 0f;
            }
        }

        // 8. APPLY MOVEMENT AND AUDIO
        // Adicionado: durante o cooldown do Player, não anda mesmo sem obstáculos
        bool inPlayerCooldown = playerDetectedLastFrame && playerClearTimer < playerResumeDelay;
        bool isActuallyMoving = wantsToMove && !centerBlocked && (!obliqueBlocked || ignoreObliqueCars) && !inPlayerCooldown;

        if (isActuallyMoving)
        {
            ApplyMovementAndTurning(dt);
        }

        // 9. AUDIO STATE CHANGE
        if (engineAudio != null)
        {
            if (isActuallyMoving && !wasMoving)
            {
                engineAudio.clip = movingSound;
                engineAudio.Play();
                wasMoving = true;
            }
            else if (!isActuallyMoving && wasMoving)
            {
                engineAudio.clip = idleSound;
                engineAudio.Play();
                wasMoving = false;
            }
        }
    }

    /// <summary>
    /// Handles driving straight and smoothly curving when forced to turn.
    /// </summary>
    private void ApplyMovementAndTurning(float dt)
    {
        float currentForwardSpeed = speed;

        if (isTurning)
        {
            currentForwardSpeed = currentSpeedDuringTurn;
            float step = currentTurnSpeed * dt;

            if (degreesTurned + step >= currentTargetAngle)
            {
                step = currentTargetAngle - degreesTurned;
                myTransform.Rotate(Vector3.up, step * turnDirection);
                isTurning = false;

                // Anti-drift snapping
                Vector3 cleanRotation = myTransform.eulerAngles;
                cleanRotation.y = Mathf.Round(cleanRotation.y);
                myTransform.eulerAngles = cleanRotation;
            }
            else
            {
                myTransform.Rotate(Vector3.up, step * turnDirection);
                degreesTurned += step;
            }
        }

        // [OPT] Translate uses cached transform
        myTransform.Translate(Vector3.forward * currentForwardSpeed * dt);
    }

    /// <summary>
    /// Checks the front and oblique sensors. 5-ray fan system.
    /// </summary>
    private void CheckSensors(out bool centerBlocked, out bool obliqueBlocked)
    {
        // Reset player detection flag para este sensor sweep
        playerDetectedThisFrame = false;

        // [OPT] cache transform properties (cada acesso é nativo!)
        Vector3 pos = myTransform.position;
        Vector3 forwardDir = myTransform.forward;

        Vector3 sensorStartPos = pos + (forwardDir * sensorFrontOffset) + SENSOR_HEIGHT_OFFSET;

        // [OPT] inline rotation calculations (Quaternion.AngleAxis allocations)
        float angleRad = obliqueSensorAngle * Mathf.Deg2Rad;
        float innerAngleRad = (obliqueSensorAngle * 0.5f) * Mathf.Deg2Rad;

        float sinOuter = Mathf.Sin(angleRad);
        float cosOuter = Mathf.Cos(angleRad);
        float sinInner = Mathf.Sin(innerAngleRad);
        float cosInner = Mathf.Cos(innerAngleRad);

        // Rotate forwardDir manually around Y axis
        Vector3 outerLeftDir = new Vector3(forwardDir.x * cosOuter - forwardDir.z * sinOuter, forwardDir.y, forwardDir.x * sinOuter + forwardDir.z * cosOuter);
        Vector3 outerRightDir = new Vector3(forwardDir.x * cosOuter + forwardDir.z * sinOuter, forwardDir.y, -forwardDir.x * sinOuter + forwardDir.z * cosOuter);
        Vector3 innerLeftDir = new Vector3(forwardDir.x * cosInner - forwardDir.z * sinInner, forwardDir.y, forwardDir.x * sinInner + forwardDir.z * cosInner);
        Vector3 innerRightDir = new Vector3(forwardDir.x * cosInner + forwardDir.z * sinInner, forwardDir.y, -forwardDir.x * sinInner + forwardDir.z * cosInner);

        // Front (3 rays)
        bool centerHit = CheckSingleRay(sensorStartPos, forwardDir, frontSensorLength);
        bool innerLeftHit = CheckSingleRay(sensorStartPos, innerLeftDir, frontSensorLength);
        bool innerRightHit = CheckSingleRay(sensorStartPos, innerRightDir, frontSensorLength);

        centerBlocked = centerHit || innerLeftHit || innerRightHit;

        // Sides (2 rays)
        bool outerLeftHit = CheckSingleRay(sensorStartPos, outerLeftDir, obliqueSensorLength);
        bool outerRightHit = CheckSingleRay(sensorStartPos, outerRightDir, obliqueSensorLength);

        obliqueBlocked = outerLeftHit || outerRightHit;
    }

    /// <summary>
    /// Fires a single raycast. Uses RaycastNonAlloc to avoid garbage collection.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        // [OPT] RaycastNonAlloc em vez de RaycastAll (zero garbage!)
        int hitCount = Physics.RaycastNonAlloc(startPos, direction, s_RaycastBuffer, length, sensorLayerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_RaycastBuffer[i];

            // 1. CHECK FOR THE PLAYER
            // [OPT] CompareTag em vez de tag ==
            if (hit.collider.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player"))
            {
                playerDetectedThisFrame = true; // Sinaliza que vi o Player neste check
                #if UNITY_EDITOR
                if (drawDebugRays) Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                #endif
                return true;
            }

            // 2. CHECK FOR OTHER CARS
            CarCityMovement otherCar = hit.collider.GetComponentInParent<CarCityMovement>();
            if (otherCar != null && otherCar.cachedInstanceID != cachedInstanceID)
            {
                if (isInNeverStopZone)
                {
                    continue;
                }
                else
                {
                    // [OPT] sqrMagnitude em vez de Distance (evita sqrt)
                    float sqrDistance = (myTransform.position - otherCar.myTransform.position).sqrMagnitude;
                    if (sqrDistance < 12.25f) // 3.5^2
                    {
                        if (cachedInstanceID > otherCar.cachedInstanceID)
                        {
                            #if UNITY_EDITOR
                            if (drawDebugRays) Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                            #endif
                            return true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    #if UNITY_EDITOR
                    if (drawDebugRays) Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                    #endif
                    return true;
                }
            }
        }

        #if UNITY_EDITOR
        if (drawDebugRays) Debug.DrawRay(startPos, direction * length, Color.green);
        #endif
        return false;
    }

    // ===== TRIGGER EVENTS =====

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) isInStopZone = true;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = true;
    }

    // [OPT] OnTriggerStay é caro — só precisas se houver casos onde Enter é missed
    // Mantemos como fallback mas com early-exit
    private void OnTriggerStay(Collider other)
    {
        // [OPT] Só reativa se estiver false (evita writes constantes)
        if (!isInStopZone && other.CompareTag(targetStopZoneTag)) isInStopZone = true;
        else if (!isInNeverStopZone && other.CompareTag(neverStopZoneTag)) isInNeverStopZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) isInStopZone = false;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = false;
    }

    /// <summary>
    /// Forces the car to initiate a turning sequence using parameters provided by an external zone.
    /// </summary>
    public void ForceTurn(float customAngle, float customTurnSpeed, float customSpeedDuringTurn)
    {
        if (!isTurning)
        {
            isTurning = true;
            degreesTurned = 0f;
            currentTargetAngle = Mathf.Abs(customAngle);
            turnDirection = Mathf.Sign(customAngle);
            currentTurnSpeed = customTurnSpeed;
            currentSpeedDuringTurn = customSpeedDuringTurn;
        }
    }

    /// <summary>
    /// Resets the car's state and moves it to a random TrafficSpawnZone.
    /// </summary>
    private void TeleportToNextSpawn()
    {
        if (TrafficSpawnZone.allSpawnZones.Count == 0)
        {
            Debug.LogWarning("[Traffic System] No TrafficSpawnZones found! Please add them to the map.");
            timeOffMagnet = 0f;
            return;
        }

        int randomIndex = Random.Range(0, TrafficSpawnZone.allSpawnZones.Count);
        TrafficSpawnZone chosenSpawn = TrafficSpawnZone.allSpawnZones[randomIndex];

        myTransform.position = chosenSpawn.transform.position;
        myTransform.rotation = Quaternion.Euler(chosenSpawn.customCarRotation);

        timeOffMagnet = 0f;
        stationaryTimer = 0f;
        lastPosition = myTransform.position;
        isTurning = false;
        ignoreObliqueCars = false;
        stuckTimer = 0f;

        Debug.Log($"[Traffic System] A lost car was teleported to {chosenSpawn.gameObject.name}.");
    }
}