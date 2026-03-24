using UnityEngine;

public class XRInteractionScripts : MonoBehaviour
{
    // Assign these in the Inspector
    public AudioSource audioSource;
    public AudioClip oneShotClip;
    public ParticleSystem particleSystemToPlay;
    public Light lightToToggle;

 
    // 1) PLAY SOUND
    public void PlaySound()
    {
        // Check that we actually have an AudioSource
        if (audioSource != null)
        {
            // If a one-shot clip is assigned, play that once
            if (oneShotClip != null)
            {
                audioSource.PlayOneShot(oneShotClip);
            }
            else
            {
                // Otherwise play whatever the AudioSource is set up with
                audioSource.Play();
            }
        }
        else
        {
            Debug.LogWarning("PlaySound: 'audioSource' is not assigned. Drag an AudioSource into the field in the Inspector.");
        }
    }


    // 2) PLAY PARTICLE SYSTEM
    public void PlayParticles()
    {
        // Check that we actually have a ParticleSystem
        if (particleSystemToPlay != null)
        {
            // If it is already playing, stop and clear so it restarts from the beginning
            if (particleSystemToPlay.isPlaying == true)
            {
                particleSystemToPlay.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Now play it
            particleSystemToPlay.Play();
        }
        else
        {
            Debug.LogWarning("PlayParticles: 'particleSystemToPlay' is not assigned. Drag a ParticleSystem into the field in the Inspector.");
        }
    }


    // 3) TOGGLE LIGHT
    public void ToggleLight()
    {
        // Check that we actually have a Light
        if (lightToToggle != null)
        {
            // If the light is enabled, turn it off; otherwise turn it on
            if (lightToToggle.enabled == true)
            {
                lightToToggle.enabled = false;
            }
            else
            {
                lightToToggle.enabled = true;
            }
        }
        else
        {
            Debug.LogWarning("ToggleLight: 'lightToToggle' is not assigned. Drag a Light into the field in the Inspector.");
        }
    }
}
