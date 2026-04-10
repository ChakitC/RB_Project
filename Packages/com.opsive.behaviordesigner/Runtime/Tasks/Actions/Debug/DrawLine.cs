#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Debug
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Debug")]
    [Opsive.Shared.Utility.Description("Draws a line between two points in the Scene view. Only visible in the editor or when Gizmos are enabled.")]
    public class DrawLine : Action
    {
        [Tooltip("The start point of the line.")]
        [SerializeField] protected SharedVariable<Vector3> m_Start;
        [Tooltip("The end point of the line.")]
        [SerializeField] protected SharedVariable<Vector3> m_End;
        [Tooltip("The color of the line.")]
        [SerializeField] protected SharedVariable<Color> m_Color = Color.white;
        [Tooltip("How long the line should be visible (in seconds). Zero means one frame.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 0f;
        [Tooltip("Should the line be obscured by objects in the scene?")]
        [SerializeField] protected SharedVariable<bool> m_DepthTest = true;

        /// <summary>
        /// Draws the line and finishes.
        /// </summary>
        /// <returns>The Finished TaskStatus.</returns>
        public override TaskStatus OnUpdate()
        {
            UnityEngine.Debug.DrawLine(m_Start.Value, m_End.Value, m_Color.Value, m_Duration.Value, m_DepthTest.Value);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Start = Vector3.zero;
            m_End = Vector3.zero;
            m_Color = Color.white;
            m_Duration = 0f;
            m_DepthTest = true;
        }
    }
}
#endif