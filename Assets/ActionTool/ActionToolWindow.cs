using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;


#if UNITY_EDITOR
public class ActionToolWindow : EditorWindow
{
    private const string ActionEventsFolderPath = "Assets/ActionEvents";
    private const string ActionPrefabsFolderPath = "Assets/ActionPrefabs";

    private ActionController preview_actor;
    private ActionScript runtimeActionScript;
    
    private ActionScript selectedActionScript;
    private Vector2 scrollPosition;
    private float previewTime = 0f;
    private ActionScript.PreviewState isPreviewState = ActionScript.PreviewState.Stop;
    private float lastPreviewUpdateTime;
    private float timelineWidth = 300f;
    private float eventHeight = 20f;
    private float minEventDuration = 0.1f;
    private ActionEvent draggingEvent;
    private bool isDraggingStart;
    private bool isDraggingEnd;

    private string actionName;

    void OnDestroy()
    {
        DestroyActionScripts();
    }

    [MenuItem("Window/Action Tool")]
    public static void ShowWindow()
    {
        ActionToolWindow window = GetWindow<ActionToolWindow>("Action Tool");
        window.preview_actor = Selection.activeGameObject.GetComponent<ActionController>();
        DestroyActionScripts();
    }

    private static void DestroyActionScripts()
    {
        ActionScript[] actionScripts = GameObject.FindObjectsOfType<ActionScript>();
        foreach (var actionScript in actionScripts)
        {
            DestroyImmediate(actionScript.gameObject);
        }
    }

    public static bool DoesAssetExist(string assetPath)
    {
        // Use AssetDatabase.LoadAssetAtPath to check if the asset exists
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return asset != null;
    }

    private void OnGUI()
    {
        GUILayout.Label("Action Tool", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        preview_actor = EditorGUILayout.ObjectField("PreviewActor", preview_actor, typeof(ActionController), true) as ActionController;
        if (EditorGUI.EndChangeCheck())
        {
            DestroyImmediate(runtimeActionScript);
            selectedActionScript = null;
        }
        
        EditorGUI.BeginChangeCheck();
        selectedActionScript = EditorGUILayout.ObjectField("ActionScript", selectedActionScript, typeof(ActionScript), false) as ActionScript;
        if (EditorGUI.EndChangeCheck())
        {
            CreateRuntimeScript();
        }

        Repaint();
        
        EditorGUILayout.BeginHorizontal();
        actionName = EditorGUILayout.TextField("ActionName", actionName);
        
        if (GUILayout.Button("Add New ActionScript"))
        {
            string actionPreafabName = $"{ActionPrefabsFolderPath}/{actionName}.prefab";
            if (DoesAssetExist(actionPreafabName))
            {
                EditorUtility.DisplayDialog("알림", "이미 존재하는 액션 이름입니다.", "확인");
            }
            else
            {
                GameObject prefab = new GameObject(actionName);
                prefab.AddComponent<ActionScript>();
                PrefabUtility.SaveAsPrefabAsset(prefab, actionPreafabName);
                DestroyImmediate(prefab);
                AssetDatabase.SaveAssets();
                prefab = PrefabUtility.LoadPrefabContents(actionPreafabName);
                selectedActionScript =  prefab.GetComponent<ActionScript>();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (selectedActionScript == null)
        {
            EditorGUILayout.HelpBox("Please select an ActionScript", MessageType.Info);
            return;
        }
        
        selectedActionScript!.actionDuration = EditorGUILayout.FloatField("Action Duration", selectedActionScript.actionDuration);
        
        if (GUILayout.Button("Add New Event"))
        {
            AddNewEvent();
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        DrawEventTimelines();
        
        EditorGUILayout.EndScrollView();
        
        DrawTimelinePreview();
    }

    private void DrawEventTimelines()
    {
        for (int i = 0; i < selectedActionScript.actionEvents.Count; i++)
        {
            ActionEvent evt = selectedActionScript.actionEvents[i];
            EditorGUILayout.BeginVertical(GUI.skin.box);
    
            evt.eventType = (ActionEventType)EditorGUILayout.EnumPopup("Event Type", evt.eventType);
    
            EditorGUI.BeginChangeCheck();
            switch (evt.eventType)
            {
                case ActionEventType.Animation:
                    EditorGUI.BeginChangeCheck();
                    if (evt.eventData is AnimationData data1)
                    {
                        data1.AnimationName = EditorGUILayout.TextField("AnimationName", data1.AnimationName);
                        data1.AnimationLayer = EditorGUILayout.IntField("AnimationLayer", data1.AnimationLayer);
                        evt.eventData = data1;
                    }
                    else
                    {
                        evt.eventData =  CreateInstance<AnimationData>();
                        
                        string uniqueName = AssetDatabase.GenerateUniqueAssetPath($"{ActionEventsFolderPath}/{Guid.NewGuid().ToString()}.asset");
                        AssetDatabase.CreateAsset(evt.eventData , uniqueName);
                        AssetDatabase.SaveAssets();
                    }
    
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (preview_actor)
                        {
                            if (evt.eventData is AnimationData animationData)
                            {
                                Animator animator = preview_actor.GetComponent<Animator>();
                                if (animator)
                                {
                                    var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                                    if (controller)
                                    {
                                        foreach (var childAnimatorState in controller.layers[animationData.AnimationLayer].stateMachine.states)
                                        {
                                            if (childAnimatorState.state.name == animationData.AnimationName)
                                            {
                                                if (childAnimatorState.state.motion is AnimationClip clip)
                                                {
                                                    float endtime = evt.startTime + clip.length;
                                                    if (endtime >= selectedActionScript.actionDuration)
                                                    {
                                                        endtime = selectedActionScript.actionDuration;
                                                    }

                                                    evt.endTime = endtime;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case ActionEventType.Effect:
                    EditorGUI.BeginChangeCheck();
                    if (evt.eventData is EffectData data2)
                    {
                        data2.effectPrefab = EditorGUILayout.ObjectField("Effect Prefab", data2.effectPrefab, typeof(GameObject), false) as GameObject;
                    }
                    else
                    {
                        evt.eventData = CreateInstance<EffectData>();
                        
                        string uniqueName = AssetDatabase.GenerateUniqueAssetPath($"{ActionEventsFolderPath}/{Guid.NewGuid().ToString()}.asset");
                        AssetDatabase.CreateAsset(evt.eventData , uniqueName);
                        AssetDatabase.SaveAssets();
                    }
    
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (evt.eventData is EffectData effectData)
                        {
                            if (effectData.effectPrefab)
                            {
                                ParticleSystem ps = effectData.effectPrefab.GetComponent<ParticleSystem>();
                                if (ps)
                                {
                                    float endTime = evt.startTime + ps.main.duration;
                                    if (endTime >= selectedActionScript.actionDuration)
                                    {
                                        endTime = selectedActionScript.actionDuration;
                                    }
    
                                    evt.endTime = endTime;
                                }
                            }
                        }
                    }
                    break;
                case ActionEventType.Sound:
                    EditorGUI.BeginChangeCheck();
                    if (evt.eventData is AudioData data3)
                    {
                        data3.soundClip = EditorGUILayout.ObjectField("Audio Prefab", data3.soundClip, typeof(AudioClip), false) as AudioClip;
                    }
                    else
                    {
                        evt.eventData = CreateInstance<AudioData>();
                        
                        string uniqueName = AssetDatabase.GenerateUniqueAssetPath($"{ActionEventsFolderPath}/{Guid.NewGuid().ToString()}.asset");
                        AssetDatabase.CreateAsset(evt.eventData , uniqueName);
                        AssetDatabase.SaveAssets();
                    }
    
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (evt.eventData is AudioData audioData)
                        {
                            float endTime = evt.startTime + audioData.soundClip.length;
                            if (endTime >= selectedActionScript.actionDuration)
                            {
                                endTime = selectedActionScript.actionDuration;
                            }
    
                            evt.endTime = endTime;
                        }
                    }
    
                    break;
            }
    
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(evt.eventData);
                EditorUtility.SetDirty(selectedActionScript);
                CreateRuntimeScript();
            }
    
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Timeline");
            Rect timelineRect = GUILayoutUtility.GetRect(timelineWidth, eventHeight);
            DrawEventTimeline(timelineRect, evt);
            EditorGUILayout.EndHorizontal();
    
            EditorGUILayout.BeginHorizontal();
            
            
            EditorGUI.BeginChangeCheck();
            evt.startTime = EditorGUILayout.FloatField("Start Time", evt.startTime);
            evt.endTime = EditorGUILayout.FloatField("End Time", evt.endTime);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(evt.eventData);
                EditorUtility.SetDirty(selectedActionScript);
                CreateRuntimeScript();
            }
            EditorGUILayout.EndHorizontal();
    
            if (GUILayout.Button("Remove Event"))
            {
                ActionEvent _evt = selectedActionScript.actionEvents[i];
                if (_evt.eventData != null)
                {
                    string path = AssetDatabase.GetAssetPath(_evt.eventData);
                    AssetDatabase.DeleteAsset(path);
                }
                selectedActionScript.actionEvents.RemoveAt(i);
                EditorUtility.SetDirty(selectedActionScript);
                i--;
            }
    
            EditorGUILayout.EndVertical();
        }
    }

    private void CreateRuntimeScript()
    {
        if (runtimeActionScript)
            DestroyImmediate(runtimeActionScript.gameObject);
        
        if (preview_actor && selectedActionScript)
        {
            runtimeActionScript = Instantiate(selectedActionScript.gameObject, preview_actor.transform.position,
                preview_actor.transform.rotation).GetComponent<ActionScript>();
        }
    }

    //
    private void DrawEventTimeline(Rect timelineRect, ActionEvent evt)
    {
        EditorGUI.DrawRect(timelineRect, new Color(0.5f, 0.5f, 0.5f));
    
        float startX = timelineRect.x + (evt.startTime / selectedActionScript.actionDuration) * timelineRect.width;
        float endX = timelineRect.x + (evt.endTime / selectedActionScript.actionDuration) * timelineRect.width;
        Rect eventRect = new Rect(startX, timelineRect.y, endX - startX, timelineRect.height);
    
        EditorGUI.DrawRect(eventRect, GetEventColor(evt.eventType));
    
        EditorGUIUtility.AddCursorRect(new Rect(eventRect.x, eventRect.y, 5, eventRect.height), MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(new Rect(eventRect.xMax - 5, eventRect.y, 5, eventRect.height), MouseCursor.ResizeHorizontal);
    
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && eventRect.Contains(e.mousePosition))
                {
                    draggingEvent = evt;
                    if (Mathf.Abs(e.mousePosition.x - eventRect.x) <= 5)
                        isDraggingStart = true;
                    else if (Mathf.Abs(e.mousePosition.x - eventRect.xMax) <= 5)
                        isDraggingEnd = true;
                    else
                        isDraggingStart = isDraggingEnd = true;
                    e.Use();
                }
                break;
    
            case EventType.MouseDrag:
                if (draggingEvent == evt)
                {
                    float dragDelta = e.delta.x / timelineRect.width * selectedActionScript.actionDuration;
                    if (isDraggingStart)
                        evt.startTime = Mathf.Clamp(evt.startTime + dragDelta, 0, evt.endTime - minEventDuration);
                    if (isDraggingEnd)
                        evt.endTime = Mathf.Clamp(evt.endTime + dragDelta, evt.startTime + minEventDuration, selectedActionScript.actionDuration);
                    EditorUtility.SetDirty(selectedActionScript);
                    CreateRuntimeScript();
                    e.Use();
                }
                break;
    
            case EventType.MouseUp:
                if (draggingEvent == evt)
                {
                    draggingEvent = null;
                    isDraggingStart = isDraggingEnd = false;
                    e.Use();
                }
                break;
        }
    }
    
    private void AddNewEvent()
    {
        int newId = selectedActionScript.GetNextEventId();
        selectedActionScript.actionEvents.Add(new ActionEvent(newId)
        {
            eventType = ActionEventType.Animation,
            startTime = 0f,
            endTime = selectedActionScript.actionDuration / 2f
        });
        EditorUtility.SetDirty(selectedActionScript);
        CreateRuntimeScript();
    }
    
    private void DrawTimelinePreview()
    {
        if (!preview_actor)
            return;
        
        EditorGUILayout.Space();
        GUILayout.Label("Preview", EditorStyles.boldLabel);
    
        EditorGUILayout.BeginHorizontal();
    
        string btnName = string.Empty;
        if (isPreviewState == ActionScript.PreviewState.Play)
        {
            btnName = "Pause";
        }
        else
        {
            btnName = "Play";
        }
        
        if (GUILayout.Button(btnName))
        {
            if (btnName == "Pause")
            {
                isPreviewState = ActionScript.PreviewState.Pause;
            }
            else
            {
                isPreviewState = ActionScript.PreviewState.Play;
            }
            lastPreviewUpdateTime = (float)EditorApplication.timeSinceStartup;
        }
        if (GUILayout.Button("Stop"))
        {
            isPreviewState = ActionScript.PreviewState.Stop;
            previewTime = 0f;
        }
        EditorGUILayout.EndHorizontal();
    
        EditorGUI.BeginChangeCheck();
        previewTime = EditorGUILayout.Slider("Time", previewTime, 0f, selectedActionScript.actionDuration);
        if (EditorGUI.EndChangeCheck())
        {
            isPreviewState = ActionScript.PreviewState.Timeline;
            PreviewActionAtTime(previewTime);
        }
        if (isPreviewState != ActionScript.PreviewState.Stop 
            && isPreviewState != ActionScript.PreviewState.Timeline
            && isPreviewState != ActionScript.PreviewState.Pause)
        {
            float deltaTime = (float)EditorApplication.timeSinceStartup - lastPreviewUpdateTime;
            lastPreviewUpdateTime = (float)EditorApplication.timeSinceStartup;
            previewTime += deltaTime;
            if (previewTime > selectedActionScript.actionDuration)
            {
                previewTime = 0f;
            }
            Repaint();
    
            PreviewActionAtTime(previewTime);
        }
    }
    
    private void PreviewActionAtTime(float time)
    {
        if (!runtimeActionScript)
        {
            return;
        }

        runtimeActionScript.SetActionController(preview_actor);
        runtimeActionScript.SetLockAction(isPreviewState);
        
        foreach (var evt in runtimeActionScript.actionEvents)
        {
            runtimeActionScript.UpdateAction(time);
        }
    
        SceneView.RepaintAll();
    }
    
    private Color GetEventColor(ActionEventType eventType)
    {
        switch (eventType)
        {
            case ActionEventType.Animation:
                return new Color(0.2f, 0.6f, 1f, 0.8f);
            case ActionEventType.Effect:
                return new Color(0.2f, 1f, 0.4f, 0.8f);
            case ActionEventType.Sound:
                return new Color(1f, 0.8f, 0.2f, 0.8f);
            default:
                return Color.gray;
        }
    }
}
#endif