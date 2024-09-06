using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

public class ActionScript : MonoBehaviour
{
    private ActionController actionController;
    
    public float actionDuration = 5f;
    public List<ActionEvent> actionEvents = new List<ActionEvent>();
    private int nextEventId = 1;
    private float currentTime = 0f;
    private bool isPlaying = false;
    private Animator _animator;

    public void SetActionController(ActionController _actionController)
    {
        actionController = _actionController;
        BindComponents();
    }
    
#if UNITY_EDITOR
    public enum PreviewState
    {
        Stop,
        Play,
        Pause,
        Timeline
    }

    void DestroyCustom(Object go)
    {
        #if UNITY_EDITOR
        {
            if (EditorApplication.isPlaying)
            {
            
            }
            else
            {
                DestroyImmediate(go);
                return;
            }
        }
        #endif
        Destroy(go);
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
            
            foreach (var actionEvent in actionEvents)
            {
                if (actionEvent.isActive && actionEvent.activeEffect)
                {
                    ParticleSystem[] pss = actionEvent.activeEffect.GetComponentsInChildren<ParticleSystem>();
                    foreach (var system in pss)
                    {
                        system.Play(true);
                    }
                }
            }
            
            // AudioSource[] sources = GetComponents<AudioSource>();
            // for (var i = 0; i < sources.Length; i++)
            // {
            //     if (!sources[i].isPlaying)
            //         sources[i].Play();
            // }
        }
        else
        {
            _animator.speed = originSpeed;
            
            foreach (var actionEvent in actionEvents)
            {
                if (actionEvent.isActive && actionEvent.activeEffect)
                {
                    ParticleSystem[] pss = actionEvent.activeEffect.GetComponentsInChildren<ParticleSystem>();
                    foreach (var system in pss)
                    {
                        system.Play(true);
                    }
                }
            }
            
            // AudioSource[] sources = GetComponents<AudioSource>();
            // for (var i = 0; i < sources.Length; i++)
            // {
            //     if (sources[i].isPlaying)
            //         sources[i].Pause();
            // }
        }
    }
#endif
    
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
        foreach (var audioSource in audioSources.Values)
        {
            DestroyCustom(audioSource);
        }
        audioSources.Clear();
        foreach (var coroutine in fadeCoroutines.Values)
        {
            StopCoroutine(coroutine);
        }
        fadeCoroutines.Clear();
        
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
                if (in_currentTime >= evt.startTime)
                {
                    _animator.Play(data.AnimationName, data.AnimationLayer, (in_currentTime - evt.startTime) / (evt.endTime - evt.startTime));
                    _animator.Update(0.0f);
                }
                break;
            case ActionEventType.Sound:
                // if (evt.activeAudio != null)
                // {
                //     AudioData audioData = evt.eventData as AudioData;
                //     if (audioData != null && audioData.soundClip != null)
                //     {
                //         if (!evt.activeAudio.isPlaying)
                //         {
                //             evt.activeAudio.Play();
                //             StartCoroutine(FadeAudioSource(evt.activeAudio, 0, 1, FADE_DURATION));
                //         }
                //         
                //         double dspTime = AudioSettings.dspTime;
                //         evt.activeAudio.SetScheduledEndTime(dspTime + PREVIEW_DURATION);
                //         StartCoroutine(FadeAudioSource(evt.activeAudio, 1, 0, FADE_DURATION, dspTime + PREVIEW_DURATION - FADE_DURATION));
                //     }
                // }
                break;
            case ActionEventType.Effect:
            {
                foreach (var actionEvent in actionEvents)
                {
                    if (actionEvent.isActive && actionEvent.activeEffect)
                    {
                        ParticleSystem[] pss = actionEvent.activeEffect.GetComponentsInChildren<ParticleSystem>();
                        foreach (var system in pss)
                        {
                            if (in_currentTime >= actionEvent.startTime)
                            {
                                system.Simulate(in_currentTime - actionEvent.startTime, true, true);
                                system.Pause(true);
                            }
                        }
                    }
                }
            }
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
        Debug.Log(evt.activeEffect);
        
        if (evt.activeEffect != null)
        {
            DestroyCustom(evt.activeEffect);
            evt.activeEffect
                = null;
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
    
     private const float PREVIEW_DURATION = 0.05f; // 50 밀리초로 증가
    private const float FADE_DURATION = 0.02f; // 20 밀리초로 증가
    private const float CROSSFADE_DURATION = 0.01f; // 10 밀리초 크로스페이드

    private Dictionary<int, AudioSource> audioSources = new Dictionary<int, AudioSource>();
    private Dictionary<int, Coroutine> fadeCoroutines = new Dictionary<int, Coroutine>();

    private void UpdateSound(ActionEvent evt, float in_currentTime)
    {
        if (evt.eventData is AudioData audioData && audioData.soundClip != null)
        {
            if (!audioSources.TryGetValue(evt.id, out AudioSource audioSource))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = audioData.soundClip;
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSources[evt.id] = audioSource;
            }

            float eventDuration = evt.endTime - evt.startTime;
            float currentEventTime = in_currentTime - evt.startTime;
            float normalizedTime = Mathf.Clamp01(currentEventTime / eventDuration);

            if (normalizedTime >= 0 && normalizedTime < 1)
            {
                float clipTime = normalizedTime * audioData.soundClip.length;
                
                // Stop any existing fade coroutine
                if (fadeCoroutines.TryGetValue(evt.id, out Coroutine existingCoroutine))
                {
                    StopCoroutine(existingCoroutine);
                }

                // Start new crossfade
                fadeCoroutines[evt.id] = StartCoroutine(CrossfadeAudioSource(audioSource, clipTime));
            }
            else if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    private IEnumerator FadeAudioSource(AudioSource audioSource, float startVolume, float endVolume, float duration, double startTime = 0)
    {
        if (startTime == 0) startTime = AudioSettings.dspTime;

        while (AudioSettings.dspTime < startTime)
        {
            yield return null;
        }

        startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            double t = (Time.time - startTime) / duration;
            if (audioSource.IsUnityNull())
                yield break;
            
            audioSource.volume = Mathf.Lerp(startVolume, endVolume, (float)t);
            yield return null;
        }
        if (audioSource.IsUnityNull())
            yield break;
        
        audioSource.volume = endVolume;
    }
    
    private IEnumerator CrossfadeAudioSource(AudioSource audioSource, float targetTime)
    {
        // 새로운 AudioSource를 생성하여 크로스페이드
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.clip = audioSource.clip;
        newSource.time = targetTime;
        newSource.volume = 0;
        newSource.Play();

        float startTime = Time.time;
        while (Time.time < startTime + CROSSFADE_DURATION)
        {
            float t = (Time.time - startTime) / CROSSFADE_DURATION;
            audioSource.volume = Mathf.Lerp(1, 0, t);
            newSource.volume = Mathf.Lerp(0, 1, t);
            yield return null;
        }

        audioSource.Stop();
        DestroyCustom(audioSource);

        // 새로운 소스에 대해 페이드 아웃 시작
        startTime = Time.time;
        while (Time.time < startTime + PREVIEW_DURATION)
        {
            if (Time.time > startTime + PREVIEW_DURATION - FADE_DURATION)
            {
                float t = (Time.time - (startTime + PREVIEW_DURATION - FADE_DURATION)) / FADE_DURATION;
                newSource.volume = Mathf.Lerp(1, 0, t);
            }
            yield return null;
        }

        newSource.Stop();
        DestroyCustom(newSource);
    }

}