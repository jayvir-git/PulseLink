using Microsoft.EntityFrameworkCore;

namespace PulseLink.Infrastructure.Data;

public class SqlitePulseLinkDbContext(DbContextOptions<SqlitePulseLinkDbContext> options)
    : PulseLinkDbContext(options);
