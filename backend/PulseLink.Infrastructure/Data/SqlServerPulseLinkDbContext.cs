using Microsoft.EntityFrameworkCore;

namespace PulseLink.Infrastructure.Data;

public class SqlServerPulseLinkDbContext(DbContextOptions<SqlServerPulseLinkDbContext> options)
    : PulseLinkDbContext(options);
