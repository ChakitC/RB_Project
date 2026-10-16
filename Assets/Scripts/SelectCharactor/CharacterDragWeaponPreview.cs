using UnityEngine;

/// <summary>Resolves the dragged character's equipment and passes it to the presentation.</summary>
public class CharacterDragWeaponPreview : MonoBehaviour
{
    // Retained for SetAnimator API compatibility. The presentation owns the live binding.
    [SerializeField] private Animator animator;
    [SerializeField] private WeaponDatabase weaponDatabase;

    BasementCharacterPresentation Presentation
    {
        get
        {
            var visual = GetComponent<CharacterDragVisualPreview>();
            return visual != null ? visual.Presentation : null;
        }
    }

    public void SetAnimator(Animator targetAnimator) => animator = targetAnimator;

    public void Build(CharacterStats selected, int currentSlot = 0, int partyIndex = -1)
    {
        var presentation = Presentation;
        if (!presentation) return;
        if (selected == null || selected != presentation.CurrentDef)
        {
            presentation.ClearWeapon();
            return;
        }

        CharacterWeaponPreviewResolver.TryResolveSelectedCharacterWeapon(
            selected, currentSlot, partyIndex, weaponDatabase, out var weapon);
        presentation.ShowWeapon(weapon);
    }

    public void BuildFromHolder(int currentSlot = 0, int partyIndex = -1)
    {
        var presentation = Presentation;
        Build(presentation != null ? presentation.CurrentDef : null, currentSlot, partyIndex);
    }

    public void Clear()
    {
        var presentation = Presentation;
        if (presentation) presentation.ClearWeapon();
    }
}
