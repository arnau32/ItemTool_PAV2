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
            if (e.PropertyName == nameof(ItemDto.ItemName))
                OnPropertyChanged(nameof(Name));

            if (e.PropertyName == nameof(ItemDto.ItemType))
                OnPropertyChanged(nameof(Type));
        };
    }

    public string Name => string.IsNullOrWhiteSpace(Item.ItemName) ? "<Unnamed>" : Item.ItemName;
    public string Type => Item.ItemType.ToString();

    private ValidationSeverity _severity;
    public ValidationSeverity Severity
    {
        get => _severity;
        set => SetProperty(ref _severity, value);
    }
}