using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Text;

// Scene-local controls. Recreates actors on reset so cooldowns, life handles and AI locks reset too.
public sealed partial class DefensiveBlockTestHarness : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject allyPrefab;
    public GameObject rectorPrefab;
    public SkillGemDefinition chargeSkill;
    public Camera testCamera;
    public DefensiveBlockCameraShot blockCamera;
    public PartySpawnPoint partySpawn;
    public bool pauseAutomaticCombat = true;
    public float startDistance = 8f;
    public bool autoBlock;
    public PlayerContext Player { get; private set; }
    public AllyContext Ally { get; private set; }
    public EnemyContext Rector { get; private set; }
    public string Status { get; private set; } = "Ready";
    Transform actors;
    bool autoRequested;
    int spawnFrame;
    public string LastDamage { get; private set; }
    public string ValidationReport { get; private set; } = "Not run";
    Coroutine validation;
    BlockAnimationProfile validationBlockProfile;
    int savedFrameRate, savedVSync;
    Coroutine reset;
    bool resetting;
    GameplayCameraController LiveCamera => GameplayCameraController.Instance;
    bool CameraReturned => LiveCamera != null && !LiveCamera.IsDefensiveBlockShotActive;

    [ContextMenu("Run Play Mode Validation")]
    public void RunValidation()
    {
        if (!Application.isPlaying || validation != null) return;
        savedFrameRate = Application.targetFrameRate; savedVSync = QualitySettings.vSyncCount;
        ValidationReport = "Running";
        validation = StartCoroutine(ValidateTrials());
    }
    IEnumerator ValidateTrials()
    {
        var report = new StringBuilder();
        for (int trial = 0; trial < 6; trial++)
        {
            startDistance = trial == 0 ? 4 : trial == 1 ? 6 : trial == 3 ? 10 : 8;
            autoBlock = trial != 4;
            if (trial == 5) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 15; }
            ResetTrial();
            yield return new WaitForSecondsRealtime(0.7f);
            while (Time.frameCount <= spawnFrame + 2) yield return null;
            float playerHp = Player.HealthSystem.currentHealth, allyHp = Ally.HealthSystem.currentHealth;
            StartCharge();
            yield return new WaitForSecondsRealtime(3.2f);
            bool expectedBlock = trial != 3 && trial != 4;
            bool passed = expectedBlock
                ? Rector.DefensiveBlockAttack.SuccessCount == 1 && Mathf.Approximately(Player.HealthSystem.currentHealth, playerHp) && Mathf.Approximately(Ally.HealthSystem.currentHealth, allyHp)
                : Rector.DefensiveBlockAttack.SuccessCount == 0 && !Ally.DefensiveBlock.IsExecuting;
            if (trial == 4) passed &= Player.HealthSystem.currentHealth < playerHp;
            bool cameraReturned = CameraReturned;
            passed &= cameraReturned;
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} trial={trial} distance={startDistance} auto={autoBlock} successes={Rector.DefensiveBlockAttack.SuccessCount} playerLoss={playerHp - Player.HealthSystem.currentHealth:0.###} allyLoss={allyHp - Ally.HealthSystem.currentHealth:0.###} cameraReturned={cameraReturned} result={Rector.DefensiveBlockAttack.LastResult} damage={LastDamage}");
        }
        Application.targetFrameRate = savedFrameRate; QualitySettings.vSyncCount = savedVSync;
        // Place a wall behind the enemy as knockback begins, without obstructing the input LOS.
        startDistance = 8f; autoBlock = true; ResetTrial();
        yield return new WaitForSecondsRealtime(0.7f);
        while (Time.frameCount <= spawnFrame + 2) yield return null;
        var wall = GameObject.Find("Knockback Test Wall (move behind Rector)");
        Vector3 wallOriginal = wall.transform.position;
        bool wallContact = false;
        Rector.KnockbackMotor.KnockbackStarted += data =>
        {
            var body = Rector.ColliderRefs.CharacterPositionCollider;
            wall.transform.position = new Vector3(0f, 1.5f, body.bounds.max.z + 0.8f);
            Physics.SyncTransforms(); wallContact = true;
        };
        StartCharge();
        yield return new WaitForSecondsRealtime(3.2f);
        bool wallPassed = wallContact && Rector.DefensiveBlockAttack.SuccessCount == 1 && !Rector.KnockbackMotor.IsActive &&
            Rector.ColliderRefs.CharacterPositionCollider.bounds.max.z <= wall.GetComponent<Collider>().bounds.min.z + 0.05f;
        report.AppendLine($"{(wallPassed ? "PASS" : "FAIL")} wall limits Rector knockback");
        wall.transform.position = wallOriginal; Physics.SyncTransforms();

        // Reset during Begin: stale cast callbacks must not affect the newly-created actors.
        autoBlock = false; ResetTrial();
        yield return new WaitForSecondsRealtime(0.7f);
        while (Time.frameCount <= spawnFrame + 2) yield return null;
        StartCharge();
        var beginResult = Rector.DefensiveBlockAttack.RequestBlock(Player);
        bool began = beginResult == InterruptionCommandResult.Success && Ally.AnimBrain.BlockPhase == BlockAnimationPhase.Begin;
        ResetTrial();
        yield return new WaitForSecondsRealtime(0.7f);
        bool resetPassed = began && !Ally.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.member.IsReserved && Rector.DefensiveBlockAttack.SuccessCount == 0;
        report.AppendLine($"{(resetPassed ? "PASS" : "FAIL")} reset during Begin releases reservation and old execution");
        foreach (string interruption in new[] { "Disable", "Death", "Down", "ControlLoss", "Reset" })
        foreach (BlockAnimationPhase phase in new[] { BlockAnimationPhase.Begin, BlockAnimationPhase.Loop, BlockAnimationPhase.Impact, BlockAnimationPhase.Exit })
        {
            autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(0.7f);
            while (Time.frameCount <= spawnFrame + 2) yield return null;
            // Phase cleanup tests must reach the phase even when Editor GC stalls a frame.
            // Normal trials above and Begin-contact tests retain the real fade timing.
            Ally.DefensiveBlock.warpFadeOutSeconds = 0f;
            StartCharge();
            Rector.DefensiveBlockAttack.RequestBlock(Player);
            // A lateral miss leaves a real Loop available without freezing the enemy's cast.
            if (phase == BlockAnimationPhase.Loop) Rector.transform.position += Vector3.right * 10f;
            // Allow Editor stalls without treating a missed wall-clock deadline as a lifecycle failure.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Ally.AnimBrain.BlockPhase != phase && Time.realtimeSinceStartup < deadline) yield return null;
            bool reached = Ally.AnimBrain.BlockPhase == phase;
            switch (interruption)
            {
                case "Disable": Ally.gameObject.SetActive(false); break;
                case "Death": Ally.HealthSystem.Die(); break;
                case "Down": Ally.stateHub.LifeSM.TryChange(LifeStateId.Down); break;
                case "ControlLoss": Ally.AnimDriver.InterruptActivePlaybackForExternalControlLoss(); break;
                case "Reset": ResetTrial(); break;
            }
            yield return null;
            while (resetting) yield return null;
            bool clean = !Ally.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.member.IsReserved &&
                Ally.AnimBrain.BlockPhase == BlockAnimationPhase.None;
            if (LiveCamera != null && LiveCamera.IsDefensiveBlockShotActive)
                yield return new WaitForSecondsRealtime(LiveCamera.blockHoldSeconds + LiveCamera.blockBlendOutSeconds + 0.15f);
            bool cameraReturned = CameraReturned;
            report.AppendLine($"{(reached && clean && cameraReturned ? "PASS" : "FAIL")} {interruption} during {phase} releases reservation and animation ownership; reached={reached} clean={clean} cameraReturned={cameraReturned} result={Rector.DefensiveBlockAttack.LastResult}");
        }
        yield return ValidateBeginContacts(report);
        yield return ValidateContactOrder(report);
        yield return ValidateProductionIntegration(report);
        yield return ValidatePlayerFallback(report);
        ResetTrial();
        autoBlock = false;
        ValidationReport = report.ToString();
        Debug.Log("DefensiveBlock PlayMode validation\n" + ValidationReport);
        validation = null;
    }

    IEnumerator ValidateBeginContacts(StringBuilder report)
    {
        foreach (float distance in new[] { 4f, 8f })
        {
            startDistance = distance; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(0.7f);
            while (Time.frameCount <= spawnFrame + 2) yield return null;
            float playerHp = Player.HealthSystem.currentHealth, allyHp = Ally.HealthSystem.currentHealth;
            BlockAnimationPhase contactPhase = BlockAnimationPhase.None;
            // Keep contact inside Begin despite variable Editor frame timing. Never edit
            // the shared profile asset: ordinary trials above use its authored duration.
            Ally.DefensiveBlock.CanBegin(Player, Rector.DefensiveBlockAttack);
            validationBlockProfile = Instantiate(Ally.DefensiveBlock.animationProfile);
            validationBlockProfile.beginSeconds = 0.4f;
            Ally.DefensiveBlock.animationProfile = validationBlockProfile;
            bool arrivedAtContact = false, allyKnockedBack = false, impactSeen = false;
            int castRequest = 0;
            bool wrongRequestRejected = false, repeatedImpactRejected = false;
            Rector.SkillManager.CastStarted += cast => castRequest = cast.RequestId;
            Rector.KnockbackMotor.KnockbackStarted += data =>
            {
                contactPhase = Ally.AnimBrain.BlockPhase;
                arrivedAtContact = Ally.DefensiveBlock.HasArrived;
                wrongRequestRejected = !Ally.DefensiveBlock.IsReadyFor(Rector.DefensiveBlockAttack, castRequest + 1) &&
                    !Ally.AnimDriver.TryBlockImpact(castRequest + 1);
            };
            Ally.KnockbackMotor.KnockbackStarted += data => allyKnockedBack = true;
            StartCharge();
            // Reproduce the reported landing into an active charge while still raising guard.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Rector.AnimBrain.TryGetActiveSkillNormalizedTime(castRequest, out float time) && time < 0.06f &&
                Time.realtimeSinceStartup < deadline) yield return null;
            var result = Rector.DefensiveBlockAttack.RequestBlock(Player);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!impactSeen && Ally.AnimBrain.BlockPhase == BlockAnimationPhase.Impact)
                {
                    impactSeen = true;
                    repeatedImpactRejected = !Ally.AnimDriver.TryBlockImpact(castRequest);
                }
                yield return null;
            }
            bool passed = result == InterruptionCommandResult.Success && arrivedAtContact &&
                contactPhase == BlockAnimationPhase.Begin && impactSeen && !allyKnockedBack &&
                wrongRequestRejected && repeatedImpactRejected &&
                Rector.DefensiveBlockAttack.SuccessCount == 1 &&
                Mathf.Approximately(Player.HealthSystem.currentHealth, playerHp) &&
                Mathf.Approximately(Ally.HealthSystem.currentHealth, allyHp) &&
                !Ally.DefensiveBlock.IsExecuting && CameraReturned;
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} Begin contact {distance} m skips Loop; contactPhase={contactPhase} arrived={arrivedAtContact} impact={impactSeen} allyKnockback={allyKnockedBack} successes={Rector.DefensiveBlockAttack.SuccessCount} wrongRequestRejected={wrongRequestRejected} repeatedImpactRejected={repeatedImpactRejected}");
            Ally.DefensiveBlock.Cancel();
            Destroy(validationBlockProfile);
            validationBlockProfile = null;
        }
        startDistance = 8f; ResetTrial();
        yield return new WaitForSecondsRealtime(0.7f);
        while (Time.frameCount <= spawnFrame + 2) yield return null;
        Ally.DefensiveBlock.CanBegin(Player, Rector.DefensiveBlockAttack);
        Ally.DefensiveBlock.warpFadeOutSeconds = 0.5f;
        int pendingRequest = 0;
        Rector.SkillManager.CastStarted += cast => pendingRequest = cast.RequestId;
        StartCharge();
        var pendingResult = Rector.DefensiveBlockAttack.RequestBlock(Player);
        bool pendingRejected = pendingResult == InterruptionCommandResult.Success &&
            Ally.AnimBrain.BlockPhase == BlockAnimationPhase.Begin && !Ally.DefensiveBlock.HasArrived &&
            !Ally.DefensiveBlock.IsReadyFor(Rector.DefensiveBlockAttack, pendingRequest);
        report.AppendLine($"{(pendingRejected ? "PASS" : "FAIL")} Begin cannot intercept before warp arrival");
        Ally.DefensiveBlock.Cancel();
    }

    void Start() { ResetTrial(); }
    public void ResetTrial()
    {
        if (reset != null) StopCoroutine(reset);
        resetting = true;
        reset = StartCoroutine(ResetActors());
    }
    IEnumerator ResetActors()
    {
        if (Rector != null) Rector.DefensiveBlockAttack?.ResetExecution();
        if (Ally != null) Ally.DefensiveBlock?.Cancel();
        if (actors != null) { actors.gameObject.SetActive(false); Destroy(actors.gameObject); }
        partySpawn.DespawnParty();
        Player = null; Ally = null; Rector = null;
        yield return null; // Allow deferred destruction before the spawn point validates the scene.
        if (!partySpawn.TrySpawnNow(out string error))
        { Status = error; Debug.LogError(error, this); resetting = false; reset = null; yield break; }
        actors = new GameObject("Trial Actors").transform;
        Player = partySpawn.CurrentParty.Player;
        Ally = partySpawn.CurrentParty.GetActor(ChainActorRole.PartySlot1).Context as AllyContext;
        Rector = Instantiate(rectorPrefab, new Vector3(0f, 0f, startDistance), Quaternion.Euler(0f, 180f, 0f), actors).GetComponent<EnemyContext>();
        Player.ResolveReferences(); Ally.ResolveReferences(); Rector.ResolveReferences();
        Rector.SkillManager.CastStarted += _ => autoRequested = false;
        if (pauseAutomaticCombat)
        {
            PauseAI(Rector);
            foreach (var actor in partySpawn.CurrentParty.Actors) PauseAI(actor.Context);
        }
        LastDamage = "none";
        Ally.HealthSystem.DamageTaken += RecordAllyDamage;
        autoRequested = false;
        spawnFrame = Time.frameCount;
        Status = "Ready: C charge / Space block / Shift dash / R reset (production input)";
        resetting = false; reset = null;
    }
    static void PauseAI(CharacteContext actor)
    {
        if (actor is AllyContext ally)
        {
            if (ally.BehaviorTree != null) ally.BehaviorTree.enabled = false;
            if (ally.AgentMoveDriver != null) ally.AgentMoveDriver.enabled = false;
            if (ally.agent != null && ally.agent.enabled && ally.agent.isOnNavMesh) ally.agent.isStopped = true;
        }
        if (actor is EnemyContext enemy)
        {
            foreach (var tree in enemy.GetComponentsInChildren<Opsive.BehaviorDesigner.Runtime.BehaviorTree>(true)) tree.enabled = false;
            foreach (var driver in enemy.GetComponentsInChildren<AgentMoveDriver>(true)) driver.enabled = false;
            if (enemy.Agent != null && enemy.Agent.enabled && enemy.Agent.isOnNavMesh) enemy.Agent.isStopped = true;
        }
    }
    void RecordAllyDamage(float amount, GameObject source)
    {
        LastDamage = $"amount={amount:0.##} phase={Ally.AnimBrain.BlockPhase} {Rector.DefensiveBlockAttack.LastProbe}";
    }
    public void StartCharge()
    {
        if (resetting || Rector == null || Time.frameCount <= spawnFrame + 2) return;
        Vector3 direction = Player.transform.position - Rector.transform.position;
        direction.y = 0;
        Rector.transform.rotation = Quaternion.LookRotation(direction);
        var result = Rector.SkillManager.TryStartExternalSkill(chargeSkill, "DefensiveBlockTest",
            usePlanarRootMotion: true, primaryTarget: SkillTargetHandle.For(Player));
        autoRequested = false;
        Status = $"Charge: {result.Kind}, request {result.RequestId}";
    }
    public void RequestBlock()
    {
        if (Player != null) Status = $"Command: {Player.interruptionCommand.TryExecuteInterruptionCommand()}";
    }
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.cKey.wasPressedThisFrame) StartCharge();
            if (keyboard.rKey.wasPressedThisFrame) ResetTrial();
        }
        if (autoBlock && !autoRequested && Rector != null && Rector.DefensiveBlockAttack.WindowOpen)
        {
            autoRequested = true;
            RequestBlock();
        }
    }
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(15, 15, 410, 285), GUI.skin.box);
        GUILayout.Label("RECTOR / DEFENSIVE BLOCK TEST");
        GUILayout.Label("C: Charge    Space: Block    Shift: Dash    R: Reset");
        GUILayout.BeginHorizontal();
        foreach (int distance in new[] { 4, 6, 8, 10 })
            if (GUILayout.Button($"{distance} m")) { startDistance = distance; ResetTrial(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Charge")) StartCharge();
        if (GUILayout.Button("Block")) RequestBlock();
        if (GUILayout.Button("Reset")) ResetTrial();
        GUILayout.EndHorizontal();
        autoBlock = GUILayout.Toggle(autoBlock, "Auto block (test assistance)");
        bool paused = GUILayout.Toggle(pauseAutomaticCombat, "Pause automatic combat (reset to apply)");
        if (paused != pauseAutomaticCombat) { pauseAutomaticCombat = paused; ResetTrial(); }
        GUILayout.Label(Status);
        if (Rector != null)
        {
            GUILayout.Label(Rector.DefensiveBlockAttack.LastResult);
            GUILayout.Label($"Window: {Rector.DefensiveBlockAttack.WindowOpen} / Successes: {Rector.DefensiveBlockAttack.SuccessCount}");
            GUILayout.Label($"Player HP {Player.HealthSystem.currentHealth:0} | Aires HP {Ally.HealthSystem.currentHealth:0}");
            GUILayout.Label($"Aires: {Ally.AnimBrain.BlockPhase} | Rector knockback: {Rector.KnockbackMotor.IsActive}");
            GUILayout.Label($"Selected: {Player.Targeting.CurrentTarget?.name ?? "none"}");
        }
        GUILayout.EndArea();
    }
    void OnDisable()
    {
        if (blockCamera != null) blockCamera.Bind(null);
        if (validation != null)
        {
            StopCoroutine(validation); validation = null;
            Application.targetFrameRate = savedFrameRate; QualitySettings.vSyncCount = savedVSync;
            ValidationReport = "Interrupted";
        }
        Rector?.DefensiveBlockAttack?.ResetExecution();
        Ally?.DefensiveBlock?.Cancel();
        if (validationBlockProfile != null) Destroy(validationBlockProfile);
        validationBlockProfile = null;
    }
}
