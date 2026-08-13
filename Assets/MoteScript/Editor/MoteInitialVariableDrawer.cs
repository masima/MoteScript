#if UNITY_EDITOR
using MoteScript.Unity;
using UnityEditor;
using UnityEngine;

namespace MoteScript.Editor
{
	[CustomPropertyDrawer(typeof(MoteInitialVariable))]
	internal sealed class MoteInitialVariableDrawer : PropertyDrawer
	{
		public override void OnGUI(
			Rect position,
			SerializedProperty property,
			GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			SerializedProperty name = property.FindPropertyRelative("_name");
			SerializedProperty type = property.FindPropertyRelative("_type");
			const float typeWidth = 72f;
			float valueWidth = Mathf.Max(80f, position.width * 0.38f);
			float nameWidth = Mathf.Max(
				80f,
				position.width - typeWidth - valueWidth - 8f);
			Rect nameRect = new(position.x, position.y, nameWidth, position.height);
			Rect typeRect = new(nameRect.xMax + 4f, position.y, typeWidth, position.height);
			Rect valueRect = new(typeRect.xMax + 4f, position.y, valueWidth, position.height);

			EditorGUI.PropertyField(nameRect, name, GUIContent.none);
			EditorGUI.PropertyField(typeRect, type, GUIContent.none);
			switch ((MoteInitialValueType)type.enumValueIndex)
			{
				case MoteInitialValueType.Number:
					EditorGUI.PropertyField(
						valueRect,
						property.FindPropertyRelative("_number"),
						GUIContent.none);
					break;
				case MoteInitialValueType.String:
					EditorGUI.PropertyField(
						valueRect,
						property.FindPropertyRelative("_string"),
						GUIContent.none);
					break;
				default:
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUI.TextField(valueRect, "null");
					}
					break;
			}
			EditorGUI.EndProperty();
		}
	}
}
#endif
