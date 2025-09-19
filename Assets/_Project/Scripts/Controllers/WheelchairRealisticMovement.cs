using UnityEngine;

public class WheelchairRealisticMovement : MonoBehaviour
{
    [Header("=== Configurações de Velocidade ===")]
    [Tooltip("Velocidade máxima em modo normal (km/h)")]
    public float velocidadeMaximaNormal = 6f;  // 6 km/h é típico para cadeiras elétricas
    
    [Tooltip("Velocidade máxima em modo lento/interior (km/h)")]
    public float velocidadeMaximaLenta = 3f;
    
    [Tooltip("Velocidade de marcha-atrás (km/h)")]
    public float velocidadeMarchaAtras = 2f;
    
    [Header("=== Configurações de Aceleração ===")]
    [Tooltip("Tempo para atingir velocidade máxima (segundos)")]
    public float tempoAceleracao = 2f;
    
    [Tooltip("Tempo para parar completamente (segundos)")]
    public float tempoTravagem = 1.5f;
    
    [Header("=== Configurações de Rotação ===")]
    [Tooltip("Velocidade de rotação (graus por segundo)")]
    public float velocidadeRotacao = 45f;  // Cadeiras elétricas rodam devagar
    
    [Tooltip("Pode rodar sem se mover para frente/trás?")]
    public bool rotacaoNoLugar = true;  // Característica de cadeiras elétricas
    
    [Header("=== Modos de Condução ===")]
    [Tooltip("Modo atual de velocidade")]
    public ModosVelocidade modoAtual = ModosVelocidade.Normal;
    
    [Header("=== Física e Limites ===")]
    [Tooltip("Inclinação máxima que consegue subir (graus)")]
    public float inclinacaoMaxima = 10f;
    
    [Tooltip("Gravidade aplicada")]
    public float gravidade = -9.81f;
    
    [Header("=== Estado Atual (Debug) ===")]
    [SerializeField] private float velocidadeAtual = 0f;
    [SerializeField] private float velocidadeDesejada = 0f;
    [SerializeField] private bool travaoDeEmergencia = false;
    
    // Componentes
    private CharacterController controller;
    private Vector3 movimentoVelocidade;
    
    // Sistema de input suavizado
    private float inputVerticalSuavizado = 0f;
    private float inputHorizontalSuavizado = 0f;
    
    public enum ModosVelocidade
    {
        Lento,      // Para interiores
        Normal,     // Uso geral
        Desligado   // Travão de emergência
    }
    
    void Start()
    {
        // Configurar o CharacterController
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            
            // Dimensões típicas de uma cadeira de rodas
            controller.height = 1.4f;    // Altura total
            controller.radius = 0.35f;   // Largura/2
            controller.center = new Vector3(0, 0.7f, 0);  // Centro de massa
        }
        
        // Converter km/h para m/s
        velocidadeMaximaNormal = velocidadeMaximaNormal / 3.6f;
        velocidadeMaximaLenta = velocidadeMaximaLenta / 3.6f;
        velocidadeMarchaAtras = velocidadeMarchaAtras / 3.6f;
    }
    
    void Update()
    {
        // Mudar modos com teclas numéricas
        GerirModos();
        
        // Processar movimento apenas se não estiver em modo desligado
        if (modoAtual != ModosVelocidade.Desligado)
        {
            ProcessarInput();
            AplicarMovimento();
        }
        else
        {
            // Parar gradualmente em modo desligado
            PararDeEmergencia();
        }
        
        // Aplicar sempre a gravidade
        AplicarGravidade();
    }
    
    void GerirModos()
    {
        // Tecla 1: Modo Lento (interior/pessoas)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            modoAtual = ModosVelocidade.Lento;
            Debug.Log("Modo: LENTO (Interior)");
        }
        // Tecla 2: Modo Normal
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            modoAtual = ModosVelocidade.Normal;
            Debug.Log("Modo: NORMAL");
        }
        // Espaço: Travão de emergência
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            modoAtual = ModosVelocidade.Desligado;
            travaoDeEmergencia = true;
            Debug.Log("TRAVÃO DE EMERGÊNCIA!");
        }
        // Soltar espaço: Voltar ao modo anterior
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            modoAtual = ModosVelocidade.Normal;
            travaoDeEmergencia = false;
        }
    }
    
    void ProcessarInput()
    {
        // Obter input do jogador
        float inputVertical = Input.GetAxis("Vertical");    // W/S ou Setas
        float inputHorizontal = Input.GetAxis("Horizontal"); // A/D ou Setas
        
        // Suavizar o input (simula o joystick analógico da cadeira)
        float suavizacao = 3f;
        inputVerticalSuavizado = Mathf.Lerp(inputVerticalSuavizado, inputVertical, suavizacao * Time.deltaTime);
        inputHorizontalSuavizado = Mathf.Lerp(inputHorizontalSuavizado, inputHorizontal, suavizacao * Time.deltaTime);
        
        // Determinar velocidade máxima baseada no modo
        float velocidadeMaxima = modoAtual == ModosVelocidade.Lento ? 
                                velocidadeMaximaLenta : velocidadeMaximaNormal;
        
        // Marcha-atrás é sempre mais lenta
        if (inputVerticalSuavizado < 0)
        {
            velocidadeMaxima = velocidadeMarchaAtras;
        }
        
        // Calcular velocidade desejada
        velocidadeDesejada = inputVerticalSuavizado * velocidadeMaxima;
        
        // Aceleração e desaceleração suave
        if (Mathf.Abs(velocidadeDesejada) > Mathf.Abs(velocidadeAtual))
        {
            // Acelerar
            float aceleracao = velocidadeMaxima / tempoAceleracao;
            velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeDesejada, aceleracao * Time.deltaTime);
        }
        else
        {
            // Desacelerar/Travar
            float desaceleracao = velocidadeMaxima / tempoTravagem;
            velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeDesejada, desaceleracao * Time.deltaTime);
        }
        
        // Rotação
        ProcessarRotacao(inputHorizontalSuavizado);
    }
    
    void ProcessarRotacao(float inputHorizontal)
    {
        // Cadeiras elétricas podem rodar no lugar ou enquanto se movem
        float multiplicadorRotacao = 1f;
        
        // Se estiver parado, rotação total
        if (rotacaoNoLugar && Mathf.Abs(velocidadeAtual) < 0.1f)
        {
            multiplicadorRotacao = 1.2f;  // Rotação ligeiramente mais rápida quando parado
        }
        // Se estiver em movimento, rotação proporcional à velocidade
        else if (Mathf.Abs(velocidadeAtual) > 0.1f)
        {
            // Quanto mais rápido, menos rotação (mais realista)
            multiplicadorRotacao = 1f - (Mathf.Abs(velocidadeAtual) / velocidadeMaximaNormal * 0.5f);
        }
        
        // Aplicar rotação
        float rotacao = inputHorizontal * velocidadeRotacao * multiplicadorRotacao * Time.deltaTime;
        transform.Rotate(0, rotacao, 0);
    }
    
    void AplicarMovimento()
    {
        // Verificar inclinação do terreno
        if (VerificarInclinacao())
        {
            // Movimento baseado na direção que a cadeira está virada
            Vector3 direcaoMovimento = transform.forward * velocidadeAtual;
            
            // Manter a componente Y para a gravidade
            direcaoMovimento.y = movimentoVelocidade.y;
            
            // Aplicar movimento
            controller.Move(direcaoMovimento * Time.deltaTime);
        }
        else
        {
            // Inclinação muito íngreme - reduzir velocidade
            velocidadeAtual *= 0.95f;
            Debug.Log("Inclinação demasiado íngreme!");
        }
    }
    
    bool VerificarInclinacao()
    {
        // Raycast para verificar o terreno à frente
        RaycastHit hit;
        Vector3 origem = transform.position + Vector3.up * 0.5f;
        Vector3 direcao = transform.forward + Vector3.down * 0.3f;
        
        if (Physics.Raycast(origem, direcao, out hit, 2f))
        {
            // Calcular ângulo da superfície
            float angulo = Vector3.Angle(hit.normal, Vector3.up);
            return angulo <= inclinacaoMaxima;
        }
        
        return true;  // Se não detetar nada, permitir movimento
    }
    
    void AplicarGravidade()
    {
        if (controller.isGrounded)
        {
            // Manter uma pequena força para baixo quando no chão
            movimentoVelocidade.y = -2f;
        }
        else
        {
            // Aplicar gravidade quando no ar
            movimentoVelocidade.y += gravidade * Time.deltaTime;
        }
    }
    
    void PararDeEmergencia()
    {
        // Parar rapidamente mas não instantaneamente
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, 0f, 10f * Time.deltaTime);
        
        // Aplicar pequeno movimento residual
        Vector3 movimentoResidual = transform.forward * velocidadeAtual;
        movimentoResidual.y = movimentoVelocidade.y;
        controller.Move(movimentoResidual * Time.deltaTime);
    }
    
    // Método para feedback visual (opcional - para as rodas)
    public float GetVelocidadeNormalizada()
    {
        return velocidadeAtual / velocidadeMaximaNormal;
    }
    
    // Método para sons do motor (opcional)
    public bool EstaEmMovimento()
    {
        return Mathf.Abs(velocidadeAtual) > 0.1f;
    }
}