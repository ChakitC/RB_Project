#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Graph Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.GraphDesigner.Runtime.Wrappers
{
    using UnityEngine;

    /// <summary>
    /// Wrapper for the SharedVariable authoring layer. Retains the source component's MonoScript GUID so
    /// serialized layers resolve with either the source or precompiled Graph Designer runtime.
    /// </summary>
    [AddComponentMenu("")]
    public class SharedVariableAuthoringLayer : Runtime.Serialization.SharedVariableAuthoringLayer
    {
    }
}
#endif