using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CAPTURA : MonoBehaviour
{
    public Text countDownText;
    public RenderTexture webcamRenderTexture; // RenderTexture da webcam (atribuir via Inspector)
    public string validationScreenName = "VALIDACAO"; // Nome da tela de validaçãos
    public int totalSeconds = 3;

    private Coroutine countdownRoutine;

    private void OnEnable()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }
        countdownRoutine = StartCoroutine(CaptureCountdownAndSnap());
    }

    private IEnumerator CaptureCountdownAndSnap()
    {
        for (int s = totalSeconds; s > 0; s--)
        {
            if (countDownText != null)
            {
                countDownText.text = s.ToString();
            }
            yield return new WaitForSeconds(1f);
        }

        // Aguarda o fim do frame para garantir que a RenderTexture foi atualizada pela GPU
        yield return new WaitForEndOfFrame();

        // Captura a RenderTexture da webcam em um Texture2D
        Texture2D snapshot = CaptureFromRenderTexture(webcamRenderTexture);
        if (snapshot == null)
        {
            Debug.LogWarning("[CAPTURA] Falha ao capturar imagem da webcam. RenderTexture não atribuída.");
            yield break;
        }

        // Armazena a imagem para ser mostrada na tela de validação
        VALIDACAO.PendingImage = snapshot;

        // Agora abre a tela de validação; OnEnable da VALIDACAO usará PendingImage
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenScreen(validationScreenName);
        }
        else
        {
            Debug.LogWarning("[CAPTURA] UIManager.Instance está nulo; não foi possível abrir a tela de validação.");
        }
    }

    private Texture2D CaptureFromRenderTexture(RenderTexture rt)
    {
        if (rt == null)
        {
            return null;
        }

        // Blita para uma RenderTexture temporária com sRGB para evitar imagem escura
        RenderTexture temp = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(rt, temp);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = temp;

        Texture2D tex = new Texture2D(temp.width, temp.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
        tex.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(temp);
        return tex;
    }
}
