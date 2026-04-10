#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Sets multiple animator parameters (float, int, bool, trigger) with validation.")]
    public class SetParameters : TargetGameObjectAction
    {
        [Tooltip("The float parameters to set.")]
        [SerializeField] protected List<FloatParameter> m_FloatParameters = new List<FloatParameter>();
        [Tooltip("The integer parameters to set.")]
        [SerializeField] protected List<IntegerParameter> m_IntegerParameters = new List<IntegerParameter>();
        [Tooltip("The boolean parameters to set.")]
        [SerializeField] protected List<BooleanParameter> m_BooleanParameters = new List<BooleanParameter>();
        [Tooltip("The trigger parameters to set.")]
        [SerializeField] protected List<TriggerParameter> m_TriggerParameters = new List<TriggerParameter>();

        private Animator m_ResolvedAnimator;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Sets all animator parameters.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            for (int i = 0; i < m_FloatParameters.Count; ++i) {
                if (m_FloatParameters[i] != null && !string.IsNullOrEmpty(m_FloatParameters[i].name) && m_FloatParameters[i].value != null) {
                    m_ResolvedAnimator.SetFloat(m_FloatParameters[i].name, m_FloatParameters[i].value.Value);
                }
            }

            for (int i = 0; i < m_IntegerParameters.Count; ++i) {
                if (m_IntegerParameters[i] != null && !string.IsNullOrEmpty(m_IntegerParameters[i].name) && m_IntegerParameters[i].value != null) {
                    m_ResolvedAnimator.SetInteger(m_IntegerParameters[i].name, m_IntegerParameters[i].value.Value);
                }
            }

            for (int i = 0; i < m_BooleanParameters.Count; ++i) {
                if (m_BooleanParameters[i] != null && !string.IsNullOrEmpty(m_BooleanParameters[i].name) && m_BooleanParameters[i].value != null) {
                    m_ResolvedAnimator.SetBool(m_BooleanParameters[i].name, m_BooleanParameters[i].value.Value);
                }
            }

            for (int i = 0; i < m_TriggerParameters.Count; ++i) {
                if (m_TriggerParameters[i] != null && !string.IsNullOrEmpty(m_TriggerParameters[i].name)) {
                    if (m_TriggerParameters[i].set) {
                        m_ResolvedAnimator.SetTrigger(m_TriggerParameters[i].name);
                    } else {
                        m_ResolvedAnimator.ResetTrigger(m_TriggerParameters[i].name);
                    }
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_FloatParameters = new List<FloatParameter>();
            m_IntegerParameters = new List<IntegerParameter>();
            m_BooleanParameters = new List<BooleanParameter>();
            m_TriggerParameters = new List<TriggerParameter>();
        }

        [System.Serializable]
        public class FloatParameter
        {
            public string name;
            public SharedVariable<float> value;
        }

        [System.Serializable]
        public class IntegerParameter
        {
            public string name;
            public SharedVariable<int> value;
        }

        [System.Serializable]
        public class BooleanParameter
        {
            public string name;
            public SharedVariable<bool> value;
        }

        [System.Serializable]
        public class TriggerParameter
        {
            public string name;
            public bool set = true;
        }
    }
}
#endif