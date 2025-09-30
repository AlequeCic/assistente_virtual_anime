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

    private AudioClip recordingClip;
    private bool isRecording = false;
    private float silenceTimer = 0f;

    private void Start()
    {
        // GARANTE que temos um AudioSource
        if (audioSource == null) 
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("🔊 AudioSource criado automaticamente");
            }
        }
        
        ConfigureAudioSource();
        ConfigureuLipSync();
        
        if (Microphone.devices.Length > 0 && string.IsNullOrEmpty(micDevice))
            micDevice = Microphone.devices[0];
        
        StartRecording();
    }

    private void ConfigureuLipSync()
    {
        // Encontra uLipSync se não foi atribuído
        if (uLipSyncComponent == null)
            uLipSyncComponent = GetComponent<uLipSync.uLipSync>();
        
        // Desativa uLipSync Microphone
        var uLipSyncMic = UnityEngine.Object.FindAnyObjectByType<uLipSync.uLipSyncMicrophone>();
        if (uLipSyncMic != null)
        {
            uLipSyncMic.enabled = false;
            Debug.Log("🎤 uLipSync Microphone desativado");
        }
        
        if (uLipSyncComponent != null)
        {
            Debug.Log("✅ uLipSync encontrado");
            
            // VERIFICA configuração manual no Inspector
            CheckManualConfiguration();
        }
        else
        {
            Debug.LogWarning("⚠️ uLipSync não encontrado - adicione o componente uLipSync");
        }
    }

    private void ConfigureAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.priority = 128;
        audioSource.volume = 1f;
        audioSource.pitch = 1f;
    }

    /// <summary>
    /// Verifica se o uLipSync está configurado manualmente no Inspector
    /// </summary>
    private void CheckManualConfiguration()
    {
        if (uLipSyncComponent == null) return;

        try
        {
            var type = uLipSyncComponent.GetType();
            
            // Verifica Audio Source Proxy
            var proxyProp = type.GetProperty("audioSourceProxy");
            if (proxyProp != null)
            {
                var currentProxy = proxyProp.GetValue(uLipSyncComponent) as AudioSource;
                if (currentProxy != null)
                {
                    Debug.Log($"✅ uLipSync: Audio Source Proxy configurado: {currentProxy.name}");
                    return;
                }
            }
            
            // Verifica campo _audioSource
            var audioSourceField = type.GetField("_audioSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (audioSourceField != null)
            {
                var currentAudioSource = audioSourceField.GetValue(uLipSyncComponent) as AudioSource;
                if (currentAudioSource != null)
                {
                    Debug.Log($"✅ uLipSync: AudioSource interno configurado: {currentAudioSource.name}");
                    return;
                }
            }
            
            Debug.LogWarning("❌ uLipSync: Nenhum AudioSource configurado");
            Debug.Log("💡 CONFIGURE MANUALMENTE NO INSPECTOR:");
            Debug.Log("💡 No componente uLipSync -> Audio Source Proxy");
            Debug.Log("💡 Arraste o AudioSource para este campo");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro verificando uLipSync: {e.Message}");
        }
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

    private IEnumerator PlayBase64Audio(string audioBase64)
    {
        Debug.Log($"🎯 Convertendo base64 para áudio...");
        
        byte[] audioBytes = Convert.FromBase64String(audioBase64);
        Debug.Log($"📊 Bytes do áudio: {audioBytes.Length} bytes");

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
        
        Debug.Log($"📁 Arquivo temporário criado: {tempPath}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            try { System.IO.File.Delete(tempPath); } 
            catch (Exception e) { Debug.LogWarning($"Erro ao limpar arquivo: {e.Message}"); }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ Erro ao carregar MP3: {www.error}");
                StartRecording();
                yield break;
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
            
            if (audioClip != null)
            {
                Debug.Log("✅ MP3 carregado com sucesso");
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
        
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            yield return null;
        }

        audioSource.clip = audioClip;
        audioSource.Play();

        Debug.Log($"⏱️ Duração do áudio: {audioClip.length:F2}s");

        // Verifica se uLipSync está pronto
        if (uLipSyncComponent != null)
        {
            Debug.Log("🔊 Áudio tocando - uLipSync deve detectar automaticamente");
        }

        // Aguarda o áudio terminar
        yield return new WaitWhile(() => audioSource.isPlaying);

        Debug.Log("🎬 Fim da fala");
        
        yield return new WaitForSeconds(0.5f);
        Debug.Log("🎤 Voltando a gravar...");
        StartRecording();
    }

    public void ForceStopAndSend()
    {
        if (isRecording)
            StopAndSend();
    }

    public void StopAudio()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        StartRecording();
    }

    [ContextMenu("Verificar Configuração uLipSync")]
    public void CheckuLipSyncConfig()
    {
        Debug.Log("🔍 Verificando configuração uLipSync...");
        CheckManualConfiguration();
    }
}