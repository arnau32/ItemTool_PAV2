#if UNITY_EDITOR
using System;

public partial class ItemBrowserWindow
{
    internal enum SortMode
    {
        NameAz,
        NameZa,
        Type
    }

    [Flags]
    private enum RefreshFlags
    {
        None = 0,
        Assets = 1 << 0,
        Filter = 1 << 1,
        Validations = 1 << 2,
        Details = 1 << 3,
        List = 1 << 4,

        Soft = Filter | Validations | List | Details,
        Hard = Assets | Filter | Validations | List | Details
    }

    internal enum ValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}
#endif