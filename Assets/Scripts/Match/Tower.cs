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
    const float SoldierHomeDistance = 1.0f;

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
    // Multiplies the next relocation's Gold cost; a granted request can make it cheaper or free.
    public float MoveCostMultiplier { get; private set; } = 1f;
    // Game time a requested special fires by itself, or below zero for none.
    float autoFireAt = -1f;

    readonly List<Soldier> soldiers = new();
    // Home offset for each soldier slot, kept so a swapped-in soldier stands where the old one did.
    readonly List<Vector3> slotHomes = new();
    float drawProgress;
    // Fractional free reserve earned from approval, paid out a whole coin at a time.
    float efficiencyBank;
    MatchManager match;
    // The body colour before disabled dimming or the ready pulse: white for drawn art, so it shows
    // as painted, or the captain colour for squares and tinted generic art.
    Color baseColor = Color.white;
    // Boosts from Buffer specials, indexed by SupportEffect. They apply to every tower and its
    // soldiers until the wave ends, so they live here rather than on each tower.
    static readonly float[] waveBoosts = new float[System.Enum.GetValues(typeof(SupportEffect)).Length];

    public float Range => Captain.range;
    public float ActCooldown => 1f / Mathf.Max(0.05f, Captain.speed * (1f + Boost(SupportEffect.ActSpeed)));
    public float SoldierPower => Captain.power * LevelMath.StatFactor(Level) * (1f + Boost(SupportEffect.Damage));
    // Multiplies damage the tower and its soldiers take.
    public float DamageTakenMultiplier => BoostMath.DamageTakenMultiplier(Boost(SupportEffect.Defense));

    bool IsWaveRunning => match != null && match.State == MatchState.WaveActive;

    // XP lives in RunState so a captain keeps it if sold and placed again within the run.
    // The asset's level is a floor, for captains authored above level 1.
    public int Xp => RunState.Current != null ? RunState.Current.XpOf(Captain) : 0;
    public int Level => Mathf.Max(Captain.level, LevelMath.LevelForXp(Xp));

    // Approval and bond rank persist between runs; see ApprovalStore.
    public float Approval => ApprovalStore.ApprovalOf(Captain);
    public int BondRank => ApprovalStore.RankOf(Captain);
    public int XpForNextLevel => LevelMath.XpForLevel(Level + 1);
    public bool IsMaxLevel => Level >= LevelMath.MaxLevel;

    // XP per point of health a Healer restores, and per working tower a Buffer's special boosts.
    const float SupportXpPerHealthHealed = 0.5f;
    const float SupportXpPerTowerBoosted = 6f;
    float supportXpBank;
    int supportXpThisWave;
    float supportXpWaveStart = -1f;

    public bool HasAbility => Captain.ability != null;
    public bool IsSpecialReady => HasAbility && SpecialCharge >= 1f;
    public float AbilityRange => Range * (HasAbility ? Captain.ability.rangeMultiplier : 1f);
    public bool IsSupportAbility => HasAbility && Captain.ability.shape == AbilityData.Shape.Support;
    // A boost lasts only until the wave ends, so a Buffer's special waits for a wave to be running.
    public bool CanUseAbility => IsSpecialReady && !IsDisabled &&
        (IsSupportAbility ? IsWaveRunning || !Captain.ability.HasBuff : FindAbilityTarget() != null);

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
        ApplyArt(captain);
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
    public int ActCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.actCost, Level);
    public int RespawnCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.respawnCost, Level);

    // Swapping a slot's class costs the new class's respawn, and the replacement starts fresh
    // at home. The old soldier is retired, not killed, so it is no casualty. A disabled tower has
    // no soldiers on the field, so its mix is locked until it is restored.
    public bool SetSlotClass(int slot, SoldierClassData newClass, bool free = false)
    {
        if (IsDisabled || newClass == null || slot < 0 || slot >= soldiers.Count) return false;
        if (soldiers[slot].Class == newClass) return false;
        if (!free && !TrySpendReserve(RespawnCostOf(newClass))) return false;

        Destroy(soldiers[slot].gameObject);
        soldiers[slot] = SpawnSoldier(newClass, slot);
        GameEvents.RaiseSoldierMixChanged(this);
        return true;
    }

    // Soldiers wait in a cross around the tower; each slot takes a random free arm.
    // Past four soldiers the arms are reused.
    void AssignSlotHomes(int count)
    {
        slotHomes.Clear();
        for (int i = 0; i < count; i++)
            slotHomes.Add(PickFreeHome());
    }

    Vector3 PickFreeHome()
    {
        var free = new List<Vector3>();
        foreach (Vector3 direction in CrossDirections)
            if (!slotHomes.Contains(direction * SoldierHomeDistance)) free.Add(direction);
        if (free.Count == 0) free.AddRange(CrossDirections);
        return free[Random.Range(0, free.Count)] * SoldierHomeDistance;
    }

    // A rank-up mid-match adds soldiers straight away (GDD: 3 at B, 4 at S), on free arms of the
    // cross. A disabled tower's new soldiers wait off the field until it is restored.
    public void GrowSoldierSlots(int count, System.Func<int, SoldierClassData> classForSlot)
    {
        while (soldiers.Count < count)
        {
            int slot = soldiers.Count;
            slotHomes.Add(PickFreeHome());
            Soldier soldier = SpawnSoldier(classForSlot(slot), slot);
            soldiers.Add(soldier);
            if (IsDisabled) soldier.Despawn();
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
                // Approval makes each coin go further: the bonus banks up into free reserve coins.
                efficiencyBank += BondMath.ReserveEfficiencyBonus(Approval);
                if (efficiencyBank >= 1f && Reserve < capacity)
                {
                    Reserve++;
                    efficiencyBank -= 1f;
                }
            }
            drawProgress = Mathf.Min(drawProgress, 1f);
        }
        reserveBar.SetFraction((float)Reserve / capacity);

        ChargeSpecial();
        TickAutoFire();
        UpdateBodyColor();
    }

    // How strong a stat boost on the tower and its soldiers is right now (0.5 = +50%).
    public float Boost(SupportEffect effect) => WaveBoost(effect);

    // The boost every tower has from Buffer specials cast this wave; 0 for Heal and None.
    public static float WaveBoost(SupportEffect effect) =>
        effect is SupportEffect.None or SupportEffect.Heal ? 0f : waveBoosts[(int)effect];

    // Casting again in the same wave adds to the boost.
    public static void AddWaveBoost(SupportEffect effect, float amount)
    {
        if (effect is SupportEffect.None or SupportEffect.Heal || amount <= 0f) return;
        waveBoosts[(int)effect] += amount;
    }

    // MatchManager calls this when a wave starts or ends and when the match finishes.
    public static void ClearWaveBoosts() => System.Array.Clear(waveBoosts, 0, waveBoosts.Length);

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

    public void GainXp(int amount)
    {
        if (amount <= 0 || RunState.Current == null) return;
        int before = Level;
        RunState.Current.AddXp(Captain, amount);
        if (Level > before) GameEvents.RaiseCaptainLeveledUp(this);
    }

    // XP from healing and buffing, banked in fractions and capped per wave (GDD).
    public void GainSupportXp(float amount)
    {
        if (amount <= 0f || match == null) return;
        if (supportXpWaveStart != match.WaveStartTime)
        {
            supportXpWaveStart = match.WaveStartTime;
            supportXpThisWave = 0;
        }
        supportXpBank += amount;
        int whole = Mathf.FloorToInt(supportXpBank);
        if (whole <= 0) return;
        supportXpBank -= whole;
        int allowed = Mathf.Min(whole, match.Data.supportXpCapPerWave - supportXpThisWave);
        if (allowed <= 0) return;
        supportXpThisWave += allowed;
        GainXp(allowed);
    }

    // Fills only while enemies are on the field, so the special can't be stockpiled between waves.
    void ChargeSpecial()
    {
        if (!HasAbility || IsDisabled || Enemy.Active.Count == 0) return;
        float perSecond = Captain.specialChargeRate * BondMath.ChargeRateMultiplier(Approval) / Mathf.Max(1f, Captain.ability.chargeSeconds);
        SpecialCharge = Mathf.Min(1f, SpecialCharge + perSecond * Time.deltaTime);
    }

    // Drawn captain art replaces the placeholder square at full tile size.
    void ApplyArt(CaptainData captain)
    {
        if (captain.sprite == null)
        {
            baseColor = captain.color;
            return;
        }
        body.sprite = captain.sprite;
        body.transform.localScale = Vector3.one;
        baseColor = captain.tintSprite ? captain.color : Color.white;
    }

    // Disabled towers are dimmed; a ready special makes the tower pulse toward white.
    void UpdateBodyColor()
    {
        if (IsDisabled)
            body.color = Color.Lerp(baseColor, Color.black, 0.65f);
        else if (IsSpecialReady)
            body.color = Color.Lerp(baseColor, Color.white, 0.2f + 0.2f * Mathf.Sin(Time.time * 6f));
        else
            body.color = baseColor;
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

    // Fires the special by itself after a delay, if it can still be used then.
    public void FireAbilitySoon(float seconds) => autoFireAt = Time.time + seconds;

    void TickAutoFire()
    {
        if (autoFireAt < 0f || Time.time < autoFireAt) return;
        autoFireAt = -1f;
        TryUseAbility();
    }

    public void GrantMoveDiscount(float costMultiplier) => MoveCostMultiplier = Mathf.Min(MoveCostMultiplier, costMultiplier);
    public void ClearMoveDiscount() => MoveCostMultiplier = 1f;

    public bool TryUseAbility()
    {
        if (!CanUseAbility) return false;
        if (IsSupportAbility)
        {
            float healed = AbilityCaster.CastSupport(this, rangeIndicator.sprite);
            int boosted = Captain.ability.HasBuff ? All.FindAll(t => !t.IsDisabled).Count : 0;
            GainSupportXp(healed * SupportXpPerHealthHealed + boosted * SupportXpPerTowerBoosted);
        }
        else AbilityCaster.Cast(this, FindAbilityTarget(), SpriteUtil.Square, rangeIndicator.sprite);
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
        Health = Mathf.Max(0f, Health - amount * DamageTakenMultiplier);
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
    static void ResetStatics()
    {
        All.Clear();
        ClearWaveBoosts();
    }
}
