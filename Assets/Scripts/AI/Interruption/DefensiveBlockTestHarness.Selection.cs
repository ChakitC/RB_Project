using System;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    [Tooltip("Scene-local choices. Refresh from Tools/RB/Defensive Block/Create or Upgrade Test Scene. Custom entries can be added here.")]
    public DefensiveBlockTestEnemy[] testEnemies = Array.Empty<DefensiveBlockTestEnemy>();
    enum Picker { None, Enemy, Skill }
    Picker picker;
    string selectionSearch = "";
    Vector2 selectionScroll, panelScroll;
    int autoWindow = -1;

    public DefensiveBlockTestEnemy SelectedEnemy => Array.Find(testEnemies ?? Array.Empty<DefensiveBlockTestEnemy>(),
        entry => entry != null && entry.prefab == rectorPrefab);
    public bool IsResetting => resetting;
    public string SelectedEnemyName => SelectedEnemy?.DisplayName ?? (rectorPrefab != null ? rectorPrefab.name : "Select enemy");
    static string SkillLabel(SkillGemDefinition skill) => skill != null ? skill.SkillDefinitionDisplayName : "Select skill";

    public bool SelectEnemy(int index)
    {
        if (validation != null || testEnemies == null || index < 0 || index >= testEnemies.Length) return false;
        var entry = testEnemies[index];
        if (entry?.prefab == null || entry.prefab.GetComponentInChildren<EnemyContext>(true) == null) return false;
        rectorPrefab = entry.prefab;
        var skills = entry.skills ?? Array.Empty<SkillGemDefinition>();
        if (Array.IndexOf(skills, chargeSkill) < 0 || chargeSkill == null)
            chargeSkill = Array.Find(skills, skill => skill != null && !skill.IsCombo && skill.defensiveBlock != null)
                ?? Array.Find(skills, skill => skill != null && !skill.IsCombo);
        ClosePicker();
        if (Application.isPlaying) ResetTrial();
        return true;
    }

    public bool SelectSkill(SkillGemDefinition skill)
    {
        if (validation != null || skill == null || skill.IsCombo ||
            Array.IndexOf(SelectedEnemy?.skills ?? Array.Empty<SkillGemDefinition>(), skill) < 0) return false;
        chargeSkill = skill;
        ClosePicker();
        if (Application.isPlaying) ResetTrial();
        return true;
    }

    void ClosePicker() { picker = Picker.None; selectionSearch = ""; selectionScroll = Vector2.zero; if (Event.current != null) GUI.FocusControl(null); }
    void TogglePicker(Picker next) { bool close = picker == next; ClosePicker(); if (!close) picker = next; }

    void DrawSelection()
    {
        if (GUILayout.Button("Enemy: " + SelectedEnemyName)) TogglePicker(Picker.Enemy);
        if (GUILayout.Button("Skill: " + SkillLabel(chargeSkill))) TogglePicker(Picker.Skill);
        if (picker == Picker.None) return;
        GUILayout.BeginVertical(GUI.skin.box);
        selectionSearch = GUILayout.TextField(selectionSearch);
        selectionScroll = GUILayout.BeginScrollView(selectionScroll, GUILayout.Height(145));
        if (picker == Picker.Enemy)
        {
            for (int i = 0; i < (testEnemies?.Length ?? 0); i++)
            {
                var entry = testEnemies[i];
                if (entry?.prefab == null || !MatchesSearch(entry.DisplayName)) continue;
                if (GUILayout.Button(entry.DisplayName)) { SelectEnemy(i); break; }
            }
        }
        else
        {
            foreach (var skill in SelectedEnemy?.skills ?? Array.Empty<SkillGemDefinition>())
            {
                if (skill == null || !MatchesSearch(SkillLabel(skill))) continue;
                string suffix = skill.defensiveBlock == null ? " [Block off]" : " [" + skill.defensiveBlock.mode + "]";
                if (GUILayout.Button(SkillLabel(skill) + suffix)) { SelectSkill(skill); break; }
            }
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Close")) ClosePicker();
        GUILayout.EndVertical();
    }

    bool MatchesSearch(string value) => string.IsNullOrWhiteSpace(selectionSearch) ||
        (value ?? "").IndexOf(selectionSearch, StringComparison.OrdinalIgnoreCase) >= 0;

    void DrawTrialPanel()
    {
        GUILayout.BeginArea(new Rect(15, 15, Mathf.Min(460, Screen.width - 30), Mathf.Min(620, Screen.height - 30)), GUI.skin.box);
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("DEFENSIVE BLOCK TEST");
        if (GUILayout.Button(TestControlsOpen ? "F1: Resume character control" : "F1: Release mouse for test controls"))
            SetTestControlsOpen(!TestControlsOpen);
        GUILayout.Label("C: Cast skill    Space: Block    Shift: Dash    R: Reset");
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && validation == null && TestControlsOpen;
        DrawSelection();
        GUILayout.Label($"Start distance: {startDistance:0.##} m");
        GUILayout.BeginHorizontal();
        foreach (float distance in new[] { 1.5f, 2f, 4f, 6f, 8f, 10f })
            if (GUILayout.Button($"{distance} m")) { startDistance = distance; ResetTrial(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUI.enabled = wasEnabled && validation == null && TestControlsOpen && !resetting && chargeSkill != null && Rector != null;
        if (GUILayout.Button("Cast Skill")) StartCharge();
        if (GUILayout.Button("Block")) RequestBlock();
        GUI.enabled = wasEnabled && validation == null && TestControlsOpen;
        if (GUILayout.Button("Reset")) ResetTrial();
        GUILayout.EndHorizontal();
        autoBlock = GUILayout.Toggle(autoBlock, "Auto block when ready");
        logDefensiveBlock = GUILayout.Toggle(logDefensiveBlock, "Log Defensive Block");
        if (logDefensiveBlock && GUILayout.Button("Open Block Log Folder")) DefensiveBlockDiagnostics.OpenFolder();
        if (logDefensiveBlock && GUILayout.Button("Capture Block State") && Player != null)
            Player.interruptionCommand?.CaptureDefensiveBlockState();
        bool paused = GUILayout.Toggle(pauseAutomaticCombat, "Pause enemy / party AI");
        if (paused != pauseAutomaticCombat) { pauseAutomaticCombat = paused; ResetTrial(); }
        GUI.enabled = wasEnabled;
        var settings = chargeSkill != null ? chargeSkill.defensiveBlock : null;
        GUILayout.Label(settings == null ? "Block is disabled on this Skill. Save its Block settings in Timeline." :
            $"Block: {settings.mode}" + (settings.UsesTimedApproach ? $" / {settings.timedApproachSeconds:0.###} s / stand-off {settings.approachStandOff:0.##} m" : ""));
        GUILayout.Label(Status);
        if (!resetting && Rector != null && Player != null)
        {
            var attack = Rector.DefensiveBlockAttack;
            if (attack == null) GUILayout.Label("Enemy is missing DefensiveBlockAttack.");
            else
            {
                GUILayout.Label($"Window: {attack.WindowOpen} | Ready: {attack.CanRequestBlock(Player)} | Successes: {attack.SuccessCount}");
                GUILayout.Label("Block result: " + attack.LastResult);
                GUILayout.Label("Contact: " + attack.LastProbe);
            }
            GUILayout.Label($"{Player.baseStats?.characterName ?? "Player"} HP: {Player.HealthSystem?.currentHealth:0} | {Ally?.baseStats?.characterName ?? "Ally"} HP: {Ally?.HealthSystem?.currentHealth:0}");
            GUILayout.Label($"Guard: {Ally?.AnimBrain?.BlockPhase} | Enemy knockback: {Rector.KnockbackMotor?.IsActive}");
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
