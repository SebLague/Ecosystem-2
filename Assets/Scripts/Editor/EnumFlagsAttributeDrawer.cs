using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// Script from:
// https://www.kevin-agwaze.com/tip-of-the-week-4-working-with-bitflag-enums-in-unity-editor/
[CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
public class EnumFlagsAttributeDrawer : PropertyDrawer
{
    private string[] enumNames;
    private readonly Dictionary<string, int> enumNameToValue = new Dictionary<string, int>();
    private readonly Dictionary<string, string> enumNameToDisplayName = new Dictionary<string, string>();
    private readonly Dictionary<string, string> enumNameToTooltip = new Dictionary<string, string>();
    private readonly List<string> activeEnumNames = new List<string>();

    private SerializedProperty serializedProperty;
    private ReorderableList reorderableList;

    private bool firstTime = true;

    private Type EnumType
    {
        get { return fieldInfo.FieldType; }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        serializedProperty = property;
        SetupIfFirstTime();
        return reorderableList.GetHeight();
    }

    private void SetupIfFirstTime()
    {
        if (!firstTime)
        {
            return;
        }

        enumNames = serializedProperty.enumNames;

        CacheEnumMetadata();
        ParseActiveEnumNames();

        reorderableList = GenerateReorderableList();
        firstTime = false;
    }

    private void CacheEnumMetadata()
    {
        for (var index = 0; index < enumNames.Length; index++)
        {
            enumNameToDisplayName[enumNames[index]] = serializedProperty.enumDisplayNames[index];
        }

        foreach (string enumName in enumNames)
        {
            enumNameToTooltip[enumName] = EnumType.Name + "." + enumName;
        }

        foreach (string name in enumNames)
        {
            enumNameToValue.Add(name, (int)Enum.Parse(EnumType, name));
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginDisabledGroup(serializedProperty.hasMultipleDifferentValues);
        reorderableList.DoList(position);
        EditorGUI.EndDisabledGroup();
    }

    private ReorderableList GenerateReorderableList()
    {
        return new ReorderableList(activeEnumNames, typeof(string), false, true, true, true)
        {

            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, new GUIContent(serializedProperty.displayName, "EnumType: " + EnumType.Name));
            },
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2;
                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    new GUIContent(enumNameToDisplayName[activeEnumNames[index]], enumNameToTooltip[activeEnumNames[index]]),
                    EditorStyles.label);

            },
            onAddDropdownCallback = (Rect buttonRect, ReorderableList l) =>
            {
                var menu = new GenericMenu();
                foreach (string enumName in enumNames)
                {
                    if (activeEnumNames.Contains(enumName) == false)
                    {
                        menu.AddItem(new GUIContent(enumNameToDisplayName[enumName]),
                            false, data =>
                            {
                                if (enumNameToValue[(string)data] == 0)
                                {
                                    activeEnumNames.Clear();
                                }
                                activeEnumNames.Add((string)data);
                                SaveActiveValues();
                                ParseActiveEnumNames();
                            },
                            enumName);
                    }
                }
                menu.ShowAsContext();
            },
            onRemoveCallback = l =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(l);
                SaveActiveValues();
                ParseActiveEnumNames();
            }
        };
    }


    private void ParseActiveEnumNames()
    {
        activeEnumNames.Clear();
        foreach (string enumValue in enumNames)
        {
            if (IsFlagSet(enumValue))
            {
                activeEnumNames.Add(enumValue);
            }
        }
    }

    private bool IsFlagSet(string enumValue)
    {
        if (enumNameToValue[enumValue] == 0)
        {
            return serializedProperty.intValue == 0;
        }
        return (serializedProperty.intValue & enumNameToValue[enumValue]) == enumNameToValue[enumValue];
    }

    private void SaveActiveValues()
    {
        serializedProperty.intValue = ConvertActiveNamesToInt();
        serializedProperty.serializedObject.ApplyModifiedProperties();
    }

    private int ConvertActiveNamesToInt()
    {
        return activeEnumNames.Aggregate(0, (current, activeEnumName) => current | enumNameToValue[activeEnumName]);
    }

}