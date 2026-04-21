using System.ComponentModel.DataAnnotations;

namespace ScSql.Api;

public enum DatabaseEngine
{
    MySql,
    SqlServer
}

public enum TaskSourceKind
{
    SqlFile,
    StoredProcedure
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Retrying
}

public enum TaskParameterType
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime
}

public sealed class AppState
{
    public List<ConnectionProfile> Connections { get; set; } = new();
    public List<SqlScriptAsset> Scripts { get; set; } = new();
    public List<ScheduledTaskDefinition> Tasks { get; set; } = new();
    public List<ExecutionRecord> Executions { get; set; } = new();
    public List<AppUser> Users { get; set; } = new();
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ConnectionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DatabaseEngine Engine { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool TrustServerCertificate { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class ConnectionProfileResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseEngine Engine { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool TrustServerCertificate { get; set; }
    public bool Enabled { get; set; }
}

public sealed class SqlScriptAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ScheduledTaskDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid ConnectionId { get; set; }
    public DatabaseEngine Engine { get; set; }
    public TaskSourceKind SourceKind { get; set; }
    public Guid? SqlScriptId { get; set; }
    public string? StoredProcedureName { get; set; }
    public List<TaskParameter> Parameters { get; set; } = new();
    public bool Automatic { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ScheduleSlot> Schedules { get; set; } = new();
    public RetryPolicy RetryPolicy { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 300;
    public DateTimeOffset? LastScheduledRunUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TaskParameter
{
    public string Name { get; set; } = string.Empty;
    public TaskParameterType Type { get; set; } = TaskParameterType.String;
    public string Value { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}

public sealed class RetryPolicy
{
    public int MaxRetries { get; set; }
    public int DelayMinutes { get; set; }
}

public sealed class ScheduleSlot
{
    public DayOfWeek DayOfWeek { get; set; }
    public string Time { get; set; } = "08:00";
}

public sealed class ExecutionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public bool ManualTrigger { get; set; }
    public int Attempts { get; set; }
    public string? ErrorSummary { get; set; }
    public string? ErrorDetail { get; set; }
    public long? DurationMs { get; set; }
    public int? RowsAffected { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class CreateConnectionRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DatabaseEngine Engine { get; set; }

    [Required]
    public string Server { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; }
}

public sealed class UpdateConnectionRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DatabaseEngine Engine { get; set; }

    [Required]
    public string Server { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool TrustServerCertificate { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class UpdateScriptRequest
{
    [Required]
    public string OriginalName { get; set; } = string.Empty;
}

public sealed class CreateTaskRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public Guid ConnectionId { get; set; }
    public DatabaseEngine Engine { get; set; }
    public TaskSourceKind SourceKind { get; set; }
    public Guid? SqlScriptId { get; set; }
    public string? StoredProcedureName { get; set; }
    public List<TaskParameter> Parameters { get; set; } = new();
    public bool Automatic { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ScheduleSlot> Schedules { get; set; } = new();
    public RetryPolicy RetryPolicy { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 300;
}

public sealed class DashboardResponse
{
    public int ActiveTasks { get; set; }
    public int TotalConnections { get; set; }
    public int FailedExecutionsLast7Days { get; set; }
    public List<ScheduledTaskDefinition> UpcomingTasks { get; set; } = new();
    public List<ExecutionRecord> RecentExecutions { get; set; } = new();
}

public sealed class DeleteExecutionsRequest
{
    [MinLength(1)]
    public List<Guid> Ids { get; set; } = new();
}