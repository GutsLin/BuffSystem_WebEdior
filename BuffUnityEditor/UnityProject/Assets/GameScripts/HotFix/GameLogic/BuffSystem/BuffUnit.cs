using System;
using System.Collections.Generic;
using UnityEngine;
using GameplayTags;

namespace GameLogic.Buffs
{
    public sealed class BuffUnit
    {
        private readonly BuffWorld _world;
        private readonly Dictionary<string, float> _baseAttributes =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly List<BuffInstance> _activeBuffs = new List<BuffInstance>();
        private readonly Dictionary<string, BuffInstance> _activeBuffIndex =
            new Dictionary<string, BuffInstance>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BuffInstance> _iterationSnapshot = new List<BuffInstance>();

        public string Id { get; }
        public PrimaryAttributeType PrimaryAttribute { get; set; }
        public int TeamId { get; set; }
        public Vector3 Position { get; set; }
        public float CurrentHp { get; private set; }
        public float CurrentMana { get; private set; }
        public bool IsAlive => CurrentHp > 0f;
        public IReadOnlyList<BuffInstance> ActiveBuffs => _activeBuffs;

        public GameplayTagContainer GameplayTags
        {
            get
            {
                GameplayTagContainer tags = new GameplayTagContainer();
                for (int index = 0; index < _activeBuffs.Count; index++)
                {
                    BuffInstance instance = _activeBuffs[index];
                    if (instance != null && !instance.IsRemoved)
                    {
                        tags.AddTags(instance.GameplayTags);
                    }
                }
                return tags;
            }
        }

        public bool HasGameplayTag(string tagName)
        {
            return GameplayTagManager.RequestTag(tagName, out GameplayTag tag) && GameplayTags.HasTag(tag);
        }

        public bool CanMove => IsAlive &&
                               !HasAnyStatusEffect("Stun", "Root", "Hex", "Fear", "Taunt");

        public bool CanAttack => IsAlive && !HasAnyStatusEffect("Stun", "Disarm", "Hex");
        public bool CanCast => IsAlive && !HasAnyStatusEffect("Stun", "Silence", "Hex");
        public bool CanUseItems => IsAlive && !HasAnyStatusEffect("Stun", "Muted", "Hex");
        public bool PassivesEnabled => IsAlive && !HasStatusEffect("Break");

        internal BuffUnit(
            BuffWorld world,
            string id,
            PrimaryAttributeType primaryAttribute,
            int teamId,
            IDictionary<string, float> baseAttributes)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            PrimaryAttribute = primaryAttribute;
            TeamId = teamId;

            if (baseAttributes != null)
            {
                foreach (KeyValuePair<string, float> pair in baseAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        _baseAttributes[pair.Key] = pair.Value;
                    }
                }
            }

            ResetResources();
        }

        public float GetBaseAttribute(string attributeType)
        {
            if (string.IsNullOrWhiteSpace(attributeType))
            {
                return 0f;
            }

            return _baseAttributes.TryGetValue(attributeType, out float value) ? value : 0f;
        }

        public void SetBaseAttribute(string attributeType, float value, bool refillResources = false)
        {
            if (string.IsNullOrWhiteSpace(attributeType))
            {
                throw new ArgumentException("属性名称不能为空。", nameof(attributeType));
            }

            _baseAttributes[attributeType] = value;

            if (refillResources)
            {
                ResetResources();
            }
            else
            {
                ClampResources();
            }

            _world.RaiseEvent(new BuffRuntimeEvent(
                BuffRuntimeEventType.BaseAttributeChanged,
                this,
                value: value,
                attributeType: attributeType));
        }

        public void AddBaseAttribute(string attributeType, float delta)
        {
            SetBaseAttribute(attributeType, GetBaseAttribute(attributeType) + delta);
        }

        public float GetAttribute(string attributeType)
        {
            if (string.IsNullOrWhiteSpace(attributeType))
            {
                return 0f;
            }

            float result;
            if (EqualsAttribute(attributeType, CombatAttributeNames.Strength))
            {
                result = CalculateModifiedAttribute(attributeType, GetBaseAttribute(CombatAttributeNames.Strength));
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.Agility))
            {
                result = CalculateModifiedAttribute(attributeType, GetBaseAttribute(CombatAttributeNames.Agility));
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.Intelligence))
            {
                result = CalculateModifiedAttribute(attributeType, GetBaseAttribute(CombatAttributeNames.Intelligence));
            }
            else
            {
                result = CalculateDerivedAttribute(attributeType);
            }

            if (EqualsAttribute(attributeType, CombatAttributeNames.AttackSpeed))
            {
                AttributeFormulaConfig formula = _world.Database.AttributeFormula;
                result = Clamp(result, formula.MinAttackSpeed, formula.MaxAttackSpeed);
            }

            return result;
        }

        public BuffInstance ApplyBuff(string idOrKey, BuffUnit source = null)
        {
            return ApplyBuff(_world.Database.GetRequired(idOrKey), source);
        }

        public BuffInstance ApplyBuff(BuffTemplate template, BuffUnit source = null)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.ModifierKind == BuffModifierKind.Debuff && HasStatusEffect("DebuffImmune"))
            {
                return null;
            }

            if (!CheckApplyConditions(template))
            {
                return null;
            }

            BuffInstance existing = FindFirstActiveInstance(template.Id, includeAuraProxies: false);
            if (template.StackPolicy == BuffStackPolicy.Independent || existing == null)
            {
                return CreateInstance(template, source ?? this, CalculateDuration(template), false, string.Empty, true);
            }

            switch (template.StackPolicy)
            {
                case BuffStackPolicy.Stack:
                    existing.Stacks = Math.Min(Math.Max(1, template.MaxStacks), existing.Stacks + 1);
                    existing.ResetDuration(CalculateDuration(template));
                    _world.RaiseEvent(new BuffRuntimeEvent(
                        BuffRuntimeEventType.BuffStackChanged,
                        this,
                        existing.Source,
                        existing,
                        existing.Stacks));
                    _world.ExecuteActions(existing, BuffEffectTrigger.OnStackChanged, this);
                    ClampResources();
                    return existing;

                case BuffStackPolicy.Replace:
                    RemoveInstance(existing, BuffRemovalReason.Replaced);
                    return CreateInstance(template, source ?? this, CalculateDuration(template), false, string.Empty, true);

                case BuffStackPolicy.Refresh:
                default:
                    existing.ResetDuration(CalculateDuration(template));
                    _world.RaiseEvent(new BuffRuntimeEvent(
                        BuffRuntimeEventType.BuffRefreshed,
                        this,
                        existing.Source,
                        existing));
                    _world.ExecuteActions(existing, BuffEffectTrigger.OnRefresh, this);
                    ClampResources();
                    return existing;
            }
        }

        public int RemoveBuff(string idOrKey, BuffRemovalReason reason = BuffRemovalReason.Manual)
        {
            if (!_world.Database.TryGet(idOrKey, out BuffTemplate template))
            {
                return 0;
            }

            List<BuffInstance> snapshot = new List<BuffInstance>(_activeBuffs);
            int removedCount = 0;
            for (int index = 0; index < snapshot.Count; index++)
            {
                BuffInstance instance = snapshot[index];
                if (!instance.IsRemoved && SameTemplate(instance.Template, template))
                {
                    RemoveInstance(instance, reason);
                    removedCount++;
                }
            }

            return removedCount;
        }

        public bool RemoveInstance(string instanceId, BuffRemovalReason reason = BuffRemovalReason.Manual)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            for (int index = 0; index < _activeBuffs.Count; index++)
            {
                BuffInstance instance = _activeBuffs[index];
                if (string.Equals(instance.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveInstance(instance, reason);
                    return true;
                }
            }

            return false;
        }

        public int Dispel(BuffDispelType dispelType, BuffModifierKind? modifierKind = null)
        {
            List<BuffInstance> snapshot = new List<BuffInstance>(_activeBuffs);
            int removedCount = 0;
            for (int index = 0; index < snapshot.Count; index++)
            {
                BuffInstance instance = snapshot[index];
                BuffTemplate template = instance.Template;
                if (instance.IsRemoved || instance.IsAuraProxy ||
                    (modifierKind.HasValue && template.ModifierKind != modifierKind.Value) ||
                    template.DispelRule == BuffDispelRule.NotDispellable)
                {
                    continue;
                }

                bool canDispel = dispelType == BuffDispelType.Strong ||
                                 template.DispelRule == BuffDispelRule.BasicDispellable;
                if (!canDispel)
                {
                    continue;
                }

                RemoveInstance(instance, BuffRemovalReason.Dispelled);
                removedCount++;
            }

            return removedCount;
        }

        public bool HasBuff(string idOrKey)
        {
            if (!_world.Database.TryGet(idOrKey, out BuffTemplate template))
            {
                return false;
            }

            return FindFirstActiveInstance(template.Id, includeAuraProxies: true) != null;
        }

        public bool HasStatusEffect(string statusEffect)
        {
            if (string.IsNullOrWhiteSpace(statusEffect))
            {
                return false;
            }

            for (int buffIndex = 0; buffIndex < _activeBuffs.Count; buffIndex++)
            {
                BuffInstance instance = _activeBuffs[buffIndex];
                if (!ShouldContribute(instance))
                {
                    continue;
                }

                List<string> statuses = instance.Template.StatusEffects;
                for (int statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
                {
                    if (string.Equals(statuses[statusIndex], statusEffect, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public BuffDamageResult TakeDamage(float rawDamage, BuffDamageType damageType, BuffUnit source = null)
        {
            BuffDamageResult result = new BuffDamageResult
            {
                RawDamage = Math.Max(0f, rawDamage),
                DamageType = damageType,
            };

            if (!IsAlive || result.RawDamage <= 0f)
            {
                result.WasBlocked = true;
                return result;
            }

            if (damageType != BuffDamageType.HpRemoval && HasStatusEffect("Invulnerable"))
            {
                result.WasBlocked = true;
                return result;
            }

            if (damageType == BuffDamageType.Physical && HasStatusEffect("Ethereal"))
            {
                result.WasBlocked = true;
                return result;
            }

            float actualDamage = result.RawDamage;
            if (damageType == BuffDamageType.Physical)
            {
                float armor = GetAttribute(CombatAttributeNames.Armor);
                float factor = _world.Database.AttributeFormula.PhysicalArmorFactor;
                float reduction = factor * armor / (1f + factor * Math.Abs(armor));
                actualDamage *= 1f - reduction;
            }
            else if (damageType == BuffDamageType.Magical)
            {
                actualDamage *= 1f - NormalizeRatio(GetAttribute(CombatAttributeNames.MagicResistance), 0.95f);
            }

            actualDamage = Math.Max(0f, actualDamage);
            CurrentHp = Math.Max(0f, CurrentHp - actualDamage);
            result.ActualDamage = actualDamage;

            _world.RaiseEvent(new BuffRuntimeEvent(
                BuffRuntimeEventType.DamageTaken,
                this,
                source,
                value: actualDamage,
                damageType: damageType));

            _world.Trigger(this, BuffEffectTrigger.OnTakeDamage, this);
            if (source != null && source != this)
            {
                _world.Trigger(source, BuffEffectTrigger.OnDealDamage, this);
            }

            if (CurrentHp <= 0f)
            {
                Die(source);
                result.KilledTarget = true;
            }

            return result;
        }

        public float Heal(float value, BuffUnit source = null)
        {
            if (!IsAlive || value <= 0f)
            {
                return 0f;
            }

            float before = CurrentHp;
            CurrentHp = Math.Min(GetAttribute(CombatAttributeNames.MaxHp), CurrentHp + value);
            float actual = CurrentHp - before;
            if (actual > 0f)
            {
                _world.RaiseEvent(new BuffRuntimeEvent(
                    BuffRuntimeEventType.Healed,
                    this,
                    source,
                    value: actual));
            }

            return actual;
        }

        public void NotifyAttackLanded(BuffUnit target)
        {
            if (!CanAttack || target == null)
            {
                return;
            }

            _world.Trigger(this, BuffEffectTrigger.OnAttackLanded, target);
        }

        public void ResetResources()
        {
            CurrentHp = Math.Max(0f, GetAttribute(CombatAttributeNames.MaxHp));
            CurrentMana = Math.Max(0f, GetAttribute(CombatAttributeNames.MaxMana));
        }

        public void Revive(float healthRatio = 1f, float manaRatio = 1f)
        {
            if (IsAlive)
            {
                return;
            }

            CurrentHp = GetAttribute(CombatAttributeNames.MaxHp) * Clamp01(healthRatio);
            CurrentMana = GetAttribute(CombatAttributeNames.MaxMana) * Clamp01(manaRatio);
            if (CurrentHp <= 0f)
            {
                CurrentHp = Math.Min(1f, GetAttribute(CombatAttributeNames.MaxHp));
            }

            _world.RaiseEvent(new BuffRuntimeEvent(BuffRuntimeEventType.UnitRevived, this));
        }

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (IsAlive)
            {
                CurrentHp = Math.Min(
                    GetAttribute(CombatAttributeNames.MaxHp),
                    CurrentHp + Math.Max(0f, GetAttribute(CombatAttributeNames.HpRegen)) * deltaTime);
                CurrentMana = Math.Min(
                    GetAttribute(CombatAttributeNames.MaxMana),
                    CurrentMana + Math.Max(0f, GetAttribute(CombatAttributeNames.ManaRegen)) * deltaTime);
            }

            CopyActiveBuffsToSnapshot();
            for (int index = 0; index < _iterationSnapshot.Count; index++)
            {
                BuffInstance instance = _iterationSnapshot[index];
                if (instance.IsRemoved || instance.IsRemoving)
                {
                    continue;
                }

                float activeDelta = instance.IsPermanent
                    ? deltaTime
                    : Math.Min(deltaTime, Math.Max(0f, instance.RemainingDuration));

                if (instance.Template.ThinkInterval > 0f && !instance.IsAuraProxy)
                {
                    instance.NextThinkRemaining -= activeDelta;
                    int safety = 0;
                    while (instance.NextThinkRemaining <= 0f && !instance.IsRemoved && safety < BuffConfig.MaxThinkAttemptsPerTick)
                    {
                        _world.ExecuteActions(instance, BuffEffectTrigger.OnIntervalThink, this);
                        instance.NextThinkRemaining += instance.Template.ThinkInterval;
                        safety++;
                    }
                }

                if (!instance.IsPermanent && !instance.IsRemoved)
                {
                    instance.RemainingDuration -= deltaTime;
                    if (instance.RemainingDuration <= 0f)
                    {
                        RemoveInstance(
                            instance,
                            instance.IsAuraProxy ? BuffRemovalReason.AuraLost : BuffRemovalReason.Expired);
                    }
                }
            }

            ClampResources();
        }

        internal BuffInstance RefreshAuraProxy(BuffInstance auraSource, float holdDuration)
        {
            string sourceInstanceId = auraSource.InstanceId;
            for (int index = 0; index < _activeBuffs.Count; index++)
            {
                BuffInstance existing = _activeBuffs[index];
                if (existing.IsAuraProxy && !existing.IsRemoved &&
                    string.Equals(existing.AuraSourceInstanceId, sourceInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    existing.AppliedDuration = holdDuration;
                    existing.RemainingDuration = holdDuration;
                    return existing;
                }
            }

            return CreateInstance(
                auraSource.Template,
                auraSource.Owner,
                holdDuration,
                true,
                sourceInstanceId,
                false);
        }

        internal void Clear(BuffRemovalReason reason)
        {
            List<BuffInstance> snapshot = new List<BuffInstance>(_activeBuffs);
            for (int index = 0; index < snapshot.Count; index++)
            {
                RemoveInstance(snapshot[index], reason);
            }

            _activeBuffIndex.Clear();
        }

        public BuffUnitSaveData SaveState()
        {
            BuffUnitSaveData saveData = new BuffUnitSaveData
            {
                unitId = Id,
                currentHp = CurrentHp,
                currentMana = CurrentMana,
                primaryAttribute = PrimaryAttribute,
                teamId = TeamId,
            };

            for (int index = 0; index < _activeBuffs.Count; index++)
            {
                BuffInstance instance = _activeBuffs[index];
                if (instance.IsRemoved)
                {
                    continue;
                }

                saveData.buffs.Add(new BuffInstanceSaveData
                {
                    templateId = instance.Template.Id,
                    remainingDuration = instance.RemainingDuration,
                    nextThinkRemaining = instance.NextThinkRemaining,
                    stacks = instance.Stacks,
                    isAuraProxy = instance.IsAuraProxy,
                    auraSourceInstanceId = instance.AuraSourceInstanceId ?? string.Empty,
                });
            }

            return saveData;
        }

        public void LoadState(BuffUnitSaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            Clear(BuffRemovalReason.Manual);
            CurrentHp = saveData.currentHp;
            CurrentMana = saveData.currentMana;
            PrimaryAttribute = saveData.primaryAttribute;
            TeamId = saveData.teamId;

            for (int index = 0; index < saveData.buffs.Count; index++)
            {
                BuffInstanceSaveData buffSave = saveData.buffs[index];
                if (!_world.Database.TryGet(buffSave.templateId, out BuffTemplate template))
                {
                    continue;
                }

                BuffInstance instance = CreateInstance(
                    template,
                    this,
                    buffSave.remainingDuration,
                    buffSave.isAuraProxy,
                    buffSave.auraSourceInstanceId,
                    false);

                instance.RemainingDuration = buffSave.remainingDuration;
                instance.NextThinkRemaining = buffSave.nextThinkRemaining;
                instance.Stacks = Math.Max(1, buffSave.stacks);
            }

            ClampResources();
        }

        private float CalculateDerivedAttribute(string attributeType)
        {
            AttributeFormulaConfig formula = _world.Database.AttributeFormula;
            float strength = GetAttribute(CombatAttributeNames.Strength);
            float agility = GetAttribute(CombatAttributeNames.Agility);
            float intelligence = GetAttribute(CombatAttributeNames.Intelligence);
            float baseValue = GetBaseAttribute(attributeType);

            if (EqualsAttribute(attributeType, CombatAttributeNames.MaxHp))
            {
                baseValue += strength * formula.StrengthToMaxHp;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.HpRegen))
            {
                baseValue += strength * formula.StrengthToHpRegen;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.MaxMana))
            {
                baseValue += intelligence * formula.IntelligenceToMaxMana;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.ManaRegen))
            {
                baseValue += intelligence * formula.IntelligenceToManaRegen;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.Armor))
            {
                baseValue += agility * formula.AgilityToArmor;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.AttackSpeed))
            {
                baseValue += agility * formula.AgilityToAttackSpeed;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.MagicResistance))
            {
                baseValue += intelligence * formula.IntelligenceToMagicResistance;
            }
            else if (EqualsAttribute(attributeType, CombatAttributeNames.AttackDamage))
            {
                switch (PrimaryAttribute)
                {
                    case PrimaryAttributeType.Strength:
                        baseValue += strength * formula.PrimaryAttributeToAttackDamage;
                        break;
                    case PrimaryAttributeType.Agility:
                        baseValue += agility * formula.PrimaryAttributeToAttackDamage;
                        break;
                    case PrimaryAttributeType.Intelligence:
                        baseValue += intelligence * formula.PrimaryAttributeToAttackDamage;
                        break;
                    case PrimaryAttributeType.Universal:
                        baseValue += (strength + agility + intelligence) *
                                     formula.UniversalAttributeToAttackDamage;
                        break;
                }
            }

            return CalculateModifiedAttribute(attributeType, baseValue);
        }

        private float CalculateModifiedAttribute(string attributeType, float baseValue)
        {
            AttributeAccumulator accumulator = new AttributeAccumulator();
            for (int buffIndex = 0; buffIndex < _activeBuffs.Count; buffIndex++)
            {
                BuffInstance instance = _activeBuffs[buffIndex];
                if (!ShouldContribute(instance))
                {
                    continue;
                }

                List<BuffAttributeModifier> modifiers = instance.Template.AttributeModifiers;
                for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    BuffAttributeModifier modifier = modifiers[modifierIndex];
                    if (modifier != null && EqualsAttribute(modifier.AttributeType, attributeType))
                    {
                        float value = modifier.Value * (modifier.ScaleByStacks ? instance.Stacks : 1);
                        accumulator.Add(modifier.Op, value, modifier.Priority);
                    }
                }

                List<BuffEffectAction> actions = instance.Template.EffectActions;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    BuffEffectAction action = actions[actionIndex];
                    if (action != null && action.ActionType == BuffEffectActionType.ModifyAttribute &&
                        IsPersistentAttributeTrigger(action.Trigger) &&
                        EqualsAttribute(action.AttributeType, attributeType))
                    {
                        float value = action.Value * (action.ScaleByStacks ? instance.Stacks : 1);
                        accumulator.Add(BuffAttributeOp.Add, value, 0);
                    }
                }
            }

            return accumulator.Apply(baseValue);
        }

        private BuffInstance CreateInstance(
            BuffTemplate template,
            BuffUnit source,
            float duration,
            bool isAuraProxy,
            string auraSourceInstanceId,
            bool executeCreatedActions)
        {
            BuffInstance instance = _world.InstancePool.Rent(
                template,
                this,
                source,
                duration,
                isAuraProxy,
                auraSourceInstanceId);
            _activeBuffs.Add(instance);

            if (!isAuraProxy)
            {
                _activeBuffIndex[template.Id] = instance;
            }

            _world.RaiseEvent(new BuffRuntimeEvent(
                BuffRuntimeEventType.BuffApplied,
                this,
                source,
                instance));

            if (executeCreatedActions && !isAuraProxy)
            {
                _world.ExecuteActions(instance, BuffEffectTrigger.OnCreated, this);
            }

            ClampResources();
            return instance;
        }

        private void RemoveInstance(BuffInstance instance, BuffRemovalReason reason)
        {
            if (instance == null || instance.IsRemoved || instance.IsRemoving)
            {
                return;
            }

            instance.IsRemoving = true;

            if (!instance.IsAuraProxy)
            {
                string templateId = instance.Template.Id;
                if (_activeBuffIndex.TryGetValue(templateId, out BuffInstance indexed) &&
                    ReferenceEquals(indexed, instance))
                {
                    _activeBuffIndex.Remove(templateId);
                }

                _world.ExecuteActions(instance, BuffEffectTrigger.OnDestroy, this);
            }

            instance.IsRemoved = true;
            _activeBuffs.Remove(instance);

            _world.RaiseEvent(new BuffRuntimeEvent(
                BuffRuntimeEventType.BuffRemoved,
                this,
                instance.Source,
                instance,
                removalReason: reason));

            instance.IsRemoving = false;
            _world.InstancePool.Return(instance);
            ClampResources();
        }

        private void Die(BuffUnit source)
        {
            CurrentHp = 0f;
            _world.RaiseEvent(new BuffRuntimeEvent(BuffRuntimeEventType.UnitDied, this, source));

            List<BuffInstance> snapshot = new List<BuffInstance>(_activeBuffs);
            for (int index = 0; index < snapshot.Count; index++)
            {
                BuffInstance instance = snapshot[index];
                if (instance.Template.RemoveOnDeath)
                {
                    RemoveInstance(instance, BuffRemovalReason.Death);
                }
            }
        }

        private float CalculateDuration(BuffTemplate template)
        {
            if (template.IsPermanent)
            {
                return -1f;
            }

            float duration = Math.Max(0f, template.Duration);
            if (template.AffectedByStatusResistance)
            {
                duration *= 1f - NormalizeRatio(GetAttribute(CombatAttributeNames.StatusResistance), BuffConfig.StatusResistanceCap);
            }

            return duration;
        }

        private bool CheckApplyConditions(BuffTemplate template)
        {
            if (template.ApplyChance < 1f)
            {
                float roll = UnityEngine.Random.value;
                if (roll > template.ApplyChance)
                {
                    return false;
                }
            }

            for (int index = 0; index < template.RequiredStatusEffects.Count; index++)
            {
                if (!HasStatusEffect(template.RequiredStatusEffects[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private BuffInstance FindFirstActiveInstance(string templateId, bool includeAuraProxies)
        {
            if (_activeBuffIndex.TryGetValue(templateId, out BuffInstance instance) &&
                !instance.IsRemoved &&
                (includeAuraProxies || !instance.IsAuraProxy))
            {
                return instance;
            }

            for (int index = 0; index < _activeBuffs.Count; index++)
            {
                instance = _activeBuffs[index];
                if (!instance.IsRemoved && (includeAuraProxies || !instance.IsAuraProxy) &&
                    string.Equals(instance.Template.Id, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return instance;
                }
            }

            return null;
        }

        private bool HasAnyStatusEffect(params string[] statuses)
        {
            for (int index = 0; index < statuses.Length; index++)
            {
                if (HasStatusEffect(statuses[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClampResources()
        {
            CurrentHp = Clamp(CurrentHp, 0f, Math.Max(0f, GetAttribute(CombatAttributeNames.MaxHp)));
            CurrentMana = Clamp(CurrentMana, 0f, Math.Max(0f, GetAttribute(CombatAttributeNames.MaxMana)));
        }

        private void CopyActiveBuffsToSnapshot()
        {
            _iterationSnapshot.Clear();
            _iterationSnapshot.AddRange(_activeBuffs);
        }

        private static bool ShouldContribute(BuffInstance instance)
        {
            return instance != null && !instance.IsRemoved &&
                   (!instance.Template.IsAura || instance.IsAuraProxy);
        }

        private static bool IsPersistentAttributeTrigger(BuffEffectTrigger trigger)
        {
            return trigger == BuffEffectTrigger.OnCreated ||
                   trigger == BuffEffectTrigger.OnRefresh ||
                   trigger == BuffEffectTrigger.OnStackChanged;
        }

        private static bool SameTemplate(BuffTemplate left, BuffTemplate right)
        {
            return left != null && right != null &&
                   string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsAttribute(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static float NormalizeRatio(float value, float maxValue = 1f)
        {
            float ratio = Math.Abs(value) > 1f ? value / 100f : value;
            return Clamp(ratio, -1f, maxValue);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private struct AttributeAccumulator
        {
            private float _add;
            private float _percentAdd;
            private float _percentMultiply;
            private bool _hasPercentMultiply;
            private bool _hasOverride;
            private int _overridePriority;
            private float _overrideValue;
            private bool _hasMin;
            private float _minValue;
            private bool _hasMax;
            private float _maxValue;

            public void Add(BuffAttributeOp op, float value, int priority)
            {
                switch (op)
                {
                    case BuffAttributeOp.Add:
                        _add += value;
                        break;
                    case BuffAttributeOp.PercentAdd:
                        _percentAdd += value;
                        break;
                    case BuffAttributeOp.PercentMultiply:
                        _percentMultiply = !_hasPercentMultiply
                            ? 1f + value / 100f
                            : _percentMultiply * (1f + value / 100f);
                        _hasPercentMultiply = true;
                        break;
                    case BuffAttributeOp.Override:
                        if (!_hasOverride || priority >= _overridePriority)
                        {
                            _hasOverride = true;
                            _overridePriority = priority;
                            _overrideValue = value;
                        }
                        break;
                    case BuffAttributeOp.Min:
                        if (!_hasMin || value > _minValue)
                        {
                            _hasMin = true;
                            _minValue = value;
                        }
                        break;
                    case BuffAttributeOp.Max:
                        if (!_hasMax || value < _maxValue)
                        {
                            _hasMax = true;
                            _maxValue = value;
                        }
                        break;
                }
            }

            public float Apply(float baseValue)
            {
                float result = (baseValue + _add) * (1f + _percentAdd / 100f);
                if (_hasPercentMultiply)
                {
                    result *= _percentMultiply;
                }

                if (_hasOverride)
                {
                    result = _overrideValue;
                }

                if (_hasMin)
                {
                    result = Math.Max(result, _minValue);
                }

                if (_hasMax)
                {
                    result = Math.Min(result, _maxValue);
                }

                return result;
            }
        }
    }
}
