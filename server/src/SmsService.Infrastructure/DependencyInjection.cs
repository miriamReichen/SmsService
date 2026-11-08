using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmsService.Application;

namespace SmsService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(config.GetConnectionString("Default")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<MessageProcessingOptions>(config.GetSection(MessageProcessingOptions.SectionName));
        services.Configure<SmsProviderOptions>(config.GetSection(SmsProviderOptions.SectionName));

        services.AddHttpClient<ISmsProviderClient, SmsProviderClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<SmsProviderOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHostedService<MessageProcessor>();
        return services;
    }
}
