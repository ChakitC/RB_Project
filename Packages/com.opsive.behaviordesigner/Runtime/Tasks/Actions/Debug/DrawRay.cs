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
    [Opsive.Shared.Utility.Description("Draws a ray from a position in a direction in the Scene view. Only visible in the editor or when Gizmos are enabled.")]
    public class DrawRay : Action
    {
        [Tooltip("The start position of the ray.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;
        [Tooltip("The direction and length of the ray.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The color of the ray.")]
        [SerializeField] protected SharedVariable<Color> m_Color = Color.white;
        [Tooltip("How long the ray should be visible (in seconds). Zero means one frame.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 0f;
        [Tooltip("Should the ray be obscured by objects in the scene?")]
        [SerializeField] protected SharedVariable<bool> m_DepthTest = true;

        /// <summary>
        /// Draws the ray and finishes.
        /// </summary>
        /// <returns>The Finished TaskStatus.</returns>
        public override TaskStatus OnUpdate()
        {
            UnityEngine.Debug.DrawRay(m_Position.Value, m_Direction.Value, m_Color.Value, m_Duration.Value, m_DepthTest.Value);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Position = Vector3.zero;
            m_Direction = Vector3.zero;
            m_Color = Color.white;
            m_Duration = 0f;
            m_DepthTest = true;
        }
    }
}
#endif