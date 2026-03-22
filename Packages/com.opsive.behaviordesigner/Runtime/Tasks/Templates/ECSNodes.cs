#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;
    using System;

    /// <summary>
    /// Helper class for ECS node templates.
    /// </summary>
    public static class ECSNodeUtility
    {
        /// <summary>
        /// Converts a name to camelCase format.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>The name in camelCase format.</returns>
        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }

    /// <summary>
    /// Template for creating a custom action node.
    /// </summary>
    [Category("ECS")]
    [DisplayName("Action")]
    [Description("Create a new ECS action node.")]
    public class ECSActionNode : INodeTemplate
    {
        public Type BaseType => typeof(IAction);
        public bool IsLogicNode => true;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            var variableName = ECSNodeUtility.ToCamelCase(name);
            return $@"using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Burst;
using UnityEngine;

/// <summary>
/// A custom ECS action node.
/// </summary>
public class {name} : ECSActionTask<{name}TaskSystem, {name}Component>
{{
    /// <summary>
    /// The type of flag that should be enabled when the task is running.
    /// </summary>
    public override ComponentType Flag {{ get => typeof({name}Flag); }}

    /// <summary>
    /// Returns a new {name}Component for use by the system.
    /// </summary>
    /// <returns>A new {name}Component for use by the system.</returns>
    public override {name}Component GetBufferElement()
    {{
        return new {name}Component() {{
            Index = RuntimeIndex,
        }};
    }}
}}

/// <summary>
/// The DOTS data structure for the {name} class.
/// </summary>
public struct {name}Component : IBufferElementData
{{
    [Tooltip(""The index of the node."")]
    public ushort Index;
}}

/// <summary>
/// A DOTS flag indicating when a {name} node is active.
/// </summary>
public struct {name}Flag : IComponentData, IEnableableComponent {{ }}

/// <summary>
/// Runs the {name} logic.
/// </summary>
[DisableAutoCreation]
public partial struct {name}TaskSystem : ISystem
{{
    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name=""state"">The current state of the system.</param>
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {{
        var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<{name}Component>().WithAll<{name}Flag, EvaluateFlag>().Build();
        state.Dependency = new {name}Job().ScheduleParallel(query, state.Dependency);
    }}

    /// <summary>
    /// Job which executes the task logic.
    /// </summary>
    [BurstCompile]
    private partial struct {name}Job : IJobEntity
    {{
        /// <summary>
        /// Executes the {name} logic.
        /// </summary>
        /// <param name=""branchComponents"">An array of BranchComponents.</param>
        /// <param name=""taskComponents"">An array of TaskComponents.</param>
        /// <param name=""{variableName}Components"">An array of {name}Components.</param>
        [BurstCompile]
        public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<{name}Component> {variableName}Components)
        {{
            for (int i = 0; i < {variableName}Components.Length; ++i) {{
                var {variableName}Component = {variableName}Components[i];
                var taskComponent = taskComponents[{variableName}Component.Index];
                var branchComponent = branchComponents[taskComponent.BranchIndex];
                if (!branchComponent.CanExecute) {{
                    continue;
                }}

                if (taskComponent.Status == TaskStatus.Queued) {{
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[{variableName}Component.Index] = taskComponent;
                }}
            }}
        }}
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom composite node.
    /// </summary>
    [Category("ECS")]
    [DisplayName("Composite")]
    [Description("Create a new ECS composite node.")]
    public class ECSCompositeNode : IParentNodeTemplate
    {
        public Type BaseType => typeof(IComposite);
        public bool IsLogicNode => true;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            var variableName = ECSNodeUtility.ToCamelCase(name);
            return $@"using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Burst;
using UnityEngine;

/// <summary>
/// A custom ECS composite node.
/// </summary>
public class {name} : ECSCompositeTask<{name}TaskSystem, {name}Component>
{{
    /// <summary>
    /// The type of tag that should be enabled when the task is running.
    /// </summary>
    public override ComponentType Flag {{ get => typeof({name}Flag); }}

    /// <summary>
    /// Returns a new {name}Component for use by the system.
    /// </summary>
    /// <returns>A new {name}Component for use by the system.</returns>
    public override {name}Component GetBufferElement()
    {{
        return new {name}Component() {{
            Index = RuntimeIndex,
        }};
    }}
}}

/// <summary>
/// The DOTS data structure for the {name} class.
/// </summary>
public struct {name}Component : IBufferElementData
{{
    [Tooltip(""The index of the node."")]
    public ushort Index;
    [Tooltip(""The index of the child that is currently active."")]
    public ushort ActiveChildIndex;
}}

/// <summary>
/// A DOTS flag indicating when a {name} node is active.
/// </summary>
public struct {name}Flag : IComponentData, IEnableableComponent {{ }}

/// <summary>
/// Runs the {name} logic.
/// </summary>
[DisableAutoCreation]
public partial struct {name}TaskSystem : ISystem
{{
    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name=""state"">The current state of the system.</param>
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {{
        var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<{name}Component>().WithAll<{name}Flag, EvaluateFlag>().Build();
        state.Dependency = new {name}Job().ScheduleParallel(query, state.Dependency);
    }}

    /// <summary>
    /// Job which executes the task logic.
    /// </summary>
    [BurstCompile]
    private partial struct {name}Job : IJobEntity
    {{
        /// <summary>
        /// Executes the {name} logic.
        /// </summary>
        /// <param name=""branchComponents"">An array of BranchComponents.</param>
        /// <param name=""taskComponents"">An array of TaskComponents.</param>
        /// <param name=""{variableName}Components"">An array of {name}Components.</param>
        [BurstCompile]
        public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<{name}Component> {variableName}Components)
        {{
            for (int i = 0; i < {variableName}Components.Length; ++i) {{
                var {variableName}Component = {variableName}Components[i];
                var taskComponent = taskComponents[{variableName}Component.Index];
                var branchComponent = branchComponents[taskComponent.BranchIndex];
                if (!branchComponent.CanExecute) {{
                    continue;
                }}

                if (taskComponent.Status == TaskStatus.Queued) {{
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[{variableName}Component.Index] = taskComponent;

                    branchComponent.NextIndex = taskComponent.ParentIndex;
                    branchComponents[taskComponent.BranchIndex] = branchComponent;
                }}
            }}
        }}
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom conditional node.
    /// </summary>
    [Category("ECS")]
    [DisplayName("Conditional")]
    [Description("Create a new ECS conditional node.")]
    public class ECSConditionalNode : INodeTemplate
    {
        public Type BaseType => typeof(IConditional);
        public bool IsLogicNode => true;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            var variableName = ECSNodeUtility.ToCamelCase(name);
            return $@"using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// A custom ECS conditional node.
/// </summary>
public class {name} : ECSConditionalTask<{name}TaskSystem, {name}Component>, IReevaluateResponder
{{
    /// <summary>
    /// The type of flag that should be enabled when the task is running.
    /// </summary>
    public override ComponentType Flag {{ get => typeof({name}Flag); }}
    /// <summary>
    /// The type of flag that should be enabled when the task is being reevaluated.
    /// </summary>
    public ComponentType ReevaluateFlag {{ get => typeof({name}ReevaluateFlag); }}
    /// <summary>
    /// The system type that the reevaluation component uses.
    /// </summary>
    public System.Type ReevaluateSystemType {{ get => typeof({name}ReevaluateTaskSystem); }}

    /// <summary>
    /// Returns a new {name}Component for use by the system.
    /// </summary>
    /// <returns>A new {name}Component for use by the system.</returns>
    public override {name}Component GetBufferElement()
    {{
        return new {name}Component() {{
            Index = RuntimeIndex,
        }};
    }}
}}

/// <summary>
/// The DOTS data structure for the {name} class.
/// </summary>
public struct {name}Component : IBufferElementData
{{
    [Tooltip(""The index of the node."")]
    public ushort Index;
}}

/// <summary>
/// A DOTS flag indicating when a {name} node is active.
/// </summary>
public struct {name}Flag : IComponentData, IEnableableComponent {{ }}

/// <summary>
/// Runs the {name} logic.
/// </summary>
[DisableAutoCreation]
public partial struct {name}TaskSystem : ISystem
{{
    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name=""state"">The current state of the system.</param>
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {{
        var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<{name}Component>().WithAll<{name}Flag, EvaluateFlag>().Build();
        state.Dependency = new {name}Job().ScheduleParallel(query, state.Dependency);
    }}

    /// <summary>
    /// Job which executes the task logic.
    /// </summary>
    [BurstCompile]
    private partial struct {name}Job : IJobEntity
    {{
        /// <summary>
        /// Executes the {name} logic.
        /// </summary>
        /// <param name=""branchComponents"">An array of BranchComponents.</param>
        /// <param name=""taskComponents"">An array of TaskComponents.</param>
        /// <param name=""{variableName}Components"">An array of {name}Components.</param>
        [BurstCompile]
        public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<{name}Component> {variableName}Components)
        {{
            for (int i = 0; i < {variableName}Components.Length; ++i) {{
                var {variableName}Component = {variableName}Components[i];
                var taskComponent = taskComponents[{variableName}Component.Index];
                var branchComponent = branchComponents[taskComponent.BranchIndex];
                if (!branchComponent.CanExecute) {{
                    continue;
                }}

                if (taskComponent.Status == TaskStatus.Queued || taskComponent.Status == TaskStatus.Running) {{ // Conditional aborts can set the status to Running.
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[{variableName}Component.Index] = taskComponent;
                }}
            }}
        }}
    }}
}}

/// <summary>
/// A DOTS flag indicating when an {name} node needs to be reevaluated.
/// </summary>
public struct {name}ReevaluateFlag : IComponentData, IEnableableComponent {{ }}

/// <summary>
/// Runs the {name} reevaluation logic.
/// </summary>
[DisableAutoCreation]
public partial struct {name}ReevaluateTaskSystem : ISystem
{{
    /// <summary>
    /// Updates the reevaluation logic.
    /// </summary>
    /// <param name=""state"">The current state of the system.</param>
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {{
        foreach (var (branchComponents, taskComponents, {variableName}Components) in
            SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<{name}Component>>().WithAll<{name}ReevaluateFlag, EvaluateFlag>()) {{
            for (int i = 0; i < {variableName}Components.Length; ++i) {{
                var {variableName}Component = {variableName}Components[i];
                var taskComponent = taskComponents[{variableName}Component.Index];
                var branchComponent = branchComponents[taskComponent.BranchIndex];
                if (!branchComponent.CanExecute) {{
                    continue;
                }}

                if (!taskComponent.Reevaluate) {{
                    continue;
                }}

                var status = TaskStatus.Success;
                if (status != taskComponent.Status) {{
                    taskComponent.Status = status;
                    var buffer = taskComponents;
                    buffer[taskComponent.Index] = taskComponent;
                }}
            }}
        }}
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom decorator node.
    /// </summary>
    [Category("ECS")]
    [DisplayName("Decorator")]
    [Description("Create a new ECS decorator node.")]
    public class ECSDecoratorNode : IParentNodeTemplate
    {
        public Type BaseType => typeof(IDecorator);
        public bool IsLogicNode => true;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            var variableName = ECSNodeUtility.ToCamelCase(name);
            return $@"using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Burst;
using UnityEngine;

/// <summary>
/// A custom ECS decorator node.
/// </summary>
public class {name} : ECSDecoratorTask<{name}TaskSystem, {name}Component>
{{
    /// <summary>
    /// The type of flag that should be enabled when the task is running.
    /// </summary>
    public override ComponentType Flag {{ get => typeof({name}Flag); }}

    /// <summary>
    /// Returns a new {name}Component for use by the system.
    /// </summary>
    /// <returns>A new {name}Component for use by the system.</returns>
    public override {name}Component GetBufferElement()
    {{
        return new {name}Component() {{
            Index = RuntimeIndex,
        }};
    }}
}}

/// <summary>
/// The DOTS data structure for the {name} class.
/// </summary>
public struct {name}Component : IBufferElementData
{{
    [Tooltip(""The index of the node."")]
    public ushort Index;
}}

/// <summary>
/// A DOTS flag indicating when a {name} node is active.
/// </summary>
public struct {name}Flag : IComponentData, IEnableableComponent {{ }}

/// <summary>
/// Runs the {name} logic.
/// </summary>
[DisableAutoCreation]
public partial struct {name}TaskSystem : ISystem
{{
    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name=""state"">The current state of the system.</param>
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {{
        var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<{name}Component>().WithAll<{name}Flag, EvaluateFlag>().Build();
        state.Dependency = new {name}Job().ScheduleParallel(query, state.Dependency);
    }}

    /// <summary>
    /// Job which executes the task logic.
    /// </summary>
    [BurstCompile]
    private partial struct {name}Job : IJobEntity
    {{
        /// <summary>
        /// Executes the {name} logic.
        /// </summary>
        /// <param name=""branchComponents"">An array of BranchComponents.</param>
        /// <param name=""taskComponents"">An array of TaskComponents.</param>
        /// <param name=""{variableName}Components"">An array of {name}Components.</param>
        [BurstCompile]
        public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<{name}Component> {variableName}Components)
        {{
            for (int i = 0; i < {variableName}Components.Length; ++i) {{
                var {variableName}Component = {variableName}Components[i];
                var taskComponent = taskComponents[{variableName}Component.Index];
                var branchComponent = branchComponents[taskComponent.BranchIndex];
                if (!branchComponent.CanExecute) {{
                    continue;
                }}

                if (taskComponent.Status == TaskStatus.Queued) {{
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[{variableName}Component.Index] = taskComponent;
                }}
            }}
        }}
    }}
}}";
        }
    }
}
#endif