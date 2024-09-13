using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterState
{
    None,
    KnockbackStart,
    Knockbacking,
    KnockbackEnd
}

public class Monster : MonoBehaviour
{
    const float KnockbackMaxHeight = 5.0f;
    private float knockBeginHeight = 0.0f;
    Rigidbody rb;

    private MonsterState monsterState = MonsterState.None;
    
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnKnockback(Vector3 force)
    {
        rb.AddForce(force, ForceMode.Impulse);
        if (monsterState == MonsterState.None)
        {
            monsterState = MonsterState.KnockbackStart;
        }

        if (monsterState == MonsterState.KnockbackStart)
        {
            knockBeginHeight = transform.position.y;
            monsterState = MonsterState.Knockbacking;
        }
    }

    private void FixedUpdate()
    {
        if (monsterState == MonsterState.KnockbackStart || monsterState == MonsterState.Knockbacking)
        {
            if (transform.position.y > knockBeginHeight + KnockbackMaxHeight)
            {
                Vector3 newPosition = transform.position;
                newPosition.y = knockBeginHeight + KnockbackMaxHeight;
                transform.position = newPosition;
                
                // 수직 속도를 0으로 설정하여 더 이상 올라가지 않도록 합니다.
                Vector3 velocity = rb.velocity;
                velocity.y = 0;
                rb.velocity = velocity;
            }

            if (transform.position.y <= knockBeginHeight && rb.velocity.y < 0)
            {
                monsterState = MonsterState.None;
            }
            else
            {
                monsterState = MonsterState.Knockbacking;
            }
        }
    }
}