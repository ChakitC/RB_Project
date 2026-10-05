using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class PassiveRecipeValidator
{
    public static List<string> Validate(PassiveRecipe recipe)
    {
        var errors = new List<string>();
        if (recipe == null) { errors.Add("Missing recipe; migrate this definition before use."); return errors; }
        try { ValidateContents(recipe, errors); }
        catch (Exception exception) when (exception is NullReferenceException || exception is ArgumentException)
        { errors.Add("Malformed recipe data: " + exception.Message); }
        return errors;
    }
    static void ValidateContents(PassiveRecipe recipe, List<string> errors)
    {
        var variables = VariableIds(recipe.variables, errors);
        var parameters = new Dictionary<string, PassiveParameterKind>();
        foreach (var parameter in recipe.parameters)
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.id) || !parameters.TryAdd(parameter.id, parameter.kind)) errors.Add("Invalid recipe parameter");
        var ids = new HashSet<string>();
        foreach (var rule in recipe.rules)
        {
            if (rule == null) { errors.Add("Null rule"); continue; }
            Id(rule.id, ids, errors);
            if (rule.trigger == PassiveRuleTrigger.BeforeShot &&
                (rule.count == null || rule.count.source != PassiveValueSource.Constant || rule.count.value != 1 ||
                 rule.cooldown == null || rule.cooldown.source != PassiveValueSource.Constant || rule.cooldown.value != 0 ||
                 rule.window == null || rule.window.source != PassiveValueSource.Constant || rule.window.value != 0 ||
                 rule.once != PassiveOnceScope.None || rule.consume != PassiveConsumePolicy.OnAttempt))
                errors.Add(rule.id + ": put consuming gates in ShotCommitted; BeforeShot only reserves charges");
            if (rule.trigger == PassiveRuleTrigger.Combat && rule.eventType == PassiveEventType.None) errors.Add(rule.id + ": missing combat event");
            if (rule.optionalSource != PassiveEventSourceKind.None && string.IsNullOrWhiteSpace(rule.eventSourceId)) errors.Add(rule.id + ": optional source needs an id");
            CheckCondition(rule.condition, errors, new HashSet<PassiveCondition>());
            ScanValues(rule.condition, parameters, variables, errors, new HashSet<object>());
            foreach (var number in new[] { rule.count, rule.window, rule.cooldown, rule.chance })
                if (number == null) errors.Add(rule.id + ": missing gate value"); else ScanValues(number, parameters, variables, errors, new HashSet<object>());
            Steps(rule.steps, new HashSet<PassiveFragment>(), new HashSet<string>(), parameters, variables, errors, rule.trigger);
        }
    }
    static HashSet<string> VariableIds(List<PassiveVariable> list, List<string> errors)
    {
        var ids = new HashSet<string>();
        foreach (var item in list)
        {
            if (item == null) { errors.Add("Null variable"); continue; }
            Id(item.id, ids, errors);
            if (!float.IsFinite(item.maximum) || !float.IsFinite(item.initial) || item.maximum < 0 || item.initial < 0 || item.initial > item.maximum) errors.Add(item.id + ": invalid variable limits");
        }
        return ids;
    }
    static void Id(string id, HashSet<string> ids, List<string> errors)
    { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("Missing or duplicate id: " + id); }
    static void Steps(List<PassiveStep> steps, HashSet<PassiveFragment> stack, HashSet<string> ids,
        Dictionary<string, PassiveParameterKind> parameters, HashSet<string> variables, List<string> errors, PassiveRuleTrigger trigger, bool reachable = true)
    {
        if (steps == null) { errors.Add("Missing steps"); return; }
        foreach (var step in steps)
        {
            if (step == null) { errors.Add("Null step"); continue; }
            Id(step.id, ids, errors);
            if (step is PassiveFragmentStep call)
            {
                if (call.fragment == null) { errors.Add(call.id + ": missing fragment"); continue; }
                if (!stack.Add(call.fragment)) { errors.Add(call.id + ": fragment cycle"); continue; }
                var declared = new Dictionary<string, PassiveParameterKind>();
                foreach (var p in call.fragment.parameters)
                {
                    if (p == null || string.IsNullOrEmpty(p.id) || !declared.TryAdd(p.id, p.kind)) errors.Add(call.id + ": duplicate parameter");
                    else if (p.kind == PassiveParameterKind.Number && !float.IsFinite(p.number)) errors.Add(call.id + ": invalid numeric parameter");
                }
                var overridden = new HashSet<string>();
                foreach (var p in call.overrides)
                    if (p == null || !overridden.Add(p.id) || !declared.TryGetValue(p.id, out var kind) || kind != p.kind) errors.Add(call.id + ": invalid parameter override");
                var locals = VariableIds(call.fragment.variables, errors);
                foreach (var binding in call.parameterBindings)
                    if (binding == null || !overridden.Add(binding.local) || !declared.TryGetValue(binding.local, out var localKind) ||
                        parameters == null || !parameters.TryGetValue(binding.parent, out var parentKind) || localKind != parentKind)
                        errors.Add(call.id + ": invalid parameter binding");
                var bound = new HashSet<string>();
                foreach (var binding in call.bindings)
                    if (binding == null || !bound.Add(binding.local) || !locals.Contains(binding.local) || !variables.Contains(binding.parent)) errors.Add(call.id + ": invalid variable binding");
                Steps(call.fragment.steps, stack, new HashSet<string>(), declared, locals, errors, trigger, reachable);
                stack.Remove(call.fragment); continue;
            }
            if (step is PassiveBranchStep branch)
            {
                CheckCondition(branch.condition, errors, new HashSet<PassiveCondition>());
                ScanValues(branch.condition, parameters, variables, errors, new HashSet<object>());
                bool trueReachable = reachable, falseReachable = reachable;
                if (trigger != PassiveRuleTrigger.Combat && branch.condition.kind == PassiveConditionKind.EventType)
                {
                    var type = trigger == PassiveRuleTrigger.Equipped ? PassiveEventType.None : trigger == PassiveRuleTrigger.ResourceSpent ? PassiveEventType.ResourceSpent : PassiveEventType.ResourceChanged;
                    trueReachable &= branch.condition.eventType == type;
                    falseReachable &= branch.condition.eventType != type;
                }
                Steps(branch.whenTrue, stack, ids, parameters, variables, errors, trigger, trueReachable);
                Steps(branch.whenFalse, stack, ids, parameters, variables, errors, trigger, falseReachable);
                continue;
            }
            if (step is PassiveModifierStep { whileEquipped: true } modifier && modifier.target.mode != PassiveTargetMode.Self) errors.Add(step.id + ": equipped modifier must target owner");
            if (step is PassiveDynamicModifierStep dynamic) CheckCondition(dynamic.condition, errors, new HashSet<PassiveCondition>());
            if (step is PassivePickupStep pickup && pickup.pickupPrefab == null) errors.Add(step.id + ": missing pickup prefab");
            if (step is PassiveAccumulatorStep accumulator && !variables.Contains(accumulator.variable)) errors.Add(step.id + ": undeclared accumulator");
            if (step is PassiveReserveVariableStep reserve && (!variables.Contains(reserve.variable) || trigger != PassiveRuleTrigger.BeforeShot)) errors.Add(step.id + ": invalid reservation");
            if (step is PassiveExpiryStep expiry) foreach (var variable in expiry.variables) if (!variables.Contains(variable)) errors.Add(step.id + ": undeclared expiry variable");
            if (step is PassiveTrackTargetStep track && !variables.Contains(track.resetVariable)) errors.Add(step.id + ": undeclared target variable");
            if (trigger == PassiveRuleTrigger.BeforeShot && step is not PassiveShotModifierStep && step is not PassiveReserveVariableStep)
                errors.Add(step.id + ": BeforeShot allows only reversible shot patches and reservations");
            if (step is PassiveProjectileStep && trigger != PassiveRuleTrigger.ShotCommitted) errors.Add(step.id + ": projectile requires ShotCommitted");
            if (step is PassiveNextAttackStep attack && !variables.Contains(attack.variable)) errors.Add(step.id + ": undeclared next-attack variable");
            if (step is PassiveConversionStep conversion && (conversion.source != StatType.StaminaRegenRate || conversion.destination != StatType.MaxStamina))
                errors.Add(step.id + ": only StaminaRegenRate -> MaxStamina conversion is supported; chained conversions are forbidden");
            if ((step is PassiveConversionStep || step is PassiveNextAttackStep || step is PassiveDynamicModifierStep || step is PassiveModifierStep { whileEquipped: true }) && trigger != PassiveRuleTrigger.Equipped && reachable)
                errors.Add(step.id + ": persistent registration requires Equipped trigger");
            if (step is PassiveStatusStep status && string.IsNullOrEmpty(status.statusParameter) && status.spec?.effect == null) errors.Add(step.id + ": missing status");
            if (step is PassiveStatusStep ps && !string.IsNullOrEmpty(ps.statusParameter) && (parameters == null || !parameters.TryGetValue(ps.statusParameter, out var pk) || pk != PassiveParameterKind.Status)) errors.Add(step.id + ": unknown status parameter");
            ScanValues(step, parameters, variables, errors, new HashSet<object>());
        }
    }
    static void CheckCondition(PassiveCondition condition, List<string> errors, HashSet<PassiveCondition> stack)
    {
        if (condition == null) { errors.Add("Missing condition"); return; }
        if (!stack.Add(condition)) { errors.Add("Condition cycle"); return; }
        if (condition.kind == PassiveConditionKind.HasStatus && condition.status == null) errors.Add("Missing condition status");
        if (condition.kind == PassiveConditionKind.Not && condition.children.Count != 1) errors.Add("Not requires exactly one child");
        foreach (var child in condition.children) CheckCondition(child, errors, stack);
        stack.Remove(condition);
    }
    // Validation only; execution never reflects serialized fields.
    static void ScanValues(object value, Dictionary<string, PassiveParameterKind> parameters, HashSet<string> variables, List<string> errors, HashSet<object> visited)
    {
        if (value == null || value is UnityEngine.Object || value is string || value.GetType().IsValueType || !visited.Add(value)) return;
        if (value is PassiveNumber number)
        {
            if (!string.IsNullOrEmpty(number.multiplyParameter) && (parameters == null || !parameters.TryGetValue(number.multiplyParameter, out var multiplierKind) || multiplierKind != PassiveParameterKind.Number)) errors.Add("Unknown multiplier parameter");
            if (!string.IsNullOrEmpty(number.multiplyVariable) && !variables.Contains(number.multiplyVariable)) errors.Add("Unknown multiplier variable");
            if (number.source == PassiveValueSource.Parameter && (parameters == null || !parameters.TryGetValue(number.reference ?? "", out var kind) || kind != PassiveParameterKind.Number)) errors.Add("Unknown number parameter: " + number.reference);
            if (number.source == PassiveValueSource.Variable && !variables.Contains(number.reference ?? "")) errors.Add("Unknown variable: " + number.reference);
            if (!float.IsFinite(number.value) || !float.IsFinite(number.multiply) || !float.IsFinite(number.add) || !float.IsFinite(number.minimum) || !float.IsFinite(number.maximum) || number.minimum > number.maximum) errors.Add("Invalid numeric value");
        }
        if (value is IEnumerable list) { foreach (var item in list) ScanValues(item, parameters, variables, errors, visited); return; }
        foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var entry = field.GetValue(value);
            if (entry == null && (field.FieldType == typeof(PassiveNumber) || field.FieldType == typeof(PassiveTarget) || field.FieldType == typeof(StatusApplicationSpec) || typeof(IList).IsAssignableFrom(field.FieldType)))
                errors.Add("Missing required field: " + field.Name);
            ScanValues(entry, parameters, variables, errors, visited);
        }
    }
    public static void CollectUpgradeIds(PassiveRecipe recipe, List<string> ids)
    { if (recipe != null) Collect(recipe.rules, ids, new HashSet<object>()); }
    static void Collect(object value, List<string> ids, HashSet<object> visited)
    {
        if (value == null || value is string || value.GetType().IsValueType || !visited.Add(value)) return;
        if (value is PassivePickupStep pickup)
        {
            if (!string.IsNullOrWhiteSpace(pickup.extraDropUpgradeId)) ids.Add(pickup.extraDropUpgradeId);
            foreach (var extra in pickup.upgradeEffects) if (!string.IsNullOrWhiteSpace(extra.requiredUpgradeId)) ids.Add(extra.requiredUpgradeId);
        }
        if (value is PassiveNumericUpgrade upgrade && !string.IsNullOrWhiteSpace(upgrade.upgradeId)) ids.Add(upgrade.upgradeId);
        if (value is PassiveCondition { kind: PassiveConditionKind.HasUpgrade } condition && !string.IsNullOrWhiteSpace(condition.id)) ids.Add(condition.id);
        if (value is IEnumerable list) { foreach (var item in list) Collect(item, ids, visited); return; }
        if (value is UnityEngine.Object && value is not PassiveFragment) return;
        foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) Collect(field.GetValue(value), ids, visited);
    }
}
