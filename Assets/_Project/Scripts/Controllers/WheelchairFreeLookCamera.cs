using UnityEngine;

public class WheelchairFreeLookCamera : MonoBehaviour
{
    [Header("=== Configurações do Olhar ===")]
    [Tooltip("Sensibilidade do rato")]
    public float sensibilidadeMouse = 2f;
    
    [Tooltip("Limite de olhar para cima/baixo (graus)")]
    public float limiteVertical = 80f;
    
    [Tooltip("Limite de olhar para esquerda/direita (graus)")]
    public float limiteHorizontal = 90f;  // Simula o limite de virar a cabeça
    
    [Header("=== Suavização ===")]
    [Tooltip("Suavizar movimento da câmara")]
    public bool suavizarMovimento = true;
    
    [Tooltip("Velocidade de suavização")]
    public float velocidadeSuavizacao = 10f;
    
    [Header("=== Debug ===")]
    [SerializeField] private float rotacaoX = 0f;  // Cima/Baixo
    [SerializeField] private float rotacaoY = 0f;  // Esquerda/Direita
    
    // Variáveis internas
    private Quaternion rotacaoAlvo;
    
    void Start()
    {
        // Bloquear cursor no centro da tela
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Guardar rotação inicial
        rotacaoAlvo = transform.localRotation;
    }
    
    void Update()
    {
        // Obter input do rato
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadeMouse;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadeMouse;
        
        // Aplicar rotações apenas se houver movimento do rato
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            // Rotação horizontal (virar cabeça esquerda/direita)
            rotacaoY += mouseX;
            rotacaoY = Mathf.Clamp(rotacaoY, -limiteHorizontal, limiteHorizontal);
            
            // Rotação vertical (olhar cima/baixo)
            rotacaoX -= mouseY;
            rotacaoX = Mathf.Clamp(rotacaoX, -limiteVertical, limiteVertical);
        }
        
        // Criar a rotação final
        rotacaoAlvo = Quaternion.Euler(rotacaoX, rotacaoY, 0f);
        
        // Aplicar rotação (com ou sem suavização)
        if (suavizarMovimento)
        {
            // Aplicar rotação suave usando interpolação esférica
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, 
                rotacaoAlvo, 
                velocidadeSuavizacao * Time.deltaTime
            );
        }
        else
        {
            // Aplicar rotação direta sem suavização
            transform.localRotation = rotacaoAlvo;
        }
        
        // TAB para mostrar/esconder cursor (útil para aceder a menus)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            AlternarCursor();
        }
        
        // Tecla C para centrar a vista
        if (Input.GetKeyDown(KeyCode.C))
        {
            CentrarVista();
        }
    }
    
    /// <summary>
    /// Alterna entre cursor bloqueado/desbloqueado
    /// Útil para aceder a menus ou interface
    /// </summary>
    void AlternarCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // Desbloquear cursor e torná-lo visível
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Bloquear cursor e escondê-lo
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    /// <summary>
    /// Centra a vista na posição frontal
    /// Simula voltar a olhar em frente
    /// </summary>
    void CentrarVista()
    {
        // Reset das rotações para zero
        rotacaoX = 0f;
        rotacaoY = 0f;
        
        // Aplicar rotação neutra imediatamente
        transform.localRotation = Quaternion.identity;
        
        // Feedback no console
        Debug.Log("Vista centrada!");
    }
}