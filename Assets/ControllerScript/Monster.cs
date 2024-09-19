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

    public void OnKnockback(float power, KnockbackTargetType knockbackTargetType, Transform attacker, KnockbackType knockbackType, KnockbackDirectionType knockbackDirectionType)
    {
        Transform targetTransform = knockbackTargetType == KnockbackTargetType.Attacker ? attacker : transform;
        
        Vector3 direction = Vector3.zero;
        if (knockbackType == KnockbackType.Absolute)
        {
            switch (knockbackDirectionType)
            {
                case KnockbackDirectionType.BACK:
                    direction = Vector3.back;
                    break;
                case KnockbackDirectionType.UP:
                    direction = Vector3.up;
                    break;
            }
        }
        else if (knockbackType == KnockbackType.Relative)
        {
            switch (knockbackDirectionType)
            {
                case KnockbackDirectionType.BACK:
                    direction = targetTransform.forward;
                    break;
                case KnockbackDirectionType.UP:
                    direction = Vector3.up;
                    break;
            }
        }
        
        rb.AddForce(direction * power, ForceMode.Impulse);
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
        // 몬스터의 상태가 넉백 시작 또는 넉백 중이라면 진입
        if (monsterState == MonsterState.KnockbackStart || monsterState == MonsterState.Knockbacking)
        {
            // 몬스터의 설정된 y값이 정해진 높이를 넘어가면
            if (transform.position.y > knockBeginHeight + KnockbackMaxHeight)
            {
                // 몬스터의 위치를 가져와서
                Vector3 newPosition = transform.position;
                // 몬스터의 y값을 올라갈 수 있는 최대 높이로 설정
                newPosition.y = knockBeginHeight + KnockbackMaxHeight;
                transform.position = newPosition;
                
                // 수직 속도를 0으로 설정하여 더 이상 올라가지 않도록 합니다.
                Vector3 velocity = rb.velocity;
                velocity.y = 0;
                rb.velocity = velocity;
            }

            if (rb.velocity.magnitude < 0)
            {
                // 몬스터의 상태를 None으로 변경
                monsterState = MonsterState.None;
            }
            else
            {
                // 움직이는 방향이 Up이면 넉백 상태로 변경
                monsterState = MonsterState.Knockbacking;
            }
        }
    }
}
