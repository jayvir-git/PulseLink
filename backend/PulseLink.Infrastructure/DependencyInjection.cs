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
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=pulselink.db";

        services.AddDbContext<PulseLinkDbContext>(options =>
            options.UseSqlite(connectionString));

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
