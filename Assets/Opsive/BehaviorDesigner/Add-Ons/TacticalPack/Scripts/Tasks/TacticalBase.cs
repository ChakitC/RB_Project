/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks
{
    using Opsive.Shared.Utility;
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime;
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System;
    using UnityEngine;

    /// <summary>
    /// Base class for tactical tasks.
    /// </summary>
    [Category("Tactical Pack")]
    public abstract class TacticalBase : FormationsBase
    {
        /// <summary>
        /// Specifies when the attack should be delayed.
        /// </summary>
        public enum AttackDelay
        {
            None,           // Attack immediately when possible.
            Arrival,        // Wait until the agent arrives at their position before attacking.
            GroupArrival    // Wait until all agents in the group arrive at their positions before attacking.
        }

        [Tooltip("The target GameObjects that should be attacked.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Targets;
        [Tooltip("Specifies when the attack should be delayed.")]
        [SerializeField] protected SharedVariable<AttackDelay> m_AttackDelay;
        [Tooltip("Specifies if the target can move.")]
        [SerializeField] protected SharedVariable<bool> m_MovingTarget = true;

        protected IDamageable[] m_TargetDamageables;
        protected IAttackAgent m_AttackAgent;
        protected Transform m_AttackTarget;
        protected IDamageable m_AttackDamageable;
        protected Vector3 m_LastTargetPosition;

        /// <summary>
        /// The states that determine if an agent can attack.
        /// </summary>
        public enum CanAttackStatus
        {
            OutOfRange, // The target is out of attack range.
            OutOfSight, // The target is not in the agent's line of sight.
            Moving,     // The agent is moving to position.
            NearTarget, // The agent is near the target.
            Allowed     // The agent can attack the target.
        }
        private CanAttackStatus m_CanAttackStatus;
        private bool m_HasAttacked;

        public CanAttackStatus AttackStatus => m_CanAttackStatus;

        /// <summary>
        /// Specifies if the agent can move into the initial formation.
        /// </summary>
        public override bool CanMoveIntoInitialFormation => false;
        /// <summary>
        /// Specifies if the agent should determine if it's out of range.
        /// </summary>
        public override bool DetermineOutOfRange => false;
        /// <summary>
        /// Specifies if the agent should determine if it's stuck.
        /// </summary>
        protected override bool DetermineIfStuck => false;
        /// <summary>
        /// Gets the target position for the agent. If attacking, returns the attack target position.
        /// </summary>
        public override Vector3 TargetPosition => m_AttackTarget != null && m_Group.State == FormationsManager.FormationState.MoveToTarget ? m_AttackTarget.position : m_Transform.position;
        /// <summary>
        /// Specifies if the target should be updated regardless of it a target is already found.
        /// </summary>
        protected virtual bool ContinuousTargetSearch => false;
        /// <summary>
        /// Specifies if the agent should stop when within attack range.
        /// </summary>
        protected virtual bool StopWithinRange => true;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            m_AttackAgent = GetComponent<IAttackAgent>();
            if (m_AttackAgent == null) {
                Debug.LogError($"Error: The agent {gameObject} must have a component that implements IAttackAgent.");
            }
            m_Targets.OnValueChange += UpdateDamageables;
        }

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_CanAttackStatus = CanAttackStatus.OutOfRange;
            m_HasAttacked = false;
            m_LastTargetPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            UpdateDamageables();
        }

        /// <summary>
        /// The Targets array has changed - update the damageable components.
        /// </summary>
        private void UpdateDamageables()
        {
            if (m_Targets.Value == null) {
                return;
            }

            if (m_TargetDamageables == null) {
                m_TargetDamageables = new IDamageable[m_Targets.Value.Length];
            } else if (m_TargetDamageables.Length != m_Targets.Value.Length) {
                Array.Resize(ref m_TargetDamageables, m_Targets.Value.Length);
            }
            for (int i = 0; i < m_TargetDamageables.Length; ++i) {
                if (m_Targets.Value[i] == null) {
                    m_TargetDamageables[i] = null;
                    continue;
                }

                m_TargetDamageables[i] = m_Targets.Value[i].GetComponent<IDamageable>();
                if (m_TargetDamageables[i] == null) {
                    Debug.LogError($"Error: The target {m_Targets.Value[i]} does not implement IDamageable.");
                }
            }
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            // If no target is found then the task should return success.
            if (!FindAttackTarget()) {
                return TaskStatus.Success;
            }

            if (m_Group.State == FormationsManager.FormationState.MoveToTarget || m_Group.State == FormationsManager.FormationState.Arrived) {
                UpdateTarget();

                var direction = m_AttackTarget.position - m_Transform.position;
                m_CanAttackStatus = CanAttack(direction);
                if (m_CanAttackStatus == CanAttackStatus.OutOfSight || m_CanAttackStatus == CanAttackStatus.Allowed) {
                    m_AttackAgent.RotateTowards(direction);
                    if (m_CanAttackStatus == CanAttackStatus.Allowed) {
                        if (StopWithinRange && direction.magnitude <= m_AttackAgent.MinAttackDistance) {
                            m_Pathfinder.Stop();
                        }
                        if (m_HasAttacked || m_AttackDelay.Value != AttackDelay.GroupArrival || CanAllAgentsAttack()) {
                            m_AttackAgent.Attack(m_AttackTarget, m_AttackDamageable);
                            m_HasAttacked = true;
                        }
                    }
                } else if (m_CanAttackStatus == CanAttackStatus.Moving && StopWithinRange && direction.magnitude <= m_AttackAgent.MinAttackDistance) {
                    m_Pathfinder.Stop();
                }

                if (m_MovingTarget.Value && m_LastTargetPosition != m_Group.TargetPosition) {
                    m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, m_AttackTarget.position, m_Group.Direction, true));
                    m_LastTargetPosition = m_Group.TargetPosition;
                }
            }

            var taskStatus = base.OnUpdate();
            if (taskStatus == TaskStatus.Failure) {
                return taskStatus;
            }
            return TaskStatus.Running;
        }

        /// <summary>
        /// Finds a target that should be attacked.
        /// </summary>
        /// <returns>True if a target is found.</returns>
        private bool FindAttackTarget()
        {
            if (!ContinuousTargetSearch && m_AttackDamageable != null && m_AttackDamageable.IsAlive) {
                return true;
            }

            if (m_Targets.Value == null || m_Targets.Value.Length == 0) {
                return false;
            }

            m_AttackTarget = null;
            m_AttackDamageable = null;

            var closestDistance = float.MaxValue;
            for (int i = 0; i < m_Targets.Value.Length; ++i) {
                if (m_Targets.Value[i] == null) {
                    continue;
                }

                if (!m_TargetDamageables[i].IsAlive) {
                    continue;
                }

                var distance = (m_Targets.Value[i].transform.position - m_Transform.position).sqrMagnitude;
                if (distance < closestDistance) {
                    closestDistance = distance;
                    m_AttackTarget = m_Targets.Value[i].transform;
                    m_AttackDamageable = m_TargetDamageables[i];
                }
            }

            return m_AttackTarget != null;
        }

        /// <summary>
        /// Determines if the agent can attack the current target.
        /// </summary>
        /// <param name="direction">The attack direction.</param>
        /// <returns>The current attack status.</returns>
        private CanAttackStatus CanAttack(Vector3 direction)
        {
            if (direction.magnitude > m_AttackAgent.MaxAttackDistance) {
                return CanAttackStatus.OutOfRange;
            }
            if (m_AttackDelay.Value == AttackDelay.None || m_Pathfinder.HasArrived() || !m_Pathfinder.HasPath()) {
                float angle;
                if (m_Is2D) {
                    var forward2D = new Vector2(direction.x, direction.y).normalized;
                    var targetAngle = Mathf.Atan2(forward2D.y, forward2D.x) * Mathf.Rad2Deg;
                    targetAngle = (270 + targetAngle) % 360;
                    angle = Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.z, targetAngle));
                } else {
                    angle = Quaternion.Angle(m_Transform.rotation, Quaternion.LookRotation(direction));
                }
                if (angle <= m_AttackAgent.AttackAngleThreshold) {
                    return CanAttackStatus.Allowed;
                } else {
                    return CanAttackStatus.OutOfSight;
                }
            }
            return CanAttackStatus.Moving;
        }

        /// <summary>
        /// Check if all agents are ready to attack.
        /// </summary>
        /// <returns>True if all agents are facing the target direction.</returns>
        protected bool CanAllAgentsAttack()
        {
            for (int i = 0; i < m_Group.Members.Count; ++i) {
                if ((m_Group.Members[i] as TacticalBase).AttackStatus != CanAttackStatus.Allowed) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Updates the group target if the targets can move.
        /// </summary>
        private void UpdateTarget()
        {
            if (m_Group.Leader != this || !m_MovingTarget.Value || m_Group.State == FormationsManager.FormationState.Initialized || m_Group.State == FormationsManager.FormationState.MoveToFormation) {
                return;
            }

            if (m_LastTargetPosition != m_AttackTarget.position) {
                m_Group.TargetPosition = m_AttackTarget.position;
                m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, m_Group.TargetPosition, m_Group.Direction, true));
                m_LastTargetPosition = m_AttackTarget.position;
            }
        }

        /// <summary>
        /// The task has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            m_Targets.OnValueChange -= UpdateDamageables;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Targets = null;
            m_MovingTarget = true;
            var defaultAttackDelay = Attribute.GetCustomAttribute(GetType(), typeof(DefaultAttackDelay)) as DefaultAttackDelay;
            if (defaultAttackDelay != null) {
                m_AttackDelay = defaultAttackDelay.Value;
            } else {
                m_AttackDelay = AttackDelay.None;
            }
        }
    }

    /// <summary>
    /// Attribute which specifies the default Attack Delay for the task.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DefaultAttackDelay : Attribute
    {
        /// <summary>
        /// The default attack delay value.
        /// </summary>
        public TacticalBase.AttackDelay Value { get; }
        /// <summary>
        /// Initializes a new instance of the DefaultAttackDelay attribute.
        /// </summary>
        /// <param name="value">The default attack delay value.</param>
        public DefaultAttackDelay(TacticalBase.AttackDelay value) { Value = value; }
    }
} 