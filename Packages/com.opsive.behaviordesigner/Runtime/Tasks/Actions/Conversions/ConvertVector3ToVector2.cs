#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Conversions")]
    [Opsive.Shared.Utility.Description("Converts a Vector3 to a Vector2 by dropping one component (X, Y, or Z).")]
    public class ConvertVector3ToVector2 : Action
    {
        /// <summary>
        /// Specifies which component to drop when converting Vector3 to Vector2.
        /// </summary>
        public enum DropComponent
        {
            DropX,  // Drop the X component (keep Y and Z).
            DropY,  // Drop the Y component (keep X and Z).
            DropZ   // Drop the Z component (keep X and Y).
        }

        [Tooltip("The Vector3 to convert.")]
        [SerializeField] protected SharedVariable<Vector3> m_InputVector;
        [Tooltip("Specifies which component to drop when converting to Vector2. Default is DropZ.")]
        [SerializeField] protected DropComponent m_DropComponent = DropComponent.DropZ;
        [Tooltip("The resulting Vector2 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_OutputVector;

        /// <summary>
        /// Converts the Vector3 to a Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_DropComponent) {
                case DropComponent.DropX:
                    m_OutputVector.Value = new Vector2(m_InputVector.Value.y, m_InputVector.Value.z);
                    break;
                case DropComponent.DropY:
                    m_OutputVector.Value = new Vector2(m_InputVector.Value.x, m_InputVector.Value.z);
                    break;
                case DropComponent.DropZ:
                    m_OutputVector.Value = new Vector2(m_InputVector.Value.x, m_InputVector.Value.y);
                    break;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_DropComponent = DropComponent.DropZ;
            m_OutputVector = null;
        }
    }
}
#endif