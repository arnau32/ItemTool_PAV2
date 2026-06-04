using System;
using UnityEditor;
using UnityEngine;

public sealed class EditorWindowPreviewController
{

    #region Properties

    // Shared playback state
    public bool IsPreviewing { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool LoopPlay { get; set; } = true;
    public float PlayMultiplier { get; set; } = 1f;
    public float Scrubber01 { get; private set; } = 0f;

    #endregion

    #region Fields

    private double _lastEditorTime;
    
    private GUIStyle _centerBigButton;
    
    #endregion

    #region Unity Callbacks

    public void OnEnable()
    {
        IsPreviewing = false;
        IsPlaying = false;
        Scrubber01 = 0f;
        _lastEditorTime = EditorApplication.timeSinceStartup;

        _centerBigButton = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 42f,
            fontStyle = FontStyle.Bold
        };
    }

    public void OnDisable()
    {
        StopPlaying();
        StopPreview();
    }

    #endregion

    #region Public API

    public void SetScrubber(float v) => Scrubber01 = Mathf.Clamp01(v);

    public void DrawPreviewControls(AnimationClip clip, Func<GameObject> getPlayer, Action<float> sampleAt01,
        Action onStopAll)
    {
        var playerGO = getPlayer?.Invoke();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            GUI.enabled = playerGO != null;
            if (GUILayout.Button(IsPreviewing ? "STOP PREVIEW" : "PREVIEW", _centerBigButton, GUILayout.Width(240)))
            {
                if (IsPreviewing)
                {
                    onStopAll?.Invoke();
                    StopPlaying();
                    StopPreview();
                }
                else
                {
                    EnsurePreviewActive();
                    sampleAt01?.Invoke(Scrubber01);
                }
            }

            GUI.enabled = true;

            GUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = playerGO != null;
                    string playLabel = IsPlaying ? "PAUSE" : "PLAY";
                    if (GUILayout.Button(playLabel, GUILayout.Height(24)))
                    {
                        if (IsPlaying) StopPlaying();
                        else StartPlaying();
                    }

                    GUI.enabled = true;

                    if (GUILayout.Button("STOP", GUILayout.Height(24)))
                    {
                        StopPlaying();
                        SetScrubber(0f);
                        EnsurePreviewActive();
                        sampleAt01?.Invoke(Scrubber01);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    LoopPlay = EditorGUILayout.ToggleLeft("Loop", LoopPlay, GUILayout.Width(70));
                    PlayMultiplier = EditorGUILayout.Slider(PlayMultiplier, 0.25f, 3f);
                }
            }

            GUILayout.FlexibleSpace();
        }

        if (playerGO == null)
            EditorGUILayout.HelpBox("Player couldn't be found (tag 'Player') to pre-visualize the animation.",
                MessageType.Warning);
    }

    public void Tick(AnimationClip clip, Func<GameObject> getPlayer, Func<float, float> speedMultiplierAt01,
        Action<float> sampleAt01, Action repaint)
    {
        if (!IsPlaying) return;
        if (clip == null) return;

        var playerGO = getPlayer?.Invoke();
        if (playerGO == null) return;

        double now = EditorApplication.timeSinceStartup;
        double dt = now - _lastEditorTime;
        _lastEditorTime = now;

        float delta = (float)Math.Max(0.0, dt);
        if (delta <= 0f) return;

        float speedMul = Mathf.Clamp(speedMultiplierAt01?.Invoke(Scrubber01) ?? 1f, 0.01f, 20f);
        float clipLen = Mathf.Max(0.0001f, clip.length);

        float dn = (delta / clipLen) * speedMul * Mathf.Max(0.01f, PlayMultiplier);
        Scrubber01 += dn;

        if (Scrubber01 >= 1f)
        {
            if (LoopPlay) Scrubber01 = Scrubber01 - Mathf.Floor(Scrubber01);
            else
            {
                Scrubber01 = 1f;
                StopPlaying();
            }
        }

        EnsurePreviewActive();
        sampleAt01?.Invoke(Scrubber01);
        repaint?.Invoke();
    }

    public void StartPlaying()
    {
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EnsurePreviewActive();
        IsPlaying = true;
    }

    public void StopPlaying() => IsPlaying = false;

    public void EnsurePreviewActive()
    {
        if (!IsPreviewing)
        {
            StartPreview();
        }
    }

    private void StartPreview()
    {
        IsPreviewing = true;
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }
    }

    private void StopPreview()
    {
        IsPreviewing = false;
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }
    }

    #endregion
}