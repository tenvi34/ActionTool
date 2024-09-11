#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


// 에디터 에서 액션을 프리뷰할 때 대리자 역활로 ActionScript랑 분리해서
// 에디터 모드에서만 사용할수 있도록 개조한 컴포넌트다.
public class ActionScript_EditorMode : MonoBehaviour
{
    private ActionController actionController;
    private Animator _animator;
    private ActionScript _actionScript;
    
    public void SetActionController(ActionController _actionController)
    {
        actionController = _actionController;
        BindComponents();
    }
    
    public void BindComponents()
    {
        if (actionController)
        {
            _animator = actionController.GetComponent<Animator>();
            _actionScript = GetComponent<ActionScript>();
        }
    }
    
    public void UpdateAction(int curFrame)
    {
        _actionScript.IsPlaying = true;
        _actionScript.UpdateAction(curFrame);
        foreach (var evt in _actionScript.actionEvents)
        {
            UpdateActionEvent(evt, curFrame);
        }
        _actionScript.IsPlaying = false;
    }
    
    
    private float FrameToTime(int frame, int framesPerSecond)
    {
        return (float)frame / framesPerSecond;
    }

    private void OnDestroy()
    {
        // 자기 자신이 삭제될때 자신이 생성한 DamageField를 삭제하고 사라진다.
        ClearDamageFields();
    }

    public void ClearDamageFields()
    {
        // 이미 _actionScript null이면 어쩔수 없이 리턴
        if (_actionScript.IsUnityNull())
            return;
        
        // 그게 아니라면 데미지 필드 제거
        foreach (var actionEvent in _actionScript.actionEvents)
        {
            // 현재 엑티브 된 데미지 필드 모두삭제
            foreach (var actionEventActiveDamageField in actionEvent.activeDamageFields)
            {
                if (actionEventActiveDamageField.IsUnityNull())
                    continue;
                
                // editor 중에서도 EditorApplication.isPlaying 플레이 중이면 Destroy 아니면 즉시 삭제 
                if (EditorApplication.isPlaying)
                    Destroy(actionEventActiveDamageField);
                else
                {
                    DestroyImmediate(actionEventActiveDamageField);
                }
            }
            actionEvent.activeDamageFields.Clear();
        }
    }

    private void UpdateActionEvent(ActionEvent evt, int curFrame)
    {
        #if UNITY_EDITOR
        switch (evt.eventType)
        {
            case ActionEventType.Animation:
                AnimationData data = evt.eventData as AnimationData;
                
                // curFrame에 대해 애니메이션을 1frmae만 재생하기 위한 트릭
                if (curFrame >= evt.startFrame)
                {
                    _animator.Play(data.AnimationName, data.AnimationLayer, (curFrame - evt.startFrame) / (float)(evt.endFrame - evt.startFrame));
                    _animator.Update(0.0f);
                }
                break;
            case ActionEventType.Sound:
                break;
            case ActionEventType.Effect:
            {
                foreach (var actionEvent in _actionScript.actionEvents)
                {
                    if (actionEvent.isActive && actionEvent.activeEffect)
                    {
                        ParticleSystem[] pss = actionEvent.activeEffect.GetComponentsInChildren<ParticleSystem>();
                        foreach (var system in pss)
                        {
                            if (curFrame >= actionEvent.startFrame)
                            {
                                // 이펙트도 시뮬레이션을 1프레임만 하고 Pause한다.
                                system.Simulate(FrameToTime(curFrame - actionEvent.startFrame, _actionScript.framesPerSecond), true, true);
                                // 그럼 1프레임 움직인걸로 보인다.
                                system.Pause(true);
                            }
                        }
                    }
                }
            }
                break;
            case ActionEventType.DamageField:
                foreach (var actionEvent in _actionScript.actionEvents)
                {
                    if (actionEvent.isActive)
                    {
                        foreach (var actionEventActiveDamageField in actionEvent.activeDamageFields)
                        {
                            DamageField df = actionEventActiveDamageField.GetComponent<DamageField>();
                            if (curFrame >= actionEvent.startFrame)
                            {
                                // UpdateActionEvent를 호출하면 그 안에서 DamageField를 시뮬레이션 한다. ( 아직 완성전 )
                                df.UpdateActionEvent(curFrame - actionEvent.startFrame);
                            }
                        }
                    }
                }

                break;
        }
        #endif
    }
}

#endif
