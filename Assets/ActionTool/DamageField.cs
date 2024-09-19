using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum DamageFieldAffectType
{
    Once,
    HitInterval
}

public class DamageField : MonoBehaviour
{
    public GameObject Owner;
    public DamageFieldAffectType damageFieldAffectType;
    public float hitInterval;
    
    private Coroutine hitIntervalCoroutine;
    
    public void UpdateActionEvent(int in_currentFrame)
    {
        AudioSource[] sources =  GetComponentsInChildren<AudioSource>();
        if (!sources.IsUnityNull() && sources.Length > 0)
        {
            foreach (var audioSource in sources)
            {
                if(!audioSource.isPlaying)
                    audioSource.Play();
            }
        }
        
        
        ParticleSystem[] pss = GetComponentsInChildren<ParticleSystem>();
        foreach (var system in pss)
        {
            system.Simulate( in_currentFrame, true, true);
            system.Pause(true);
        }
    }
}