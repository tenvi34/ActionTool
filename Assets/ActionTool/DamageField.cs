using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum DamageFieldAffactType
{
    Once,
    HitInterval
}

public class DamageField : MonoBehaviour
{
    public DamageFieldAffactType damageFieldAffactType;
    public float hitInterval;
    
    private Coroutine hitIntervalCoroutine;
    
    void OnTriggerEnter(Collider collision)
    {
        Debug.Log(collision.gameObject.name);
    }
    
    void OnTriggerExit(Collider collision)
    {
        
    }

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
