using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;

public class QRCODE : MonoBehaviour
{
   
    private ConfigManager config;
    public bool timerRunning;
    public float totalTime;
    public float currentTime;
    private string serverUrl;

    // Tentativas de obter QR e atraso entre elas
    [Header("Retries de QR")]
    public int maxQrRetries = 5;
    public float retryDelaySeconds = 2f;

    public RawImage qrCodeImage;

    [Header("Polling de Sessão")]
    public int sessionPollMaxAttempts = 60;
    public float sessionPollDelaySeconds = 2f;
    private string currentSessionId;
    private bool sessionPollingStarted;

    private void Awake()
    {
        config = new();
        totalTime = float.Parse(config.GetValue("Timer", "QRCODE"));
        serverUrl = config.GetValue("Net", "serverUrl");
    }

    private void OnEnable()
    {
        currentTime = totalTime;
        timerRunning = true;

        StartCoroutine(FetchAndApplyQr());
    }

   
    private void Update()
    {
        OnEspaceKeyDown();

        if (!timerRunning)
            return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0)
        {
            currentTime = 0;
            timerRunning = false;
            SaveLog("TOTEM_QRCODE_TEMPO_ESGOTADO");
        }

        // if (UDPReceiver.Instance != null)
        // {
        //     string latest = UDPReceiver.Instance.GetLastestData();
        //     if (!string.IsNullOrEmpty(latest))
        //     {
        //         if (latest.ToUpper() == "INSTRUCOES")
        //         {
        //             UIManager.Instance.OpenScreen(latest.ToUpper());
        //         }
        //     }
        // }
    }

    private void OnEspaceKeyDown(){
        if (Input.GetKeyDown(KeyCode.Space))
        {
            UIManager.Instance.OpenScreen("INSTRUCOES");
        }
    }

    [System.Serializable]
    private class QrGenResponse
    {
        public string qr_png;
        public string qr_svg;
        public string short_url;
        public string slug;
        public string sessionId;
    }

    private IEnumerator FetchAndApplyQr()
    {
        string endpoint = (serverUrl ?? string.Empty).TrimEnd('/') + "/totem/qrcode/init";
        int attempts = 0;
        bool success = false;

        while (attempts < Mathf.Max(1, maxQrRetries) && !success)
        {
            attempts++;
            Debug.Log($"[QRCODE] Tentativa {attempts}/{maxQrRetries} de obter QR em '{endpoint}'");

            using (UnityWebRequest req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    Debug.LogWarning($"[QRCODE] Falha na tentativa {attempts}: {req.error}");
                }
                else
                {
                    var json = req.downloadHandler.text;
                    QrGenResponse resp = null;
                    try
                    {
                        resp = JsonUtility.FromJson<QrGenResponse>(json);
                        Debug.Log(resp.short_url);

                        if (resp != null && !string.IsNullOrEmpty(resp.sessionId))
                        {
                            currentSessionId = resp.sessionId;
                            if (!sessionPollingStarted)
                            {
                                sessionPollingStarted = true;
                                StartCoroutine(PollSessionStep(currentSessionId));
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[QRCODE] Erro ao parsear JSON na tentativa {attempts}: {ex.Message}");
                        resp = null;
                    }

                    if (resp != null && !string.IsNullOrEmpty(resp.qr_png))
                    {
                        using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(resp.qr_png))
                        {
                            yield return texReq.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                            if (texReq.result != UnityWebRequest.Result.Success)
#else
                            if (texReq.isNetworkError || texReq.isHttpError)
#endif
                            {
                                Debug.LogWarning($"[QRCODE] Falha ao baixar PNG na tentativa {attempts}: {texReq.error}");
                            }
                            else
                            {
                                Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                                if (qrCodeImage != null)
                                {
                                    qrCodeImage.texture = tex;
                                    success = true;
                                    Debug.Log("[QRCODE] QR aplicado com sucesso.");
                                }
                                else
                                {
                                    Debug.LogWarning("[QRCODE] qrCodeImage não está atribuído no Inspector.");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[QRCODE] Resposta inválida: campo 'qr_png' ausente ou vazio.");
                    }
                }
            }

            if (!success && attempts < Mathf.Max(1, maxQrRetries))
            {
                yield return new WaitForSeconds(Mathf.Max(0f, retryDelaySeconds));
            }
        }

        if (!success)
        {
            Debug.LogError("[QRCODE] Todas as tentativas de obter/aplicar o QR falharam.");
        }
    }

    [System.Serializable]
    private class SessionResponse
    {
        public string sessionId;
        public string step;
    }

    private IEnumerator PollSessionStep(string sessionId)
    {
        if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(sessionId))
        {
            yield break;
        }

        string endpoint = (serverUrl ?? string.Empty).TrimEnd('/') + "/totem/session/" + sessionId;
        int attempts = 0;

        while (attempts < Mathf.Max(1, sessionPollMaxAttempts))
        {
            attempts++;

            using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Accept", "application/json");

                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    Debug.LogWarning($"[QRCODE] Falha no polling (tentativa {attempts}): {req.error}");
                }
                else
                {
                    var json = req.downloadHandler.text;
                    SessionResponse resp = null;
                    try
                    {
                        resp = JsonUtility.FromJson<SessionResponse>(json);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[QRCODE] Erro ao parsear sessão na tentativa {attempts}: {ex.Message}");
                    }

                    var step = resp != null ? resp.step : null;
                    if (!string.IsNullOrEmpty(step))
                    {
                        if (step.Equals("continue", System.StringComparison.OrdinalIgnoreCase))
                        {
                            UIManager.Instance.OpenScreen("INSTRUCOES");
                            yield break;
                        }
                    }
                }
            }

            if (attempts < Mathf.Max(1, sessionPollMaxAttempts))
            {
                yield return new WaitForSeconds(Mathf.Max(0f, sessionPollDelaySeconds));
            }
        }

        Debug.LogWarning($"[QRCODE] Polling de sessão esgotado sem 'continue' (sessionId={sessionId}).");
    }

    void SaveLog(string message, string additional="")
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
                try
                {
                    LogUtilSdk.SaveLogToJson(dataLog);
                }
                    catch (System.Exception ex)
                {
                    Debug.LogWarning($"Falha ao salvar log: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("Erro ao carregar o DataLog do JSON.");
            }
        });
        SceneManager.LoadScene("SampleScene");
    }
}
