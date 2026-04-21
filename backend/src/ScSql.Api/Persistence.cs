using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;

namespace ScSql.Api;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ConnectionProfile> Connections => Set<ConnectionProfile>();
    public DbSet<SqlScriptAsset> Scripts => Set<SqlScriptAsset>();
    public DbSet<ScheduledTaskDefinition> Tasks => Set<ScheduledTaskDefinition>();
    public DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var scheduleConverter = new JsonValueConverter<List<ScheduleSlot>>(jsonOptions, () => new List<ScheduleSlot>());
        var scheduleComparer = new JsonValueComparer<List<ScheduleSlot>>(jsonOptions, () => new List<ScheduleSlot>());
        var parameterConverter = new JsonValueConverter<List<TaskParameter>>(jsonOptions, () => new List<TaskParameter>());
        var parameterComparer = new JsonValueComparer<List<TaskParameter>>(jsonOptions, () => new List<TaskParameter>());
        var retryConverter = new JsonValueConverter<RetryPolicy>(jsonOptions, () => new RetryPolicy());
        var retryComparer = new JsonValueComparer<RetryPolicy>(jsonOptions, () => new RetryPolicy());

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Username).HasMaxLength(120);
            entity.HasIndex(candidate => candidate.Username).IsUnique();
            entity.Property(candidate => candidate.PasswordHash).HasMaxLength(200);
        });

        modelBuilder.Entity<ConnectionProfile>(entity =>
        {
            entity.ToTable("connections");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Name).HasMaxLength(120);
            entity.Property(candidate => candidate.Server).HasMaxLength(200);
            entity.Property(candidate => candidate.Database).HasMaxLength(120);
            entity.Property(candidate => candidate.Username).HasMaxLength(120);
            entity.Property(candidate => candidate.Password).HasMaxLength(400);
        });

        modelBuilder.Entity<SqlScriptAsset>(entity =>
        {
            entity.ToTable("scripts");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.OriginalName).HasMaxLength(255);
            entity.Property(candidate => candidate.StoredFileName).HasMaxLength(255);
            entity.Property(candidate => candidate.RelativePath).HasMaxLength(255);
        });

        modelBuilder.Entity<ScheduledTaskDefinition>(entity =>
        {
            entity.ToTable("tasks");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Name).HasMaxLength(150);
            entity.Property(candidate => candidate.StoredProcedureName).HasMaxLength(255);
            entity.Property(candidate => candidate.Parameters)
                .HasColumnType("json")
                .HasConversion(parameterConverter)
                .Metadata.SetValueComparer(parameterComparer);
            entity.Property(candidate => candidate.Schedules)
                .HasColumnType("json")
                .HasConversion(scheduleConverter)
                .Metadata.SetValueComparer(scheduleComparer);
            entity.Property(candidate => candidate.RetryPolicy)
                .HasColumnType("json")
                .HasConversion(retryConverter)
                .Metadata.SetValueComparer(retryComparer);
        });

        modelBuilder.Entity<ExecutionRecord>(entity =>
        {
            entity.ToTable("executions");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.TaskName).HasMaxLength(150);
            entity.Property(candidate => candidate.SuccessSummary).HasMaxLength(500);
            entity.Property(candidate => candidate.SuccessDetail).HasColumnType("longtext");
            entity.Property(candidate => candidate.ErrorSummary).HasMaxLength(500);
            entity.Property(candidate => candidate.ErrorDetail).HasColumnType("longtext");
            entity.HasIndex(candidate => candidate.StartedAtUtc);
            entity.HasIndex(candidate => candidate.TaskId);
        });
    }
}

public sealed class ApplicationBootstrapper
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly StorageOptions _storageOptions;
    private readonly AdminUserOptions _adminOptions;
    private readonly ConnectionSecretProtector _secretProtector;
    private readonly ILogger<ApplicationBootstrapper> _logger;

    public ApplicationBootstrapper(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        IOptions<StorageOptions> storageOptions,
        IOptions<AdminUserOptions> adminOptions,
        ConnectionSecretProtector secretProtector,
        ILogger<ApplicationBootstrapper> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _storageOptions = storageOptions.Value;
        _adminOptions = adminOptions.Value;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await WaitForDatabaseAsync(cancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureExecutionResponseColumnsAsync(cancellationToken);
        await ImportLegacyJsonStateAsync(cancellationToken);
        await ProtectConnectionSecretsAsync(cancellationToken);
        await EnsureAdminUserAsync(cancellationToken);
    }

    private async Task EnsureExecutionResponseColumnsAsync(CancellationToken cancellationToken)
    {
        await EnsureExecutionColumnAsync(
            "SuccessSummary",
            "ALTER TABLE executions ADD COLUMN SuccessSummary varchar(500) NULL",
            cancellationToken);

        await EnsureExecutionColumnAsync(
            "SuccessDetail",
            "ALTER TABLE executions ADD COLUMN SuccessDetail longtext NULL",
            cancellationToken);
    }

    private async Task EnsureExecutionColumnAsync(string columnName, string alterSql, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var existsCommand = connection.CreateCommand();
            existsCommand.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = 'executions'
                  AND column_name = @columnName
                """;

            var parameter = existsCommand.CreateParameter();
            parameter.ParameterName = "@columnName";
            parameter.Value = columnName;
            existsCommand.Parameters.Add(parameter);

            var existsResult = await existsCommand.ExecuteScalarAsync(cancellationToken);
            var columnExists = Convert.ToInt32(existsResult) > 0;
            if (columnExists)
            {
                return;
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = alterSql;
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                if (await _dbContext.Database.CanConnectAsync(cancellationToken))
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogWarning(exception, "Database connection attempt {Attempt} failed", attempt);
            }

            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("No se pudo conectar a la base interna MySQL.", lastException);
    }

    private async Task ImportLegacyJsonStateAsync(CancellationToken cancellationToken)
    {
        var isDatabaseEmpty = !await _dbContext.Users.AnyAsync(cancellationToken)
            && !await _dbContext.Connections.AnyAsync(cancellationToken)
            && !await _dbContext.Scripts.AnyAsync(cancellationToken)
            && !await _dbContext.Tasks.AnyAsync(cancellationToken)
            && !await _dbContext.Executions.AnyAsync(cancellationToken);

        if (!isDatabaseEmpty)
        {
            return;
        }

        var rootPath = Path.IsPathRooted(_storageOptions.RootPath)
            ? _storageOptions.RootPath
            : Path.Combine(_environment.ContentRootPath, _storageOptions.RootPath);
        var legacyFilePath = Path.Combine(rootPath, "app-state.json");
        if (!File.Exists(legacyFilePath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(legacyFilePath, cancellationToken);
        var state = JsonSerializer.Deserialize<AppState>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (state is null)
        {
            return;
        }

        _dbContext.Users.AddRange(state.Users);
        _dbContext.Connections.AddRange(state.Connections);
        _dbContext.Scripts.AddRange(state.Scripts);
        _dbContext.Tasks.AddRange(state.Tasks);
        _dbContext.Executions.AddRange(state.Executions);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var importedFilePath = Path.Combine(rootPath, $"app-state.imported-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        File.Move(legacyFilePath, importedFilePath);
        _logger.LogInformation("Imported legacy JSON state into MySQL and moved file to {ImportedFilePath}", importedFilePath);
    }

    private async Task ProtectConnectionSecretsAsync(CancellationToken cancellationToken)
    {
        var connections = await _dbContext.Connections.ToListAsync(cancellationToken);
        var updated = 0;

        foreach (var connection in connections)
        {
            if (string.IsNullOrWhiteSpace(connection.Password) || _secretProtector.IsProtected(connection.Password))
            {
                continue;
            }

            connection.Password = _secretProtector.Protect(connection.Password);
            updated++;
        }

        if (updated == 0)
        {
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Protected {Count} legacy connection secrets in MySQL.", updated);
    }

    private async Task EnsureAdminUserAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.Users.Add(new AppUser
        {
            Username = _adminOptions.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(_adminOptions.Password)
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class JsonValueConverter<TValue> : ValueConverter<TValue, string>
{
    public JsonValueConverter(JsonSerializerOptions options, Func<TValue> fallbackFactory)
        : base(
            value => JsonSerializer.Serialize(value, options),
            value => string.IsNullOrWhiteSpace(value)
                ? fallbackFactory()
                : JsonSerializer.Deserialize<TValue>(value, options) ?? fallbackFactory())
    {
    }
}

internal sealed class JsonValueComparer<TValue> : ValueComparer<TValue>
{
    public JsonValueComparer(JsonSerializerOptions options, Func<TValue> fallbackFactory)
        : base(
            (left, right) => Serialize(left, options) == Serialize(right, options),
            value => Serialize(value, options).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<TValue>(Serialize(value, options), options) ?? fallbackFactory())
    {
    }

    private static string Serialize(TValue? value, JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(value, options);
    }
}