using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum ActionEventType { Animation, Effect, Sound }

[System.Serializable]
public class ActionEvent
{
    public int id;
    public ActionEventType eventType;
    public ScriptableObject eventData; // AnimationClip, GameObject (for effect), or AudioClip
    public float startTime;
    public float endTime;
    public bool isActive = false;
    public GameObject activeEffect;
    public AudioSource activeAudio;

    public ActionEvent(int id)
    {
        this.id = id;
    }
}

public class ActionController : MonoBehaviour
{
    public float actionDuration = 5f;
    public List<ActionEvent> actionEvents = new List<ActionEvent>();
    private int nextEventId = 1;
    private float currentTime = 0f;
    private bool isPlaying = false;
    private Animator _animator;

#if UNITY_EDITOR
    public enum PreviewState
    {
        Stop,
        Play,
        Pause,
        Timeline
    }
    
    public float originSpeed;
    public bool bLockAction;
    public void SetLockAction(PreviewState _v)
    {
        if (_v == PreviewState.Stop)
        {
            bLockAction = false;
        }
        else
        {
            bLockAction = true;
        }
        
        if (bLockAction)
        {
            if (_animator.speed != 0)
                originSpeed = _animator.speed;

            _animator.speed = 0.0f;
        }
        else
        {
            _animator.speed = originSpeed;
        }
    }
#endif
    
    void Awake()
    {
        BindComponents();
    }

    public void BindComponents()
    {
        _animator = GetComponent<Animator>();
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
    }

    private void Update()
    {
        if (!isPlaying) return;

        if (bLockAction)
        {
            _animator.speed = 0.0f;
            return;
        }

        UpdateAction(currentTime);
        currentTime += Time.deltaTime;
    }

    public void UpdateAction(float in_currentTime)
    {
        if (0 >= in_currentTime)
        {
            StartAction();
        }
        
        foreach (var evt in actionEvents)
        {
            if (in_currentTime >= evt.startTime && in_currentTime < evt.endTime && !evt.isActive)
            {
                StartActionEvent(evt);
            }
            else if ((in_currentTime >= evt.endTime || in_currentTime < evt.startTime) && evt.isActive)
            {
                StopActionEvent(evt);
            }

            if (evt.isActive)
            {
                UpdateActionEvent(evt, in_currentTime);
            }
        }
        
        if (in_currentTime >= actionDuration)
        {
            StopAction();
        }
    }

    private void UpdateActionEvent(ActionEvent evt, float in_currentTime)
    {
        #if UNITY_EDITOR
        switch (evt.eventType)
        {
            case ActionEventType.Animation:
                AnimationData data = evt.eventData as AnimationData;
                _animator.Play(data.AnimationName, data.AnimationLayer, in_currentTime / actionDuration);
                _animator.Update(0.0f);
                break;
        }
        #endif
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
        if (evt.activeEffect != null)
        {
            Destroy(evt.activeEffect);
            evt.activeEffect = null;
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
            Destroy(evt.activeAudio);
            evt.activeAudio = null;
        }
    }
}