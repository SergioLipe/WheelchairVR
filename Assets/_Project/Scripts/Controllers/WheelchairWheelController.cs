using UnityEngine;

public class WheelchairWheelController : MonoBehaviour
{
    [Header("=== Joints de Viragem (Steering) ===")]
    [Tooltip("Joint central das rodas frontais - controla viragem")]
    public Transform joint4_ViragemFrontal;
    
    [Tooltip("Joint central das rodas traseiras - controla viragem")]
    public Transform joint5_ViragemTraseira;
    
    [Header("=== Joints de Rotação das Rodas ===")]
    [Tooltip("Joint da roda frontal esquerda - gira a roda")]
    public Transform joint6_RodaFrontalEsquerda;
    
    [Tooltip("Joint da roda frontal direita - gira a roda")]
    public Transform joint7_RodaFrontalDireita;
    
    [Tooltip("Joint da roda traseira esquerda - gira a roda")]
    public Transform joint8_RodaTraseiraEsquerda;
    
    [Tooltip("Joint da roda traseira direita - gira a roda")]
    public Transform joint9_RodaTraseiraDireita;
    
    [Header("=== Tipo de Cadeira de Rodas ===")]
    [Tooltip("Tipo de direção da cadeira")]
    public TipoDirecao tipoDirecao = TipoDirecao.DirecaoFrontal;
    
    [Tooltip("Tecla para alternar tipo de direção")]
    public KeyCode teclaAlternarDirecao = KeyCode.T;
    
    [Header("=== Configuração Física ===")]
    [Tooltip("Velocidade máxima da cadeira em km/h")]
    public float velocidadeMaximaKmH = 6f;
    
    [Tooltip("Diâmetro das rodas traseiras em metros")]
    public float diametroRodasTraseiras = 0.6f;  // 60cm típico
    
    [Tooltip("Diâmetro das rodas frontais em metros")]
    public float diametroRodasFrontais = 0.15f;  // 15cm típico
    
    [Tooltip("Multiplicador de velocidade de rotação")]
    public float multiplicadorVelocidade = 5f;
    
    [Header("=== Configuração de Viragem ===")]
    [Tooltip("Ângulo máximo de viragem")]
    [Range(0f, 45f)]
    public float anguloMaximoViragem = 30f;
    
    [Tooltip("Velocidade de viragem")]
    [Range(1f, 10f)]
    public float velocidadeViragem = 5f;
    
    [Header("=== Configuração de Rotação ===")]
    [Tooltip("Fazer rodas girarem de forma diferencial nas curvas")]
    public bool rotacaoDiferencial = true;
    
    [Tooltip("Intensidade da rotação diferencial")]
    [Range(0f, 2f)]
    public float intensidadeDiferencial = 0.5f;
    
    [Tooltip("Inverter direção de rotação")]
    public bool inverterRotacao = false;
    
    [Header("=== Debug Info ===")]
    [SerializeField] private float rotacaoRodaFrontalEsq = 0f;
    [SerializeField] private float rotacaoRodaFrontalDir = 0f;
    [SerializeField] private float rotacaoRodaTraseiraEsq = 0f;
    [SerializeField] private float rotacaoRodaTraseiraDir = 0f;
    [SerializeField] private float anguloViragemAtual = 0f;
    [SerializeField] private float velocidadeAtual = 0f;
    [SerializeField] private float inputViragem = 0f;
    [SerializeField] private bool estaEmMovimento = false;
    
    // Enum para tipos de direção
    public enum TipoDirecao
    {
        DirecaoFrontal,    // Rodas da frente viram (cadeira standard)
        DirecaoTraseira    // Rodas de trás viram (cadeira mais manobrável)
    }
    
    // Referências internas
    private WheelchairRealisticMovement movementScript;
    private Rigidbody rb;
    
    // Rotações iniciais para reset
    private Quaternion rotInicialJoint4;
    private Quaternion rotInicialJoint5;
    private Quaternion rotInicialJoint6;
    private Quaternion rotInicialJoint7;
    private Quaternion rotInicialJoint8;
    private Quaternion rotInicialJoint9;
    
    private Vector3 posicaoAnterior;
    
    // Eixos corretos
    private readonly Vector3 EIXO_ROTACAO = Vector3.forward;  // Z para girar as rodas
    private readonly Vector3 EIXO_VIRAGEM = Vector3.up;       // Y para virar (steering)
    
    void Start()
    {
        // Obter componentes
        movementScript = GetComponent<WheelchairRealisticMovement>();
        rb = GetComponent<Rigidbody>();
        posicaoAnterior = transform.position;
        
        // Procurar joints automaticamente
        ProcurarJointsAutomaticamente();
        
        // Guardar rotações iniciais
        GuardarRotacoesIniciais();
        
        // Verificar configuração
        VerificarConfiguracao();
        
        Debug.Log($"🦽 Cadeira de Rodas - Modo: {tipoDirecao}");
        Debug.Log($"   Tecla {teclaAlternarDirecao} para alternar tipo de direção");
    }
    
    void ProcurarJointsAutomaticamente()
    {
        Transform[] todosTransforms = GetComponentsInChildren<Transform>();
        
        foreach (Transform t in todosTransforms)
        {
            switch(t.name)
            {
                case "joint4":
                    joint4_ViragemFrontal = t;
                    Debug.Log("✅ joint4 (Viragem Frontal) encontrado!");
                    break;
                case "joint5":
                    joint5_ViragemTraseira = t;
                    Debug.Log("✅ joint5 (Viragem Traseira) encontrado!");
                    break;
                case "joint6":
                    joint6_RodaFrontalEsquerda = t;
                    Debug.Log("✅ joint6 (Roda Frontal Esquerda) encontrado!");
                    break;
                case "joint7":
                    joint7_RodaFrontalDireita = t;
                    Debug.Log("✅ joint7 (Roda Frontal Direita) encontrado!");
                    break;
                case "joint8":
                    joint8_RodaTraseiraEsquerda = t;
                    Debug.Log("✅ joint8 (Roda Traseira Esquerda) encontrado!");
                    break;
                case "joint9":
                    joint9_RodaTraseiraDireita = t;
                    Debug.Log("✅ joint9 (Roda Traseira Direita) encontrado!");
                    break;
            }
        }
    }
    
    void GuardarRotacoesIniciais()
    {
        if (joint4_ViragemFrontal != null)
            rotInicialJoint4 = joint4_ViragemFrontal.localRotation;
            
        if (joint5_ViragemTraseira != null)
            rotInicialJoint5 = joint5_ViragemTraseira.localRotation;
            
        if (joint6_RodaFrontalEsquerda != null)
            rotInicialJoint6 = joint6_RodaFrontalEsquerda.localRotation;
            
        if (joint7_RodaFrontalDireita != null)
            rotInicialJoint7 = joint7_RodaFrontalDireita.localRotation;
            
        if (joint8_RodaTraseiraEsquerda != null)
            rotInicialJoint8 = joint8_RodaTraseiraEsquerda.localRotation;
            
        if (joint9_RodaTraseiraDireita != null)
            rotInicialJoint9 = joint9_RodaTraseiraDireita.localRotation;
    }
    
    void VerificarConfiguracao()
    {
        bool tudoOk = true;
        
        if (joint4_ViragemFrontal == null)
        {
            Debug.LogWarning("⚠️ joint4 (Viragem Frontal) não encontrado!");
            tudoOk = false;
        }
        
        if (joint5_ViragemTraseira == null)
        {
            Debug.LogWarning("⚠️ joint5 (Viragem Traseira) não encontrado!");
            tudoOk = false;
        }
        
        if (tudoOk)
        {
            Debug.Log("✅ Sistema de viragem configurado!");
        }
        
        int rodasConfiguradas = 0;
        if (joint6_RodaFrontalEsquerda != null) rodasConfiguradas++;
        if (joint7_RodaFrontalDireita != null) rodasConfiguradas++;
        if (joint8_RodaTraseiraEsquerda != null) rodasConfiguradas++;
        if (joint9_RodaTraseiraDireita != null) rodasConfiguradas++;
        
        Debug.Log($"🎮 {rodasConfiguradas}/4 rodas configuradas para rotação");
    }
    
    void Update()
    {
        // Alternar tipo de direção
        if (Input.GetKeyDown(teclaAlternarDirecao))
        {
            AlternarTipoDirecao();
        }
        
        // Obter velocidade e direção
        ObterVelocidadeEDirecao();
        
        // Verificar se está em movimento
        estaEmMovimento = Mathf.Abs(velocidadeAtual) > 0.01f || Mathf.Abs(inputViragem) > 0.01f;
        
        // Aplicar viragem baseada no tipo de cadeira
        AplicarViragem();
        
        // Aplicar rotação às rodas
        AplicarRotacaoRodas();
    }
    
    void AlternarTipoDirecao()
    {
        if (tipoDirecao == TipoDirecao.DirecaoFrontal)
        {
            tipoDirecao = TipoDirecao.DirecaoTraseira;
            Debug.Log("🦽 Direção Traseira - Rodas de trás viram (mais manobrável)");
            
            // Ajustar características de movimento
            if (movementScript != null)
            {
                movementScript.velocidadeRotacao = 60f;  // Vira mais rápido
                movementScript.rotacaoNoLugar = true;    // Pode girar no lugar
            }
        }
        else
        {
            tipoDirecao = TipoDirecao.DirecaoFrontal;
            Debug.Log("🦽 Direção Frontal - Rodas da frente viram (standard)");
            
            // Ajustar características de movimento
            if (movementScript != null)
            {
                movementScript.velocidadeRotacao = 45f;  // Vira mais devagar
                movementScript.rotacaoNoLugar = false;   // Precisa de espaço para virar
            }
        }
        
        // Reset das posições de viragem ao trocar de modo
        ResetarViragem();
    }
    
    void ObterVelocidadeEDirecao()
    {
        // Método 1: Script de movimento
        if (movementScript != null)
        {
            velocidadeAtual = movementScript.GetVelocidadeNormalizada();
            inputViragem = Input.GetAxis("Horizontal");
        }
        // Método 2: Rigidbody
        else if (rb != null)
        {
            float velocidadeFrontal = Vector3.Dot(rb.linearVelocity, transform.forward);
            velocidadeAtual = velocidadeFrontal / (velocidadeMaximaKmH / 3.6f);
            inputViragem = rb.angularVelocity.y / 2f;
        }
        // Método 3: Cálculo manual
        else
        {
            Vector3 movimento = transform.position - posicaoAnterior;
            float velocidadeFrontal = Vector3.Dot(movimento / Time.deltaTime, transform.forward);
            velocidadeAtual = velocidadeFrontal / (velocidadeMaximaKmH / 3.6f);
            inputViragem = Input.GetAxis("Horizontal");
            posicaoAnterior = transform.position;
        }
        
        // Clamp dos valores
        velocidadeAtual = Mathf.Clamp(velocidadeAtual, -1f, 1f);
        inputViragem = Mathf.Clamp(inputViragem, -1f, 1f);
    }
    
    void AplicarViragem()
    {
        // Calcular ângulo de viragem alvo
        float anguloAlvo = inputViragem * anguloMaximoViragem;
        
        // Suavizar a viragem
        anguloViragemAtual = Mathf.Lerp(anguloViragemAtual, anguloAlvo, velocidadeViragem * Time.deltaTime);
        
        // Aplicar viragem baseada no tipo de direção
        if (tipoDirecao == TipoDirecao.DirecaoFrontal)
        {
            // Só as rodas da frente viram
            if (joint4_ViragemFrontal != null)
            {
                Quaternion rotacaoViragem = Quaternion.AngleAxis(anguloViragemAtual, EIXO_VIRAGEM);
                joint4_ViragemFrontal.localRotation = rotInicialJoint4 * rotacaoViragem;
            }
            
            // Rodas traseiras permanecem retas
            if (joint5_ViragemTraseira != null)
            {
                joint5_ViragemTraseira.localRotation = rotInicialJoint5;
            }
        }
        else // TipoDirecao.DirecaoTraseira
        {
            // Só as rodas de trás viram
            if (joint5_ViragemTraseira != null)
            {
                Quaternion rotacaoViragem = Quaternion.AngleAxis(anguloViragemAtual, EIXO_VIRAGEM);
                joint5_ViragemTraseira.localRotation = rotInicialJoint5 * rotacaoViragem;
            }
            
            // Rodas frontais permanecem retas
            if (joint4_ViragemFrontal != null)
            {
                joint4_ViragemFrontal.localRotation = rotInicialJoint4;
            }
        }
    }
    
    void AplicarRotacaoRodas()
    {
        // Calcular velocidade de rotação base das rodas traseiras
        float circunferenciaTraseira = Mathf.PI * diametroRodasTraseiras;
        float rotacoesPorMetroTraseira = 1f / circunferenciaTraseira;
        float velocidadeMetrosPorSegundo = velocidadeAtual * (velocidadeMaximaKmH / 3.6f);
        float rotacoesPorSegundoTraseira = velocidadeMetrosPorSegundo * rotacoesPorMetroTraseira;
        float grausPorSegundoTraseira = rotacoesPorSegundoTraseira * 360f * multiplicadorVelocidade;
        
        // Calcular velocidade de rotação das rodas frontais
        float circunferenciaFrontal = Mathf.PI * diametroRodasFrontais;
        float rotacoesPorMetroFrontal = 1f / circunferenciaFrontal;
        float rotacoesPorSegundoFrontal = velocidadeMetrosPorSegundo * rotacoesPorMetroFrontal;
        float grausPorSegundoFrontal = rotacoesPorSegundoFrontal * 360f * multiplicadorVelocidade;
        
        if (inverterRotacao)
        {
            grausPorSegundoTraseira = -grausPorSegundoTraseira;
            grausPorSegundoFrontal = -grausPorSegundoFrontal;
        }
        
        // Calcular rotação diferencial baseada na viragem
        float deltaRotacaoEsquerda = 1f;
        float deltaRotacaoDireita = 1f;
        
        if (rotacaoDiferencial && Mathf.Abs(inputViragem) > 0.01f)
        {
            // Ajustar diferencial baseado no tipo de direção
            float intensidade = intensidadeDiferencial;
            
            if (tipoDirecao == TipoDirecao.DirecaoTraseira)
            {
                // Direção traseira tem diferencial mais agressivo
                intensidade *= 1.5f;
            }
            
            if (inputViragem > 0)  // Virando para a direita
            {
                deltaRotacaoEsquerda = 1f + (Mathf.Abs(inputViragem) * intensidade);
                deltaRotacaoDireita = 1f - (Mathf.Abs(inputViragem) * intensidade * 0.5f);
            }
            else  // Virando para a esquerda
            {
                deltaRotacaoDireita = 1f + (Mathf.Abs(inputViragem) * intensidade);
                deltaRotacaoEsquerda = 1f - (Mathf.Abs(inputViragem) * intensidade * 0.5f);
            }
        }
        
        // Atualizar rotações
        rotacaoRodaTraseiraEsq += grausPorSegundoTraseira * deltaRotacaoEsquerda * Time.deltaTime;
        rotacaoRodaTraseiraDir += grausPorSegundoTraseira * deltaRotacaoDireita * Time.deltaTime;
        rotacaoRodaFrontalEsq += grausPorSegundoFrontal * deltaRotacaoEsquerda * Time.deltaTime;
        rotacaoRodaFrontalDir += grausPorSegundoFrontal * deltaRotacaoDireita * Time.deltaTime;
        
        // Aplicar rotação às rodas
        if (joint8_RodaTraseiraEsquerda != null)
        {
            Quaternion rotacao = Quaternion.AngleAxis(rotacaoRodaTraseiraEsq, EIXO_ROTACAO);
            joint8_RodaTraseiraEsquerda.localRotation = rotInicialJoint8 * rotacao;
        }
        
        if (joint9_RodaTraseiraDireita != null)
        {
            Quaternion rotacao = Quaternion.AngleAxis(rotacaoRodaTraseiraDir, EIXO_ROTACAO);
            joint9_RodaTraseiraDireita.localRotation = rotInicialJoint9 * rotacao;
        }
        
        if (joint6_RodaFrontalEsquerda != null)
        {
            Quaternion rotacao = Quaternion.AngleAxis(rotacaoRodaFrontalEsq, EIXO_ROTACAO);
            joint6_RodaFrontalEsquerda.localRotation = rotInicialJoint6 * rotacao;
        }
        
        if (joint7_RodaFrontalDireita != null)
        {
            Quaternion rotacao = Quaternion.AngleAxis(rotacaoRodaFrontalDir, EIXO_ROTACAO);
            joint7_RodaFrontalDireita.localRotation = rotInicialJoint7 * rotacao;
        }
    }
    
    void ResetarViragem()
    {
        anguloViragemAtual = 0f;
        
        if (joint4_ViragemFrontal != null)
            joint4_ViragemFrontal.localRotation = rotInicialJoint4;
            
        if (joint5_ViragemTraseira != null)
            joint5_ViragemTraseira.localRotation = rotInicialJoint5;
    }
    
    // Métodos públicos úteis
    public void PararRodas()
    {
        rotacaoRodaFrontalEsq = 0f;
        rotacaoRodaFrontalDir = 0f;
        rotacaoRodaTraseiraEsq = 0f;
        rotacaoRodaTraseiraDir = 0f;
        anguloViragemAtual = 0f;
        velocidadeAtual = 0f;
        inputViragem = 0f;
        
        ResetarViragem();
        
        if (joint6_RodaFrontalEsquerda != null)
            joint6_RodaFrontalEsquerda.localRotation = rotInicialJoint6;
            
        if (joint7_RodaFrontalDireita != null)
            joint7_RodaFrontalDireita.localRotation = rotInicialJoint7;
            
        if (joint8_RodaTraseiraEsquerda != null)
            joint8_RodaTraseiraEsquerda.localRotation = rotInicialJoint8;
            
        if (joint9_RodaTraseiraDireita != null)
            joint9_RodaTraseiraDireita.localRotation = rotInicialJoint9;
        
        Debug.Log("🛑 Todas as rodas paradas e resetadas!");
    }
    
    public bool EstaEmMovimento()
    {
        return estaEmMovimento;
    }
    
    public float GetVelocidadeAtual()
    {
        return velocidadeAtual;
    }
    
    public TipoDirecao GetTipoDirecao()
    {
        return tipoDirecao;
    }
    
    // Interface de debug simplificada
    void OnGUI()
    {
        if (!Application.isEditor) return;
        
        // Info do tipo de direção
        GUI.color = tipoDirecao == TipoDirecao.DirecaoFrontal ? Color.cyan : Color.magenta;
        GUI.Box(new Rect(10, 10, 220, 80), "");
        
        GUI.color = Color.white;
        GUI.Label(new Rect(15, 15, 210, 20), $"Tipo: {(tipoDirecao == TipoDirecao.DirecaoFrontal ? "Direção Frontal" : "Direção Traseira")}");
        GUI.Label(new Rect(15, 35, 210, 20), $"Rodas que viram: {(tipoDirecao == TipoDirecao.DirecaoFrontal ? "Frontais" : "Traseiras")}");
        GUI.Label(new Rect(15, 55, 210, 20), $"Velocidade: {(velocidadeAtual * velocidadeMaximaKmH):F1} km/h");
        GUI.Label(new Rect(15, 70, 210, 20), $"Tecla {teclaAlternarDirecao} para alternar");
    }
}