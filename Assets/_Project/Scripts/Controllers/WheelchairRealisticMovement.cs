using UnityEngine;
using System.Collections;

public class WheelchairRealisticMovement : MonoBehaviour
{
    [Header("=== Configurações de Velocidade ===")]
    [Tooltip("Velocidade máxima em modo normal (km/h)")]
    public float velocidadeMaximaNormal = 6f;

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
    public float velocidadeRotacao = 45f;

    [Tooltip("Pode rodar sem se mover para frente/trás?")]
    public bool rotacaoNoLugar = false;

    [Header("=== Modos de Condução ===")]
    [Tooltip("Modo atual de velocidade")]
    public ModosVelocidade modoAtual = ModosVelocidade.Normal;

    [Header("=== Física e Limites ===")]
    [Tooltip("Inclinação máxima que consegue subir (graus)")]
    public float inclinacaoMaxima = 10f;

    [Tooltip("Gravidade aplicada")]
    public float gravidade = -9.81f;

    [Header("=== Sistema de Colisão ===")]
    [Tooltip("Ativar avisos de colisão")]
    public bool avisosColisaoAtivos = true;

    [Tooltip("Distância para aviso de proximidade")]
    public float distanciaAviso = 1.5f;

    [Header("=== Estado Atual (Debug) ===")]
    [SerializeField] private float velocidadeAtual = 0f;
    [SerializeField] private float velocidadeDesejada = 0f;
    [SerializeField] private bool travaoDeEmergencia = false;
    [SerializeField] private string tipoDirecaoAtual = "Frontal";
    [SerializeField] private bool emColisao = false;
    [SerializeField] private string objetoColidido = "";
    [SerializeField] private float distanciaObstaculo = 999f;

    // Componentes
    private CharacterController controller;
    private Vector3 movimentoVelocidade;
    private WheelchairWheelController wheelController;

    // Sistema de input suavizado
    private float inputVerticalSuavizado = 0f;
    private float inputHorizontalSuavizado = 0f;

    // Variáveis de colisão
    private bool avisoProximidade = false;
    private float tempoColisao = 0f;

    public enum ModosVelocidade
    {
        Lento,      // Para interiores
        Normal,     // Uso geral
        Desligado   // Travão de emergência
    }

    void Start()
    {
           // Configurar o CharacterController com valores MÍNIMOS
    controller = GetComponent<CharacterController>();
    if (controller == null)
    {
        controller = gameObject.AddComponent<CharacterController>();
    }
    
    // Valores ajustados para scale 1x1x1
    controller.height = 1.4f;
    controller.radius = 0.35f;
    controller.center = new Vector3(0, 0.7f, 0);
    
    // CRÍTICO: Skin Width deve ser maior que zero mas pequeno
    controller.skinWidth = 0.001f;  // Unity recomenda 10% do raio
    controller.minMoveDistance = 0;
    controller.stepOffset = 0.1f;
    
    // Elevar um pouco no início para não ficar preso no chão
    transform.position += Vector3.up * 0.1f;
    

        // Obter referência ao wheel controller
        wheelController = GetComponent<WheelchairWheelController>();

        // Converter km/h para m/s
        velocidadeMaximaNormal = velocidadeMaximaNormal / 3.6f;
        velocidadeMaximaLenta = velocidadeMaximaLenta / 3.6f;
        velocidadeMarchaAtras = velocidadeMarchaAtras / 3.6f;
        
        Debug.Log("✅ WheelchairRealisticMovement iniciado!");
        Debug.Log("📍 Sistema de colisão ativo - Plane será ignorado");
    }

    void Update()
    {
        // Atualizar tipo de direção para debug
        if (wheelController != null)
        {
            tipoDirecaoAtual = wheelController.GetTipoDirecao().ToString();
        }

        // Verificar obstáculos
        if (avisosColisaoAtivos)
        {
            VerificarObstaculos();
        }

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
        
        // Reset automático da colisão após 2 segundos
        if (emColisao && Time.time - tempoColisao > 2f)
        {
            emColisao = false;
            objetoColidido = "";
        }
    }

    void GerirModos()
    {
        // Tecla 1: Modo Lento (interior/pessoas)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            modoAtual = ModosVelocidade.Lento;
            Debug.Log("Modo: LENTO (Interior) - 3 km/h");
        }
        // Tecla 2: Modo Normal
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            modoAtual = ModosVelocidade.Normal;
            Debug.Log("Modo: NORMAL - 6 km/h");
        }
        // Espaço: Travão de emergência
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            modoAtual = ModosVelocidade.Desligado;
            travaoDeEmergencia = true;
            Debug.Log("TRAVÃO DE EMERGÊNCIA ATIVADO!");
        }
        // Soltar espaço: Voltar ao modo normal
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
    
    // === NOVO: Ajuste suave durante colisão ===
    if (emColisao)
    {
        // Redução muito gradual (5% por frame)
        velocidadeDesejada *= 0.95f;
        
        // Se o jogador insiste em ir para frente, permitir movimento mínimo
        if (inputVerticalSuavizado > 0.5f)
        {
            // Permite 10% da velocidade para "empurrar" levemente
            velocidadeDesejada = Mathf.Max(velocidadeDesejada, velocidadeMaxima * 0.1f);
        }
        else if (inputVerticalSuavizado < -0.1f)
        {
            // Permitir marcha-atrás para sair da colisão
            velocidadeDesejada = inputVerticalSuavizado * velocidadeMarchaAtras;
            emColisao = false;  // Limpar estado de colisão ao recuar
        }
    }

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
        
        // Se em colisão, desacelerar mais devagar para não tremer
        if (emColisao)
        {
            desaceleracao *= 0.5f;  // Desaceleração mais suave em colisão
        }
        
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeDesejada, desaceleracao * Time.deltaTime);
    }

    // Rotação
    ProcessarRotacao(inputHorizontalSuavizado);
}
    void ProcessarRotacao(float inputHorizontal)
    {
        float multiplicadorRotacao = 1f;

        // Se o wheelController existir, verificar tipo de direção
        if (wheelController != null)
        {
            // Direção traseira é mais ágil
            if (wheelController.GetTipoDirecao() == WheelchairWheelController.TipoDirecao.DirecaoTraseira)
            {
                multiplicadorRotacao = 1.3f;  // 30% mais ágil
            }
        }

        // Verificar se pode rodar parado 
        bool estaParado = Mathf.Abs(velocidadeAtual) < 0.1f;

        if (estaParado && !rotacaoNoLugar) // Se está parado E não pode rodar parado - NÃO RODA
        {
            return;
        }
        else if (estaParado && rotacaoNoLugar)  // Se pode rodar parado - boost na rotação 
        {
            multiplicadorRotacao *= 1.2f;
        }
        else if (!estaParado)  // Em movimento - rotação proporcional à velocidade
        {
            multiplicadorRotacao *= (1f - (Mathf.Abs(velocidadeAtual) / velocidadeMaximaNormal * 0.3f));
        }

        // Só aplica rotação se chegou até aqui
        float rotacao = inputHorizontal * velocidadeRotacao * multiplicadorRotacao * Time.deltaTime;
        transform.Rotate(0, rotacao, 0);
    }

void AplicarMovimento()
{
    // Movimento simples e direto
    Vector3 direcaoMovimento = transform.forward * velocidadeAtual * Time.deltaTime;
    direcaoMovimento.y = movimentoVelocidade.y;
    
    // Aplicar sem verificações
    controller.Move(direcaoMovimento);
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

        // Parar as rodas também quando totalmente parado
        if (wheelController != null && velocidadeAtual < 0.01f)
        {
            wheelController.PararRodas();
        }
    }

void VerificarObstaculos()
{
    Vector3 origem = transform.position + Vector3.up * 0.5f;
    avisoProximidade = false;
    float menorDist = 999f;
    string objetoMaisProximo = "";
    
    // VERIFICAR 360 GRAUS - não só à frente
    for (float angulo = 0f; angulo < 360f; angulo += 30f)
    {
        Vector3 dir = Quaternion.Euler(0, angulo, 0) * transform.forward;
        RaycastHit hit;
        
        float distRay = distanciaAviso;
        
        if (Physics.Raycast(origem, dir, out hit, distRay))
        {
            // Ignorar chão
            string nomeObjeto = hit.collider.name.ToLower();
            if (nomeObjeto.Contains("plane") || 
                nomeObjeto.Contains("ground") ||
                nomeObjeto.Contains("floor"))
            {
                continue;
            }
            
            float dist = hit.distance;
            
            if (dist < menorDist)
            {
                menorDist = dist;
                objetoMaisProximo = hit.collider.name;
                avisoProximidade = true;
            }
            
            // Cores diferentes para diferentes direções
            Color corRaio = Color.green;
            if (dist < 0.3f)
                corRaio = Color.red;
            else if (dist < 0.6f)
                corRaio = Color.yellow;
            else if (dist < 1f)
                corRaio = Color.cyan;
                
            Debug.DrawRay(origem, dir * hit.distance, corRaio);
        }
    }
    
    distanciaObstaculo = menorDist;
    if (avisoProximidade && !emColisao)
    {
        objetoColidido = objetoMaisProximo;
    }
}
    // === SISTEMA DE COLISÕES ===
 private float ultimoTempoColisao = 0f;

 void OnControllerColliderHit(ControllerColliderHit hit)
{
    // Ignorar chão
    if (hit.gameObject.name.ToLower().Contains("plane")) return;
    
    // Evitar múltiplas deteções no mesmo frame
    if (Time.time - ultimoTempoColisao < 0.1f) return;
    
    // DETETAR COLISÃO EM QUALQUER DIREÇÃO
    Vector3 dirParaObstaculo = (hit.point - transform.position);
    dirParaObstaculo.y = 0; // Ignorar altura
    dirParaObstaculo.Normalize();
    
    float angulo = Vector3.Angle(transform.forward, dirParaObstaculo);
    
    // Determinar tipo de colisão baseado no ângulo
    string tipoColisao = "";
    float reducaoVelocidade = 0f;
    
    if (angulo < 45f)
    {
        // FRONTAL
        tipoColisao = "FRONTAL";
      //  reducaoVelocidade = 0.5f;  // Reduz muito
    }
    else if (angulo > 135f)
    {
        // TRASEIRA
        tipoColisao = "TRASEIRA";
      //  reducaoVelocidade = 0.2f;  // Reduz pouco
    }
    else
    {
        // LATERAL
        Vector3 cross = Vector3.Cross(transform.forward, dirParaObstaculo);
        if (cross.y > 0)
        {
            tipoColisao = "LATERAL DIREITA";
        }
        else
        {
            tipoColisao = "LATERAL ESQUERDA";
        }
        reducaoVelocidade = 0.3f;  // Reduz médio
    }
    
    // Registar colisão GUI
    emColisao = true;
    objetoColidido = $"{hit.gameObject.name} ({tipoColisao})";
    tempoColisao = Time.time;
    ultimoTempoColisao = Time.time;
    
    /*
    // Ajustar velocidade baseado no tipo
        velocidadeAtual *= (1f - reducaoVelocidade);
    
    // Empurrar ligeiramente na direção oposta
    Vector3 pushDir = -dirParaObstaculo;
    pushDir.y = 0;
    controller.Move(pushDir * 0.005f);
    */
    Debug.Log($"💥 Colisão {tipoColisao} com {hit.gameObject.name}");
    Debug.Log($"   Ângulo: {angulo:F0}°");
}


    IEnumerator EfeitoColisao()
    {
        Vector3 posOriginal = transform.position;
        float duracao = 0.2f;
        float tempo = 0;
        
        while (tempo < duracao)
        {
            float intensidade = (1 - tempo / duracao) * 0.01f;
            transform.position = posOriginal + Random.insideUnitSphere * intensidade;
            tempo += Time.deltaTime;
            yield return null;
        }
        
        transform.position = posOriginal;
    }

    // Método para feedback visual (para as rodas)
    public float GetVelocidadeNormalizada()
    {
        return velocidadeAtual / velocidadeMaximaNormal;
    }

    // Método para sons do motor (futuro)
    public bool EstaEmMovimento()
    {
        return Mathf.Abs(velocidadeAtual) > 0.1f;
    }

    // Método público para os sensores poderem reduzir velocidade
public void ReduzirVelocidade(float multiplicador)
{
    velocidadeAtual *= multiplicador;
}

    // GUI de debug
    void OnGUI()
    {
        if (!Application.isEditor) return;

        // Info de movimento
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 100, 250, 160), "");

        GUI.color = Color.white;
        GUI.Label(new Rect(15, 105, 240, 20), "=== CADEIRA DE RODAS ===");
        GUI.Label(new Rect(15, 125, 240, 20), $"Modo: {modoAtual}");
        GUI.Label(new Rect(15, 145, 240, 20), $"Velocidade: {(velocidadeAtual * 3.6f):F1} / {(modoAtual == ModosVelocidade.Lento ? 3 : 6)} km/h");
        GUI.Label(new Rect(15, 165, 240, 20), $"Direção: {tipoDirecaoAtual}");
        GUI.Label(new Rect(15, 185, 240, 20), $"Distância Obstáculo: {(distanciaObstaculo < 10 ? $"{distanciaObstaculo:F2}m" : "Livre")}");
        GUI.Label(new Rect(15, 205, 240, 20), $"Estado: {(emColisao ? "EM COLISÃO!" : "Livre")}");
        GUI.Label(new Rect(15, 225, 240, 20), $"Objeto: {(objetoColidido != "" ? objetoColidido : "Nenhum")}");

        if (travaoDeEmergencia)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(15, 245, 240, 20), "TRAVÃO ATIVO!");
        }

        // AVISO DE COLISÃO
        if (emColisao)
        {
            GUI.color = new Color(1, 0, 0, 0.9f);
            GUI.Box(new Rect(10, 270, 250, 60), "");
            GUI.color = Color.white;
            
            if (Time.time % 0.5f < 0.25f)
            {
                GUI.Label(new Rect(15, 275, 240, 25), "⚠️ COLISÃO DETETADA! ⚠️");
            }
            GUI.Label(new Rect(15, 295, 240, 20), $"Bateu em: {objetoColidido}");
            GUI.Label(new Rect(15, 310, 240, 20), "Prima ESPAÇO para travar!");
        }
        else if (avisoProximidade && distanciaObstaculo < 1f)
        {
            GUI.color = new Color(1, 1, 0, 0.8f);
            GUI.Box(new Rect(10, 270, 250, 45), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(15, 275, 240, 20), "⚠️ OBSTÁCULO PRÓXIMO");
            GUI.Label(new Rect(15, 290, 240, 20), $"Distância: {distanciaObstaculo:F2}m");
        }

        // Controlos
        GUI.color = new Color(0, 0.5f, 0, 0.8f);
        GUI.Box(new Rect(10, 340, 250, 85), "");
        GUI.color = Color.white;
        GUI.Label(new Rect(15, 345, 240, 20), "=== CONTROLOS ===");
        GUI.Label(new Rect(15, 365, 240, 20), "WASD/Setas - Mover");
        GUI.Label(new Rect(15, 380, 240, 20), "1/2 - Modo Lento/Normal");
        GUI.Label(new Rect(15, 395, 240, 20), "T - Alternar direção");
        GUI.Label(new Rect(15, 410, 240, 20), "ESPAÇO - Travão");
    }
}