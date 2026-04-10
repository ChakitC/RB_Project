/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Base class for all Formation Pack tasks.
    /// </summary>
    [Category("Formation Pack")]
    public abstract class FormationsBase : Action
    {
#if UNITY_EDITOR
        private const string c_PathfinderTypeKey = "Opsive.BehaviorDesigner.AddOns.PathfinderType";
        public static string PathfinderTypeKey => c_PathfinderTypeKey;
#endif

        /// <summary>
        /// Specifies the orientation of the group.
        /// </summary>
        public enum OrientationDirection
        {
            TransformDirection, // Use the leader's forward direction.
            MovementDirection,  // Use the direction from the target position to the Transform position.
            Specified           // Use the specified vector.
        }

        [Tooltip("The pathfinding implementation for this agent.")]
        [SerializeField] [HideInInspector] protected Pathfinder m_Pathfinder;
        [Tooltip("Is this formation being used in a 2D space?")]
        [SerializeField] protected bool m_Is2D = false;
        [Tooltip("The ID of the formation group this task will belong to.")]
        [SerializeField] protected SharedVariable<int> m_FormationGroupID;
        [Tooltip("Should the agent be forced as the leader?")]
        [SerializeField] protected SharedVariable<bool> m_ForceLeader;
        [Tooltip("Specifies the orientation of the group.")]
        [SerializeField] protected SharedVariable<OrientationDirection> m_FormationDirection;
        [Tooltip("The forward direction the formation should face (if not specified, will face the movement direction)")]
        [SerializeField] protected SharedVariable<Vector3> m_SpecifiedDirection = Vector3.forward;
        [Tooltip("Specifies if the agents should first move into the formation before moving to the target.")]
        [SerializeField] protected SharedVariable<bool> m_MoveToInitialFormation = true;
        [Tooltip("The rotation speed in degrees per second.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 180f;
        [Tooltip("The angle threshold in degrees at which the agent will stop rotating towards the target direction.")]
        [SerializeField] protected SharedVariable<float> m_RotationThreshold = 1f;
        [Tooltip("The distance difference between the agent and the leader when the agent is considered out of range. This value must be larger than the InRnageDistanceDelta.")]
        [SerializeField] protected SharedVariable<float> m_OutOfRangeDistanceDelta = 0.6f;
        [Tooltip("The distance difference between the agent and the leader when the agent is considered back in range. This value must be smaller than the OutOfRangeDistanceDelta.")]
        [SerializeField] protected SharedVariable<float> m_InRangeDistanceDelta = 0.4f;
        [Tooltip("The speed multiplier when the agent is out of range.")]
        [SerializeField] protected SharedVariable<float> m_OutOfRangeSpeedMultiplier = 0.5f;
        [Tooltip("The amount of time that must elapse with a zero velocity before the agent is considered stuck.")]
        [SerializeField] protected SharedVariable<float> m_StuckDuration = 0.2f;
        [Tooltip("Should the task fail if any agent leaves the formation?")]
        [SerializeField] protected SharedVariable<bool> m_FailOnAgentRemoval;
        [Tooltip("Should the formation unit locations be updated if an agent leaves?")]
        [SerializeField] protected SharedVariable<bool> m_UpdateUnitLocationsOnAgentRemoval;
        [Tooltip("Should the pathfinder be stopped when the task ends?")]
        [SerializeField] protected SharedVariable<bool> m_StopOnTaskEnd = true;

        public Pathfinder Pathfinder { get => m_Pathfinder; set => m_Pathfinder = value; }
        public bool Is2D => m_Is2D;
        public bool ForceLeader => m_ForceLeader.Value;
        public bool UpdateUnitLocationsOnAgentRemoval => m_UpdateUnitLocationsOnAgentRemoval.Value;

        public float OutOfRangeSpeedMultiplier => m_OutOfRangeSpeedMultiplier.Value;
        public bool FailOnAgentRemoval => m_FailOnAgentRemoval.Value;
        public OrientationDirection FormationDirection => m_FormationDirection.Value;
        public Transform Transform { get => transform; }

        protected int m_FormationIndex = -1;
        protected FormationsManager.FormationGroup m_Group;
        private int m_GroupID = -1;
        private float m_OriginalSpeed;
        private Vector3 m_DesiredPosition;
        private bool m_OutOfRange;
        private bool m_IsStuck;
        private float m_StuckTime = -1;

        /// <summary>
        /// Should the optimal indicies be assigned? This should be set to false for random formations.
        /// </summary>
        public virtual bool AssignOptimialIndicies => true;

        /// <summary>
        /// Can the agents move into their initial formation?
        /// </summary>
        public virtual bool CanMoveIntoInitialFormation => true;

        /// <summary>
        /// Should the agents be checked to determine if they are out of range while moving?
        /// </summary>
        public virtual bool DetermineOutOfRange => true;

        /// <summary>
        /// Should the task determine if any agents are stuck?
        /// </summary>
        protected virtual bool DetermineIfStuck => true;

        /// <summary>
        /// Returns the position the formation should move towards.
        /// </summary>
        public abstract Vector3 TargetPosition { get; }

        public int FormationIndex { get => m_FormationIndex; }
        public FormationsManager.FormationGroup Group { set { m_Group = value; } }
        public float OriginalSpeed { get => m_OriginalSpeed; }

        public Vector3 DesiredPosition { get => m_DesiredPosition; set => m_DesiredPosition = value; }
        public bool OutOfRange { get => m_OutOfRange; set => m_OutOfRange = value; }
        public bool IsStuck { get => m_IsStuck; }

        /// <summary>
        /// Calculate the position for this agent in the formation.
        /// </summary>
        /// <param name="index">The index of this agent in the formation.</param>
        /// <param name="totalAgents">The total number of agents in the formation.</param>
        /// <param name="center">The center position of the formation.</param>
        /// <param name="forward">The forward direction of the formation.</param>
        /// <param name="samplePosition">Should the position be sampled?</param>
        /// <returns>The position for this agent.</returns>
        public abstract Vector3 CalculateFormationPosition(int index, int totalAgents, Vector3 center, Vector3 forward, bool samplePosition);

        /// <summary>
        /// The task has been initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            if (m_Pathfinder == null) {
                m_Pathfinder = new NavMeshAgentPathfinder();
                m_Pathfinder.Reset();
            }
            m_Pathfinder.Initialize(gameObject);
            m_FormationGroupID.OnValueChange += FormationGroupIDChanged;
            m_OriginalSpeed = m_Pathfinder.Speed;
        }

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            // Add this task to the formation group. The index will be set after all of the agents have been added to the group.
            FormationsManager.AddTaskToGroup(m_FormationGroupID.Value, this);
            m_GroupID = m_FormationGroupID.Value;
            m_Group = FormationsManager.GetFormationGroup(m_GroupID);
            m_Pathfinder.OnStart();
            m_IsStuck = false;
            m_StuckTime = -1;
        }

        /// <summary>
        /// Move the formation to the target position.
        /// </summary>
        /// <returns>The status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Group == null) {
                return TaskStatus.Failure;
            }

            if (m_Group.State == FormationsManager.FormationState.Arrived) {
                return TaskStatus.Success;
            } else if (m_Group.State == FormationsManager.FormationState.Failure) {
                return TaskStatus.Failure;
            }

            // Agents should rotate towards the target direction after they have arrived in their initial positions.
            if (m_Group.State == FormationsManager.FormationState.MoveToFormation && HasArrived()) {
                RotateTowardsTarget();
            }

            // The leader manages the state of all of its members.
            if (m_Group.Leader == this) {
                ManageFormation();
            }
            // The agent may be stuck if they haven't moved in awhile.
            if (DetermineIfStuck && !m_Pathfinder.HasArrived() && m_Pathfinder.Velocity.sqrMagnitude < 0.1f) {
                if (m_StuckTime == -1) {
                    m_StuckTime = Time.time;
                } else if (m_StuckTime + m_StuckDuration.Value < Time.time) {
                    m_IsStuck = true;
                }
            } else if (m_StuckTime != -1) {
                m_StuckTime = -1;
                m_IsStuck = false;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Rotate towards the target direction.
        /// </summary>
        protected void RotateTowardsTarget()
        {
            // Calculate the amount that the agent needs to rotate.
            var forward = Vector3.ProjectOnPlane(m_Transform.forward, m_Transform.up).normalized;
            var targetDirection = Vector3.ProjectOnPlane(m_Group.Direction, m_Transform.up).normalized;
            var angle = Vector3.SignedAngle(forward, targetDirection, transform.up);

            // Stop rotating when facing the correct direction.
            if (Mathf.Abs(angle) < m_RotationThreshold.Value) {
                return;
            }

            // The agent still needs to rotate.
            var rotation = Quaternion.AngleAxis(Mathf.Sign(angle) * m_RotationSpeed.Value * Time.deltaTime, m_Transform.up);
            m_Transform.rotation = rotation * m_Transform.rotation;
        }

        /// <summary>
        /// Manages the pathfinding and state of the formation for all members. This should only be run by the formation Leader.
        /// </summary>
        private void ManageFormation()
        {
            if (m_Group.State == FormationsManager.FormationState.Initialized) {
                if (m_Group.StartTime + FormationsManager.StartDelay < Time.time) {
                    m_Group.State = FormationsManager.FormationState.MoveToFormation;
                    // Move all agents to their initial formation location.
                    m_Group.TargetPosition = m_Transform.position;
                    if (m_FormationDirection.Value == OrientationDirection.TransformDirection) {
                        m_Group.Direction = m_Transform.forward;
                    } else if (m_FormationDirection.Value == OrientationDirection.MovementDirection) {
                        m_Group.Direction = (TargetPosition - m_Transform.position).normalized;
                    } else { // Use the specified vector.
                        m_Group.Direction = m_SpecifiedDirection.Value.normalized;
                    }
                    FormationsManager.AssignIndices(m_FormationGroupID.Value);
                }
            } else if (m_Group.State == FormationsManager.FormationState.MoveToFormation) {
                var arrived = true;
                if (CanMoveIntoInitialFormation && m_MoveToInitialFormation.Value) {
                    for (int i = 0; i < m_Group.Members.Count; ++i) {
                        if (!m_Group.Members[i].HasArrived()) {
                            arrived = false;
                            if (m_Group.Members[i].IsStuck) {
                                m_Group.State = FormationsManager.FormationState.Failure;
                                return;
                            }
                            break;
                        }
                    }
                }
                if (arrived && (!CanMoveIntoInitialFormation || !m_MoveToInitialFormation.Value || HaveAllAgentsRotated())) {
                    m_Group.State = FormationsManager.FormationState.MoveToTarget;
                    m_Group.TargetPosition = TargetPosition;
                    for (int i = 0; i < m_Group.Members.Count; ++i) {
                        m_Group.Members[i].DesiredPosition = m_Group.Members[i].UpdateFormationDestination();
                    }
                }
            } else if (m_Group.State == FormationsManager.FormationState.MoveToTarget) {
                var arrived = true;
                var dirty = false;
                var groupOutOfRange = false;
                for (int i = 0; i < m_Group.Members.Count; ++i) {
                    if (!m_Group.Members[i].HasArrived()) {
                        arrived = false;
                        if (m_Group.Members[i].IsStuck) {
                            m_Group.State = FormationsManager.FormationState.Failure;
                            return;
                        }

                        if (DetermineOutOfRange) {
                            var wasOutOfRange = m_Group.Members[i].OutOfRange;
                            m_Group.Members[i].OutOfRange = m_Group.Members[i].IsOutOfRange();
                            dirty = dirty || (wasOutOfRange != m_Group.Members[i].OutOfRange);
                            if (m_Group.Members[i].OutOfRange) {
                                if (m_Group.Members[i].OutOfRangeSpeedMultiplier > 1) {
                                    // If the multiplier is greater than 1 then only the current agent needs to speed up.
                                    m_Group.Members[i].Pathfinder.Speed = m_Group.Members[i].OriginalSpeed * m_Group.Members[i].OutOfRangeSpeedMultiplier;
                                } else {
                                    // If the multiplier is less than 1 then all of the other agents need to slow down so the current agent can catch up.
                                    groupOutOfRange = true;
                                }
                            } else if (wasOutOfRange && m_Group.Members[i].OutOfRangeSpeedMultiplier > 1) {
                                m_Group.Members[i].Pathfinder.Speed = m_Group.Members[i].OriginalSpeed;
                            }
                        }
                    }
                }
                if (arrived) {
                    m_Group.State = FormationsManager.FormationState.Arrived;
                } else if (dirty) {
                    for (int i = 0; i < m_Group.Members.Count; ++i) {
                        if (!groupOutOfRange || m_Group.Members[i].OutOfRange) {
                            m_Group.Members[i].Pathfinder.Speed = m_Group.Members[i].OriginalSpeed;
                        } else {
                            m_Group.Members[i].Pathfinder.Speed = m_Group.Members[i].OriginalSpeed * m_Group.Members[i].OutOfRangeSpeedMultiplier;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set new pathfinding destination for this agent in the formation.
        /// </summary>
        /// <param name="desiredPosition">Optionally sets the desired position if it has already been computed.</param>
        /// <returns>True if the destination is valid.</returns>
        public Vector3 UpdateFormationDestination(Vector3? desiredPosition = null)
        {
            if (m_FormationIndex == -1) {
                return Vector3.zero;
            }

            // Calculate this agent's position in the formation and move towards it.
            var position = desiredPosition.HasValue ? desiredPosition.Value : CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, m_Group.TargetPosition, m_Group.Direction, m_Group.State == FormationsManager.FormationState.MoveToTarget);
            m_Pathfinder.SetDesination(position);
            return position;
        }

        /// <summary>
        /// Check if all agents have rotated to face the target direction.
        /// </summary>
        /// <returns>True if all agents are facing the target direction.</returns>
        private bool HaveAllAgentsRotated()
        {
            var targetDirection = Vector3.ProjectOnPlane(m_Group.Direction, m_Transform.up).normalized;
            for (int i = 0; i < m_Group.Members.Count; ++i) {
                var forward = Vector3.ProjectOnPlane(m_Group.Members[i].Transform.forward, m_Group.Members[i].Transform.up).normalized;
                if (Vector3.Angle(forward, targetDirection) > m_RotationThreshold.Value) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Is the agent out of range and is falling behind the group?
        /// </summary>
        /// <returns>True if the agent is out of range.</returns>
        public bool IsOutOfRange()
        {
            if (m_FormationIndex == -1 || m_Group.Members.Count <= 1) {
                return false;
            }

            // Use the desired position to determine if the agent is falling behind the group. If the agent is the leader then compare against the first follower.
            var distance = (m_DesiredPosition - m_Transform.position).magnitude;
            var comparisonIndex = m_Group.Leader == this ? 1 : 0;
            var comparisonDistance = (m_Group.Members[comparisonIndex].DesiredPosition - m_Group.Members[comparisonIndex].transform.position).magnitude;
            if (distance > comparisonDistance + (m_OutOfRange ? m_InRangeDistanceDelta.Value : m_OutOfRangeDistanceDelta.Value)) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update the formation index for this task.
        /// </summary>
        /// <param name="index">The new index in the formation.</param>
        /// <param name="desiredPosition">Optionally sets the desired position if it has already been computed.</param>
        public virtual void UpdateFormationIndex(int index, Vector3? desiredPosition = null)
        {
            m_FormationIndex = index;
            if (m_Group.State == FormationsManager.FormationState.MoveToFormation || m_Group.State == FormationsManager.FormationState.MoveToTarget) {
                m_DesiredPosition = UpdateFormationDestination(desiredPosition);
            }
        }

        /// <summary>
        /// Has this agent arrived at its formation position?
        /// </summary>
        /// <returns>True if the agent has arrived at its position.</returns>
        public bool HasArrived()
        {
            return m_Pathfinder.HasArrived();
        }

        /// <summary>
        /// Sample a position to ensure it's on the NavMesh.
        /// </summary>
        /// <param name="position">The position to sample and modify if necessary.</param>
        /// <returns>True if a valid position was found.</returns>
        protected bool SamplePosition(ref Vector3 position)
        {
            return m_Pathfinder.SamplePosition(ref position);
        }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public override void OnEnd()
        {
            m_Pathfinder.Speed = m_OriginalSpeed;
            if (m_StopOnTaskEnd.Value) {
                m_Pathfinder.Stop();
            }

            // Remove the task from the formation.
            FormationsManager.RemoveTaskFromGroup(m_FormationGroupID.Value, this);
            m_FormationIndex = m_GroupID = -1;
            m_Group = null;

            m_Pathfinder.OnEnd();
        }

        /// <summary>
        /// The FormationGroupID SharedVariable has updated.
        /// </summary>
        private void FormationGroupIDChanged()
        {
            // Store the old group's state before adding the agent to a new group.
            var state = FormationsManager.FormationState.Initialized;
            if (m_FormationIndex != -1 && m_Group != null) {
                state = m_Group.State;
            }
            m_GroupID = m_FormationGroupID.Value;

            // Add to new group. AddTaskToGroup will remove the agent from the old group.
            m_FormationIndex = -1;
            FormationsManager.AddTaskToGroup(m_FormationGroupID.Value, this);

            m_Group = FormationsManager.GetFormationGroup(m_FormationGroupID.Value);
            if (m_Group != null) {
                if (m_Group.Leader == this && m_Group.State == FormationsManager.FormationState.Initialized && state != FormationsManager.FormationState.Initialized) {
                    m_Group.State = state;
                    // Ensure direction and indices are set.
                    if (m_FormationDirection.Value == OrientationDirection.TransformDirection) {
                        m_Group.Direction = m_Transform.forward;
                    } else if (m_FormationDirection.Value == OrientationDirection.MovementDirection) {
                        m_Group.Direction = (TargetPosition - m_Transform.position).normalized;
                    } else { // Use the specified vector.
                        m_Group.Direction = m_SpecifiedDirection.Value.normalized;
                    }
                }

                // If the group is already initialized then the task should be added at the last index.
                if (m_Group.State != FormationsManager.FormationState.Initialized) {
                    m_Group.Members[m_Group.Members.Count - 1].UpdateFormationIndex(m_Group.Members.Count - 1);
                }
            }
        }

        /// <summary>
        /// Data structure for saving formation state.
        /// </summary>
        protected class FormationSaveData
        {
            [Tooltip("The index of this agent in the formation.")]
            public int FormationIndex;
            [Tooltip("The desired position for this agent in the formation.")]
            public Vector3 DesiredPosition;
            [Tooltip("Indicates if this agent is out of range from the formation.")]
            public bool OutOfRange;
            [Tooltip("Indicates if this agent is stuck and unable to move.")]
            public bool IsStuck;
            [Tooltip("The time at which the agent became stuck.")]
            public float StuckTime;
            [Tooltip("The original movement speed of the agent before any modifications.")]
            public float OriginalSpeed;
            [Tooltip("The current destination that the agent is moving towards.")]
            public Vector3 Destination;
            [Tooltip("Indicates if the agent has a valid destination to move towards.")]
            public bool HasDestination;
            [Tooltip("The saved state of the formation group if this agent is the leader.")]
            public FormationGroupSaveData GroupState;
        }

        /// <summary>
        /// Data structure for saving formation group state.
        /// </summary>
        protected class FormationGroupSaveData
        {
            [Tooltip("The current state of the formation (Initialized, MoveToFormation, MoveToTarget, etc.).")]
            public FormationsManager.FormationState State;
            [Tooltip("The target position of the formation.")]
            public Vector3 TargetPosition;
            [Tooltip("The forward direction that the formation is facing.")]
            public Vector3 Direction;
            [Tooltip("The elapsed time since the formation started.")]
            public float ElapsedTime;
            [Tooltip("The formation indices of all members in the group.")]
            public int[] MemberIndices;
        }

        /// <summary>
        /// Specifies the type of reflection that should be used to save the task.
        /// </summary>
        /// <param name="index">The index of the sub-task. This is used for the task set allowing each contained task to have their own save type.</param>
        public override MemberVisibility GetSaveReflectionType(int index) { return MemberVisibility.None; }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            if (!m_BehaviorTree.IsNodeActive(true, m_RuntimeIndex)) return null;

            var saveData = new FormationSaveData();
            saveData.FormationIndex = m_FormationIndex;
            saveData.DesiredPosition = m_DesiredPosition;
            saveData.OutOfRange = m_OutOfRange;
            saveData.IsStuck = m_IsStuck;
            saveData.StuckTime = m_StuckTime;
            saveData.OriginalSpeed = m_OriginalSpeed;

            // Only save the pathfinder destination if we have a path and haven't arrived
            if (m_Pathfinder.HasPath() && !m_Pathfinder.HasArrived()) {
                saveData.Destination = m_Pathfinder.Destination;
                saveData.HasDestination = true;
            }

            // If this agent is the leader, save the group state
            var group = FormationsManager.GetFormationGroup(m_FormationGroupID.Value);
            if (group != null && group.Leader == this) {
                saveData.GroupState = new FormationGroupSaveData();
                saveData.GroupState.State = group.State;
                saveData.GroupState.TargetPosition = group.TargetPosition;
                saveData.GroupState.Direction = group.Direction;
                saveData.GroupState.ElapsedTime = Time.time - group.StartTime;
                saveData.GroupState.MemberIndices = new int[group.Members.Count];
                for (int i = 0; i < group.Members.Count; ++i) {
                    saveData.GroupState.MemberIndices[i] = group.Members[i].FormationIndex;
                }
            }

            return saveData;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, World world, Entity entity)
        {
            if (saveData == null) {
                return;
            }

            var formationSaveData = (FormationSaveData)saveData;
            m_FormationIndex = formationSaveData.FormationIndex;
            m_DesiredPosition = formationSaveData.DesiredPosition;
            m_OutOfRange = formationSaveData.OutOfRange;
            m_IsStuck = formationSaveData.IsStuck;
            m_StuckTime = formationSaveData.StuckTime;
            m_OriginalSpeed = formationSaveData.OriginalSpeed;
            if (formationSaveData.HasDestination) {
                m_Pathfinder.SetDesination(formationSaveData.Destination);
            }

            // Restore the group state if the agent is a leader.
            if (formationSaveData.GroupState != null) {
                var group = FormationsManager.GetFormationGroup(m_FormationGroupID.Value);
                if (group != null && group.Leader == this) {
                    group.State = formationSaveData.GroupState.State;
                    group.TargetPosition = formationSaveData.GroupState.TargetPosition;
                    group.Direction = formationSaveData.GroupState.Direction;
                    group.StartTime = Time.time - formationSaveData.GroupState.ElapsedTime;
                }
            }
        }

        /// <summary>
        /// The behavior tree has been stopped.
        /// </summary>
        /// <param name="paused">Was the behavior tree paused.</param>
        public override void OnBehaviorTreeStopped(bool paused)
        {
            base.OnBehaviorTreeStopped(paused);

            if (m_Pathfinder.HasPath()) {
                m_Pathfinder.Stop();
            }
        }

        /// <summary>
        /// The behavior tree has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            m_FormationGroupID.OnValueChange -= FormationGroupIDChanged;
            if (m_Pathfinder != null) {
                m_Pathfinder.Stop();
            }

            // Remove this task from the formation.
            FormationsManager.RemoveTaskFromGroup(m_GroupID, this);
            m_FormationIndex = m_GroupID = -1;
            m_Group = null;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
#if UNITY_EDITOR
            if (m_Pathfinder == null) {
                var defaultPathfinderType = UnityEditor.EditorPrefs.GetString(c_PathfinderTypeKey);
                if (!string.IsNullOrEmpty(defaultPathfinderType)) {
                    var pathfinderType = TypeUtility.GetType(defaultPathfinderType);
                    if (pathfinderType != null) {
                        m_Pathfinder = System.Activator.CreateInstance(pathfinderType) as Pathfinder;
                    }
                }

                // The NavMeshAgentPathfinder will always be available.
                if (m_Pathfinder == null) {
                    m_Pathfinder = new NavMeshAgentPathfinder();
                }
            }
#endif
            m_Pathfinder.Reset();
            m_Is2D = false;
            m_FormationGroupID = 0;
            m_ForceLeader = false;
            m_FormationDirection = OrientationDirection.TransformDirection;
            m_SpecifiedDirection = Vector3.forward;
            m_RotationSpeed = 180f;
            m_RotationThreshold = 1f;
            m_OutOfRangeDistanceDelta = 0.6f;
            m_InRangeDistanceDelta = 0.4f;
            m_OutOfRangeSpeedMultiplier = 0.5f;
            m_StuckDuration = 0.2f;
            m_FailOnAgentRemoval = false;
            m_UpdateUnitLocationsOnAgentRemoval = false;
            m_StopOnTaskEnd = true;
        }
    }
}
