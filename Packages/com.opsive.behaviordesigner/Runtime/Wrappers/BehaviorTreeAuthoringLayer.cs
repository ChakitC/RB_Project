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
    /// Wrapper for the BehaviorTree authoring layer. Retains the source component's MonoScript GUID so
    /// serialized layers resolve with either the source or precompiled Behavior Designer runtime.
    /// </summary>
    [AddComponentMenu("")]
    public class BehaviorTreeAuthoringLayer : Runtime.Authoring.BehaviorTreeAuthoringLayer
    {
    }
}
#endif