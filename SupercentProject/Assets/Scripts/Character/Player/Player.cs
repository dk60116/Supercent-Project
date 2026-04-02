using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PlayerStatus
{
    public float pickingSpeed;
}

public class Player : Character
{
    [SerializeField]
    private PlayerStatus playerStat;

    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public PlayerStatus PlayerStatus => playerStat;
    public PlayerController Controller => controller;
}
