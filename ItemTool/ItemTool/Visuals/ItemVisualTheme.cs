using System.Windows.Media;
using ItemTool.Domain.Enums;

namespace ItemTool.App.Visuals;

public static class ItemVisualTheme
{
    private static readonly Brush NeutralBrush = CreateBrush(90, 90, 90);
    private static readonly Brush NeutralPreviewBrush = CreateBrush(120, 120, 120);

    private static readonly Brush EquipmentBrush = CreateBrush(210, 155, 70);
    private static readonly Brush WeaponBrush = CreateBrush(190, 70, 70);
    private static readonly Brush ConsumableBrush = CreateBrush(80, 160, 95);
    private static readonly Brush CraftingBrush = CreateBrush(90, 135, 180);
    private static readonly Brush CollectableBrush = CreateBrush(150, 95, 190);

    private static readonly Brush CommonBrush = CreateBrush(135, 135, 135);
    private static readonly Brush RareBrush = CreateBrush(70, 135, 210);
    private static readonly Brush EpicBrush = CreateBrush(165, 95, 220);
    private static readonly Brush LegendaryBrush = CreateBrush(230, 165, 65);

    private static readonly Brush CommonPreviewBrush = CreateBrush(165, 165, 165);
    private static readonly Brush RarePreviewBrush = CreateBrush(105, 165, 235);
    private static readonly Brush EpicPreviewBrush = CreateBrush(190, 125, 240);
    private static readonly Brush LegendaryPreviewBrush = CreateBrush(245, 190, 95);

    public static Brush GetItemKindBrush(ItemKind? itemKind)
    {
        return itemKind switch
        {
            ItemKind.Equipment => EquipmentBrush,
            ItemKind.Weapon => WeaponBrush,
            ItemKind.Consumable => ConsumableBrush,
            ItemKind.Crafting => CraftingBrush,
            ItemKind.Collectable => CollectableBrush,
            _ => NeutralBrush
        };
    }

    public static Brush GetRarityBrush(ItemRarity? rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => CommonBrush,
            ItemRarity.Rare => RareBrush,
            ItemRarity.Epic => EpicBrush,
            ItemRarity.Legendary => LegendaryBrush,
            _ => NeutralBrush
        };
    }

    public static Brush GetRarityPreviewBrush(ItemRarity? rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => CommonPreviewBrush,
            ItemRarity.Rare => RarePreviewBrush,
            ItemRarity.Epic => EpicPreviewBrush,
            ItemRarity.Legendary => LegendaryPreviewBrush,
            _ => NeutralPreviewBrush
        };
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        SolidColorBrush brush = new(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}