﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bit.Client.Xamarin.Prism.View;
using Bit.Core.Contracts;
using Bit.Core.Implementations;
using Bit.Core.Models.Events;
using Bit.View;
using Bit.ViewModel;
using Bit.ViewModel.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Prism;
using Prism.Autofac;
using Prism.Events;
using Prism.Ioc;
using Prism.Navigation;
using Prism.Plugin.Popups;
using Prism.Regions;
using Prism.Regions.Adapters;
using Prism.Regions.Behaviors;
using Prism.Services;
using Rg.Plugins.Popup.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

#if !UWP
[assembly: XmlnsDefinition("https://bitframework.com", "Bit", AssemblyName = "Bit.Client.Xamarin.Prism")]
[assembly: XmlnsDefinition("https://bitframework.com", "Bit.View", AssemblyName = "Bit.Client.Xamarin.Prism")]
#endif

[assembly: Xamarin.Forms.Internals.Preserve]

namespace Bit
{
    public abstract class BitApplication : PrismApplication, IAsyncDisposable
    {
        private readonly Lazy<IEventAggregator> _eventAggregator = default!;
        private readonly TaskCompletionSource<object?> _isReadyTaskCompletionSource = new TaskCompletionSource<object?>();

        /// <summary>
        /// To be called in shared/net-standard project.
        /// https://docs.microsoft.com/bg-bg/xamarin/xamarin-forms/xaml/custom-namespace-schemas#consuming-a-custom-namespace-schema
        /// </summary>
        public static void XamlInit()
        {

        }

#if !Android && !iOS && !UWP
        public BitApplication()
            : this(null)
        {

        }
#endif

        protected BitApplication(IPlatformInitializer? platformInitializer = null)
            : base(platformInitializer)
        {
            _eventAggregator = new Lazy<IEventAggregator>(() =>
            {
                return Container.Resolve<IEventAggregator>();
            }, isThreadSafe: true);

            if (MainPage == null)
                MainPage = new ContentPage { Title = "DefaultPage" };
        }

        protected sealed override async void OnInitialized()
        {
            try
            {
                IEnumerable<ITelemetryService> allTelemetryServices = Container.Resolve<IEnumerable<ITelemetryService>>();
                ISecurityServiceBase securityService = Container.Resolve<ISecurityServiceBase>();
                IMessageReceiver? messageReceiver = Container.Resolve<ILifetimeScope>().ResolveOptional<IMessageReceiver>();
                IDeviceService deviceService = Container.Resolve<IDeviceService>();

                bool isLoggedIn = await securityService.IsLoggedInAsync().ConfigureAwait(false);
                string? userId = !isLoggedIn ? null : await securityService.GetUserIdAsync(default).ConfigureAwait(false);

                foreach (TelemetryServiceBase telemetryService in Container.Resolve<IEnumerable<ITelemetryService>>().OfType<TelemetryServiceBase>())
                {
                    telemetryService.LogPreviousSessionCrashIfAny();

                    if (isLoggedIn)
                        telemetryService.SetUserId(userId);
                    else
                        telemetryService.SetUserId(null);

                    if (messageReceiver != null)
                        telemetryService.MessageReceiver = messageReceiver;
                }

                if (BitExceptionHandler.Current is BitExceptionHandler exceptionHandler)
                    exceptionHandler.ServiceProvider = Container.Resolve<IServiceProvider>();

                DependencyDelegates.Current.StartTimer = (interval, callback) => deviceService.StartTimer(interval, () => callback());
                DependencyDelegates.Current.GetNavigationUriPath = () => Current?.NavigationService?.CurrentPageNavService?.GetNavigationUriPath() ?? "?";

                if (Device.RuntimePlatform == Device.UWP || Device.RuntimePlatform == Device.iOS || Device.RuntimePlatform == Device.Android)
                {
                    Connectivity.ConnectivityChanged += (sender, e) =>
                    {
                        _eventAggregator.Value.GetEvent<ConnectivityChangedEvent>()
                            .Publish(new ConnectivityChangedEvent { IsConnected = e.NetworkAccess != Xamarin.Essentials.NetworkAccess.None });
                    };
                }

                await OnInitializedAsync();

                await Task.Yield();

                _isReadyTaskCompletionSource.SetResult(null);
            }
            catch (Exception exp)
            {
                _isReadyTaskCompletionSource.SetException(exp);
                BitExceptionHandler.Current.OnExceptionReceived(exp);
            }
        }

        protected virtual Task OnInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public new INavService NavigationService => (PrismNavigationService == null ? null : Container.Resolve<INavServiceFactory>()(PrismNavigationService, Container.Resolve<IPopupNavigation>(), Container.Resolve<IRegionManager>()))!;

        public new static BitApplication Current => (PrismApplicationBase.Current as BitApplication)!;

        public INavigationService PrismNavigationService => base.NavigationService;

        protected sealed override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            ContainerBuilder containerBuilder = containerRegistry.GetBuilder();
            AutofacDependencyManager dependencyManager = (AutofacDependencyManager)containerBuilder.Properties[nameof(dependencyManager)]!;
            IServiceCollection services = (IServiceCollection)containerBuilder.Properties[nameof(services)]!;

            RegisterTypes(dependencyManager, containerRegistry, containerBuilder, services);

            containerBuilder.Populate(services);
        }

        protected virtual void RegisterTypes(IDependencyManager dependencyManager, IContainerRegistry containerRegistry, ContainerBuilder containerBuilder, IServiceCollection services)
        {
            dependencyManager.RegisterUsing(resolver => Container, lifeCycle: DependencyLifeCycle.SingleInstance, overwriteExisting: false);
            dependencyManager.RegisterUsing(resolver => Container.GetContainer(), lifeCycle: DependencyLifeCycle.SingleInstance, overwriteExisting: false);
            BitCSharpClientControls.UseBitPopupNavigation();
            containerRegistry.RegisterPopupNavigationService();

            containerRegistry.RegisterRegionServices();

            // workaround begin
            containerRegistry.Register<CarouselViewRegionAdapter>();
            containerRegistry.Register<LayoutViewRegionAdapter>();
            containerRegistry.Register<ScrollViewRegionAdapter>();
            containerRegistry.Register<ContentViewRegionAdapter>();

            containerRegistry.Register<SingleActiveRegion, BitSingleActiveRegion>();

            containerRegistry.Register<DelayedRegionCreationBehavior>();
            containerRegistry.Register<RegionBehaviorFactory>();
            containerRegistry.Register<BindRegionContextToVisualElementBehavior>();
            containerRegistry.Register<RegionActiveAwareBehavior>();
            containerRegistry.Register<SyncRegionContextWithHostBehavior>();
            containerRegistry.Register<BindRegionContextToVisualElementBehavior>();
            containerRegistry.Register<RegionManagerRegistrationBehavior>();
            containerRegistry.Register<RegionMemberLifetimeBehavior>();
            containerRegistry.Register<ClearChildViewsRegionBehavior>();
            containerRegistry.Register<AutoPopulateRegionBehavior>();
            containerRegistry.Register<DestructibleRegionBehavior>();
            // workaround end

            //containerRegistry.RegisterPopupDialogService();
            containerRegistry.RegisterForNav<PageWhichWeStayThereUntilRegionsAreDisposedPage>("PageWhichWeStayThereUntilRegionsAreDisposed");
        }

        public virtual async ValueTask DisposeAsync()
        {
            await NavigationService.NavigateAsync("/PageWhichWeStayThereUntilRegionsAreDisposed");
            await Container.GetContainer().DisposeAsync();
        }

        public Task Ready()
        {
            return _isReadyTaskCompletionSource.Task;
        }
    }

    public class BitSingleActiveRegion : SingleActiveRegion
    {
        public override void Activate(VisualElement view)
        {
            var currentActiveView = ActiveViews.FirstOrDefault();

            if (currentActiveView != null && currentActiveView != view && Views.Contains(currentActiveView))
            {
                Remove(currentActiveView);
            }

            base.Activate(view);
        }
    }
}
