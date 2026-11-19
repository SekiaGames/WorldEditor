using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private float _waveTimer;

    private static readonly Dictionary<int, string> RaceMelee = new()
    {
        [1] = "hfoo",
        [2] = "ogru",
        [3] = "ugho",
        [4] = "esen",
    };

    private void TickAiWaves(float delta)
    {
        _waveTimer += delta;
        float interval = AiDifficulty switch
        {
            0 => 130f,
            2 => 62f,
            _ => 90f,
        };
        interval *= TimeScaleHint < 1f ? TimeScaleHint : 1f;
        if (_waveTimer < interval) return;
        _waveTimer = 0f;
        SpawnWave();
    }

    private void SpawnWave()
    {
        if (SimDriven)
        {
            SpawnSimWave();
            return;
        }
        foreach (MapPlayerInfo player in _map.Players)
        {
            if (player.Id == ViewPlayer) continue;

            Unit hall = FindHall(player.Id);
            if (hall == null) continue;

            Unit waveHero = FindHero(player.Id);
            if (waveHero != null && waveHero.RetreatOrdered) continue;

            int race = Mathf.Clamp(player.Race, 1, 4);
            List<string> wave = WaveComposition(player.Id, race);
            int repeat = AiDifficulty >= 2 ? 2 : 1;
            float angle = Mathf.Pi;
            if (_waveLogBudget > 0)
            {
                _waveLogBudget--;
                var names = new List<string>();
                foreach (string id in wave) names.Add(UnitFactory.Get(id)?.Name ?? id);
                GD.Print($"[WAVE] P{player.Id} 出兵 {wave.Count * repeat} 个：{string.Join("、", names)}"
                    + $"（{AiCounters.Describe(AiCounters.Analyze(_allUnits, player.Id, AreAllied))}）");
            }
            _log?.Add($"[AI] {PlayerName(player.Id)} 出兵 {wave.Count * repeat} 个");

            int slot = 0;
            for (int copy = 0; copy < repeat; copy++)
            {
                foreach (string unitId in wave)
                {
                    float ring = 220f + (slot / 4) * 90f;
                    float spread = Mathf.Cos(angle) * ring;
                    float lateral = Mathf.Sin((float)(slot * 0.9)) * 60f;
                    Unit attacker = Spawn(unitId, player.Id,
                        hall.Position.X + spread + lateral,
                        hall.Position.Z + Mathf.Sin(angle) * ring,
                        Mathf.Pi + angle, 0f, 1f);
                    slot++;
                    if (attacker == null) continue;

                    _world.Units.Add(attacker);
                    attacker.Target = NearestTargetOf(ViewPlayer, hall.Position);
                    attacker.RetreatOrdered = false;
                }
            }

            // 有活着的英雄就一起上（英雄自己会施法/治疗）
            if (waveHero != null)
            {
                waveHero.Target = NearestTargetOf(ViewPlayer, hall.Position);
                waveHero.MoveTarget = null;
                waveHero.Path = null;
            }
        }
    }

    /// <summary>
    /// 波次编成：优先用这家已经造出来的出兵建筑能产的兵种（去重、最多 3 种），
    /// 每种出 1 个，凑不满就退回本族步兵。用的是真实生产表，不是硬编码的种族表。
    /// </summary>
    private List<string> WaveComposition(int player, int race)
    {
        var options = new List<string>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            foreach (string id in unit.Type.Trains)
            {
                UnitType type = UnitFactory.Get(id);
                if (type == null || type.IsHero || type.IsWorkerType || type.FoodUsed <= 0) continue;
                if (!options.Contains(id)) options.Add(id);
            }
        }

        if (options.Count == 0) options.Add(RaceMelee[race]);

        // 波次编成也按对手阵容挑：同样的兵造出来，打对面护甲收益高的排前面
        EnemyComposition enemy = AiCounters.Analyze(_allUnits, player, AreAllied);
        options.Sort((left, right) =>
            AiCounters.Score(UnitFactory.Get(right), enemy).CompareTo(AiCounters.Score(UnitFactory.Get(left), enemy)));

        int breadth = AiDifficulty >= 2 ? 4 : AiDifficulty == 0 ? 2 : 3;
        if (options.Count > breadth) options.RemoveRange(breadth, options.Count - breadth);
        return options;
    }

    /// <summary>
    /// 撤退：血量低于 30% 且离基地较远时，放下目标往主基地走；
    /// 回到 60% 以上或已经到家就解除（不做治疗逻辑，靠自然回血）。
    /// </summary>
    private int _waveLogBudget = 8;
    private int _retreatLogBudget = 6;

    private void TickAiRetreat(PlayerSlot slot)
    {
        Unit hall = FindHall(slot.Id);
        if (hall == null) return;

        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker || unit.IsHero) continue;
            if (unit.InjuryFlee != null) continue;
            if (IsHumanAttackGroupMember(slot.Id, unit)) continue;

            float ratio = unit.MaxHp > 0f ? unit.Hp / unit.MaxHp : 1f;
            float distance = unit.DistanceTo(hall);

            if (unit.RetreatOrdered)
            {
                if (ratio > 0.6f || distance < 420f)
                {
                    unit.RetreatOrdered = false;
                    if (distance < 420f) unit.MoveTarget = null;
                }
                continue;
            }

            if (ratio > 0.3f + (AiDifficulty >= 2 ? 0f : 0.1f) || distance < 700f) continue;

            unit.RetreatOrdered = true;
            unit.Target = null;
            unit.GoldMine = null;
            unit.Tree = null;
            unit.MoveTarget = hall.Position;
            unit.Path = null;

            if (_retreatLogBudget > 0)
            {
                _retreatLogBudget--;
                GD.Print($"[AI] P{slot.Id} {unit.Type.Name} 残血 {ratio * 100f:0}% 撤退回基地");
            }
        }
    }

    /// <summary>
    /// 集火：每 2 秒给本家「还没目标、没在撤退」的部队指定同一个敌人——
    /// 目标按威胁分挑（英雄 > 远程/法师 > 攻城 > 普通，同分看残血），
    /// 距离还远时先拐到敌人稀少的侧翼列阵，进入 900 才正式咬上去。
    /// </summary>
    private void TickAiFocus(PlayerSlot slot)
    {
        Unit hall = FindHall(slot.Id);
        if (hall == null) return;

        Unit focus = null;
        float bestScore = 0f;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            if (unit.Player == slot.Id || AreAllied(unit.Player, slot.Id)) continue;
            if (unit.Player == 15 || unit.Player == 12) continue;
            if (unit.DistanceTo(hall) > 1800f) continue;

            float score = AiTactics.ThreatScore(unit);
            if (score <= bestScore) continue;
            bestScore = score;
            focus = unit;
        }
        if (focus == null) return;

        var attackers = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker || unit.RetreatOrdered || unit.Kiting || unit.HeroEscort) continue;
            if (unit.HireShop != null) continue;
            if (unit.InjuryFlee != null) continue;
            if (IsHumanAttackGroupMember(slot.Id, unit)) continue;
            // 召唤物当前排：不受基地 1800 限制，直接压上去当肉盾
            if (unit.DistanceTo(hall) > 1800f && !unit.IsSummon) continue;
            attackers.Add(unit);
        }
        if (attackers.Count == 0) return;

        Vector3 flank = AiTactics.FlankPoint(focus, hall.Position, _allUnits, AreAllied, slot.Id,
            passable: point => _world != null && _world.CanStandAt(point, 32f)
                && (_pathGrid == null || _pathGrid.FindPath(hall.Position, point, 32f, PathLayer.Ground) != null));
        Dictionary<Unit, Vector3> approach = GroupFormation.Layout(attackers, flank, flank - hall.Position);

        int assigned = 0;
        int marching = 0;
        foreach (Unit unit in attackers)
        {
            float distance = unit.DistanceTo(focus);
            // 太远就直接压上去：绕侧翼只用在最后一段，避免跨地图绕路/卡路
            if (distance > 1500f || unit.FlankTimer > 10f)
            {
                unit.Target = focus;
                unit.Kiting = false;
                unit.MoveTarget = null;
                unit.Path = null;
                unit.FlankTimer = 0f;
                assigned++;
                continue;
            }

            if (distance > 900f)
            {
                Vector3 spot = approach.TryGetValue(unit, out Vector3 assignedSpot) ? assignedSpot : flank;
                unit.FlankTimer += 2f;
                // 已经在往同一个侧翼点走就别每 2 秒重下命令（否则一直在重新寻路）
                if (unit.MoveTarget.HasValue && unit.Target == null)
                {
                    float dx = unit.MoveTarget.Value.X - spot.X;
                    float dz = unit.MoveTarget.Value.Z - spot.Z;
                    if (dx * dx + dz * dz < 150f * 150f)
                    {
                        marching++;
                        continue;
                    }
                }

                unit.Target = null;
                unit.Kiting = false;
                unit.MoveTarget = spot;
                unit.Path = null;
                marching++;
                continue;
            }

            unit.Target = focus;
            unit.Kiting = false;
            unit.MoveTarget = null;
            unit.Path = null;
            unit.FlankTimer = 0f;
            assigned++;
        }

        if ((assigned > 0 || marching > 0) && _focusLogBudget > 0)
        {
            _focusLogBudget--;
            GD.Print($"[AI] P{slot.Id} 集火 {focus.Type.Name}（{assigned} 个咬住 / {marching} 个绕侧翼）"
                + $" 威胁分 {bestScore:0.0} 侧翼 ({flank.X:0},{flank.Z:0})");
        }
    }

    private int _heroGuardLogBudget = 6;

    private void TickAiHeroGuard(PlayerSlot slot, Unit hall)
    {
        Unit hero = FindHero(slot.Id);
        if (hero == null)
        {
            ClearHeroEscorts(slot.Id);
            return;
        }

        if (IsHumanAttackGroupMember(slot.Id, hero))
        {
            ClearHeroEscorts(slot.Id);
            return;
        }

        float ratio = hero.MaxHp > 0f ? hero.Hp / hero.MaxHp : 1f;
        float homeDistance = hero.DistanceTo(hall);

        if (hero.RetreatOrdered)
        {
            if (ratio > 0.7f || homeDistance < 500f)
            {
                hero.RetreatOrdered = false;
                if (homeDistance < 500f)
                {
                    hero.MoveTarget = null;
                    hero.Path = null;
                }
                ClearHeroEscorts(slot.Id);
                return;
            }
        }
        else if (ratio <= 0.35f && homeDistance > 500f && hero.InjuryFlee == null
            && (hero.Target != null || IsHeroThreatened(hero)))
        {
            hero.RetreatOrdered = true;
            hero.Target = null;
            hero.MoveTarget = hall.Position;
            hero.Path = null;
            if (_heroGuardLogBudget > 0)
            {
                _heroGuardLogBudget--;
                GD.Print($"[AI] P{slot.Id} 英雄 {hero.Type.Name} 残血 {ratio * 100f:0}% 撤退回基地");
            }
        }

        if (!hero.RetreatOrdered)
        {
            ClearHeroEscorts(slot.Id);
            return;
        }

        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker || unit.IsHero) continue;
            if (unit.DistanceTo(hero) > 1200f) continue;
            if (unit.InjuryFlee != null) continue;

            bool fresh = !unit.HeroEscort;
            unit.HeroEscort = true;
            unit.Kiting = false;
            if (!fresh && unit.MoveTarget.HasValue
                && (unit.MoveTarget.Value - hero.Position).LengthSquared() < 220f * 220f) continue;

            unit.Target = null;
            unit.MoveTarget = hero.Position;
            unit.Path = null;
        }
    }

    private void ClearHeroEscorts(int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || unit.Player != player || !unit.HeroEscort) continue;
            unit.HeroEscort = false;
            unit.MoveTarget = null;
            unit.Path = null;
            unit.PathIndex = 0;
        }
    }

    private bool IsHeroThreatened(Unit hero)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive) continue;
            if (unit.Player == hero.Player || AreAllied(unit.Player, hero.Player)) continue;
            if (ReferenceEquals(unit.Target, hero)) return true;
        }
        return false;
    }

    private Unit FindHero(int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player || !unit.IsHero) continue;
            return unit;
        }
        return null;
    }

    private int _focusLogBudget = 8;

    /// <summary>
    /// 派工修理：己方有建筑掉到 95% 以下时，找一个没在采集/建造/修理的工人去修；
    /// 修理本身按物编 DataA 速率回血、按 DataC 扣金（World.TickRepairs）。
    /// </summary>
    private void TickAiRepair(PlayerSlot slot)
    {
        Unit worst = null;
        float lowest = 0.95f;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (unit.IsUnderConstruction) continue;

            float ratio = unit.Hp / Mathf.Max(1f, unit.MaxHp);
            if (ratio >= lowest) continue;
            lowest = ratio;
            worst = unit;
        }
        if (worst == null) return;

        foreach (Unit worker in _allUnits)
        {
            if (!worker.Alive || worker.Player != slot.Id || !worker.IsWorker) continue;
            if (worker.BuildSite != null || worker.RepairTarget != null) continue;
            if (worker.GoldMine != null || worker.Tree != null) continue;
            if (worker.FindAbility("Ahrp") == null && worker.FindAbility("Arep") == null) continue;

            worker.RepairTarget = worst;
            worker.Repairing = false;
            worker.RepairGoldDebt = 0f;
            worker.Target = null;
            worker.MoveTarget = null;
            worker.Path = null;
            return;
        }
    }

    /// <summary>
    /// 守家阵型：没目标、没撤退的部队在基地前按编队层排开（物编 formation：0 近战在前、2 远程在后）。
    /// 只有离自己的位超过 120 才重新下令，避免每 3 秒把人推来推去。
    /// </summary>
    private void TickAiFormation(PlayerSlot slot, Unit hall)
    {
        Vector3 forward = FormationForward(slot, hall);
        var idle = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker || unit.IsHero) continue;
            if (unit.Target != null || unit.RetreatOrdered || unit.HeroEscort) continue;
            if (unit.InjuryFlee != null) continue;
            if (IsHumanAttackGroupMember(slot.Id, unit)) continue;
            if (unit.DistanceTo(hall) > 1200f) continue;
            idle.Add(unit);
        }
        if (idle.Count < 2) return;

        Vector3 staging = hall.Position + forward * 260f;
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(idle, staging, forward);
        int moved = 0;
        foreach (KeyValuePair<Unit, Vector3> pair in slots)
        {
            Unit unit = pair.Key;
            Vector3 spot = pair.Value;
            float dx = unit.Position.X - spot.X;
            float dz = unit.Position.Z - spot.Z;
            if (dx * dx + dz * dz < 120f * 120f) continue;

            unit.MoveTarget = spot;
            unit.Path = null;
            moved++;
        }

        if (moved > 0 && _formationLogBudget > 0)
        {
            _formationLogBudget--;
            GD.Print($"[AI] P{slot.Id} 守家阵型重排 {moved} 个单位（前排 {CountRank(idle, 0)}、后排 {CountRank(idle, 2)}）");
        }
    }

    private int _formationLogBudget = 6;

    private static int CountRank(List<Unit> units, int rank)
    {
        int count = 0;
        foreach (Unit unit in units)
        {
            if (unit.FormationRank == rank) count++;
        }
        return count;
    }

    /// <summary>阵型朝向：优先朝最近的敌人；没敌人就按大厅朝向。</summary>
    private Vector3 FormationForward(PlayerSlot slot, Unit hall)
    {
        Unit nearest = null;
        float best = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            if (unit.Player == slot.Id || AreAllied(unit.Player, slot.Id)) continue;
            if (unit.Player == 15 || unit.Player == 12) continue;
            float distance = unit.DistanceTo(hall);
            if (distance >= best) continue;
            best = distance;
            nearest = unit;
        }

        if (nearest != null)
        {
            Vector3 direction = nearest.Position - hall.Position;
            direction.Y = 0f;
            if (direction.LengthSquared() > 1f) return direction.Normalized();
        }
        return new Vector3(Mathf.Sin(hall.Facing), 0f, Mathf.Cos(hall.Facing));
    }

    /// <summary>
    /// 交战拉择：远程/法师被敌人贴到射程 35% 以内时，沿远离威胁的方向后撤到射程 60% 左右，
    /// 后退期间不再咬目标（Kiting），到位后重新索敌开火。冷却 2.5 秒，避免原地抖动。
    /// </summary>
    private void TickAiKite(PlayerSlot slot)
    {
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker || unit.RetreatOrdered || unit.HeroEscort) continue;
            if (IsHumanAttackGroupMember(slot.Id, unit)) continue;
            if (unit.KiteCooldown > 0f) unit.KiteCooldown -= 0.3f;
            if (unit.IsHero) continue;
            if (unit.Type.AttackRange < 250f) continue;

            Unit threat = unit.Target;
            if (threat == null || !threat.Alive)
            {
                unit.Kiting = false;
                continue;
            }
            if (unit.KiteCooldown > 0f) continue;

            float range = unit.Type.AttackRange;
            if (unit.DistanceTo(threat) > Mathf.Max(120f, range * 0.35f)) continue;

            Vector3 away = unit.Position - threat.Position;
            away.Y = 0f;
            if (away.LengthSquared() < 1f) away = new Vector3(1f, 0f, 0f);
            away = away.Normalized();

            Vector3 spot = unit.Position;
            float[] turns = { 0f, 0.6f, -0.6f, 1.2f, -1.2f, 1.8f, -1.8f };
            foreach (float turn in turns)
            {
                Vector3 candidate = unit.Position + away.Rotated(Vector3.Up, turn) * (range * 0.6f + 60f);
                if (!_world.CanStandAt(candidate, unit.Radius)) continue;
                if (_pathGrid != null && unit.Type != null
                    && _pathGrid.FindPath(unit.Position, candidate, unit.Radius, unit.Type.PathingLayer) == null) continue;
                spot = candidate;
                break;
            }

            unit.Target = null;
            unit.MoveTarget = spot;
            unit.Path = null;
            unit.Kiting = true;
            unit.KiteCooldown = 2.5f;

            if (_kiteLogBudget > 0)
            {
                _kiteLogBudget--;
                GD.Print($"[AI] P{slot.Id} {unit.Type.Name} 被贴脸后撤（距离 {unit.DistanceTo(threat):0}，射程 {range:0}）");
            }
        }
    }

    private int _kiteLogBudget = 8;

    /// <summary>
    /// 买装备：AI 英雄站在商店 700 内、背包有空位、钱够，就买店里最便宜且有货的那件。
    /// 贵的东西（比如 1000 金神器）要等经济起来，这条规则自然就把它排在后面。
    /// </summary>
    private void TickAiShopping(PlayerSlot slot)
    {
        Unit hero = null;
        foreach (Unit unit in _allUnits)
        {
            if (unit.Alive && unit.Player == slot.Id && unit.IsHero) { hero = unit; break; }
        }
        if (hero == null || !hero.HasFreeSlot) return;

        Unit shop = null;
        float bestDistance = 700f;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || !unit.IsShop) continue;
            float distance = hero.DistanceTo(unit);
            if (distance >= Mathf.Min(bestDistance, ShopPurchaseRange(unit))) continue;
            bestDistance = distance;
            shop = unit;
        }
        if (shop == null) return;

        string pick = null;
        int pickCost = int.MaxValue;
        foreach (string itemId in shop.Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(itemId);
            if (type == null || type.GoldCost <= 0) continue;
            if (shop.StockOf(itemId) <= 0) continue;
            if (type.GoldCost > slot.Gold) continue;
            if (type.GoldCost >= pickCost) continue;
            pickCost = type.GoldCost;
            pick = itemId;
        }
        if (pick == null) return;

        if (!BuyItemFromShop(shop, slot.Id, pick)) return;
        if (_shopLogBudget > 0)
        {
            _shopLogBudget--;
            GD.Print($"[AI] P{slot.Id} 英雄买了 {ItemCatalog.Get(pick)?.Name}（{pickCost} 金）");
        }
    }

    private int _shopLogBudget = 8;

    /// <summary>
    /// 中立建筑买兵：己方单位站在酒馆/雇佣兵营地/地精实验室的购买范围内时，
    /// 挑一个买得起、有货、人口与前置都满足的买下；贵的优先（战力口径同换兵）。
    /// 客户端也是「有单位在店里才能买」，AI 不额外跑腿，只看路过时能不能顺手补一个。
    /// </summary>
    private void TickAiHiring(PlayerSlot slot)
    {
        foreach (Unit own in _allUnits)
        {
            if (own == null || !own.Alive || own.Player != slot.Id || own.IsBuilding) continue;

            foreach (Unit shop in _allUnits)
            {
                if (shop == null || !shop.Alive || !shop.Type.SellsUnits) continue;
                if (own.DistanceTo(shop) > ShopPurchaseRange(shop)) continue;

                string pick = PickHire(slot, shop);
                if (pick == null) continue;
                if (!BuyUnitFromShop(shop, slot.Id, pick)) continue;

                ClearHireTrips(slot.Id, shop);
                if (_hireLogBudget > 0)
                {
                    _hireLogBudget--;
                    GD.Print($"[AI] P{slot.Id} 在 {shop.Type.Name} 买了 {UnitFactory.Get(pick)?.Name}");
                }
                return;
            }
        }

        DispatchHireTrip(slot);
    }

    private void DispatchHireTrip(PlayerSlot slot)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id) continue;
            if (unit.HireShop != null)
            {
                if (!unit.HireShop.Alive || PickHire(slot, unit.HireShop) == null)
                {
                    unit.HireShop = null;
                    unit.MoveTarget = null;
                }
                continue;
            }
        }

        foreach (Unit shop in _allUnits)
        {
            if (shop == null || !shop.Alive || !shop.Type.SellsUnits) continue;

            bool inbound = false;
            foreach (Unit unit in _allUnits)
            {
                if (unit != null && unit.Player == slot.Id && unit.HireShop == shop) { inbound = true; break; }
            }
            if (inbound) continue;

            string pick = PickHire(slot, shop);
            if (pick == null) continue;

            Unit carrier = null;
            float best = float.MaxValue;
            foreach (Unit own in _allUnits)
            {
                if (own == null || !own.Alive || own.Player != slot.Id) continue;
                if (own.IsBuilding || own.IsWorker || own.IsHero || own.HireShop != null) continue;
                if (own.RetreatOrdered || own.Kiting || own.HeroEscort || own.InjuryFlee != null) continue;
                if (IsHumanAttackGroupMember(slot.Id, own)) continue;
                float distance = own.DistanceTo(shop);
                if (distance <= ShopPurchaseRange(shop) || distance >= best) continue;
                best = distance;
                carrier = own;
            }
            if (carrier == null) continue;

            Vector3 offset = carrier.Position - shop.Position;
            offset.Y = 0f;
            if (offset.LengthSquared() < 1f) offset = new Vector3(1f, 0f, 0f);
            offset = offset.Normalized() * (ShopPurchaseRange(shop) * 0.6f);
            var spot = new Vector3(shop.Position.X + offset.X, shop.Position.Y, shop.Position.Z + offset.Z);

            carrier.HireShop = shop;
            carrier.Target = null;
            carrier.MoveTarget = spot;
            carrier.Path = null;
            if (_hireLogBudget > 0)
            {
                _hireLogBudget--;
                GD.Print($"[AI] P{slot.Id} 派 {carrier.Type.Name} 去 {shop.Type.Name} 买 {UnitFactory.Get(pick)?.Name}");
            }
            return;
        }
    }

    private void ClearHireTrips(int player, Unit shop)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || unit.Player != player || unit.HireShop != shop) continue;
            unit.HireShop = null;
            if (unit.MoveTarget.HasValue && unit.DistanceTo(shop) <= ShopPurchaseRange(shop))
                unit.MoveTarget = null;
        }
    }

    private int _hireLogBudget = 8;

    private string PickHire(PlayerSlot slot, Unit shop)
    {
        string pick = null;
        int pickCost = -1;

        foreach (string unitId in shop.Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(unitId);
            if (type == null) continue;
            if (shop.UnitStockOf(unitId) <= 0) continue;
            if (type.GoldCost + 100 > slot.Gold || type.LumberCost > slot.Lumber) continue;
            if (type.FoodUsed > 0 && FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id)) continue;
            if (type.IsHero && HeroSlotBlocked(slot, type)) continue;

            string[] requires = TechTree.Row(unitId)?.RequiresPrimary ?? Array.Empty<string>();
            if (!PurchaseRequirementsMet(slot, requires)) continue;
            if (CountUnitsOfType(slot.Id, unitId) >= 3) continue;
            if (type.GoldCost <= pickCost) continue;

            pickCost = type.GoldCost;
            pick = unitId;
        }
        return pick;
    }

    private int CountUnitsOfType(int player, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player) continue;
            if (!string.Equals(unit.Type?.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
        }
        return count;
    }

    /// <summary>
    /// AI 英雄喝药：血低于 60% 喝回血、蓝低于 40% 喝回蓝，只动「点了即用」的药水，
    /// 目标型物品不进这里（AI 不选目标）。
    /// </summary>
    private void TickAiItems(PlayerSlot slot)
    {
        if (_world == null) return;

        foreach (Unit hero in _allUnits)
        {
            if (!hero.Alive || hero.Player != slot.Id || !hero.IsHero || hero.Inventory == null) continue;

            for (int slotIndex = 0; slotIndex < hero.Inventory.Length; slotIndex++)
            {
                Item item = hero.Inventory[slotIndex];
                ItemType type = item?.Type;
                if (type == null || item.CooldownRemaining > 0f) continue;

                ItemUsePlan plan = ItemUseRules.PlanFor(type);
                if (!plan.Usable || plan.TargetMode != ItemTargetMode.None) continue;

                ItemConsumable effect = type.Consumable;
                if (effect.IsEmpty) continue;

                bool needHeal = effect.Heal > 0f && hero.Hp < hero.MaxHp * 0.6f;
                bool needMana = effect.Mana > 0f && hero.MaxMana > 0f && hero.Mana < hero.MaxMana * 0.4f;
                if (!needHeal && !needMana) continue;

                bool used = SimDriven
                    ? UseSimItem(hero, item, slotIndex, null, hero.Position)
                    : _world.UseItem(hero, item);
                if (!used) continue;
                if (_itemLogBudget > 0)
                {
                    _itemLogBudget--;
                    GD.Print($"[AI] P{slot.Id} 英雄喝了 {type.Name}（{hero.Hp:0}/{hero.MaxHp:0} 血 {hero.Mana:0}/{hero.MaxMana:0} 蓝）");
                }
                return;
            }
        }
    }

    private int _itemLogBudget = 6;

    private Unit NearestTargetOf(int player, Vector3 from)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player) continue;
            float distance = new Vector2(unit.Position.X - from.X, unit.Position.Z - from.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private Unit FindWorker(int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit.Alive && unit.Player == player && unit.IsWorker) return unit;
        }
        return null;
    }

    public void RunHousekeeping()
    {
        AiEnabled = true;
        foreach (PlayerSlot slot in Slots())
        {
            slot.Ai = true;
            BuildPlan(slot);
        }
        _hud?.SetHint("[内务] 四个 AI 同时开工");
    }

    public void RunHousekeeping(int player)
    {
        if (Slot(player) == null) return;
        BuildPlan(Slot(player));
    }

    private void BuildPlan(PlayerSlot slot)
    {
        if (IsHumanOriginalAi(slot))
        {
            InitializeHumanAi(slot);
            return;
        }
        if (IsOrcOriginalAi(slot))
        {
            InitializeOrcAi(slot);
            return;
        }

        slot.Tasks.Clear();
        slot.Done = false;
        slot.Announced = false;

        Unit hall = FindHall(slot.Id);
        if (hall == null)
        {
            GD.Print($"[内务] P{slot.Id} 找不到主基地，跳过");
            return;
        }

        string workerId = hall.Type.Trains.Length > 0 ? hall.Type.Trains[0] : null;
        UnitType worker = UnitFactory.Get(workerId);

        var hallFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectUpgradeFamily(hall.Type.Id, hallFamily);

        var buildable = new List<string>();
        if (worker != null)
        {
            foreach (string id in worker.Builds)
            {
                if (hallFamily.Contains(id)) continue;
                buildable.Add(id);
            }
        }

        // 建造优先级：先人口（否则卡人口），再英雄祭坛（原版 AI 也是先出英雄），然后出兵建筑
        buildable.Sort((left, right) => BuildPriority(left).CompareTo(BuildPriority(right)));

        foreach (string id in buildable)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;
            int total = Mathf.Max(1, type.UpgradeTo.Length);
            if (type.FoodMade > 0) total = Mathf.Max(total, 2);
            slot.Tasks.Add(new InhouseTask { Kind = "build", Id = id, Total = total, Label = type.Name });
        }

        foreach (string id in buildable)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;
            foreach (string upgradeId in type.Researches)
            {
                UpgradeType upgrade = TechTree.Upgrade(upgradeId);
                if (upgrade == null) continue;
                slot.Tasks.Add(new InhouseTask { Kind = "research", Id = upgradeId, Total = upgrade.MaxLevel, Label = upgrade.Name });
            }
        }

        var trainSources = new List<string>(buildable) { hall.Type.Id };
        var trained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in trainSources)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;
            foreach (string unitId in type.Trains)
            {
                if (!trained.Add(unitId)) continue;
                UnitType unit = UnitFactory.Get(unitId);
                if (unit == null) continue;
                int total = unitId == workerId ? 6 : 1;
                slot.Tasks.Add(new InhouseTask { Kind = "train", Id = unitId, Total = total, Label = unit.Name });
            }
        }

        GD.Print($"[内务] P{slot.Id} 计划 {slot.Tasks.Count} 条（建筑 {buildable.Count} 种，工人 {worker?.Name}）");
    }

    /// <summary>0=人口建筑，1=能出英雄的祭坛，2=出兵建筑，3=其余。</summary>
    private static int BuildPriority(string id)
    {
        UnitType type = UnitFactory.Get(id);
        if (type == null) return 9;
        if (type.FoodMade > 0) return 0;

        foreach (string unitId in type.Trains)
        {
            UnitType trained = UnitFactory.Get(unitId);
            if (trained != null && trained.IsHero) return 1;
        }
        return type.Trains.Length > 0 ? 2 : 3;
    }

    private void CollectUpgradeFamily(string id, HashSet<string> family)
    {
        if (string.IsNullOrEmpty(id) || !family.Add(id)) return;
        UnitType type = UnitFactory.Get(id);
        if (type == null) return;
        foreach (string target in type.UpgradeTo) CollectUpgradeFamily(target, family);
    }

    /// <summary>托管座位的英雄加点：不受内务计划是否完成影响，攒着没花的点每 0.3 秒清一次。</summary>
    private void TickAiHeroSkills(PlayerSlot slot)
    {
        if (_world == null) return;
        if (SimDriven)
        {
            TickSimHeroSkills(slot);
            return;
        }
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id || !unit.IsHero) continue;
            if (unit.SkillPoints <= 0) continue;

            var before = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Ability ability in unit.Abilities) before[ability.Id] = ability.Level;

            _world.AutoLearnHeroSkills(unit);

            foreach (Ability ability in unit.Abilities)
            {
                int old = before.TryGetValue(ability.Id, out int level) ? level : 0;
                if (ability.Level <= old) continue;
                if (_heroSkillLogBudget <= 0) break;
                _heroSkillLogBudget--;
                GD.Print($"[AI] P{slot.Id} {unit.Type.Name} 加点 {ability.Name} L{ability.Level}"
                    + $"（等级 {unit.Level}，剩 {unit.SkillPoints} 点）");
            }
        }
    }

    private int _heroSkillLogBudget = 40;

    private void TickAi(PlayerSlot slot, float delta)
    {
        if (!slot.Ai) return;

        slot.Timer += delta;
        if (slot.Timer < 0.3f + slot.Id * 0.05f) return;
        slot.Timer = 0f;

        TickAiHeroSkills(slot);

        if (slot.Done) return;

        if (IsOrcOriginalAi(slot))
        {
            TickOrcAi(slot, 0.3f);
            return;
        }
        if (IsHumanOriginalAi(slot))
        {
            TickHumanAi(slot, 0.3f);
            return;
        }

        Unit hall = FindHall(slot.Id);
        if (hall == null) return;

        // 对手阵容每 2 秒重算一次：出兵/波次编成靠它挑兵种
        slot.EnemyTimer += 0.3f;
        if (slot.EnemyTimer >= 2f)
        {
            slot.EnemyTimer = 0f;
            slot.Enemy = AiCounters.Analyze(_allUnits, slot.Id, AreAllied);
        }

        TickAiRetreat(slot);
        TickAiHeroGuard(slot, hall);
        TickAiKite(slot);
        slot.FocusTimer += 0.3f;
        if (slot.FocusTimer >= 2f)
        {
            slot.FocusTimer = 0f;
            TickAiFocus(slot);
        }
        slot.ShopTimer += 0.3f;
        if (slot.ShopTimer >= 6f)
        {
            slot.ShopTimer = 0f;
            TickAiShopping(slot);
        }

        slot.ItemTimer += 0.3f;
        if (slot.ItemTimer >= 3f)
        {
            slot.ItemTimer = 0f;
            TickAiItems(slot);
        }

        slot.HireTimer += 0.3f;
        if (slot.HireTimer >= 8f)
        {
            slot.HireTimer = 0f;
            TickAiHiring(slot);
        }

        slot.FormationTimer += 0.3f;
        if (slot.FormationTimer >= 3f)
        {
            slot.FormationTimer = 0f;
            TickAiFormation(slot, hall);
        }

        slot.RepairTimer += 0.3f;
        if (slot.RepairTimer >= 5f && !IsHumanOriginalAi(slot))
        {
            slot.RepairTimer = 0f;
            TickAiRepair(slot);
        }

        if (IsHumanOriginalAi(slot))
        {
            TickHumanAi(slot, 0.3f);
            return;
        }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (PlayerOrders(slot.Id) >= 4) break;
            if (FoodCap(slot.Id) - FoodUsed(slot.Id) <= 4 && ActionFarm(slot, hall)) continue;
            if (ActionBuild(slot, hall)) continue;
            if (ActionUpgrade(slot, hall)) continue;
            if (ActionResearch(slot, hall)) continue;
            if (ActionTrain(slot)) continue;
            if (ActionStaffSites(slot)) continue;
            if (ActionGather(slot)) continue;
            break;
        }

        slot.StatusTimer += 0.3f;
        if (slot.StatusTimer >= 12f)
        {
            slot.StatusTimer = 0f;
            LogStatus(slot);
        }

        if (PlayerOrders(slot.Id) > 0) return;

        foreach (InhouseTask task in slot.Tasks)
        {
            if (TaskDone(slot, task) < task.Total) return;
        }

        slot.Done = true;
        if (slot.Announced) return;
        slot.Announced = true;

        int buildings = 0;
        int units = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding) buildings++;
            else units++;
        }
        GD.Print($"[内务] P{slot.Id} 完成：建筑 {buildings} 座，单位 {units} 个，资源 {slot.Gold}/{slot.Lumber}，人口 {FoodUsed(slot.Id)}/{FoodCap(slot.Id)}");
    }

    private int PlayerOrders(int player)
    {
        if (SimDriven)
        {
            int simCount = 0;
            foreach (SimTrainJob job in _simTrainJobs)
                if (job.Building != null && job.Building.Player == player) simCount++;
            foreach (SimBuildJob job in _simBuildJobs)
                if (job.Site != null && job.Site.Player == player) simCount++;
            return simCount;
        }

        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player == player) count++;
        }
        return count;
    }

    private int CountBuildings(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountBuildingsUnderConstruction(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || !unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountUnits(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || unit.IsBuilding) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountTrainOrders(int player, string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != player || order.Kind != OrderKind.Train) continue;
            if (string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountPlayerResearchOrders(int player, string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != player || order.Kind != OrderKind.Research) continue;
            if (string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int TaskDone(PlayerSlot slot, InhouseTask task)
    {
        switch (task.Kind)
        {
            case "build":
                return Mathf.Max(CountBuildings(slot, task.Id), slot.BuiltCount(task.Id));
            case "research":
                return slot.ResearchLevel(task.Id) + CountPlayerResearchOrders(slot.Id, task.Id);
            case "train":
                return CountUnits(slot, task.Id) + CountTrainOrders(slot.Id, task.Id);
            default:
                return 0;
        }
    }

    private void LogStatus(PlayerSlot slot)
    {
        int buildDone = 0;
        int buildTotal = 0;
        int researchDone = 0;
        int researchTotal = 0;
        int trainDone = 0;
        int trainTotal = 0;
        foreach (InhouseTask task in slot.Tasks)
        {
            int done = TaskDone(slot, task);
            switch (task.Kind)
            {
                case "build":
                    buildDone += Mathf.Min(done, task.Total);
                    buildTotal += task.Total;
                    break;
                case "research":
                    researchDone += Mathf.Min(done, task.Total);
                    researchTotal += task.Total;
                    break;
                case "train":
                    trainDone += Mathf.Min(done, task.Total);
                    trainTotal += task.Total;
                    break;
            }
        }

        int golders = 0;
        int choppers = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) golders++;
            else if (unit.Tree != null) choppers++;
        }

        if (slot.Id == ViewPlayer)
        {
            foreach (Unit worker in _allUnits)
            {
                if (!worker.Alive || worker.Player != slot.Id || worker.IsBuilding) continue;
                string state = worker.BuildSite != null ? "建造" : worker.GoldMine != null ? "采金" : worker.Tree != null ? "伐木"
                    : worker.Target != null ? "战斗" : worker.MoveTarget.HasValue ? "移动" : "空闲";
                GD.Print($"[内务]   工人 {worker.Type.Id} 状态={state} HP={worker.Hp:0} 位置({worker.Position.X:0},{worker.Position.Z:0})"
                    + $" 携带={worker.Carry}");
            }
        }

        GD.Print($"[内务] P{slot.Id} 进度 建筑 {buildDone}/{buildTotal}  科技 {researchDone}/{researchTotal}  兵种 {trainDone}/{trainTotal}"
            + $"  队列 {PlayerOrders(slot.Id)}  人口 {FoodUsed(slot.Id)}/{FoodCap(slot.Id)}  采金 {golders} 伐木 {choppers}  资源 {slot.Gold}/{slot.Lumber}");
    }

    private bool ActionStaffSites(PlayerSlot slot)
    {
        foreach (Unit site in _allUnits)
        {
            if (!site.Alive || site.Player != slot.Id || !site.IsBuilding || !site.IsUnderConstruction) continue;
            if (SiteHasBuilder(site)) continue;

            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (Unit unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
                if (!IsIdle(unit)) continue;
                float distance = unit.DistanceTo(site);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            if (best == null) continue;
            _world.SetBuild(best, site);
            return true;
        }
        return false;
    }

    private bool ActionGather(PlayerSlot slot)
    {
        Unit worker = null;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (!unit.Type.CanGatherGold && !unit.Type.CanGatherLumber) continue;
            if (!IsIdle(unit)) continue;
            worker = unit;
            break;
        }
        if (worker == null) return false;

        Unit mine = null;
        float mineDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            float distance = new Vector2(worker.Position.X - unit.Position.X, worker.Position.Z - unit.Position.Z).Length();
            if (distance < mineDistance)
            {
                mineDistance = distance;
                mine = unit;
            }
        }

        int goldWorkers = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) goldWorkers++;
        }

        if (worker.BuildSite != null) return false;

        if (worker.Type.CanGatherGold && mine != null && goldWorkers < 3)
        {
            _world.SetGather(worker, mine);
            return true;
        }

        if (!worker.Type.CanGatherLumber) return false;
        TreeState tree = NearestTreeTo(worker.Position);
        if (tree == null) return false;
        _world.SetChop(worker, tree);
        return true;
    }

    private static bool IsIdle(Unit unit)
    {
        return unit.GoldMine == null && unit.Tree == null && unit.BuildSite == null
            && unit.Target == null && !unit.MoveTarget.HasValue;
    }

    private TreeState NearestTreeTo(Vector3 position)
    {
        TreeState best = null;
        float bestDistance = float.MaxValue;
        foreach (TreeState tree in _trees)
        {
            if (!tree.Alive) continue;
            float distance = new Vector2(position.X - tree.Position.X, position.Z - tree.Position.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }
        return best;
    }

    private bool ActionFarm(PlayerSlot slot, Unit hall)
    {
        Unit worker = FindWorker(slot.Id);
        if (worker == null) return false;

        foreach (string id in worker.Type.Builds)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null || type.FoodMade <= 0) continue;
            if (type.Trains.Length > 0) continue;
            if (CountBuildings(slot, id) >= 12) continue;
            if (CountBuildingsUnderConstruction(slot, id) > 0) continue;
            if (!RequirementsMet(slot, type.Requires)) continue;
            return BuildOne(slot, id, hall);
        }
        return false;
    }

    private bool ActionBuild(PlayerSlot slot, Unit hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "build") continue;
            if (TaskDone(slot, task) >= task.Total) continue;
            UnitType type = UnitFactory.Get(task.Id);
            if (type == null || !RequirementsMet(slot, type.Requires)) continue;
            return BuildOne(slot, task.Id, hall);
        }
        return false;
    }

    private int CountActiveSites(PlayerSlot slot)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (unit.IsUnderConstruction) count++;
        }
        return count;
    }

    private bool SiteHasBuilder(Unit site)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit.Alive && ReferenceEquals(unit.BuildSite, site)) return true;
        }
        return false;
    }

    private bool BuildOne(PlayerSlot slot, string buildingId, Unit hall)
    {
        UnitType type = UnitFactory.Get(buildingId);
        if (type == null) return false;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost) return false;
        if (CountActiveSites(slot) >= 3) return false;

        // 不能把所有工人都派去盖房子，否则采集直接归零（亡灵 3 侍僧尤其明显）
        int freeWorkers = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (unit.BuildSite == null) freeWorkers++;
        }
        if (freeWorkers == 0) return false;

        Vector3 spot;
        if (IsMineConsumingType(type))
        {
            Unit mine = FindConsumableMine(hall.Position, 2400f);
            if (mine == null)
            {
                GD.Print($"[内务] P{slot.Id} 找不到可占据的金矿，跳过 {type.Name}");
                return false;
            }
            spot = mine.Position;
        }
        else if (!TryFindBuildSpot(hall.Position, Mathf.Max(32f, type.FootprintRadius), out spot))
        {
            GD.Print($"[内务] P{slot.Id} 找不到 {type.Name} 的位置");
            return false;
        }

        Unit site = PlaceBuildingAt(slot, buildingId, spot, false);
        if (site == null) return false;
        return true;
    }

    private bool ActionUpgrade(PlayerSlot slot, Unit hall)
    {
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (unit.Type.UpgradeTo.Length == 0) continue;

            string targetId = null;
            foreach (string candidate in unit.Type.UpgradeTo)
            {
                if (CountBuildings(slot, candidate) > 0) continue;
                UnitType target = UnitFactory.Get(candidate);
                if (target == null || !RequirementsMet(slot, target.Requires)) continue;
                targetId = candidate;
                break;
            }
            if (targetId == null) continue;

            if (SimDriven && QueueLength(unit) > 0) continue;
            CancelTrainingOrders(unit);
            if (QueueLength(unit) > 0) continue;
            return StartBuildingUpgrade(unit, targetId);
        }
        return false;
    }

    private bool ActionResearch(PlayerSlot slot, Unit hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "research") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UpgradeType upgrade = TechTree.Upgrade(task.Id);
            if (upgrade == null || !RequirementsMet(slot, upgrade.Requires)) continue;

            foreach (Unit unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Researches, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                return StartResearch(unit, task.Id);
            }
        }
        return false;
    }

    /// <summary>
    /// 出兵：按任务表把没完成的兵种列出来，按「对手阵容收益」排序后逐个试造。
    /// 排序口径：工人优先（不然经济直接崩）→ 英雄带加成 → 其余看护甲倍率。
    /// 排前面的造不出来（钱/人口/队列）就接着试下一个，不会卡住整轮。
    /// </summary>
    private bool ActionTrain(PlayerSlot slot)
    {
        var picks = new List<(InhouseTask Task, float Score)>();
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "train") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UnitType type = UnitFactory.Get(task.Id);
            if (type == null || !RequirementsMet(slot, type.Requires)) continue;
            if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id)) continue;
            if (!HasProducer(slot.Id, task.Id)) continue;

            float score = type.IsWorkerType ? 2f : AiCounters.Score(type, slot.Enemy);
            if (type.IsHero) score += 0.4f;
            picks.Add((task, score));
        }

        picks.Sort((left, right) => right.Score.CompareTo(left.Score));
        foreach ((InhouseTask Task, float Score) pick in picks)
        {
            if (StartTrainingOf(slot, pick.Task.Id)) return true;
        }
        return false;
    }

    private bool HasProducer(int player, string unitId)
    {
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (Array.IndexOf(unit.Type.Trains, unitId) >= 0) return true;
        }
        return false;
    }

    private bool StartTrainingOf(PlayerSlot slot, string unitId)
    {
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (Array.IndexOf(unit.Type.Trains, unitId) < 0) continue;
            if (QueueLength(unit) >= 2) continue;
            if (!SimDriven && HasPendingUpgrade(unit)) continue;
            return StartTraining(unit, unitId);
        }
        return false;
    }
}
