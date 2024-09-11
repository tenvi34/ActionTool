#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

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
        ClearDamageFields();
    }

    public void ClearDamageFields()
    {
        if (_actionScript.IsUnityNull())
            return;
        
        foreach (var actionEvent in _actionScript.actionEvents)
        {
            foreach (var actionEventActiveDamageField in actionEvent.activeDamageFields)
            {
                if (actionEventActiveDamageField.IsUnityNull())
                    continue;
                
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
                                system.Simulate(FrameToTime(curFrame - actionEvent.startFrame, _actionScript.framesPerSecond), true, true);
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
