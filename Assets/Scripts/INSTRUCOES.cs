using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;


public class INSTRUCOES : MonoBehaviour
{
     private ConfigManager config;
    public bool timerRunning;
    public float totalTime;
    public float currentTime;

    public RawImage circleButton;
    public float pulseSpeed = 2f;
    public float minAlpha = 0.4f;
    public float maxAlpha = 1f;
    private Color circleInitialColor;

    private void Awake()
    {
        config = new();
        totalTime = float.Parse(config.GetValue("Timer", "INSTRUCOES"));
    }
    private void OnEnable()
    {
        currentTime = totalTime;
        timerRunning = true;

        if (circleButton != null)
        {
            circleInitialColor = circleButton.color;
        }
    }

    private void Update()
    {
        if (timerRunning)
        {
            currentTime -= Time.deltaTime;
            if (currentTime <= 0)
            {
                currentTime = 0;
                timerRunning = false;
                SaveLog("TOTEM_INSTRUCOES_TEMPO_ESGOTADO");
            }
        }

        if (circleButton != null)
        {
            float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            float a = Mathf.Lerp(minAlpha, maxAlpha, t);
            var c = circleButton.color;
            c.a = a;
            circleButton.color = c;
        }
    }

    private void OnDisable()
    {
        if (circleButton != null)
        {
            circleButton.color = circleInitialColor;
        }
    }

    void SaveLog(string message, string additional="")
    {
        StartCoroutine(SaveLogCoroutine(message, additional));
        StartCoroutine(SaveLogInNewLogCenterCoroutine(message, "INFO", new List<string> { "totem" }, additional));
    }

     IEnumerator SaveLogCoroutine(string message,string additional)
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
