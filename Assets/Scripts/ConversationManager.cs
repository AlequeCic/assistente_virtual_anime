using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class WebhookRequest
{
    public string audio;
    public int sampleRate;
    public int channels;
}

[Serializable]
public class WebhookResponse
{
    public string audio;
    public string animation;
}

public class ConversationManager : MonoBehaviour
{
    [Header("Audio / Microphone")]
    public AudioSource audioSource;
    public int sampleRate = 16000;
    public int maxRecordTime = 60;
    public string micDevice = "";

    [Header("Silence detection")]
    public float silenceThreshold = 0.01f;
    public float silenceTimeout = 3f;

    [Header("Webhook")]
    public string webhookUrl = "https://auto.intbin.com.br/webhook/base44";

    [Header("uLipSync Integration")]
    public uLipSync.uLipSync uLipSyncComponent;

    [Header("Animation Control - Deixe vazio para busca automática")]
    public GameObject characterObject;
    private Animator characterAnimator;

    // Nomes dos parâmetros
    private const string WAVING_TRIGGER = "Waving";
    private const string IDLE_ARMS_BOOL = "IdleArms";
    private const string IDLE_SHY_BOOL = "IdleShy";

    private AudioClip recordingClip;
    private bool isRecording = false;
    private float silenceTimer = 0f;

    private void Start()
    {
        // Configura AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("🔊 AudioSource criado automaticamente");
            }
        }

        // Busca automática do personagem
        FindCharacterAutomatically();

        ConfigureAudioSource();
        ConfigureuLipSync();
        ResetToNeutralAnimation();

        if (Microphone.devices.Length > 0 && string.IsNullOrEmpty(micDevice))
            micDevice = Microphone.devices[0];

        StartRecording();
    }

    /// <summary>
    /// Busca automaticamente o personagem na cena - PRIORIZANDO "feira_2025"
    /// </summary>
    private void FindCharacterAutomatically()
    {
        Debug.Log("🔍 Iniciando busca automática do personagem...");

        // Se já foi configurado manualmente, usa esse
        if (characterObject != null && characterAnimator != null)
        {
            Debug.Log($"✅ Usando personagem configurado manualmente: {characterObject.name}");
            return;
        }

        // Lista todas as estratégias de busca - PRIORIDADE PARA feira_2025
        var searchStrategies = new System.Func<Animator>[]
        {
            FindByExactName,           // PRIORIDADE 1: Nome exato "feira_2025"
            FindByCommonCharacterNames, // PRIORIDADE 2: Nomes comuns
            FindByAnimatorWithParameters, // PRIORIDADE 3: Animator com parâmetros certos
            FindAnyAnimatorInScene,    // PRIORIDADE 4: Qualquer Animator
            FindByTag,                 // PRIORIDADE 5: Por tag
            FindByNameContains         // PRIORIDADE 6: Por nome que contém
        };

        // Tenta cada estratégia até encontrar
        foreach (var strategy in searchStrategies)
        {
            characterAnimator = strategy();
            if (characterAnimator != null)
            {
                characterObject = characterAnimator.gameObject;
                Debug.Log($"🎯 Personagem encontrado via {GetStrategyName(strategy.Method.Name)}: {characterObject.name}");
                break;
            }
        }

        if (characterAnimator == null)
        {
            Debug.LogError("❌ Não foi possível encontrar o personagem na cena!");
            Debug.Log("💡 Dicas:");
            Debug.Log("💡 1. Arraste o objeto 'feira_2025' para 'Character Object' no Inspector");
            Debug.Log("💡 2. Certifique-se que o objeto tem um componente Animator");
            Debug.Log("💡 3. Verifique se os parâmetros Waving, IdleArms e IdleShy existem no Animator");
        }
        else
        {
            Debug.Log($"✅ Personagem configurado: {characterObject.name}");
            CheckAnimatorParameters();
        }
    }

    private string GetStrategyName(string methodName)
    {
        return methodName switch
        {
            "FindByExactName" => "NOME EXATO 'feira_2025'",
            "FindByCommonCharacterNames" => "NOMES COMUNS",
            "FindByAnimatorWithParameters" => "PARÂMETROS DE ANIMAÇÃO",
            "FindAnyAnimatorInScene" => "QUALQUER ANIMATOR",
            "FindByTag" => "TAG",
            "FindByNameContains" => "NOME PARCIAL",
            _ => methodName
        };
    }

    /// <summary>
    /// PRIORIDADE 1: Busca pelo nome exato "feira_2025"
    /// </summary>
    private Animator FindByExactName()
    {
        Debug.Log("🎯 Buscando objeto com nome exato 'feira_2025'...");

        GameObject obj = GameObject.Find("feira_2025");
        if (obj != null)
        {
            Debug.Log("✅ Objeto 'feira_2025' encontrado!");

            // Procura Animator no objeto principal
            Animator animator = obj.GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log("✅ Animator encontrado no objeto principal 'feira_2025'");
                return animator;
            }

            // Procura Animator nos filhos
            animator = obj.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                Debug.Log($"✅ Animator encontrado nos filhos de 'feira_2025': {animator.gameObject.name}");
                return animator;
            }

            Debug.LogWarning("⚠️ Objeto 'feira_2025' encontrado, mas não tem Animator");
        }
        else
        {
            Debug.Log("❌ Objeto 'feira_2025' não encontrado na cena");
        }

        return null;
    }

    /// <summary>
    /// PRIORIDADE 2: Busca por nomes comuns de personagem
    /// </summary>
    private Animator FindByCommonCharacterNames()
    {
        Debug.Log("🔍 Buscando por nomes comuns de personagem...");

        string[] commonNames = {
            "Player", "Character", "Personagem", "Avatar", "Boneco",
            "Person", "Model", "Mesh", "Armature", "Root",
            "XR Origin", "OVRPlayer", "FirstPersonPlayer"
        };

        foreach (string name in commonNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Animator animator = obj.GetComponent<Animator>();
                if (animator != null)
                {
                    Debug.Log($"✅ Animator encontrado em objeto comum: {name}");
                    return animator;
                }

                animator = obj.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    Debug.Log($"✅ Animator encontrado nos filhos de: {name}");
                    return animator;
                }
            }
        }

        Debug.Log("❌ Nenhum objeto com nome comum encontrado");
        return null;
    }

    /// <summary>
    /// PRIORIDADE 3: Busca por qualquer Animator que tenha os parâmetros que precisamos
    /// </summary>
    private Animator FindByAnimatorWithParameters()
    {
        Debug.Log("🔍 Buscando Animator com parâmetros Waving, IdleArms, IdleShy...");

        Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsSortMode.None);

        foreach (Animator animator in allAnimators)
        {
            if (HasRequiredParameters(animator))
            {
                Debug.Log($"🎭 Animator com parâmetros encontrado: {animator.gameObject.name}");
                return animator;
            }
        }

        Debug.Log("❌ Nenhum Animator com os parâmetros necessários encontrado");
        return null;
    }

    /// <summary>
    /// PRIORIDADE 4: Busca qualquer Animator na cena
    /// </summary>
    private Animator FindAnyAnimatorInScene()
    {
        Debug.Log("🔍 Buscando qualquer Animator na cena...");

        Animator animator = FindAnyObjectByType<Animator>();
        if (animator != null)
        {
            Debug.Log($"🔎 Primeiro Animator encontrado: {animator.gameObject.name}");
            return animator;
        }

        Debug.Log("❌ Nenhum Animator encontrado na cena");
        return null;
    }

    /// <summary>
    /// PRIORIDADE 5: Busca por tag
    /// </summary>
    private Animator FindByTag()
    {
        Debug.Log("🔍 Buscando por tags comuns...");

        string[] commonTags = { "Player", "Character", "MainCharacter" };

        foreach (string tag in commonTags)
        {
            try
            {
                GameObject obj = GameObject.FindWithTag(tag);
                if (obj != null)
                {
                    Animator animator = obj.GetComponent<Animator>();
                    if (animator != null)
                    {
                        Debug.Log($"✅ Animator encontrado por tag: {tag}");
                        return animator;
                    }

                    animator = obj.GetComponentInChildren<Animator>(true);
                    if (animator != null)
                    {
                        Debug.Log($"✅ Animator encontrado nos filhos da tag: {tag}");
                        return animator;
                    }
                }
            }
            catch (UnityException) { } // Tag não existe
        }

        Debug.Log("❌ Nenhum objeto com tags comuns encontrado");
        return null;
    }

    /// <summary>
    /// PRIORIDADE 6: Busca por nome que contenha palavras-chave
    /// </summary>
    private Animator FindByNameContains()
    {
        Debug.Log("🔍 Buscando por nomes que contenham palavras-chave...");

        string[] keywords = { "feira", "2025", "char", "player", "person", "avatar", "model" };

        Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsSortMode.None);

        foreach (Animator animator in allAnimators)
        {
            string objName = animator.gameObject.name.ToLower();
            foreach (string keyword in keywords)
            {
                if (objName.Contains(keyword))
                {
                    Debug.Log($"🔍 Animator encontrado por nome contendo '{keyword}': {animator.gameObject.name}");
                    return animator;
                }
            }
        }

        Debug.Log("❌ Nenhum Animator encontrado por nome parcial");
        return null;
    }

    /// <summary>
    /// Verifica se o Animator tem os parâmetros necessários
    /// </summary>
    private bool HasRequiredParameters(Animator animator)
    {
        if (animator == null) return false;

        bool hasWaving = false;
        bool hasIdleArms = false;
        bool hasIdleShy = false;

        foreach (var param in animator.parameters)
        {
            if (param.name == WAVING_TRIGGER && param.type == AnimatorControllerParameterType.Trigger)
                hasWaving = true;
            else if (param.name == IDLE_ARMS_BOOL && param.type == AnimatorControllerParameterType.Bool)
                hasIdleArms = true;
            else if (param.name == IDLE_SHY_BOOL && param.type == AnimatorControllerParameterType.Bool)
                hasIdleShy = true;
        }

        return hasWaving && hasIdleArms && hasIdleShy;
    }

    /// <summary>
    /// Verifica os parâmetros do Animator
    /// </summary>
    private void CheckAnimatorParameters()
    {
        if (characterAnimator == null) return;

        Debug.Log("🔍 Verificando parâmetros do Animator...");

        bool hasWaving = false;
        bool hasIdleArms = false;
        bool hasIdleShy = false;

        foreach (var param in characterAnimator.parameters)
        {
            if (param.name == WAVING_TRIGGER)
            {
                hasWaving = (param.type == AnimatorControllerParameterType.Trigger);
                Debug.Log($"✅ {WAVING_TRIGGER}: {param.type}");
            }
            else if (param.name == IDLE_ARMS_BOOL)
            {
                hasIdleArms = (param.type == AnimatorControllerParameterType.Bool);
                Debug.Log($"✅ {IDLE_ARMS_BOOL}: {param.type}");
            }
            else if (param.name == IDLE_SHY_BOOL)
            {
                hasIdleShy = (param.type == AnimatorControllerParameterType.Bool);
                Debug.Log($"✅ {IDLE_SHY_BOOL}: {param.type}");
            }
        }

        if (!hasWaving) Debug.LogError($"❌ Parâmetro '{WAVING_TRIGGER}' não encontrado!");
        if (!hasIdleArms) Debug.LogError($"❌ Parâmetro '{IDLE_ARMS_BOOL}' não encontrado!");
        if (!hasIdleShy) Debug.LogError($"❌ Parâmetro '{IDLE_SHY_BOOL}' não encontrado!");

        if (hasWaving && hasIdleArms && hasIdleShy)
        {
            Debug.Log("🎉 Todos os parâmetros encontrados! Pronto para animar!");

            // Teste automático das animações
            Debug.Log("🧪 Executando teste automático das animações...");
            StartCoroutine(AutoTestAnimations());
        }
    }

    /// <summary>
    /// Teste automático das animações
    /// </summary>
    private IEnumerator AutoTestAnimations()
    {
        yield return new WaitForSeconds(1f);

        Debug.Log("🧪 TESTE: Acionando Waving...");
        TriggerWaving();
        yield return new WaitForSeconds(3f);

        Debug.Log("🧪 TESTE: Ativando IdleArms...");
        SetIdleArms();
        yield return new WaitForSeconds(3f);

        Debug.Log("🧪 TESTE: Ativando IdleShy...");
        SetIdleShy();
        yield return new WaitForSeconds(3f);

        Debug.Log("🧪 TESTE: Resetando para neutro...");
        ResetToNeutralAnimation();

        Debug.Log("✅ Teste automático concluído!");
    }

    private void ConfigureuLipSync()
    {
        if (uLipSyncComponent == null)
            uLipSyncComponent = GetComponent<uLipSync.uLipSync>();

        var uLipSyncMic = FindAnyObjectByType<uLipSync.uLipSyncMicrophone>();
        if (uLipSyncMic != null)
        {
            uLipSyncMic.enabled = false;
            Debug.Log("🎤 uLipSync Microphone desativado");
        }

        if (uLipSyncComponent != null)
        {
            Debug.Log("✅ uLipSync encontrado");
        }
    }

    private void ConfigureAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
    }

    private void Update()
    {
        if (!isRecording) return;

        int pos = Microphone.GetPosition(micDevice);
        if (pos <= 0) return;

        int sampleWindow = 1024;
        if (pos < sampleWindow) return;

        float[] samples = new float[sampleWindow];
        int start = pos - sampleWindow;
        recordingClip.GetData(samples, start);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += Mathf.Abs(samples[i]);

        float avg = sum / samples.Length;

        if (avg < silenceThreshold)
        {
            silenceTimer += Time.deltaTime;
            if (silenceTimer >= silenceTimeout)
            {
                StopAndSend();
                silenceTimer = 0f;
            }
        }
        else
        {
            silenceTimer = 0f;
        }
    }

    public void StartRecording()
    {
        if (isRecording) return;
        Debug.Log("🎤 Iniciando gravação...");
        recordingClip = Microphone.Start(micDevice, false, maxRecordTime, sampleRate);
        isRecording = true;
        silenceTimer = 0f;

        ResetToNeutralAnimation();
    }

    private void StopAndSend()
    {
        if (!isRecording) return;

        int pos = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;

        if (pos <= 0)
        {
            Debug.LogWarning("Nada foi gravado.");
            StartRecording();
            return;
        }

        float[] samples = new float[pos * recordingClip.channels];
        recordingClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("trimmed", pos, recordingClip.channels, recordingClip.frequency, false);
        trimmed.SetData(samples, 0);

        StartCoroutine(ProcessAndSend(trimmed));
    }

    private IEnumerator ProcessAndSend(AudioClip clip)
    {
        Debug.Log("📤 Enviando áudio para API...");

        byte[] wav = WavUtility.FromAudioClip(clip);
        string base64 = Convert.ToBase64String(wav);

        WebhookRequest req = new WebhookRequest
        {
            audio = base64,
            sampleRate = clip.frequency,
            channels = clip.channels
        };
        string json = JsonUtility.ToJson(req);

        using (UnityWebRequest www = new UnityWebRequest(webhookUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ Erro na API: {www.error}");
                StartRecording();
            }
            else
            {
                Debug.Log("✅ Resposta recebida da API");
                string respText = www.downloadHandler.text;
                Debug.Log($"📄 Resposta JSON: {respText}");

                WebhookResponse resp = null;
                try
                {
                    resp = JsonUtility.FromJson<WebhookResponse>(respText);
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Erro no JSON: {e}");
                    StartRecording();
                    yield break;
                }

                if (resp != null && !string.IsNullOrEmpty(resp.audio))
                {
                    if (!string.IsNullOrEmpty(resp.animation))
                    {
                        Debug.Log($"🎭 Animação recebida da API: {resp.animation}");
                        ExecuteAnimation(resp.animation);
                    }

                    yield return StartCoroutine(PlayBase64Audio(resp.audio));
                }
                else
                {
                    Debug.LogWarning("⚠️ Nenhum áudio retornado pela API");
                    StartRecording();
                }
            }
        }
    }

    private void ExecuteAnimation(string animationName)
    {
        if (characterAnimator == null)
        {
            Debug.LogWarning("⚠️ Animator não configurado");
            return;
        }

        Debug.Log($"🎬 Executando animação: {animationName}");

        string anim = animationName.ToLower().Trim();

        switch (anim)
        {
            case "waving":
                TriggerWaving();
                break;
            case "idlearms":
                SetIdleArms();
                break;
            case "idleshy":
                SetIdleShy();
                break;
            case "none":
                ResetToNeutralAnimation();
                break;
            default:
                Debug.LogWarning($"⚠️ Animação desconhecida: {animationName}");
                ResetToNeutralAnimation();
                break;
        }
    }

    // ========== MÉTODOS DE ANIMAÇÃO CORRIGIDOS ==========

    public void TriggerWaving()
    {
        if (characterAnimator == null) return;

        try
        {
            // Para Waving, apenas reseta os bools mas mantém o trigger
            characterAnimator.SetBool(IDLE_ARMS_BOOL, false);
            characterAnimator.SetBool(IDLE_SHY_BOOL, false);
            characterAnimator.SetTrigger(WAVING_TRIGGER);
            Debug.Log($"👋 Acionado: {WAVING_TRIGGER}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Erro ao acionar {WAVING_TRIGGER}: {e.Message}");
        }
    }

    public void SetIdleArms()
    {
        if (characterAnimator == null) return;

        try
        {
            // Para IdleArms, desativa Shy e ativa Arms
            characterAnimator.SetBool(IDLE_SHY_BOOL, false);
            characterAnimator.SetBool(IDLE_ARMS_BOOL, true);
            characterAnimator.ResetTrigger(WAVING_TRIGGER);
            Debug.Log($"💪 Ativado: {IDLE_ARMS_BOOL} = true | {IDLE_SHY_BOOL} = false");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Erro ao definir {IDLE_ARMS_BOOL}: {e.Message}");
        }
    }

    public void SetIdleShy()
    {
        if (characterAnimator == null) return;

        try
        {
            // Para IdleShy, desativa Arms e ativa Shy
            characterAnimator.SetBool(IDLE_ARMS_BOOL, false);
            characterAnimator.SetBool(IDLE_SHY_BOOL, true);
            characterAnimator.ResetTrigger(WAVING_TRIGGER);
            Debug.Log($"😊 Ativado: {IDLE_SHY_BOOL} = true | {IDLE_ARMS_BOOL} = false");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Erro ao definir {IDLE_SHY_BOOL}: {e.Message}");
        }
    }

    public void ResetToNeutralAnimation()
    {
        if (characterAnimator == null) return;

        try
        {
            characterAnimator.SetBool(IDLE_ARMS_BOOL, false);
            characterAnimator.SetBool(IDLE_SHY_BOOL, false);
            characterAnimator.ResetTrigger(WAVING_TRIGGER);
            Debug.Log("🔄 Reset para neutro - Todos os parâmetros desativados");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Erro ao resetar animação: {e.Message}");
        }
    }

    // ========== MÉTODOS DE ÁUDIO ==========

    private IEnumerator PlayBase64Audio(string audioBase64)
    {
        Debug.Log($"🎯 Convertendo base64 para áudio...");
        byte[] audioBytes = Convert.FromBase64String(audioBase64);

        AudioClip audioClip = null;
        bool isWav = true;

        try
        {
            audioClip = WavUtility.ToAudioClip(audioBytes);
            Debug.Log("✅ Áudio carregado como WAV");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Não é WAV, tentando como MP3: {e.Message}");
            isWav = false;
        }

        if (isWav && audioClip != null)
        {
            yield return StartCoroutine(PlayAudioWithLipSync(audioClip));
        }
        else
        {
            yield return StartCoroutine(LoadMp3FromBytes(audioBytes));
        }
    }

    private IEnumerator LoadMp3FromBytes(byte[] mp3Bytes)
    {
        string tempPath = Application.persistentDataPath + "/temp_response.mp3";
        System.IO.File.WriteAllBytes(tempPath, mp3Bytes);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            try { System.IO.File.Delete(tempPath); } catch { }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ Erro ao carregar MP3: {www.error}");
                StartRecording();
                yield break;
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
            if (audioClip != null)
            {
                yield return StartCoroutine(PlayAudioWithLipSync(audioClip));
            }
            else
            {
                Debug.LogError("❌ Falha ao criar AudioClip do MP3");
                StartRecording();
            }
        }
    }

    private IEnumerator PlayAudioWithLipSync(AudioClip audioClip)
    {
        Debug.Log($"🎵 Iniciando fala do personagem");

        if (audioSource.isPlaying) audioSource.Stop();
        yield return null;

        audioSource.clip = audioClip;
        audioSource.Play();

        yield return new WaitWhile(() => audioSource.isPlaying);

        Debug.Log("🎬 Fim da fala");
        yield return new WaitForSeconds(2f);
        ResetToNeutralAnimation();
        yield return new WaitForSeconds(0.5f);
        Debug.Log("🎤 Voltando a gravar...");
        StartRecording();
    }

    // ========== MÉTODOS PÚBLICOS ==========

    [ContextMenu("🔍 Buscar Personagem Novamente")]
    public void SearchCharacterAgain()
    {
        FindCharacterAutomatically();
    }

    [ContextMenu("👋 Testar Waving")]
    public void TestWaving() => TriggerWaving();

    [ContextMenu("💪 Testar IdleArms")]
    public void TestIdleArms() => SetIdleArms();

    [ContextMenu("😊 Testar IdleShy")]
    public void TestIdleShy() => SetIdleShy();

    [ContextMenu("🔄 Reset Animação")]
    public void TestResetAnimation() => ResetToNeutralAnimation();

    [ContextMenu("🎭 Debug: Ver Estado Atual")]
    public void DebugCurrentState()
    {
        if (characterAnimator == null)
        {
            Debug.Log("❌ Animator não encontrado");
            return;
        }

        bool arms = characterAnimator.GetBool(IDLE_ARMS_BOOL);
        bool shy = characterAnimator.GetBool(IDLE_SHY_BOOL);

        Debug.Log($"🔍 Estado atual: {IDLE_ARMS_BOOL}={arms}, {IDLE_SHY_BOOL}={shy}");
    }

    public void ForceStopAndSend() { if (isRecording) StopAndSend(); }
    public void StopAudio() { if (audioSource.isPlaying) audioSource.Stop(); StartRecording(); }
}