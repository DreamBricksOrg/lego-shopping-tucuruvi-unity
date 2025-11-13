using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class MANUTENCAO : MonoBehaviour
{
    private MANUTENCAO Instance;

    [Header("Tela de manutenção")]
    [Tooltip("GameObject que contém a imagem da tela de manutenção.")]
    [SerializeField] private GameObject maintenanceScreen;
    [Tooltip("Tecla para ativar a tela de manutenção.")]
    [SerializeField] private KeyCode triggerKey = KeyCode.M;

    public string ctaScreen;

    [Header("Monitoramento")]
    [Tooltip("URL do servidor para verificar disponibilidade.")]
    [SerializeField] private string serverUrl = "https://dbcomfyui5090.ngrok.app";
    [Tooltip("Intervalo (segundos) entre verificações de status do servidor.")]
    [Min(0.5f)] public float pollIntervalSeconds = 5f;
    [Tooltip("Timeout (segundos) para cada requisição de verificação.")]
    [Range(1, 60)] public int requestTimeoutSeconds = 5;

    private Coroutine pollingRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        StartMonitoring();
    }

    private void OnDisable()
    {
        StopMonitoring();
    }

    void Update()
    {
        EnableMaintenanceScreen();
    }

    void EnableMaintenanceScreen()
    {
         if (Input.GetKeyDown(triggerKey))
        {
            if (maintenanceScreen == null)
            {
                Debug.LogWarning("[MANUTENCAO] 'maintenanceScreen' não atribuído no Inspector.");
                return;
            }

            bool isActive = maintenanceScreen.activeSelf;
            var cg = maintenanceScreen.GetComponent<CanvasGroup>();

            if (isActive)
            {
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
                maintenanceScreen.SetActive(false);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.OpenScreen(ctaScreen);
                }
                else
                {
                    Debug.LogWarning("[MANUTENCAO] UIManager.Instance não disponível para abrir CTA.");
                }
            }
            else
            {
                // Ativa manutenção e desabilita todas as telas
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.DisableAllScreens();
                }

                maintenanceScreen.SetActive(true);
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
        }
    }

    // Inicia monitoramento contínuo do servidor
    public void StartMonitoring()
    {
        if (pollingRoutine == null)
        {
            pollingRoutine = StartCoroutine(PollingLoop());
            Debug.Log($"[MANUTENCAO] Monitoramento iniciado. Intervalo={pollIntervalSeconds}s, Timeout={requestTimeoutSeconds}s");
        }
    }

    // Para monitoramento contínuo
    public void StopMonitoring()
    {
        if (pollingRoutine != null)
        {
            StopCoroutine(pollingRoutine);
            pollingRoutine = null;
            Debug.Log("[MANUTENCAO] Monitoramento parado.");
        }
    }

    private IEnumerator PollingLoop()
    {
        while (enabled)
        {
            yield return CheckServerStatusOnce();
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private IEnumerator CheckServerStatusOnce()
    {
        using (var uwr = UnityWebRequest.Get(serverUrl))
        {
            // Ainda setamos timeout do UWR (nem sempre respeitado). Implementamos timeout manual abaixo.
            uwr.timeout = requestTimeoutSeconds;
            Debug.Log($"[MANUTENCAO] Verificando servidor: {serverUrl}");

            var op = uwr.SendWebRequest();
            float start = Time.time;
            while (!op.isDone)
            {
                if (Time.time - start > requestTimeoutSeconds)
                {
                    Debug.LogWarning($"[MANUTENCAO] Timeout atingido ({requestTimeoutSeconds}s) ao verificar servidor. Abortando requisição.");
                    uwr.Abort();
                    break;
                }
                yield return null;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                // Tratar falhas (inclui timeout, DNS, conexão recusada, 5xx sob certas condições)
                Debug.LogWarning($"[MANUTENCAO] Falha ao verificar servidor: result={uwr.result}, erro='{uwr.error}', code={uwr.responseCode}. Entrar em manutenção se CTA ativa.");
                if (IsCTAActive())
                {
                    ActivateMaintenance();
                }
                else
                {
                    Debug.Log("[MANUTENCAO] Falha de verificação. Já em manutenção, mantendo estado.");
                }
                yield break;
            }

            var code = uwr.responseCode;
            Debug.Log($"[MANUTENCAO] Resposta do servidor: HTTP {code}");

            if (code == 200)
            {
                if (IsMaintenanceActive())
                {
                    Debug.Log("[MANUTENCAO] Servidor OK (200). Saindo de manutenção e voltando para CTA.");
                    ActivateCTA();
                }
                else
                {
                    Debug.Log("[MANUTENCAO] Servidor OK (200). CTA já ativa, nenhuma ação.");
                }
            }
            else if (code == 400)
            {
                if (IsCTAActive())
                {
                    Debug.Log("[MANUTENCAO] Servidor retornou 400. Entrando em manutenção e desativando CTA.");
                    ActivateMaintenance();
                }
                else
                {
                    Debug.Log("[MANUTENCAO] Servidor 400. Já em manutenção, mantendo estado.");
                }
            }
            else
            {
                Debug.LogWarning($"[MANUTENCAO] HTTP {code} não tratado explicitamente. Mantendo estado.");
            }
        }
    }

    private bool IsMaintenanceActive()
    {
        return maintenanceScreen != null && maintenanceScreen.activeSelf;
    }

    private bool IsCTAActive()
    {
        var ui = UIManager.Instance;
        if (ui == null) return false;
        if (ui.currentScreen != null)
        {
            return ui.currentScreen.name == ctaScreen;
        }
        var ctaGo = GetCtaGO();
        return ctaGo != null && ctaGo.activeSelf;
    }

    private GameObject GetCtaGO()
    {
        var ui = UIManager.Instance;
        if (ui != null && ui.screenDictionary != null)
        {
            if (ui.screenDictionary.TryGetValue(ctaScreen, out var go))
            {
                return go;
            }
        }
        return null;
    }

    private void ActivateMaintenance()
    {
        var ui = UIManager.Instance;
        if (ui != null)
        {
            ui.DisableAllScreens();
        }

        if (maintenanceScreen != null)
        {
            maintenanceScreen.SetActive(true);
            var cg = maintenanceScreen.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
        else
        {
            Debug.LogWarning("[MANUTENCAO] 'maintenanceScreen' não atribuído ao ativar manutenção.");
        }
    }

    private void ActivateCTA()
    {
        if (maintenanceScreen != null)
        {
            var cg = maintenanceScreen.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
            maintenanceScreen.SetActive(false);
        }

        var ui = UIManager.Instance;
        if (ui != null)
        {
            ui.OpenScreen(ctaScreen);
        }
        else
        {
            Debug.LogWarning("[MANUTENCAO] UIManager.Instance não disponível para abrir CTA.");
        }
    }
}
