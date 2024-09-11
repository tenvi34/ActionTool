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
    // Action Event들의 저장경로 (eventType에 따른)
    private const string ActionEventsFolderPath = "Assets/ActionEvents";
    // Action Prefab의 저장 경로(Action 그 자체)
    private const string ActionPrefabsFolderPath = "Assets/ActionPrefabs";

    // 내가 제어할 캐릭터
    private ActionController preview_actor;
    
    // 현재 프리뷰 할 액션스크립트
    private ActionScript runtimeActionScript;
    
    // 현재 프리뷰 할 액션 스크립트 제어
    private ActionScript_EditorMode runtimeActionScript_EditMode;
    
    // 현재 선택된 액션 스크립트
    private ActionScript selectedActionScript;
    
    // ScrollPoisition Begin부터 End까지를 스크롤로 묶어서 관리할때 현재 스크롤의 좌표
    // scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
    //     
    // DrawEventTimelines();
    //     
    // EditorGUILayout.EndScrollView();
    private Vector2 scrollPosition;
    
    // 프리뷰 중 현재 프레임
    private int previewFrame = 0;
    
    // 현재 프리뷰의 상태
    private PreviewState isPreviewState = PreviewState.Stop;
    
    // 리얼타임(실제시간으로 전 틱에서 기록된 시간 값
    private double lastPreviewUpdateTime;
    
    // 리얼타임(실제시간으로 누적된 시간 값
    private double previewTime = 0.0f;
    
    // 타임라인의 기본 두깨
    private float timelineWidth = 300f;
    
    // 타임라인의 기본 높이
    private float eventHeight = 20f;
    
    // 이벤트를 발생시킬수 있는 최소 프레임
    private int minEventDurationFrames = 1; // Minimum 5 frames for an event
    
    // 타임라인을 드래깅하기 위한 Event
    private ActionEvent draggingEvent;
    
    // 드래깅중인지
    private bool isDraggingStart;
    
    // 드래킹이 끝났는지
    private bool isDraggingEnd;
    
    // 액션 이벤트 네임
    private string actionName;

    // 프레임을 2번째 인자에 맞게 시간단위로 변화시키는 api
    private float FrameToTime(int frame, int framesPerSecond)
    {
        return (float)frame / framesPerSecond;
    }

    // 시간을 2번째 인자에 맞게 프레임 단위로 변화시키는 api
    private int TimeToFrame(float time, int framesPerSecond)
    {
        return Mathf.RoundToInt(time * framesPerSecond);
    }

    // 액션툴이 꺼졌을때
    void OnDestroy()
    {
        DestroyActionScripts();
    }
    

    // 경로를 설정하면 상단의 바에 Custom Menubar를 생성시킬수 있다. 
    [MenuItem("Window/Action Tool")]
    public static void ShowWindow()
    {
        // 액션툴을 켜라
        ActionToolWindow window = GetWindow<ActionToolWindow>("Action Tool");
        
        // 선택된 오브젝트가 있으면 그 오브젝트가 previewActor이다.
        if (Selection.activeGameObject)
            window.preview_actor = Selection.activeGameObject.GetComponent<ActionController>();
        
        // 혹시 맵에 남아있는 ActionScript가 있으면 싹 지운다.
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

    // 해당 Path에 에셋이 유효한지 검사한다.
    public static bool DoesAssetExist(string assetPath)
    {
        // Use AssetDatabase.LoadAssetAtPath to check if the asset exists
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return asset != null;
    }
    
    // 현재 프리뷰중인 캐릭터의 Animator의 runtimeAnimationController의 Layer의 StateMatchine의 State들을 레이어가 몇번이고 StateName 무엇인지 반환하는 함수
    private List<(int, string)> GetAnimationNames(ActionController actionController)
    {
        // BT_ActionController에서 애니메이션 클립 이름 가져오기
        var animator = actionController?.GetComponent<Animator>().runtimeAnimatorController;
        if (animator)
        {
            // 튜플을 이용하여 int는 State의 Layer, string은 stateName을 누적한다.
            List<(int, string)> result = new List<(int, string)>();
            
            // 에디터에서 편집 가능한 AnimatorController를 얻어와서
            // 이 데이터를 잘 건들면 Anmation State Mathcine을 자유자재로 뜯어고칠수 있음
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

    // 에디터상에서 UI가 그려지고 있을때 마치 Update처럼 호출되는 함수
    private void OnGUI()
    {
        GUILayout.Label("Action Tool", EditorStyles.boldLabel);
     
        // BeginChangeCheck ~ EndChangeCheck까지 검사르를해서 EditorGUILayout 의 변경사항이 있으면 후처리를 할수 있는 함수들
        EditorGUI.BeginChangeCheck();
        preview_actor = EditorGUILayout.ObjectField("PreviewActor", preview_actor, typeof(ActionController), true) as ActionController;
        if (EditorGUI.EndChangeCheck())
        {
            // preaview_actor가 변했다면
            if (runtimeActionScript)
            {
                // 툴이기 때문에 즉시 삭제를 해준다.
                DestroyImmediate(runtimeActionScript.gameObject);
            }
            
            // preview_actor가 변한것은 현재 액션을 안쓴다고 가정하고 null화
            selectedActionScript = null;
        }
        
        EditorGUI.BeginChangeCheck();
        selectedActionScript = EditorGUILayout.ObjectField("ActionScript", selectedActionScript, typeof(ActionScript), false) as ActionScript;
        if (EditorGUI.EndChangeCheck())
        {
            CreateRuntimeScript();
        }
        
        // 에디터 다시그리기
        Repaint();
        
        // 수평으로 UI를 정렬 되게 배치한다.
        EditorGUILayout.BeginHorizontal();
        actionName = EditorGUILayout.TextField("ActionName", actionName);

        // 버튼이 눌렸다면 ture이다.
        if (GUILayout.Button("Add New ActionScript"))
        {
            // 해당 경로에 액션을 생성하는데 이미 있다면 생성실패다.
            string actionPreafabName = $"{ActionPrefabsFolderPath}/{actionName}.prefab";
            if (DoesAssetExist(actionPreafabName))
            {
                EditorUtility.DisplayDialog("알림", "이미 존재하는 액션 이름입니다.", "확인");
            }
            else
            {
                // 에셋을 생성하고 저장한다.
                GameObject prefab = new GameObject(actionName);
                prefab.AddComponent<ActionScript>();
                prefab.AddComponent<ActionScript_EditorMode>();
                
                // Assets에 저장한다.
                PrefabUtility.SaveAsPrefabAsset(prefab, actionPreafabName);
                // 저장했으니 임시파일은 삭제한다.
                DestroyImmediate(prefab);
                // ctrl + s를 작동시킨다.
                AssetDatabase.SaveAssets();
                
                // 방금 생성한 오브젝트를 로드한다.
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
        
        // 액션의 총 길이(시간)을 설정한다.
        selectedActionScript!.totalFrames = EditorGUILayout.IntField("Total Frames", selectedActionScript.totalFrames);
        
        // 해당 액션이 몇 프레임 기준 action인지 기술한다. 60프레임 이면 1초에 60번 호출되는거다.
        selectedActionScript!.framesPerSecond = EditorGUILayout.IntField("Frames Per Second", selectedActionScript.framesPerSecond);
        
        if (GUILayout.Button("Add New Event"))
        {
            // 이벤트를 추가한다.
            AddNewEvent();
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        DrawEventTimelines();
        
        EditorGUILayout.EndScrollView();
        
        DrawTimelinePreview();
    }

    private void DrawEventTimelines()
    {
        // 내가 추가한 actionEvent들을 UI화 시켜서 제어 가능한 UI형태가 되는 코드
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
                    // 만약 이미 eventData의 타입이 AnimationData 타입이면
                    if (evt.eventData is AnimationData data1)
                    {
                        // 프리뷰 캐릭터의 Animator의 runtimeAnimatorController의 state들을 모두 가져온다.
                        List<(int, string)> names = GetAnimationNames(preview_actor);
                        
                        // 현재 내 액션네임과 매칭시킨다.
                        int selectedAnimationIndex = names.IndexOf((data1.AnimationLayer, data1.AnimationName));
                        if (names.Count > 0)
                        {
                            //스테이트가 하나라도 있으면 그 선택된 ActionName과 Layer로 설정한다.
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
                    // 그 값이 비어있으면
                    else
                    {
                        // 그 값을 object로 생성하고
                        evt.eventData =  CreateInstance<AnimationData>();
                        
                        // 액션 스크립트 네임은 네이밍 하기 힘드니까 그냥 guid로 랜덤하게 만들어준다. ( 가독성 문제가 좀 있다. )
                        string uniqueName = AssetDatabase.GenerateUniqueAssetPath($"{ActionEventsFolderPath}/{Guid.NewGuid().ToString()}.asset");
                        AssetDatabase.CreateAsset(evt.eventData , uniqueName);
                        AssetDatabase.SaveAssets();
                        // ctrl + s 효과를 낸다.
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
                                                    // 현재 내가 갖고있는 animator의 state들의 프렝미이 0보다 작아지거나 totalFrames보다 커질 위험성을 없애는 함수
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
        // 에디터에서만 작동하는 EditMode스크립트가 가진 DamageField를 모두 제거한다.
        if (runtimeActionScript_EditMode)
        {
            runtimeActionScript_EditMode.ClearDamageFields();
            runtimeActionScript_EditMode = null;
        }
        
        // 현재 쓰고있는 스크립트가 있으면 삭제한다.
        if (runtimeActionScript)
        {
            DestroyImmediate(runtimeActionScript.gameObject);
        }

        if (preview_actor && selectedActionScript)
        {
            GameObject go = Instantiate(selectedActionScript.gameObject, preview_actor.transform.position,
                preview_actor.transform.rotation);
            
            // 프리뷰를 위한 setting을 해준다.
            runtimeActionScript = go.AddComponent<ActionScript>();
            runtimeActionScript_EditMode = go.AddComponent<ActionScript_EditorMode>();
        }
    }

    private float deltaX = 0.0f;
    
    //
    private void DrawEventTimeline(Rect timelineRect, ActionEvent evt)
{
    // 최대기링가 timelineRect의 width인 박스를 생성한다.
    EditorGUI.DrawRect(timelineRect, new Color(0.5f, 0.5f, 0.5f));

    // rect밖을 벗어나지 않도록 계산한다.
    float startX = timelineRect.x + ((float)evt.startFrame / selectedActionScript.totalFrames) * timelineRect.width;
    float endX = timelineRect.x + ((float)evt.endFrame / selectedActionScript.totalFrames) * timelineRect.width;
    //
    // 현재 액션이벤트의 Start와 End를 그려준다. ( 나중에는 OnceTime이나 랜덤타입 추가예정
    Rect eventRect = new Rect(startX, timelineRect.y, endX - startX, timelineRect.height);

    EditorGUI.DrawRect(eventRect, GetEventColor(evt.eventType));

    // 마우스가 사이즈를 변경할수 있는 박스의 끝이면 늘어나는 ui로 변경한다.
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
                // 현재 마우스가 얼마나 움직였는지를 저장
                deltaX += e.delta.x;
                
                // width를 totalFrames으로 나눠 계산하기 위해 필요한 변수
                float pixelsPerFrame = timelineRect.width / selectedActionScript.totalFrames;
                int dragDelta = (int)(deltaX / pixelsPerFrame);
                
                if (dragDelta != 0)  // Only update if there's at least 1 frame of movement
                {
                    // 델타값이 이게 0이 아니면
                    deltaX = 0.0f;
                    
                    // 그리고 드래그 스타트 부분을 조종하고 싶을 때 (타이밍 )
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
                    
                    // 드래그 엔드를 조종하고 싶을 때 (타이핑 )
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
        
        // 이 데이터는 변경되었음을 유니티에 알린다.
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