using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

public class RuntimeDebugConsole : MonoBehaviour
{
    [Header("Atalhos")]
    [Tooltip("Tecla para abrir/fechar o console")] public KeyCode toggleKey = KeyCode.C;

    [Header("Aparência")]
    [Tooltip("Altura da janela em pixels")] public int windowHeight = 400;
    [Tooltip("Margem da janela")] public int margin = 12;
    [Tooltip("Tamanho da fonte")] public int fontSize = 14;
    [Tooltip("Limite máximo de logs mantidos em memória")] public int maxLogs = 1000;

    [Header("Opções")]
    [Tooltip("Exibir stack trace junto com a mensagem")] public bool showStackTrace = false;

    private bool visible;
    private Vector2 scrollPos;
    private Rect windowRect;

    private readonly List<LogEntry> logs = new List<LogEntry>();
    private readonly ConcurrentQueue<LogEntry> pending = new ConcurrentQueue<LogEntry>();

    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _buttonStyle;

    private struct LogEntry
    {
        public DateTime time;
        public LogType type;
        public string message;
        public string stackTrace;
    }

    private void Awake()
    {
        int width = Screen.width;
        windowRect = new Rect(margin, margin, Mathf.Max(320, width - margin * 2), windowHeight);
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        Application.logMessageReceivedThreaded += HandleLogThreaded;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
    }

    private void Update()
    {
        // Alterna visibilidade
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
        }

        // Drena logs vindos de threads
        while (pending.TryDequeue(out var entry))
        {
            logs.Add(entry);
            // mantém tamanho sob controle
            if (logs.Count > maxLogs)
            {
                int overflow = logs.Count - maxLogs;
                logs.RemoveRange(0, overflow);
            }
        }
    }

    private void OnGUI()
    {
        if (!visible) return;

        EnsureStyles();

        // Cabeçalho e ações
        GUILayout.BeginArea(windowRect, GUI.skin.box);
        GUILayout.BeginVertical();

        GUILayout.Label($"Console (Logs: {logs.Count}) — {DateTime.Now:HH:mm:ss}", _headerStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fechar", _buttonStyle)) visible = false;
        if (GUILayout.Button("Limpar", _buttonStyle)) logs.Clear();
        if (GUILayout.Button("Copiar", _buttonStyle)) GUIUtility.systemCopyBuffer = BuildExportText();
        showStackTrace = GUILayout.Toggle(showStackTrace, "StackTrace");
        GUILayout.EndHorizontal();

        // Área de scroll com logs
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        foreach (var entry in logs)
        {
            var color = ColorForType(entry.type);
            var prevColor = GUI.color;
            GUI.color = color;

            string prefix = entry.type switch
            {
                LogType.Error => "[ERROR]",
                LogType.Warning => "[WARN]",
                LogType.Assert => "[ASSERT]",
                LogType.Exception => "[EXCEPT]",
                _ => "[LOG]"
            };

            string line = $"{entry.time:HH:mm:ss} {prefix} {entry.message}";
            GUILayout.Label(line, _labelStyle);
            if (showStackTrace && !string.IsNullOrWhiteSpace(entry.stackTrace))
            {
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                GUILayout.Label(entry.stackTrace, _labelStyle);
            }

            GUI.color = prevColor;
        }
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        // Logs no main thread
        EnqueueLog(condition, stackTrace, type);
    }

    private void HandleLogThreaded(string condition, string stackTrace, LogType type)
    {
        // Logs thread-safe
        EnqueueLog(condition, stackTrace, type);
    }

    private void EnqueueLog(string message, string stack, LogType type)
    {
        pending.Enqueue(new LogEntry
        {
            time = DateTime.Now,
            type = type,
            message = message,
            stackTrace = stack
        });
    }

    private void EnsureStyles()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = fontSize
            };
        }
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold
            };
        }
        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize
            };
        }
    }

    private static Color ColorForType(LogType type)
    {
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
                return new Color(1f, 0.5f, 0.5f); // vermelho claro
            case LogType.Warning:
                return new Color(1f, 0.85f, 0.4f); // amarelo
            case LogType.Assert:
                return new Color(0.85f, 0.7f, 1f); // lilás
            default:
                return Color.white;
        }
    }

    private string BuildExportText()
    {
        var sb = new System.Text.StringBuilder(logs.Count * 64);
        foreach (var entry in logs)
        {
            sb.Append(entry.time.ToString("yyyy-MM-dd HH:mm:ss"))
              .Append(' ')
              .Append(entry.type.ToString().ToUpper())
              .Append(' ')
              .AppendLine(entry.message);
            if (showStackTrace && !string.IsNullOrWhiteSpace(entry.stackTrace))
            {
                sb.AppendLine(entry.stackTrace);
            }
        }
        return sb.ToString();
    }
}