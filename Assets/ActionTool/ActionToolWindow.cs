using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;

public enum PreviewState
{
    Stop,
    Play,
    Pause,
    Timeline
}

#if UNITY_EDITOR
public class ActionToolWindow : EditorWindow
{
    private const string ActionEventsFolderPath = "Assets/ActionEvents";
    private const string ActionPrefabsFolderPath = "Assets/ActionPrefabs";

    private ActionController preview_actor;
    private ActionScript runtimeActionScript;
    private ActionScript_EditorMode runtimeActionScript_EditMode;
    
    private ActionScript selectedActionScript;
    private Vector2 scrollPosition;
    private int previewFrame = 0;
    private PreviewState isPreviewState = PreviewState.Stop;
    private double lastPreviewUpdateTime;
    private float timelineWidth = 300f;
    private float eventHeight = 20f;
    private int minEventDurationFrames = 1; // Minimum 5 frames for an event
    private ActionEvent draggingEvent;
    private bool isDraggingStart;
    private bool isDraggingEnd;

    private double previewTime = 0.0f;

    private string actionName;

    private float FrameToTime(int frame, int framesPerSecond)
    {
        return (float)frame / framesPerSecond;
    }

    private int TimeToFrame(float time, int framesPerSecond)
    {
        return Mathf.RoundToInt(time * framesPerSecond);
    }

    void OnDestroy()
    {
        DestroyActionScripts();
    }

    [MenuItem("Window/Action Tool")]
    public static void ShowWindow()
    {
        ActionToolWindow window = GetWindow<ActionToolWindow>("Action Tool");
        if (Selection.activeGameObject)
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
    
    private List<(int, string)> GetAnimationNames(ActionController actionController)
    {
        // BT_ActionController에서 애니메이션 클립 이름 가져오기
        var animator = actionController?.GetComponent<Animator>().runtimeAnimatorController;
        if (animator)
        {
            List<(int, string)> result = new List<(int, string)>();
            
            var controller = animator as UnityEditor.Animations.AnimatorController;
            if (controller)
            {
                int index = 0;
                foreach (var layer in controller.layers)
                {   
                    foreach (var childAnimatorState in layer.stateMachine.states)
                    {
                        result.Add((index, childAnimatorState.state.name)); 
                    }

                    index++;
                }
            }

            return result;
        }
        else
        {
            return new List<(int, string)>();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Action Tool", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        preview_actor = EditorGUILayout.ObjectField("PreviewActor", preview_actor, typeof(ActionController), true) as ActionController;
        if (EditorGUI.EndChangeCheck())
        {
            if (runtimeActionScript)
            {
                DestroyImmediate(runtimeActionScript.gameObject);
            }
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
                prefab.AddComponent<ActionScript_EditorMode>();
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
        
        selectedActionScript!.totalFrames = EditorGUILayout.IntField("Total Frames", selectedActionScript.totalFrames);
        selectedActionScript!.framesPerSecond = EditorGUILayout.IntField("Frames Per Second", selectedActionScript.framesPerSecond);
        
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
                        List<(int, string)> names = GetAnimationNames(preview_actor);
                        int selectedAnimationIndex = names.IndexOf((data1.AnimationLayer, data1.AnimationName));
                        if (names.Count > 0)
                        {
                            if (selectedAnimationIndex >= 0)
                            {
                                selectedAnimationIndex = EditorGUILayout.Popup("Select Animation", selectedAnimationIndex, names.Select(e => e.Item2).ToArray());
                                data1.AnimationLayer = names[selectedAnimationIndex].Item1;
                                data1.AnimationName = names[selectedAnimationIndex].Item2;
                            }
                            else
                            {
                                EditorGUILayout.LabelField("No animations found.");
                                if (GUILayout.Button($"ResetAnimFile (NowName : {data1.AnimationName ?? "NoName"}) "))
                                {
                                    data1.AnimationLayer = names[0].Item1;
                                    data1.AnimationName = names[0].Item2;
                                }
                            }
                        }
                        else if (!preview_actor)
                        {
                            data1.AnimationName = EditorGUILayout.TextField("AnimationName", data1.AnimationName);
                        }else
                        {
                            EditorGUILayout.LabelField("No Animaitor.");
                            data1.AnimationName = EditorGUILayout.TextField("AnimationName", data1.AnimationName);
                        }
                        
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
                                                    int endtime = evt.startFrame + TimeToFrame(clip.length, selectedActionScript.framesPerSecond);
                                                    if (endtime >= selectedActionScript.totalFrames)
                                                    {
                                                        endtime = selectedActionScript.totalFrames;
                                                    }

                                                    evt.endFrame = endtime;
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
                                    int endTime = evt.startFrame + TimeToFrame(ps.main.duration, selectedActionScript.framesPerSecond);
                                    if (endTime >= selectedActionScript.totalFrames)
                                    {
                                        endTime = selectedActionScript.totalFrames;
                                    }
    
                                    evt.endFrame = endTime;
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
                            int endTime = evt.startFrame + TimeToFrame(audioData.soundClip.length, selectedActionScript.framesPerSecond);
                            if (endTime >= selectedActionScript.totalFrames)
                            {
                                endTime = selectedActionScript.totalFrames;
                            }
    
                            evt.endFrame = endTime;
                        }
                    }
    
                    break;
                case ActionEventType.DamageField:
                    EditorGUI.BeginChangeCheck();
                    if (evt.eventData is DamageFieldData data4)
                    {
                        data4.damageFieldPrefab = EditorGUILayout.ObjectField("DamageField Prefab", data4.damageFieldPrefab, typeof(GameObject), false) as GameObject;
                        data4.EndActionType = (DamageFieldEndAction)EditorGUILayout.EnumPopup("EndAction Type", data4.EndActionType);
                    }
                    else
                    {
                        evt.eventData = CreateInstance<DamageFieldData>();
                        
                        string uniqueName = AssetDatabase.GenerateUniqueAssetPath($"{ActionEventsFolderPath}/{Guid.NewGuid().ToString()}.asset");
                        AssetDatabase.CreateAsset(evt.eventData , uniqueName);
                        AssetDatabase.SaveAssets();
                    }
    
                    if (EditorGUI.EndChangeCheck())
                    {
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
            evt.startFrame = EditorGUILayout.IntField("Start Time", evt.startFrame);
            evt.endFrame = EditorGUILayout.IntField("End Time", evt.endFrame);
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
        if (runtimeActionScript_EditMode)
        {
            runtimeActionScript_EditMode.ClearDamageFields();
            runtimeActionScript_EditMode = null;
        }
        
        if (runtimeActionScript)
        {
            DestroyImmediate(runtimeActionScript.gameObject);
        }

        if (preview_actor && selectedActionScript)
        {
            GameObject go = Instantiate(selectedActionScript.gameObject, preview_actor.transform.position,
                preview_actor.transform.rotation);
            runtimeActionScript = go.AddComponent<ActionScript>();
            runtimeActionScript_EditMode = go.AddComponent<ActionScript_EditorMode>();
        }
    }

    private float deltaX = 0.0f;
    
    //
    private void DrawEventTimeline(Rect timelineRect, ActionEvent evt)
{
    EditorGUI.DrawRect(timelineRect, new Color(0.5f, 0.5f, 0.5f));

    float startX = timelineRect.x + ((float)evt.startFrame / selectedActionScript.totalFrames) * timelineRect.width;
    float endX = timelineRect.x + ((float)evt.endFrame / selectedActionScript.totalFrames) * timelineRect.width;
    Rect eventRect = new Rect(startX, timelineRect.y, endX - startX, timelineRect.height);

    EditorGUI.DrawRect(eventRect, GetEventColor(evt.eventType));

    EditorGUIUtility.AddCursorRect(new Rect(eventRect.x, eventRect.y, 5, eventRect.height), MouseCursor.ResizeHorizontal);
    EditorGUIUtility.AddCursorRect(new Rect(eventRect.xMax - 5, eventRect.y, 5, eventRect.height), MouseCursor.ResizeHorizontal);

    Event e = Event.current;
    switch (e.type)
    {
        case EventType.MouseDown:
            deltaX = 0.0f;
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
                deltaX += e.delta.x;
                
                float pixelsPerFrame = timelineRect.width / selectedActionScript.totalFrames;
                int dragDelta = (int)(deltaX / pixelsPerFrame);
                
                if (dragDelta != 0)  // Only update if there's at least 1 frame of movement
                {
                    deltaX = 0.0f;
                    
                    if (isDraggingStart)
                    {
                        int newStartFrame = Mathf.Clamp(evt.startFrame + dragDelta, 0, evt.endFrame - minEventDurationFrames);
                        if (newStartFrame != evt.startFrame)
                        {
                            evt.startFrame = newStartFrame;
                            EditorUtility.SetDirty(selectedActionScript);
                            CreateRuntimeScript();
                        }
                    }
                    if (isDraggingEnd)
                    {
                        int newEndFrame = Mathf.Clamp(evt.endFrame + dragDelta, evt.startFrame + minEventDurationFrames, selectedActionScript.totalFrames);
                        if (newEndFrame != evt.endFrame)
                        {
                            evt.endFrame = newEndFrame;
                            EditorUtility.SetDirty(selectedActionScript);
                            CreateRuntimeScript();
                        }
                    }
                }
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

    // 현재 드래그 중인 프레임 표시 (선택사항)
    if (draggingEvent == evt)
    {
        int currentFrame = isDraggingStart ? evt.startFrame : evt.endFrame;
        float currentX = timelineRect.x + ((float)currentFrame / selectedActionScript.totalFrames) * timelineRect.width;
        EditorGUI.DrawRect(new Rect(currentX - 1, timelineRect.y, 2, timelineRect.height), Color.white);
        
        string frameText = currentFrame.ToString();
        Vector2 textSize = GUI.skin.label.CalcSize(new GUIContent(frameText));
        GUI.Label(new Rect(currentX - textSize.x / 2, timelineRect.y - textSize.y, textSize.x, textSize.y), frameText);
    }
}

    private void AddNewEvent()
    {
        int newId = selectedActionScript.GetNextEventId();
        selectedActionScript.actionEvents.Add(new ActionEvent(newId)
        {
            eventType = ActionEventType.Animation,
            startFrame = 0,
            endFrame = selectedActionScript.totalFrames / 2
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
    
        string btnName = isPreviewState == PreviewState.Play ? "Pause" : "Play";
        
        if (GUILayout.Button(btnName))
        {
            isPreviewState = isPreviewState == PreviewState.Play ? PreviewState.Pause : PreviewState.Play;
            lastPreviewUpdateTime = EditorApplication.timeSinceStartup;
            previewTime = 0.0f;

            if (isPreviewState == PreviewState.Play)
            {
                runtimeActionScript_EditMode?.ClearDamageFields();
            }
        }
        if (GUILayout.Button("Stop"))
        {
            isPreviewState = PreviewState.Stop;
            previewFrame = 0;
            previewTime = 0.0f;
            runtimeActionScript_EditMode?.ClearDamageFields();
        }
        EditorGUILayout.EndHorizontal();
    
        EditorGUI.BeginChangeCheck();
        previewFrame = EditorGUILayout.IntSlider("Frame", previewFrame, 0, selectedActionScript.totalFrames);
        if (EditorGUI.EndChangeCheck())
        {
            isPreviewState = PreviewState.Timeline;
            PreviewActionAtFrame(previewFrame);
        }
        if (isPreviewState == PreviewState.Play)
        {
            double frameDiff = EditorApplication.timeSinceStartup - lastPreviewUpdateTime;
            previewTime += frameDiff * selectedActionScript.framesPerSecond;
            lastPreviewUpdateTime = EditorApplication.timeSinceStartup;

            previewFrame = (int)previewTime;
            
            if (previewFrame >= selectedActionScript.totalFrames)
            {
                previewFrame = 0;
                previewTime = 0.0f;
            }
            Repaint();
    
            PreviewActionAtFrame(previewFrame);
        }
    }
    
    private void PreviewActionAtFrame(int frame)
    {
        if (runtimeActionScript_EditMode.IsUnityNull())
            return;
        
        runtimeActionScript_EditMode.SetActionController(preview_actor);
        runtimeActionScript_EditMode.UpdateAction(frame);
    
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
            case ActionEventType.DamageField:
                return new Color(1f, 0.0f, 0.2f, 0.8f);
            default:
                return Color.gray;
        }
    }
}
#endif