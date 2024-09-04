using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class AnimationData : ScriptableObject
{
    [SerializeField]
    public string AnimationName;
    
    [SerializeField]
    public int AnimationLayer;
}

public class EffectData : ScriptableObject
{
    [SerializeField]
    public GameObject effectPrefab;
}

public class AudioData : ScriptableObject
{
    [SerializeField]
    public AudioClip soundClip;
}

#if UNITY_EDITOR
public class ActionToolWindow : EditorWindow
{
    private const string ActionEventsFolderPath = "Assets/ActionEvents";
    
    private ActionController selectedAction;
    private Vector2 scrollPosition;
    private float previewTime = 0f;
    private ActionController.PreviewState isPreviewState = ActionController.PreviewState.Stop;
    private float lastPreviewUpdateTime;
    private float timelineWidth = 300f;
    private float eventHeight = 20f;
    private float minEventDuration = 0.1f;
    private ActionEvent draggingEvent;
    private bool isDraggingStart;
    private bool isDraggingEnd;

    [MenuItem("Window/Action Tool")]
    public static void ShowWindow()
    {
        GetWindow<ActionToolWindow>("Action Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Action Tool", EditorStyles.boldLabel);

        selectedAction = EditorGUILayout.ObjectField("Action Controller", selectedAction, typeof(ActionController), true) as ActionController;

        if (selectedAction == null)
        {
            EditorGUILayout.HelpBox("Please select an ActionController", MessageType.Info);
            return;
        }

        selectedAction.actionDuration = EditorGUILayout.FloatField("Action Duration", selectedAction.actionDuration);

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
        for (int i = 0; i < selectedAction.actionEvents.Count; i++)
        {
            ActionEvent evt = selectedAction.actionEvents[i];
            EditorGUILayout.BeginVertical(GUI.skin.box);

            evt.eventType = (ActionEventType)EditorGUILayout.EnumPopup("Event Type", evt.eventType);

            EditorGUI.BeginChangeCheck();
            switch (evt.eventType)
            {
                case ActionEventType.Animation:
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
                    break;
                case ActionEventType.Effect:
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
                    break;
                case ActionEventType.Sound:
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
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(evt.eventData);
                EditorUtility.SetDirty(selectedAction);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Timeline");
            Rect timelineRect = GUILayoutUtility.GetRect(timelineWidth, eventHeight);
            DrawEventTimeline(timelineRect, evt);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            evt.startTime = EditorGUILayout.FloatField("Start Time", evt.startTime);
            evt.endTime = EditorGUILayout.FloatField("End Time", evt.endTime);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Remove Event"))
            {
                ActionEvent _evt = selectedAction.actionEvents[i];
                if (_evt.eventData != null)
                {
                    string path = AssetDatabase.GetAssetPath(_evt.eventData);
                    AssetDatabase.DeleteAsset(path);
                }
                selectedAction.actionEvents.RemoveAt(i);
                EditorUtility.SetDirty(selectedAction);
                i--;
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawEventTimeline(Rect timelineRect, ActionEvent evt)
    {
        EditorGUI.DrawRect(timelineRect, new Color(0.5f, 0.5f, 0.5f));

        float startX = timelineRect.x + (evt.startTime / selectedAction.actionDuration) * timelineRect.width;
        float endX = timelineRect.x + (evt.endTime / selectedAction.actionDuration) * timelineRect.width;
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
                    float dragDelta = e.delta.x / timelineRect.width * selectedAction.actionDuration;
                    if (isDraggingStart)
                        evt.startTime = Mathf.Clamp(evt.startTime + dragDelta, 0, evt.endTime - minEventDuration);
                    if (isDraggingEnd)
                        evt.endTime = Mathf.Clamp(evt.endTime + dragDelta, evt.startTime + minEventDuration, selectedAction.actionDuration);
                    EditorUtility.SetDirty(selectedAction);
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
        int newId = selectedAction.GetNextEventId();
        selectedAction.actionEvents.Add(new ActionEvent(newId)
        {
            eventType = ActionEventType.Animation,
            startTime = 0f,
            endTime = selectedAction.actionDuration / 2f
        });
        EditorUtility.SetDirty(selectedAction);
    }

    private void DrawTimelinePreview()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        string btnName = string.Empty;
        if (isPreviewState == ActionController.PreviewState.Play)
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
                isPreviewState = ActionController.PreviewState.Pause;
            }
            else
            {
                isPreviewState = ActionController.PreviewState.Play;
            }
            lastPreviewUpdateTime = (float)EditorApplication.timeSinceStartup;
        }
        if (GUILayout.Button("Stop"))
        {
            isPreviewState = ActionController.PreviewState.Stop;
            previewTime = 0f;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        previewTime = EditorGUILayout.Slider("Time", previewTime, 0f, selectedAction.actionDuration);
        if (EditorGUI.EndChangeCheck())
        {
            isPreviewState = ActionController.PreviewState.Timeline;
            PreviewActionAtTime(previewTime);
        }
        if (isPreviewState != ActionController.PreviewState.Stop 
            && isPreviewState != ActionController.PreviewState.Timeline
            && isPreviewState != ActionController.PreviewState.Pause)
        {
            float deltaTime = (float)EditorApplication.timeSinceStartup - lastPreviewUpdateTime;
            lastPreviewUpdateTime = (float)EditorApplication.timeSinceStartup;
            previewTime += deltaTime;
            if (previewTime > selectedAction.actionDuration)
            {
                previewTime = 0f;
            }
            Repaint();

            PreviewActionAtTime(previewTime);
        }
    }

    private void PreviewActionAtTime(float time)
    {
        selectedAction.BindComponents();
        selectedAction.SetLockAction(isPreviewState);
        
        foreach (var evt in selectedAction.actionEvents)
        {
            selectedAction.UpdateAction(time);
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