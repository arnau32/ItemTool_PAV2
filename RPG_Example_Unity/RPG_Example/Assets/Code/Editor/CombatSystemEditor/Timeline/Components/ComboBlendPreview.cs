using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Handles blend preview for combo transitions in AttackWindowEditor.
/// Creates a hidden clone of the player and blends transforms between current and next attack clips.
internal sealed class ComboBlendPreview
{
    #region Fields

    private AttackData _nextAttackPreview;
    private GameObject _nextPreviewGO;
    private int _nextPreviewPlayerInstanceId = -1;
    private List<(Transform a, Transform b)> _blendPairs;

    private const float TIME_MARKER_HIT_W = 14f;

    
    #endregion

    #region Properties

    public AttackData NextAttack
    {
        get => _nextAttackPreview;
        set
        {
            if (_nextAttackPreview != value)
            {
                _nextAttackPreview = value;
                ResetCache();
            }
        }
    }

    #endregion

    #region Public API

    /// Draws the combo transition controls UI.
    public void DrawControls(SerializedProperty timeToChangeProp, Action onTimeChanged)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Combo Transition", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float t = EditorGUILayout.Slider("Time To Change Anim", Mathf.Clamp01(timeToChangeProp.floatValue), 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            timeToChangeProp.floatValue = Mathf.Clamp01(t);
            onTimeChanged?.Invoke();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            _nextAttackPreview = (AttackData)EditorGUILayout.ObjectField("Next Attack (Preview)", _nextAttackPreview, typeof(AttackData), false);
            if (EditorGUI.EndChangeCheck())
            {
                ResetCache();
            }

            GUI.enabled = _nextAttackPreview != null;
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _nextAttackPreview = null;
                ResetCache();
            }
            GUI.enabled = true;
        }
    }

    /// Updates the blend preview
    public void UpdatePreview(GameObject player, AnimationClip currentClip, float currentTime01, float timeToChangeAnim01)
    {
        if (player == null || currentClip == null) return;
        if (_nextAttackPreview == null || _nextAttackPreview.animationClip == null) return;

        float changeSec = Mathf.Clamp01(timeToChangeAnim01) * currentClip.length;
        float tSec = Mathf.Clamp01(currentTime01) * currentClip.length;

        if (tSec >= changeSec)
        {
            EnsureCloneExists(player);

            float crossFadeSec = Mathf.Max(0f, _nextAttackPreview.crossFade);
            float tNext = Mathf.Clamp(tSec - changeSec, 0f, Mathf.Max(0.0001f, _nextAttackPreview.animationClip.length));

            AnimationMode.SampleAnimationClip(_nextPreviewGO, _nextAttackPreview.animationClip, tNext);

            if (crossFadeSec <= 0f)
            {
                BlendFromClone(1f);
            }
            else
            {
                float alpha = Mathf.Clamp01((tSec - changeSec) / crossFadeSec);
                BlendFromClone(alpha);
            }
        }
    }

    /// Draws the timeToChangeAnim marker and blend zone on the timeline.
    public void DrawMarkerAndBlendZone(Rect timelineRect, AnimationClip currentClip, float timeToChangeAnim01)
    {
        if (currentClip == null) return;

        float t01 = Mathf.Clamp01(timeToChangeAnim01);
        float x = Mathf.Lerp(timelineRect.x, timelineRect.xMax, t01);

        // Visual blend zone
        if (_nextAttackPreview != null && _nextAttackPreview.animationClip != null)
        {
            float crossFadeSec = Mathf.Max(0f, _nextAttackPreview.crossFade);
            if (crossFadeSec > 0f)
            {
                float n = crossFadeSec / Mathf.Max(0.0001f, currentClip.length);
                float x2 = Mathf.Lerp(timelineRect.x, timelineRect.xMax, Mathf.Clamp01(t01 + n));
                var fadeRect = new Rect(x, timelineRect.y, Mathf.Max(0f, x2 - x), timelineRect.height);
                EditorGUI.DrawRect(fadeRect, new Color(0.2f, 0.9f, 0.9f, 0.08f));
            }
        }

        // Marker line
        EditorGUI.DrawRect(new Rect(x - 1f, timelineRect.y, 2f, timelineRect.height), new Color(0.2f, 0.9f, 0.9f, 0.9f));

        // Handle
        Rect handleRect = new Rect(x - (TIME_MARKER_HIT_W * 0.5f), timelineRect.y + 2f, TIME_MARKER_HIT_W, 12f);
        EditorGUI.DrawRect(handleRect, new Color(0.2f, 0.9f, 0.9f, 0.95f));

        // Label
        var labelRect = new Rect(x + 8f, timelineRect.y + 1f, 160f, 16f);
        GUI.Label(labelRect, $"TChange {t01:0.00}", EditorStyles.whiteMiniLabel);
    }

    /// Returns the marker hit rect for mouse interaction.
    public Rect GetMarkerHitRect(Rect timelineRect, float timeToChangeAnim01)
    {
        float t01 = Mathf.Clamp01(timeToChangeAnim01);
        float x = Mathf.Lerp(timelineRect.x, timelineRect.xMax, t01);
        return new Rect(x - (TIME_MARKER_HIT_W * 0.5f), timelineRect.y + 2f, TIME_MARKER_HIT_W, 12f);
    }

    /// Cleans up the preview clone. Call this in OnDisable().
    public void Cleanup()
    {
        ResetCache();
    }

    #endregion

    #region Internal Implementation

    private void ResetCache()
    {
        _blendPairs = null;

        if (_nextPreviewGO != null)
        {
            UnityEngine.Object.DestroyImmediate(_nextPreviewGO);
            _nextPreviewGO = null;
        }

        _nextPreviewPlayerInstanceId = -1;
    }

    private void EnsureCloneExists(GameObject player)
    {
        if (player == null) return;

        int id = player.GetInstanceID();
        if (_nextPreviewGO != null && _nextPreviewPlayerInstanceId == id) return;

        // Rebuild clone
        if (_nextPreviewGO != null)
        {
            UnityEngine.Object.DestroyImmediate(_nextPreviewGO);
            _nextPreviewGO = null;
        }

        _nextPreviewPlayerInstanceId = id;

        _nextPreviewGO = UnityEngine.Object.Instantiate(player);
        _nextPreviewGO.name = $"{player.name}__NEXT_CLIP_PREVIEW";
        _nextPreviewGO.hideFlags = HideFlags.HideAndDontSave;

        // Disable renderers and behaviours to avoid side effects in edit mode.
        var renderers = _nextPreviewGO.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = false;

        var behaviours = _nextPreviewGO.GetComponentsInChildren<Behaviour>(true);
        foreach (var b in behaviours) b.enabled = false;

        _blendPairs = BuildBlendPairsRelative(player.transform, _nextPreviewGO.transform);
    }

    private List<(Transform a, Transform b)> BuildBlendPairsRelative(Transform rootA, Transform rootB)
    {
        var mapB = new Dictionary<string, Transform>(512);
        BuildRelativePathMap(rootB, "", mapB);

        var pairs = new List<(Transform a, Transform b)>(512);
        BuildPairsFromA(rootA, "", mapB, pairs);

        return pairs;
    }

    private void BuildRelativePathMap(Transform t, string prefix, Dictionary<string, Transform> map)
    {
        // Skip root name to allow different clone root names
        string currentPrefix = prefix;

        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            string path = string.IsNullOrEmpty(currentPrefix) ? c.name : $"{currentPrefix}/{c.name}";
            map[path] = c;
            BuildRelativePathMap(c, path, map);
        }
    }

    private void BuildPairsFromA(Transform tA, string prefix, Dictionary<string, Transform> mapB, List<(Transform a, Transform b)> pairs)
    {
        string currentPrefix = prefix;

        for (int i = 0; i < tA.childCount; i++)
        {
            var cA = tA.GetChild(i);
            string path = string.IsNullOrEmpty(currentPrefix) ? cA.name : $"{currentPrefix}/{cA.name}";

            if (mapB.TryGetValue(path, out var cB))
                pairs.Add((cA, cB));

            BuildPairsFromA(cA, path, mapB, pairs);
        }
    }

    private void BlendFromClone(float alpha)
    {
        if (_blendPairs == null || _blendPairs.Count == 0) return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < _blendPairs.Count; i++)
        {
            var (a, b) = _blendPairs[i];
            if (a == null || b == null) continue;

            a.localPosition = Vector3.Lerp(a.localPosition, b.localPosition, alpha);
            a.localRotation = Quaternion.Slerp(a.localRotation, b.localRotation, alpha);
            a.localScale = Vector3.Lerp(a.localScale, b.localScale, alpha);
        }
    }

    #endregion
}
