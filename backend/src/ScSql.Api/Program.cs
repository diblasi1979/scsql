using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ScSql.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminUserOptions>(builder.Configuration.GetSection("AdminUser"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection("Scheduler"));
builder.Services.Configure<ConnectionSecurityOptions>(builder.Configuration.GetSection("ConnectionSecurity"));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var internalConnectionString = builder.Configuration.GetConnectionString("InternalDatabase")
    ?? throw new InvalidOperationException("Missing connection string 'InternalDatabase'.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(
        internalConnectionString,
        ServerVersion.AutoDetect(internalConnectionString),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure());
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ExecutionService>();
builder.Services.AddScoped<SchedulerService>();
builder.Services.AddScoped<ApplicationBootstrapper>();
builder.Services.AddSingleton<ConnectionSecretProtector>();
builder.Services.AddSingleton<SqlFileStore>();
builder.Services.AddSingleton<ExecutionCoordinator>();
builder.Services.AddSingleton<IDatabaseExecutionEngine, MySqlExecutionEngine>();
builder.Services.AddSingleton<IDatabaseExecutionEngine, SqlServerExecutionEngine>();
builder.Services.AddHostedService<SchedulerWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ApplicationBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return Results.Ok(new { status = canConnect ? "ok" : "degraded" });
});

app.MapPost("/api/auth/login", async (LoginRequest request, AuthService authService, CancellationToken cancellationToken) =>
{
    var token = await authService.LoginAsync(request, cancellationToken);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new { token });
});

var adminGroup = app.MapGroup("/api")
    .RequireAuthorization(new AuthorizeAttribute());

adminGroup.MapGet("/dashboard", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var recentExecutions = await dbContext.Executions
        .AsNoTracking()
        .OrderByDescending(execution => execution.StartedAtUtc)
        .Take(10)
        .ToListAsync(cancellationToken);
    var upcomingTasks = await dbContext.Tasks
        .AsNoTracking()
        .Where(task => task.Enabled)
        .OrderBy(task => task.Name)
        .Take(10)
        .ToListAsync(cancellationToken);
    var response = new DashboardResponse
    {
        ActiveTasks = await dbContext.Tasks.CountAsync(task => task.Enabled, cancellationToken),
        TotalConnections = await dbContext.Connections.CountAsync(cancellationToken),
        FailedExecutionsLast7Days = await dbContext.Executions.CountAsync(execution => execution.Status == ExecutionStatus.Failed && execution.StartedAtUtc >= DateTimeOffset.UtcNow.AddDays(-7), cancellationToken),
        RecentExecutions = recentExecutions,
        UpcomingTasks = upcomingTasks
    };
    return Results.Ok(response);
});

adminGroup.MapGet("/connections", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var connections = await dbContext.Connections.AsNoTracking().OrderBy(candidate => candidate.Name).ToListAsync(cancellationToken);
    return Results.Ok(connections.Select(ToConnectionResponse));
});

adminGroup.MapPost("/connections", async (CreateConnectionRequest request, AppDbContext dbContext, ConnectionSecretProtector secretProtector, CancellationToken cancellationToken) =>
{
    var connection = new ConnectionProfile
    {
        Name = request.Name,
        Engine = request.Engine,
        Server = request.Server,
        Port = request.Port,
        Database = request.Database,
        Username = request.Username,
        Password = secretProtector.Protect(request.Password),
        TrustServerCertificate = request.TrustServerCertificate
    };
    dbContext.Connections.Add(connection);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/connections/{connection.Id}", ToConnectionResponse(connection));
});

adminGroup.MapPut("/connections/{id:guid}", async (Guid id, UpdateConnectionRequest request, AppDbContext dbContext, ConnectionSecretProtector secretProtector, CancellationToken cancellationToken) =>
{
    var connection = await dbContext.Connections.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (connection is null)
    {
        return Results.NotFound();
    }

    connection.Name = request.Name;
    connection.Engine = request.Engine;
    connection.Server = request.Server;
    connection.Port = request.Port;
    connection.Database = request.Database;
    connection.Username = request.Username;
    connection.TrustServerCertificate = request.TrustServerCertificate;
    connection.Enabled = request.Enabled;

    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        connection.Password = secretProtector.Protect(request.Password);
    }

    var relatedTasks = await dbContext.Tasks.Where(candidate => candidate.ConnectionId == id).ToListAsync(cancellationToken);
    foreach (var task in relatedTasks)
    {
        task.Engine = request.Engine;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToConnectionResponse(connection));
});

adminGroup.MapDelete("/connections/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var connection = await dbContext.Connections.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (connection is null)
    {
        return Results.NotFound();
    }

    var isUsedByTasks = await dbContext.Tasks.AnyAsync(candidate => candidate.ConnectionId == id, cancellationToken);
    if (isUsedByTasks)
    {
        return Results.Conflict(new { message = "No se puede eliminar una conexión asociada a tareas existentes." });
    }

    dbContext.Connections.Remove(connection);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

adminGroup.MapPost("/connections/{id:guid}/test", async (Guid id, AppDbContext dbContext, IEnumerable<IDatabaseExecutionEngine> engines, CancellationToken cancellationToken) =>
{
    var connection = await dbContext.Connections.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (connection is null)
    {
        return Results.NotFound();
    }

    var engine = engines.First(candidate => candidate.Engine == connection.Engine);
    await engine.TestConnectionAsync(connection, cancellationToken);
    return Results.Ok(new { success = true });
});

adminGroup.MapGet("/scripts", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var scripts = await dbContext.Scripts.AsNoTracking().OrderByDescending(candidate => candidate.UploadedAtUtc).ToListAsync(cancellationToken);
    return Results.Ok(scripts);
});

adminGroup.MapPost("/scripts", async (HttpRequest request, AppDbContext dbContext, SqlFileStore fileStore, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Se esperaba multipart/form-data." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.FirstOrDefault();
    if (file is null)
    {
        return Results.BadRequest(new { message = "Debe adjuntar un archivo .sql." });
    }

    var saved = await fileStore.SaveAsync(file, cancellationToken);
    dbContext.Scripts.Add(saved);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/scripts/{saved.Id}", saved);
});

adminGroup.MapPut("/scripts/{id:guid}", async (Guid id, HttpRequest request, AppDbContext dbContext, SqlFileStore fileStore, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Se esperaba multipart/form-data." });
    }

    var script = await dbContext.Scripts.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (script is null)
    {
        return Results.NotFound();
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var originalName = form["originalName"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(originalName))
    {
        return Results.BadRequest(new { message = "Debe indicar el nombre del script." });
    }

    var file = form.Files.FirstOrDefault();
    script.OriginalName = originalName.Trim();

    if (file is not null)
    {
        await fileStore.ReplaceAsync(script, file, cancellationToken);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(script);
});

adminGroup.MapDelete("/scripts/{id:guid}", async (Guid id, AppDbContext dbContext, SqlFileStore fileStore, CancellationToken cancellationToken) =>
{
    var script = await dbContext.Scripts.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (script is null)
    {
        return Results.NotFound();
    }

    var isUsedByTasks = await dbContext.Tasks.AnyAsync(candidate => candidate.SqlScriptId == id, cancellationToken);
    if (isUsedByTasks)
    {
        return Results.Conflict(new { message = "No se puede eliminar un script asociado a tareas existentes." });
    }

    await fileStore.DeleteAsync(script, cancellationToken);
    dbContext.Scripts.Remove(script);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

adminGroup.MapGet("/tasks", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var tasks = await dbContext.Tasks.AsNoTracking().OrderBy(candidate => candidate.Name).ToListAsync(cancellationToken);
    return Results.Ok(tasks);
});

adminGroup.MapPost("/tasks", async (CreateTaskRequest request, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var validationError = ValidateTaskRequest(request);
    if (validationError is not null)
    {
        return validationError;
    }

    var connection = await dbContext.Connections.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == request.ConnectionId, cancellationToken);
    if (connection is null)
    {
        return Results.BadRequest(new { message = "La conexión seleccionada no existe." });
    }

    var task = new ScheduledTaskDefinition
    {
        Name = request.Name,
        ConnectionId = request.ConnectionId,
        Engine = connection.Engine,
        SourceKind = request.SourceKind,
        SqlScriptId = request.SqlScriptId,
        StoredProcedureName = request.StoredProcedureName,
        Parameters = request.Parameters,
        Automatic = request.Automatic,
        Enabled = request.Enabled,
        Schedules = request.Schedules,
        RetryPolicy = request.RetryPolicy,
        TimeoutSeconds = request.TimeoutSeconds
    };

    dbContext.Tasks.Add(task);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/tasks/{task.Id}", task);
});

adminGroup.MapPut("/tasks/{id:guid}", async (Guid id, CreateTaskRequest request, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var validationError = ValidateTaskRequest(request);
    if (validationError is not null)
    {
        return validationError;
    }

    var task = await dbContext.Tasks.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (task is null)
    {
        return Results.NotFound();
    }

    var connection = await dbContext.Connections.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == request.ConnectionId, cancellationToken);
    if (connection is null)
    {
        return Results.BadRequest(new { message = "La conexión seleccionada no existe." });
    }

    task.Name = request.Name;
    task.ConnectionId = request.ConnectionId;
    task.Engine = connection.Engine;
    task.SourceKind = request.SourceKind;
    task.SqlScriptId = request.SourceKind == TaskSourceKind.SqlFile ? request.SqlScriptId : null;
    task.StoredProcedureName = request.SourceKind == TaskSourceKind.StoredProcedure ? request.StoredProcedureName : null;
    task.Parameters = request.Parameters;
    task.Automatic = request.Automatic;
    task.Enabled = request.Enabled;
    task.Schedules = request.Schedules;
    task.RetryPolicy = request.RetryPolicy;
    task.TimeoutSeconds = request.TimeoutSeconds;
    task.LastScheduledRunUtc = request.Automatic ? DateTimeOffset.UtcNow : null;

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(task);
});

adminGroup.MapDelete("/tasks/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var task = await dbContext.Tasks.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    if (task is null)
    {
        return Results.NotFound();
    }

    var hasActiveExecution = await dbContext.Executions.AnyAsync(
        execution => execution.TaskId == id
            && execution.FinishedAtUtc == null
            && (execution.Status == ExecutionStatus.Pending || execution.Status == ExecutionStatus.Running || execution.Status == ExecutionStatus.Retrying),
        cancellationToken);

    if (hasActiveExecution)
    {
        return Results.Conflict(new { message = "No se puede eliminar una tarea con una ejecución en curso." });
    }

    dbContext.Tasks.Remove(task);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

adminGroup.MapPost("/tasks/{id:guid}/run", async (Guid id, ExecutionService executionService, CancellationToken cancellationToken) =>
{
    var execution = await executionService.RunTaskAsync(id, manualTrigger: true, cancellationToken);
    return Results.Ok(execution);
});

adminGroup.MapGet("/executions", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var executions = await dbContext.Executions
        .AsNoTracking()
        .OrderByDescending(execution => execution.StartedAtUtc)
        .Take(100)
        .ToListAsync(cancellationToken);
    return Results.Ok(executions);
});

static ConnectionProfileResponse ToConnectionResponse(ConnectionProfile connection)
{
    return new ConnectionProfileResponse
    {
        Id = connection.Id,
        Name = connection.Name,
        Engine = connection.Engine,
        Server = connection.Server,
        Port = connection.Port,
        Database = connection.Database,
        Username = connection.Username,
        Password = string.Empty,
        TrustServerCertificate = connection.TrustServerCertificate,
        Enabled = connection.Enabled
    };
}

static IResult? ValidateTaskRequest(CreateTaskRequest request)
{
    if (request.Automatic && request.Schedules.Count == 0)
    {
        return Results.BadRequest(new { message = "Una tarea automática debe tener al menos un horario." });
    }

    if (request.SourceKind == TaskSourceKind.SqlFile && request.SqlScriptId is null)
    {
        return Results.BadRequest(new { message = "Debe seleccionar un archivo SQL." });
    }

    if (request.SourceKind == TaskSourceKind.StoredProcedure && string.IsNullOrWhiteSpace(request.StoredProcedureName))
    {
        return Results.BadRequest(new { message = "Debe indicar el stored procedure." });
    }

    return null;
}

adminGroup.MapGet("/executions/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var execution = await dbContext.Executions.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    return execution is null ? Results.NotFound() : Results.Ok(execution);
});

app.Run();