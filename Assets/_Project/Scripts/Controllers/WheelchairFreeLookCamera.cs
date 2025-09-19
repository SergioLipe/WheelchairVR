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
    
    [Header("=== Efeitos Realistas ===")]
    [Tooltip("Retornar ao centro quando não está a mover o rato")]
    public bool retornarAoCentro = false;
    
    [Tooltip("Velocidade de retorno ao centro")]
    public float velocidadeRetorno = 1f;
    
    [Header("=== Debug ===")]
    [SerializeField] private float rotacaoX = 0f;  // Cima/Baixo
    [SerializeField] private float rotacaoY = 0f;  // Esquerda/Direita
    
    // Variáveis internas
    private Quaternion rotacaoAlvo;
    private float tempoSemInput = 0f;
    
    void Start()
    {
        // Bloquear cursor no centro
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
        
        // Aplicar rotações
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            // Rotação horizontal (virar cabeça esquerda/direita)
            rotacaoY += mouseX;
            rotacaoY = Mathf.Clamp(rotacaoY, -limiteHorizontal, limiteHorizontal);
            
            // Rotação vertical (olhar cima/baixo)
            rotacaoX -= mouseY;
            rotacaoX = Mathf.Clamp(rotacaoX, -limiteVertical, limiteVertical);
            
            // Reset do tempo sem input
            tempoSemInput = 0f;
        }
        else
        {
            // Contar tempo sem movimento do rato
            tempoSemInput += Time.deltaTime;
            
            // Retornar ao centro após algum tempo sem input (opcional)
            if (retornarAoCentro && tempoSemInput > 2f)
            {
                rotacaoY = Mathf.Lerp(rotacaoY, 0f, velocidadeRetorno * Time.deltaTime);
                rotacaoX = Mathf.Lerp(rotacaoX, 0f, velocidadeRetorno * Time.deltaTime);
            }
        }
        
        // Criar a rotação final
        rotacaoAlvo = Quaternion.Euler(rotacaoX, rotacaoY, 0f);
        
        // Aplicar rotação (com ou sem suavização)
        if (suavizarMovimento)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, 
                rotacaoAlvo, 
                velocidadeSuavizacao * Time.deltaTime
            );
        }
        else
        {
            transform.localRotation = rotacaoAlvo;
        }
        
        // TAB para mostrar/esconder cursor (útil para menus)
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
    
    void AlternarCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    void CentrarVista()
    {
        rotacaoX = 0f;
        rotacaoY = 0f;
        transform.localRotation = Quaternion.identity;
        Debug.Log("Vista centrada!");
    }
    
    // Método público para obter direção do olhar (útil para interações)
    public Vector3 GetDirecaoOlhar()
    {
        return transform.forward;
    }
    
    // Método para verificar se está a olhar para algo
    public bool EstaAOlharPara(Transform alvo, float anguloMaximo = 30f)
    {
        Vector3 direcaoParaAlvo = (alvo.position - transform.position).normalized;
        float angulo = Vector3.Angle(transform.forward, direcaoParaAlvo);
        return angulo <= anguloMaximo;
    }
    
    // Método para limitar temporariamente o olhar (útil em cutscenes ou ao falar com NPCs)
    public void DefinirLimites(float limiteH, float limiteV)
    {
        limiteHorizontal = limiteH;
        limiteVertical = limiteV;
    }
    
    // Restaurar limites padrão
    public void RestaurarLimites()
    {
        limiteHorizontal = 90f;
        limiteVertical = 80f;
    }
}