#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Graph Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.GraphDesigner.Runtime.Variables
{
    using Unity.Entities;

    /// <summary>
    /// Lightweight typed index into a SharedVariableElement buffer.
    /// Store this in your IBufferElementData component instead of a raw int field to get type-safe access to a shared variable value from within a Burst job.
    /// </summary>
    public readonly struct ECSSharedVariableIndex<T> where T : unmanaged
    {
        /// <summary>
        /// The index of the shared variable within the shared variable buffer.
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// Initializes a new instance of the <see cref="ECSSharedVariableIndex{T}"/> struct.
        /// </summary>
        /// <param name="index">The index of the shared variable within the shared variable buffer.</param>
        public ECSSharedVariableIndex(int index) => Index = index;

        /// <summary>
        /// Reads the current value from the shared variable buffer at this index.
        /// </summary>
        /// <param name="buffer">The buffer that stores the shared variable values.</param>
        /// <returns>The value stored at this shared variable index.</returns>
        public T Get(DynamicBuffer<SharedVariableElement> buffer) => buffer.Get<T>(Index);

        /// <summary>
        /// Writes a value into the shared variable buffer at this index.
        /// </summary>
        /// <param name="buffer">The buffer that stores the shared variable values.</param>
        /// <param name="value">The value that should be written to the shared variable buffer.</param>
        public void Set(DynamicBuffer<SharedVariableElement> buffer, T value) => buffer.Set(Index, value);
    }
}
#endif