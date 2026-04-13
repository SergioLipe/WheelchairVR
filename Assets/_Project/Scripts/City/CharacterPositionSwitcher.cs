using UnityEngine;
using UnityEngine.AI; // Necessário caso ela tenha um NavMeshAgent

public class CharacterPositionSwitcher : MonoBehaviour
{
    [Header("=== Referência da Cadeira ===")]
    [Tooltip("Arrasta a Wheelchair_VR da tua cena (Hierarchy) para aqui")]
    public GameObject vrWheelchair;

    [Header("=== Posições da Personagem ===")]
    [Tooltip("Posição onde ela deve estar se jogares em VR")]
    public Vector3 positionForVR = new Vector3(-0.04f, 0f, 30.81f);

    [Tooltip("Posição onde ela deve estar se jogares no PC")]
    public Vector3 positionForPC = new Vector3(0f, 0f, 0f);

    void Start()
    {
        // Espera 0.1s para garantir que todas as cadeiras e sistemas já carregaram
        Invoke(nameof(MoveCharacter), 0.1f);
    }

    private void MoveCharacter()
    {
        // Descobre para onde ela tem de ir
        Vector3 targetPosition = positionForPC;
        if (vrWheelchair != null && vrWheelchair.activeInHierarchy)
        {
            targetPosition = positionForVR;
        }

        // 1. Procura se a personagem tem controladores de física/movimento
        CharacterController cc = GetComponent<CharacterController>();
        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        // 2. Desliga a física para o Unity não bloquear o teletransporte
        if (cc != null) cc.enabled = false;
        if (agent != null) agent.enabled = false;

        // 3. Teletransporta a personagem
        transform.position = targetPosition;

        // 4. Volta a ligar a física na nova posição
        if (cc != null) cc.enabled = true;
        if (agent != null) agent.enabled = true;
    }
}