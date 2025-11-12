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

    public RawImage qrCodeImage;

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

        if (UDPReceiver.Instance != null)
        {
            string latest = UDPReceiver.Instance.GetLastestData();
            if (!string.IsNullOrEmpty(latest))
            {
                if (latest.ToUpper() == "INSTRUCOES")
                {
                    UIManager.Instance.OpenScreen(latest.ToUpper());
                }
            }
        }
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
    }

    private IEnumerator FetchAndApplyQr()
    {
        string endpoint = (serverUrl ?? string.Empty).TrimEnd('/') + "/totem/qrcode/init";

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
                Debug.LogError($"Falha ao obter QR em '{endpoint}': {req.error}");
                yield break;
            }

            var json = req.downloadHandler.text;
            QrGenResponse resp = null;
            try
            {
                resp = JsonUtility.FromJson<QrGenResponse>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erro ao parsear JSON da resposta: {ex.Message}\nConteúdo: {json}");
                yield break;
            }

            if (resp == null || string.IsNullOrEmpty(resp.qr_png))
            {
                Debug.LogError("Resposta inválida: campo 'qr_png' ausente ou vazio.");
                yield break;
            }

            using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(resp.qr_png))
            {
                yield return texReq.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (texReq.result != UnityWebRequest.Result.Success)
#else
                if (texReq.isNetworkError || texReq.isHttpError)
#endif
                {
                    Debug.LogError($"Falha ao baixar PNG do QR: {texReq.error}");
                    yield break;
                }

                Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                if (qrCodeImage != null)
                {
                    qrCodeImage.texture = tex;
                    // Se quiser ajustar o tamanho do RawImage à textura:
                    // qrCodeImage.SetNativeSize();
                }
                else
                {
                    Debug.LogWarning("qrCodeImage não está atribuído no Inspector.");
                }
            }
        }
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
               LogUtilSdk.SaveLogToJson(dataLog);
           }
           else
           {
               Debug.LogError("Erro ao carregar o DataLog do JSON.");
           }
               SceneManager.LoadScene("SampleScene");

       });
    }
}
