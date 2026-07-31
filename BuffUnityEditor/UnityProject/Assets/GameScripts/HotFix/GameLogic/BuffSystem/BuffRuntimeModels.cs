using System;
using TEngine;
using GameplayTags;

namespace GameLogic.Buffs
{
    public enum BuffRemovalReason
    {
        Expired,
        Manual,
        Dispelled,
        Replaced,
        Death,
        AuraLost,
        WorldCleared,
    }

    public enum BuffRuntimeEventType
    {
        BuffApplied,
        BuffRefreshed,
        BuffStackChanged,
        BuffRemoved,
        DamageTaken,
        Healed,
        UnitDied,
        UnitRevived,
        BaseAttributeChanged,
    }

    public static class BuffEventIds
    {
        public static readonly int AnyChanged = RuntimeId.ToRuntimeId("GameLogic.Buffs.AnyChanged");
        public static readonly int BuffApplied = RuntimeId.ToRuntimeId("GameLogic.Buffs.BuffApplied");
        public static readonly int BuffRefreshed = RuntimeId.ToRuntimeId("GameLogic.Buffs.BuffRefreshed");
        public static readonly int BuffStackChanged = RuntimeId.ToRuntimeId("GameLogic.Buffs.BuffStackChanged");
        public static readonly int BuffRemoved = RuntimeId.ToRuntimeId("GameLogic.Buffs.BuffRemoved");
        public static readonly int DamageTaken = RuntimeId.ToRuntimeId("GameLogic.Buffs.DamageTaken");
        public static readonly int Healed = RuntimeId.ToRuntimeId("GameLogic.Buffs.Healed");
        public static readonly int UnitDied = RuntimeId.ToRuntimeId("GameLogic.Buffs.UnitDied");
        public static readonly int UnitRevived = RuntimeId.ToRuntimeId("GameLogic.Buffs.UnitRevived");
        public static readonly int BaseAttributeChanged = RuntimeId.ToRuntimeId("GameLogic.Buffs.BaseAttributeChanged");

        public static int GetEventId(BuffRuntimeEventType eventType)
        {
            switch (eventType)
            {
                case BuffRuntimeEventType.BuffApplied:
                    return BuffApplied;
                case BuffRuntimeEventType.BuffRefreshed:
                    return BuffRefreshed;
                case BuffRuntimeEventType.BuffStackChanged:
                    return BuffStackChanged;
                case BuffRuntimeEventType.BuffRemoved:
                    return BuffRemoved;
                case BuffRuntimeEventType.DamageTaken:
                    return DamageTaken;
                case BuffRuntimeEventType.Healed:
                    return Healed;
                case BuffRuntimeEventType.UnitDied:
                    return UnitDied;
                case BuffRuntimeEventType.UnitRevived:
                    return UnitRevived;
                case BuffRuntimeEventType.BaseAttributeChanged:
                    return BaseAttributeChanged;
                default:
                    return AnyChanged;
            }
        }
    }

    public sealed class BuffRuntimeEvent
    {
        public BuffRuntimeEventType EventType { get; }
        public BuffUnit Unit { get; }
        public BuffUnit Source { get; }
        public BuffInstance Instance { get; }
        public BuffRemovalReason? RemovalReason { get; }
        public BuffDamageType? DamageType { get; }
        public float Value { get; }
        public string AttributeType { get; }

        public BuffRuntimeEvent(
            BuffRuntimeEventType eventType,
            BuffUnit unit,
            BuffUnit source = null,
            BuffInstance instance = null,
            float value = 0f,
            BuffDamageType? damageType = null,
            BuffRemovalReason? removalReason = null,
            string attributeType = "")
        {
            EventType = eventType;
            Unit = unit;
            Source = source;
            Instance = instance;
            Value = value;
            DamageType = damageType;
            RemovalReason = removalReason;
            AttributeType = attributeType ?? string.Empty;
        }
    }

    public sealed class BuffDamageResult
    {
        public float RawDamage { get; internal set; }
        public float ActualDamage { get; internal set; }
        public BuffDamageType DamageType { get; internal set; }
        public bool WasBlocked { get; internal set; }
        public bool KilledTarget { get; internal set; }
    }

    public sealed class BuffInstance
    {
        public string InstanceId { get; }
        public BuffTemplate Template { get; }
        public BuffUnit Owner { get; }
        public BuffUnit Source { get; }
        public int Stacks { get; internal set; }
        public float AppliedDuration { get; internal set; }
        public float RemainingDuration { get; internal set; }
        public float NextThinkRemaining { get; internal set; }
        public bool IsAuraProxy { get; }
        public string AuraSourceInstanceId { get; }
        public GameplayTagContainer GameplayTags { get; }

        public bool IsPermanent => AppliedDuration < 0f;
        public bool IsRemoved { get; internal set; }

        public float RemainingRatio
        {
            get
            {
                if (IsPermanent || AppliedDuration <= 0f)
                {
                    return 1f;
                }

                return Clamp01(RemainingDuration / AppliedDuration);
            }
        }

        internal bool IsRemoving { get; set; }

        internal BuffInstance(
            BuffTemplate template,
            BuffUnit owner,
            BuffUnit source,
            float duration,
            bool isAuraProxy,
            string auraSourceInstanceId)
        {
            InstanceId = Guid.NewGuid().ToString("N");
            Template = template;
            Owner = owner;
            Source = source ?? owner;
            Stacks = 1;
            IsAuraProxy = isAuraProxy;
            AuraSourceInstanceId = auraSourceInstanceId ?? string.Empty;
            GameplayTags = template.GameplayTags;
            ResetDuration(duration);
        }

        internal void ResetDuration(float duration)
        {
            AppliedDuration = duration < 0f ? -1f : Math.Max(0f, duration);
            RemainingDuration = AppliedDuration;
            NextThinkRemaining = Template.ThinkInterval > 0f ? Template.ThinkInterval : -1f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
