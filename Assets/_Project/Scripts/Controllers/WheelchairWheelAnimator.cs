using UnityEngine;

public class WheelchairWheelAnimator : MonoBehaviour
{
    [Header("=== Referências das Rodas ===")]
    [Tooltip("Roda traseira esquerda (a que gira)")]
    public Transform rodaEsquerda;
    
    [Tooltip("Roda traseira direita (a que gira)")]
    public Transform rodaDireita;
    
    [Tooltip("Rodas pequenas da frente (opcional)")]
    public Transform[] rodasFrente;
    
    [Header("=== Configurações ===")]
    [Tooltip("Diâmetro das rodas traseiras em metros")]
    public float diametroRodas = 0.6f;  // 60cm é típico
    
    [Tooltip("Velocidade de rotação das rodas")]
    public float multiplicadorVelocidade = 1f;
    
    [Header("=== Debug ===")]
    [SerializeField] private float rotacaoAtual = 0f;
    
    // Referência ao script de movimento
    private WheelchairRealisticMovement movementScript;
    private float velocidadeAnterior = 0f;
    
    void Start()
    {
        // Obter referência ao script de movimento
        movementScript = GetComponent<WheelchairRealisticMovement>();
        
        if (movementScript == null)
        {
            Debug.LogError("WheelchairWheelAnimator: Não encontrei o script WheelchairRealisticMovement!");
            enabled = false;
            return;
        }
        
        // Tentar encontrar as rodas automaticamente se não foram atribuídas
        if (rodaEsquerda == null || rodaDireita == null)
        {
            ProcurarRodasAutomaticamente();
        }
    }
    
    void ProcurarRodasAutomaticamente()
    {
        // Procurar por objetos com "wheel", "roda" no nome
        Transform[] todosFilhos = GetComponentsInChildren<Transform>();
        
        foreach (Transform filho in todosFilhos)
        {
            string nomeLower = filho.name.ToLower();
            
            // Rodas traseiras
            if (nomeLower.Contains("left") || nomeLower.Contains("esquerda"))
            {
                if (nomeLower.Contains("wheel") || nomeLower.Contains("roda"))
                {
                    rodaEsquerda = filho;
                    Debug.Log($"Roda esquerda encontrada: {filho.name}");
                }
            }
            else if (nomeLower.Contains("right") || nomeLower.Contains("direita"))
            {
                if (nomeLower.Contains("wheel") || nomeLower.Contains("roda"))
                {
                    rodaDireita = filho;
                    Debug.Log($"Roda direita encontrada: {filho.name}");
                }
            }
        }
    }
    
    void Update()
    {
        if (movementScript == null) return;
        
        // Obter velocidade atual normalizada
        float velocidadeNormalizada = movementScript.GetVelocidadeNormalizada();
        
        // Calcular rotação baseada na velocidade e diâmetro da roda
        float circunferencia = Mathf.PI * diametroRodas;
        float rotacaoPorMetro = 360f / circunferencia;
        
        // Velocidade em metros por segundo (aproximado)
        float velocidadeMetrosPorSegundo = velocidadeNormalizada * 6f / 3.6f;  // 6 km/h max
        
        // Rotação das rodas
        float velocidadeRotacao = velocidadeMetrosPorSegundo * rotacaoPorMetro * multiplicadorVelocidade;
        
        // Aplicar rotação suave
        rotacaoAtual += velocidadeRotacao * Time.deltaTime;
        
        // Aplicar às rodas traseiras (eixo X para rotação frontal)
        if (rodaEsquerda != null)
        {
            rodaEsquerda.localRotation = Quaternion.Euler(rotacaoAtual, 0, 0);
        }
        
        if (rodaDireita != null)
        {
            rodaDireita.localRotation = Quaternion.Euler(rotacaoAtual, 0, 0);
        }
        
        // Rodas da frente (giram mais rápido e podem virar)
        AnimarRodasFrente(velocidadeRotacao);
        
        // Efeito de derrapagem/travagem
        if (Mathf.Abs(velocidadeAnterior) > 0.1f && Mathf.Abs(velocidadeNormalizada) < 0.01f)
        {
            // Som de travagem ou partículas aqui
            Debug.Log("Travagem!");
        }
        
        velocidadeAnterior = velocidadeNormalizada;
    }
    
    void AnimarRodasFrente(float velocidadeRotacao)
    {
        if (rodasFrente == null || rodasFrente.Length == 0) return;
        
        foreach (Transform rodaFrente in rodasFrente)
        {
            if (rodaFrente != null)
            {
                // Rodas da frente giram mais rápido (são menores)
                rodaFrente.Rotate(velocidadeRotacao * 2f * Time.deltaTime, 0, 0, Space.Self);
                
                // Opcional: fazer as rodas da frente virarem com a direção
                float inputHorizontal = Input.GetAxis("Horizontal");
                Vector3 rotacaoLocal = rodaFrente.localEulerAngles;
                rotacaoLocal.y = inputHorizontal * 30f;  // Máximo 30 graus de viragem
                rodaFrente.localEulerAngles = rotacaoLocal;
            }
        }
    }
    
    // Método público para parar instantaneamente as rodas
    public void PararRodas()
    {
        rotacaoAtual = 0f;
        
        if (rodaEsquerda != null)
            rodaEsquerda.localRotation = Quaternion.identity;
            
        if (rodaDireita != null)
            rodaDireita.localRotation = Quaternion.identity;
    }
}