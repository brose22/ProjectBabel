using System.Collections.Generic;
using UnityEngine;

public enum DrawRate { Low, Normal, High }

// A tower is a captain plus their soldiers. It pulls Gold from the shared pool into its own
// reserve at the player-set draw rate; soldiers spend from that reserve to act and respawn.
public class Tower : MonoBehaviour
{
    public static readonly List<Tower> All = new();

    [SerializeField] SpriteRenderer body;
    [SerializeField] SpriteRenderer rangeIndicator;
    [SerializeField] HealthBar reserveBar;
    [SerializeField] Soldier soldierPrefab;

    public CaptainData Captain { get; private set; }
    public Vector2Int Cell { get; private set; }
    public DrawRate DrawRate { get; private set; } = DrawRate.Normal;
    public int Reserve { get; private set; }
    public IReadOnlyList<Soldier> Soldiers => soldiers;

    readonly List<Soldier> soldiers = new();
    float drawProgress;

    public float Range => Captain.range;
    public float ActCooldown => 1f / Mathf.Max(0.05f, Captain.speed);
    public float SoldierPower => Captain.power * (1f + 0.1f * (Captain.level - 1));

    public int LivingSoldiers
    {
        get
        {
            int count = 0;
            foreach (Soldier soldier in soldiers)
                if (!soldier.IsDead) count++;
            return count;
        }
    }

    static float DrawMultiplier(DrawRate rate) => rate switch
    {
        DrawRate.Low => 0.5f,
        DrawRate.High => 2f,
        _ => 1f,
    };

    // One soldier per entry in slotClasses; the soldier list index is the slot index.
    public void Init(CaptainData captain, IReadOnlyList<SoldierClassData> slotClasses, Vector2Int cell)
    {
        Captain = captain;
        Cell = cell;
        body.color = captain.color;
        // The range sprite is a disc two units across, so its scale equals the radius.
        rangeIndicator.transform.localScale = Vector3.one * captain.range;
        rangeIndicator.enabled = false;

        for (int i = 0; i < slotClasses.Count; i++)
            soldiers.Add(SpawnSoldier(slotClasses[i], i, slotClasses.Count));

        All.Add(this);
    }

    Soldier SpawnSoldier(SoldierClassData soldierClass, int slot, int slotCount)
    {
        Soldier soldier = Instantiate(soldierPrefab, transform);
        soldier.Init(this, soldierClass, HomeOffset(slot, slotCount));
        return soldier;
    }

    public SoldierClassData SlotClass(int slot) => soldiers[slot].Class;

    // Soldier costs scale with the captain's level, not the soldier's.
    public int ActCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.actCost, Captain.level);
    public int RespawnCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.respawnCost, Captain.level);

    // Swapping a slot's class costs the new class's respawn, and the replacement starts fresh
    // at home. The old soldier is retired, not killed, so it is no casualty.
    public bool SetSlotClass(int slot, SoldierClassData newClass)
    {
        if (newClass == null || slot < 0 || slot >= soldiers.Count) return false;
        if (soldiers[slot].Class == newClass) return false;
        if (!TrySpendReserve(RespawnCostOf(newClass))) return false;

        Destroy(soldiers[slot].gameObject);
        soldiers[slot] = SpawnSoldier(newClass, slot, soldiers.Count);
        GameEvents.RaiseSoldierMixChanged(this);
        return true;
    }

    static Vector3 HomeOffset(int index, int count)
    {
        float angle = Mathf.PI * 2f * index / count + Mathf.PI * 0.25f;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.3f;
    }

    void Update()
    {
        int capacity = Captain.resourceCapacity;
        if (Reserve >= capacity)
        {
            drawProgress = 0f;
        }
        else
        {
            // Draw whole coins from the pool as the regen accumulates.
            drawProgress += Captain.resourceRegen * DrawMultiplier(DrawRate) * Time.deltaTime;
            while (drawProgress >= 1f && Reserve < capacity && RunState.Current.TrySpendGold(1))
            {
                Reserve++;
                drawProgress -= 1f;
            }
            drawProgress = Mathf.Min(drawProgress, 1f);
        }
        reserveBar.SetFraction((float)Reserve / capacity);
    }

    public bool TrySpendReserve(int cost)
    {
        if (Reserve < cost) return false;
        Reserve -= cost;
        return true;
    }

    public void SetDrawRate(DrawRate rate)
    {
        if (rate == DrawRate) return;
        DrawRate = rate;
        GameEvents.RaiseDrawRateChanged(this);
    }

    public void SetSelected(bool selected) => rangeIndicator.enabled = selected;

    public bool IsInRange(Vector3 position, float slack = 0f) =>
        Vector2.Distance(transform.position, position) <= Range + slack;

    // Soldiers always pick the enemy closest to themselves, among those inside the tower's range.
    public Enemy FindClosestEnemy(Vector3 from)
    {
        Enemy closest = null;
        float closestDistance = float.MaxValue;
        foreach (Enemy enemy in Enemy.Active)
        {
            if (!IsInRange(enemy.transform.position)) continue;
            float distance = Vector2.Distance(from, enemy.transform.position);
            if (distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }
        return closest;
    }

    // Unspent reserve goes back to the shared pool.
    public void Sell()
    {
        RunState.Current.AddGold(Reserve);
        Reserve = 0;
        All.Remove(this);
        GameEvents.RaiseTowerSold(this);
        Destroy(gameObject);
    }

    void OnDestroy() => All.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();
}
