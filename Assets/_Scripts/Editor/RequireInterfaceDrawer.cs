using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Draws a <see cref="RequireInterfaceAttribute"/> field as an object field that only ever
    /// stores something implementing the required interface.
    /// </summary>
    /// <remarks>
    /// Three jobs, and all three are why the attribute is worth having at all:
    /// <list type="number">
    /// <item>NARROW the picker to the tightest base type the field's own declared type allows, so
    /// whole categories of object are never offered.</item>
    /// <item>RESOLVE a dropped GameObject to the component on it that implements the interface -
    /// the field stores the component, never the GameObject.</item>
    /// <item>REFUSE anything that cannot satisfy the interface, loudly, leaving the field EMPTY
    /// rather than holding a reference whose cast fails at runtime.</item>
    /// </list>
    /// First-party replacement for <c>Assets/SerializeInterface/Editor/</c>. It lives under an
    /// <c>Editor/</c> folder, so it compiles into <c>Assembly-CSharp-Editor</c> and never reaches a
    /// player build - no <c>#if UNITY_EDITOR</c> guard needed, which is the safer of the two
    /// patterns in <c>Docs/CONDITIONAL_COMPILATION.md</c>.
    /// </remarks>
    [CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
    public class RequireInterfaceDrawer : PropertyDrawer
    {
        // Clears the object field's own picker button, so the hint never sits under it.
        const int HintRightInset = 18;
        const int HintExtraWidth = 3;
        const int HintVerticalInset = 1;

        static GUIStyle hintStyle;
        static bool hintStyleIsProSkin;

        RequireInterfaceAttribute Required => (RequireInterfaceAttribute)attribute;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // One line for a scalar - identical to the base implementation for an object
            // reference. The array branch draws a size field plus a line per element, which the
            // base implementation does not know about: the drop this replaced drew those extra
            // lines over whatever was underneath.
            if (!IsArrayRoot(property)) return EditorGUIUtility.singleLineHeight;

            return EditorGUIUtility.singleLineHeight * (property.arraySize + 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Type interfaceType = Required.InterfaceType;

            if (interfaceType == null || !interfaceType.IsInterface)
            {
                DrawMisuse(position, label, interfaceType);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            if (IsArrayRoot(property)) DrawArray(position, property, label, interfaceType);
            else DrawField(position, property, label, interfaceType);

            EditorGUI.EndProperty();
        }

        // Unity normally hands a property drawer each ELEMENT of a marked collection rather than
        // the collection itself, so this branch is defensive rather than routine - no shipped
        // field exercises it today. A string also reports isArray, hence the Generic test.
        static bool IsArrayRoot(SerializedProperty property) =>
            property.isArray && property.propertyType == SerializedPropertyType.Generic;

        void DrawArray(Rect position, SerializedProperty property, GUIContent label, Type interfaceType)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Clamped: the arraySize setter throws on a negative, and an IntField will hand one over.
            property.arraySize = Mathf.Max(0, EditorGUI.IntField(line, $"{label.text} Size", property.arraySize));

            for (int i = 0; i < property.arraySize; i++)
            {
                line.y += EditorGUIUtility.singleLineHeight;
                DrawField(line, property.GetArrayElementAtIndex(i), new GUIContent($"Element {i}"), interfaceType);
            }
        }

        void DrawField(Rect position, SerializedProperty property, GUIContent label, Type interfaceType)
        {
            Object current = property.objectReferenceValue;
            Object picked = EditorGUI.ObjectField(position, label, current, PickerType(interfaceType), true);

            if (picked == null) property.objectReferenceValue = null;
            else if (picked != current) property.objectReferenceValue = Resolve(picked, interfaceType);

            DrawHint(position, property.objectReferenceValue, interfaceType);
        }

        /// <summary>
        /// The tightest type the object picker can be narrowed to. Unity cannot filter a picker by
        /// interface, so this narrows as far as the FIELD's declared type allows and
        /// <see cref="Resolve"/> does the rest.
        /// </summary>
        Type PickerType(Type interfaceType)
        {
            Type elementType = ElementTypeOf(fieldInfo?.FieldType);
            if (elementType == null) return typeof(Object);

            // The field is already declared as something implementing the interface, so the picker
            // can be exact and nothing unqualified is ever offered.
            if (interfaceType.IsAssignableFrom(elementType)) return elementType;

            if (typeof(ScriptableObject).IsAssignableFrom(elementType)) return typeof(ScriptableObject);
            if (typeof(MonoBehaviour).IsAssignableFrom(elementType)) return typeof(MonoBehaviour);

            return typeof(Object);
        }

        static Type ElementTypeOf(Type fieldType)
        {
            if (fieldType == null) return null;
            if (fieldType.IsArray) return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];

            return fieldType;
        }

        /// <summary>
        /// What actually gets stored. A dropped GameObject resolves to the component on it that
        /// implements the interface; anything that cannot satisfy the interface is refused, and the
        /// field is left EMPTY rather than holding a reference that will fail its cast at runtime.
        /// </summary>
        static Object Resolve(Object picked, Type interfaceType)
        {
            if (picked is GameObject gameObject)
            {
                Component component = gameObject.GetComponent(interfaceType);
                if (component != null) return component;
            }
            else if (interfaceType.IsInstanceOfType(picked))
            {
                return picked;
            }

            // Once per assignment, not per frame: Resolve only runs when the picked object changed.
            Debug.LogWarning(
                $"[RequireInterface] '{picked.name}' ({picked.GetType().Name}) does not implement " +
                $"{interfaceType.Name}. The field was left empty.", picked);

            return null;
        }

        /// <summary>
        /// The "(IVessel)" tag on the right-hand end of the field - the only thing on screen that
        /// says WHICH interface is required. Collapses to "*" once something is assigned, and
        /// returns on hover.
        /// </summary>
        static void DrawHint(Rect position, Object assigned, Type interfaceType)
        {
            // Requested on EVERY event, not only Repaint: IMGUI control ids are positional, so
            // skipping the call on some events desynchronises the id stream between layout and
            // repaint. The -1 addresses the object field drawn immediately above, so the hint
            // shares that field's drag-and-drop highlight instead of drawing a second one.
            int controlId = GUIUtility.GetControlID(FocusType.Passive) - 1;
            if (Event.current.type != EventType.Repaint) return;

            EnsureHintStyle();

            bool hovering = position.Contains(Event.current.mousePosition);
            var content = new GUIContent(assigned == null || hovering ? $"({interfaceType.Name})" : "*");

            Vector2 size = hintStyle.CalcSize(content);
            Rect rect = position;
            rect.width = size.x + HintExtraWidth;
            rect.x += position.width - rect.width - HintRightInset;
            rect.y += HintVerticalInset;
            rect.height -= HintVerticalInset * 2;

            hintStyle.Draw(rect, content, controlId, DragAndDrop.activeControlID == controlId, false);
        }

        static void EnsureHintStyle()
        {
            // Rebuilt on a skin flip as well as on first use: the style carries EditorStyles.label's
            // text colour, which differs between the light and dark skins, and a static survives
            // that change.
            if (hintStyle != null && hintStyleIsProSkin == EditorGUIUtility.isProSkin) return;

            hintStyle = new GUIStyle(EditorStyles.label)
            {
                font = EditorStyles.objectField.font,
                fontSize = EditorStyles.objectField.fontSize,
                fontStyle = EditorStyles.objectField.fontStyle,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 2, 0, 0)
            };
            hintStyleIsProSkin = EditorGUIUtility.isProSkin;
        }

        /// <summary>
        /// <c>[RequireInterface(typeof(SomethingThatIsNotAnInterface))]</c>, reported on the field
        /// itself. Never a silent fallback to a plain object field, which is the failure mode this
        /// whole drawer exists to prevent.
        /// </summary>
        static void DrawMisuse(Rect position, GUIContent label, Type given)
        {
            Color previous = GUI.color;
            GUI.color = Color.red;
            EditorGUI.LabelField(position, label,
                new GUIContent($"[RequireInterface] {(given == null ? "null" : given.Name)} is not an interface"));
            GUI.color = previous;
        }
    }
}
