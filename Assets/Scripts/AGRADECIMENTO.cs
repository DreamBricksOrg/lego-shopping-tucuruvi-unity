using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class AGRADECIMENTO : MonoBehaviour
{
    private ConfigManager config;
    public bool timerRunning;
    public float totalTime;
    public float currentTime;
    private string serverUrl;

    // public RawImage qrCodeImage;

    public RawImage resultImage;
    private Vector2 resultImageInitialSize;


    private void Awake()
    {
        config = new();
        totalTime = float.Parse(config.GetValue("Timer", "AGRADECIMENTO"));
        serverUrl = config.GetValue("Net", "serverUrl");
    }

    private void OnEnable()
    {
        currentTime = totalTime;
        timerRunning = true;

        if (resultImage != null)
        {
            // Guarda o tamanho configurado no Editor para reaplicar após carregar a textura
            resultImageInitialSize = resultImage.rectTransform.sizeDelta;
        }

        string imageUrl = PlayerPrefs.GetString("image_url", string.Empty);
        Debug.Log($"[AGRADECIMENTO] imageUrl: {imageUrl}");
        if (!string.IsNullOrEmpty(imageUrl) && resultImage != null)
        {
            StartCoroutine(LoadImageFromUrl(imageUrl));
        }

    }

    private void OnDisable()
    {
        PlayerPrefs.DeleteKey("image_url");
        PlayerPrefs.Save();

        if (resultImage != null)
        {
            resultImage.texture = null;
        }
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

            SceneManager.LoadScene("SampleScene");
        }
    }

    private IEnumerator LoadImageFromUrl(string url)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AGRADECIMENTO] Falha ao carregar imagem: {req.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex != null && resultImage != null)
            {
                resultImage.texture = tex;
                // Reaplica o tamanho definido no Editor (não usar SetNativeSize)
                resultImage.rectTransform.sizeDelta = resultImageInitialSize;
            }
        }
    }

    private void OnEspaceKeyDown()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
