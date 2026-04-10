#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Generates 2D Perlin noise value at the specified coordinates.")]
    public class PerlinNoise2D : Action
    {
        [Tooltip("The X coordinate for the noise.")]
        [SerializeField] protected SharedVariable<float> m_X;
        [Tooltip("The Y coordinate for the noise.")]
        [SerializeField] protected SharedVariable<float> m_Y;
        [Tooltip("The resulting Perlin noise value (0 to 1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Generates 2D Perlin noise.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.PerlinNoise(m_X.Value, m_Y.Value);

            return TaskStatus.Success;
        }
    }
}
#endif