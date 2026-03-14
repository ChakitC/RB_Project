#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;
    using UnityEngine;

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder node that represents an action node being created from a template. This node will be replaced with the actual action node after Unity has finished compiling the generated script.")]
    public class PlaceholderActionNode : ActionNode, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder node that represents a conditional node being created from a template. This node will be replaced with the actual conditional node after Unity has finished compiling the generated script.")]
    public class PlaceholderConditionalNode : ConditionalNode, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder task that represents an action task being created from a template. This task will be replaced with the actual action task after Unity has finished compiling the generated script.")]
    public class PlaceholderAction : Action, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder node that represents a composite node being created from a template. This node will be replaced with the actual composite node after Unity has finished compiling the generated script.")]
    public class PlaceholderCompositeNode : CompositeNode, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder task that represents a conditional task being created from a template. This task will be replaced with the actual conditional task after Unity has finished compiling the generated script.")]
    public class PlaceholderConditional : Conditional, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }

    [HideInFilterWindow]
    [Opsive.Shared.Utility.Description("A temporary placeholder node that represents a decorator node being created from a template. This node will be replaced with the actual decorator node after Unity has finished compiling the generated script.")]
    public class PlaceholderDecoratorNode : DecoratorNode, IPlaceholderNode
    {
        [Tooltip("The type name of the target task that will replace this placeholder.")]
        [SerializeField] [HideInInspector] private string m_TargetType;
        public string TargetType { get => m_TargetType; set => m_TargetType = value; }
    }
}
#endif