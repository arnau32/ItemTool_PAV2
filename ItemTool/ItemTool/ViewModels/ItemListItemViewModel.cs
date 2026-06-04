using System.Windows.Media;
using ItemTool.App.Visuals;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.App.ViewModels;

public sealed class ItemListItemViewModel : ViewModelBase
{
    public ItemDto Item { get; }

    public string? SourceId { get; private set; }

    public ItemListItemViewModel(ItemDto item)
    {
        Item = item;
        SourceId = item.Id;

        Item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemDto.DisplayName) ||
                e.PropertyName == nameof(ItemDto.ItemNameId))
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Subtitle));
            }

            if (e.PropertyName == nameof(ItemDto.ItemKind) ||
                e.PropertyName == nameof(ItemDto.ItemType))
            {
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(KindBrush));
                OnPropertyChanged(nameof(IconFallbackText));
            }

            if (e.PropertyName == nameof(ItemDto.ItemRarity))
            {
                OnPropertyChanged(nameof(RarityText));
                OnPropertyChanged(nameof(RarityBrush));
            }

            if (e.PropertyName == nameof(ItemDto.IconPath))
            {
                OnPropertyChanged(nameof(IconSource));
            }
        };
    }

    public void MarkAsSaved()
    {
        SourceId = Item.Id;
    }

    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Item.DisplayName))
                return Item.DisplayName;

            if (!string.IsNullOrWhiteSpace(Item.ItemNameId))
                return Item.ItemNameId;

            return "<Unnamed>";
        }
    }

    public string Type => Item.ItemKind.ToString();

    public string IconFallbackText
    {
        get
        {
            return Item.ItemKind switch
            {
                ItemKind.Equipment => "E",
                ItemKind.Weapon => "W",
                ItemKind.Consumable => "C",
                ItemKind.Crafting => "M",
                ItemKind.Collectable => "Q",
                _ => "?"
            };
        }
    }

    public string RarityText => Item.ItemRarity.ToString();

    public Brush KindBrush => ItemVisualTheme.GetItemKindBrush(Item.ItemKind);

    public Brush RarityBrush => ItemVisualTheme.GetRarityBrush(Item.ItemRarity);

    public ImageSource? IconSource => ItemIconSourceLoader.Load(Item.IconPath);

    public string Subtitle
    {
        get
        {
            string id = string.IsNullOrWhiteSpace(Item.ItemNameId)
                ? "No ID"
                : Item.ItemNameId;

            return $"{Item.ItemKind} · {id}";
        }
    }

    private ValidationSeverity _severity;

    public ValidationSeverity Severity
    {
        get => _severity;
        set => SetProperty(ref _severity, value);
    }
}