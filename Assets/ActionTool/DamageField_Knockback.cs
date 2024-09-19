using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum KnockbackType
{
    Absolute,
    Relative
}

public enum KnockbackDirectionType
{
    BACK,
    UP,
    PULL
}

public enum KnockbackTargetType
{
    Attacker,
    Self
}

public class DamageField_KnockBack : DamageField
{
    public float power;
    public KnockbackType knockbackType;
    public KnockbackDirectionType knockbackDirectionType;
    public KnockbackTargetType knockbackTargetType;
    
    void OnTriggerEnter(Collider collision)
    {
        if (collision.TryGetComponent(out Monster monster))
        {
            monster.OnKnockback(power, knockbackTargetType, Owner.transform, knockbackType, knockbackDirectionType);

            if (damageFieldAffectType == DamageFieldAffectType.Once)
            {
                Destroy(gameObject);
            }
        }
    }
}