using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseLink.Infrastructure.Data;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = DatabaseProvider.Read(configuration);

        if (provider == DatabaseProvider.SqlServer)
        {
            var connectionString = configuration.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException(
                    "Database:Provider=SqlServer requires ConnectionStrings:SqlServer.");

            services.AddDbContext<SqlServerPulseLinkDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddScoped<PulseLinkDbContext>(sp =>
                sp.GetRequiredService<SqlServerPulseLinkDbContext>());
        }
        else
        {
            var connectionString = configuration.GetConnectionString("Sqlite")
                ?? configuration.GetConnectionString("Default")
                ?? "Data Source=pulselink.db";

            services.AddDbContext<SqlitePulseLinkDbContext>(options =>
                options.UseSqlite(connectionString));
            services.AddScoped<PulseLinkDbContext>(sp =>
                sp.GetRequiredService<SqlitePulseLinkDbContext>());
        }

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<PulseLinkDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
