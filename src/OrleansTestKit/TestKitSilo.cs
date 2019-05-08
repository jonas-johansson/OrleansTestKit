using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Orleans.Core;
using Orleans.Runtime;
using Orleans.Streams;
using Orleans.TestKit.Reminders;
using Orleans.TestKit.Services;
using Orleans.TestKit.Storage;
using Orleans.TestKit.Streams;
using Orleans.TestKit.Timers;

namespace Orleans.TestKit
{
    public sealed class TestKitSilo
    {
        private readonly TestGrainCreator _grainCreator;

        private readonly TestGrainRuntime _grainRuntime;

        private readonly Dictionary<Grain, TestGrainLifecycle> _grainLifecycles = new Dictionary<Grain, TestGrainLifecycle>();

        /// <summary>
        /// Silo service provider used when creating new grain instances.
        /// </summary>
        /// <returns></returns>
        public TestServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Silo grain factory used by the test grain with creating other grains
        /// This should only be used by the grain, not any test code.
        /// </summary>
        public TestGrainFactory GrainFactory { get; }

        /// <summary>
        /// Manages all test silo timers.
        /// </summary>
        public TestTimerRegistry TimerRegistry { get; }

        /// <summary>
        /// Manages all test silo timers.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use TestKitSilo.TimerRegistry")]
        public TestTimerRegistry TimerReistry => this.TimerRegistry;

        /// <summary>
        /// Manages all test silo reminders
        /// </summary>
        /// <returns></returns>
        public TestReminderRegistry ReminderRegistry { get; }

        /// <summary>
        /// Manages all test silo streams
        /// </summary>
        /// <returns></returns>
        public TestStreamProviderManager StreamProviderManager { get; }

        /// <summary>
        /// Manages all test silo storage
        /// </summary>
        /// <returns></returns>
        public StorageManager StorageManager { get; }

        /// <summary>
        /// Configures the test silo
        /// </summary>
        /// <returns></returns>
        public TestKitOptions Options { get; } = new TestKitOptions();

        public TestKitSilo()
        {
            ServiceProvider = new TestServiceProvider(Options);

            StorageManager = new StorageManager();

            TimerRegistry = new TestTimerRegistry();

            ReminderRegistry = new TestReminderRegistry();

            StreamProviderManager = new TestStreamProviderManager(Options);

            ServiceProvider.AddService<IKeyedServiceCollection<string, IStreamProvider>>(StreamProviderManager);

            GrainFactory = new TestGrainFactory(Options);

            _grainRuntime = new TestGrainRuntime(GrainFactory, TimerRegistry, ReminderRegistry, ServiceProvider, StorageManager);

            _grainCreator = new TestGrainCreator(_grainRuntime, ServiceProvider);

            GrainFactory.TestKitSilo = this;
        }

        #region CreateGrains

        public Task<TGrain> CreateGrainAsync<TGrain>(long id) where TGrain : Grain, IGrainWithIntegerKey
            => CreateGrainAsync<TGrain, TGrain>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain>(Guid id) where TGrain : Grain, IGrainWithGuidKey
            => CreateGrainAsync<TGrain, TGrain>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain>(string id) where TGrain : Grain, IGrainWithStringKey
            => CreateGrainAsync<TGrain, TGrain>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain>(Guid id, string keyExtension) where TGrain : Grain, IGrainWithGuidCompoundKey
            => CreateGrainAsync<TGrain, TGrain>(new TestGrainIdentity(id, keyExtension));

        public Task<TGrain> CreateGrainAsync<TGrain>(long id, string keyExtension) where TGrain : Grain, IGrainWithIntegerCompoundKey
            => CreateGrainAsync<TGrain, TGrain>(new TestGrainIdentity(id, keyExtension));

        public Task<TGrain> CreateGrainAsync<TGrain, TInterface>(long id) where TGrain : Grain, IGrainWithIntegerKey
            => CreateGrainAsync<TGrain, TInterface>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain, TInterface>(Guid id) where TGrain : Grain, IGrainWithGuidKey
            => CreateGrainAsync<TGrain, TInterface>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain, TInterface>(string id) where TGrain : Grain, IGrainWithStringKey
            => CreateGrainAsync<TGrain, TInterface>(new TestGrainIdentity(id));

        public Task<TGrain> CreateGrainAsync<TGrain, TInterface>(Guid id, string keyExtension) where TGrain : Grain, IGrainWithGuidCompoundKey
            => CreateGrainAsync<TGrain, TInterface>(new TestGrainIdentity(id, keyExtension));

        public Task<TGrain> CreateGrainAsync<TGrain, TInterface>(long id, string keyExtension) where TGrain : Grain, IGrainWithIntegerCompoundKey
            => CreateGrainAsync<TGrain, TInterface>(new TestGrainIdentity(id, keyExtension));

        private async Task<TGrain> CreateGrainAsync<TGrain, TInterface>(IGrainIdentity identity) where TGrain : Grain, IGrain
        {
            var grainLifecycle = new TestGrainLifecycle();

            var grainContext = new TestGrainActivationContext
            {
                ActivationServices = ServiceProvider,
                GrainIdentity = identity,
                GrainType = typeof(TGrain),
                ObservableLifecycle = grainLifecycle,
            };

            //Create a stateless grain
            var grain = _grainCreator.CreateGrainInstance(grainContext) as TGrain;

            if (grain == null)
                throw new Exception($"Unable to instantiate grain {typeof(TGrain)} properly");

            _grainLifecycles.Add(grain, grainLifecycle);

            //Check if there are any reminders for this grain
            var remindable = grain as IRemindable;

            //Set the reminder target
            if (remindable != null)
                ReminderRegistry.SetGrainTarget(remindable);

            //Trigger the lifecycle hook that will get the grain's state from the runtime
            await grainLifecycle.TriggerStartAsync();

            GrainFactory.AddGrain<TGrain, TInterface>(identity, grain);

            return grain as TGrain;
        }

        public async Task<TGrainInterface> CreateGrainFromInterfaceAsync<TGrainInterface>(IGrainIdentity identity) where TGrainInterface : IGrain
        {
            var grainLifecycle = new TestGrainLifecycle();

            var grainContext = new TestGrainActivationContext
            {
                ActivationServices = ServiceProvider,
                GrainIdentity = identity,
                GrainType = typeof(TGrainInterface),
                ObservableLifecycle = grainLifecycle,
            };

            //Create a stateless grain
            var grain = _grainCreator.CreateGrainInstance(grainContext);

            if (grain == null)
                throw new Exception($"Unable to instantiate grain {typeof(TGrainInterface)} properly");

            _grainLifecycles.Add(grain, grainLifecycle);

            //Check if there are any reminders for this grain
            var remindable = grain as IRemindable;

            //Set the reminder target
            if (remindable != null)
                ReminderRegistry.SetGrainTarget(remindable);

            //Trigger the lifecycle hook that will get the grain's state from the runtime
            await grainLifecycle.TriggerStartAsync();

            // TODO: GrainFactory.AddGrain<TGrainInterface, TInterface>(identity, grain);

            return (TGrainInterface)(object)grain;
        }

        #endregion

        #region Verifies

        public void VerifyRuntime(Expression<Action<IGrainRuntime>> expression, Func<Times> times)
        {
            _grainRuntime.Mock.Verify(expression, times);
        }

        #endregion Verifies

        /// <summary>
        /// Deactivate the given <see cref="Grain"/>
        /// </summary>
        /// <param name="grain">Grain to Deactivate</param>
        public Task DeactivateAsync(Grain grain)
        {
            var grainLifecycle = _grainLifecycles[grain];
            return grainLifecycle.TriggerStopAsync();
        }
    }
}
