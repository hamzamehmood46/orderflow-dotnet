using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Core.Messaging;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Payments;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderFlowInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Orders") ?? "Data Source=orderflow.db";
        services.AddDbContext<OrdersDbContext>(o => o.UseSqlite(connection));

        services.Configure<WorkerOptions>(config.GetSection(WorkerOptions.Section));

        services.AddSingleton<ChannelMessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<ChannelMessageBus>());
        services.AddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
        services.AddScoped<OrderProcessor>();
        services.AddHostedService<OrderWorker>();

        return services;
    }
}
