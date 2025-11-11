using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class VALIDACAO : MonoBehaviour
{
    public RawImage sendImage;
    public Button acceptButton;
    public Button rejectButton;
    public GameObject spinner;
    public float pollIntervalSeconds = 2f;
    public static Texture2D PendingImage;

    private ConfigManager config;

    private void Awake()
    {
        config = new ConfigManager();
    }

    private void OnEnable()
    {
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

        StartCoroutine(UploadAndPoll(tex));
    }

    private void OnReject()
    {
        // Limpa a imagem pendente e volta para a tela de CAPTURA
        PendingImage = null;
        if (sendImage != null) sendImage.texture = null;
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
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = config?.GetValue("Net", "API_URL");
        }
        catch { }

        string uploadUrl = $"{baseUrl.TrimEnd('/')}/upload";

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
        string statusUrl = $"{baseUrl.TrimEnd('/')}/result?request_id={UnityWebRequest.EscapeURL(requestId)}";
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
                        ResetUIAfterProcess();
                        yield break;
                    case "done":
                        if (!string.IsNullOrEmpty(js.image_url))
                        {
                            PlayerPrefs.SetString("image_url", js.image_url);
                            PlayerPrefs.Save();
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
}
