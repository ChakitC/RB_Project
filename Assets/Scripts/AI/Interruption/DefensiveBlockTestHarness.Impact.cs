using System.Collections;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

public sealed partial class DefensiveBlockTestHarness
{
    [ContextMenu("Run Impact Marker Validation (Cooperative sample)")]
    public void RunImpactMarkerValidation()
    {
        if (!Application.isPlaying || validation != null || chargeSkill?.defensiveBlock?.mode != DefensiveBlockMode.Cooperative) return;
        validation = StartCoroutine(ValidateImpactMarkers());
    }

    IEnumerator ValidateImpactMarkers()
    {
        var original = chargeSkill;
        var trialSkill = Instantiate(original);
        // This fixture collapses all authored windows into one. Retain every step binding
        // so a later strike cannot interrupt the guard while testing Continue playback.
        var steps = original.defensiveBlock.HasMultipleWindowData
            ? original.defensiveBlock.windows.SelectMany(w => w.hitboxSteps).Distinct().ToArray()
            : (int[])original.defensiveBlock.hitboxSteps.Clone();
        var report = new StringBuilder();
        bool savedAuto = autoBlock;
        autoBlock = false;
        chargeSkill = trialSkill;
        ValidationReport = "Impact marker validation running";
        try
        {
            foreach (string trial in new[] { "CooperativeSuccess", "CooperativeFailure", "CooperativeDefault", "TimedInterrupt", "TimedContinue", "TimedDefault", "CancelPreparing",
                "CooperativeEnemyLoopSuccess", "CooperativeEnemyLoopFailure", "CooperativeEnemyLoopCancel", "CooperativeEnemyLoopDisable" })
            {
                bool cooperative = trial.StartsWith("Cooperative") || trial == "CancelPreparing";
                bool marked = !trial.EndsWith("Default");
                bool continues = trial == "TimedContinue";
                bool enemyLoop = trial.Contains("EnemyLoop");
                var settings = trialSkill.defensiveBlock;
                // Reusing the attack clip stress-tests state isolation and skipped payload events.
                settings.cooperative.enemyStruggleClip = enemyLoop ? trialSkill.skillClip.Clip : null;
                settings.mode = cooperative ? DefensiveBlockMode.Cooperative : DefensiveBlockMode.TimedApproach;
                settings.windows = new[] { new DefensiveBlockWindow {
                    startNormalized = 0f, endNormalized = .62f, useImpactMarker = marked, impactNormalized = .7f,
                    hitboxSteps = (int[])steps.Clone(),
                    onSuccess = continues ? DefensiveBlockOutcome.ContinueSkill : DefensiveBlockOutcome.InterruptSkill } };
                startDistance = 8f;
                ResetTrial(); while (resetting) yield return null;
                yield return new WaitForSecondsRealtime(.7f);
                int commits = 0, releases = 0, hits = 0;
                Rector.SkillManager.CastCommitted += _ => commits++;
                Rector.SkillManager.CastReleased += _ => releases++;
                Player.HealthSystem.DamageTaken += (_, __) => hits++;
                Ally.HealthSystem.DamageTaken += (_, __) => hits++;
                StartCharge();
                var attack = Rector.DefensiveBlockAttack;
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!attack.CanRequestBlock(Player) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                var accepted = attack.RequestBlock(Player);
                var session = Player.interruptionCommand.ActiveCooperativeBlock;
                bool exact = !marked, resumed = !continues;
                bool loopValid = true;
                bool shakeValid = CooperativeCameraShake() == Vector3.zero;
                bool successShake = false;
                if (session != null) session.Ended += (_, result) =>
                {
                    successShake = result == CooperativeBlockState.Success
                        ? CooperativeCameraShake().sqrMagnitude > 0f : CooperativeCameraShake() == Vector3.zero;
                };
                bool contactVfxValid = CooperativeContactVfx(session) == null;
                bool windValid = CooperativeWind(session) == null;
                PlayerScreenEffectHandle wind = null;
                GameObject contactEffect = null;
                Animancer.AnimancerState loopState = null;
                if (trial == "CancelPreparing") session?.Cancel();
                deadline = Time.realtimeSinceStartupAsDouble + 4d;
                while ((attack.IsTimedApproach || session?.State == CooperativeBlockState.Preparing) && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    if (marked && Rector.AnimBrain.TryGetActiveSkillNormalizedTime(attack.RequestId, out float pose))
                        exact |= Mathf.Abs(pose - .7f) < .0001f;
                    yield return null;
                }
                if (cooperative && session?.State == CooperativeBlockState.Struggling)
                {
                    Rector.AnimBrain.TryGetActiveSkillNormalizedTime(session.RequestId, out float held);
                    exact = !marked || Mathf.Abs(held - .7f) < .0001f;
                    loopState = EnemyStruggleState();
                    float loopBefore = loopState != null ? loopState.NormalizedTime : 0f;
                    wind = CooperativeWind(session);
                    var windParticle = WindParticle(wind);
                    float windBefore = windParticle != null ? windParticle.time : -1f;
                    windValid &= wind?.IsActive == true && windParticle != null;
                    Vector3 rootBefore = Rector.transform.position;
                    int timelineEvents = 0;
                    Vector3 shakeAtContact = CooperativeCameraShake();
                    shakeValid &= shakeAtContact.sqrMagnitude > 0f;
                    contactEffect = CooperativeContactVfx(session);
                    contactVfxValid &= contactEffect != null && Vector3.Distance(contactEffect.transform.position,
                        (Player.DefensiveBlock.GuardCenter + Ally.DefensiveBlock.GuardCenter) * .5f) < .01f;
                    if (enemyLoop) Rector.AnimBrain.SkillTimelineEventRaised += (_, __) => timelineEvents++;
                    yield return new WaitForSecondsRealtime(.15f);
                    shakeValid &= CooperativeCameraShake() != shakeAtContact;
                    contactVfxValid &= CooperativeContactVfx(session) == contactEffect;
                    windValid &= windParticle != null && windParticle.time > windBefore;
                    Rector.AnimBrain.TryGetActiveSkillNormalizedTime(session.RequestId, out float stillHeld);
                    exact &= Mathf.Abs(held - stillHeld) < .0001f;
                    if (enemyLoop)
                    {
                        loopValid = loopState != null && loopState.Clip == settings.cooperative.enemyStruggleClip &&
                            loopState.NormalizedTime != loopBefore && loopState.SharedEvents == null && timelineEvents == 0 &&
                            Vector3.Distance(rootBefore, Rector.transform.position) < .01f &&
                            !Rector.AnimDriver.TryBeginEnemyCooperativeStruggle(session.RequestId + 1, loopState.Clip);
                        int pause = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                        try
                        {
                            float beforePause = loopState != null ? loopState.NormalizedTime : 0f;
                            float meterBefore = session.Meter;
                            Vector3 shakeBefore = CooperativeCameraShake();
                            float windTime = windParticle != null ? windParticle.time : -1f;
                            yield return new WaitForSecondsRealtime(.15f);
                            shakeValid &= CooperativeCameraShake() == shakeBefore;
                            windValid &= windParticle != null && Mathf.Approximately(windParticle.time, windTime);
                            loopValid &= loopState != null && Mathf.Approximately(loopState.NormalizedTime, beforePause) && Mathf.Approximately(session.Meter, meterBefore);
                        }
                        finally { GlobalTimeScaleManager.Instance.ReleasePauseToken(pause); }
                    }
                    if (trial.EndsWith("LoopCancel")) session.Cancel();
                    if (trial.EndsWith("LoopDisable")) Rector.DefensiveBlockAttack.enabled = false;
                    if (!trial.EndsWith("Failure"))
                        for (int press = 0; press < 10 && session.State == CooperativeBlockState.Struggling; press++) session.Press();
                    if (session.State == CooperativeBlockState.Success)
                    {
                        int successPause = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                        Vector3 won = CooperativeCameraShake();
                        try
                        {
                            yield return new WaitForSecondsRealtime(.1f);
                            shakeValid &= CooperativeCameraShake() == won;
                        }
                        finally { GlobalTimeScaleManager.Instance.ReleasePauseToken(successPause); }
                        yield return new WaitForSecondsRealtime(.05f);
                        shakeValid &= CooperativeCameraShake() != won;
                    }
                }
                if (continues)
                {
                    yield return new WaitForSecondsRealtime(.1f);
                    resumed = Rector.AnimBrain.TryGetActiveSkillNormalizedTime(attack.RequestId, out float pose) && pose > .7f;
                }
                deadline = Time.realtimeSinceStartupAsDouble + 6d;
                while ((session?.IsActive == true || Player.DefensiveBlock.IsExecuting || Ally.DefensiveBlock.IsExecuting ||
                    Player.KnockbackMotor.IsActive || Ally.KnockbackMotor.IsActive || !CameraReturned) &&
                    Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                var expected = trial == "CancelPreparing" || trial.EndsWith("LoopCancel") || trial.EndsWith("LoopDisable") ? CooperativeBlockState.Cancelled :
                    trial.EndsWith("Failure") ? CooperativeBlockState.Failure : CooperativeBlockState.Success;
                bool outcome = cooperative ? session?.Outcome == expected : attack.SuccessCount == 1;
                shakeValid &= !cooperative || successShake;
                bool clean = !Player.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.IsExecuting &&
                    !Player.FieldAllyMember.IsReserved && !Ally.FieldAllyMember.IsReserved && CameraReturned &&
                    Player.interruptionCommand.ActiveCooperativeBlock == null;
                if (enemyLoop) clean &= EnemyStruggleState() == null && loopState != null && !loopState.Playable.IsValid();
                clean &= CooperativeCameraShake() == Vector3.zero;
                clean &= CooperativeContactVfx(session) == null && contactEffect == null;
                clean &= CooperativeWind(session) == null && (wind == null || !wind.IsActive);
                int expectedHits = trial.EndsWith("Failure") ? 2 : 0;
                bool pass = accepted == InterruptionCommandResult.Success && outcome && clean && resumed &&
                    (exact || trial == "CancelPreparing") && loopValid && shakeValid && contactVfxValid && windValid && hits == expectedHits && commits == 1 && releases == 1;
                report.AppendLine($"{(pass ? "PASS" : "FAIL")} {trial}: accepted={accepted} exact={exact} loop={loopValid} shake={shakeValid} contactVfx={contactVfxValid} wind={windValid} resumed={resumed} outcome={outcome} hits={hits} costs={commits}/{releases} clean={clean}");
                ValidationReport = report.ToString();
            }
        }
        finally
        {
            chargeSkill = original; autoBlock = savedAuto;
            ResetTrial(); Destroy(trialSkill);
            validation = null;
        }
        Debug.Log("Block Impact PlayMode\n" + ValidationReport);
    }

    Animancer.AnimancerState EnemyStruggleState()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var skillState = typeof(CharacterAnimBrain).GetField("skill", flags).GetValue(Rector.AnimBrain);
        return skillState?.GetType().GetField("_enemyStruggleState", flags).GetValue(skillState) as Animancer.AnimancerState;
    }

    Vector3 CooperativeCameraShake() => LiveCamera == null ? Vector3.zero :
        (Vector3)typeof(GameplayCameraController).GetMethod("SampleCooperativeShake",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(LiveCamera, null);

    static GameObject CooperativeContactVfx(CooperativeBlockSession session) => session == null ? null :
        typeof(CooperativeBlockSession).GetField("contactVfx",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(session) as GameObject;

    static PlayerScreenEffectHandle CooperativeWind(CooperativeBlockSession session) => session == null ? null :
        typeof(CooperativeBlockSession).GetField("struggleWind",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(session) as PlayerScreenEffectHandle;

    static ParticleSystem WindParticle(PlayerScreenEffectHandle wind)
    {
        if (wind == null) return null;
        var instance = typeof(PlayerScreenEffectHandle).GetField("instance",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(wind) as GameObject;
        return instance != null ? instance.GetComponentInChildren<ParticleSystem>() : null;
    }
}
