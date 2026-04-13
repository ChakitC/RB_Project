using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ItemPickupVisualPresenter : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;

    GameObject currentVisual;

    void Reset()
    {
        EnsureVisualRoot();
    }

    public void Present(ItemDefinition item)
    {
        EnsureVisualRoot();
        ClearVisual();

        if (item == null)
            return;

        GameObject visualPrefab = item.ResolvePickupVisualPrefab();
        if (visualPrefab == null)
            return;

        currentVisual = Instantiate(visualPrefab, visualRoot);
        var visualTransform = currentVisual.transform;
        visualTransform.localPosition = item.pickupVisualPositionOffset;
        visualTransform.localRotation = Quaternion.Euler(item.pickupVisualRotationOffset);
        visualTransform.localScale = item.ResolvePickupVisualScale();

        PrepareVisualInstance(currentVisual);
    }

    void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        var existing = transform.Find("VisualRoot");
        if (existing != null)
        {
            visualRoot = existing;
            return;
        }

        var created = new GameObject("VisualRoot");
        created.transform.SetParent(transform, false);
        visualRoot = created.transform;
    }

    void ClearVisual()
    {
        if (visualRoot == null)
            return;

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            var child = visualRoot.GetChild(i).gameObject;
            child.SetActive(false);

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        currentVisual = null;
    }

    void PrepareVisualInstance(GameObject visualInstance)
    {
        if (visualInstance == null)
            return;

        foreach (var collider in visualInstance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var rigidbody in visualInstance.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
        }

        foreach (var characterController in visualInstance.GetComponentsInChildren<CharacterController>(true))
            characterController.enabled = false;

        foreach (var agent in visualInstance.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;

        foreach (var audioSource in visualInstance.GetComponentsInChildren<AudioSource>(true))
            audioSource.enabled = false;

        foreach (var behaviour in visualInstance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            behaviour.enabled = false;
        }
    }
}
