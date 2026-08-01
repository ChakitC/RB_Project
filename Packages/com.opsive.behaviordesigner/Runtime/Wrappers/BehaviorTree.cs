#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Wrappers
{
    using UnityEngine;

    /// <summary>
    /// Wrapper for the BehaviorTree component. Retains the original MonoScript GUID so existing scenes
    /// and prefabs continue to resolve after the implementation moved into the precompiled runtime DLL.
    /// </summary>
    [AddComponentMenu("Opsive/Behavior Designer/Behavior Tree")]
    public class BehaviorTree : Runtime.BehaviorTree
    {
    }
}
#endif