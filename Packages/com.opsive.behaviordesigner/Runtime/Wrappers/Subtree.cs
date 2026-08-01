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
    /// Wrapper for the Subtree ScriptableObject. Retains the original MonoScript GUID so existing .asset
    /// files continue to resolve after the implementation moved into the precompiled runtime DLL.
    /// </summary>
    [CreateAssetMenu(fileName = "Subtree", menuName = "Opsive/Behavior Designer/Subtree", order = 1)]
    public class Subtree : Runtime.Subtree
    {
    }
}
#endif