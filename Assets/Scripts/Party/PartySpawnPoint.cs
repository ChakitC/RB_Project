using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPartySpawnedReceiver
{
    void PrepareParty(PartyRuntime party);
    void PartySpawned(PartyRuntime party);
}

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class PartySpawnPoint : MonoBehaviour
{
    [SerializeField] private PartySpawnConfigSO config;
    [SerializeField] private bool spawnOnAwake = true;

    public static event Action<PartyRuntime> Spawned;

    public PartySpawnConfigSO Config => config;
    public PartyRuntime CurrentParty { get; private set; }

    void Awake()
    {
        if (!spawnOnAwake)
            return;

        if (!TrySpawnNow(out string error))
            Debug.LogError($"[PartySpawnPoint] {error}", this);
    }

    public bool TrySpawnNow(out string error)
    {
        if (CurrentParty != null)
        {
            error = "This spawn point has already created a party.";
            return false;
        }

        if (!TryValidateSceneSetup(out error))
            return false;

        GameObject runtimeRoot = null;
        GameObject uiRoot = null;

        try
        {
            runtimeRoot = new GameObject("PartyRuntimeRoot");
            runtimeRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            runtimeRoot.SetActive(false);

            var party = new PartyRuntime(runtimeRoot);
            for (int i = 0; i < config.Members.Count; i++)
                SpawnActor(config.Members[i], runtimeRoot.transform, party);

            uiRoot = Instantiate(config.PlayerUIPrefab, runtimeRoot.transform);
            uiRoot.name = "PlayerUI";
            PlayerUIContext uiContext = uiRoot.GetComponentInChildren<PlayerUIContext>(true);
            party.SetPlayerUI(uiRoot, uiContext);

            if (!PartyRuntimeBinder.TryBind(party, out error))
            {
                Rollback(runtimeRoot, uiRoot);
                return false;
            }

            IPartySpawnedReceiver[] receivers = ResolveReceivers(runtimeRoot);
            for (int i = 0; i < receivers.Length; i++)
                receivers[i].PrepareParty(party);

            uiRoot.SetActive(false);
            uiRoot.transform.SetParent(null, false);

            runtimeRoot.SetActive(true);
            uiRoot.SetActive(true);

            CurrentParty = party;
            for (int i = 0; i < receivers.Length; i++)
                receivers[i].PartySpawned(party);

            Spawned?.Invoke(party);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            CurrentParty = null;
            Rollback(runtimeRoot, uiRoot);
            error = $"Party creation failed: {exception.Message}";
            Debug.LogException(exception, this);
            return false;
        }
    }

    public bool TryValidateSceneSetup(out string error)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("Party spawn config is missing.");
        }
        else
        {
            config.Validate(errors);
        }

        int activeMarkerCount = 0;
        PartySpawnPoint[] markers =
            FindObjectsByType<PartySpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && markers[i].gameObject.scene == gameObject.scene)
                activeMarkerCount++;
        }

        if (activeMarkerCount != 1)
            errors.Add($"Scene '{gameObject.scene.name}' must contain exactly one active PartySpawnPoint; found {activeMarkerCount}.");

        if (HasExistingPartyActors())
            errors.Add("Scene already contains a manually placed Player or Ally. Remove the legacy PlayerSquad before spawning.");

        if (HasExistingPlayerUI())
            errors.Add("Scene already contains a manually placed PlayerUIContext. Remove the legacy Player UI before spawning.");

        error = string.Join("\n", errors);
        return errors.Count == 0;
    }

    [ContextMenu("Validate Party Setup")]
    void ValidateFromContextMenu()
    {
        if (TryValidateSceneSetup(out string error))
            Debug.Log("[PartySpawnPoint] Party setup is valid.", this);
        else
            Debug.LogError($"[PartySpawnPoint] Party setup is invalid:\n{error}", this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!gameObject.scene.IsValid() || config == null)
            return;

        if (!TryValidateSceneSetup(out string error))
            Debug.LogWarning($"[PartySpawnPoint] {error}", this);
    }
#endif

    static void SpawnActor(PartySpawnEntry entry, Transform parent, PartyRuntime party)
    {
        GameObject actorRoot = Instantiate(entry.Prefab, parent);
        actorRoot.name = RoleObjectName(entry.Role);
        actorRoot.transform.localPosition = entry.LocalPosition;
        actorRoot.transform.localRotation = entry.LocalRotation;

        CharacteContext context = actorRoot.GetComponentInChildren<CharacteContext>(true);
        CharacterContextPartyLoader loader =
            actorRoot.GetComponentInChildren<CharacterContextPartyLoader>(true);
        FieldAllyMember fieldMember = actorRoot.GetComponentInChildren<FieldAllyMember>(true);

        party.AddActor(new PartyRuntimeActor(
            entry.Role,
            entry.PartyIndex,
            actorRoot,
            context,
            loader,
            fieldMember));
    }

    IPartySpawnedReceiver[] ResolveReceivers(GameObject runtimeRoot)
    {
        var receivers = new List<IPartySpawnedReceiver>();
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            if (roots[rootIndex] == runtimeRoot)
                continue;

            MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                if (behaviours[behaviourIndex] is IPartySpawnedReceiver receiver)
                    receivers.Add(receiver);
            }
        }

        return receivers.ToArray();
    }

    bool HasExistingPartyActors()
    {
        CharacteContext[] contexts =
            FindObjectsByType<CharacteContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext context = contexts[i];
            if (context != null &&
                context.gameObject.scene == gameObject.scene &&
                (context is PlayerContext || context is AllyContext))
            {
                return true;
            }
        }

        return false;
    }

    bool HasExistingPlayerUI()
    {
        PlayerUIContext[] contexts =
            FindObjectsByType<PlayerUIContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            if (contexts[i] != null && contexts[i].gameObject.scene == gameObject.scene)
                return true;
        }

        return false;
    }

    static string RoleObjectName(ChainActorRole role)
    {
        return role switch
        {
            ChainActorRole.Player => "Player",
            ChainActorRole.PartySlot1 => "Ally 1",
            ChainActorRole.PartySlot2 => "Ally 2",
            ChainActorRole.Helper => "Ally_Helper",
            _ => role.ToString(),
        };
    }

    static void Rollback(GameObject runtimeRoot, GameObject uiRoot)
    {
        if (uiRoot != null && (runtimeRoot == null || uiRoot.transform.parent != runtimeRoot.transform))
            DestroyObject(uiRoot);

        if (runtimeRoot != null)
            DestroyObject(runtimeRoot);
    }

    static void DestroyObject(GameObject target)
    {
        if (target == null)
            return;

        target.SetActive(false);
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
