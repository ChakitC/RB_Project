#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks
{
    using Opsive.GraphDesigner.Runtime.Variables.ECS;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Base class for a boilerplate ECS task.
    /// </summary>
    public abstract class ECSTask<TSystem, TBufferElement, TComponentFlag> : ITreeLogicNode, IAuthoringTask where TBufferElement : unmanaged, IBufferElementData
    {
        [Tooltip("The index of the node.")]
        [SerializeField] ushort m_Index;
        [Tooltip("The parent index of the node. ushort.MaxValue indicates no parent.")]
        [SerializeField] ushort m_ParentIndex;
        [Tooltip("The sibling index of the node. ushort.MaxValue indicates no sibling.")]
        [SerializeField] ushort m_SiblingIndex;
        [Tooltip("Specifies if the node is enabled.")]
        [SerializeField] protected bool m_Enabled = true;

        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public ComponentType Flag => typeof(TComponentFlag);
        /// <summary>
        /// The system type that the component uses.
        /// </summary>
        public System.Type SystemType => typeof(TSystem);

        public ushort Index
        {
            get => m_Index;
            set => m_Index = value;
        }

        public ushort ParentIndex
        {
            get => m_ParentIndex;
            set => m_ParentIndex = value;
        }

        public ushort SiblingIndex
        {
            get => m_SiblingIndex;
            set => m_SiblingIndex = value;
        }
        public bool Enabled { get => m_Enabled; set => m_Enabled = value; }

        public ushort RuntimeIndex { get; set; }

        /// <summary>
        /// Resets the node values back to their default.
        /// </summary>
        public virtual void Reset() { }

        /// <summary>
        /// Adds the IBufferElementData to the entity and registers any SharedVariable fields.
        /// Override this method to register SharedVariable fields before calling base.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="registry">The ECS variable registry for registering SharedVariable fields.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public virtual int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
        {
            DynamicBuffer<TBufferElement> buffer;
            if (world.EntityManager.HasBuffer<TBufferElement>(entity)) {
                buffer = world.EntityManager.GetBuffer<TBufferElement>(entity);
            } else {
                buffer = world.EntityManager.AddBuffer<TBufferElement>(entity);
            }

            buffer.Add(GetBufferElement());
            return buffer.Length - 1;
        }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public abstract TBufferElement GetBufferElement();

        /// <summary>
        /// Clears the IBufferElementData from the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be cleared from.</param>
        public void ClearBufferElement(World world, Entity entity)
        {
            DynamicBuffer<TBufferElement> buffer;
            if (world.EntityManager.HasBuffer<TBufferElement>(entity)) {
                buffer = world.EntityManager.GetBuffer<TBufferElement>(entity);
                buffer.Clear();
            }
        }
    }

    /// <summary>
    /// Base class for an ECS action task.
    /// </summary>
    public abstract class ECSActionTask<TSystem, TBufferElement, TComponentFlag> : ECSTask<TSystem, TBufferElement, TComponentFlag>, IAction where TBufferElement : unmanaged, IBufferElementData { }

    /// <summary>
    /// Base class for an ECS composite task.
    /// </summary>
    public abstract class ECSCompositeTask<TSystem, TBufferElement, TComponentFlag> : ECSTask<TSystem, TBufferElement, TComponentFlag>, IComposite, IParentNode where TBufferElement : unmanaged, IBufferElementData
    {
        /// <summary>
        /// The maximum number of children the node can have.
        /// </summary>
        public virtual ushort MaxChildCount { get => ushort.MaxValue; }
    }

    /// <summary>
    /// Base class for an ECS conditional task.
    /// </summary>
    public abstract class ECSConditionalTask<TSystem, TBufferElement, TComponentFlag> : ECSTask<TSystem, TBufferElement, TComponentFlag>, IConditional where TBufferElement : unmanaged, IBufferElementData { }

    /// <summary>
    /// Base class for an ECS decorator task.
    /// </summary>
    public abstract class ECSDecoratorTask<TSystem, TBufferElement, TComponentFlag> : ECSTask<TSystem, TBufferElement, TComponentFlag>, IDecorator, IParentNode where TBufferElement : unmanaged, IBufferElementData
    {
        /// <summary>
        /// The maximum number of children the node can have.
        /// </summary>
        public ushort MaxChildCount { get => 1; }
    }
}
#endif