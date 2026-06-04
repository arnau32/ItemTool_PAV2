using System;
using UnityEngine;

// Base Class for any character in this game

[RequireComponent(typeof(CharacterStats), typeof(Rigidbody))]
public class CharacterBase : MonoBehaviour
{
    protected StateMachine _stateMachine;
    
    protected virtual void Awake()
    {
        _stateMachine = new StateMachine();
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        _stateMachine.Tick(Time.deltaTime);
    }

    protected virtual void FixedUpdate()
    {
        _stateMachine.FixedTick(Time.fixedDeltaTime);
    }

    protected virtual void LateUpdate() { }
}
