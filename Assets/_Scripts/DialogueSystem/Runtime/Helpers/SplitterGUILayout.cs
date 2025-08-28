#if !LINUX_BUILD
using UnityEngine;
using UnityEditor;

public static class SplitterGUILayout
{
    private static bool _dragging = false;
    private static float _dragStart = 0f;
    private static float _startPos = 0f;

    /// <summary>
    /// Draw a vertical splitter.  
    ///  - pos: current X?position (width) of the panel to the left of the splitter  
    ///  - min: minimum allowed width  
    ///  - max: maximum allowed width  
    ///  - vertical: true means a vertical bar (drag left/right), false would be horizontal drag.
    /// </summary>
    public static float Splitter(float pos, float min, float max, bool vertical = true)
    {
        // Reserve a tiny area for the splitter handle
        Rect rect = GUILayoutUtility.GetRect(
            vertical ? 4 : 0,
            vertical ? 0 : 4,
            vertical ? GUILayout.ExpandHeight(true) : GUILayout.ExpandWidth(true),
            vertical ? GUILayout.ExpandWidth(false) : GUILayout.ExpandHeight(false)
        );

        // Change cursor when hovering
        EditorGUIUtility.AddCursorRect(rect, vertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);

        // Handle mouse events
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            _dragging = true;
            _dragStart = vertical ? Event.current.mousePosition.x : Event.current.mousePosition.y;
            _startPos = pos;
            Event.current.Use();
        }

        if (_dragging && Event.current.type == EventType.MouseDrag)
        {
            float delta = (vertical ? Event.current.mousePosition.x : Event.current.mousePosition.y) - _dragStart;
            pos = Mathf.Clamp(_startPos + delta, min, max);
            GUI.changed = true;
            Event.current.Use();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            _dragging = false;
        }

        return pos;
    }
}
#endif
