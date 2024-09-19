using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

public enum ActionEventType { Animation, Effect, Sound, DamageField }

[System.Serializable]
public class ActionEvent
{
    public int id;
    public ActionEventType eventType;
    public ScriptableObject eventData; // AnimationClip, GameObject (for effect), or AudioClip
    public int startFrame;
    public int endFrame;
    public bool isActive = false;
    public GameObject activeEffect;
    public AudioSource activeAudio;
    public List<GameObject> activeDamageFields = new();
    
    public ActionEvent(int id)
    {
        this.id = id;
    }
}

public class ActionScript : MonoBehaviour
{
    private ActionController actionController;
    
    // 액션의 길이를 frames으로 저장하는 방식
    public int totalFrames = 60;
    // New field for frames per second
    
    // 1초를 몇배로 뉘니게 할지
    public int framesPerSecond = 60;

    public List<ActionEvent> actionEvents = new List<ActionEvent>();
    private int nextEventId = 1;
     
    // 현재 액션의 시간을 저장하고 이 시간을 기준으로 제어한다.
    private float currentTime = 0f;
    
    // 현재 액션 스크립트가 작동 중인지
    private bool isPlaying = false;
    private Animator _animator;

    // 원래 애니메이션의 정보는 private이기도하고 마음대로 변경되면 안되기에 Editor모드에서만 작동하도록 api를 만들었음
#if UNITY_EDITOR
    public bool IsPlaying
    {
        get
        {
            return isPlaying;
        }
        set
        {
            isPlaying = value;
        }
    }
#endif
    
    
    // 추후 쓰일 예정
    public void SetActionController(ActionController _actionController)
    {
        actionController = _actionController;
        BindComponents();
    }
    
    // 그냥 일일이 editor체크하기 귀찮아서 editor면 즉시삭제하고
    // 아니면 그냥 삭제
    void DestroyCustom(Object go)
    {
    #if UNITY_EDITOR
        
        if (!EditorApplication.isPlaying)
        {
            DestroyImmediate(go);
            return;
        }
    #endif
        
        Destroy(go);
    }
    
    private int TimeToFrame(float time)
    {
        return Mathf.RoundToInt(time * framesPerSecond);
    }
    
    
    public void BindComponents()
    {
        if (actionController)
        {
            _animator = actionController.GetComponent<Animator>();
        }
    }

    public int GetNextEventId()
    {
        return nextEventId++;
    }

    public void StartAction()
    {
        isPlaying = true;
        currentTime = 0f;
    }

    public void StopAction()
    {
        isPlaying = false;
        currentTime = 0f;
        foreach (var evt in actionEvents)
        {
            if (evt.isActive)
            {
                StopActionEvent(evt);
            }
        }
        
        Destroy(this.gameObject);
    }
    
    private void Update()
    {
        if (!isPlaying) return;
        
        UpdateAction(TimeToFrame(currentTime));
        currentTime += Time.deltaTime;
    }

    public void UpdateAction(int curFrame)
    {
        if (0 >= curFrame)
        {
            StartAction();
        }
        
        foreach (var evt in actionEvents)
        {
            // 시작 프레임보다 크면서 endFrame보다는 작은 이벤트를 발생시킴, (액티브 안되어있어야 됨)
            if (curFrame >= evt.startFrame && curFrame < evt.endFrame && !evt.isActive)
            {
                StartActionEvent(evt);
            }
            
            // endFrame을 넘어갔거나 startFrame보다 작으면 액션을 취소
            else if ((curFrame >= evt.endFrame || curFrame < evt.startFrame) && evt.isActive)
            {
                StopActionEvent(evt);
            }

            // 현재는 사용되지 않으나 추후 확장될거다.
            if (evt.isActive)
            {
                UpdateActionEvent(evt, curFrame);
            }
        }
        
        if (curFrame >= totalFrames)
        {
            StopAction();
        }
    }

    private void UpdateActionEvent(ActionEvent evt, int curFrame)
    {
    }

    private void StartActionEvent(ActionEvent evt)
    {
        evt.isActive = true;
        Debug.Log($"Starting event: {evt.id}");

        switch (evt.eventType)
        {
            case ActionEventType.Animation:
                PlayAnimation(evt);
                break;
            case ActionEventType.Effect:
                PlayEffect(evt);
                break;
            case ActionEventType.Sound:
                PlaySound(evt);
                break;
            case ActionEventType.DamageField:
                SpawnDamageField(evt);
                break;
        }
    }

    private void StopActionEvent(ActionEvent evt)
    {
        evt.isActive = false;
        Debug.Log($"Stopping event: {evt.id}");

        switch (evt.eventType)
        {
            case ActionEventType.Animation:
                StopAnimation(evt);
                break;
            case ActionEventType.Effect:
                StopEffect(evt);
                break;
            case ActionEventType.Sound:
                StopSound(evt);
                break;
            case ActionEventType.DamageField:
                StopDamageField(evt);
                break;
        }
    }

    private void PlayAnimation(ActionEvent evt)
    {
        var animationData = evt.eventData as AnimationData;
        if (_animator != null && animationData != null)
        {
            _animator.Play(animationData.AnimationName, animationData.AnimationLayer);
        }
    }

    private void StopAnimation(ActionEvent evt)
    {
        // Optionally reset to a default animation state
    }

    private void PlayEffect(ActionEvent evt)
    {
        if (evt.eventData is EffectData effectData)
        {
            evt.activeEffect = Instantiate(effectData.effectPrefab, transform.position, Quaternion.identity);
        }
    }

    private void StopEffect(ActionEvent evt)
    {
        Debug.Log(evt.activeEffect);
        
        if (evt.activeEffect != null)
        {
            DestroyCustom(evt.activeEffect);
            evt.activeEffect
                = null;
        }
    }
    
    private void SpawnDamageField(ActionEvent evt)
    {
        if (evt.eventData is DamageFieldData data)
        {
            GameObject instance = Instantiate(data.damageFieldPrefab, transform.position, Quaternion.identity);
            instance.GetComponent<DamageField>().Owner = gameObject;
            evt.activeDamageFields.Add(instance);
        }
    }

    private void StopDamageField(ActionEvent evt)
    {
        if (evt.eventData is DamageFieldData data)
        {
            if (data.EndActionType == DamageFieldEndAction.Destroy)
            {
                foreach (var evtActiveDamageField in evt.activeDamageFields)
                {
                    DestroyCustom(evtActiveDamageField);
                }
            }
            
            evt.activeDamageFields.Clear();
        }
    }

    private void PlaySound(ActionEvent evt)
    {
        if (evt.eventData is AudioData data)
        {
            evt.activeAudio = gameObject.AddComponent<AudioSource>();
            evt.activeAudio.clip = data.soundClip;
            evt.activeAudio.Play();
        }
    }

    private void StopSound(ActionEvent evt)
    {
        if (evt.activeAudio != null)
        {
            evt.activeAudio.Stop();
            DestroyCustom(evt.activeAudio);
            evt.activeAudio = null;
        }
    }
}