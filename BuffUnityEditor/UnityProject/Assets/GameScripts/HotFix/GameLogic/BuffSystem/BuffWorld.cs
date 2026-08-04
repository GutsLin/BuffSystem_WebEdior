using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Buffs
{
    public sealed class BuffWorld : IDisposable
    {
        private readonly Dictionary<string, BuffUnit> _units =
            new Dictionary<string, BuffUnit>(StringComparer.OrdinalIgnoreCase);

        private readonly List<BuffUnit> _unitSnapshot = new List<BuffUnit>();
        private readonly List<BuffInstance> _auraSnapshot = new List<BuffInstance>();
        private float _auraRefreshElapsed;
        private int _actionDepth;

        public BuffConfigDatabase Database { get; }
        public IReadOnlyCollection<BuffUnit> Units => _units.Values;
        public float AuraRefreshInterval { get; set; } = BuffConfig.AuraRefreshInterval;
        public Func<BuffUnit, BuffUnit, bool> AuraTargetFilter { get; set; }
        internal BuffInstancePool InstancePool { get; } = new BuffInstancePool();

        public event Action<BuffRuntimeEvent> EventRaised;

        public BuffWorld(BuffConfigDatabase database)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            AuraTargetFilter = DefaultAuraTargetFilter;
        }

        public BuffUnit CreateUnit(
            string id,
            PrimaryAttributeType? primaryAttribute = null,
            int teamId = 0,
            IDictionary<string, float> baseAttributes = null)
        {
            string unitId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            if (_units.ContainsKey(unitId))
            {
                throw new InvalidOperationException($"BuffUnit ID 已存在：{unitId}");
            }

            BuffUnit unit = new BuffUnit(
                this,
                unitId,
                primaryAttribute ?? Database.AttributeFormula.PrimaryAttributeDefault,
                teamId,
                baseAttributes);
            _units.Add(unitId, unit);
            return unit;
        }

        public bool TryGetUnit(string id, out BuffUnit unit)
        {
            unit = null;
            return !string.IsNullOrWhiteSpace(id) && _units.TryGetValue(id, out unit);
        }

        public bool RemoveUnit(string id)
        {
            if (!TryGetUnit(id, out BuffUnit unit))
            {
                return false;
            }

            unit.Clear(BuffRemovalReason.WorldCleared);
            return _units.Remove(id);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            CopyUnitsToSnapshot();
            for (int index = 0; index < _unitSnapshot.Count; index++)
            {
                _unitSnapshot[index].Tick(deltaTime);
            }

            _auraRefreshElapsed += deltaTime;
            if (_auraRefreshElapsed >= Math.Max(BuffConfig.MinAuraRefreshInterval, AuraRefreshInterval))
            {
                _auraRefreshElapsed = 0f;
                RefreshAuras();
            }
        }

        public void Clear()
        {
            CopyUnitsToSnapshot();
            for (int index = 0; index < _unitSnapshot.Count; index++)
            {
                _unitSnapshot[index].Clear(BuffRemovalReason.WorldCleared);
            }

            _units.Clear();
            _auraSnapshot.Clear();
            _unitSnapshot.Clear();
        }

        public void Dispose()
        {
            Clear();
            InstancePool.Clear();
            EventRaised = null;
        }

        internal void ExecuteActions(
            BuffInstance instance,
            BuffEffectTrigger trigger,
            BuffUnit eventTarget)
        {
            if (instance == null || instance.IsRemoved || instance.IsAuraProxy || _actionDepth >= BuffConfig.MaxActionDepth)
            {
                return;
            }

            _actionDepth++;
            try
            {
                List<BuffEffectAction> actions = instance.Template.EffectActions;
                for (int index = 0; index < actions.Count; index++)
                {
                    BuffEffectAction action = actions[index];
                    if (action == null || action.Trigger != trigger)
                    {
                        continue;
                    }

                    if (!IsConditionMet(action, instance, eventTarget))
                    {
                        continue;
                    }

                    if (action.TargetSelector == BuffTargetSelector.AuraTargets)
                    {
                        List<BuffUnit> targets = new List<BuffUnit>();
                        CollectAuraTargets(instance, targets);
                        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                        {
                            ExecuteAction(instance, action, targets[targetIndex], trigger);
                        }
                    }
                    else
                    {
                        BuffUnit target = ResolveTarget(instance, action.TargetSelector, eventTarget);
                        ExecuteAction(instance, action, target, trigger);
                    }
                }
            }
            finally
            {
                _actionDepth--;
            }
        }

        internal void Trigger(BuffUnit unit, BuffEffectTrigger trigger, BuffUnit eventTarget)
        {
            if (unit == null)
            {
                return;
            }

            List<BuffInstance> snapshot = new List<BuffInstance>(unit.ActiveBuffs);
            for (int index = 0; index < snapshot.Count; index++)
            {
                BuffInstance instance = snapshot[index];
                if (!instance.IsRemoved && !instance.IsAuraProxy)
                {
                    ExecuteActions(instance, trigger, eventTarget ?? unit);
                }
            }
        }

        internal void RaiseEvent(BuffRuntimeEvent runtimeEvent)
        {
            EventRaised?.Invoke(runtimeEvent);
        }

        public void TriggerCustomEvent(BuffUnit unit, string eventName, BuffUnit eventTarget = null)
        {
            if (unit == null || string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            List<BuffInstance> snapshot = new List<BuffInstance>(unit.ActiveBuffs);
            for (int index = 0; index < snapshot.Count; index++)
            {
                BuffInstance instance = snapshot[index];
                if (instance.IsRemoved || instance.IsAuraProxy)
                {
                    continue;
                }

                List<BuffEffectAction> actions = instance.Template.EffectActions;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    BuffEffectAction action = actions[actionIndex];
                    if (action != null &&
                        action.Trigger == BuffEffectTrigger.OnCustomEvent &&
                        string.Equals(action.EventName, eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        ExecuteActionDirect(instance, action, eventTarget ?? unit);
                    }
                }
            }
        }

        private static bool IsConditionMet(
            BuffEffectAction action,
            BuffInstance instance,
            BuffUnit eventTarget)
        {
            BuffEffectCondition condition = action.Condition;
            if (condition == null)
            {
                return true;
            }

            BuffUnit checkTarget = action.TargetSelector == BuffTargetSelector.Self
                ? instance.Owner
                : eventTarget ?? instance.Owner;

            if (checkTarget == null)
            {
                return false;
            }

            if (condition.HealthPercentMin.HasValue || condition.HealthPercentMax.HasValue)
            {
                float maxHp = checkTarget.GetAttribute(CombatAttributeNames.MaxHp);
                if (maxHp <= 0f)
                {
                    return false;
                }

                float healthPercent = checkTarget.CurrentHp / maxHp * 100f;
                if (condition.HealthPercentMin.HasValue && healthPercent < condition.HealthPercentMin.Value)
                {
                    return false;
                }

                if (condition.HealthPercentMax.HasValue && healthPercent > condition.HealthPercentMax.Value)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(condition.RequiredStatusEffect) &&
                !checkTarget.HasStatusEffect(condition.RequiredStatusEffect))
            {
                return false;
            }

            return true;
        }

        private void ExecuteActionDirect(
            BuffInstance instance,
            BuffEffectAction action,
            BuffUnit eventTarget)
        {
            if (_actionDepth >= BuffConfig.MaxActionDepth)
            {
                return;
            }

            _actionDepth++;
            try
            {
                ExecuteAction(instance, action,
                    ResolveTarget(instance, action.TargetSelector, eventTarget),
                    action.Trigger);
            }
            finally
            {
                _actionDepth--;
            }
        }

        private void ExecuteAction(
            BuffInstance instance,
            BuffEffectAction action,
            BuffUnit target,
            BuffEffectTrigger trigger)
        {
            if (target == null)
            {
                return;
            }

            float value = action.Value * (action.ScaleByStacks ? instance.Stacks : 1);
            BuffUnit source = instance.Source ?? instance.Owner;

            switch (action.ActionType)
            {
                case BuffEffectActionType.DealDamage:
                    if (source != null && action.DamageType != BuffDamageType.HpRemoval)
                    {
                        value *= 1f + source.GetAttribute(CombatAttributeNames.SpellAmplification) / 100f;
                    }

                    target.TakeDamage(value, action.DamageType ?? BuffDamageType.Magical, source);
                    break;

                case BuffEffectActionType.Heal:
                    target.Heal(value, source);
                    break;

                case BuffEffectActionType.ModifyAttribute:
                    if (!IsPersistentAttributeTrigger(trigger) && !string.IsNullOrWhiteSpace(action.AttributeType))
                    {
                        target.AddBaseAttribute(action.AttributeType, value);
                    }

                    break;

                case BuffEffectActionType.ApplyModifier:
                    if (!string.IsNullOrWhiteSpace(action.ModifierTemplateId) &&
                        Database.TryGet(action.ModifierTemplateId, out BuffTemplate applyTemplate))
                    {
                        target.ApplyBuff(applyTemplate, source);
                    }

                    break;

                case BuffEffectActionType.RemoveModifier:
                    if (!string.IsNullOrWhiteSpace(action.ModifierTemplateId))
                    {
                        target.RemoveBuff(action.ModifierTemplateId, BuffRemovalReason.Manual);
                    }

                    break;

                case BuffEffectActionType.RefreshModifier:
                    if (!string.IsNullOrWhiteSpace(action.ModifierTemplateId) &&
                        Database.TryGet(action.ModifierTemplateId, out BuffTemplate refreshTemplate))
                    {
                        target.ApplyBuff(refreshTemplate, source);
                    }

                    break;

                case BuffEffectActionType.Dispel:
                    target.Dispel(action.DispelType ?? BuffDispelType.Basic, BuffModifierKind.Debuff);
                    break;
            }
        }

        private void RefreshAuras()
        {
            _auraSnapshot.Clear();
            CopyUnitsToSnapshot();

            for (int unitIndex = 0; unitIndex < _unitSnapshot.Count; unitIndex++)
            {
                BuffUnit source = _unitSnapshot[unitIndex];
                if (!source.IsAlive || !source.PassivesEnabled)
                {
                    continue;
                }

                IReadOnlyList<BuffInstance> activeBuffs = source.ActiveBuffs;
                for (int buffIndex = 0; buffIndex < activeBuffs.Count; buffIndex++)
                {
                    BuffInstance instance = activeBuffs[buffIndex];
                    if (!instance.IsRemoved && !instance.IsAuraProxy && instance.Template.IsAura)
                    {
                        _auraSnapshot.Add(instance);
                    }
                }
            }

            float minimumHold = Math.Max(0.05f, AuraRefreshInterval * 2f);
            for (int auraIndex = 0; auraIndex < _auraSnapshot.Count; auraIndex++)
            {
                BuffInstance aura = _auraSnapshot[auraIndex];
                float holdDuration = Math.Max(minimumHold, aura.Template.LingerDuration);
                for (int targetIndex = 0; targetIndex < _unitSnapshot.Count; targetIndex++)
                {
                    BuffUnit target = _unitSnapshot[targetIndex];
                    if (IsValidAuraTarget(aura, target))
                    {
                        target.RefreshAuraProxy(aura, holdDuration);
                    }
                }
            }
        }

        private void CollectAuraTargets(BuffInstance instance, List<BuffUnit> output)
        {
            output.Clear();
            CopyUnitsToSnapshot();
            for (int index = 0; index < _unitSnapshot.Count; index++)
            {
                BuffUnit target = _unitSnapshot[index];
                if (IsValidAuraTarget(instance, target))
                {
                    output.Add(target);
                }
            }
        }

        private bool IsValidAuraTarget(BuffInstance aura, BuffUnit target)
        {
            if (aura == null || target == null || !target.IsAlive)
            {
                return false;
            }

            BuffUnit source = aura.Owner;
            if (source == null || !source.IsAlive || (AuraTargetFilter != null && !AuraTargetFilter(source, target)))
            {
                return false;
            }

            float radius = Math.Max(0f, aura.Template.AuraRadius);
            return (target.Position - source.Position).sqrMagnitude <= radius * radius;
        }

        private static BuffUnit ResolveTarget(
            BuffInstance instance,
            BuffTargetSelector selector,
            BuffUnit eventTarget)
        {
            switch (selector)
            {
                case BuffTargetSelector.Source:
                    return instance.Source ?? instance.Owner;
                case BuffTargetSelector.Target:
                    return eventTarget ?? instance.Owner;
                case BuffTargetSelector.Self:
                default:
                    return instance.Owner;
            }
        }

        private static bool DefaultAuraTargetFilter(BuffUnit source, BuffUnit target)
        {
            return source.TeamId == target.TeamId;
        }

        private static bool IsPersistentAttributeTrigger(BuffEffectTrigger trigger)
        {
            return trigger == BuffEffectTrigger.OnCreated ||
                   trigger == BuffEffectTrigger.OnRefresh ||
                   trigger == BuffEffectTrigger.OnStackChanged;
        }

        private void CopyUnitsToSnapshot()
        {
            _unitSnapshot.Clear();
            foreach (BuffUnit unit in _units.Values)
            {
                _unitSnapshot.Add(unit);
            }
        }
    }
}
