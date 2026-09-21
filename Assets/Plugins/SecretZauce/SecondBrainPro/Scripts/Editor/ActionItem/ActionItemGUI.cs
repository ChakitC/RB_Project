using System;
using SecretZauce.SecondBrain.Editor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public class ActionItemGUI : ActionItemGUIBase
    {
        readonly ActionItem actionItem;
        readonly GUIStyle style;
        Rect arrowRect;
        Rect rowRect;

        public ActionItemGUI(ActionItem actionItem, GUIStyle style, Rect arrowRect, Rect rowRect) : base(actionItem, style, arrowRect, rowRect)
        {
            this.actionItem = actionItem;
            this.style = style;
            this.arrowRect = arrowRect;
            this.rowRect = rowRect;
        }

        public override bool TryDrawActionItem()
        {
            if (actionItem == null)
                return false;
            
            // Minimal execute button for ActionItem rows. Placed to the left of the usual arrow area.
            float execButtonSize = 16f;
            float execButtonPadding = 2f;
            float execButtonX = arrowRect.x - execButtonSize - execButtonPadding;
            Rect execButtonRect = new Rect(execButtonX, rowRect.y + (rowRect.height - execButtonSize) / 2f, execButtonSize, execButtonSize);

            bool isHoveringExec = Event.current != null && execButtonRect.Contains(Event.current.mousePosition);
            // Reuse cached arrow style with per-call color.
            style.normal.textColor = isHoveringExec ? new Color(1f, 1f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);

            Rect execRectNudged = execButtonRect;
            execRectNudged.y -= 1;
            if (GUI.Button(execRectNudged, new GUIContent(IconUtils.Load("play")), style))
            {
                try
                {
                    actionItem.Execute();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                Event.current?.Use();
                return true;
            }

            return false;
        }
        
        public override GUIContent TryAppendActionItemDetail(GUIContent labelContent)
        {
            if (actionItem == null)
                return labelContent;

            try
            {
                var d = actionItem.GetDetailDisplay();
                if (!string.IsNullOrEmpty(d))
                {
                    string baseText = labelContent.text ?? actionItem.name;
                    labelContent = new GUIContent(baseText + ": " + d, labelContent.image);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            return labelContent;
        }
    }
}
