using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseObject : MonoBehaviour
{
    [SerializeField]
    protected string objectName;

    [SerializeField]
    protected AudioSource audioSource;

    public void PlaySound(AudioClip clip, bool loop = false)
    {
        if (audioSource == null)
            return;
        if (clip == null)
            return;

        audioSource.loop = loop;

        audioSource.clip = clip;
        audioSource.Play();
    }

    public void StopSound(AudioClip clip = null)
    {
        if (audioSource == null)
            return;

        if (clip != null && audioSource.clip != clip)
            return;

        audioSource.Stop();
        audioSource.loop = false;

        if (clip == null || audioSource.clip == clip)
        {
            audioSource.clip = null;
        }
    }

    public bool IsPlayingSound(AudioClip clip, bool loop)
    {
        if (audioSource == null || clip == null)
            return false;

        return audioSource.isPlaying
            && audioSource.clip == clip
            && audioSource.loop == loop;
    }
}
