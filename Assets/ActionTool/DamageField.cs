using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class DamageField : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        
    }

    public void UpdateActionEvent(ActionEvent evt, int in_currentFrame)
    {
        AudioSource[] sources =  GetComponentsInChildren<AudioSource>();
        if (!sources.IsUnityNull() && sources.Length > 0)
        {
            foreach (var audioSource in sources)
            {
                audioSource.PlayOneShot(audioSource.clip);
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
