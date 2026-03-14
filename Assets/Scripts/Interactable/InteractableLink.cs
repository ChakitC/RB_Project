using UnityEngine;

public class InteractableLink : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] targets;

    void Awake()
    {
        if (targets == null || targets.Length == 0)
        {
            var found = GetComponentsInParent<MonoBehaviour>(true);
            var list = new System.Collections.Generic.List<MonoBehaviour>();

            foreach (var mb in found)
            {
                if (mb is IInteractable)
                    list.Add(mb);
            }

            targets = list.ToArray();
        }
    }

    public IInteractable GetBest(Interactor interactor)
    {
        IInteractable best = null;
        int bestPrio = int.MinValue;

        if (targets == null)
            return null;

        for (int i = 0; i < targets.Length; i++)
        {
            var mb = targets[i];
            if (!mb) continue;
            if (mb is not IInteractable it) continue;

            if (!it.CanInteract(interactor)) continue;

            if (it.Priority > bestPrio)
            {
                bestPrio = it.Priority;
                best = it;
            }
        }

        return best;
    }
}