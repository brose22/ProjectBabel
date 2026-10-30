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
    // Tiles per second a captain walks to a new cell.
    const float WalkSpeed = 3f;
    // True while the captain is on their way to Cell.
    public bool IsWalking { get; private set; }
    Vector3 walkTarget;
    // Game time a requested special stops waiting to fire by itself, or below zero for none.
    float autoFireUntil = -1f;

    readonly List<Soldier> soldiers = new();
    // The tile each soldier slot waits on.
    readonly List<Vector2Int> slotCells = new();
    float drawProgress;
    // Fractional free reserve earned from approval, paid out a whole coin at a time.
    float efficiencyBank;
    MatchManager match;
    GridMap grid;
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
    float fightChargeThisWave;
    float fightChargeWaveStart = -1f;
    bool casting;

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
    public void Init(MatchManager owner, GridMap map, CaptainData captain, IReadOnlyList<SoldierClassData> slotClasses, Vector2Int cell)
    {
        match = owner;
        grid = map;
        Captain = captain;
        Cell = cell;
        ApplyArt(captain);
        // The range sprite is a disc two units across, so its scale equals the radius.
        rangeIndicator.transform.localScale = Vector3.one * captain.range;
        rangeIndicator.enabled = false;
        Health = MaxHealth;
        UpdateHealthBar();

        slotCells.AddRange(PickHomes(grid, cell, slotClasses));
        for (int i = 0; i < slotClasses.Count; i++)
            soldiers.Add(SpawnSoldier(slotClasses[i], i));

        All.Add(this);
    }

    Soldier SpawnSoldier(SoldierClassData soldierClass, int slot)
    {
        Soldier soldier = Instantiate(soldierPrefab, transform);
        soldier.Init(this, soldierClass, HomeOffset(slot));
        return soldier;
    }

    Vector3 HomeOffset(int slot)
    {
        Vector2Int offset = slotCells[slot] - Cell;
        return new Vector3(offset.x, offset.y, 0f);
    }

    public SoldierClassData SlotClass(int slot) => soldiers[slot].Class;

    // Soldier costs scale with the captain's level, not the soldier's.
    public int ActCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.actCost, Level);
    public int RespawnCostOf(SoldierClassData soldierClass) => GoldCosts.Scaled(soldierClass.respawnCost, Level);

    // Swapping a slot's class costs the new class's respawn, and the replacement starts fresh
    // on a tile of its own choosing. The old soldier is retired, not killed, so it is no casualty. A disabled tower has
    // no soldiers on the field, so its mix is locked until it is restored.
    public bool SetSlotClass(int slot, SoldierClassData newClass, bool free = false)
    {
        if (IsDisabled || newClass == null || slot < 0 || slot >= soldiers.Count) return false;
        if (soldiers[slot].Class == newClass) return false;
        if (!free && !TrySpendReserve(RespawnCostOf(newClass))) return false;

        Destroy(soldiers[slot].gameObject);
        slotCells[slot] = PickHomeInsteadOf(slot, newClass);
        soldiers[slot] = SpawnSoldier(newClass, slot);
        GameEvents.RaiseSoldierMixChanged(this);
        return true;
    }

    // Each soldier waits on the tile around the tower that suits their class best.
    Vector2Int PickHome(SoldierClassData soldierClass) =>
        SoldierHomeMath.Pick(Cell, soldierClass.homeTile, grid.IsRoad, grid.CanStandOn, IsHomeTaken);

    // The tiles a full set of soldiers would take around a cell, in slot order. Placing, moving and
    // the placement preview all come through here, so the preview shows exactly what will happen.
    // A tower being moved gives up its own tile and its soldiers' tiles.
    public static List<Vector2Int> PickHomes(GridMap grid, Vector2Int cell, IReadOnlyList<SoldierClassData> classes, Tower moving = null)
    {
        var homes = new List<Vector2Int>();
        bool CanStand(Vector2Int home) => grid.CanStandOn(home) || (moving != null && home == moving.Cell);
        bool IsTaken(Vector2Int home) => homes.Contains(home) || IsSoldierHome(home, moving);

        foreach (SoldierClassData soldierClass in classes)
            homes.Add(SoldierHomeMath.Pick(cell, soldierClass.homeTile, grid.IsRoad, CanStand, IsTaken));
        return homes;
    }

    public List<SoldierClassData> SlotClasses()
    {
        var classes = new List<SoldierClassData>();
        foreach (Soldier soldier in soldiers)
            classes.Add(soldier.Class);
        return classes;
    }

    // The slot's own tile counts as free, so its soldier can stay put if that is still the best.
    Vector2Int PickHomeInsteadOf(int slot, SoldierClassData soldierClass)
    {
        slotCells[slot] = Cell;
        return PickHome(soldierClass);
    }

    bool IsHomeTaken(Vector2Int cell) => slotCells.Contains(cell) || IsSoldierHome(cell);

    // A disabled tower's tiles count too, since its soldiers come back when it is restored.
    public static bool IsSoldierHome(Vector2Int cell, Tower except = null)
    {
        foreach (Tower tower in All)
            if (tower != except && tower.slotCells.Contains(cell)) return true;
        return false;
    }

    // A rank-up mid-match adds soldiers straight away (GDD: 3 at B, 4 at S). A disabled tower's
    // new soldiers wait off the field until it is restored.
    public void GrowSoldierSlots(int count, System.Func<int, SoldierClassData> classForSlot)
    {
        while (soldiers.Count < count)
        {
            int slot = soldiers.Count;
            SoldierClassData soldierClass = classForSlot(slot);
            slotCells.Add(PickHome(soldierClass));
            Soldier soldier = SpawnSoldier(soldierClass, slot);
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

        if (IsWalking) Walk();
        ChargeSpecial();
        TickAutoFire();
        UpdateBodyColor();
    }

    void Walk()
    {
        transform.position = Vector3.MoveTowards(transform.position, walkTarget, WalkSpeed * Time.deltaTime);
        if (transform.position == walkTarget) IsWalking = false;
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

    // The slow timer fills only while enemies are on the field, so the special can't be stockpiled
    // between waves. Most charge comes from fighting (AddFightCharge).
    void ChargeSpecial()
    {
        if (!HasAbility || IsDisabled || Enemy.Active.Count == 0) return;
        float perSecond = Captain.specialChargeRate * BondMath.ChargeRateMultiplier(Approval) / Mathf.Max(1f, Captain.ability.chargeSeconds);
        SpecialCharge = Mathf.Min(1f, SpecialCharge + perSecond * ChargeMath.TrickleMultiplier * Time.deltaTime);
    }

    // Charge earned by this captain's soldiers hitting and killing enemies, as a fraction of a full
    // meter. Scaled by charge rate and approval, capped per wave, and ignored while the special
    // itself is being cast so it cannot refill itself.
    public void AddFightCharge(float amount)
    {
        if (amount <= 0f || casting || !HasAbility || IsDisabled || match == null) return;
        if (fightChargeWaveStart != match.WaveStartTime)
        {
            fightChargeWaveStart = match.WaveStartTime;
            fightChargeThisWave = 0f;
        }
        amount *= Captain.specialChargeRate * BondMath.ChargeRateMultiplier(Approval);
        amount = Mathf.Min(amount, ChargeMath.WaveCap - fightChargeThisWave);
        if (amount <= 0f) return;
        fightChargeThisWave += amount;
        SpecialCharge = Mathf.Min(1f, SpecialCharge + amount);
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

    // Fires the special as soon as it can be used (a target in range, say), giving up after the timeout.
    public void FireAbilityWhenAble(float timeoutSeconds) => autoFireUntil = Time.time + timeoutSeconds;

    void TickAutoFire()
    {
        if (autoFireUntil < 0f) return;
        if (Time.time > autoFireUntil || IsDisabled || !IsSpecialReady || TryUseAbility()) autoFireUntil = -1f;
    }

    public void GrantMoveDiscount(float costMultiplier) => MoveCostMultiplier = Mathf.Min(MoveCostMultiplier, costMultiplier);
    public void ClearMoveDiscount() => MoveCostMultiplier = 1f;

    public bool TryUseAbility()
    {
        if (!CanUseAbility) return false;
        casting = true;
        if (IsSupportAbility)
        {
            float healed = AbilityCaster.CastSupport(this, rangeIndicator.sprite);
            int boosted = Captain.ability.HasBuff ? All.FindAll(t => !t.IsDisabled).Count : 0;
            GainSupportXp(healed * SupportXpPerHealthHealed + boosted * SupportXpPerTowerBoosted);
        }
        else AbilityCaster.Cast(this, FindAbilityTarget(), SpriteUtil.Square, rangeIndicator.sprite);
        casting = false;
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

    // The captain claims the new cell at once (Cell, the grid and the soldiers' tiles all switch)
    // and walks there in a straight line; their soldiers, being children, walk along with them.
    // Calling everyone home keeps deployed soldiers consistent with the new formation.
    public void MoveTo(Vector2Int cell, Vector3 position)
    {
        Cell = cell;
        walkTarget = position;
        IsWalking = true;
        List<Vector2Int> homes = PickHomes(grid, cell, SlotClasses(), this);
        slotCells.Clear();
        slotCells.AddRange(homes);
        for (int slot = 0; slot < soldiers.Count; slot++)
        {
            soldiers[slot].SetHome(HomeOffset(slot));
            soldiers[slot].ReturnHome();
        }
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

    // A won match returns part of the unspent reserve to the shared pool; returns the Gold refunded.
    public int ReturnReserve(float fraction)
    {
        int refund = GoldCosts.Refund(Reserve, fraction);
        RunState.Current.AddGold(refund);
        Reserve = 0;
        return refund;
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
