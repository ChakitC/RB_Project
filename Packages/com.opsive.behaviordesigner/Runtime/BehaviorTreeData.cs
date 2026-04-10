#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Events;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Storage class for the graph data.
    /// </summary>
    [System.Serializable]
    public partial class BehaviorTreeData
    {
        [Tooltip("The serialized Task data.")]
        [SerializeField] private Serialization[] m_TaskData;
        [Tooltip("The serialized EventTask data.")]
        [SerializeField] private Serialization[] m_EventTaskData;
        [Tooltip("The serialized SharedVariable data.")]
        [SerializeField] private Serialization[] m_SharedVariableData;
        [Tooltip("The serialized disabled event nodes data.")]
        [SerializeField] private Serialization[] m_DisabledEventNodesData;
        [Tooltip("The serialized disabled logic nodes data.")]
        [SerializeField] private Serialization[] m_DisabledLogicNodesData;
        [Tooltip("The unique ID of the data.")]
        [SerializeField] private int m_UniqueID;

        private ITreeLogicNode[] m_Tasks;
        private IEventNode[] m_EventTasks;
        private SharedVariable[] m_SharedVariables;
        private ushort[] m_DisabledLogicNodes;
        private ushort[] m_DisabledEventNodes;
        private Dictionary<VariableAssignment, SharedVariable> m_VariableByNameMap;
        private int m_RuntimeUniqueID;
        [System.NonSerialized] private ResizableArray<InjectedGraphReference> m_InjectedGraphReferences;
        [System.NonSerialized] private HashSet<IEventNode> m_InjectedSubtreeEventNodes;

        public ITreeLogicNode[] LogicNodes
        {
            get => m_Tasks;
            set {
                if (value == null) {
                    m_Tasks = null;
                } else {
                    if (m_Tasks == null) {
                        m_Tasks = new ITreeLogicNode[value.Length];
                    } else if (m_Tasks.Length != value.Length) {
                        Array.Resize(ref m_Tasks, value.Length);
                    }
                    for (int i = 0; i < value.Length; ++i) {
                        m_Tasks[i] = value[i];
                    }
                }
            }
        }
        public IEventNode[] EventNodes
        {
            get => m_EventTasks;
            set {
                if (value == null) {
                    m_EventTasks = null;
                } else {
                    if (m_EventTasks == null) {
                        m_EventTasks = new IEventNode[value.Length];
                    } else if (m_EventTasks.Length != value.Length) {
                        Array.Resize(ref m_EventTasks, value.Length);
                    }
                    for (int i = 0; i < value.Length; ++i) {
                        m_EventTasks[i] = value[i];
                    }
                }
            }
        }
        public SharedVariable[] SharedVariables { get => m_SharedVariables; set => m_SharedVariables = value; }
        public int UniqueID { get => m_UniqueID; }
        public int RuntimeUniqueID { get => m_RuntimeUniqueID; internal set => m_RuntimeUniqueID = value; }
        public ushort[] DisabledLogicNodes { get => m_DisabledLogicNodes; set => m_DisabledLogicNodes = value; }
        public ushort[] DisabledEventNodes { get => m_DisabledEventNodes; set => m_DisabledEventNodes = value; }
        internal Dictionary<VariableAssignment, SharedVariable> VariableByNameMap { get => m_VariableByNameMap; set => m_VariableByNameMap = value; }
        internal ResizableArray<InjectedGraphReference> InjectedGraphReferences { get => m_InjectedGraphReferences; }

#if UNITY_EDITOR
        [Tooltip("The serialized logic node properties data.")]
        [SerializeField] private Serialization[] m_LogicNodePropertiesData;
        [Tooltip("The serialized event node properties data.")]
        [SerializeField] private Serialization[] m_EventNodePropertiesData;
        [Tooltip("The serialized group properties data.")]
        [SerializeField] private Serialization[] m_GroupPropertiesData;
        [Tooltip("The serialized shared variables group data.")]
        [SerializeField] private Serialization[] m_SharedVariableGroupsData;

        private LogicNodeProperties[] m_LogicNodeProperties;
        private NodeProperties[] m_EventNodeProperties;
        private GroupProperties[] m_GroupProperties;
        [System.NonSerialized] private SharedVariableGroup[] m_SharedVariableGroups;

        public LogicNodeProperties[] LogicNodeProperties { get => m_LogicNodeProperties; set { m_LogicNodeProperties = value; } }
        public NodeProperties[] EventNodeProperties { get => m_EventNodeProperties; set { m_EventNodeProperties = value; } }
        public GroupProperties[] GroupProperties { get => m_GroupProperties; set => m_GroupProperties = value; }
        public SharedVariableGroup[] SharedVariableGroups { get => m_SharedVariableGroups; set => m_SharedVariableGroups = value;  }
#endif

        private ResizableArray<InjectedSubtreeReference> m_InjectedSubtreeReference;
        private ResizableArray<VariableField> m_VariableFields;
        [System.NonSerialized] private bool m_Deserializing;

        internal ResizableArray<InjectedSubtreeReference> InjectedSubtreeReferences { get => m_InjectedSubtreeReference; set => m_InjectedSubtreeReference = value; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BehaviorTreeData()
        {
            m_UniqueID = Guid.NewGuid().GetHashCode();
        }

        /// <summary>
        /// Adds the specified node.
        /// </summary>
        /// <param name="node">The node that should be added.</param>
        public void AddNode(ITreeLogicNode node)
        {
            if (m_Tasks == null) {
                m_Tasks = new ITreeLogicNode[1];
            } else {
                Array.Resize(ref m_Tasks, m_Tasks.Length + 1);
            }
            node.Index = (ushort)(m_Tasks.Length - 1);
            node.ParentIndex = ushort.MaxValue;
            node.SiblingIndex = ushort.MaxValue;
            node.RuntimeIndex = ushort.MaxValue;
            m_Tasks[m_Tasks.Length - 1] = node;
        }

        /// <summary>
        /// Removes the specified logic node.
        /// </summary>
        /// <param name="node">The node that should be removed.</param>
        /// <returns>True if the node was removed.</returns>
        public bool RemoveNode(ITreeLogicNode node)
        {
            if (m_Tasks == null || node.Index >= m_Tasks.Length) {
                return false;
            }

            var dest = new ITreeLogicNode[m_Tasks.Length - 1];
            Array.Copy(m_Tasks, dest, node.Index);
            Array.Copy(m_Tasks, node.Index + 1, dest, node.Index, m_Tasks.Length - node.Index - 1);
            m_Tasks = dest;
            return true;
        }

        /// <summary>
        /// Adds the specified event node.
        /// </summary>
        /// <param name="eventNode">The event node that should be added.</param>
        public void AddNode(IEventNode eventNode)
        {
            if (m_EventTasks == null) {
                m_EventTasks = new IEventNode[1];
            } else {
                Array.Resize(ref m_EventTasks, m_EventTasks.Length + 1);
            }
            eventNode.Index = (ushort)(m_EventTasks.Length - 1);
            m_EventTasks[m_EventTasks.Length - 1] = eventNode;
        }

        /// <summary>
        /// Removes the specified event node.
        /// </summary>
        /// <param name="eventNode">The event node that should be removed.</param>
        /// <returns>True if the event node was removed.</returns>
        public bool RemoveNode(IEventNode eventNode)
        {
            if (m_EventTasks == null) {
                return false;
            }

            var index = m_EventTasks.IndexOf(eventNode);
            if (index == -1) {
                return false;
            }

            var dest = new IEventNode[m_EventTasks.Length - 1];
            Array.Copy(m_EventTasks, dest, index);
            Array.Copy(m_EventTasks, index + 1, dest, index, m_EventTasks.Length - index - 1);
            m_EventTasks = dest;
            return true;
        }

        /// <summary>
        /// Serializes the behavior tree.
        /// </summary>
        public void Serialize()
        {
            if (Application.isPlaying) {
                return;
            }

            m_TaskData = Serialization.Serialize<ITreeLogicNode>(m_Tasks, ValidateSerializedObject);
            m_EventTaskData = Serialization.Serialize<IEventNode>(m_EventTasks, ValidateSerializedObject);
            SerializeSharedVariables();

            // Disabled array removed in version 3.0.
            m_DisabledLogicNodesData = null;
            m_DisabledEventNodesData = null;
            m_UniqueID = Guid.NewGuid().GetHashCode();

#if UNITY_EDITOR
            // Ensure the node data is up to date.
            if (m_LogicNodeProperties != null && m_Tasks != null && m_LogicNodeProperties.Length <= m_Tasks.Length) {
                for (int i = 0; i < m_LogicNodeProperties.Length; ++i) {
                    var nodeData = m_LogicNodeProperties[i].Data;
                    nodeData.ParentIndex = m_Tasks[i].ParentIndex;
                    nodeData.SiblingIndex = m_Tasks[i].SiblingIndex;
                    nodeData.IsParent = m_Tasks[i] is IParentNode;
                    m_LogicNodeProperties[i].Data = nodeData;
                }
            }
            m_LogicNodePropertiesData = Serialization.Serialize<LogicNodeProperties>(m_LogicNodeProperties);
            m_EventNodePropertiesData = Serialization.Serialize<NodeProperties>(m_EventNodeProperties);
            m_GroupPropertiesData = Serialization.Serialize<GroupProperties>(m_GroupProperties);
#endif
        }

        /// <summary>
        /// Validates the serialized object.
        /// </summary>
        /// <param name="type">The type of object.</param>
        /// <param name="field">The field that the object belongs to.</param>
        /// <param name="value">The value of the object</param>
        /// <returns>The validated object.</returns>
        public static Serialization.ValidatedObject ValidateSerializedObject(Type type, FieldInfo field, object value)
        {
            if (value == null) {
                return new Serialization.ValidatedObject() { Type = type, Obj = value };
            }

            // Replace ILogicNode with ushort index values.
            if (typeof(IList).IsAssignableFrom(type)) {
                var elementType = Serializer.GetElementType(type);
                if (typeof(ILogicNode).IsAssignableFrom(elementType)) {
                    if (field == null || field.GetCustomAttribute<InspectNodeAttribute>() == null) {
                        var tasks = value as IList;
                        if (tasks == null) {
                            return new Serialization.ValidatedObject() { Type = type, Obj = value };
                        }

                        var indexValues = new ushort[tasks.Count];
                        for (int i = 0; i < indexValues.Length; ++i) {
                            indexValues[i] = ((ILogicNode)tasks[i]).Index;
                        }
                        return new Serialization.ValidatedObject() { Type = typeof(ushort[]), Obj = indexValues };
                    }
                } else if (Application.isPlaying && (typeof(GameObject).IsAssignableFrom(elementType) || typeof(Component).IsAssignableFrom(elementType))) { // Scene objects cannot be serialized at runtime.
                    var listValue = value as IList;
                    if (listValue != null) {
                        IList objects;
                        if (type.IsArray) {
                            objects = Array.CreateInstance(elementType, listValue.Count);
                        } else {
                            if (type.IsGenericType) {
                                objects = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType)) as IList;
                            } else {
                                objects = Activator.CreateInstance(type) as IList;
                            }
                        }
                        for (int i = 0; i < listValue.Count; ++i) {
                            GameObject gameObjectValue = null;
                            if (listValue[i] is Component componentValue) {
                                gameObjectValue = componentValue.gameObject;
                            } else {
                                gameObjectValue = listValue[i] as GameObject;
                            }
                            if (gameObjectValue != null && gameObjectValue.scene.IsValid()) {
                                if (type.IsArray) {
                                    objects[i] = null;
                                } else {
                                    objects.Add(null);
                                }
                            } else {
                                if (type.IsArray) {
                                    objects[i] = listValue[i];
                                } else {
                                    objects.Add(listValue[i]);
                                }
                            }
                        }
                        listValue = objects;
                    }
                    return new Serialization.ValidatedObject() { Type = type, Obj = listValue };
                }
            } else if (typeof(ILogicNode).IsAssignableFrom(type)) {
                if (field == null || field.GetCustomAttribute<InspectNodeAttribute>() == null) {
                    return new Serialization.ValidatedObject() { Type = typeof(ushort), Obj = ((ILogicNode)value).Index };
                }
            } else if (Application.isPlaying && (typeof(GameObject).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type))) { // Scene objects cannot be serialized at runtime.
                GameObject gameObjectValue = null;
                if (value is Component componentValue) {
                    gameObjectValue = componentValue.gameObject;
                } else {
                    gameObjectValue = value as GameObject;
                }
                if (gameObjectValue != null && gameObjectValue.scene.IsValid()) {
                    return new Serialization.ValidatedObject() { Type = type, Obj = null };
                }
            }
            return new Serialization.ValidatedObject() { Type = type, Obj = value };
        }

        /// <summary>
        /// Serializes the SharedVariables. This allows the SharedVariables to be serialized independently.
        /// </summary>
        public void SerializeSharedVariables()
        {
            if (Application.isPlaying) {
                return;
            }

            m_SharedVariableData = Serialization.Serialize<SharedVariable>(m_SharedVariables);
#if UNITY_EDITOR
            m_SharedVariableGroupsData = Serialization.Serialize<SharedVariableGroup>(m_SharedVariableGroups);
#endif

            // Update the mapping for any variable name changes.
            if (m_VariableByNameMap == null) {
                m_VariableByNameMap = new Dictionary<VariableAssignment, SharedVariable>();
            } else {
                m_VariableByNameMap.Clear();
            }
            if (m_SharedVariables != null) {
                for (int i = 0; i < m_SharedVariables.Length; ++i) {
                    if (m_SharedVariables[i] == null) {
                        continue;
                    }
                    m_VariableByNameMap.Add(new VariableAssignment(m_SharedVariables[i].Name, m_SharedVariables[i].Scope), m_SharedVariables[i]);
                }
            }
        }

        /// <summary>
        /// Deserialize the behavior tree.
        /// </summary>
        /// <param name="graphComponent">The component that the graph is being deserialized from.</param>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="force">Should the behavior tree be force deserialized?</param>
        /// <param name="forceSharedVariables">Should the shared variables be force deserialized?</param>
        /// <param name="injectSubtrees">Should the subtrees be injected into the behavior tree?</param>
        /// <param name="canDeepCopyVariables">Can the SharedVariables be deep copied?</param>
        /// <param name="sharedVariableOverrides">A list of SharedVariables that should override the current SharedVariable value.</param>
        /// <returns>True if the tree was deserialized.</returns>
        public bool Deserialize(IGraphComponent graphComponent, IGraph graph, bool force, bool forceSharedVariables, bool injectSubtrees, bool canDeepCopyVariables = true, SharedVariableOverride[] sharedVariableOverrides = null)
        {
            // No need to deserialize if the data is already deserialized.
            if (!force && ((m_Tasks != null && m_TaskData != null) || (m_EventTasks != null && m_EventTaskData != null))) {
                // SharedVariables may still need to be deserialized separately.
                DeserializeSharedVariables(graph, false, canDeepCopyVariables, sharedVariableOverrides);

                if (Application.isPlaying && m_RuntimeUniqueID == 0) {
                    m_RuntimeUniqueID = m_UniqueID;
                }
                return true;
            }

            var deserialized = DeserializeInternal(graphComponent, graph, force, forceSharedVariables, injectSubtrees, canDeepCopyVariables, sharedVariableOverrides);
#if UNITY_EDITOR
            if (deserialized) {
                UpdateInjectedGraphReferences();
            } else {
                m_InjectedGraphReferences = null;
            }
#endif
            return deserialized;
        }

        /// <summary>
        /// Internal method which deserialize the behavior tree.
        /// </summary>
        /// <param name="graphComponent">The component that the graph is being deserialized from.</param>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="force">Should the behavior tree be force deserialized?</param>
        /// <param name="forceSharedVariables">Should the shared variables be force deserialized?</param>
        /// <param name="injectSubtrees">Should the subtrees be injected into the behavior tree?</param>
        /// <param name="canDeepCopyVariables">Can the SharedVariables be deep copied?</param>
        /// <param name="sharedVariableOverrides">A list of SharedVariables that should override the current SharedVariable value.</param>
        /// <returns>True if the tree was deserialized.</returns>
        private bool DeserializeInternal(IGraphComponent graphComponent, IGraph graph, bool force, bool forceSharedVariables, bool injectSubtrees, bool canDeepCopyVariables, SharedVariableOverride[] sharedVariableOverrides = null)
        {
            // Prevent the tree from being deserialized recursively.
            if (m_Deserializing) {
                Debug.LogError($"Error: Unable to deserialize {graph}. This can be caused by recursive subtree references.");
                return false;
            }

            m_Deserializing = true;
            m_RuntimeUniqueID = Application.isPlaying ? m_UniqueID : 0;
            m_VariableFields = null;
            var errorState = false;
#if UNITY_EDITOR
            // Deserialize the properties first so it can be used elsewhere.
            if (m_LogicNodePropertiesData != null && m_LogicNodePropertiesData.Length > 0) {
                m_LogicNodeProperties = new LogicNodeProperties[m_LogicNodePropertiesData.Length];
                for (int i = 0; i < m_LogicNodePropertiesData.Length; ++i) {
                    try {
                        m_LogicNodeProperties[i] = m_LogicNodePropertiesData[i].DeserializeFields(MemberVisibility.Public) as LogicNodeProperties;
                    } catch (Exception e) {
                        m_LogicNodeProperties[i] = new LogicNodeProperties();
                        Debug.LogError($"Error: Unable to load task editor data at index {i} due to exception:\n{e}");
                    }
                }
            } else {
                m_LogicNodeProperties = null;
            }
            if (m_EventNodePropertiesData != null && m_EventNodePropertiesData.Length > 0) {
                m_EventNodeProperties = new NodeProperties[m_EventNodePropertiesData.Length];
                for (int i = 0; i < m_EventNodePropertiesData.Length; ++i) {
                    m_EventNodeProperties[i] = m_EventNodePropertiesData[i].DeserializeFields(MemberVisibility.Public) as NodeProperties;
                }
            } else {
                m_EventNodeProperties = null;
            }
            if (m_GroupPropertiesData != null && m_GroupPropertiesData.Length > 0) {
                m_GroupProperties = new GroupProperties[m_GroupPropertiesData.Length];
                for (int i = 0; i < m_GroupPropertiesData.Length; ++i) {
                    m_GroupProperties[i] = m_GroupPropertiesData[i].DeserializeFields(MemberVisibility.Public) as GroupProperties;
                }
            } else {
                m_GroupProperties = null;
            }
#endif
            DeserializeSharedVariables(graph, forceSharedVariables, canDeepCopyVariables, sharedVariableOverrides);

            // The disabled node indicies need to be deserialized before the nodes.
            if (m_DisabledEventNodesData != null && m_DisabledEventNodesData.Length > 0 && m_EventTaskData != null) {
                m_DisabledEventNodes = new ushort[m_DisabledEventNodesData.Length];
                var offset = 0;
                for (int i = 0; i < m_DisabledEventNodesData.Length; ++i) {
                    m_DisabledEventNodes[i] = (ushort)m_DisabledEventNodesData[i].DeserializeFields(MemberVisibility.Public);
                    // The node index may no longer be valid.
                    if (m_DisabledEventNodes[i - offset] >= m_EventTaskData.Length) {
                        offset++;
                    }
                }
                if (offset > 0) {
                    Array.Resize(ref m_DisabledEventNodes, m_DisabledEventNodes.Length - offset);
                }
            } else {
                m_DisabledEventNodes = null;
            }
            if (m_DisabledLogicNodesData != null && m_DisabledLogicNodesData.Length > 0 && m_TaskData != null) {
                m_DisabledLogicNodes = new ushort[m_DisabledLogicNodesData.Length];
                var offset = 0;
                for (int i = 0; i < m_DisabledLogicNodesData.Length; ++i) {
                    m_DisabledLogicNodes[i - offset] = (ushort)m_DisabledLogicNodesData[i].DeserializeFields(MemberVisibility.Public);
                    // The node index may no longer be valid.
                    if (m_DisabledLogicNodes[i - offset] >= m_TaskData.Length) {
                        offset++;
                    }
                }
                if (offset > 0) {
                    Array.Resize(ref m_DisabledLogicNodes, m_DisabledLogicNodes.Length - offset);
                }
            } else {
                m_DisabledLogicNodes = null;
            }

            ResizableArray<TaskAssignment> taskReferences = null;
            if (m_InjectedSubtreeReference != null) {
                m_InjectedSubtreeReference.Clear();
            }
            if (m_TaskData != null && m_TaskData.Length > 0) {
                m_Tasks = new ITreeLogicNode[m_TaskData.Length];
                for (int i = 0; i < m_TaskData.Length; ++i) {
                    try {
                        var task = m_TaskData[i].DeserializeFields(MemberVisibility.Public, ValidateDeserializedTypeObject, (object fieldInfoObj, object task, object value) =>
                        {
                            var validatedValue = ValidateDeserializedObject(fieldInfoObj, task, value, ref m_VariableByNameMap, ref taskReferences, sharedVariableOverrides);
                            if (validatedValue != null && validatedValue is SharedVariable sharedVariable && sharedVariable.Scope == SharedVariable.SharingScope.Graph) {
                                if (m_VariableFields == null) { m_VariableFields = new ResizableArray<VariableField>(); }
                                m_VariableFields.Add(new VariableField() { Field = fieldInfoObj as FieldInfo, Task = task, Name = sharedVariable.Name });
                            }
                            return validatedValue;
                        }) as ILogicNode;
                        if (task is ITreeLogicNode treeLogicNode) {
                            m_Tasks[i] = treeLogicNode;
                        } else if (task is ILogicNode) {
                            Debug.LogError($"Error: The task {m_TaskData[i].ObjectType} at index {i} must implement ITreeLogicNode.");
                        }
                    } catch (Exception e) {
                        Debug.LogError($"Error: Unable to load task {m_TaskData[i].ObjectType} at index {i} due to exception:\n{e}");
                    }

                    // Account for tasks where the object no longer exists.
                    if (m_Tasks[i] == null) {
                        // Check if the type has moved using the MovedFrom attribute.
                        var taskType = TypeUtility.GetType(m_TaskData[i].ObjectType);
                        if (taskType != null) {
                            // The type was found (possibly via MovedFrom), try to deserialize again.
                            try {
                                m_TaskData[i].ObjectType = taskType.FullName;
                                var task = m_TaskData[i].DeserializeFields(MemberVisibility.Public, ValidateDeserializedTypeObject, (object fieldInfoObj, object taskObj, object value) =>
                                {
                                    var validatedValue = ValidateDeserializedObject(fieldInfoObj, taskObj, value, ref m_VariableByNameMap, ref taskReferences, sharedVariableOverrides);
                                    if (validatedValue != null && validatedValue is SharedVariable sharedVariable && sharedVariable.Scope == SharedVariable.SharingScope.Graph) {
                                        if (m_VariableFields == null) { m_VariableFields = new ResizableArray<VariableField>(); }
                                        m_VariableFields.Add(new VariableField() { Field = fieldInfoObj as FieldInfo, Task = taskObj, Name = sharedVariable.Name });
                                    }
                                    return validatedValue;
                                }) as ILogicNode;
                                if (task is ITreeLogicNode treeLogicNode) {
                                    m_Tasks[i] = treeLogicNode;
                                } else if (task is ILogicNode) {
                                    Debug.LogError($"Error: The task {m_TaskData[i].ObjectType} at index {i} must implement ITreeLogicNode.");
                                }
                            } catch (Exception e) {
                                Debug.LogError($"Error: Unable to load task {m_TaskData[i].ObjectType} at index {i} after MovedFrom resolution due to exception:\n{e}");
                            }
                        }

                        // If still null, create an unknown task.
                        if (m_Tasks[i] == null) {
#if UNITY_EDITOR
                            if (m_LogicNodeProperties[i].Data.IsParent) {
                                m_Tasks[i] = new UnknownParentTaskNode(m_TaskData[i].ObjectType);
                            } else {
                                m_Tasks[i] = new UnknownTaskNode(m_TaskData[i].ObjectType);
                            }

                            m_Tasks[i].Index = (ushort)i;
                            m_Tasks[i].ParentIndex = m_LogicNodeProperties[i].Data.ParentIndex;
                            m_Tasks[i].SiblingIndex = m_LogicNodeProperties[i].Data.SiblingIndex;
#else
                            if (i + 1 < m_Tasks.Length && m_Tasks[i + 1] != null && m_Tasks[i + 1].ParentIndex == i) {
                                m_Tasks[i] = new UnknownParentTaskNode(m_TaskData[i].ObjectType);
                            } else {
                                m_Tasks[i] = new UnknownTaskNode(m_TaskData[i].ObjectType);
                            }
                            m_Tasks[i].Index = (ushort)i;
                            m_Tasks[i].ParentIndex = ushort.MaxValue;
                            m_Tasks[i].SiblingIndex = ushort.MaxValue;
#endif
                            Debug.LogError($"Error: Unable to deserialize task of type {m_TaskData[i].ObjectType}. Use the [MovedFrom] attribute for refactoring.");
                        }
                    }

                    // The RuntimeIndex is assigned later when the tree is initialized.
                    m_Tasks[i].RuntimeIndex = ushort.MaxValue;
#if UNITY_EDITOR
                    // Sanity checks.
                    if (m_Tasks[i].Index >= m_TaskData.Length) { m_Tasks[i].Index = (ushort)i; }
                    if (m_Tasks[i].ParentIndex != ushort.MaxValue && m_Tasks[i].ParentIndex >= m_TaskData.Length) { m_Tasks[i].ParentIndex = ushort.MaxValue; }
                    if (m_Tasks[i].SiblingIndex != ushort.MaxValue && m_Tasks[i].SiblingIndex >= m_TaskData.Length) { m_Tasks[i].SiblingIndex = ushort.MaxValue; }
#endif

                    // Migrate from the deprecated disabled array to the Enabled property.
                    if (m_DisabledLogicNodes != null && m_DisabledLogicNodes.Length > 0) {
                        for (int j = 0; j < m_DisabledLogicNodes.Length; ++j) {
                            if (m_DisabledLogicNodes[j] == i) {
                                m_Tasks[i].Enabled = false;
                                break;
                            }
                        }
                    }

                    if (injectSubtrees) {
                        // If the previous task is a parent the current task has to be a child otherwise the tree is in an error state. The error will also occur
                        // if there is only one task and that task is a parent task.
                        if ((m_Tasks[i].ParentIndex != ushort.MaxValue && (i > 0 && m_Tasks[i - 1] is IParentNode && m_Tasks[i].ParentIndex != m_Tasks[i - 1].Index)) || (m_Tasks[i] is IParentNode && i + 1 == m_Tasks.Length)) {
                            Debug.LogError($"Error: {graph} contains the parent task {m_Tasks[i].GetType().Name} which does not have any children. All parent tasks must contain at least one child.", graph.Parent);
                            errorState = true;
                            continue;
                        }

                        // Subtrees will be evaluated after all tasks are assigned.
                        if (m_Tasks[i] is ISubtreeReferenceNode subtreeReference) {
                            // Subtrees can be nested.
                            subtreeReference.EvaluateSubgraphs(graphComponent);
                            var subtrees = subtreeReference.Subtrees;
                            if (subtrees != null) {
                                // The parent must be able to accept the number of subtrees that there are.
                                var parentIndex = m_Tasks[i].ParentIndex;
                                IParentNode parentNode = null;
                                if (parentIndex != ushort.MaxValue) {
                                    parentNode = m_Tasks[parentIndex] as IParentNode;
                                }

                                if ((parentNode == null && subtrees.Length > 1) || (parentNode != null && subtrees.Length > parentNode.MaxChildCount)) {
                                    Debug.LogError($"Error: {graph} on object {graph.Parent} contains multiple subtrees as the starting task or as a child of a parent task which cannot contain so many children (such as a decorator).", graph.Parent);
                                    errorState = true;
                                    continue;
                                }

                                var deserializedNodes = new ITreeLogicNode[subtrees.Length][];
                                for (int j = 0; j < subtrees.Length; ++j) {
                                    if (subtrees[j] == null) {
                                        continue;
                                    }
                                    if (!subtrees[j].Deserialize(graphComponent, force && !subtrees[j].Pooled, forceSharedVariables && !subtrees[j].Pooled, true, true, subtreeReference.SharedVariableOverrides)) {
                                        errorState = true;
                                        break;
                                    };
                                    if (subtrees[j].Data.m_VariableFields != null && subtrees[j].Data.m_VariableFields.Count > 0) {
                                        if (m_VariableFields == null) { m_VariableFields = new ResizableArray<VariableField>(); ; }
                                        m_VariableFields.AddRange(subtrees[j].Data.m_VariableFields);
                                    }
                                    // Keep a reference to the deserialized nodes. This will ensure they are unique and do not get overwritten.
                                    deserializedNodes[j] = subtrees[j].TreeLogicNodes;

                                    // Add any new subtree variables to the current tree.
                                    if (subtrees[j].SharedVariables != null) {
                                        // In order to reduce allocations the first loop will determine the number of variables that need to be added.
                                        var length = subtrees[j].SharedVariables.Length;
                                        var variableCount = 0;
                                        for (int k = 0; k < length; ++k) {
                                            var subtreeVariable = subtrees[j].SharedVariables[k];
                                            if (GetVariable(graph, subtreeVariable.Name, SharedVariable.SharingScope.Graph) == null) {
                                                variableCount++;
                                            }
                                        }

                                        // And the second loop will actually add the variables.
                                        if (variableCount > 0) {
                                            var insertIndex = 0;
                                            if (m_SharedVariables == null) {
                                                m_SharedVariables = new SharedVariable[variableCount];
                                                m_VariableByNameMap = new Dictionary<VariableAssignment, SharedVariable>();
                                            } else {
                                                insertIndex = m_SharedVariables.Length;
                                                Array.Resize(ref m_SharedVariables, m_SharedVariables.Length + variableCount);
                                            }
                                            for (int k = 0; k < length; ++k) {
                                                var subtreeVariable = subtrees[j].SharedVariables[k];
                                                if (!m_VariableByNameMap.ContainsKey(new VariableAssignment(subtreeVariable.Name, SharedVariable.SharingScope.Graph))) {
                                                    m_SharedVariables[insertIndex] = subtreeVariable;
                                                    m_VariableByNameMap.Add(new VariableAssignment(subtreeVariable.Name, SharedVariable.SharingScope.Graph), subtreeVariable);
                                                    insertIndex++;
                                                }
                                            }
                                        }
                                    }
                                }

                                // Do not add the subtree if it causes an error.
                                if (!errorState) {
                                    if (m_InjectedSubtreeReference == null) { m_InjectedSubtreeReference = new ResizableArray<InjectedSubtreeReference>(); }
                                    m_InjectedSubtreeReference.Add(new InjectedSubtreeReference()
                                    {
                                        GraphReference = subtreeReference,
                                        NodeIndex = (ushort)i,
                                        Subtrees = subtrees,
                                        Nodes = deserializedNodes,
#if UNITY_EDITOR
                                        GraphReferenceNodeProperties = m_LogicNodeProperties[i],
#endif
                                    });
                                }
                            }
                        }
                    }
                }

                // Migrate from the deprecated disabled array to the Enabled property.
                m_DisabledLogicNodes = null;
            } else {
                m_Tasks = null;
            }

            // Add the event tasks before the subtrees are injected. Connected indices will be adjusted after injection.
            var baseEventTaskCount = 0;
            if (m_EventTaskData != null && m_EventTaskData.Length > 0) {
                m_EventTasks = new IEventNode[m_EventTaskData.Length];
                for (int i = 0; i < m_EventTaskData.Length; ++i) {
                    try {
                        var eventTaskObj = m_EventTaskData[i].DeserializeFields(MemberVisibility.Public, ValidateDeserializedTypeObject, (object fieldInfoObj, object task, object value) =>
                        {
                            var validatedValue = ValidateDeserializedObject(fieldInfoObj, task, value, ref m_VariableByNameMap, ref taskReferences, sharedVariableOverrides);
                            if (validatedValue != null && validatedValue is SharedVariable sharedVariable && sharedVariable.Scope == SharedVariable.SharingScope.Graph) {
                                if (m_VariableFields == null) { m_VariableFields = new ResizableArray<VariableField>(); }
                                m_VariableFields.Add(new VariableField() { Field = fieldInfoObj as FieldInfo, Task = task, Name = sharedVariable.Name });
                            }
                            return validatedValue;
                        });

                        if (eventTaskObj is IEventNode eventNode) {
                            m_EventTasks[i] = eventNode;
                        } else if (eventTaskObj != null) {
                            Debug.LogError($"Error: The event task {m_EventTaskData[i].ObjectType} at index {i} must implement IEventNode.");
                        }
                    } catch (Exception e) {
                        Debug.LogError($"Error: Unable to load event task {m_EventTaskData[i].ObjectType} at index {i} due to exception:\n{e}");
                    }

                    if (m_EventTasks[i] == null) {
                        m_EventTasks[i] = new UnknownEventTask(m_EventTaskData[i].ObjectType);

                        Debug.LogError($"Error: Unable to deserialize event of type {m_EventTaskData[i].ObjectType}.");
                    }
                    m_EventTasks[i].Index = (ushort)i;

                    // Migrate from the deprecated disabled array to the Enabled property.
                    if (m_DisabledEventNodes != null && m_DisabledEventNodes.Length > 0) {
                        for (int j = 0; j < m_DisabledEventNodes.Length; ++j) {
                            if (m_DisabledEventNodes[j] == i) {
                                m_EventTasks[i].Enabled = false;
                                break;
                            }
                        }
                    }
                }

                // Migrate from the deprecated disabled array to the Enabled property.
                m_DisabledEventNodes = null;
            } else {
                m_EventTasks = null;
            }
            baseEventTaskCount = m_EventTasks != null ? m_EventTasks.Length : 0;

            // Subtrees should be injected into the tree.
            InjectSubtrees();

            // Modify the ConnectedIndex to match the injection for the base event tasks.
            if (m_EventTasks != null && m_InjectedSubtreeReference != null && baseEventTaskCount > 0) {
                for (int i = 0; i < baseEventTaskCount; ++i) {
                    // A subtree may have injected nodes before the originally connected index. Modify the index to match the injection.
                    var offset = 0;
                    for (int j = 0; j < m_InjectedSubtreeReference.Count; ++j) {
                        if (m_InjectedSubtreeReference[j].NodeIndex >= m_EventTasks[i].ConnectedIndex) {
                            break;
                        }
                        offset += m_InjectedSubtreeReference[j].NodeCount > 0 ? m_InjectedSubtreeReference[j].NodeCount - 1 : 0;
                    }
                    if (offset > 0) {
                        m_EventTasks[i].ConnectedIndex += (ushort)offset;
                    }
                }
            }

            // After the tree has been deserialized the task references need to be assigned.
            AssignTaskReferences(m_Tasks, taskReferences);

            m_Deserializing = false;
            return !errorState;
        }

        /// <summary>
        /// Validates the object type when deserializing.
        /// </summary>
        /// <param name="type">The type of object that should be validated.</param>
        /// <param name="field">The field that contains the object.</param>
        /// <returns>The validated type.</returns>
        public static Type ValidateDeserializedTypeObject(Type type, FieldInfo field)
        {
            if (typeof(IList).IsAssignableFrom(type)) {
                var elementType = Serializer.GetElementType(type);
                if (typeof(ILogicNode).IsAssignableFrom(elementType) && (field == null || field.GetCustomAttribute<InspectNodeAttribute>() == null)) {
                    return typeof(ushort[]);
                }
            } else if (typeof(ILogicNode).IsAssignableFrom(type) && (field == null || field.GetCustomAttribute<InspectNodeAttribute>() == null)) {
                return typeof(ushort);
            }
            return type;
        }

        /// <summary>
        /// Validates the object when deserializing.
        /// </summary>
        /// <param name="fieldInfoObj">The FieldInfo that is being deserialized.</param>
        /// <param name="target">The object being deserialized.</param>
        /// <param name="value">The value of the field.</param>
        /// <param name="variableByNameMap">A reference to the map between the VariableAssignment and SharedVariable.</param>
        /// <param name="taskReferences">A reference to the list of task references that need to be resolved later.</param>
        /// <param name="sharedVariableOverrides">A list of SharedVariables that should override the current SharedVariable value.</param>
        /// <returns>The validated object.</returns>
        public static object ValidateDeserializedObject(object fieldInfoObj, object target, object value, ref Dictionary<VariableAssignment, SharedVariable> variableByNameMap,
                                    ref ResizableArray<TaskAssignment> taskReferences, SharedVariableOverride[] sharedVariableOverrides = null)
        {
            var fieldInfo = fieldInfoObj as FieldInfo;
            if (fieldInfo == null) {
                return value;
            }

            var type = fieldInfo.FieldType;
            if (value == null) {
                // A SharedVariable object should always exist.
                if (!type.IsAbstract && typeof(SharedVariable).IsAssignableFrom(type)) {
                    return Activator.CreateInstance(type);
                }
                return null;
            }

            if (typeof(IList).IsAssignableFrom(type)) {
                var elementType = Serializer.GetElementType(type);
                if (typeof(ILogicNode).IsAssignableFrom(elementType) && fieldInfo.GetCustomAttribute<InspectNodeAttribute>() == null) {
                    // The task reference will be assigned after all of the tasks have been deserialized.
                    if (taskReferences == null) { taskReferences = new ResizableArray<TaskAssignment>(); }
                    taskReferences.Add(new TaskAssignment() { Field = fieldInfo, Target = target, Value = value });
                } else if (typeof(SharedVariable).IsAssignableFrom(elementType)) {
                    var listValue = value as IList;
                    if (listValue != null) {
                        for (int i = 0; i < listValue.Count; ++i) {
                            var sharedVariableElement = listValue[i] as SharedVariable;
                            if (variableByNameMap != null && sharedVariableElement != null && !string.IsNullOrEmpty(sharedVariableElement.Name)) {
                                if (variableByNameMap.TryGetValue(new VariableAssignment(sharedVariableElement.Name, sharedVariableElement.Scope), out var mappedSharedVariable)) {
                                    if (Application.isPlaying && sharedVariableElement.Scope == SharedVariable.SharingScope.Dynamic && sharedVariableElement.GetType() != mappedSharedVariable.GetType()) {
                                        Debug.LogError($"Error: The dynamic variables with name {sharedVariableElement.Name} have different types. All dynamic variables must have the same type.");
                                        listValue[i] = sharedVariableElement;
                                    } else {
                                        listValue[i] = GetOverrideVariable(sharedVariableOverrides, mappedSharedVariable, false);
                                    }
                                } else if (sharedVariableElement.Scope == SharedVariable.SharingScope.Dynamic) {
                                    // New dynamic variables should have the default value.
                                    var sharedVariableValueType = sharedVariableElement.GetType().GetGenericArguments()[0];
                                    if (sharedVariableValueType.IsValueType) {
                                        sharedVariableElement.SetValue(Activator.CreateInstance(sharedVariableValueType));
                                    } else {
                                        sharedVariableElement.SetValue(null);
                                    }

                                    // Dynamic variables are created when the task is deserialized. The variable needs to be added to the mapping so it can be reused.
                                    variableByNameMap.Add(new VariableAssignment(sharedVariableElement.Name, sharedVariableElement.Scope), sharedVariableElement);
                                    listValue[i] = sharedVariableElement;
                                }
                            }

                        }
                        return listValue;
                    }
                }
            } else if (typeof(ILogicNode).IsAssignableFrom(type) && fieldInfo.GetCustomAttribute<InspectNodeAttribute>() == null) {
                // The task reference will be assigned after all of the tasks have been deserialized.
                if (taskReferences == null) { taskReferences = new ResizableArray<TaskAssignment>(); }
                taskReferences.Add(new TaskAssignment() { Field = fieldInfo, Target = target, Value = value });
            } else if (typeof(SharedVariable).IsAssignableFrom(type)) {
                var sharedVariable = value as SharedVariable;
                if (variableByNameMap != null && sharedVariable != null && !string.IsNullOrEmpty(sharedVariable.Name)) {
                    if (variableByNameMap.TryGetValue(new VariableAssignment(sharedVariable.Name, sharedVariable.Scope), out var mappedSharedVariable)) {
                        if (Application.isPlaying && sharedVariable.Scope == SharedVariable.SharingScope.Dynamic && sharedVariable.GetType() != mappedSharedVariable.GetType()) {
                            Debug.LogError($"Error: The dynamic variables with name {sharedVariable.Name} have different types. Dynamic variables with the same name must have the same type.");
                            return sharedVariable;
                        }
                        return GetOverrideVariable(sharedVariableOverrides, mappedSharedVariable, false);
                    } else if (Application.isPlaying && sharedVariable.Scope == SharedVariable.SharingScope.Dynamic) {
                        // New dynamic variables should have the default value.
                        var sharedVariableValueType = sharedVariable.GetType().GetGenericArguments()[0];
                        if (sharedVariableValueType.IsValueType) {
                            sharedVariable.SetValue(Activator.CreateInstance(sharedVariableValueType));
                        } else {
                            sharedVariable.SetValue(null);
                        }

                        // Dynamic variables are created when the task is deserialized. The variable needs to be added to the mapping so it can be reused.
                        variableByNameMap.Add(new VariableAssignment(sharedVariable.Name, sharedVariable.Scope), sharedVariable);
                        return sharedVariable;
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Deserializes the SharedVariables. This allows the SharedVariables to be deserialized independently.
        /// </summary>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="force">Should the variables be forced deserialized?</param>
        /// <param name="canDeepCopy">Can the SharedVariables be deep copied?</param>
        /// <param name="sharedVariableOverrides">A list of SharedVariables that should override the current SharedVariable value.</param>
        /// <returns>True if the SharedVariables were deserialized.</returns>
        public bool DeserializeSharedVariables(IGraph graph, bool force, bool canDeepCopy, SharedVariableOverride[] sharedVariableOverrides = null)
        {
            // No need to deserialize if the data is already deserialized.
            if (!force && (m_SharedVariables != null || m_VariableByNameMap != null
#if UNITY_EDITOR
                || m_SharedVariableGroups != null
#endif
                )) {
                return false;
            }

            if (m_SharedVariableData != null && m_SharedVariableData.Length > 0) {
                m_SharedVariables = new SharedVariable[m_SharedVariableData.Length];
                for (int i = 0; i < m_SharedVariableData.Length; ++i) {
                    try {
                        m_SharedVariables[i] = m_SharedVariableData[i].DeserializeFields(MemberVisibility.Public) as SharedVariable;
                    } catch (Exception e) {
                        Debug.LogError($"Error: Unable to load variable {m_SharedVariableData[i].ObjectType} at index {i} due to exception:\n{e}");
                    }

                    if (m_SharedVariables[i] == null) {
                        var originalTypeName = m_SharedVariableData[i].ObjectType;
                        m_SharedVariableData[i].ObjectType = typeof(UnknownSharedVariable).FullName;
                        m_SharedVariables[i] = m_SharedVariableData[i].DeserializeFields(MemberVisibility.Public) as SharedVariable;
                        m_SharedVariableData[i].ObjectType = originalTypeName;
                        // Store the original type name in the unknown variable.
                        if (m_SharedVariables[i] is UnknownSharedVariable unknownVar) {
                            unknownVar.UnknownType = originalTypeName;
                        }

                        Debug.LogError($"Error: Unable to deserialize SharedVariable {m_SharedVariables[i].Name} of type {originalTypeName}.");
                    }

                    // The override variable can set a value specific for the subtree.
                    if (Application.isPlaying) {
                        m_SharedVariables[i].Initialize();

                        var overrideVariable = GetOverrideVariable(sharedVariableOverrides, m_SharedVariables[i], true);
                        // If the overridden scope is self then only the value should be overridden and not the SharedVariable reference.
                        if (overrideVariable != null && overrideVariable.Scope == SharedVariable.SharingScope.Self) {
                            m_SharedVariables[i].SetValue(overrideVariable.GetValue());
                        }
                    }
                }
            } else {
                m_SharedVariables = null;
            }
            m_VariableByNameMap = PopulateSharedVariablesMapping(graph, canDeepCopy);

#if UNITY_EDITOR
            if (m_SharedVariableGroupsData != null && m_SharedVariableGroupsData.Length > 0) {
                m_SharedVariableGroups = new SharedVariableGroup[m_SharedVariableGroupsData.Length];
                for (int i = 0; i < m_SharedVariableGroupsData.Length; ++i) {
                    m_SharedVariableGroups[i] = m_SharedVariableGroupsData[i].DeserializeFields(MemberVisibility.Public) as SharedVariableGroup;
                }
            } else {
                m_SharedVariableGroups = null;
            }
#endif

            return true;
        }

        /// <summary>
        /// Returns the override SharedVariable from the source SharedVariable.
        /// </summary>
        /// <param name="sharedVariableOverrides">The list of override SharedVariables.</param>
        /// <param name="graphVariable">The variable that should be overridden.</param>
        /// <param name="deserialize">Is the method being called when the variables are being deserialized?</param>
        /// <returns>The override SharedVariable (can be null).</returns>
        private static SharedVariable GetOverrideVariable(SharedVariableOverride[] sharedVariableOverrides, SharedVariable graphVariable, bool deserialize)
        {
            if (sharedVariableOverrides == null) {
                return deserialize ? null : graphVariable;
            }

            for (int i = 0; i < sharedVariableOverrides.Length; ++i) {
                var overrideVariable = sharedVariableOverrides[i].Override;
                // Empty variables indicate that the variable should not be overridden.
                if (overrideVariable == null || overrideVariable.Scope == SharedVariable.SharingScope.Empty) {
                    continue;
                }

                // The override variable should be used if the name and the type matches.
                var sourceVariable = sharedVariableOverrides[i].Source;
                if (sourceVariable.GetType() != graphVariable.GetType() || sourceVariable.Name != graphVariable.Name) {
                    continue;
                }

                // If the scope is self then the graphVariable value should be updated instead of completely replaced.
                if (overrideVariable.Scope == SharedVariable.SharingScope.Self) {
                    graphVariable.SetValue(overrideVariable.GetValue());
                    return graphVariable;
                }

                return overrideVariable;
            }

            return graphVariable;
        }

        /// <summary>
        /// Populates the SharedVariable Mapping at runtime.
        /// </summary>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="canDeepCopy">Can the SharedVariables be deep copied?</param>
        /// <returns>A reference to the map between the VariableAssignment and SharedVariable.</returns>
        public static Dictionary<VariableAssignment, SharedVariable> PopulateSharedVariablesMapping(IGraph graph, bool canDeepCopy)
        {
            return PopulateSharedVariablesMapping(graph, graph.SharedVariables, canDeepCopy);
        }

        /// <summary>
        /// Populates the SharedVariable Mapping at runtime.
        /// </summary>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="graphSharedVariables">The SharedVariables that should be used for graph scope variables.</param>
        /// <param name="canDeepCopy">Can the SharedVariables be deep copied?</param>
        /// <returns>A reference to the map between the VariableAssignment and SharedVariable.</returns>
        private static Dictionary<VariableAssignment, SharedVariable> PopulateSharedVariablesMapping(IGraph graph, SharedVariable[] graphSharedVariables, bool canDeepCopy)
        {
            var variableByNameMap = new Dictionary<VariableAssignment, SharedVariable>();
            PopulateSharedVariablesMapping(graph, graphSharedVariables, SharedVariable.SharingScope.Graph, canDeepCopy, ref variableByNameMap);

            if (graph.Parent is GameObject parentGameObject) {
                var gameObjectSharedVariablesContainer = parentGameObject.GetComponent<GameObjectSharedVariables>();
                if (gameObjectSharedVariablesContainer != null) {
                    gameObjectSharedVariablesContainer.Deserialize(false);
                    PopulateSharedVariablesMapping(graph, gameObjectSharedVariablesContainer.SharedVariables, SharedVariable.SharingScope.GameObject, canDeepCopy, ref variableByNameMap);
                }
            }

            var sceneSharedVariablesContainer = SceneSharedVariables.Instance;
            if (sceneSharedVariablesContainer != null) {
                sceneSharedVariablesContainer.Deserialize(false);
                PopulateSharedVariablesMapping(graph, sceneSharedVariablesContainer.SharedVariables, SharedVariable.SharingScope.Scene, canDeepCopy, ref variableByNameMap);
            }

            var projectSharedVariablesContainer = ProjectSharedVariables.Instance;
            if (projectSharedVariablesContainer != null) {
                projectSharedVariablesContainer.Deserialize(false);
                PopulateSharedVariablesMapping(graph, projectSharedVariablesContainer.SharedVariables, SharedVariable.SharingScope.Project, canDeepCopy, ref variableByNameMap);
            }
            return variableByNameMap;
        }

        /// <summary>
        /// Populates the name variables mapping with the specified SharedVariables.
        /// </summary>
        /// <param name="graph">The graph that is being deserialized.</param>
        /// <param name="sharedVariables">The SharedVariables that should be populated.</param>
        /// <param name="scope">The scope of SharedVariables.</param>
        /// <param name="canDeepCopy">Can the SharedVariables be deep copied?</param>
        /// <param name="variableByNameMap">A reference to the map between the VariableAssignment and SharedVariable.</param>
        private static void PopulateSharedVariablesMapping(IGraph graph, SharedVariable[] sharedVariables, SharedVariable.SharingScope scope, bool canDeepCopy, ref Dictionary<VariableAssignment, SharedVariable> variableByNameMap)
        {
            if (sharedVariables == null) {
                return;
            }

            var deepCopy = canDeepCopy && graph is Subtree && scope == SharedVariable.SharingScope.Graph; // Deep copy variables so the instance is not bound to the subtree.
            for (int i = 0; i < sharedVariables.Length; ++i) {
                if (sharedVariables[i] == null) {
                    continue;
                }
                if (variableByNameMap.ContainsKey(new VariableAssignment(sharedVariables[i].Name, scope))) {
#if UNITY_EDITOR
                    Debug.LogWarning("Warning: Multiple SharedVariables with the same name have been added. Please email support@opsive.com with the steps to reproduce this warning. Thank you.");
#endif
                    continue;
                }
                var val = new VariableAssignment(sharedVariables[i].Name, scope);
                variableByNameMap.Add(val, deepCopy ? CopyUtility.DeepCopy(sharedVariables[i]) as SharedVariable : sharedVariables[i]);
            }
        }

        /// <summary>
        /// Injects the subtree into the task list.
        /// </summary>
        private void InjectSubtrees()
        {
            if (m_InjectedSubtreeReference == null || m_InjectedSubtreeReference.Count == 0) {
                return;
            }

            // The behavior tree must generate a new ID when subtrees are injected.
            m_RuntimeUniqueID = Guid.NewGuid().GetHashCode();

            var taskCount = 0;
            var subtreeReferenceCount = 0;
            var subtreeAssignments = new ResizableArray<SubtreeAssignment>();
            var eventAssignments = new ResizableArray<SubtreeAssignment>();
            var lastParentIndex = m_Tasks[m_InjectedSubtreeReference[0].NodeIndex].ParentIndex;
            var parentIndexOffset = 0;
            for (int i = 0; i < m_InjectedSubtreeReference.Count; ++i) {
                var subtreeReference = m_Tasks[m_InjectedSubtreeReference[i].NodeIndex] as ISubtreeReferenceNode;
                var subtrees = subtreeReference.Subtrees;
                if (subtrees != null) {
                    var indexOffset = (ushort)0; // The index offset is relative to each individual ISubtreeReferenceNode task.

                    // The parent index will change based on the number of tasks that have been added.
                    var parentIndex = m_Tasks[m_InjectedSubtreeReference[i].NodeIndex].ParentIndex;
                    if (parentIndex != ushort.MaxValue && (parentIndex > lastParentIndex || (i > 0 && lastParentIndex == ushort.MaxValue))) {
                        parentIndexOffset = (ushort)(taskCount - subtreeReferenceCount);
                        lastParentIndex = parentIndex;
                    } else if (parentIndex < lastParentIndex) {
                        parentIndexOffset = 0;
                        lastParentIndex = parentIndex;
                    }

                    // Calculate the parent index offset based on previously injected subtrees
                    for (int j = 0; j < subtrees.Length; ++j) {
                        if (subtrees[j] == null || subtrees[j].LogicNodes == null || subtrees[j].EventNodes == null) {
                            continue;
                        }

                        var eventNodes = subtrees[j].EventNodes;
                        if (eventNodes == null) {
                            continue;
                        }

                        for (int k = 0; k < eventNodes.Length; ++k) {
                            var eventNode = eventNodes[k];
                            if (eventNode == null) {
                                continue;
                            }

                            if (eventNode.ConnectedIndex == ushort.MaxValue || !subtrees[j].IsNodeEnabled(false, k)) {
                                continue;
                            }

                            var sourceIndex = eventNode.ConnectedIndex;
                            var subtreeNodes = m_InjectedSubtreeReference[i].TreeNodes[j];
                            if (subtreeNodes == null || sourceIndex >= subtreeNodes.Length) {
                                continue;
                            }

                            var firstNode = subtreeNodes[sourceIndex];
                            var subtreeNodeCount = GetChildCount(firstNode, subtreeNodes) + 1; // firstNode should be included in addition to the children.

                            if (eventNode.GetType() == typeof(Start)) {
                                taskCount += subtreeNodeCount;
                                subtreeAssignments.Add(new SubtreeAssignment()
                                {
                                    EventNodeType = eventNode.GetType(),
                                    EventNodeIndex = (ushort)k,
                                    SourceIndex = sourceIndex,
                                    ReferenceIndex = i,
                                    NodeIndex = m_InjectedSubtreeReference[i].NodeIndex,
                                    SubtreeIndex = j,
                                    Subtree = subtrees[j],
                                    NodeCount = (ushort)subtreeNodeCount,
                                    IndexOffset = indexOffset,
                                    ParentIndex = (ushort)(parentIndex + parentIndexOffset),
                                    SiblingIndex = m_Tasks[m_InjectedSubtreeReference[i].NodeIndex].SiblingIndex,
#if UNITY_EDITOR
                                    NodePropertiesPosition = m_LogicNodeProperties[m_InjectedSubtreeReference[i].NodeIndex].Position,
                                    Collapsed = m_LogicNodeProperties[m_InjectedSubtreeReference[i].NodeIndex].Collapsed
#endif
                                });
                                indexOffset += (ushort)subtreeNodeCount;
                            } else {
                                eventAssignments.Add(new SubtreeAssignment()
                                {
                                    EventNodeType = eventNode.GetType(),
                                    EventNodeIndex = (ushort)k,
                                    SourceIndex = sourceIndex,
                                    ReferenceIndex = i,
                                    NodeIndex = m_InjectedSubtreeReference[i].NodeIndex,
                                    SubtreeIndex = j,
                                    Subtree = subtrees[j],
                                    NodeCount = (ushort)subtreeNodeCount,
                                    ParentIndex = ushort.MaxValue,
                                    SiblingIndex = ushort.MaxValue
                                });
                            }
                        }
                    }

                    // Update the parent index offset for the next subtree reference
                    if (indexOffset > 0) { // Subtree References may not contain any valid subtrees.
                        subtreeReferenceCount++;
                    }
                    var subtreeNodesReferenceOrig = m_InjectedSubtreeReference[i];
                    subtreeNodesReferenceOrig.NodeCount = indexOffset;
                    m_InjectedSubtreeReference[i] = subtreeNodesReferenceOrig;
                }
            }

            if (taskCount > 0) {
                var targetCount = m_Tasks.Length + taskCount - subtreeReferenceCount;
                var originalTaskCount = m_Tasks.Length;
                if (m_Tasks.Length != targetCount) {
                    Array.Resize(ref m_Tasks, targetCount);
#if UNITY_EDITOR
                    Array.Resize(ref m_LogicNodeProperties, targetCount);
#endif
                }

                // Make space for all of the subtree tasks.
                var addedTasks = 0;
                for (int i = 0; i < subtreeAssignments.Count; ++i) {
                    var subtreeIndex = (ushort)(subtreeAssignments[i].NodeIndex + addedTasks);
                    var subtreeTaskCount = subtreeAssignments[i].NodeCount - (subtreeAssignments[i].IndexOffset == 0 ? 1 : 0);
                    if (subtreeTaskCount > 0) { // subtreeTaskCount will be zero if a single task replaces the reference task.
                        for (int j = originalTaskCount - 1 + addedTasks; j > subtreeIndex; --j) {
                            var node = m_Tasks[j];
                            node.Index += (ushort)subtreeTaskCount;
                            if (node.ParentIndex > subtreeIndex && node.ParentIndex != ushort.MaxValue) {
                                node.ParentIndex += (ushort)subtreeTaskCount;
                            }
                            if (node.SiblingIndex > subtreeIndex && node.SiblingIndex != ushort.MaxValue) {
                                node.SiblingIndex += (ushort)subtreeTaskCount;
                            }
                            m_Tasks[j + subtreeTaskCount] = node;
                            m_Tasks[j] = null;
#if UNITY_EDITOR
                            m_LogicNodeProperties[j + subtreeTaskCount] = m_LogicNodeProperties[j];
#endif
                        }

                        // The parents need to adjust their sibling index offsets for the newly added nodes. This should only be done with an index offset of 0
                        // as grouped subtrees have the same parents.
                        if (subtreeAssignments[i].IndexOffset == 0) {
                            var parentIndex = m_Tasks[subtreeIndex].ParentIndex;
                            while (parentIndex != ushort.MaxValue) {
                                var parentNode = m_Tasks[parentIndex];
                                if (parentNode.SiblingIndex != ushort.MaxValue) {
                                    parentNode.SiblingIndex += (ushort)subtreeTaskCount;
                                    m_Tasks[parentIndex] = parentNode;
                                }
                                parentIndex = parentNode.ParentIndex;
                            }
                        }

                        subtreeAssignments[i] = new SubtreeAssignment {
                            EventNodeType = subtreeAssignments[i].EventNodeType,
                            EventNodeIndex = subtreeAssignments[i].EventNodeIndex,
                            SourceIndex = subtreeAssignments[i].SourceIndex,
                            ReferenceIndex = subtreeAssignments[i].ReferenceIndex,
                            NodeIndex = subtreeAssignments[i].NodeIndex,
                            SubtreeIndex = subtreeAssignments[i].SubtreeIndex,
                            Subtree = subtreeAssignments[i].Subtree,
                            NodeCount = subtreeAssignments[i].NodeCount,
                            IndexOffset = subtreeAssignments[i].IndexOffset,
                            ParentIndex = subtreeAssignments[i].ParentIndex,
                            SiblingIndex = subtreeAssignments[i].SiblingIndex,
#if UNITY_EDITOR
                            NodePropertiesPosition = subtreeAssignments[i].NodePropertiesPosition,
                            Collapsed = subtreeAssignments[i].Collapsed,
#endif
                        };
                    }
                    // Tasks were added to the tree. Update the tree to the correct indicies.
                    var subtreeAssignment = subtreeAssignments[i];
                    subtreeAssignment.IndexOffset = (ushort)(addedTasks + (subtreeAssignments[i].IndexOffset == 0 ? 0 : 1));
                    subtreeAssignments[i] = subtreeAssignment;

                    addedTasks += subtreeTaskCount;
                }

                // Populate the tasks with the subtree.
                for (int i = 0; i < subtreeAssignments.Count; ++i) {
                    var subtreeIndex = (ushort)(subtreeAssignments[i].NodeIndex + subtreeAssignments[i].IndexOffset);
                    var subtreeParentIndex = subtreeAssignments[i].ParentIndex;
                    var rootSiblingIndex = ushort.MaxValue;
                    if (i + 1 < subtreeAssignments.Count && subtreeAssignments[i + 1].ReferenceIndex == subtreeAssignments[i].ReferenceIndex) {
                        // Point to the first node of the next subtree.
                        rootSiblingIndex = (ushort)(subtreeAssignments[i + 1].NodeIndex + subtreeAssignments[i + 1].IndexOffset);
                    } else {
                        // Use the original SiblingIndex from the reference task.
                        rootSiblingIndex = subtreeAssignments[i].SiblingIndex != ushort.MaxValue ? (ushort)(subtreeIndex + subtreeAssignments[i].NodeCount) : ushort.MaxValue;
                    }

                    var subtreeReference = m_InjectedSubtreeReference[subtreeAssignments[i].ReferenceIndex];
                    var subtreeNodes = subtreeReference.TreeNodes[subtreeAssignments[i].SubtreeIndex];
                    if (subtreeNodes == null || subtreeAssignments[i].SourceIndex >= subtreeNodes.Length) {
                        continue;
                    }

                    InjectSubtreeLogicNodes(subtreeAssignments[i], subtreeNodes, subtreeReference.Subtrees[subtreeAssignments[i].SubtreeIndex].Pooled, subtreeIndex, subtreeParentIndex,
                        rootSiblingIndex, subtreeReference.GraphReference.Enabled, true, false);
                }
            }

            InjectSubtreeEventNodes(eventAssignments);
        }

        /// <summary>
        /// Injects subtree logic nodes into the task list.
        /// </summary>
        /// <param name="assignment">The subtree assignment.</param>
        /// <param name="subtreeNodes">The subtree nodes to inject.</param>
        /// <param name="pooled">Is the subtree pooled?</param>
        /// <param name="branchStartIndex">The index to start inserting nodes at.</param>
        /// <param name="rootParentIndex">The parent index for the root node.</param>
        /// <param name="rootSiblingIndex">The sibling index for the root node.</param>
        /// <param name="subtreeReferenceEnabled">Is the subtree reference enabled?</param>
        /// <param name="applyPositionOffset">Should node properties be offset to match the reference?</param>
        /// <param name="buildNodeMap">Should a node map be built for event node remapping?</param>
        /// <returns>A map of original nodes to copied nodes (can be null).</returns>
        private Dictionary<object, object> InjectSubtreeLogicNodes(SubtreeAssignment assignment, ITreeLogicNode[] subtreeNodes, bool pooled, ushort branchStartIndex,
            ushort rootParentIndex, ushort rootSiblingIndex, bool subtreeReferenceEnabled, bool applyPositionOffset, bool buildNodeMap)
        {
            if (subtreeNodes == null || assignment.SourceIndex >= subtreeNodes.Length) {
                return null;
            }

            var indexOffset = (int)branchStartIndex - assignment.SourceIndex;
            Dictionary<object, object> nodeMap = null;
            if (!pooled && buildNodeMap) {
                nodeMap = new Dictionary<object, object>();
            }
#if UNITY_EDITOR
            var positionOffset = Vector2.zero;
#endif
            for (int j = 0; j < assignment.NodeCount; ++j) {
                var node = subtreeNodes[assignment.SourceIndex + j];
                // The node needs to be copied if it isn't pooled to prevent the same node from being used in multiple trees.
                if (!pooled) {
                    node = CopySubtreeLogicNode(node, nodeMap);
                }

                node.Index = (ushort)(branchStartIndex + j);
                node.RuntimeIndex = ushort.MaxValue;
                if (j == 0) {
                    node.ParentIndex = rootParentIndex;
                    node.SiblingIndex = rootSiblingIndex;
                } else {
                    // Adjust the subsequent subtree tasks by the location of the insertion.
                    if (node.ParentIndex != ushort.MaxValue) {
                        node.ParentIndex = (ushort)(node.ParentIndex + indexOffset);
                    }
                    if (node.SiblingIndex != ushort.MaxValue) {
                        node.SiblingIndex = (ushort)(node.SiblingIndex + indexOffset);
                    }
                }

                // If the parent reference task is disabled then all subtree nodes should be disabled.
                if (!subtreeReferenceEnabled) {
                    node.Enabled = false;
                }

                m_Tasks[branchStartIndex + j] = node;
#if UNITY_EDITOR
                if (m_LogicNodeProperties != null && assignment.Subtree.LogicNodeProperties != null && assignment.SourceIndex + j < assignment.Subtree.LogicNodeProperties.Length) {
                    var nodeProperties = CopyUtility.DeepCopy(assignment.Subtree.LogicNodeProperties[assignment.SourceIndex + j]) as LogicNodeProperties;
                    nodeProperties.GuidString = Guid.NewGuid().ToString();
                    if (applyPositionOffset) {
                        if (j == 0) {
                            // Keep the tasks in the same relative position as the subtree reference.
                            positionOffset = assignment.NodePropertiesPosition - assignment.Subtree.LogicNodeProperties[assignment.SourceIndex + j].Position;
                        } else {
                            // Apply a small offset for stacked subtrees so they are not directly overlapping.
                            positionOffset += new Vector2(2, 2);
                        }
                        nodeProperties.Position += positionOffset;
                        nodeProperties.Collapsed = assignment.Collapsed;
                    }
                    m_LogicNodeProperties[branchStartIndex + j] = nodeProperties;
                }
#endif
            }

            return nodeMap;
        }

        /// <summary>
        /// Copies the subtree logic node while updating variable fields and any node maps.
        /// </summary>
        /// <param name="node">The node to copy.</param>
        /// <param name="nodeMap">The node map to update (can be null).</param>
        /// <returns>The copied node.</returns>
        private ITreeLogicNode CopySubtreeLogicNode(ITreeLogicNode node, Dictionary<object, object> nodeMap)
        {
            var copiedNode = CopyUtility.DeepCopy(node) as ITreeLogicNode;
            if ((m_VariableFields != null && m_VariableFields.Count > 0) || nodeMap != null) {
                // Replace the old node reference with the updated reference.
                var localMap = new Dictionary<object, object>();
                localMap.Add(node, copiedNode);
                if (node is IContainerNode containerNode) {
                    if (containerNode.Nodes != null) {
                        var copiedContainerNode = copiedNode as IContainerNode;
                        for (int k = 0; k < containerNode.Nodes.Length; ++k) {
                            localMap.Add(containerNode.Nodes[k], copiedContainerNode.Nodes[k]);
                        }
                    }
                }

                if (nodeMap != null) {
                    foreach (var pair in localMap) {
                        nodeMap.Add(pair.Key, pair.Value);
                    }
                }

                if (m_VariableFields != null && m_VariableFields.Count > 0) {
                    for (int k = 0; k < m_VariableFields.Count; ++k) {
                        if (localMap.TryGetValue(m_VariableFields[k].Task, out var copiedTask)) {
                            var variableField = m_VariableFields[k];
                            variableField.Task = copiedTask;
                            m_VariableFields[k] = variableField;
                        }
                    }
                }
            }
            copiedNode.Enabled = node.Enabled;
            return copiedNode;
        }

        /// <summary>
        /// Injects subtree event nodes and their connected logic nodes at the end of the task list.
        /// </summary>
        /// <param name="eventAssignments">The event node assignments to inject.</param>
        private void InjectSubtreeEventNodes(ResizableArray<SubtreeAssignment> eventAssignments)
        {
            if (eventAssignments == null || eventAssignments.Count == 0) {
                return;
            }

            var injectAssignments = new ResizableArray<SubtreeAssignment>();
            var singleInstanceEventTypes = new HashSet<Type>();
            if (m_EventTasks != null && m_EventTasks.Length > 0) {
                for (int i = 0; i < m_EventTasks.Length; ++i) {
                    var eventTask = m_EventTasks[i];
                    if (eventTask == null) {
                        continue;
                    }
                    var eventType = eventTask.GetType();
                    if (AllowsMultipleEventNodeTypes(eventType)) {
                        continue;
                    }
                    singleInstanceEventTypes.Add(eventType);
                }
            }
            for (int i = 0; i < eventAssignments.Count; ++i) {
                var assignment = eventAssignments[i];
                var eventType = assignment.EventNodeType;
                if (eventType == null) {
                    continue;
                }
                if (!AllowsMultipleEventNodeTypes(eventType)) {
                    if (singleInstanceEventTypes.Contains(eventType)) {
                        continue;
                    }
                    singleInstanceEventTypes.Add(eventType);
                }
                injectAssignments.Add(assignment);
            }
            if (injectAssignments.Count == 0) {
                return;
            }

            if (m_Tasks == null) {
                m_Tasks = new ITreeLogicNode[0];
            }

            var originalTaskCount = m_Tasks.Length;
            var addedTaskCount = 0;
            for (int i = 0; i < injectAssignments.Count; ++i) {
                addedTaskCount += injectAssignments[i].NodeCount;
            }

            if (addedTaskCount > 0) {
                Array.Resize(ref m_Tasks, originalTaskCount + addedTaskCount);
#if UNITY_EDITOR
                Array.Resize(ref m_LogicNodeProperties, originalTaskCount + addedTaskCount);
#endif
            }

            var addedTasks = 0;
            for (int i = 0; i < injectAssignments.Count; ++i) {
                var assignment = injectAssignments[i];
                var subtreeReference = m_InjectedSubtreeReference[assignment.ReferenceIndex];
                var subtree = assignment.Subtree;
                var subtreeNodes = subtreeReference.TreeNodes[assignment.SubtreeIndex];
                if (subtreeNodes == null || assignment.SourceIndex >= subtreeNodes.Length) {
                    continue;
                }

                var pooled = subtreeReference.Subtrees[assignment.SubtreeIndex].Pooled;
                var subtreeReferenceEnabled = subtreeReference.GraphReference.Enabled;
                var branchStartIndex = (ushort)(originalTaskCount + addedTasks);
                var nodeMap = InjectSubtreeLogicNodes(assignment, subtreeNodes, pooled, branchStartIndex, ushort.MaxValue,
                                                        ushort.MaxValue, subtreeReferenceEnabled, false, true);

                var eventNodes = subtree.EventNodes;
                if (eventNodes == null || assignment.EventNodeIndex >= eventNodes.Length) {
                    addedTasks += assignment.NodeCount;
                    continue;
                }

                var eventNode = eventNodes[assignment.EventNodeIndex];
                if (eventNode == null) {
                    addedTasks += assignment.NodeCount;
                    continue;
                }

                var injectedEventNode = eventNode;
                if (!pooled) {
                    injectedEventNode = CopyUtility.DeepCopy(eventNode) as IEventNode;
                    if (m_VariableFields != null && m_VariableFields.Count > 0) {
                        for (int k = 0; k < m_VariableFields.Count; ++k) {
                            if (ReferenceEquals(m_VariableFields[k].Task, eventNode)) {
                                var variableField = m_VariableFields[k];
                                variableField.Task = injectedEventNode;
                                m_VariableFields[k] = variableField;
                            }
                        }
                    }
                    RemapEventNodeReferences(injectedEventNode, nodeMap);
                }

                injectedEventNode.ConnectedIndex = branchStartIndex;
                if (!subtreeReferenceEnabled) {
                    injectedEventNode.Enabled = false;
                }

                var eventIndex = (ushort)(m_EventTasks != null ? m_EventTasks.Length : 0);
                if (m_EventTasks == null) {
                    m_EventTasks = new IEventNode[1];
                } else {
                    Array.Resize(ref m_EventTasks, m_EventTasks.Length + 1);
                }
                m_EventTasks[eventIndex] = injectedEventNode;
                injectedEventNode.Index = eventIndex;
                if (m_InjectedSubtreeEventNodes == null) {
                    m_InjectedSubtreeEventNodes = new HashSet<IEventNode>();
                }
                m_InjectedSubtreeEventNodes.Add(injectedEventNode);
#if UNITY_EDITOR
                if (m_EventNodeProperties != null) {
                    Array.Resize(ref m_EventNodeProperties, m_EventTasks.Length);
                    if (subtree.EventNodeProperties != null && assignment.EventNodeIndex < subtree.EventNodeProperties.Length) {
                        var nodeProperties = CopyUtility.DeepCopy(subtree.EventNodeProperties[assignment.EventNodeIndex]) as NodeProperties;
                        nodeProperties.GuidString = Guid.NewGuid().ToString();
                        m_EventNodeProperties[eventIndex] = nodeProperties;
                    }
                }
#endif

                addedTasks += assignment.NodeCount;
            }
        }

        /// <summary>
        /// Returns true if the event node type allows multiple nodes of that type.
        /// </summary>
        /// <param name="eventNodeType">The event node type.</param>
        /// <returns>True if the type allows multiple nodes.</returns>
        private bool AllowsMultipleEventNodeTypes(Type eventNodeType)
        {
            return eventNodeType != null && Attribute.IsDefined(eventNodeType, typeof(AllowMultipleTypes), true);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Updates the cached set of injected graph references.
        /// </summary>
        private void UpdateInjectedGraphReferences()
        {
            if (m_InjectedSubtreeReference == null || m_InjectedSubtreeReference.Count == 0) {
                m_InjectedGraphReferences = null;
                return;
            }

            if (m_InjectedGraphReferences == null) {
                m_InjectedGraphReferences = new ResizableArray<InjectedGraphReference>(m_InjectedSubtreeReference.Count);
            } else {
                m_InjectedGraphReferences.Clear();
            }
            PopulateInjectedSubtreeReferences(ref m_InjectedGraphReferences);
        }

        /// <summary>
        /// Retrieves all of the injected subtree references and stores the result in m_InjectedSubtreeReferences.
        /// </summary>
        /// <param name="injectedGraphReferences">A reference to the array that the injected graph references should be added to.</param>
        private void PopulateInjectedSubtreeReferences(ref ResizableArray<InjectedGraphReference> injectedGraphReferences)
        {
            if (m_InjectedSubtreeReference == null || injectedGraphReferences == null) {
                return;
            }

            var nodeCount = 0;
            for (int i = 0; i < m_InjectedSubtreeReference.Count; ++i) {
                var injectedSubtreeReference = m_InjectedSubtreeReference[i];
                // Emit a single top-level reference entry per SubtreeReference node. Multiple selected subtrees
                // are represented by Nodes[subgraphIndex] instead of additional reference nodes.
                var injectedReferenceRuntimeNodeIndex = (ushort)(injectedSubtreeReference.NodeIndex + nodeCount);
                LogicNodeProperties graphReferenceNodeProperties = null;
                if (m_LogicNodeProperties != null && injectedReferenceRuntimeNodeIndex < m_LogicNodeProperties.Length) {
                    graphReferenceNodeProperties = m_LogicNodeProperties[injectedReferenceRuntimeNodeIndex];
                }
                IGraph injectedReferenceGraph = null;
                if (injectedSubtreeReference.Graphs != null) {
                    for (int j = 0; j < injectedSubtreeReference.Graphs.Length; ++j) {
                        if (injectedSubtreeReference.Graphs[j] != null) {
                            injectedReferenceGraph = injectedSubtreeReference.Graphs[j];
                            break;
                        }
                    }
                }
                var injectedReference = new InjectedGraphReference() {
                    Graph = injectedReferenceGraph,
                    NodeIndex = injectedSubtreeReference.NodeIndex,
                    RuntimeNodeIndex = injectedReferenceRuntimeNodeIndex,
                    NodeCount = injectedSubtreeReference.NodeCount,
                    GraphReference = injectedSubtreeReference.GraphReference,
                    Nodes = injectedSubtreeReference.Nodes,
                    GraphReferenceNodeProperties = graphReferenceNodeProperties
                };
                injectedGraphReferences.Add(injectedReference);

                // Track how far into the injected runtime span each selected subtree starts.
                var subtreeRuntimeOffset = (ushort)0;
                var nodeDelta = injectedSubtreeReference.NodeCount > 0 ? injectedSubtreeReference.NodeCount - 1 : 0; // The SubtreeRefrence node itself doesn't count.
                if (injectedSubtreeReference.Graphs == null || injectedSubtreeReference.Nodes == null) {
                    nodeCount += nodeDelta; 
                    continue;
                }
                for (int j = 0; j < injectedSubtreeReference.Graphs.Length; ++j) {
                    var graph = injectedSubtreeReference.Graphs[j];
                    ushort subtreeNodeCount = 0;
                    if (j < injectedSubtreeReference.Nodes.Length && injectedSubtreeReference.Nodes[j] != null) {
                        subtreeNodeCount = (ushort)injectedSubtreeReference.Nodes[j].Length;
                    }
                    if (graph != null && graph.InjectedGraphReferences != null) {
                        for (int k = 0; k < graph.InjectedGraphReferences.Length; ++k) {
                            var graphInjectedGraphReferences = graph.InjectedGraphReferences[k];
                            // Nested references already have a runtime-relative index within the selected subtree.
                            var nestedReferenceRuntimeNodeIndex = graphInjectedGraphReferences.RuntimeNodeIndex != ushort.MaxValue ? graphInjectedGraphReferences.RuntimeNodeIndex : graphInjectedGraphReferences.NodeIndex;
                            if (graph.LogicNodeProperties == null || nestedReferenceRuntimeNodeIndex >= graph.LogicNodeProperties.Length) {
                                continue;
                            }
                            var subgraphReferenceNodeProperties = graph.LogicNodeProperties[nestedReferenceRuntimeNodeIndex];

                            var subgraphInjectedReference = new InjectedGraphReference() {
                                Graph = graphInjectedGraphReferences.Graph,
                                NodeIndex = graphInjectedGraphReferences.NodeIndex,
                                // Compose runtime index in the root graph:
                                // root reference start + selected subtree start + nested reference offset.
                                RuntimeNodeIndex = (ushort)(injectedReferenceRuntimeNodeIndex + subtreeRuntimeOffset + nestedReferenceRuntimeNodeIndex),
                                NodeCount = graphInjectedGraphReferences.NodeCount,
                                GraphReference = graphInjectedGraphReferences.GraphReference,
                                Nodes = graphInjectedGraphReferences.Nodes,
                                GraphReferenceNodeProperties = subgraphReferenceNodeProperties
                            };
                            injectedGraphReferences.Add(subgraphInjectedReference);
                        }
                    }

                    subtreeRuntimeOffset += subtreeNodeCount;
                }
                nodeCount += nodeDelta; // The SubtreeRefrence node itself doesn't count.
            }
        }
#endif

        /// <summary>
        /// Remaps any ILogicNode references on the event node to the copied nodes.
        /// </summary>
        /// <param name="eventNode">The event node to update.</param>
        /// <param name="nodeMap">A map of original nodes to copied nodes.</param>
        private void RemapEventNodeReferences(IEventNode eventNode, Dictionary<object, object> nodeMap)
        {
            if (eventNode == null || nodeMap == null || nodeMap.Count == 0) {
                return;
            }

            var fields = eventNode.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; ++i) {
                var field = fields[i];
                var fieldType = field.FieldType;
                if (typeof(ILogicNode).IsAssignableFrom(fieldType)) {
                    var value = field.GetValue(eventNode);
                    if (value != null && nodeMap.TryGetValue(value, out var mapped)) {
                        field.SetValue(eventNode, mapped);
                    }
                } else if (typeof(IList).IsAssignableFrom(fieldType)) {
                    var elementType = Serializer.GetElementType(fieldType);
                    if (!typeof(ILogicNode).IsAssignableFrom(elementType)) {
                        continue;
                    }
                    var listValue = field.GetValue(eventNode) as IList;
                    if (listValue == null) {
                        continue;
                    }
                    for (int j = 0; j < listValue.Count; ++j) {
                        var listItem = listValue[j];
                        if (listItem != null && nodeMap.TryGetValue(listItem, out var mapped)) {
                            listValue[j] = mapped;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When the behavior tree loads not all tasks will be deserialized instantly. TaskA may reference TaskB but TaskB hasn't
        /// been deserialized yet. The TaskAssignment data structure will store all of the references that need to be restored after
        /// the behavior tree has fully been deserialized.
        /// </summary>
        /// <param name="tasks">The tasks that belong to the graph.</param>
        /// <param name="taskReferences">The tasks that should be referenced.</param>
        public static void AssignTaskReferences(ILogicNode[] tasks, ResizableArray<TaskAssignment> taskReferences)
        {
            if (taskReferences == null) {
                return;
            }

            for (int i = 0; i < taskReferences.Count; ++i) {
                var taskReference = taskReferences[i];
                var fieldType = taskReference.Field.FieldType;
                object value = null;

                // The field can be a list or single value.
                if (typeof(IList).IsAssignableFrom(fieldType)) {
                    var elements = (IList)taskReferences[i].Value;
                    if (fieldType.IsArray) {
                        // The field type is an array. Create a new array with all of the task instances.
                        var array = Array.CreateInstance(Serializer.GetElementType(fieldType), elements.Count) as ILogicNode[];
                        for (int j = 0; j < array.Length; ++j) {
                            var index = (ushort)elements[j];
                            if (index < tasks.Length) {
                                array[j] = tasks[index];
                            }
                        }
                        value = array;
                    } else {
                        // The field type is a list. Create a new list with all of the task instances.
                        IList taskList;
                        if (fieldType.IsGenericType) {
                            taskList = Activator.CreateInstance(typeof(List<>).MakeGenericType(Serializer.GetElementType(fieldType))) as IList;
                        } else {
                            taskList = Activator.CreateInstance(fieldType) as IList;
                        }

                        for (int j = 0; j < elements.Count; ++j) {
                            var index = (ushort)elements[j];
                            if (index < tasks.Length) {
                                taskList.Add(tasks[index]);
                            }
                        }
                        value = taskList;
                    }
                } else { // Single ILogicNode value.
                    var index = (ushort)taskReference.Value;
                    if (index < tasks.Length) {
                        value = tasks[index];
                    }
                }
                if (value != null) {
                    taskReference.Field.SetValue(taskReference.Target, value);
                }
            }
        }

        /// <summary>
        /// Returns the Node of the specified type.
        /// </summary>
        /// <param name="type">The type of Node that should be retrieved.</typeparam>
        /// <returns>The Node of the specified type (can be null).</returns>
        public ITreeLogicNode GetNode(Type type)
        {
            if (m_Tasks == null) {
                return null;
            }

            for (int i = 0; i < m_Tasks.Length; ++i) {
                if (m_Tasks[i].GetType() == type) {
                    return m_Tasks[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the event node of the specified type.
        /// </summary>
        /// <param name="type">The type of EventNode that should be retrieved.</typeparam>
        /// <returns>The EventNode of the specified type (can be null). If the node is found the index will also be returned.</returns>
        public (IEventNode, ushort) GetEventNode(Type type)
        {
            if (m_EventTasks == null) {
                return (null, ushort.MaxValue);
            }

            for (ushort i = 0; i < m_EventTasks.Length; ++i) {
                if (m_EventTasks[i].GetType() == type) {
                    return (m_EventTasks[i], i);
                }
            }
            return (null, ushort.MaxValue);
        }

        /// <summary>
        /// Returns the total number of children belonging to the specified node.
        /// </summary>
        /// <param name="node">The node to retrieve the child count of.</param>
        /// <param name="nodes">All of the nodes that belong to the graph.</param>
        /// <returns>The total number of children belonging to the specified node.</returns>
        public int GetChildCount(ITreeLogicNode node, ITreeLogicNode[] nodes)
        {
            if (node.SiblingIndex != ushort.MaxValue) {
                return node.SiblingIndex - node.Index - 1;
            }

            if (node.Index + 1 == nodes.Length) {
                return 0;
            }

            var child = nodes[node.Index + 1];
            if (child.ParentIndex != node.Index) {
                return 0;
            }

            // Determine the child count based off of the sibling index.
            while (child.SiblingIndex != ushort.MaxValue) {
                child = nodes[child.SiblingIndex];
            }

            return child.Index - node.Index + GetChildCount(child, nodes);
        }

        /// <summary>
        /// Reevaluates the ISubtreeReferenceNodes by calling the EvaluateSubgraphs method.
        /// </summary>
        /// <param name="graphComponent">The component that the graph is being deserialized from.</param>
        /// <param name="graph">The graph that is being reevaluated.</param>
        /// <param name="onBeforeReevaluationSwap">Action that should be done before the tasks are swapped.</param>
        /// <returns>True if the subtree was reevaluated.</returns>
        public bool ReevaluateSubtreeReferences(IGraphComponent graphComponent, IGraph graph, Action onBeforeReevaluationSwap)
        {
            // The tree must contain tasks.
            if (!Application.isPlaying || m_Tasks == null || m_Tasks.Length == 0) {
                return false;
            }

            // Subtree references must exist.
            if (m_InjectedSubtreeReference == null || m_InjectedSubtreeReference.Count == 0) {
                return false;
            }

            if (onBeforeReevaluationSwap != null) {
                onBeforeReevaluationSwap();
            }

            RemoveInjectedSubtreeEventNodes();
            var baseEventTaskCount = m_EventTasks != null ? m_EventTasks.Length : 0;

            // Find the new reevaluated nodes.
            for (int i = m_InjectedSubtreeReference.Count - 1; i >= 0; --i) {
                var subtreeNodesReference = m_InjectedSubtreeReference[i];
                var subtreeReference = m_InjectedSubtreeReference[i].GraphReference as ISubtreeReferenceNode;
                subtreeReference.EvaluateSubgraphs(graphComponent);
                var reevaluatedSubtrees = subtreeReference.Subtrees;
                if (reevaluatedSubtrees == null) {
                    continue;
                }

                // The parent must be able to accept the number of subtrees that there are.
                var parentIndex = m_Tasks[m_InjectedSubtreeReference[i].NodeIndex].ParentIndex;
                IParentNode parentNode = null;
                if (parentIndex != ushort.MaxValue) {
                    parentNode = m_Tasks[parentIndex] as IParentNode;
                }

                if ((parentNode == null && reevaluatedSubtrees.Length > 1) || (parentNode != null && reevaluatedSubtrees.Length > parentNode.MaxChildCount)) {
                    Debug.LogError($"Error: the reevaluated graph contains multiple subtrees as the starting task or as a child of a parent task which cannot contain so many children (such as a decorator).");
                    continue;
                }

                var reevaluatedNodes = new ITreeLogicNode[reevaluatedSubtrees.Length][];
                var errorState = false;
                for (int j = 0; j < reevaluatedSubtrees.Length; ++j) {
                    if (reevaluatedSubtrees[j] == null) {
                        continue;
                    }
                    if (!reevaluatedSubtrees[j].Deserialize(graphComponent, true, true, true, true, subtreeReference.SharedVariableOverrides)) {
                        errorState = true;
                        break;
                    };
                    // Keep a reference to the deserialized nodes. This will ensure they are unique and do not get overwritten.
                    reevaluatedNodes[j] = reevaluatedSubtrees[j].TreeLogicNodes;
                }
                if (errorState) {
                    continue;
                }

                // The subtree index will be offsetted from the original index value if there are multiple subtree references.
                var nodeOffset = 0;
                for (int j = i - 1; j >= 0; --j) {
                    nodeOffset += m_InjectedSubtreeReference[j].NodeCount > 0 ? m_InjectedSubtreeReference[j].NodeCount - 1 : 0;
                }

                // All of the reevaluated nodes have been determined. Remove the old subtree nodes.
                var nodeCount = m_InjectedSubtreeReference[i].NodeCount;

                // Replace the first node with the subtree reference, and remove the rest of the added nodes.
                m_Tasks[m_InjectedSubtreeReference[i].NodeIndex + nodeOffset] = m_InjectedSubtreeReference[i].GraphReference as ITreeLogicNode;
                for (int j = m_InjectedSubtreeReference[i].NodeIndex + nodeOffset + 1; j < m_Tasks.Length - nodeCount + 1; ++j) {
                    m_Tasks[j] = m_Tasks[j + nodeCount - 1];
                    m_Tasks[j].Index = (ushort)j;
                    if (m_Tasks[j].ParentIndex != ushort.MaxValue && m_Tasks[j].ParentIndex > m_InjectedSubtreeReference[i].NodeIndex + nodeOffset) {
                        m_Tasks[j].ParentIndex -= (ushort)(nodeCount - 1);
                    }
                    if (m_Tasks[j].SiblingIndex != ushort.MaxValue && m_Tasks[j].SiblingIndex > m_InjectedSubtreeReference[i].NodeIndex + nodeOffset) {
                        m_Tasks[j].SiblingIndex -= (ushort)(nodeCount - 1);
                    }

#if UNITY_EDITOR
                    m_LogicNodeProperties[j] = m_LogicNodeProperties[j + nodeCount - 1];
#endif
                }

                // Restore the original sibling index value for parent nodes.
                parentIndex = m_Tasks[m_InjectedSubtreeReference[i].NodeIndex + nodeOffset].ParentIndex;
                while (parentIndex != ushort.MaxValue) {
                    var parentTask = m_Tasks[parentIndex];
                    if (parentTask.SiblingIndex != ushort.MaxValue) {
                        parentTask.SiblingIndex -= (ushort)(nodeCount - 1);
                        m_Tasks[parentIndex] = parentTask;
                    }
                    parentIndex = parentTask.ParentIndex;
                }

                // Restore the original ConnectedIndex value.
                if (m_EventTasks != null) {
                    for (int j = 0; j < m_EventTasks.Length; ++j) {
                        if (m_EventTasks[j].ConnectedIndex > m_InjectedSubtreeReference[i].NodeIndex) {
                            m_EventTasks[j].ConnectedIndex -= (ushort)(nodeCount - 1);
                        }
                    }
                }
                Array.Resize(ref m_Tasks, m_Tasks.Length - nodeCount + 1);
#if UNITY_EDITOR
                Array.Resize(ref m_LogicNodeProperties, m_LogicNodeProperties.Length - nodeCount + 1);
#endif

                // Replace the old nodes with the new nodes.
                subtreeNodesReference.Nodes = reevaluatedNodes;
                m_InjectedSubtreeReference[i] = subtreeNodesReference;
            }

            // The tasks array has been restored to the original set of nodes with the ISubtreeReference. Inject the new nodes.
            InjectSubtrees();

            // Modify the ConnectedIndex to match the injection for the base event tasks.
            if (m_EventTasks != null && m_InjectedSubtreeReference != null && baseEventTaskCount > 0) {
                for (int i = 0; i < baseEventTaskCount; ++i) {
                    var offset = 0;
                    for (int j = 0; j < m_InjectedSubtreeReference.Count; ++j) {
                        if (m_InjectedSubtreeReference[j].NodeIndex >= m_EventTasks[i].ConnectedIndex) {
                            break;
                        }
                        offset += m_InjectedSubtreeReference[j].NodeCount > 0 ? m_InjectedSubtreeReference[j].NodeCount - 1 : 0;
                    }
                    if (offset != 0) {
                        m_EventTasks[i].ConnectedIndex += (ushort)offset;
                    }
                }
            }

#if UNITY_EDITOR
            UpdateInjectedGraphReferences();
#endif

            return true;
        }

        /// <summary>
        /// Removes any injected subtree event nodes and their appended logic nodes.
        /// </summary>
        private void RemoveInjectedSubtreeEventNodes()
        {
            if (m_EventTasks == null || m_EventTasks.Length == 0 || m_InjectedSubtreeEventNodes == null || m_InjectedSubtreeEventNodes.Count == 0) {
                return;
            }

            var originalEventTasks = m_EventTasks;
#if UNITY_EDITOR
            var originalEventNodeProperties = m_EventNodeProperties;
#endif
            var totalBranchCount = 0;
            var injectedEventTaskCount = 0;
            for (int i = 0; i < originalEventTasks.Length; ++i) {
                var eventTask = originalEventTasks[i];
                if (eventTask == null || !m_InjectedSubtreeEventNodes.Contains(eventTask)) {
                    continue;
                }

                injectedEventTaskCount++;
                if (eventTask.ConnectedIndex == ushort.MaxValue || eventTask.ConnectedIndex >= m_Tasks.Length) {
                    continue;
                }
                totalBranchCount += GetChildCount(m_Tasks[eventTask.ConnectedIndex], m_Tasks) + 1;
            }

            if (injectedEventTaskCount == 0) {
                m_InjectedSubtreeEventNodes.Clear();
                return;
            }

            if (totalBranchCount > 0 && totalBranchCount <= m_Tasks.Length) {
                Array.Resize(ref m_Tasks, m_Tasks.Length - totalBranchCount);
#if UNITY_EDITOR
                Array.Resize(ref m_LogicNodeProperties, m_LogicNodeProperties.Length - totalBranchCount);
#endif
            }

            var retainedEventTaskCount = originalEventTasks.Length - injectedEventTaskCount;
            var retainedEventTasks = new IEventNode[retainedEventTaskCount];
            var retainedIndex = 0;
            for (int i = 0; i < originalEventTasks.Length; ++i) {
                var eventTask = originalEventTasks[i];
                if (eventTask != null && m_InjectedSubtreeEventNodes.Contains(eventTask)) {
                    continue;
                }

                if (eventTask != null) {
                    eventTask.Index = (ushort)retainedIndex;
                }
                retainedEventTasks[retainedIndex] = eventTask;
                retainedIndex++;
            }
            m_EventTasks = retainedEventTasks;
#if UNITY_EDITOR
            if (originalEventNodeProperties != null) {
                var retainedEventNodeProperties = new NodeProperties[retainedEventTaskCount];
                retainedIndex = 0;
                for (int i = 0; i < originalEventTasks.Length && i < originalEventNodeProperties.Length; ++i) {
                    var eventTask = originalEventTasks[i];
                    if (eventTask != null && m_InjectedSubtreeEventNodes.Contains(eventTask)) {
                        continue;
                    }
                    retainedEventNodeProperties[retainedIndex] = originalEventNodeProperties[i];
                    retainedIndex++;
                }
                m_EventNodeProperties = retainedEventNodeProperties;
            }
#endif
            m_InjectedSubtreeEventNodes.Clear();
        }

        /// <summary>
        /// Returns the SharedVariable with the specified name.
        /// </summary>
        /// <param name="graph">The graph that the data belongs to.</typeparam>
        /// <param name="name">The name of the SharedVariable that should be retrieved.</typeparam>
        /// <param name="scope">The scope of the SharedVariable that should be retrieved.</param>
        /// <returns>The SharedVariable with the specified name (can be null).</returns>
        public SharedVariable GetVariable(IGraph graph, PropertyName name, SharedVariable.SharingScope scope)
        {
            if (m_VariableByNameMap == null) {
                DeserializeSharedVariables(graph, false, true, null);
            }

            if (m_VariableByNameMap != null && m_VariableByNameMap.TryGetValue(new VariableAssignment(name, scope), out var variable)) {
                return variable;
            }

            return null;
        }

        /// <summary>
        /// Returns the SharedVariable of the specified type.
        /// </summary>
        /// <param name="graph">The graph that the data belongs to.</typeparam>
        /// <param name="name">The name of the SharedVariable that should be retrieved.</typeparam>
        /// <param name="scope">The scope of the SharedVariable that should be retrieved.</param>
        /// <returns>The SharedVariable with the specified name (can be null).</returns>
        public SharedVariable<T> GetVariable<T>(IGraph graph, PropertyName name, SharedVariable.SharingScope scope)
        {
            return GetVariable(graph, name, scope) as SharedVariable<T>;
        }

        /// <summary>
        /// Sets the value of the SharedVariable.
        /// </summary>
        /// <typeparam name="T">The type of SharedVarible.</typeparam>
        /// <param name="graph">The graph that the data belongs to.</typeparam>
        /// <param name="name">The name of the SharedVariable.</param>
        /// <param name="value">The value of the SharedVariable.</param>
        /// <param name="scope">The scope of the SharedVariable that should be set.</typeparam>
        /// <returns>True if the value was set.</returns>
        public bool SetVariableValue<T>(IGraph graph, PropertyName name, T value, SharedVariable.SharingScope scope)
        {
            if (m_VariableByNameMap == null) {
                DeserializeSharedVariables(graph, false, true, null);
            }

            if (m_VariableByNameMap == null || !m_VariableByNameMap.TryGetValue(new VariableAssignment(name, scope), out var variable)) {
                return false;
            }

            (variable as SharedVariable<T>).Value = value;
            return true;
        }

        /// <summary>
        /// Overrides the SharedVariable binding. The name must match an exsting variable.
        /// </summary>
        /// <param name="graph">The graph that the data belongs to.</typeparam>
        /// <param name="variable">The reference to the SharedVariable.</param>
        internal void OverrideVariableBinding(IGraph graph, SharedVariable variable)
        {
            if (string.IsNullOrEmpty(variable.Name)) {
                return;
            }

            DeserializeSharedVariables(graph, false, true, null);
            var dirty = false;
            if (m_SharedVariables != null) {
                for (int i = 0; i < m_SharedVariables.Length; ++i) {
                    if (m_SharedVariables[i].Name == variable.Name) {
                        var variableType = variable.GetType();
                        if (variableType.IsGenericType && variableType.GetGenericTypeDefinition().IsAssignableFrom(typeof(SharedVariableBinding<>))) {
                            m_SharedVariables[i] = variable.Clone() as SharedVariable;
                            dirty = true;
                        }
                        break;
                    } 
                }
            }

            if (dirty) {
                // The graph may be a BehaviorTree pointing to local variables while this data belongs to a subtree.
                // Rebuild graph-scope mappings from this data's variables to preserve the cloned binding instance.
                m_VariableByNameMap = PopulateSharedVariablesMapping(graph, m_SharedVariables, true);
            }
        }

        /// <summary>
        /// Replaces the data with the specified BehaviorTreeData.
        /// </summary>
        /// <param name="graph">The graph that the current data belongs to.</param>
        /// <param name="other">The data that should be replaced.</param>
        /// <param name="originalSharedVariables">The SharedVariables of the current graph.</param>
        internal void OverrideData(IGraph graph, BehaviorTreeData other, SharedVariable[] originalSharedVariables, bool updateFields)
        {
            EventNodes = other.EventNodes;
            LogicNodes = other.LogicNodes;
            InjectedSubtreeReferences = other.InjectedSubtreeReferences;
            m_InjectedSubtreeEventNodes = other.m_InjectedSubtreeEventNodes;
            m_SharedVariables = other.SharedVariables;
            m_SharedVariableData = other.m_SharedVariableData;
            m_VariableByNameMap = PopulateSharedVariablesMapping(graph, false);
            m_DisabledLogicNodes = other.DisabledLogicNodes;
            m_DisabledEventNodes = other.DisabledEventNodes;
            // The other tree may be pooled. Update the variable references to point to the local graph variables.
            if (updateFields && other.m_VariableFields != null) {
                for (int i = 0; i < other.m_VariableFields.Count; ++i) {
                    var variableField = other.m_VariableFields[i];
                    var localVariable = GetVariable(graph, variableField.Name, SharedVariable.SharingScope.Graph);
                    if (localVariable != null) {
                        variableField.Field.SetValue(variableField.Task, localVariable);
                    }
                }
            }
            // The original tree variable value should override the other variable value.
            if (originalSharedVariables != null) {
                for (int i = 0; i < originalSharedVariables.Length; ++i) {
                    OverrideVariableValue(graph, originalSharedVariables[i]);
                }
            }
#if UNITY_EDITOR
            m_EventNodeProperties = other.EventNodeProperties;
            m_LogicNodeProperties = other.LogicNodeProperties;
            m_SharedVariableGroups = other.SharedVariableGroups;
            m_SharedVariableGroupsData = other.m_SharedVariableGroupsData;
            m_GroupProperties = other.GroupProperties;
            m_InjectedGraphReferences = other.m_InjectedGraphReferences;
#endif
        }

        /// <summary>
        /// Overrides the SharedVariable value. The name must match an exsting variable.
        /// </summary>
        /// <param name="graph">The graph that the data belongs to.</typeparam>
        /// <param name="variable">The reference to the SharedVariable.</param>
        /// <returns>True if the value was overridden.</returns>
        private bool OverrideVariableValue(IGraph graph, SharedVariable variable)
        {
            if (string.IsNullOrEmpty(variable.Name)) {
                return false;
            }

            var dirty = false;
            if (m_SharedVariables != null) {
                for (int i = 0; i < m_SharedVariables.Length; ++i) {
                    if (m_SharedVariables[i].Name == variable.Name) {
                        var variableType = variable.GetType();
                        if (m_SharedVariables[i].GetType() == variableType) {
                            m_SharedVariables[i].SetValue(variable.GetValue());
                            dirty = true;
                        }
                        break;
                    }
                }
            }

            return dirty;
        }

        /// <summary>
        /// Is the node with the specified index enabled?
        /// </summary>
        /// <param name="logicNode">Is the node a LogicNode?</param>
        /// <param name="index">The index of the node.</param>
        /// <returns>True if the node with the specified index is enabled.</returns>
        public bool IsNodeEnabled(bool logicNode, int index)
        {
            if (index == ushort.MaxValue) {
                return true;
            }

            // Check the Enabled property on the node directly.
            if (logicNode) {
                if (m_Tasks == null || index >= m_Tasks.Length) {
                    return true;
                }
                return m_Tasks[index].Enabled;
            } else {
                if (m_EventTasks == null || index >= m_EventTasks.Length) {
                    return true;
                }
                return m_EventTasks[index].Enabled;
            }
        }
    }
}
#endif