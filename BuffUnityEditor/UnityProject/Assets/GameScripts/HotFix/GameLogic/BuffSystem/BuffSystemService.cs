using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Buffs
{
    public sealed class BuffSystemService : Singleton<BuffSystemService>, IUpdate
    {
        private bool _isInitializing;
        private BuffWorld _world;

        public bool IsInitialized { get; private set; }
        public string ConfigLocation { get; private set; } = BuffSchema.DefaultConfigLocation;
        public BuffConfigDatabase Database { get; private set; }
        public BuffWorld World => _world;

        public async UniTask InitializeAsync(string configLocation = BuffSchema.DefaultConfigLocation)
        {
            while (_isInitializing)
            {
                await UniTask.Yield();
            }

            if (IsInitialized)
            {
                return;
            }

            await LoadConfigAsync(configLocation);
        }

        public async UniTask ReloadAsync(string configLocation = null)
        {
            while (_isInitializing)
            {
                await UniTask.Yield();
            }

            await LoadConfigAsync(string.IsNullOrWhiteSpace(configLocation) ? ConfigLocation : configLocation);
        }

        public void InitializeFromJson(string json)
        {
            BuffConfigDatabase database = BuffConfigDatabase.FromJson(json);
            ReplaceWorld(database);
        }

        public BuffUnit CreateUnit(
            string id,
            PrimaryAttributeType? primaryAttribute = null,
            int teamId = 0,
            IDictionary<string, float> baseAttributes = null)
        {
            EnsureInitialized();
            return _world.CreateUnit(id, primaryAttribute, teamId, baseAttributes);
        }

        public bool TryGetTemplate(string idOrKey, out BuffTemplate template)
        {
            template = null;
            return Database != null && Database.TryGet(idOrKey, out template);
        }

        public void OnUpdate()
        {
            if (IsInitialized && _world != null)
            {
                _world.Update(Time.deltaTime);
            }
        }

        protected override void OnRelease()
        {
            DisposeWorld();
            Database = null;
            IsInitialized = false;
            _isInitializing = false;
        }

        private async UniTask LoadConfigAsync(string configLocation)
        {
            _isInitializing = true;
            TextAsset configAsset = null;
            try
            {
                ConfigLocation = string.IsNullOrWhiteSpace(configLocation)
                    ? BuffSchema.DefaultConfigLocation
                    : configLocation;
                configAsset = await GameModule.Resource.LoadAssetAsync<TextAsset>(ConfigLocation);
                if (configAsset == null)
                {
                    throw new BuffConfigurationException($"无法加载 Buff 配置资源：{ConfigLocation}");
                }

                BuffConfigDatabase database = BuffConfigDatabase.FromJson(configAsset.text);
                ReplaceWorld(database);
                Log.Info(
                    $"Buff 系统初始化完成：Schema {database.Data.SchemaVersion}，共 {database.Templates.Count} 个效果。");

                for (int index = 0; index < database.Warnings.Count; index++)
                {
                    Log.Warning($"Buff 配置警告：{database.Warnings[index]}");
                }
            }
            finally
            {
                if (configAsset != null)
                {
                    GameModule.Resource.UnloadAsset(configAsset);
                }

                _isInitializing = false;
            }
        }

        private void ReplaceWorld(BuffConfigDatabase database)
        {
            DisposeWorld();
            Database = database;
            _world = new BuffWorld(database);
            _world.EventRaised += OnWorldEventRaised;
            IsInitialized = true;
        }

        private void DisposeWorld()
        {
            if (_world == null)
            {
                return;
            }

            _world.EventRaised -= OnWorldEventRaised;
            _world.Dispose();
            _world = null;
        }

        private static void OnWorldEventRaised(BuffRuntimeEvent runtimeEvent)
        {
            GameEvent.Send(BuffEventIds.AnyChanged, runtimeEvent);
            int eventId = BuffEventIds.GetEventId(runtimeEvent.EventType);
            if (eventId != BuffEventIds.AnyChanged)
            {
                GameEvent.Send(eventId, runtimeEvent);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _world == null)
            {
                throw new InvalidOperationException("BuffSystemService 尚未初始化，请先调用 InitializeAsync。");
            }
        }
    }
}
