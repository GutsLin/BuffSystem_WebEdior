using System;
using System.Collections.Generic;
using System.Reflection;
using GameLogic.Buffs;
using GameplayTags;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;

public static class GameplayTagsConfigurationValidator
{
    private const string GameplayTagsAssetPath = "Assets/AssetRaw/Configs/gameplay_tags.json";
    private const string BuffSystemAssetPath = "Assets/AssetRaw/Configs/buff_system_data.json";

    [MenuItem("BuffWork/Gameplay Tags/Validate Configuration", false, 120)]
    private static void ValidateFromMenu()
    {
        bool valid = Validate();
        EditorUtility.DisplayDialog(
            "GameplayTags Validation",
            valid ? "GameplayTags 配置检查通过。" : "GameplayTags 配置检查失败，请查看 Console。",
            "确定");
    }

    public static bool Validate()
    {
        ValidationReport report = BuildReport();

        for (int index = 0; index < report.Warnings.Count; index++)
        {
            Debug.LogWarning($"[GameplayTags] {report.Warnings[index]}");
        }

        for (int index = 0; index < report.Errors.Count; index++)
        {
            Debug.LogError($"[GameplayTags] {report.Errors[index]}");
        }

        if (report.Errors.Count == 0)
        {
            Debug.Log(
                $"[GameplayTags] 配置检查通过：{report.ActiveTagCount} 个可用标签，{report.Warnings.Count} 个警告。");
            return true;
        }

        Debug.LogError(
            $"[GameplayTags] 配置检查失败：{report.Errors.Count} 个错误，{report.Warnings.Count} 个警告。");
        return false;
    }

    public static void ValidateForCi()
    {
        if (!Validate())
        {
            throw new InvalidOperationException("GameplayTags 配置检查失败。");
        }
    }

    private static ValidationReport BuildReport()
    {
        ValidationReport report = new ValidationReport();
        Dictionary<string, TagRecord> codeTags = new Dictionary<string, TagRecord>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, TagRecord> allTags = new Dictionary<string, TagRecord>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> deprecatedJsonTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        CollectAssemblyTags(codeTags, report);
        foreach (KeyValuePair<string, TagRecord> pair in codeTags)
        {
            allTags[pair.Key] = pair.Value;
        }

        GameplayTagsConfigFile gameplayTagsFile = LoadGameplayTagsFile(report);
        if (gameplayTagsFile != null)
        {
            ValidateJsonTags(
                gameplayTagsFile,
                codeTags,
                allTags,
                deprecatedJsonTags,
                report);
        }

        ValidateBuffReferences(allTags, deprecatedJsonTags, report);
        report.ActiveTagCount = CountActiveTags(allTags, deprecatedJsonTags);
        return report;
    }

    private static void CollectAssemblyTags(
        Dictionary<string, TagRecord> codeTags,
        ValidationReport report)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Assembly assembly = assemblies[assemblyIndex];
            object[] rawAttributes;
            try
            {
                rawAttributes = assembly.GetCustomAttributes(typeof(GameplayTagAttribute), false);
            }
            catch (Exception exception)
            {
                report.Error(
                    $"扫描程序集 {assembly.GetName().Name} 的 GameplayTagAttribute 失败：{exception.Message}");
                continue;
            }

            for (int attributeIndex = 0; attributeIndex < rawAttributes.Length; attributeIndex++)
            {
                GameplayTagAttribute attribute = (GameplayTagAttribute)rawAttributes[attributeIndex];
                if (!ValidateTagName(attribute.TagName, $"程序集 {assembly.GetName().Name}", report))
                {
                    continue;
                }

                TagRecord candidate = new TagRecord
                {
                    Name = attribute.TagName,
                    Description = attribute.Description,
                    Flags = attribute.Flags,
                    Source = $"程序集 {assembly.GetName().Name}",
                    Deprecated = false,
                };

                if (!codeTags.TryGetValue(candidate.Name, out TagRecord existing))
                {
                    codeTags.Add(candidate.Name, candidate);
                    continue;
                }

                if (existing.Flags != candidate.Flags ||
                    !string.Equals(existing.Description, candidate.Description, StringComparison.Ordinal))
                {
                    report.Error(
                        $"程序集标签同名定义冲突：{candidate.Name}（{existing.Source} 与 {candidate.Source}）。");
                }
                else
                {
                    report.Warning($"程序集标签重复声明，将合并为一个标签：{candidate.Name}。");
                }
            }
        }
    }

    private static GameplayTagsConfigFile LoadGameplayTagsFile(ValidationReport report)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GameplayTagsAssetPath);
        if (asset == null)
        {
            report.Error($"找不到 GameplayTags 配置：{GameplayTagsAssetPath}。");
            return null;
        }

        GameplayTagsConfigFile file;
        try
        {
            file = JsonConvert.DeserializeObject<GameplayTagsConfigFile>(asset.text);
        }
        catch (Exception exception)
        {
            report.Error($"GameplayTags JSON 解析失败：{exception.Message}");
            return null;
        }

        if (file == null)
        {
            report.Error("GameplayTags JSON 为空。");
            return null;
        }

        if (!string.Equals(file.SchemaVersion, GameplayTagRuntimeConfig.SchemaVersion, StringComparison.Ordinal))
        {
            report.Error(
                $"GameplayTags Schema 版本不支持：{file.SchemaVersion}，当前支持 {GameplayTagRuntimeConfig.SchemaVersion}。");
        }

        if (file.Version < GameplayTagRuntimeConfig.MinimumVersion)
        {
            report.Error($"GameplayTags 配置版本过低：{file.Version}。");
        }

        if (file.Tags == null)
        {
            report.Error("GameplayTags JSON 缺少 tags 数组。");
        }

        return file;
    }

    private static void ValidateJsonTags(
        GameplayTagsConfigFile file,
        Dictionary<string, TagRecord> codeTags,
        Dictionary<string, TagRecord> allTags,
        HashSet<string> deprecatedJsonTags,
        ValidationReport report)
    {
        if (file.Tags == null)
        {
            return;
        }

        HashSet<string> jsonNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < file.Tags.Count; index++)
        {
            GameplayTagConfigEntry entry = file.Tags[index];
            if (entry == null)
            {
                report.Error($"GameplayTags tags[{index}] 为空。");
                continue;
            }

            string location = $"JSON tags[{index}]";
            if (!ValidateTagName(entry.Name, location, report))
            {
                continue;
            }

            if (!jsonNames.Add(entry.Name))
            {
                report.Error($"JSON 内存在重复或大小写冲突的标签：{entry.Name}。");
                continue;
            }

            TagRecord jsonRecord = new TagRecord
            {
                Name = entry.Name,
                Description = entry.Description,
                Flags = entry.Flags,
                Source = location,
                Deprecated = entry.Deprecated,
            };

            if (entry.Deprecated)
            {
                deprecatedJsonTags.Add(entry.Name);
            }

            if (codeTags.TryGetValue(entry.Name, out TagRecord codeRecord))
            {
                if (entry.Deprecated)
                {
                    report.Error($"JSON 不能废弃程序集核心标签：{entry.Name}。");
                }

                if (codeRecord.Flags != entry.Flags)
                {
                    report.Error($"程序集与 JSON 标签 flags 冲突：{entry.Name}。");
                }
                else if (!string.Equals(codeRecord.Description, entry.Description, StringComparison.Ordinal))
                {
                    report.Warning(
                        $"程序集与 JSON 标签描述不同，将使用程序集定义：{entry.Name}。");
                }

                if (!string.Equals(codeRecord.Name, entry.Name, StringComparison.Ordinal))
                {
                    report.Error(
                        $"程序集与 JSON 标签名称大小写不一致：{codeRecord.Name} / {entry.Name}。");
                }

                continue;
            }

            if (allTags.TryGetValue(entry.Name, out TagRecord existing))
            {
                report.Error($"标签来源冲突：{entry.Name}（{existing.Source} 与 {location}）。");
                continue;
            }

            allTags.Add(entry.Name, jsonRecord);
        }

        foreach (KeyValuePair<string, TagRecord> pair in allTags)
        {
            string parentName = GetParentName(pair.Key);
            if (!string.IsNullOrEmpty(parentName) && !allTags.ContainsKey(parentName))
            {
                report.Warning(
                    $"标签 {pair.Key} 缺少父级 {parentName}，运行时会自动补齐父级。");
            }
        }
    }

    private static void ValidateBuffReferences(
        Dictionary<string, TagRecord> allTags,
        HashSet<string> deprecatedJsonTags,
        ValidationReport report)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(BuffSystemAssetPath);
        if (asset == null)
        {
            report.Error($"找不到 Buff 配置：{BuffSystemAssetPath}。");
            return;
        }

        BuffSystemData data;
        try
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            settings.Converters.Add(new StringEnumConverter());
            data = JsonConvert.DeserializeObject<BuffSystemData>(asset.text, settings);
        }
        catch (Exception exception)
        {
            report.Error($"Buff JSON 解析失败：{exception.Message}");
            return;
        }

        if (data == null || data.Buffs == null)
        {
            report.Error("Buff JSON 缺少 buffs 数组。");
            return;
        }

        for (int buffIndex = 0; buffIndex < data.Buffs.Count; buffIndex++)
        {
            BuffTemplate buff = data.Buffs[buffIndex];
            if (buff == null || buff.Tags == null)
            {
                continue;
            }

            for (int tagIndex = 0; tagIndex < buff.Tags.Count; tagIndex++)
            {
                string tagName = buff.Tags[tagIndex];
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    report.Warning($"Buff {buff.Key} 的 tags[{tagIndex}] 为空。");
                    continue;
                }

                if (!allTags.TryGetValue(tagName, out TagRecord tagRecord))
                {
                    report.Error($"Buff {buff.Key} 引用了不存在的 GameplayTag：{tagName}。");
                    continue;
                }

                if (!string.Equals(tagRecord.Name, tagName, StringComparison.Ordinal))
                {
                    report.Error(
                        $"Buff {buff.Key} 引用的 GameplayTag 大小写不一致：{tagName}，应使用 {tagRecord.Name}。");
                }

                if (deprecatedJsonTags.Contains(tagName))
                {
                    report.Error($"Buff {buff.Key} 引用了已废弃的 GameplayTag：{tagName}。");
                }
            }
        }
    }

    private static bool ValidateTagName(string name, string location, ValidationReport report)
    {
        try
        {
            GameplayTagUtility.ValidateName(name);
            return true;
        }
        catch (Exception exception)
        {
            report.Error($"{location} 的标签名称非法：{name}（{exception.Message}）。");
            return false;
        }
    }

    private static int CountActiveTags(
        Dictionary<string, TagRecord> allTags,
        HashSet<string> deprecatedJsonTags)
    {
        int count = 0;
        foreach (string name in allTags.Keys)
        {
            if (!deprecatedJsonTags.Contains(name))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetParentName(string name)
    {
        int separatorIndex = name.LastIndexOf('.');
        return separatorIndex > 0 ? name.Substring(0, separatorIndex) : string.Empty;
    }

    private sealed class TagRecord
    {
        public string Name;
        public string Description;
        public GameplayTagFlags Flags;
        public string Source;
        public bool Deprecated;
    }

    private sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int ActiveTagCount;

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }
    }
}
