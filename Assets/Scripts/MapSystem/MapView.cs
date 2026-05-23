using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MapView : MonoBehaviour
{
    [Tooltip("TMP_Text ที่ใช้แสดง map graph แบบข้อความ")]
    [SerializeField] private TMP_Text textTarget;

    [Tooltip("ถ้าเปิด node ที่ยังซ่อนอยู่จะแสดงชนิดจริงแทนเครื่องหมาย ?")]
    [SerializeField] private bool showHiddenNodeTypes;

    [Tooltip("ข้อความที่ใช้แทน node ที่ยังไม่ถูก reveal")]
    [SerializeField] private string hiddenLabel = "?";

    private readonly StringBuilder builder = new();

    public void Refresh(MapRunController runController)
    {
        if (textTarget == null || runController == null || runController.CurrentGraph == null)
            return;

        MapGraph graph = runController.CurrentGraph;
        MapNode current = runController.CurrentNode;

        builder.Clear();
        for (int i = 0; i < graph.CriticalPathIds.Count; i++)
        {
            MapNode node = graph.GetNode(graph.CriticalPathIds[i]);
            if (node == null)
                continue;

            if (i > 0)
                builder.Append(" -- ");

            AppendNodeLabel(node, current);
        }

        if (current != null)
        {
            for (int i = 0; i < current.OutgoingIds.Count; i++)
            {
                MapNode branch = graph.GetNode(current.OutgoingIds[i]);
                if (branch == null || branch.IsCriticalPath)
                    continue;

                builder.AppendLine();
                builder.Append("  \\-- ");
                AppendNodeLabel(branch, current);
            }
        }

        textTarget.text = builder.ToString();
    }

    void AppendNodeLabel(MapNode node, MapNode current)
    {
        bool visible = node.State != MapNodeRevealState.Hidden;
        string label = visible || showHiddenNodeTypes ? node.Type.ToString() : hiddenLabel;

        if (node == current)
        {
            builder.Append("[");
            builder.Append(label);
            builder.Append("*]");
            return;
        }

        builder.Append("[");
        builder.Append(label);
        builder.Append("]");
    }
}
