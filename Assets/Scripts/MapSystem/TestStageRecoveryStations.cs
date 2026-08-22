using UnityEngine;

/// <summary>
/// Turns a Heal room's authored stations into the Test Stage recovery point: a one-use 50% party
/// heal and a reserve-only party ammo refill. Authored stations are configured in place; simple
/// runtime stands are created only when the prefab is missing one.
///
/// This lives on the Heal room prefab rather than in <see cref="RoomController"/> so the generic
/// room controller carries no stage-specific behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class TestStageRecoveryStations : MonoBehaviour, IRoomLifecycleListener
{
    [Tooltip("สัดส่วน HP ที่ Heal Point ฟื้นให้ทั้งปาร์ตี้ (0.5 = 50%)")]
    [SerializeField, Range(0f, 1f)] private float healPercent = 0.5f;

    [Tooltip("ตำแหน่ง local ของ Heal Point ที่สร้างสำรองเมื่อ prefab ไม่ได้ author ไว้")]
    [SerializeField] private Vector3 fallbackHealStationPosition = new(-1.5f, 0.25f, 0f);

    [Tooltip("ตำแหน่ง local ของ Ammo Point ที่สร้างสำรองเมื่อ prefab ไม่ได้ author ไว้")]
    [SerializeField] private Vector3 fallbackAmmoStationPosition = new(1.5f, 0.25f, 0f);

    private bool configured;

    public void OnRoomInitialized(RoomController room, MapNode node)
    {
        // Only Test Stages run the recovery contract, and only on a Heal node.
        if (configured ||
            room == null ||
            room.RunController == null ||
            room.RunController.RunConfig == null ||
            !room.RunController.RunConfig.IsTestStage ||
            node == null ||
            node.Type != MapNodeType.Heal)
        {
            return;
        }

        configured = true;
        Transform fallbackParent = room.RuntimeContent.PersistentRoot;

        HealInteractable healStation = room.GetComponentInChildren<HealInteractable>(true);
        if (healStation != null)
        {
            healStation.ConfigurePartyPercentHeal(healPercent);
            healStation.GetComponent<InteractableLink>().RefreshTargets();
        }
        else
        {
            CreateHealStation(fallbackParent, fallbackHealStationPosition, healPercent);
        }

        AmmoRefillInteractable ammoStation = room.GetComponentInChildren<AmmoRefillInteractable>(true);
        if (ammoStation != null)
        {
            ammoStation.ConfigurePartyReserveRefill();
            ammoStation.GetComponent<InteractableLink>().RefreshTargets();
        }
        else
        {
            CreateAmmoStation(fallbackParent, fallbackAmmoStationPosition);
        }
    }

    public void OnRoomBegan(RoomController room, MapNode node)
    {
    }

    public void OnRoomCleared(RoomController room, MapNode node)
    {
    }

    static void CreateHealStation(Transform parent, Vector3 localPosition, float healPercent)
    {
        Transform wrapper = new GameObject("HealStation").transform;
        wrapper.SetParent(parent, false);

        GameObject station = CreateRecoveryStationVisual(wrapper, "Heal Point", localPosition, new Color(0.2f, 0.85f, 0.35f));
        station.AddComponent<HealInteractable>().ConfigurePartyPercentHeal(healPercent);
        station.GetComponent<InteractableLink>().RefreshTargets();
    }

    static void CreateAmmoStation(Transform parent, Vector3 localPosition)
    {
        Transform wrapper = new GameObject("AmmoStation").transform;
        wrapper.SetParent(parent, false);

        GameObject station = CreateRecoveryStationVisual(wrapper, "Ammo Point", localPosition, new Color(0.2f, 0.55f, 1f));
        station.AddComponent<AmmoRefillInteractable>().ConfigurePartyReserveRefill();
        station.GetComponent<InteractableLink>().RefreshTargets();
    }

    static GameObject CreateRecoveryStationVisual(Transform parent, string name, Vector3 localPosition, Color color)
    {
        GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        station.name = name;
        station.transform.SetParent(parent, false);
        station.transform.localPosition = localPosition;
        station.transform.localScale = new Vector3(0.75f, 0.25f, 0.75f);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            station.layer = interactableLayer;

        Collider stationCollider = station.GetComponent<Collider>();
        if (stationCollider != null)
            stationCollider.isTrigger = true;

        Renderer stationRenderer = station.GetComponent<Renderer>();
        if (stationRenderer != null)
            stationRenderer.material.color = color;

        return station;
    }
}
