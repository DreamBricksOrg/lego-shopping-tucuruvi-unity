using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
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
                SceneManager.LoadScene("SampleScene");
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

}
