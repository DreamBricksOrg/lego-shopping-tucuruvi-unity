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

    private void Awake()
    {
        config = new();
        totalTime = float.Parse(config.GetValue("Timer", "INSTRUCOES"));
    }
    private void OnEnable()
    {
        currentTime = totalTime;
        timerRunning = true;
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
    }

}
