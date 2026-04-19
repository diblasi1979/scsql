using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

namespace ScSql.Api;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

public sealed class AdminUserOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "storage";
    public string SqlPath { get; set; } = "storage/sql";
}

public sealed class SchedulerOptions
{
    public int PollSeconds { get; set; } = 5;
    public int CatchUpWindowDays { get; set; } = 7;
    public string TimeZoneId { get; set; } = "UTC";
}

public sealed class ConnectionSecurityOptions
{
    public string EncryptionKey { get; set; } = "change-this-32-byte-connection-key";
}

public sealed class ConnectionSecretProtector
{
    private const string Prefix = "enc::";
    private readonly byte[] _keyBytes;

    public ConnectionSecretProtector(IOptions<ConnectionSecurityOptions> options)
    {
        var rawKey = options.Value.EncryptionKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException("ConnectionSecurity:EncryptionKey no puede estar vacío.");
        }

        _keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        if (IsProtected(plaintext))
        {
            return plaintext;
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_keyBytes, 16);
        aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);
        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return string.Empty;
        }

        if (!IsProtected(protectedValue))
        {
            return protectedValue;
        }

        var payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        if (payload.Length < 28)
        {
            throw new InvalidOperationException("El secreto cifrado de conexión no tiene un formato válido.");
        }

        var nonce = payload[..12];
        var tag = payload[12..28];
        var cipherBytes = payload[28..];
        var plaintextBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_keyBytes, 16);
        aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public bool IsProtected(string value)
    {
        return value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}

public sealed class SqlFileStore
{
    private readonly string _sqlPath;

    public SqlFileStore(IOptions<StorageOptions> options, IWebHostEnvironment environment)
    {
        _sqlPath = Path.IsPathRooted(options.Value.SqlPath)
            ? options.Value.SqlPath
            : Path.Combine(environment.ContentRootPath, options.Value.SqlPath);
        Directory.CreateDirectory(_sqlPath);
    }

    public async Task<SqlScriptAsset> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var identifier = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{identifier:N}{extension}";
        var absolutePath = Path.Combine(_sqlPath, storedFileName);

        await using var fileStream = File.Create(absolutePath);
        await file.CopyToAsync(fileStream, cancellationToken);

        return new SqlScriptAsset
        {
            Id = identifier,
            OriginalName = file.FileName,
            StoredFileName = storedFileName,
            RelativePath = Path.Combine("sql", storedFileName).Replace('\\', '/')
        };
    }

    public async Task ReplaceAsync(SqlScriptAsset script, IFormFile file, CancellationToken cancellationToken)
    {
        var oldAbsolutePath = Path.Combine(_sqlPath, script.StoredFileName);
        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{script.Id:N}{extension}";
        var newAbsolutePath = Path.Combine(_sqlPath, storedFileName);

        await using (var fileStream = File.Create(newAbsolutePath))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        if (!string.Equals(oldAbsolutePath, newAbsolutePath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldAbsolutePath))
        {
            File.Delete(oldAbsolutePath);
        }

        script.StoredFileName = storedFileName;
        script.RelativePath = Path.Combine("sql", storedFileName).Replace('\\', '/');
        script.UploadedAtUtc = DateTimeOffset.UtcNow;
    }

    public Task DeleteAsync(SqlScriptAsset script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = Path.Combine(_sqlPath, script.StoredFileName);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public async Task<string> ReadScriptAsync(SqlScriptAsset script, CancellationToken cancellationToken)
    {
        var absolutePath = Path.Combine(_sqlPath, script.StoredFileName);
        return await File.ReadAllTextAsync(absolutePath, cancellationToken);
    }
}

public sealed class AuthService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public AuthService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<string?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class ExecutionCoordinator
{
    private readonly ConcurrentDictionary<Guid, byte> _runningTasks = new();

    public bool TryStart(Guid taskId)
    {
        return _runningTasks.TryAdd(taskId, 0);
    }

    public void Complete(Guid taskId)
    {
        _runningTasks.TryRemove(taskId, out _);
    }
}

public interface IDatabaseExecutionEngine
{
    DatabaseEngine Engine { get; }
    Task<int> ExecuteSqlAsync(ConnectionProfile connection, string sql, int timeoutSeconds, CancellationToken cancellationToken);
    Task<int> ExecuteStoredProcedureAsync(ConnectionProfile connection, IReadOnlyCollection<TaskParameter> parameters, string procedureName, int timeoutSeconds, CancellationToken cancellationToken);
    Task TestConnectionAsync(ConnectionProfile connection, CancellationToken cancellationToken);
}

public sealed class MySqlExecutionEngine : IDatabaseExecutionEngine
{
    private readonly ConnectionSecretProtector _secretProtector;

    public MySqlExecutionEngine(ConnectionSecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public DatabaseEngine Engine => DatabaseEngine.MySql;

    public async Task<int> ExecuteSqlAsync(ConnectionProfile connection, string sql, int timeoutSeconds, CancellationToken cancellationToken)
    {
        await using var db = new MySqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, db)
        {
            CommandTimeout = timeoutSeconds
        };
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> ExecuteStoredProcedureAsync(ConnectionProfile connection, IReadOnlyCollection<TaskParameter> parameters, string procedureName, int timeoutSeconds, CancellationToken cancellationToken)
    {
        await using var db = new MySqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(procedureName, db)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = timeoutSeconds
        };

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, TaskParameterValueParser.ToDatabaseValue(parameter));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TestConnectionAsync(ConnectionProfile connection, CancellationToken cancellationToken)
    {
        await using var db = new MySqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
    }

    private string BuildConnectionString(ConnectionProfile connection)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = connection.Server,
            Port = (uint)connection.Port,
            Database = connection.Database,
            UserID = connection.Username,
            Password = _secretProtector.Unprotect(connection.Password),
            AllowUserVariables = true
        };

        return builder.ConnectionString;
    }
}

public sealed class SqlServerExecutionEngine : IDatabaseExecutionEngine
{
    private readonly ConnectionSecretProtector _secretProtector;

    public SqlServerExecutionEngine(ConnectionSecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public DatabaseEngine Engine => DatabaseEngine.SqlServer;

    public async Task<int> ExecuteSqlAsync(ConnectionProfile connection, string sql, int timeoutSeconds, CancellationToken cancellationToken)
    {
        await using var db = new SqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, db)
        {
            CommandTimeout = timeoutSeconds
        };
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> ExecuteStoredProcedureAsync(ConnectionProfile connection, IReadOnlyCollection<TaskParameter> parameters, string procedureName, int timeoutSeconds, CancellationToken cancellationToken)
    {
        await using var db = new SqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(procedureName, db)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = timeoutSeconds
        };

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, TaskParameterValueParser.ToDatabaseValue(parameter));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TestConnectionAsync(ConnectionProfile connection, CancellationToken cancellationToken)
    {
        await using var db = new SqlConnection(BuildConnectionString(connection));
        await db.OpenAsync(cancellationToken);
    }

    private string BuildConnectionString(ConnectionProfile connection)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{connection.Server},{connection.Port}",
            InitialCatalog = connection.Database,
            UserID = connection.Username,
            Password = _secretProtector.Unprotect(connection.Password),
            TrustServerCertificate = connection.TrustServerCertificate,
            Encrypt = !connection.TrustServerCertificate
        };

        return builder.ConnectionString;
    }
}

public static class TaskParameterValueParser
{
    public static object ToDatabaseValue(TaskParameter parameter)
    {
        if (!TryParse(parameter, out var value, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value ?? DBNull.Value;
    }

    public static bool TryParse(TaskParameter parameter, out object? value, out string? errorMessage)
    {
        var rawValue = parameter.Value?.Trim() ?? string.Empty;

        if (parameter.IsNullable && string.IsNullOrWhiteSpace(rawValue))
        {
            value = DBNull.Value;
            errorMessage = null;
            return true;
        }

        if (!parameter.IsNullable && parameter.Type != TaskParameterType.String && string.IsNullOrWhiteSpace(rawValue))
        {
            value = null;
            errorMessage = $"El parámetro '{parameter.Name}' requiere un valor.";
            return false;
        }

        switch (parameter.Type)
        {
            case TaskParameterType.String:
                value = parameter.Value ?? string.Empty;
                errorMessage = null;
                return true;
            case TaskParameterType.Integer:
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                {
                    value = integerValue;
                    errorMessage = null;
                    return true;
                }

                value = null;
                errorMessage = $"El parámetro '{parameter.Name}' debe ser un entero válido.";
                return false;
            case TaskParameterType.Decimal:
                if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    value = decimalValue;
                    errorMessage = null;
                    return true;
                }

                value = null;
                errorMessage = $"El parámetro '{parameter.Name}' debe ser un decimal válido usando punto como separador.";
                return false;
            case TaskParameterType.Boolean:
                if (TryParseBoolean(rawValue, out var booleanValue))
                {
                    value = booleanValue;
                    errorMessage = null;
                    return true;
                }

                value = null;
                errorMessage = $"El parámetro '{parameter.Name}' debe ser true, false, 1 o 0.";
                return false;
            case TaskParameterType.DateTime:
                if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var dateTimeValue))
                {
                    value = dateTimeValue;
                    errorMessage = null;
                    return true;
                }

                value = null;
                errorMessage = $"El parámetro '{parameter.Name}' debe ser una fecha válida en formato ISO 8601 o compatible.";
                return false;
            default:
                value = null;
                errorMessage = $"El parámetro '{parameter.Name}' usa un tipo no soportado.";
                return false;
        }
    }

    private static bool TryParseBoolean(string rawValue, out bool result)
    {
        if (bool.TryParse(rawValue, out result))
        {
            return true;
        }

        if (rawValue == "1")
        {
            result = true;
            return true;
        }

        if (rawValue == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}

public sealed class ExecutionService
{
    private readonly AppDbContext _dbContext;
    private readonly SqlFileStore _sqlFileStore;
    private readonly IReadOnlyDictionary<DatabaseEngine, IDatabaseExecutionEngine> _engines;
    private readonly ExecutionCoordinator _executionCoordinator;

    public ExecutionService(
        AppDbContext dbContext,
        SqlFileStore sqlFileStore,
        IEnumerable<IDatabaseExecutionEngine> engines,
        ExecutionCoordinator executionCoordinator)
    {
        _dbContext = dbContext;
        _sqlFileStore = sqlFileStore;
        _engines = engines.ToDictionary(engine => engine.Engine);
        _executionCoordinator = executionCoordinator;
    }

    public async Task<ExecutionRecord> RunTaskAsync(Guid taskId, bool manualTrigger, CancellationToken cancellationToken)
    {
        if (!_executionCoordinator.TryStart(taskId))
        {
            throw new InvalidOperationException("La tarea ya se encuentra en ejecución.");
        }

        try
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken)
                ?? throw new InvalidOperationException("Tarea no encontrada.");
            var connection = await _dbContext.Connections
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == task.ConnectionId, cancellationToken)
                ?? throw new InvalidOperationException("Conexión no encontrada.");

            var execution = new ExecutionRecord
            {
                TaskId = task.Id,
                TaskName = task.Name,
                ManualTrigger = manualTrigger,
                Status = ExecutionStatus.Running,
                Attempts = 0,
                StartedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.Executions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            Exception? lastException = null;

            for (var attempt = 0; attempt <= task.RetryPolicy.MaxRetries; attempt++)
            {
                execution.Attempts = attempt + 1;
                execution.Status = attempt == 0 ? ExecutionStatus.Running : ExecutionStatus.Retrying;
                await _dbContext.SaveChangesAsync(cancellationToken);

                try
                {
                    var rowsAffected = await ExecuteTaskCoreAsync(task, connection, cancellationToken);
                    stopwatch.Stop();
                    execution.RowsAffected = rowsAffected;
                    execution.Status = ExecutionStatus.Success;
                    execution.DurationMs = stopwatch.ElapsedMilliseconds;
                    execution.FinishedAtUtc = DateTimeOffset.UtcNow;
                    execution.ErrorSummary = null;
                    execution.ErrorDetail = null;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return execution;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    execution.ErrorSummary = exception.Message;
                    execution.ErrorDetail = exception.ToString();
                    execution.Status = attempt < task.RetryPolicy.MaxRetries ? ExecutionStatus.Retrying : ExecutionStatus.Failed;
                    execution.FinishedAtUtc = attempt < task.RetryPolicy.MaxRetries ? null : DateTimeOffset.UtcNow;
                    execution.DurationMs = stopwatch.ElapsedMilliseconds;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (attempt < task.RetryPolicy.MaxRetries)
                    {
                        var delay = TimeSpan.FromMinutes(Math.Max(task.RetryPolicy.DelayMinutes, 1));
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            stopwatch.Stop();
            execution.Status = ExecutionStatus.Failed;
            execution.DurationMs = stopwatch.ElapsedMilliseconds;
            execution.FinishedAtUtc = DateTimeOffset.UtcNow;
            execution.ErrorSummary = lastException?.Message;
            execution.ErrorDetail = lastException?.ToString();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return execution;
        }
        finally
        {
            _executionCoordinator.Complete(taskId);
        }
    }

    private async Task<int> ExecuteTaskCoreAsync(ScheduledTaskDefinition task, ConnectionProfile connection, CancellationToken cancellationToken)
    {
        var engine = _engines[task.Engine];
        return task.SourceKind switch
        {
            TaskSourceKind.SqlFile => await ExecuteSqlFileAsync(task, connection, engine, cancellationToken),
            TaskSourceKind.StoredProcedure => await engine.ExecuteStoredProcedureAsync(
                connection,
                task.Parameters,
                task.StoredProcedureName ?? throw new InvalidOperationException("La tarea no tiene stored procedure configurado."),
                task.TimeoutSeconds,
                cancellationToken),
            _ => throw new InvalidOperationException("Tipo de tarea no soportado.")
        };
    }

    private async Task<int> ExecuteSqlFileAsync(ScheduledTaskDefinition task, ConnectionProfile connection, IDatabaseExecutionEngine engine, CancellationToken cancellationToken)
    {
        var script = await _dbContext.Scripts
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == task.SqlScriptId, cancellationToken)
            ?? throw new InvalidOperationException("Script SQL no encontrado.");
        var sql = await _sqlFileStore.ReadScriptAsync(script, cancellationToken);
        return await engine.ExecuteSqlAsync(connection, sql, task.TimeoutSeconds, cancellationToken);
    }
}

public sealed class SchedulerService
{
    private readonly AppDbContext _dbContext;
    private readonly SchedulerOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public SchedulerService(AppDbContext dbContext, IOptions<SchedulerOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _timeZone = ResolveTimeZone(_options.TimeZoneId);
    }

    public async Task<IReadOnlyList<Guid>> ClaimDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tasks = await _dbContext.Tasks
            .Where(candidate => candidate.Automatic && candidate.Enabled)
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var claimedTaskIds = new List<Guid>();
        foreach (var task in tasks)
        {
            while (TryGetNextDueOccurrence(task, now, out var dueOccurrence))
            {
                task.LastScheduledRunUtc = dueOccurrence;
                claimedTaskIds.Add(task.Id);
            }
        }

        if (claimedTaskIds.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return claimedTaskIds;
    }

    private bool TryGetNextDueOccurrence(ScheduledTaskDefinition task, DateTimeOffset now, out DateTimeOffset dueOccurrence)
    {
        dueOccurrence = default;
        if (task.Schedules.Count == 0)
        {
            return false;
        }

        var zonedNow = TimeZoneInfo.ConvertTime(now, _timeZone);
        var localNow = zonedNow.DateTime;
        var baseline = TimeZoneInfo.ConvertTime(task.LastScheduledRunUtc ?? task.CreatedAtUtc, _timeZone).DateTime;
        var searchStartDate = baseline.Date.AddDays(-Math.Max(0, _options.CatchUpWindowDays - 1));
        if (searchStartDate > localNow.Date)
        {
            searchStartDate = localNow.Date;
        }

        DateTime? nextOccurrence = null;
        for (var currentDate = searchStartDate.Date; currentDate <= localNow.Date; currentDate = currentDate.AddDays(1))
        {
            foreach (var schedule in task.Schedules)
            {
                if (schedule.DayOfWeek != currentDate.DayOfWeek || !TimeSpan.TryParse(schedule.Time, out var scheduleTime))
                {
                    continue;
                }

                var occurrence = currentDate.Add(scheduleTime);
                if (occurrence <= baseline || occurrence > localNow)
                {
                    continue;
                }

                if (nextOccurrence is null || occurrence < nextOccurrence.Value)
                {
                    nextOccurrence = occurrence;
                }
            }
        }

        if (nextOccurrence is null)
        {
            return false;
        }

        dueOccurrence = new DateTimeOffset(nextOccurrence.Value, _timeZone.GetUtcOffset(nextOccurrence.Value));
        return true;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

public sealed class SchedulerWorker : BackgroundService
{
    private readonly ILogger<SchedulerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulerOptions _options;

    public SchedulerWorker(ILogger<SchedulerWorker> logger, IServiceScopeFactory scopeFactory, IOptions<SchedulerOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(_options.PollSeconds, 2)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var schedulerService = scope.ServiceProvider.GetRequiredService<SchedulerService>();
                var dueTaskIds = await schedulerService.ClaimDueTasksAsync(DateTimeOffset.Now, stoppingToken);
                foreach (var taskId in dueTaskIds)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var runScope = _scopeFactory.CreateScope();
                            var executionService = runScope.ServiceProvider.GetRequiredService<ExecutionService>();
                            await executionService.RunTaskAsync(taskId, manualTrigger: false, CancellationToken.None);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Error ejecutando la tarea programada {TaskId}", taskId);
                        }
                    }, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Scheduler cycle failed");
            }
        }
    }
}