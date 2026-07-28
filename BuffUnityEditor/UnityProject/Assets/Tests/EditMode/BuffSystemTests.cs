using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Buffs.Tests
{
    public sealed class BuffSystemTests
    {
        [Test]
        public void CurrentWebExport_CanBeLoaded()
        {
            string path = Path.Combine(Application.dataPath, "AssetRaw/Configs/buff_system_data.json");
            BuffConfigDatabase database = BuffConfigDatabase.FromJson(File.ReadAllText(path));

            Assert.AreEqual("2.0.0", database.Data.SchemaVersion);
            Assert.GreaterOrEqual(database.Templates.Count, 15);
            Assert.IsTrue(database.TryGet("PoisonDoT", out BuffTemplate poison));
            Assert.AreEqual(BuffStackPolicy.Stack, poison.StackPolicy);
            Assert.IsTrue(database.TryGet("demo-spell-shield", out _));
        }

        [Test]
        public void StackBuff_ModifiesAttributeAndExpires()
        {
            BuffTemplate stackBuff = CreateTemplate("stack", duration: 1f, BuffStackPolicy.Stack);
            stackBuff.MaxStacks = 3;
            stackBuff.AttributeModifiers.Add(new BuffAttributeModifier
            {
                AttributeType = CombatAttributeNames.Strength,
                Op = BuffAttributeOp.Add,
                Value = 2f,
                ScaleByStacks = true,
            });

            using BuffWorld world = CreateWorld(stackBuff);
            BuffUnit unit = CreateUnit(world, "Hero", teamId: 1);

            unit.ApplyBuff("stack");
            BuffInstance instance = unit.ApplyBuff("stack");

            Assert.AreEqual(2, instance.Stacks);
            Assert.AreEqual(14f, unit.GetAttribute(CombatAttributeNames.Strength), 0.001f);

            world.Update(1.01f);

            Assert.AreEqual(0, unit.ActiveBuffs.Count);
            Assert.AreEqual(10f, unit.GetAttribute(CombatAttributeNames.Strength), 0.001f);
        }

        [Test]
        public void IntervalDamage_TriggersAtExpiryBoundary()
        {
            BuffTemplate poison = CreateTemplate("poison", duration: 2f, BuffStackPolicy.Refresh);
            poison.ModifierKind = BuffModifierKind.Debuff;
            poison.ThinkInterval = 1f;
            poison.EffectActions.Add(new BuffEffectAction
            {
                Trigger = BuffEffectTrigger.OnIntervalThink,
                ActionType = BuffEffectActionType.DealDamage,
                TargetSelector = BuffTargetSelector.Target,
                Value = 10f,
                DamageType = BuffDamageType.Magical,
            });

            using BuffWorld world = CreateWorld(poison);
            BuffUnit source = CreateUnit(world, "Source", teamId: 2);
            BuffUnit target = CreateUnit(world, "Target", teamId: 1);
            target.ApplyBuff("poison", source);

            world.Update(1f);
            Assert.AreEqual(90f, target.CurrentHp, 0.001f);

            world.Update(1f);
            Assert.AreEqual(80f, target.CurrentHp, 0.001f);
            Assert.AreEqual(0, target.ActiveBuffs.Count);
        }

        [Test]
        public void NestedModifier_IsRemovedWithParent()
        {
            BuffTemplate shield = CreateTemplate("shield", duration: 8f, BuffStackPolicy.Refresh);
            BuffTemplate link = CreateTemplate("link", duration: 1f, BuffStackPolicy.Refresh);
            link.EffectActions.Add(new BuffEffectAction
            {
                Trigger = BuffEffectTrigger.OnCreated,
                ActionType = BuffEffectActionType.ApplyModifier,
                TargetSelector = BuffTargetSelector.Target,
                ModifierTemplateId = shield.Id,
            });
            link.EffectActions.Add(new BuffEffectAction
            {
                Trigger = BuffEffectTrigger.OnDestroy,
                ActionType = BuffEffectActionType.RemoveModifier,
                TargetSelector = BuffTargetSelector.Target,
                ModifierTemplateId = shield.Id,
            });

            using BuffWorld world = CreateWorld(link, shield);
            BuffUnit unit = CreateUnit(world, "Hero", teamId: 1);

            unit.ApplyBuff("link");
            Assert.IsTrue(unit.HasBuff("shield"));

            world.Update(1.01f);
            Assert.IsFalse(unit.HasBuff("link"));
            Assert.IsFalse(unit.HasBuff("shield"));
        }

        [Test]
        public void StrongDispel_RemovesBasicAndStrongDebuffs()
        {
            BuffTemplate basic = CreateTemplate("basic", duration: 10f, BuffStackPolicy.Refresh);
            basic.ModifierKind = BuffModifierKind.Debuff;
            basic.DispelRule = BuffDispelRule.BasicDispellable;

            BuffTemplate strong = CreateTemplate("strong", duration: 10f, BuffStackPolicy.Refresh);
            strong.ModifierKind = BuffModifierKind.Debuff;
            strong.DispelRule = BuffDispelRule.StrongDispellable;

            using BuffWorld world = CreateWorld(basic, strong);
            BuffUnit unit = CreateUnit(world, "Hero", teamId: 1);
            unit.ApplyBuff("basic");
            unit.ApplyBuff("strong");

            Assert.AreEqual(2, unit.Dispel(BuffDispelType.Strong, BuffModifierKind.Debuff));
            Assert.AreEqual(0, unit.ActiveBuffs.Count);
        }

        [Test]
        public void Aura_AppliesToAllyAndLingersAfterLeavingRange()
        {
            BuffTemplate aura = CreateTemplate("aura", duration: -1f, BuffStackPolicy.Refresh);
            aura.IsPassive = true;
            aura.IsAura = true;
            aura.AuraRadius = 5f;
            aura.LingerDuration = 0.2f;
            aura.AttributeModifiers.Add(new BuffAttributeModifier
            {
                AttributeType = CombatAttributeNames.Armor,
                Op = BuffAttributeOp.Add,
                Value = 5f,
            });

            using BuffWorld world = CreateWorld(aura);
            BuffUnit source = CreateUnit(world, "Source", teamId: 1);
            BuffUnit ally = CreateUnit(world, "Ally", teamId: 1);
            source.Position = Vector3.zero;
            ally.Position = new Vector3(2f, 0f, 0f);
            source.ApplyBuff("aura");

            world.Update(0.1f);
            Assert.AreEqual(5f, ally.GetAttribute(CombatAttributeNames.Armor), 0.001f);

            ally.Position = new Vector3(20f, 0f, 0f);
            world.Update(0.1f);
            Assert.AreEqual(5f, ally.GetAttribute(CombatAttributeNames.Armor), 0.001f);

            world.Update(0.11f);
            Assert.AreEqual(0f, ally.GetAttribute(CombatAttributeNames.Armor), 0.001f);
        }

        private static BuffWorld CreateWorld(params BuffTemplate[] templates)
        {
            BuffSystemData data = new BuffSystemData
            {
                SchemaVersion = BuffSchema.CurrentVersion,
                AttributeFormula = new AttributeFormulaConfig
                {
                    StrengthToMaxHp = 0f,
                    StrengthToHpRegen = 0f,
                    AgilityToArmor = 0f,
                    AgilityToAttackSpeed = 0f,
                    IntelligenceToMaxMana = 0f,
                    IntelligenceToManaRegen = 0f,
                    IntelligenceToMagicResistance = 0f,
                    PrimaryAttributeToAttackDamage = 0f,
                    UniversalAttributeToAttackDamage = 0f,
                    MinAttackSpeed = 0f,
                    MaxAttackSpeed = 1000f,
                    PhysicalArmorFactor = 0.06f,
                },
                Buffs = new List<BuffTemplate>(templates),
            };
            return new BuffWorld(new BuffConfigDatabase(data));
        }

        private static BuffUnit CreateUnit(BuffWorld world, string id, int teamId)
        {
            return world.CreateUnit(
                id,
                PrimaryAttributeType.Strength,
                teamId,
                new Dictionary<string, float>
                {
                    [CombatAttributeNames.Strength] = 10f,
                    [CombatAttributeNames.Agility] = 10f,
                    [CombatAttributeNames.Intelligence] = 10f,
                    [CombatAttributeNames.MaxHp] = 100f,
                    [CombatAttributeNames.MaxMana] = 50f,
                    [CombatAttributeNames.AttackSpeed] = 100f,
                });
        }

        private static BuffTemplate CreateTemplate(string id, float duration, BuffStackPolicy stackPolicy)
        {
            return new BuffTemplate
            {
                Id = id,
                Key = id,
                DisplayName = id,
                Duration = duration,
                StackPolicy = stackPolicy,
                MaxStacks = 1,
                ThinkInterval = -1f,
                DispelRule = BuffDispelRule.BasicDispellable,
                RemoveOnDeath = true,
            };
        }
    }
}
