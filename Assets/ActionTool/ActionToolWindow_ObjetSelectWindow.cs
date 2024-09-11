using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class ActionToolWindow_ObjetSelectWindow : EditorWindow
{   
    private List<string> actionScriptPrefabPaths = new List<string>();
    private Vector2 scrollPosition;

    public Action<GameObject> SelectedActionScript;
    
    public static void ShowWindow()
    {
        GetWindow<ActionToolWindow_ObjetSelectWindow>("ObjetSelectWindow");
    }

    private void OnEnable()
    {
        // 켜질 때 써칭 한번 한다.
        RefreshPrefabList();
    }

    public void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (string path in actionScriptPrefabPaths)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path));
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                SelectedActionScript?.Invoke(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                Selection.activeGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshPrefabList()
    {
        actionScriptPrefabPaths.Clear();
        
        // find Assets을 하는데 type이 Prefab인 오브젝트를  ActionToolWindow.ActionPrefabsFolderPath여기 안에서 찾아와라
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ActionToolWindow.ActionPrefabsFolderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // ActionScript를 포함하는 prefab인지 검사해서
            if (ContainsActionScript(path))
            {
                // 포함되어 있으면 리스트에 추가
                actionScriptPrefabPaths.Add(path);
            }
        }
    }

    private bool ContainsActionScript(string prefabPath)
    {
        var components = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
        foreach (var component in components)
        {
            if (component != null && component.GetType().Name == "ActionScript")
            {
                return true;
            }
        }
        return false;
    }
}