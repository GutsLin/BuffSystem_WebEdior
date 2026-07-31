using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayTags;
using Newtonsoft.Json;
using TEngine;
using UnityEngine;

namespace GameLogic.Buffs
{
    public static class GameplayTagRuntimeConfig
    {
        public const string SchemaVersion = "1.0.0";
        public const string DefaultConfigLocation = "gameplay_tags";
        public const int MinimumVersion = 1;
    }

    [Serializable]
    public sealed class GameplayTagConfigEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("flags")]
        public GameplayTagFlags Flags { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("deprecated")]
        public bool Deprecated { get; set; }
    }

    [Serializable]
    public sealed class GameplayTagsConfigFile
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = GameplayTagRuntimeConfig.SchemaVersion;

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("exportedAt")]
        public string ExportedAt { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public List<GameplayTagConfigEntry> Tags { get; set; } = new List<GameplayTagConfigEntry>();
    }

    public sealed class GameplayTagConfigurationException : Exception
    {
        public GameplayTagConfigurationException(string message) : base(message)
        {
        }

        public GameplayTagConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public static class GameplayTagRuntimeLoader
    {
        public static int ConfigVersion { get; private set; }
        public static string ConfigSchemaVersion { get; private set; } = GameplayTagRuntimeConfig.SchemaVersion;

        public static void InitializeFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameplayTagConfigurationException("GameplayTags 配置 JSON 为空。");
            }

            GameplayTagsConfigFile file;
            try
            {
                file = JsonConvert.DeserializeObject<GameplayTagsConfigFile>(json);
            }
            catch (Exception exception)
            {
                throw new GameplayTagConfigurationException("GameplayTags 配置 JSON 格式不正确。", exception);
            }

            ValidateFile(file);
            ConfigVersion = file.Version;
            ConfigSchemaVersion = file.SchemaVersion;

            List<GameplayTagRuntimeData> runtimeTags = new List<GameplayTagRuntimeData>();
            for (int index = 0; index < file.Tags.Count; index++)
            {
                GameplayTagConfigEntry entry = file.Tags[index];
                if (entry.Deprecated)
                {
                    continue;
                }

                runtimeTags.Add(new GameplayTagRuntimeData
                {
                    Name = entry.Name,
                    Description = entry.Description,
                    Flags = entry.Flags,
                });
            }

            GameplayTagManager.Initialize(runtimeTags);
        }

        public static async UniTask InitializeFromJsonAsync(
            string configLocation = GameplayTagRuntimeConfig.DefaultConfigLocation)
        {
            TextAsset configAsset = null;
            try
            {
                configAsset = await GameModule.Resource.LoadAssetAsync<TextAsset>(configLocation);
                if (configAsset == null)
                {
                    throw new GameplayTagConfigurationException(
                        $"无法加载 GameplayTags 配置资源：{configLocation}");
                }

                InitializeFromJson(configAsset.text);
                Log.Info(
                    $"GameplayTags 初始化完成：Schema {ConfigSchemaVersion}，Version {ConfigVersion}，共 {GameplayTagManager.GetAllTags().Length} 个标签。");
            }
            catch (GameplayTagConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GameplayTagConfigurationException(
                    $"GameplayTags 初始化失败，资源：{configLocation}。", exception);
            }
            finally
            {
                if (configAsset != null)
                {
                    GameModule.Resource.UnloadAsset(configAsset);
                }
            }
        }

        private static void ValidateFile(GameplayTagsConfigFile file)
        {
            if (file == null)
            {
                throw new GameplayTagConfigurationException("GameplayTags 配置为空。");
            }

            if (!string.Equals(file.SchemaVersion, GameplayTagRuntimeConfig.SchemaVersion, StringComparison.Ordinal))
            {
                throw new GameplayTagConfigurationException(
                    $"不支持的 GameplayTags Schema 版本：{file.SchemaVersion}，当前支持 {GameplayTagRuntimeConfig.SchemaVersion}。");
            }

            if (file.Version < GameplayTagRuntimeConfig.MinimumVersion)
            {
                throw new GameplayTagConfigurationException(
                    $"GameplayTags 配置版本过低：{file.Version}。");
            }

            if (file.Tags == null)
            {
                throw new GameplayTagConfigurationException("GameplayTags 配置缺少 tags 数组。");
            }

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < file.Tags.Count; index++)
            {
                GameplayTagConfigEntry entry = file.Tags[index];
                if (entry == null)
                {
                    throw new GameplayTagConfigurationException(
                        $"GameplayTags 配置 tags[{index}] 为空。");
                }

                try
                {
                    GameplayTagUtility.ValidateName(entry.Name);
                }
                catch (Exception exception)
                {
                    throw new GameplayTagConfigurationException(
                        $"GameplayTag 名称格式不正确：{entry.Name}", exception);
                }

                if (!names.Add(entry.Name))
                {
                    throw new GameplayTagConfigurationException(
                        $"GameplayTag 名称重复：{entry.Name}");
                }
            }
        }
    }
}
