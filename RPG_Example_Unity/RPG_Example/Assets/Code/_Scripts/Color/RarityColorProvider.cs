using UnityEngine;

public static class RarityColorProvider
{
    private static RarityColorConfig _RarityColorConfig;


    private static RarityColorConfig _RarityTextColorConfig;

    public static RarityColorConfig RarityColorConfig
    {
        get
        {
            if (_RarityColorConfig == null)
            {
                _RarityColorConfig = Resources.Load<RarityColorConfig>("RarityColorConfig");
            }
            return _RarityColorConfig;
        }
    }

    public static RarityColorConfig RarityTextColorConfig
    {
        get
        {
            if (_RarityTextColorConfig == null)
            {
                _RarityTextColorConfig = Resources.Load<RarityColorConfig>("RarityTextColorConfig");
            }
            return _RarityTextColorConfig;
        }
    }
}
