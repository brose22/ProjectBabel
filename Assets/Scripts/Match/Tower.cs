using System.Collections.Generic;
using UnityEngine;

public enum DrawRate { Low, Normal, High }

// A tower is a captain plus their soldiers. It pulls Gold from the shared pool into its own
// reserve at the player-set draw rate; soldiers spend from that reserve to act and respawn.
public class Tower : MonoBehaviour
{
    public static readonly List<Tower> All = new();

    static readonly Vector3[] CrossDirections = { Vector3.up, Vector3.right, Vector3.down, Vector3.left };
    // Far enough that a soldier stands clear of the tower body (0.8 tiles wide).
    const float SoldierHomeDistance = 0.65f;

    [SerializeField] SpriteRenderer body;
    [SerializeField] SpriteRenderer rangeIndicator;
    [SerializeField] HealthBar reserveBar;
    [SerializeField] Soldier soldierPrefab;
    // Optional until the prefab builder adds it.
    [SerializeField] HealthBar healthBar;

    public CaptainData Captain { get; private set; }
    public Vector2Int Cell { get; private set; }
    public DrawRate DrawRate { get; private set; } = DrawRate.Normal;
    public int Reserve { get; private set; }
    public IReadOnlyList<Soldier> Soldiers => soldiers;
    public float Health { get; private set; }
    public float MaxHealth => Mathf.Max(1, Captain.health);
    public bool IsDisabled { get; private set; }
    // The wave number whose end makes a disabled tower restorable. Set by MatchManager.
    public int RestorableAfterWave { get; private set; }
    // Special-ability meter, 0 to 1. The ability can fire once it reaches 1.
    public float SpecialCharge { get; private set; }

    readonly List<Soldier> soldiers = new();
    // Home offset for each soldier slot, kept so a swapped-in soldier stands where the old one did.
    readonly List<Vector3> slotHomes = new();
    float drawProgress;
    MatchManager match;
    // Timed boosts from support abilities, indexed by SupportEffect: amount and game time they end.
    readonly float[] timedBoostAmount = new float[System.Enum.GetValues(typeof(SupportEffect)).Length];
    readonly float[] timedBoostUntil = new float[System.Enum.GetValues(typeof(SupportEffect)).Length];

    public float Range => Captain.range;
    public float ActCooldown => 1f / Mathf.Max(0.05f, Captain.speed * (1f + Boost(SupportEffect.ActSpeed)));
    public float SoldierPower => Captain.power * (1f + 0.1f * (Captain.level - 1)) * (1f + Boost(SupportEffect.Damage));
    // Multiplies damage this tower's soldiers take.
    public float SoldierDamageTakenMultiplier => AuraMath.DamageTakenMultiplier(Boost(SupportEffect.Defense));

    bool IsWaveRunning => match != null && match.State == MatchState.WaveActive;
    public bool IsAuraActive => Captain.aura != SupportEffect.None && IsWaveRunning && !IsDisabled;

    public bool HasAbility => Captain.ability != null;
    public bool IsSpecialReady => HasAbility && SpecialCharge >= 1f;
    public float AbilityRange => Range * (HasAbility ? Captain.ability.rangeMultiplier : 1f);
    public bool IsSupportAbility => HasAbility && Captain.ability.shape == AbilityData.Shape.Support;
    public bool CanUseAbility => IsSpecialReady && !IsDisabled && (IsSupportAbility || FindAbilityTarget() != null);

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
    public void Init(MatchManager owner, CaptainData captain, IReadOnlyList<SoldierClassData> slotClasses, Vector2Int cell)
    {
        match = owner;
        Captain = captain;
        Cell = cell;
        body.color = captain.color;
        // The range sprite is a disc two units across, so its scale equals the radius.
        rangeIndicator.transform.localScale = Vector3.one * captain.range;
        rangeIndicator.enabled = false;
        Health = MaxHealth;
        UpdateHealthBar();

        AssignSlotHomes(slotClasses.Count);
        for (int i = 0; i < slotClasses.Count; i++)
            soldiers.Add(SpawnSoldier(slotClasses[i], i));

        All.Add(this);
    }

    Soldier SpawnSoldier(SoldierClassData soldierClass, int slot)
    {
        Soldier soldier = Instantiate(soldierPrefab, transform);
        soldier.Init(this, soldierClass, slotHomes[slot]);
        return soldier;
    }

    public SoldierClassData SlotClass(int slot) => soldiers[slot].Class;

    // Soldier costs scale with the captain's level, not the soldier's.
    public int ActCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.actCost, Captain.level);
    public int RespawnCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.respawnCost, Captain.level);

    // Swapping a slot's class costs the new class's respawn, and the replacement starts fresh
    // at home. The old soldier is retired, not killed, so it is no casualty. A disabled tower has
    // no soldiers on the field, so its mix is locked until it is restored.
    public bool SetSlotClass(int slot, SoldierClassData newClass)
    {
        if (IsDisabled || newClass == null || slot < 0 || slot >= soldiers.Count) return false;
        if (soldiers[slot].Class == newClass) return false;
        if (!TrySpendReserve(RespawnCostOf(newClass))) return false;

        Destroy(soldiers[slot].gameObject);
        soldiers[slot] = SpawnSoldier(newClass, slot);
        GameEvents.RaiseSoldierMixChanged(this);
        return true;
    }

    // Soldiers wait in a cross around the tower; each slot takes a random free arm.
    // Past four soldiers the arms are reshuffled and reused.
    void AssignSlotHomes(int count)
    {
        slotHomes.Clear();
        var free = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            if (free.Count == 0) free.AddRange(CrossDirections);
            int pick = Random.Range(0, free.Count);
            slotHomes.Add(free[pick] * SoldierHomeDistance);
            free.RemoveAt(pick);
        }
    }

    void Update()
    {
        int capacity = Captain.resourceCapacity;
        if (Reserve >= capacity || IsDisabled)
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

        ChargeSpecial();
        TickHealAura();
        UpdateBodyColor();
    }

    // How strong a stat boost on this tower's soldiers is right now (0.5 = +50%): its own aura,
    // which starts full when the wave begins and fades over the wave, plus any timed boost.
    public float Boost(SupportEffect effect)
    {
        if (effect is SupportEffect.None or SupportEffect.Heal) return 0f;
        float boost = Time.time < timedBoostUntil[(int)effect] ? timedBoostAmount[(int)effect] : 0f;
        if (Captain.aura == effect && IsAuraActive)
            boost += AuraMath.FadedStrength(Captain.auraStrength, Time.time - match.WaveStartTime, Captain.auraFadeSeconds);
        return boost;
    }

    // Seconds left on a timed boost, or 0.
    public float TimedBoostSecondsLeft(SupportEffect effect) =>
        effect is SupportEffect.None or SupportEffect.Heal ? 0f : Mathf.Max(0f, timedBoostUntil[(int)effect] - Time.time);

    // Re-applying a boost keeps the stronger amount and the later end time rather than stacking.
    public void AddTimedBoost(SupportEffect effect, float amount, float seconds)
    {
        if (effect is SupportEffect.None or SupportEffect.Heal || amount <= 0f || seconds <= 0f) return;
        int i = (int)effect;
        bool active = Time.time < timedBoostUntil[i];
        timedBoostAmount[i] = active ? Mathf.Max(timedBoostAmount[i], amount) : amount;
        timedBoostUntil[i] = Mathf.Max(timedBoostUntil[i], Time.time + seconds);
    }

    // Restores a fraction of max health to the tower and each living soldier. Returns the total
    // health restored. Disabled towers can only be brought back by Restore.
    public float HealAll(float fraction)
    {
        if (IsDisabled || fraction <= 0f) return 0f;
        float healed = Mathf.Min(MaxHealth * fraction, MaxHealth - Health);
        Health += healed;
        UpdateHealthBar();
        foreach (Soldier soldier in soldiers)
            healed += soldier.Heal(soldier.MaxHealth * fraction);
        return healed;
    }

    // Healer auras restore living soldiers a little every frame while the wave runs.
    void TickHealAura()
    {
        if (Captain.aura != SupportEffect.Heal || !IsAuraActive) return;
        float amount = Captain.auraStrength * Time.deltaTime;
        foreach (Soldier soldier in soldiers)
            soldier.Heal(amount);
    }

    // Fills only while enemies are on the field, so the special can't be stockpiled between waves.
    void ChargeSpecial()
    {
        if (!HasAbility || IsDisabled || Enemy.Active.Count == 0) return;
        float perSecond = Captain.specialChargeRate / Mathf.Max(1f, Captain.ability.chargeSeconds);
        SpecialCharge = Mathf.Min(1f, SpecialCharge + perSecond * Time.deltaTime);
    }

    // Disabled towers are dimmed; a ready special makes the tower pulse toward white.
    void UpdateBodyColor()
    {
        if (IsDisabled)
            body.color = Color.Lerp(Captain.color, Color.black, 0.65f);
        else if (IsSpecialReady)
            body.color = Color.Lerp(Captain.color, Color.white, 0.2f + 0.2f * Mathf.Sin(Time.time * 6f));
        else
            body.color = Captain.color;
    }

    // The ability aims at the enemy closest to the tower within the ability's range.
    public Enemy FindAbilityTarget()
    {
        if (!HasAbility) return null;
        Enemy closest = null;
        float closestDistance = AbilityRange;
        foreach (Enemy enemy in Enemy.Active)
        {
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance <= closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }
        return closest;
    }

    public bool TryUseAbility()
    {
        if (!CanUseAbility) return false;
        if (IsSupportAbility) AbilityCaster.CastSupport(this, rangeIndicator.sprite);
        else AbilityCaster.Cast(this, FindAbilityTarget(), body.sprite, rangeIndicator.sprite);
        SpecialCharge = 0f;
        GameEvents.RaiseAbilityUsed(this);
        return true;
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

    // Soldiers are children of the tower, so deployed ones would be dragged along mid-act;
    // calling everyone home keeps them consistent with the new position.
    public void MoveTo(Vector2Int cell, Vector3 position)
    {
        Cell = cell;
        transform.position = position;
        foreach (Soldier soldier in soldiers)
            soldier.ReturnHome();
    }

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

    // The closest tower that is not disabled within radius of position, or null.
    public static Tower FindClosestActive(Vector3 position, float radius)
    {
        Tower closest = null;
        float closestDistance = radius;
        foreach (Tower tower in All)
        {
            if (tower.IsDisabled) continue;
            float distance = Vector2.Distance(position, tower.transform.position);
            if (distance <= closestDistance)
            {
                closest = tower;
                closestDistance = distance;
            }
        }
        return closest;
    }

    public void TakeDamage(float amount)
    {
        if (IsDisabled) return;
        Health = Mathf.Max(0f, Health - amount);
        UpdateHealthBar();
        if (Health <= 0f) Disable();
    }

    // The tower keeps its reserve but stops drawing Gold, and its soldiers leave the field.
    void Disable()
    {
        IsDisabled = true;
        drawProgress = 0f;
        foreach (Soldier soldier in soldiers)
            soldier.Despawn();
        GameEvents.RaiseTowerDisabled(this);
    }

    public void SetRestorableAfterWave(int waveNumber) => RestorableAfterWave = waveNumber;

    public void Restore()
    {
        if (!IsDisabled) return;
        IsDisabled = false;
        Health = MaxHealth;
        UpdateHealthBar();
        foreach (Soldier soldier in soldiers)
            soldier.Respawn();
        GameEvents.RaiseTowerRestored(this);
    }

    void UpdateHealthBar()
    {
        if (healthBar != null) healthBar.SetFraction(Health / MaxHealth);
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
