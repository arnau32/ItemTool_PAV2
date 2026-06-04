using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class GlobalLogConsoleWindow : EditorWindow
{
    [Serializable]
    private class LogEntry
    {
        public string message;
        public string stackTrace;
        public LogType type;
        public DateTime time;
        public bool selected;
    }

    private readonly List<LogEntry> _logs = new();

    private Vector2 _scroll;
    private string _search = "";

    private bool _pause;
    private bool _showLogs = true;
    private bool _showWarnings = true;
    private bool _showErrors = true;
    private bool _includeStackTrace = false;
    private bool _autoScroll = true;

    [MenuItem("Tools/Debug/Global Log Console")]
    public static void Open()
    {
        GetWindow<GlobalLogConsoleWindow>("Global Log Console");
    }

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogReceived;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogReceived;
    }

    private void OnLogReceived(string condition, string stackTrace, LogType type)
    {
        if (_pause) return;

        _logs.Add(new LogEntry
        {
            message = condition,
            stackTrace = stackTrace,
            type = type,
            time = DateTime.Now,
            selected = false
        });

        if (_autoScroll)
            _scroll.y = float.MaxValue;

        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawOptions();
        DrawLogs();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Copy All", EditorStyles.toolbarButton, GUILayout.Width(75)))
            CopyAllVisible();

        if (GUILayout.Button("Copy Selected", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CopySelected();

        if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(75)))
            SetVisibleSelection(true);

        if (GUILayout.Button("Deselect All", EditorStyles.toolbarButton, GUILayout.Width(85)))
            SetVisibleSelection(false);

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            _logs.Clear();

        _pause = GUILayout.Toggle(_pause, "Pause", EditorStyles.toolbarButton, GUILayout.Width(60));

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOptions()
    {
        EditorGUILayout.BeginHorizontal();

        _showLogs = GUILayout.Toggle(_showLogs, "Logs", GUILayout.Width(60));
        _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", GUILayout.Width(85));
        _showErrors = GUILayout.Toggle(_showErrors, "Errors", GUILayout.Width(70));

        GUILayout.Space(10);

        _includeStackTrace = GUILayout.Toggle(_includeStackTrace, "Include StackTrace", GUILayout.Width(140));
        _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", GUILayout.Width(95));

        GUILayout.Space(10);

        GUILayout.Label("Search:", GUILayout.Width(50));
        _search = EditorGUILayout.TextField(_search);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLogs()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (LogEntry log in _logs)
        {
            if (!IsVisible(log))
                continue;

            EditorGUILayout.BeginHorizontal();

            log.selected = EditorGUILayout.Toggle(log.selected, GUILayout.Width(20));

            GUIStyle style = GetStyle(log.type);

            string prefix = $"[{log.time:HH:mm:ss}] [{log.type}] ";
            EditorGUILayout.SelectableLabel(prefix + log.message, style, GUILayout.Height(22));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private bool IsVisible(LogEntry log)
    {
        if (log.type == LogType.Log && !_showLogs)
            return false;

        if (log.type == LogType.Warning && !_showWarnings)
            return false;

        if ((log.type == LogType.Error ||
             log.type == LogType.Exception ||
             log.type == LogType.Assert) && !_showErrors)
            return false;

        if (!string.IsNullOrWhiteSpace(_search) &&
            !log.message.ToLower().Contains(_search.ToLower()))
            return false;

        return true;
    }

    private GUIStyle GetStyle(LogType type)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = false
        };

        switch (type)
        {
            case LogType.Warning:
                style.normal.textColor = new Color(1f, 0.75f, 0.25f);
                break;

            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                style.normal.textColor = new Color(1f, 0.35f, 0.35f);
                break;

            default:
                style.normal.textColor = Color.white;
                break;
        }

        return style;
    }

    private void CopyAllVisible()
    {
        StringBuilder sb = new();

        foreach (LogEntry log in _logs)
        {
            if (!IsVisible(log))
                continue;

            AppendLog(sb, log);
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("[Global Log Console] Visible logs copied to clipboard.");
    }

    private void CopySelected()
    {
        StringBuilder sb = new();

        foreach (LogEntry log in _logs)
        {
            if (!log.selected)
                continue;

            AppendLog(sb, log);
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("[Global Log Console] Selected logs copied to clipboard.");
    }

    private void SetVisibleSelection(bool selected)
    {
        foreach (LogEntry log in _logs)
        {
            if (IsVisible(log))
                log.selected = selected;
        }
    }

    private void AppendLog(StringBuilder sb, LogEntry log)
    {
        sb.AppendLine($"[{log.time:HH:mm:ss}] [{log.type}] {log.message}");

        if (_includeStackTrace && !string.IsNullOrWhiteSpace(log.stackTrace))
        {
            sb.AppendLine(log.stackTrace);
        }

        sb.AppendLine();
    }
}