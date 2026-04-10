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
    [Opsive.Shared.Utility.Description("Generates 3D Perlin noise value at the specified coordinates. Note: Unity's Mathf.PerlinNoise only supports 2D, so this uses a workaround combining multiple 2D samples.")]
    public class PerlinNoise3D : Action
    {
        [Tooltip("The X coordinate for the noise.")]
        [SerializeField] protected SharedVariable<float> m_X;
        [Tooltip("The Y coordinate for the noise.")]
        [SerializeField] protected SharedVariable<float> m_Y;
        [Tooltip("The Z coordinate for the noise.")]
        [SerializeField] protected SharedVariable<float> m_Z;
        [Tooltip("The resulting Perlin noise value (0 to 1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Generates 3D Perlin noise.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
// Unity's Mathf.PerlinNoise only supports 2D, so we combine multiple 2D samples
            var xy = Mathf.PerlinNoise(m_X.Value, m_Y.Value);
            var xz = Mathf.PerlinNoise(m_X.Value, m_Z.Value);
            var yz = Mathf.PerlinNoise(m_Y.Value, m_Z.Value);
            m_Result.Value = (xy + xz + yz) / 3f;

            return TaskStatus.Success;
        }
    }
}
#endif