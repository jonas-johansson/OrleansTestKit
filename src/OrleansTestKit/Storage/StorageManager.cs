using System;
using System.Collections.Generic;
using System.Linq;
using Orleans.Core;

namespace Orleans.TestKit.Storage
{
    public sealed class StorageManager
    {
        private Dictionary<Grain, object> _storages = new Dictionary<Grain, object>();

        public IStorage<TState> GetStorage<TState>(Grain grain) where TState : new()
        {
            object storage;

            if (_storages.ContainsKey(grain))
            {
                storage = _storages[grain];
            }
            else
            {
                storage = new TestStorage<TState>();
                _storages[grain] = storage;
            }

            return storage as IStorage<TState>;
        }

        public TestStorageStats GetStorageStats()
        {
            var accumulatedStats = new TestStorageStats();

            foreach (var statsObj in _storages.Values)
            {
                var s = (statsObj as IStorageStats).Stats;
                accumulatedStats.Clears += s.Clears;
                accumulatedStats.Reads += s.Reads;
                accumulatedStats.Writes += s.Writes;
            }

            return accumulatedStats;
        }

        public void ResetCounts()
        {
            foreach (var statsObj in _storages.Values)
            {
                var s = (statsObj as IStorageStats).Stats;
                s.Clears = 0;
                s.Reads = 0;
                s.Writes = 0;
            }
        }
    }
}
