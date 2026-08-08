using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the final combined stats for a character while it only exists as data
/// (Basement / character-select hub), where there is no live character hierarchy and therefore no
/// <see cref="StatsHub"/> to ask.
///
/// Bonus sources included: equipped weapon (base + upgrade + static affixes), equipped accessories
/// (base + reforge modifier), and always-on passives. Temporary status effects and timed/stacking
/// weapon affix buffs are deliberately excluded - they can never be active in the lobby.
///
/// The math itself is not duplicated here: everything funnels through
/// <see cref="CharacterStatFormula"/>, the same code path <see cref="StatsHub"/> uses in combat.
/// </summary>
public static class LobbyCharacterStatPreview
{
    static readonly List<RuntimeStatModifier> ModifierBuffer = new();
    static readonly List<PassiveDefinition> PassiveBuffer = new();

    public static CharacterStatTotals Build(
        CharacterStats stats,
        int level,
        string characterId,
        PlayerInventory inventory,
        WeaponAffixDatabase affixDatabase = null)
    {
        if (stats == null)
            return default;

        float levelOffset = Mathf.Max(0f, level - 1f);
        string ownerId = CharacterEquipment.BuildCharacterOwnerId(characterId);

        InventorySlotData weaponSlot = EquipmentAssignmentService.GetEquippedSlotData(
            inventory,
            EquipmentItemKind.Weapon,
            ownerId);

        GunConfig weapon = weaponSlot != null && !weaponSlot.IsEmpty ? weaponSlot.item as GunConfig : null;
        WeaponInstanceData weaponInstance = weaponSlot != null ? weaponSlot.weaponInstance : null;

        var inputs = new CharacterStatInputs
        {
            // Same shape as StatsHub.GetCharacter*Base(): value + scaling * max(0, level - 1).
            CharacterDamage = stats.Damage + stats.DamageScaling * levelOffset,
            CharacterArmor = stats.armor + stats.ArmorScaling * levelOffset,
            CharacterMoveSpeed = stats.speed + stats.SpeedScaling * levelOffset,
            CharacterCritRate = stats.critRate + stats.CritrateScaling * levelOffset,
            CharacterCritMultiplier = Mathf.Max(1f, stats.critMultiplier + stats.CritDamageScaling * levelOffset),
            CharacterMaxHealth = stats.maxHP + stats.MAXHPScaling * levelOffset,
            CharacterMaxStamina = stats.maxStamina + stats.StaminaScaling * levelOffset,
            CharacterMaxEnergy = stats.Enagy + stats.EnagyScaling * levelOffset,
            Weapon = weapon
        };

        ModifierBuffer.Clear();
        PassiveBuffer.Clear();

        WeaponStatPreview.AppendWeaponUpgradeModifiers(ModifierBuffer, weapon, weaponInstance);
        WeaponStatPreview.AppendStaticAffixModifiers(ModifierBuffer, weaponInstance, affixDatabase);
        AppendAccessoryModifiers(ModifierBuffer, PassiveBuffer, inventory, ownerId);
        AppendPassiveModifiers(ModifierBuffer, PassiveBuffer);

        CharacterStatTotals totals = CharacterStatFormula.Compute(inputs, ModifierBuffer);

        ModifierBuffer.Clear();
        PassiveBuffer.Clear();
        return totals;
    }

    static void AppendAccessoryModifiers(
        List<RuntimeStatModifier> modifierBuffer,
        List<PassiveDefinition> passiveBuffer,
        PlayerInventory inventory,
        string ownerId)
    {
        int slotCount = AccessoryLoadout.ResolveOwnerSlotCount(ownerId);

        for (int i = 0; i < slotCount; i++)
        {
            InventorySlotData slotData = EquipmentAssignmentService.GetEquippedSlotData(
                inventory,
                EquipmentItemKind.Accessory,
                ownerId,
                i);

            if (slotData == null || slotData.IsEmpty || !slotData.HasAccessoryInstance)
                continue;

            if (slotData.item is not AccessoryDefinition definition)
                continue;

            AccessoryLoadout.AppendInstanceStatModifiers(modifierBuffer, definition, slotData.accessoryInstance);
            AccessoryLoadout.AppendInstancePassiveDefinitions(passiveBuffer, definition, slotData.accessoryInstance);
        }
    }

    /// <summary>
    /// Mirrors PassiveController.AddAlwaysOnPassive: only always-on passives contribute stat
    /// modifiers, using the raw authored value with no stack multiplier.
    /// </summary>
    static void AppendPassiveModifiers(List<RuntimeStatModifier> buffer, List<PassiveDefinition> passives)
    {
        for (int i = 0; i < passives.Count; i++)
        {
            if (passives[i] is not AlwaysOnPassiveDef alwaysOn || alwaysOn.modifiers == null)
                continue;

            string modifierKey = $"passive:{alwaysOn.RuntimeId}";
            for (int j = 0; j < alwaysOn.modifiers.Count; j++)
            {
                PassiveStatModifier modifier = alwaysOn.modifiers[j];
                if (modifier == null)
                    continue;

                buffer.Add(new RuntimeStatModifier(
                    modifier.statType,
                    modifier.operation,
                    modifier.value,
                    modifierKey));
            }
        }
    }
}
