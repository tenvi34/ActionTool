using UnityEngine;

public enum DamageFieldEndAction
{
    Destroy,
    Continues,
}

public class DamageFieldData : ScriptableObject
{
    [SerializeField]
    public GameObject damageFieldPrefab;

    [SerializeField] public DamageFieldEndAction EndActionType;
}