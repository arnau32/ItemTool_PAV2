using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

public class MapPOIIcon : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private RectTransform iconTransform;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private LocalizeStringEvent localizedLabel;

    
    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            return rectTransform;
        }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(MapPOIData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[MapPOIIcon] Setup called with null data.");
            return;
        }

        // Root Size
        if (rectTransform != null)
        {
            rectTransform.localScale = new Vector2(data._allscale, data._allscale);
        }
        
        // Icon Size
        if (iconTransform != null)
        {
            iconTransform.localScale = new Vector2(data._iconscale, data._iconscale);
        }
        

        // Icon
        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color = data.icon_color;
            iconImage.enabled = data.icon != null;
        }

        // Border
        if (borderImage != null)
        {
            borderImage.sprite = data.border;
            borderImage.enabled = data.border != null;
        }

        // Background
        if (backgroundImage != null)
        {
            backgroundImage.sprite = data.background;
            backgroundImage.color = data.background_color;
            backgroundImage.enabled = data.background != null;
        }

        // Localization
        if (localizedLabel != null && data.localizedName != null)
        {
            localizedLabel.StringReference.TableReference =
                data.localizedName.TableReference;

            localizedLabel.StringReference.TableEntryReference =
                data.localizedName.TableEntryReference;

            localizedLabel.StringReference.Arguments =
                data.localizedName.Arguments;

            localizedLabel.RefreshString();
        }
    }
}