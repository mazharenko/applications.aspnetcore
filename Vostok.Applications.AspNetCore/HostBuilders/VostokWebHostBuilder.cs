﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vostok.Applications.AspNetCore.Builders;
using Vostok.Applications.AspNetCore.Models;
using Vostok.Applications.AspNetCore.StartupFilters;
using Vostok.Commons.Helpers;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
using Vostok.Hosting.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;

// ReSharper disable PartialTypeWithSinglePart

namespace Vostok.Applications.AspNetCore.HostBuilders
{
    internal partial class VostokWebHostBuilder
    {
        private readonly IVostokHostingEnvironment environment;
        private readonly VostokKestrelBuilder kestrelBuilder;
        private readonly VostokMiddlewaresBuilder middlewaresBuilder;
        private readonly List<IDisposable> disposables;
        private readonly Type startupType;

        private readonly AtomicBoolean webHostEnabled;
        private readonly Customization<IWebHostBuilder> webHostCustomization;

        public VostokWebHostBuilder(
            IVostokHostingEnvironment environment,
            VostokKestrelBuilder kestrelBuilder,
            VostokMiddlewaresBuilder middlewaresBuilder,
            List<IDisposable> disposables,
            Type startupType)
        {
            this.environment = environment;
            this.kestrelBuilder = kestrelBuilder;
            this.middlewaresBuilder = middlewaresBuilder;
            this.disposables = disposables;
            this.startupType = startupType;

            webHostEnabled = true;
            webHostCustomization = new Customization<IWebHostBuilder>();
        }

        public void Disable()
            => webHostEnabled.Value = false;

        public void Customize(Action<IWebHostBuilder> customization)
            => webHostCustomization.AddCustomization(customization);

        public void ConfigureWebHost(IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureServices(RegisterBasePath);

            webHostBuilder.ConfigureServices(middlewaresBuilder.Register);

            ConfigureWebHostInternal(webHostBuilder);
        }

        private void ConfigureWebHostInternal(IWebHostBuilder webHostBuilder)
        {
            ConfigureUrl(webHostBuilder);

            var urlsBefore = webHostBuilder.GetSetting(WebHostDefaults.ServerUrlsKey);

            webHostBuilder.UseKestrel(kestrelBuilder.ConfigureKestrel);
            webHostBuilder.UseSockets();
            webHostBuilder.UseShutdownTimeout(environment.ShutdownTimeout.Cut(100.Milliseconds(), 0.05));

            if (startupType != null && startupType != typeof(EmptyStartup))
                webHostBuilder.UseStartup(startupType);

            webHostCustomization.Customize(webHostBuilder);

            EnsureUrlsNotChanged(urlsBefore, webHostBuilder.GetSetting(WebHostDefaults.ServerUrlsKey));
        }

        private void ConfigureUrl(IWebHostBuilder webHostBuilder)
        {
            if (!environment.ServiceBeacon.ReplicaInfo.TryGetUrl(out var url))
                throw new Exception("Port or url should be configured in ServiceBeacon using VostokHostingEnvironmentSetup.");

            middlewaresBuilder.Customize(settings => settings.BaseUrl = url);
            webHostBuilder.UseUrls($"{url.Scheme}://*:{url.Port}/");
        }

        private void RegisterBasePath(IServiceCollection services)
            => services.AddTransient<IStartupFilter>(_ => new UrlPathStartupFilter(environment));

        private static void EnsureUrlsNotChanged(string urlsBefore, string urlsAfter)
        {
            if (urlsAfter.Contains(urlsBefore))
                return;

            throw new Exception(
                "Application url should be configured in ServiceBeacon instead of WebHostBuilder.\n" +
                $"ServiceBeacon url: '{urlsBefore}'. WebHostBuilder urls: '{urlsAfter}'.\n" +
                "To configure application port (without url) use VostokHostingEnvironmentSetup extension: `vostokHostingEnvironmentSetup.SetPort(...)`.\n" +
                "To configure application url use VostokHostingEnvironmentSetup: `vostokHostingEnvironmentSetup.SetupServiceBeacon(serviceBeaconBuilder => serviceBeaconBuilder.SetupReplicaInfo(replicaInfo => replicaInfo.SetUrl(...)))`.");
        }
    }
}