using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.EventSystems;

[System.Serializable]
public struct PromptData
{
    public InputIconType iconType;
    public LocalizedString text;
}

[System.Serializable]
public class PromptGroup
{
    public LocalizedString header;
    public List<PromptData> prompts;
}


public class InputPromptList : MonoBehaviour
{
    public SelectedGameObjectSeter SelectedGameObjectSeter;
    public UISelectionKeeper UISelectionKeeper;

    bool IsHaveObjectSelected = false;


    [Header("UI")]
    public InputPromptItem itemPrefab;
    public GameObject headerPrefab;
    public Transform container;


    [Header("Groups")]
    public List<PromptGroup> groups = new();

    private List<GameObject> spawned = new();




    private void Start()
    {
        Build();
    }

    public void Build()
    {
        Clear();

        foreach (var group in groups)
        {
            if (group.header != null)
            {
                var headerGO = Instantiate(headerPrefab, container);
                var text = headerGO.GetComponentInChildren<TMPro.TextMeshProUGUI>();

                group.header.StringChanged += (value) =>
                {
                    text.text = value;
                };

                spawned.Add(headerGO);
            }

            foreach (var data in group.prompts)
            {
                var item = Instantiate(itemPrefab, container);

                data.text.StringChanged += (value) =>
                {
                    item.Setup(data.iconType, value);
                };

                spawned.Add(item.gameObject);

                if (!IsHaveObjectSelected)
                {
                    IsHaveObjectSelected = true;
                    SelectedGameObjectSeter.firstSelectedButton = item.gameObject;
                    UISelectionKeeper.defaultSelected = item.gameObject;
                }
            }
        }
        SetupLoopNavigation();
    }

    public void Clear()
    {
        foreach (var go in spawned)
        {
            Destroy(go);
        }

        spawned.Clear();
    }


    private void SetupLoopNavigation()
    {
        List<UnityEngine.UI.Selectable> selectables = new();

        foreach (var go in spawned)
        {
            var selectable = go.GetComponent<UnityEngine.UI.Selectable>();

            if (selectable != null)
                selectables.Add(selectable);
        }

        if (selectables.Count < 2)
            return;

        var first = selectables[0];
        var last = selectables[^1];

        SetupLoop(first.gameObject, MoveDirection.Up, last);
        SetupLoop(last.gameObject, MoveDirection.Down, first);
    }

    private void SetupLoop(GameObject go, MoveDirection dir, UnityEngine.UI.Selectable target)
    {
        var loop = go.GetComponent<LoopNavigationEdge>();

        if (loop == null)
            loop = go.AddComponent<LoopNavigationEdge>();

        loop.triggerDirection = dir;
        loop.targetSelectable = target;
    }
}