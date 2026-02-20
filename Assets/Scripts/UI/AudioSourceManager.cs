using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class AudioSourceManager : MonoBehaviour
{
    [Header("SE—p")]
    [SerializeField] private AudioClip[] pistolClips;
    [SerializeField] private AudioClip[] reloadClips;
   // [SerializeField] private AudioClip[] assaultClips;
    [SerializeField] private float pitchRange = 0.1f;
    protected AudioSource source;
   
    private void Awake()
    {
        source = GetComponents<AudioSource>()[0];
    }

    public void PlayPistolFireSE()
    {
        source.pitch = 1.0f + Random.Range(-pitchRange, pitchRange);
        source.PlayOneShot(pistolClips[Random.Range(0, pistolClips.Length)]);
    }

    public void PlayReloadSE()
    {
        source.pitch = 1.0f + Random.Range(-pitchRange, pitchRange);
        source.PlayOneShot(pistolClips[Random.Range(0, pistolClips.Length)]);
    }
}
