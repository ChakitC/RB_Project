#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Groups
{
    using System;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Group that executes all of the tasks.
    /// </summary>
    [UpdateInGroup(typeof(TraversalSystemGroup))]
    public partial class TraversalTaskSystemGroup : ComponentSystemGroup
    {
#if UNITY_EDITOR
        public static Action OnCreatedEditor;
        private bool m_Initialized;
#endif

        [Tooltip("Callback before the outher tasks are updated.")]
        public Action OnPreUpdate;
        [Tooltip("Callback after the outher tasks are updated.")]
        public Action OnPostUpdate;

#if UNITY_EDITOR
        /// <summary>
        /// The group has been created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_Initialized = false;
        }
#endif

        /// <summary>
        /// Updates the group.
        /// </summary>
        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            // The editor normally attaches to the graph after the OnPlayModeStateChange callback. This is too late for tasks that instantly progress within the first frame.
            // Issue the callback before the tasks are traversed.
            if (OnCreatedEditor != null && !m_Initialized) {
                m_Initialized = true;
                OnCreatedEditor();
            }
#endif
            if (OnPreUpdate != null) {
                OnPreUpdate();
            }

            base.OnUpdate();

            if (OnPostUpdate != null) {
                OnPostUpdate();
            }
        }
    }
}
#endif