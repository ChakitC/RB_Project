#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Graph Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.GraphDesigner.Runtime.Variables
{
    using Opsive.GraphDesigner.Runtime.ECS.Core;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Unmanaged buffer element storing shared variable values as raw bytes.
    /// Supports any unmanaged type up to 16 bytes (float, int, bool, float2, float3, float4, etc.).
    /// </summary>
    public struct SharedVariableElement : IBufferElementData
    {
        [Tooltip("16-byte storage that covers float, int, bool, double, float3, float4, and similar values.")]
        public float4 Value;
    }

    /// <summary>
    /// Extension methods for reading and writing typed values in a SharedVariableElement buffer.
    /// Burst-compatible via UnsafeUtility.MemCpy.
    /// </summary>
    public static class SharedVariableBufferExtensions
    {
        /// <summary>
        /// Reads a value of type T from the buffer at the specified index.
        /// T must be an unmanaged type no larger than 16 bytes.
        /// </summary>
        /// <param name="buffer">The buffer that stores the shared variable values.</param>
        /// <param name="index">The index of the shared variable within the buffer.</param>
        /// <returns>The value stored at the specified buffer index.</returns>
        public static unsafe T Get<T>(this DynamicBuffer<SharedVariableElement> buffer, int index) where T : unmanaged
        {
            return SharedVariableBufferCore.Unpack<T>(buffer[index].Value);
        }

        /// <summary>
        /// Writes a value of type T into the buffer at the specified index.
        /// T must be an unmanaged type no larger than 16 bytes.
        /// </summary>
        /// <param name="buffer">The buffer that stores the shared variable values.</param>
        /// <param name="index">The index of the shared variable within the buffer.</param>
        /// <param name="value">The value that should be written to the buffer.</param>
        public static unsafe void Set<T>(this DynamicBuffer<SharedVariableElement> buffer, int index, T value) where T : unmanaged
        {
            var element = buffer[index];
            element.Value = SharedVariableBufferCore.Pack(value);
            buffer[index] = element;
        }
    }
}
#endif