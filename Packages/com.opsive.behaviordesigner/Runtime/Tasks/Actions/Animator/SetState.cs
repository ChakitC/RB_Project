#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Sets animator state by name or hash with transition duration and layer selection.")]
    public class SetState : TargetGameObjectAction
    {
        [Tooltip("The state name (leave empty to use State Hash).")]
        [SerializeField] protected SharedVariable<string> m_StateName = "";
        [Tooltip("The state hash (only used if State Name is empty).")]
        [SerializeField] protected SharedVariable<int> m_StateHash = 0;
        [Tooltip("The layer index.")]
        [SerializeField] protected SharedVariable<int> m_Layer = 0;
        [Tooltip("The transition duration.")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private Animator m_ResolvedAnimator;
        private bool m_HasSetState;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_HasSetState = false;
        }

        /// <summary>
        /// Sets the animator state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            if (!m_HasSetState) {
                if (!string.IsNullOrEmpty(m_StateName.Value)) {
                    m_ResolvedAnimator.Play(m_StateName.Value, m_Layer.Value, m_TransitionDuration.Value);
                } else if (m_StateHash.Value != 0) {
                    m_ResolvedAnimator.Play(m_StateHash.Value, m_Layer.Value, m_TransitionDuration.Value);
                }
                m_HasSetState = true;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StateName = "";
            m_StateHash = 0;
            m_Layer = 0;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif