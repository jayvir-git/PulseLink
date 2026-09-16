using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseLinkDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (DatabaseProvider.ShouldMigrateOnStartup(environment, configuration))
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogInformation(
                "Skipping database migrate at startup. Apply migrations as a deploy step, or set Database:MigrateOnStartup=true.");
        }

        if (db.Database.IsSqlServer())
        {
            // Azure SQL serverless may reject the first connection while resuming.
            // Retry only opening the connection, before any seed writes occur.
            var startupConnection = new SqlServerRetryingExecutionStrategy(db, 6, TimeSpan.FromSeconds(30), null);
            var connection = db.GetService<IRelationalConnection>();
            await startupConnection.ExecuteAsync(() => connection.OpenAsync(CancellationToken.None));
        }

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (await db.Agencies.AnyAsync())
        {
            return;
        }

        var password = configuration["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Set Demo:Password before initializing hosted demo accounts.");
            password = "Demo123!";
        }

        var agency = new Agency
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Camden County EMS",
            Region = "NJ-South"
        };

        var hospital = new Hospital
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Cooper University Hospital",
            City = "Camden"
        };

        var hospital2 = new Hospital
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Virtua Our Lady of Lourdes",
            City = "Camden"
        };

        db.Agencies.Add(agency);
        db.Hospitals.AddRange(hospital, hospital2);
        await db.SaveChangesAsync();

        await CreateUserAsync(userManager, "paramedic@pulselink.demo", "Paramedic Demo", password, AppRoles.Paramedic, agency.Id, null);
        await CreateUserAsync(userManager, "hospital@pulselink.demo", "Hospital Demo", password, AppRoles.HospitalStaff, null, hospital.Id);
        await CreateUserAsync(userManager, "admin@pulselink.demo", "Admin Demo", password, AppRoles.Admin, null, null);
    }

    private static async Task CreateUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string displayName,
        string password,
        string role,
        Guid? agencyId,
        Guid? hospitalId)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            AgencyId = agencyId,
            HospitalId = hospitalId
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);
    }
}
