using System.Collections.Generic;
using UnityEngine;

public class AttackQueueManager : MonoBehaviour
{
    public static AttackQueueManager Instance;

    public int maxAttackers = 3;
    private List<EnemyPatrol> queue = new List<EnemyPatrol>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public bool RequestAttackSlot(EnemyPatrol enemy)
    {
        if (queue.Contains(enemy))
            return true;

        if (queue.Count < maxAttackers)
        {
            queue.Add(enemy);
            return true;
        }

        return false;
    }

    public void ReleaseSlot(EnemyPatrol enemy)
    {
        if (queue.Contains(enemy))
            queue.Remove(enemy);
    }

    public int GetQueueIndex(EnemyPatrol enemy)
    {
        return queue.IndexOf(enemy);
    }

}
