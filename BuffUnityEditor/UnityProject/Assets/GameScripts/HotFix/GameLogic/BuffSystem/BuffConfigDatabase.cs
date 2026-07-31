using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using GameplayTags;

namespace GameLogic.Buffs
{
    public sealed class BuffConfigurationException : Exception
    {
        public BuffConfigurationException(string message) : base(message)
        {
        }

        public BuffConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class BuffConfigDatabase
    {
        private readonly Dictionary<string, BuffTemplate> _byId =
            new Dictionary<string, BuffTemplate>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, BuffTemplate> _byKey =
            new Dictionary<string, BuffTemplate>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _warnings = new List<string>();

        public BuffSystemData Data { get; }
        public AttributeFormulaConfig AttributeFormula => Data.AttributeFormula;
        public IReadOnlyList<BuffTemplate> Templates => Data.Buffs;
        public IReadOnlyList<string> Warnings => _warnings;

        public BuffConfigDatabase(BuffSystemData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            NormalizeAndBuildIndices();
        }

        public static BuffConfigDatabase FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new BuffConfigurationException("Buff 配置 JSON 为空。");
            }

            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                };
                settings.Converters.Add(new StringEnumConverter());

                BuffSystemData data = JsonConvert.DeserializeObject<BuffSystemData>(json, settings);
                if (data == null)
                {
                    throw new BuffConfigurationException("Buff 配置 JSON 无法反序列化。");
                }

                return new BuffConfigDatabase(data);
            }
            catch (BuffConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BuffConfigurationException("Buff 配置 JSON 格式不正确。", exception);
            }
        }

        public bool TryGet(string idOrKey, out BuffTemplate template)
        {
            template = null;
            if (string.IsNullOrWhiteSpace(idOrKey))
            {
                return false;
            }

            return _byId.TryGetValue(idOrKey, out template) || _byKey.TryGetValue(idOrKey, out template);
        }

        public BuffTemplate GetRequired(string idOrKey)
        {
            if (TryGet(idOrKey, out BuffTemplate template))
            {
                return template;
            }

            throw new KeyNotFoundException($"未找到 Buff 配置：{idOrKey}");
        }

        private void NormalizeAndBuildIndices()
        {
            if (string.IsNullOrWhiteSpace(Data.SchemaVersion))
            {
                throw new BuffConfigurationException("缺少 schemaVersion。");
            }

            if (!Data.SchemaVersion.StartsWith("2.", StringComparison.Ordinal))
            {
                throw new BuffConfigurationException(
                    $"不支持的 Buff Schema 版本：{Data.SchemaVersion}，当前运行时支持 2.x。");
            }

            if (!string.Equals(Data.SchemaVersion, BuffSchema.CurrentVersion, StringComparison.Ordinal))
            {
                _warnings.Add(
                    $"配置版本为 {Data.SchemaVersion}，运行时版本为 {BuffSchema.CurrentVersion}，将按 2.x 兼容模式读取。");
            }

            Data.AttributeFormula ??= new AttributeFormulaConfig();
            Data.Buffs ??= new List<BuffTemplate>();

            for (int index = 0; index < Data.Buffs.Count; index++)
            {
                BuffTemplate template = Data.Buffs[index];
                if (template == null)
                {
                    throw new BuffConfigurationException($"buffs[{index}] 为空。");
                }

                NormalizeTemplate(template, index);

                if (_byId.ContainsKey(template.Id))
                {
                    throw new BuffConfigurationException($"Buff ID 重复：{template.Id}");
                }

                if (_byKey.ContainsKey(template.Key))
                {
                    throw new BuffConfigurationException($"Buff Key 重复：{template.Key}");
                }

                _byId.Add(template.Id, template);
                _byKey.Add(template.Key, template);
            }

            ValidateModifierReferences();
            ResolveGameplayTags();
        }

        private static void NormalizeTemplate(BuffTemplate template, int index)
        {
            if (string.IsNullOrWhiteSpace(template.Id))
            {
                throw new BuffConfigurationException($"buffs[{index}] 缺少 id。");
            }

            if (string.IsNullOrWhiteSpace(template.Key))
            {
                throw new BuffConfigurationException($"Buff {template.Id} 缺少 key。");
            }

            template.MaxStacks = Math.Max(1, template.MaxStacks);
            template.AuraRadius = Math.Max(0f, template.AuraRadius);
            template.LingerDuration = Math.Max(0f, template.LingerDuration);
            template.StatusEffects ??= new List<string>();
            template.AttributeModifiers ??= new List<BuffAttributeModifier>();
            template.EffectActions ??= new List<BuffEffectAction>();
            template.Tags ??= new List<string>();

            for (int modifierIndex = 0; modifierIndex < template.AttributeModifiers.Count; modifierIndex++)
            {
                BuffAttributeModifier modifier = template.AttributeModifiers[modifierIndex];
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.AttributeType))
                {
                    throw new BuffConfigurationException(
                        $"Buff {template.Key} 的属性修改器 [{modifierIndex}] 缺少 attributeType。");
                }
            }
        }

        private void ValidateModifierReferences()
        {
            for (int buffIndex = 0; buffIndex < Data.Buffs.Count; buffIndex++)
            {
                BuffTemplate template = Data.Buffs[buffIndex];
                for (int actionIndex = 0; actionIndex < template.EffectActions.Count; actionIndex++)
                {
                    BuffEffectAction action = template.EffectActions[actionIndex];
                    if (action == null)
                    {
                        _warnings.Add($"Buff {template.Key} 的效果动作 [{actionIndex}] 为空，运行时会忽略。");
                        continue;
                    }

                    bool needsModifier = action.ActionType == BuffEffectActionType.ApplyModifier ||
                                         action.ActionType == BuffEffectActionType.RemoveModifier;
                    if (!needsModifier)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(action.ModifierTemplateId))
                    {
                        _warnings.Add($"Buff {template.Key} 的动作 {action.Id} 缺少 modifierTemplateId。");
                        continue;
                    }

                    if (!TryGet(action.ModifierTemplateId, out _))
                    {
                        _warnings.Add(
                            $"Buff {template.Key} 的动作 {action.Id} 引用了不存在的效果：{action.ModifierTemplateId}");
                    }
                }
            }
        }

        private void ResolveGameplayTags()
        {
            GameplayTagManager.InitializeIfNeeded();
            for (int buffIndex = 0; buffIndex < Data.Buffs.Count; buffIndex++)
            {
                BuffTemplate template = Data.Buffs[buffIndex];
                GameplayTagContainer container = new GameplayTagContainer();
                for (int tagIndex = 0; tagIndex < template.Tags.Count; tagIndex++)
                {
                    string tagName = template.Tags[tagIndex];
                    if (string.IsNullOrWhiteSpace(tagName))
                    {
                        continue;
                    }

                    if (!GameplayTagManager.RequestTag(tagName, out GameplayTag tag))
                    {
                        throw new BuffConfigurationException(
                            $"Buff {template.Key} 引用了不存在的 GameplayTag：{tagName}");
                    }

                    container.AddTag(tag);
                }
                template.GameplayTags = container;
            }
        }
    }
}
