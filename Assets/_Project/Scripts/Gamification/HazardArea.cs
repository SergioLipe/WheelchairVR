using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

/// <summary>
/// Universal hazard zone. Stops the player, shows a custom (localized) message, 
/// and activates the Game Over panel for both PC and VR.
/// </summary>
public class HazardArea : MonoBehaviour
{
    [Header("=== Hazard Settings ===")]
    [Tooltip("Escolhe a Tabela + Entrada deste hazard (ex: GameText / nao_vas_para_a_relva)")]
    public LocalizedString hazardMessage;

    [Tooltip("CHECK THIS FOR CROSSWALKS: If player is already inside when this turns on, they won't die.")]
    public bool allowSafeExitIfAlreadyInside = false;

    [Header("=== UI References (PC) ===")]
    public TMP_Text warningTextPC;
    public GameObject hazardPanelPC;

    [Header("=== UI References (VR) ===")]
    public TMP_Text warningTextVR;
    public GameObject hazardPanelVR;
    public Transform vrCamera;

    [Header("=== VR Hand Manager ===")]
    public HandVisibilityManager handVisibilityManager;
    public float vrPanelDistance = 1.5f;

    [Header("=== Optimization ===")]
    [Tooltip("Layer where the player is. Reduces OverlapBox cost massively.")]
    public LayerMask playerLayerMask = ~0;

    [Tooltip("Enable verbose debug logs (disable for production builds)")]
    public bool enableDebugLogs = false;

    private static bool isGameOver = false;
    private bool playerIsSafe = false;

    // [OPT] Pre-allocated buffer
    private static readonly Collider[] s_OverlapBuffer = new Collider[8];

    private void Start()
    {
        isGameOver = false;
    }

    private void OnEnable()
    {
        if (allowSafeExitIfAlreadyInside)
        {
            playerIsSafe = false;

            BoxCollider myBox = GetComponent<BoxCollider>();
            if (myBox != null)
            {
                // [OPT] cache transform
                Transform t = transform;
                Vector3 boxCenter = t.TransformPoint(myBox.center);
                Vector3 boxHalfExtents = Vector3.Scale(myBox.size, t.lossyScale) * 0.5f;

                // Abs values (negative scale fix)
                boxHalfExtents.x = Mathf.Abs(boxHalfExtents.x);
                boxHalfExtents.y = Mathf.Abs(boxHalfExtents.y);
                boxHalfExtents.z = Mathf.Abs(boxHalfExtents.z);

                // [OPT] OverlapBoxNonAlloc + LayerMask
                int hitCount = Physics.OverlapBoxNonAlloc(
                    boxCenter, boxHalfExtents, s_OverlapBuffer,
                    t.rotation, playerLayerMask);

                for (int i = 0; i < hitCount; i++)
                {
                    if (s_OverlapBuffer[i].CompareTag("Player"))
                    {
                        playerIsSafe = true;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Aplica a mensagem localizada deste hazard ao texto indicado.
    /// Se o texto tiver um Localize String Event, redireciona-o para a entrada
    /// deste hazard (mantendo a tradução). Caso contrário, escreve o texto resolvido.
    /// IMPORTANTE: chamar ANTES de ativar o painel.
    /// </summary>
    private void ApplyHazardMessage(TMP_Text textComponent)
    {
        if (textComponent == null) return;

        LocalizeStringEvent localize = textComponent.GetComponent<LocalizeStringEvent>();

        if (localize != null)
        {
            // Redireciona o Localize String Event para a entrada deste hazard.
            // Quando o painel for ativado, o localize vai usar ESTA referência.
            localize.StringReference = hazardMessage;
            localize.RefreshString();
        }
        else if (!hazardMessage.IsEmpty)
        {
            // Sem Localize String Event: escreve o texto já traduzido.
            textComponent.text = hazardMessage.GetLocalizedString();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!this.enabled) return;
        if (isGameOver) return;

        if (other.CompareTag("Player"))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"<color=red>HAZARD activated by:</color> {other.gameObject.name}");
            }

            if (allowSafeExitIfAlreadyInside && playerIsSafe)
            {
                return;
            }

            // GAME OVER LOGIC
            isGameOver = true;
            Time.timeScale = 0f;

            if (LevelManagerVR.Instance != null)
            {
                LevelManagerVR.Instance.isLevelActive = false;
            }

            // PC UI  --> definir a mensagem ANTES de ativar o painel
            ApplyHazardMessage(warningTextPC);
            if (hazardPanelPC != null) hazardPanelPC.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // VR UI  --> definir a mensagem ANTES de ativar o painel
            ApplyHazardMessage(warningTextVR);
            if (hazardPanelVR != null)
            {
                if (vrCamera != null)
                {
                    Vector3 camPos = vrCamera.position;
                    Vector3 spawnPos = camPos + (vrCamera.forward * vrPanelDistance);
                    spawnPos.y = camPos.y;

                    Transform panelT = hazardPanelVR.transform;
                    panelT.position = spawnPos;
                    panelT.LookAt(vrCamera);
                    panelT.Rotate(0, 180, 0);
                }

                hazardPanelVR.SetActive(true);

                if (handVisibilityManager != null)
                {
                    handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PauseMenu;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsSafe = false;
        }
    }
}