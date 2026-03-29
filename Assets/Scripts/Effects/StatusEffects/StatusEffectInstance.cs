using UnityEngine;

public class StatusEffectInstance
{
    public StatusEffectSO Definition { get; }
    public int Stacks { get; private set; }
    public bool IsExpired => Stacks <= 0;

    public StatusEffectInstance(StatusEffectSO definition, int initialStacks)
    {
        Definition = definition;
        Stacks = initialStacks;
    }

    public void AddStacks(int amount)
    {
        Stacks += amount;
    }

    public void RemoveStacks(int amount)
    {
        Stacks = Mathf.Max(0, Stacks - amount);
    }
}
