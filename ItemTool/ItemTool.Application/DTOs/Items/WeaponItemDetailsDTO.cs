using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class WeaponItemDetailsDto : ObservableObject
{
    [ObservableProperty]
    private WeaponHandType handType = WeaponHandType.Single;

    [ObservableProperty]
    private WeaponFamily familyType = WeaponFamily.Sword;

    [ObservableProperty]
    private int weaponTier = 1;

    [ObservableProperty]
    private float enemyDamageMultiplier = 1f;

    [ObservableProperty]
    private float skillScoreNeeded = 6f;

    [ObservableProperty]
    private string? prefabVariantPath;
}