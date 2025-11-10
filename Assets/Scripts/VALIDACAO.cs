using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class VALIDACAO : MonoBehaviour
{
    public RawImage sendImage;
    public Button acceptButton;
    public Button rejectButton;
    public string workflow = "default";

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

        StartCoroutine(UploadImage(tex));
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

    private IEnumerator UploadImage(Texture texture)
    {
        string baseUrl = null;
        try
        {
            baseUrl = config?.GetValue("Net", "serverUrl");
        }
        catch { /* Ignora erros de config */ }

        string url = string.IsNullOrEmpty(baseUrl) ? "/api/sendimage" : ($"{baseUrl.TrimEnd('/')}/api/sendimage");

        // Garante Texture2D (converte se vier RenderTexture)
        Texture2D tex2D = texture as Texture2D;
        if (tex2D == null && texture is RenderTexture rt)
        {
            tex2D = ConvertRenderTextureToTexture2D(rt);
        }

        if (tex2D == null)
        {
            Debug.LogError("[VALIDACAO] Falha ao preparar imagem para upload.");
            yield break;
        }

        byte[] pngBytes = tex2D.EncodeToPNG();

        WWWForm form = new WWWForm();
        form.AddBinaryData("image", pngBytes, "image.png", "image/png");
        form.AddField("workflow", workflow ?? string.Empty);

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VALIDACAO] Upload falhou: {req.error}");
            }
            else
            {
                Debug.Log("[VALIDACAO] Upload realizado com sucesso.");
            }
        }
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
