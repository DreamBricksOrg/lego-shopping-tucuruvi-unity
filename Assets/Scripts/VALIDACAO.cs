using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class VALIDACAO : MonoBehaviour
{
    private ConfigManager config;
    public bool timerRunning;
    public float totalTime;
    public float currentTime;
    public RawImage sendImage;
    public Button acceptButton;
    public Button rejectButton;
    public GameObject spinner;
    public float pollIntervalSeconds = 2f;
    public float spinnerRotateSpeed = 180f; // graus por segundo
    public static Texture2D PendingImage;
    private bool isProcessing = false;


    private void Awake()
    {
        config = new();
        totalTime = float.Parse(config.GetValue("Timer", "VALIDACAO"));
    }

    private void OnEnable()
    {
        currentTime = totalTime;
        timerRunning = true;
        isProcessing = false;

        // Popular a imagem capturada, se houver
        if (sendImage != null && PendingImage != null)
        {
            sendImage.texture = PendingImage;
            sendImage.SetNativeSize();
        }

        if (spinner != null) spinner.SetActive(false);

        // Registrar handlers dos botões
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAccept);
        if (rejectButton != null) rejectButton.onClick.AddListener(OnReject);
    }

    private void OnDisable()
    {
        if (acceptButton != null) acceptButton.onClick.RemoveListener(OnAccept);
        if (rejectButton != null) rejectButton.onClick.RemoveListener(OnReject);
    }

    private void Update()
    {
        if (!timerRunning)
            return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0)
        {
            currentTime = 0;
            timerRunning = false;

            if (!isProcessing)
            {
                SaveLog("TOTEM_VALIDACAO_TEMPO_ESGOTADO");
                // Bloqueia troca de cena se manutenção estiver em hold
                if (MANUTENCAO.Instance != null && MANUTENCAO.Instance.maintenanceScreen.activeSelf)
                {
                    Debug.Log("[VALIDACAO] Manutenção em hold; bloqueando troca de cena.");
                }
                else
                {
                    SceneManager.LoadScene("SampleScene");
                }
            }
        }

        if (spinner != null && spinner.activeInHierarchy)
        {
            spinner.transform.Rotate(0f, 0f, spinnerRotateSpeed * Time.deltaTime);
        }
    }

    private void OnAccept()
    {
        Texture tex = sendImage != null ? sendImage.texture : null;
        if (tex == null)
        {
            Debug.LogWarning("[VALIDACAO] Nenhuma imagem para enviar.");
            return;
        }

        // Desativa botões e mostra spinner
        if (acceptButton != null) acceptButton.interactable = false;
        if (rejectButton != null) rejectButton.interactable = false;
        if (spinner != null) spinner.SetActive(true);
        isProcessing = true;

        StartCoroutine(UploadAndPoll(tex));
    }

    private void OnReject()
    {
        // Limpa a imagem pendente e volta para a tela de CAPTURA
        PendingImage = null;
        if (sendImage != null) sendImage.texture = null;
        isProcessing = false;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenScreen("CAPTURA");
        }
        else
        {
            Debug.LogWarning("[VALIDACAO] UIManager.Instance está nulo; não foi possível voltar para CAPTURA.");
        }
    }

    [Serializable]
    private class UploadResponse { public string status; public string request_id; public int position_in_queue; public float estimated_wait_seconds; public string error; }

    [Serializable]
    private class JobStatusResponse { public string status; public string image_url; public string error; }

    private IEnumerator UploadAndPoll(Texture texture)
    {
        string baseUrl = null;
        try
        {
            baseUrl = config?.GetValue("Net", "comfyUIUrl");
            Debug.Log($"[VALIDACAO] comfyUIUrl: {baseUrl}");
        }
        catch { }

        string uploadUrl = $"{baseUrl.TrimEnd('/')}/api/upload";

        // Garante Texture2D
        Texture2D tex2D = texture as Texture2D;
        if (tex2D == null && texture is RenderTexture rt)
        {
            tex2D = ConvertRenderTextureToTexture2D(rt);
        }
        if (tex2D == null)
        {
            Debug.LogError("[VALIDACAO] Falha ao preparar imagem para upload.");
            ResetUIAfterProcess();
            yield break;
        }

        byte[] pngBytes = tex2D.EncodeToPNG();

        WWWForm form = new WWWForm();
        form.AddBinaryData("image", pngBytes, "image.png", "image/png");

        using (UnityWebRequest req = UnityWebRequest.Post(uploadUrl, form))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VALIDACAO] Upload falhou: {req.error}");
                ResetUIAfterProcess();
                yield break;
            }

            // Parse da resposta: espera request_id
            UploadResponse up = null;
            try { up = JsonUtility.FromJson<UploadResponse>(req.downloadHandler.text); }
            catch (Exception ex)
            {
                Debug.LogError($"[VALIDACAO] Erro ao parsear resposta de upload: {ex.Message}\n{req.downloadHandler.text}");
                ResetUIAfterProcess();
                yield break;
            }
            if (up == null || string.IsNullOrEmpty(up.request_id))
            {
                Debug.LogError($"[VALIDACAO] Resposta inválida de upload, sem request_id. Corpo: {req.downloadHandler.text}");
                ResetUIAfterProcess();
                yield break;
            }

            // Polling do status
            yield return StartCoroutine(PollJobStatus(baseUrl, up.request_id));
        }
    }

    private IEnumerator PollJobStatus(string baseUrl, string requestId)
    {
        string statusUrl = $"{baseUrl.TrimEnd('/')}/api/result?request_id={UnityWebRequest.EscapeURL(requestId)}";
        while (true)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(statusUrl))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[VALIDACAO] Falha ao consultar status: {req.error}");
                    ResetUIAfterProcess();
                    yield break;
                }

                JobStatusResponse js = null;
                try { js = JsonUtility.FromJson<JobStatusResponse>(req.downloadHandler.text); }
                catch (Exception ex)
                {
                    Debug.LogError($"[VALIDACAO] Erro ao parsear status: {ex.Message}\n{req.downloadHandler.text}");
                    ResetUIAfterProcess();
                    yield break;
                }

                if (js == null)
                {
                    Debug.LogError("[VALIDACAO] Resposta de status vazia.");
                    ResetUIAfterProcess();
                    yield break;
                }

                string st = (js.status ?? string.Empty).ToLowerInvariant();
                switch (st)
                {
                    case "queued":
                        // aguardando
                        break;
                    case "processing":
                        // processando
                        break;
                    case "error":
                        Debug.LogError($"[VALIDACAO] Erro no job: {js.error}");

                        if (MANUTENCAO.Instance.isMaintEnable)
                        {

                            SaveLog("ERRO_JOB_ENTRANDO_EM_MANUNTENCAO", js.error);

                            var manutencao = MANUTENCAO.Instance;
                            if (manutencao != null)
                            {
                                manutencao.ActivateMaintenance();
                            }
                        }

                        ResetUIAfterProcess();
                        yield break;
                    case "done":
                        if (!string.IsNullOrEmpty(js.image_url))
                        {
                            PlayerPrefs.SetString("image_url", js.image_url);
                            PlayerPrefs.Save();
                            SaveLog("TOTEM_IMAGEM_RECEBIDA", js.image_url);

                        }
                        if (UIManager.Instance != null)
                        {
                            UIManager.Instance.OpenScreen("AGRADECIMENTO");
                        }
                        ResetUIAfterProcess();
                        yield break;
                    default:
                        Debug.LogWarning($"[VALIDACAO] Status desconhecido: {js.status}");
                        break;
                }
            }
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private void ResetUIAfterProcess()
    {
        if (spinner != null) spinner.SetActive(false);
        if (acceptButton != null) acceptButton.interactable = true;
        if (rejectButton != null) rejectButton.interactable = true;
        isProcessing = false;
    }

    private Texture2D ConvertRenderTextureToTexture2D(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        return tex;
    }

    // Atualiza a imagem na tela de validação quando a captura ocorre após a mudança de tela
    public void SetImage(Texture2D img)
    {
        PendingImage = img;
        if (sendImage != null && img != null)
        {
            sendImage.texture = img;
            sendImage.SetNativeSize();
        }
    }

    void SaveLog(string message, string additional = "")
    {
        StartCoroutine(SaveLogCoroutine(message, additional));
        StartCoroutine(SaveLogInNewLogCenterCoroutine(message, "INFO", new List<string> { "totem" }, additional));
    }

    IEnumerator SaveLogCoroutine(string message, string additional)
    {
        yield return LogUtil.GetDatalogFromJsonCoroutine((dataLog) =>
        {
            if (dataLog != null)
            {
                dataLog.status = message;
                dataLog.additional = additional;
                LogUtil.SaveLog(dataLog);
            }
            else
            {
                Debug.LogError("Erro ao carregar o DataLog do JSON.");
            }
        });
    }

    IEnumerator SaveLogInNewLogCenterCoroutine(string message, string level, List<string> tags, string additional)
    {
        yield return LogUtilSdk.GetDatalogFromJsonCoroutine((dataLog) =>
       {
           if (dataLog != null)
           {
               dataLog.message = message;
               dataLog.level = level;
               dataLog.tags = tags;
               dataLog.data = new { additional };
               LogUtilSdk.SaveLogToJson(dataLog);
           }
           else
           {
               Debug.LogError("Erro ao carregar o DataLog do JSON.");
           }
       });
    }
}
