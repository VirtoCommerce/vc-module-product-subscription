using System;
using System.IO;
using System.Linq;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.BackgroundJobs;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using VirtoCommerce.ProductSubscription.Data.Import;
using VirtoCommerce.ProductSubscription.Data.MySql;
using VirtoCommerce.ProductSubscription.Data.Payments;
using VirtoCommerce.ProductSubscription.Data.PostgreSql;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.ProductSubscription.Data.Repositories;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.ProductSubscription.Data.SqlServer;
using VirtoCommerce.ProductSubscription.Xapi;
using VirtoCommerce.ProductSubscription.Xapi.TypeHooks;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.ProductSubscription.Web;

public class Module : IModule, IHasConfiguration
{
    public const string EnableMockPaymentMethodKey = "ProductSubscription:EnableMockPaymentMethod";

    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<ProductSubscriptionDbContext>(options =>
        {
            var databaseProvider = Configuration.GetValue("DatabaseProvider", "SqlServer");
            var connectionString = Configuration.GetConnectionString(ModuleInfo.Id) ?? Configuration.GetConnectionString("VirtoCommerce");

            switch (databaseProvider)
            {
                case "MySql":
                    options.UseMySqlDatabase(connectionString, typeof(MySqlDataAssemblyMarker), Configuration);
                    break;
                case "PostgreSql":
                    options.UsePostgreSqlDatabase(connectionString, typeof(PostgreSqlDataAssemblyMarker), Configuration);
                    break;
                default:
                    options.UseSqlServerDatabase(connectionString, typeof(SqlServerDataAssemblyMarker), Configuration);
                    break;
            }
        });

        serviceCollection.AddTransient<IProductSubscriptionRepository, ProductSubscriptionRepository>();
        serviceCollection.AddTransient<Func<IProductSubscriptionRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IProductSubscriptionRepository>());

        serviceCollection.AddTransient<ISubscriptionPlanService, SubscriptionPlanService>();
        serviceCollection.AddTransient<ISubscriptionPlanSearchService, SubscriptionPlanSearchService>();
        serviceCollection.AddTransient<ISubscriptionService, SubscriptionService>();
        serviceCollection.AddTransient<ISubscriptionSearchService, SubscriptionSearchService>();
        serviceCollection.AddTransient<IRenewalService, RenewalService>();
        serviceCollection.AddTransient<IRenewalSearchService, RenewalSearchService>();
        serviceCollection.AddTransient<ISubscriptionCartItemService, SubscriptionCartItemService>();
        serviceCollection.AddTransient<ISubscriptionCartItemSearchService, SubscriptionCartItemSearchService>();

        serviceCollection.AddTransient<ISubscriptionOfferService, SubscriptionOfferService>();
        serviceCollection.AddTransient<ISubscriptionTermsService, SubscriptionTermsService>();
        serviceCollection.AddTransient<ISubscriptionNotificationService, SubscriptionNotificationService>();
        serviceCollection.AddTransient<ISubscriptionManager, SubscriptionManager>();
        serviceCollection.AddTransient<IRenewalCartBuilder, RenewalCartBuilder>();
        serviceCollection.AddTransient<IRenewalProcessor, RenewalProcessor>();
        serviceCollection.AddTransient<ILegacySubscriptionImporter, LegacySubscriptionImporter>();
        serviceCollection.AddTransient<LegacySubscriptionJsonMapper>();

        serviceCollection.AddTransient<OrderChangedEventHandler>();
        serviceCollection.AddTransient<CartChangedEventHandler>();
        serviceCollection.AddBackgroundJob<OrderChangedJob, OrderChangedJobPayload>(triggerable: false);
        serviceCollection.AddRecurringJob<RenewalSchedulerJob, RenewalSchedulerJobPayload>(schedule => schedule
            .WithId(RenewalSchedulerJob.JobId)
            .FromSettings(ModuleConstants.Settings.General.SchedulerEnabled, ModuleConstants.Settings.General.SchedulerCronExpression)
            // A failed tick is superseded by the next one; retrying it only duplicates work.
            .WithMaxRetryAttempts(0));
        serviceCollection.AddTransient<MockStoredCardPaymentMethod>();

        // Subscription discount: mark subscription lines in xCart's promotion pipeline, then add the reward
        // by decorating the Marketing evaluator. Neither xCart nor Marketing is changed.
        serviceCollection.AddPipeline<PromotionEvaluationContextCartMap>(builder =>
        {
            builder.AddMiddleware(typeof(SubscriptionPromoEntriesMiddleware));
        });
        DecoratePromoEvaluator(serviceCollection);
        DecorateCartAvailMethods(serviceCollection);

        _ = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(XapiAssemblyMarker));
            builder.AddGraphTypeHook<ProductTypeHook>();
            builder.AddGraphTypeHook<LineItemTypeHook>();
        });

        serviceCollection.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.Store.AllStoreSettings, nameof(Store));

        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Product Subscription", ModuleConstants.Security.Permissions.AllPermissions);

        appBuilder.UseScopedSchema<XapiAssemblyMarker>("product-subscription");

        using (var serviceScope = serviceProvider.CreateScope())
        {
            using var dbContext = serviceScope.ServiceProvider.GetRequiredService<ProductSubscriptionDbContext>();
            dbContext.Database.Migrate();
        }

        appBuilder.RegisterEventHandler<OrderChangedEvent, OrderChangedEventHandler>();
        appBuilder.RegisterEventHandler<CartChangedEvent, CartChangedEventHandler>();

        var notificationRegistrar = serviceProvider.GetRequiredService<INotificationRegistrar>();
        var templatesPath = Path.Combine(ModuleInfo.FullPhysicalPath, "NotificationTemplates");
        notificationRegistrar.RegisterNotification<SubscriptionCreatedEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<RenewalReminderEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<RenewalReviewRequiredEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<RenewalPaymentActionRequiredEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<RenewalPaymentFailedEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<SubscriptionCancelledEmailNotification>().WithTemplatesFromPath(templatesPath);
        notificationRegistrar.RegisterNotification<SubscriptionAnnualReminderEmailNotification>().WithTemplatesFromPath(templatesPath);

        // The mock stored-card connector is for development, demos and CI only.
        if (Configuration.GetValue(EnableMockPaymentMethodKey, false))
        {
            var paymentMethodsRegistrar = serviceProvider.GetRequiredService<IPaymentMethodsRegistrar>();
            paymentMethodsRegistrar.RegisterPaymentMethod(() => serviceProvider.GetRequiredService<MockStoredCardPaymentMethod>());
        }
    }

    public void Uninstall()
    {
        // Nothing to do here
    }

    private static void DecoratePromoEvaluator(IServiceCollection services)
    {
        Decorate<IMarketingPromoEvaluator>(services, (_, inner) => new SubscriptionPromoEvaluatorDecorator(inner));
    }

    private static void DecorateCartAvailMethods(IServiceCollection services)
    {
        Decorate<ICartAvailMethodsService>(services, (provider, inner) =>
            new SubscriptionCartAvailMethodsDecorator(inner, provider.GetRequiredService<ISubscriptionCartItemSearchService>()));
    }

    /// <summary>Wraps the last registration of <typeparamref name="TService"/>, keeping its lifetime.</summary>
    private static void Decorate<TService>(IServiceCollection services, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(TService));
        if (descriptor == null)
        {
            return;
        }

        services.Remove(descriptor);

        Func<IServiceProvider, TService> createInner = descriptor switch
        {
            { ImplementationInstance: TService instance } => _ => instance,
            { ImplementationFactory: not null } => provider => (TService)descriptor.ImplementationFactory(provider),
            _ => provider => (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType),
        };

        services.Add(new ServiceDescriptor(typeof(TService), provider => decorate(provider, createInner(provider)), descriptor.Lifetime));
    }
}
