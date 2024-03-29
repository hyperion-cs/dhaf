﻿using Dhaf.Core;
using dotnet_etcd;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public class ExtensionStorageProvider : IExtensionStorageProvider
    {
        private readonly EtcdClient _etcdClient;
        private readonly Grpc.Core.Metadata _etcdHeaders;
        private readonly ClusterConfig _clusterConfig;
        protected DhafInternalConfig _dhafInternalConfig;
        private readonly string _extensionSign;

        protected string _etcdRootPath
        {
            get => $"/{_clusterConfig.Dhaf.ClusterName}/{_dhafInternalConfig.Etcd.ExtensionStoragePath}/{_extensionSign}/";
        }

        public ExtensionStorageProvider(EtcdClient etcdClient, Grpc.Core.Metadata etcdHeaders,
            ClusterConfig clusterConfig, DhafInternalConfig dhafInternalConfig,
            string extensionPrefix)
        {
            _etcdClient = etcdClient;
            _etcdHeaders = etcdHeaders;
            _clusterConfig = clusterConfig;
            _dhafInternalConfig = dhafInternalConfig;
            _extensionSign = extensionPrefix;
        }

        public async Task DeleteAsync(string key)
        {
            var realKey = _etcdRootPath + key;
            await _etcdClient.DeleteAsync(realKey, _etcdHeaders);
        }

        public async Task DeleteRangeAsync(string keyPrefix)
        {
            var realKeyPrefix = _etcdRootPath + keyPrefix;
            await _etcdClient.DeleteRangeAsync(realKeyPrefix, _etcdHeaders);
        }

        public async Task<string> GetAsyncOfDefault(string key)
        {
            var realKey = _etcdRootPath + key;
            var value = await _etcdClient.GetValAsync(realKey, _etcdHeaders);

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value;
        }

        public async Task<IDictionary<string, string>> GetRangeAsync(string keyPrefix)
        {
            var realKeyPrefix = _etcdRootPath + keyPrefix;

            var values = await _etcdClient.GetRangeValAsync(realKeyPrefix, _etcdHeaders);
            return values;
        }

        public async Task PutAsync(string key, string value)
        {
            var realKey = _etcdRootPath + key;
            await _etcdClient.PutAsync(realKey, value, _etcdHeaders);
        }
    }
}
