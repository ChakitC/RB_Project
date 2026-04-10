#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Finds GameObject(s) by tag. Can find closest to a position, find all with tag, or search in children only.")]
    public class FindGameObjectByTag : Action
    {
        [Tooltip("The tag to search for.")]
        [SerializeField] protected SharedVariable<string> m_Tag;
        [Tooltip("Should the closest GameObject to a position be found?")]
        [SerializeField] protected SharedVariable<bool> m_FindClosest = false;
        [Tooltip("The position to find closest to. Only used if Find Closest is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_ReferencePosition;
        [Tooltip("Should all GameObjects with the tag be found?")]
        [SerializeField] protected SharedVariable<bool> m_FindAll = false;
        [Tooltip("Should the search be limited to children only?")]
        [SerializeField] protected SharedVariable<bool> m_ChildrenOnly = false;
        [Tooltip("The root GameObject to search from. Only used if Children Only is enabled.")]
        [SerializeField] protected SharedVariable<GameObject> m_RootGameObject;
        [Tooltip("The found GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_FoundObject;
        [Tooltip("The found GameObjects array. Only used if Find All is enabled.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject[]> m_FoundObjects;

        /// <summary>
        /// Finds GameObject(s) by tag.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Tag.Value)) {
                m_FoundObject.Value = null;
                m_FoundObjects.Value = new GameObject[0];
                return TaskStatus.Success;
            }

            GameObject[] foundArray;

            if (m_ChildrenOnly.Value && m_RootGameObject.Value != null) {
                // Search in children only.
                var list = new List<GameObject>();
                FindInChildren(m_RootGameObject.Value.transform, m_Tag.Value, list);
                foundArray = list.ToArray();
            } else {
                // Search in entire scene.
                foundArray = UnityEngine.GameObject.FindGameObjectsWithTag(m_Tag.Value);
            }

            if (m_FindAll.Value) {
                m_FoundObjects.Value = foundArray;
                m_FoundObject.Value = foundArray.Length > 0 ? foundArray[0] : null;
            } else if (m_FindClosest.Value && m_ReferencePosition.Value != null) {
                // Find closest to reference position.
                GameObject closest = null;
                var minDistance = float.MaxValue;

                foreach (var obj in foundArray) {
                    var distance = Vector3.Distance(obj.transform.position, m_ReferencePosition.Value);
                    if (distance < minDistance) {
                        minDistance = distance;
                        closest = obj;
                    }
                }

                m_FoundObject.Value = closest;
            } else {
                // Find first.
                m_FoundObject.Value = foundArray.Length > 0 ? foundArray[0] : null;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Finds GameObjects with tag in children recursively.
        /// </summary>
        private void FindInChildren(Transform parent, string tag, List<GameObject> results)
        {
            foreach (Transform child in parent) {
                if (child.CompareTag(tag)) {
                    results.Add(child.gameObject);
                }
                FindInChildren(child, tag, results);
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Tag = null;
            m_FindClosest = false;
            m_ReferencePosition = null;
            m_FindAll = false;
            m_ChildrenOnly = false;
            m_RootGameObject = null;
            m_FoundObject = null;
            m_FoundObjects = null;
        }
    }
}
#endif