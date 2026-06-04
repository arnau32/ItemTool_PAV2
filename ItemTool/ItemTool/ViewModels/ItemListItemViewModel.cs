using ItemTool.Application.DTOs;
using ItemTool.Domain.Validation;

namespace ItemTool.App.ViewModels;

public sealed class ItemListItemViewModel : ViewModelBase
{
    public ItemDto Item { get; }

    public ItemListItemViewModel(ItemDto item)
    {
        Item = item;

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
            }
        };
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