using System.ComponentModel;
using ItemTool.Application.DTOs;

namespace ItemTool.App.ViewModels;

public sealed class ItemEditorViewModel : ViewModelBase
{
    private ItemDto? _selectedItem;

    public ItemDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != null)
            {
                _selectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;

                if (_selectedItem.SlotDimension != null)
                    _selectedItem.SlotDimension.PropertyChanged -= OnSelectedItemDimensionsChanged;
            }

            if (SetProperty(ref _selectedItem, value))
            {
                if (_selectedItem != null)
                {
                    _selectedItem.PropertyChanged += OnSelectedItemPropertyChanged;

                    if (_selectedItem.SlotDimension != null)
                        _selectedItem.SlotDimension.PropertyChanged += OnSelectedItemDimensionsChanged;
                }

                ItemChanged?.Invoke();
            }
        }
    }

    public event Action? ItemChanged;

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemDto.SlotDimension))
        {
            if (sender is ItemDto item)
            {
                if (item.SlotDimension != null)
                    item.SlotDimension.PropertyChanged += OnSelectedItemDimensionsChanged;
            }
        }

        ItemChanged?.Invoke();
    }

    private void OnSelectedItemDimensionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ItemChanged?.Invoke();
    }
}