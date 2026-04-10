/// ---------------------------------------------
/// Formations Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Tasks;
    using UnityEngine;
    using System.Collections.Generic;

    /// <summary>
    /// Manages formation groups and their agents.
    /// </summary>
    public class FormationsManager : MonoBehaviour
    {
        public static FormationsManager Instance
        {
            get {
                if (s_Instance == null) {
                    var formationManager = new GameObject("Formations Manager");
                    s_Instance = formationManager.AddComponent<FormationsManager>();
                }
                return s_Instance;
            }
        }
        private static FormationsManager s_Instance;

        [Tooltip("The amount of time before the formation group starts to move after registering. This allows all agents to register themselves without the formation starting too soon.")]
        [SerializeField] protected float m_StartDelay = 0.1f;

        public static float StartDelay => Instance.m_StartDelay;

        private Dictionary<int, FormationGroup> m_FormationGroups;

        /// <summary>
        /// The current state of the formation.
        /// </summary>
        public enum FormationState
        {
            Initialized,        // The formation has been initialized.
            MoveToFormation,    // The members are moving into formation.
            MoveToTarget,       // The members are moving to the target set by the leader.
            Arrived,            // The members have arrived at the target.
            Failure             // The formation has failed.
        }

        /// <summary>
        /// Stores the current state of a formation and its members.
        /// </summary>
        public class FormationGroup
        {
            [Tooltip("The leader of this formation group.")]
            protected FormationsBase m_Leader;
            [Tooltip("The list of agents in this formation group.")]
            protected List<FormationsBase> m_Members;
            [Tooltip("The current state of the formation.")]
            protected FormationState m_State;
            [Tooltip("The target position for the formation.")]
            protected Vector3 m_TargetPosition;
            [Tooltip("The forward direction the formation should face.")]
            protected Vector3 m_Direction;
            [Tooltip("The time when the formation group was created.")]
            protected float m_StartTime;

            public FormationsBase Leader { get => m_Leader; set => m_Leader = value;  }
            public List<FormationsBase> Members { get => m_Members; set => m_Members = value; }
            public FormationState State { get => m_State; set => m_State = value; }
            public Vector3 TargetPosition { get => m_TargetPosition; set => m_TargetPosition = value; }
            public Vector3 Direction { get => m_Direction; set => m_Direction = value; }
            public float StartTime { get => m_StartTime; set => m_StartTime = value; }

            /// <summary>
            /// Default constructor.
            /// </summary>
            public FormationGroup()
            {
                m_Members = new List<FormationsBase>();
                m_Leader = null;
                m_TargetPosition = Vector3.zero;
                m_Direction = Vector3.forward;
                m_State = FormationState.Initialized;
            }
        }

        /// <summary>
        /// Initialize any default objects.
        /// </summary>
        private void Awake()
        {
            m_FormationGroups = new Dictionary<int, FormationGroup>();
        }

        /// <summary>
        /// The object has been enabled.
        /// </summary>
        private void OnEnable()
        {
            s_Instance = this;
        }

        /// <summary>
        /// Gets or creates a new formation group with the specified ID.
        /// </summary>
        /// <param name="groupID">The ID of the group to create.</param>
        /// <returns>The new group.</returns>
        private FormationGroup GetOrCreateFormationGroup(int groupID)
        {
            var group = GetFormationGroup(groupID);
            if (group != null) {
                return group;
            }
            group = new FormationGroup();
            m_FormationGroups[groupID] = group;
            return group;
        }

        /// <summary>
        /// Gets the formation group from the specified ID.
        /// </summary>
        /// <param name="groupID">The ID of the group.</param>
        /// <returns>The formation group from the specified ID (can be null).</returns>
        public static FormationGroup GetFormationGroup(int groupID)
        {
            return Instance.GetFormationGroupInternal(groupID);
        }

        /// <summary>
        /// Internal method which gets the formation group from the specified ID.
        /// </summary>
        /// <param name="groupID">The ID of the group.</param>
        /// <returns>The formation group from the specified ID (can be null).</returns>
        private FormationGroup GetFormationGroupInternal(int groupID)
        {
            if (m_FormationGroups.TryGetValue(groupID, out var group)) {
                return group;
            }
            return null;
        }

        /// <summary>
        /// Add a formation task to a group. If an agent is already within a group that agent will be removed from the old group.
        /// </summary>
        /// <param name="groupID">The ID of the group to add to.</param>
        /// <param name="task">The formation task to add.</param>
        /// <returns>True if the task was added to the group.</returns>
        public static bool AddTaskToGroup(int groupID, FormationsBase task)
        {
            return Instance.AddTaskToGroupInternal(groupID, task);
        }

        /// <summary>
        /// Internal method which adds a formation task to a group. If an agent is already within a group that agent will be removed from the old group.
        /// </summary>
        /// <param name="groupID">The ID of the group to add to.</param>
        /// <param name="task">The formation task to add.</param>
        /// <returns>True if the task was added to the group.</returns>
        private bool AddTaskToGroupInternal(int groupID, FormationsBase task)
        {
            if (task == null) {
                return false;
            }

            RemoveTaskFromExistingGroup(task);

            // Don't allow new agents to join if the group is in a terminal state.
            var group = GetOrCreateFormationGroup(groupID);
            if (group.State == FormationState.Arrived || group.State == FormationState.Failure) {
                return false;
            }

            if (task.ForceLeader && (group.Leader == null || !group.Leader.ForceLeader)) {
                // The leader should always be the first member.
                group.Members.Insert(0, task);
                group.Leader = task;
                if (group.Members.Count == 1) {
                    group.StartTime = Time.time;
                }
            } else {
                group.Members.Add(task);
                // The first task should be the leader if no leader exists.
                if (group.Members.Count == 1) {
                    group.Leader = task;
                    group.StartTime = Time.time;
                }
#if UNITY_EDITOR
                else {
                    // 2D sanity check.
                    if (group.Members[0].Is2D != task.Is2D) {
                        Debug.LogWarning("Warning: The added task does not use the same perspective as the group leader.");
                    }
                }
#endif
            }

            // If the group is already in progress, assign the last index to the new agent and update the positions.
            if (group.State != FormationState.Initialized) {
                task.Group = group;
                task.UpdateFormationIndex(group.Members.Count - 1);
                for (int i = 0; i < group.Members.Count; ++i) {
                    group.Members[i].DesiredPosition = group.Members[i].UpdateFormationDestination();
                }
            }

            return true;
        }

        /// <summary>
        /// Assigns formation indices.
        /// </summary>
        /// <param name="group">The formation group to assign indices for.</param>
        public static void AssignIndices(int groupID)
        {
            Instance.AssignIndicesInternal(groupID);
        }

        /// <summary>
        /// Internal method which assigns formation indices.
        /// </summary>
        /// <param name="group">The formation group to assign indices for.</param>
        public void AssignIndicesInternal(int groupID)
        {
            var group = GetFormationGroup(groupID);
            if (group == null) {
                return;
            }

            if (!group.Leader.AssignOptimialIndicies) {
                for (int i = 0; i < group.Members.Count; ++i) {
                    group.Members[i].UpdateFormationIndex(i);
                }
                return;
            }

            // If requested assign the indicies based on their proximity to target positions.
            var formationPositions = new List<Vector3>();
            for (int i = 0; i < group.Members.Count; ++i) {
                formationPositions.Add(group.Leader.CalculateFormationPosition(i, group.Members.Count, group.TargetPosition, group.Direction, false));
            }

            // Create a list of available formation indices.
            var availableIndices = new List<int>();
            for (int i = 0; i < group.Members.Count; ++i) {
                availableIndices.Add(i);
            }

            // For each agent, find the closest available formation position.
            for (int i = 0; i < group.Members.Count; ++i) {
                var agent = group.Members[i];
                var minDistance = float.MaxValue;
                var bestIndex = -1;

                // Find the closest available formation position
                for (int j = 0; j < availableIndices.Count; ++j) {
                    var formationIndex = availableIndices[j];
                    var distance = Vector3.SqrMagnitude(agent.Transform.position - formationPositions[formationIndex]);
                    if (distance < minDistance) {
                        minDistance = distance;
                        bestIndex = j;
                    }
                }

                // Assign the agent to the closest available position
                if (bestIndex != -1) {
                    agent.UpdateFormationIndex(availableIndices[bestIndex], formationPositions[availableIndices[bestIndex]]);
                    availableIndices.RemoveAt(bestIndex);
                }
            }
        }

        /// <summary>
        /// Remove a formation task from a group and update remaining indices.
        /// </summary>
        /// <param name="groupID">The ID of the group to remove from.</param>
        /// <param name="task">The formation task to remove.</param>
        public static void RemoveTaskFromGroup(int groupID, FormationsBase task)
        {
            if (s_Instance == null) {
                return;
            }
            s_Instance.RemoveTaskFromGroupInternal(groupID, task);
        }

        /// <summary>
        /// Remove a formation task from a group and update remaining indices.
        /// </summary>
        /// <param name="groupID">The ID of the group to remove from.</param>
        /// <param name="task">The formation task to remove.</param>
        private void RemoveTaskFromGroupInternal(int groupID, FormationsBase task)
        {
            if (task == null || !m_FormationGroups.TryGetValue(groupID, out var group)) {
                return;
            }

            var index = group.Members.IndexOf(task);
            if (index != -1) {
                group.Members.RemoveAt(index);
                // Remove the formation group if there are no more members.
                if (group.Members.Count == 0) {
                    m_FormationGroups.Remove(groupID);
                } else {
                    if (group.Leader.FailOnAgentRemoval) {
                        group.State = FormationState.Failure;
                    } else {
                        // Assign a new leader if the leader left.
                        if (group.Leader == task) {
                            if (group.Members.Count > 0) {
                                group.Leader = group.Members[0];
                                // Update the formation target based on the new leader's position
                                if (group.State == FormationState.MoveToTarget) {
                                    group.Leader.UpdateFormationDestination();
                                }
                            } else {
                                group.Leader = null;
                                group.State = FormationState.Failure;
                                return;
                            }
                        }

                        // Update indices for remaining tasks to indicate their new position.
                        if (group.Leader.UpdateUnitLocationsOnAgentRemoval) {
                            for (int i = index; i < group.Members.Count; ++i) {
                                group.Members[i].UpdateFormationIndex(i);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Remove a formation task from any group it's in.
        /// </summary>
        /// <param name="task">The formation task to remove.</param>
        private void RemoveTaskFromExistingGroup(FormationsBase task)
        {
            foreach (var group in m_FormationGroups) {
                if (group.Value.Members.Contains(task)) {
                    RemoveTaskFromGroup(group.Key, task);
                    break;
                }
            }
        }

        /// <summary>
        /// The component has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            s_Instance = null;
        }

        /// <summary>
        /// Reset the static variables for domain reloading.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DomainReset()
        {
            s_Instance = null;
        }
    }
}
