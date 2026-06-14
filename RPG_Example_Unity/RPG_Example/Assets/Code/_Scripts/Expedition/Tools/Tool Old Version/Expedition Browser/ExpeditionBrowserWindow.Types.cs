#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ExpeditionBrowserWindow
{
    internal enum SceneEntityKind
    {
        Enemy,
        EnemyPoint,
        LootPoint
    }

    internal enum ExpeditionListTab
    {
        Enemies,
        LootPoints,
        Map
    }

    internal enum ValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    [Flags]
    internal enum RefreshFlags
    {
        None = 0,
        Scan = 1 << 0,
        Filter = 1 << 1,
        Validations = 1 << 2,
        List = 1 << 3,
        Details = 1 << 4,

        Soft = Filter | Validations | List | Details,
        Hard = Scan | Filter | Validations | List | Details
    }

    internal sealed class ValidationMessage
    {
        public ValidationSeverity Severity;
        public string Message;
    }

    internal sealed class SceneEntityRecord
    {
        public GameObject GameObject;
        public Component Component;
        public SceneEntityKind Kind;
        public string Name;
        public string HierarchyPath;
    }

    internal sealed class ListRowRefs
    {
        public UnityEngine.UIElements.Label Name;
        public UnityEngine.UIElements.Label Indicator;
    }
}
#endif