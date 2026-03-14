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
    /// Template for creating a custom action node.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Action")]
    [Description("Create a new action node.")]
    public class GameObjectActionNode : INodeTemplate
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
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

/// <summary>
/// A custom action node.
/// </summary>
public class {name} : ActionNode
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom action task.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Stacked Action")]
    [Description("Create a new stacked action node.")]
    public class GameObjectAction : INodeTemplate
    {
        public Type BaseType => typeof(IAction);
        public bool IsLogicNode => false;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

/// <summary>
/// A custom action task.
/// </summary>
public class {name} : Action
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom composite node.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Composite")]
    [Description("Create a new composite node.")]
    public class GameObjectCompositeNode : IParentNodeTemplate
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
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;

/// <summary>
/// A custom composite task.
/// </summary>
public class {name} : CompositeNode
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom conditional node.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Conditional")]
    [Description("Create a new conditional node.")]
    public class GameObjectConditionalNode : INodeTemplate
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
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

/// <summary>
/// A custom conditional node.
/// </summary>
public class {name} : ConditionalNode
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom conditional task.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Stacked Conditional")]
    [Description("Create a new stacked conditional node.")]
    public class GameObjectConditional : INodeTemplate
    {
        public Type BaseType => typeof(IConditional);
        public bool IsLogicNode => false;

        /// <summary>
        /// Returns the script that should be used for the template file.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <returns>The node script.</returns>
        public string GetScript(string name)
        {
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

/// <summary>
/// A custom conditional task.
/// </summary>
public class {name} : Conditional
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }

    /// <summary>
    /// Template for creating a custom decorator node.
    /// </summary>
    [Category("GameObjects")]
    [DisplayName("Decorator")]
    [Description("Create a new decorator node.")]
    public class GameObjectDecoratorNode : IParentNodeTemplate
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
            return $@"using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;

/// <summary>
/// A custom decorator task.
/// </summary>
public class {name} : DecoratorNode
{{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {{
        return TaskStatus.Success;
    }}
}}";
        }
    }
}
#endif