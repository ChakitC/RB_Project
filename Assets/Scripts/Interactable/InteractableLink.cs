using UnityEngine;
using System.Collections.Generic;

public class InteractableLink : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] targets;

    void Awake()
    {
        if (targets == null || targets.Length == 0)
        {
            var list = new List<MonoBehaviour>();
            AddInteractables(GetComponentsInParent<MonoBehaviour>(true), list);

            var ownerContext = GetComponentInParent<CharacteContext>();
            if (ownerContext != null)
            {
                AddInteractables(ownerContext.GetComponentsInChildren<MonoBehaviour>(true), list);
            }
            else if (transform.parent != null)
            {
                AddInteractables(transform.parent.GetComponentsInChildren<MonoBehaviour>(true), list);
            }

            targets = list.ToArray();
        }
    }

    static void AddInteractables(MonoBehaviour[] found, List<MonoBehaviour> list)
    {
        if (found == null)
            return;

        foreach (var mb in found)
        {
            if (!mb) continue;
            if (mb is not IInteractable) continue;
            if (list.Contains(mb)) continue;

            list.Add(mb);
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
