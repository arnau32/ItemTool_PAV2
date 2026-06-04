using UnityEngine;
using System.Linq;

public class ResolutionSelector : MonoBehaviour
{
    public Resolution[] resolutions;
    public OptionSelector selector;

    Vector2Int[] commonResolutions = new Vector2Int[]
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
    };

    void Start()
    {
        resolutions = commonResolutions
        .Where(r => Screen.resolutions.Any(sr => sr.width == r.x && sr.height == r.y))
        .Select(r => new Resolution { width = r.x, height = r.y })
        .ToArray();

        string[] options = new string[resolutions.Length];

        for (int i = 0; i < resolutions.Length; i++)
        {
            options[i] = resolutions[i].width + " × " + resolutions[i].height;
        }

        int currentIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentIndex = i;
                break;
            }
        }

        selector.SetOptions(options, currentIndex);

        selector.OnValueChanged += OnResolutionChanged;
    }

    void OnResolutionChanged(int i)
    {
        var r = resolutions[i];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
    }
}