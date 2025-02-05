﻿#if NETCOREAPP
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Vostok.Applications.AspNetCore.Builders;
using Vostok.Applications.AspNetCore.HostBuilders;
using Vostok.Hosting.Abstractions;
using Vostok.Hosting.Abstractions.Helpers;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;

namespace Vostok.Applications.AspNetCore
{
    /// <summary>
    /// <para><see cref="VostokNetCoreApplication"/> is the abstract class developers inherit from in order to create Vostok-compatible .Net Core applications based on Microsoft generic host.</para>
    /// <para>Implement <see cref="Setup"/> method to configure <see cref="IHostBuilder"/> (see <see cref="IVostokNetCoreApplicationBuilder"/>).</para>
    /// </summary>
    [PublicAPI]
    public abstract class VostokNetCoreApplication : IVostokApplication, IDisposable
    {
        private volatile GenericHostManager manager;

        public virtual async Task InitializeAsync(IVostokHostingEnvironment environment)
        {
            var log = environment.Log.ForContext<VostokNetCoreApplication>();

            var hostBuilder = new GenericHostFactory(environment, this);

            var applicationBuilder = new VostokNetCoreApplicationBuilder(hostBuilder);

            Setup(applicationBuilder, environment);

            manager = new GenericHostManager(hostBuilder.CreateHost(), log);

            using (new OperationContextToken("Warmup"))
                await WarmupServicesAsync(environment, manager.Services);

            await manager.StartHostAsync(environment.ShutdownToken, environment.HostExtensions.TryGet<IVostokHostShutdown>(out var shutdown) ? shutdown : null);

            using (new OperationContextToken("Warmup"))
                await WarmupAsync(environment, manager.Services);
        }

        /// <inheritdoc cref="VostokAspNetCoreApplication.WarmupServicesAsync"/>
        public virtual Task WarmupServicesAsync(IVostokHostingEnvironment environment, IServiceProvider serviceProvider) =>
            Task.CompletedTask;
        
        /// <inheritdoc cref="VostokAspNetCoreApplication.WarmupAsync"/>
        public virtual Task WarmupAsync(IVostokHostingEnvironment environment, IServiceProvider serviceProvider) =>
            Task.CompletedTask;

        public Task RunAsync(IVostokHostingEnvironment environment) =>
            manager.RunHostAsync();

        /// <summary>
        /// Implement this method to configure <see cref="IHostBuilder"/>.
        /// </summary>
        public abstract void Setup([NotNull] IVostokNetCoreApplicationBuilder builder, [NotNull] IVostokHostingEnvironment environment);

        /// <summary>
        /// Override this method to perform any dispose actions that needs to happen after host has been stopped.
        /// </summary>
        public virtual void DoDispose()
        {
        }

        /// <summary>
        /// Override this method to perform any Async dispose actions that needs to happen after host has been stopped.
        /// </summary>
        public virtual Task DoDisposeAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            manager?.Dispose();
            DoDisposeAsync().GetAwaiter().GetResult();
            DoDispose();
        }
    }
}
#endif