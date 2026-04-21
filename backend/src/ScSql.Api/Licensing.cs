using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ScSql.Api;

public enum LicensePlan
{
    Perpetual,
    Subscription
}

public enum LicenseState
{
    Disabled,
    Active,
    Missing,
    Invalid,
    Expired,
    NotYetActive
}

public sealed class LicensingOptions
{
    public bool RequireValidLicense { get; set; }
    public string ProductCode { get; set; } = "scsql";
    public string PublicKeyPem { get; set; } = string.Empty;
    public string CurrentLicenseKey { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
}

public sealed class LicensePayload
{
    public string ProductCode { get; set; } = "scsql";
    public string CustomerName { get; set; } = string.Empty;
    public LicensePlan Plan { get; set; } = LicensePlan.Subscription;
    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NotBeforeUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string InstanceId { get; set; } = string.Empty;
}

public sealed class LicenseValidationSnapshot
{
    public LicenseState State { get; init; }
    public bool EnforcementEnabled { get; init; }
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public LicensePayload? Payload { get; init; }
    public bool ShouldBlock => EnforcementEnabled && !IsValid;
}

public sealed class LicenseStatusResponse
{
    public LicenseState State { get; init; }
    public bool EnforcementEnabled { get; init; }
    public bool IsValid { get; init; }
    public bool ShouldBlock { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ProductCode { get; init; } = "scsql";
    public string? CustomerName { get; init; }
    public string? InstanceId { get; init; }
    public LicensePlan? Plan { get; init; }
    public string PlanLabel { get; init; } = string.Empty;
    public DateTimeOffset? IssuedAtUtc { get; init; }
    public DateTimeOffset? NotBeforeUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class LicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LicensingOptions _options;
    private readonly string _publicKeyPem;

    public LicenseService(IOptions<LicensingOptions> options)
    {
        _options = options.Value;
        _publicKeyPem = NormalizePem(_options.PublicKeyPem);
    }

    public LicenseValidationSnapshot GetSnapshot(DateTimeOffset? now = null)
    {
        var currentInstant = now ?? DateTimeOffset.UtcNow;
        var currentLicenseKey = _options.CurrentLicenseKey?.Trim() ?? string.Empty;
        var hasLicenseConfig = !string.IsNullOrWhiteSpace(_publicKeyPem) || !string.IsNullOrWhiteSpace(currentLicenseKey);
        if (!_options.RequireValidLicense && !hasLicenseConfig)
        {
            return new LicenseValidationSnapshot
            {
                State = LicenseState.Disabled,
                EnforcementEnabled = false,
                IsValid = true,
                Message = "La validación de licencia está desactivada."
            };
        }

        if (string.IsNullOrWhiteSpace(_publicKeyPem))
        {
            return InvalidSnapshot(LicenseState.Missing, "Falta configurar Licensing:PublicKeyPem.");
        }

        if (string.IsNullOrWhiteSpace(currentLicenseKey))
        {
            return InvalidSnapshot(LicenseState.Missing, "Falta configurar Licensing:CurrentLicenseKey.");
        }

        var tokenParts = currentLicenseKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokenParts.Length != 2)
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia no tiene un formato válido.");
        }

        byte[] payloadBytes;
        byte[] signatureBytes;
        try
        {
            payloadBytes = WebEncoders.Base64UrlDecode(tokenParts[0]);
            signatureBytes = WebEncoders.Base64UrlDecode(tokenParts[1]);
        }
        catch (FormatException)
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia no se pudo decodificar.");
        }

        if (!VerifySignature(payloadBytes, signatureBytes))
        {
            return InvalidSnapshot(LicenseState.Invalid, "La firma digital de la licencia no es válida.");
        }

        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return InvalidSnapshot(LicenseState.Invalid, "El contenido de la licencia no tiene un JSON válido.");
        }

        if (payload is null)
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia está vacía o incompleta.");
        }

        var validation = ValidatePayload(payload, currentInstant);
        return validation;
    }

    public LicenseStatusResponse GetStatus(DateTimeOffset? now = null)
    {
        var snapshot = GetSnapshot(now);
        return new LicenseStatusResponse
        {
            State = snapshot.State,
            EnforcementEnabled = snapshot.EnforcementEnabled,
            IsValid = snapshot.IsValid,
            ShouldBlock = snapshot.ShouldBlock,
            Message = snapshot.Message,
            ProductCode = snapshot.Payload?.ProductCode ?? _options.ProductCode,
            CustomerName = snapshot.Payload?.CustomerName,
            InstanceId = snapshot.Payload?.InstanceId,
            Plan = snapshot.Payload?.Plan,
            PlanLabel = ToPlanLabel(snapshot.Payload?.Plan),
            IssuedAtUtc = snapshot.Payload?.IssuedAtUtc,
            NotBeforeUtc = snapshot.Payload?.NotBeforeUtc,
            ExpiresAtUtc = snapshot.Payload?.ExpiresAtUtc
        };
    }

    private LicenseValidationSnapshot ValidatePayload(LicensePayload payload, DateTimeOffset currentInstant)
    {
        if (!string.Equals(payload.ProductCode?.Trim(), _options.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia no corresponde a este producto.", payload);
        }

        if (string.IsNullOrWhiteSpace(payload.CustomerName))
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia no incluye el cliente licenciado.", payload);
        }

        if (payload.NotBeforeUtc.HasValue && payload.NotBeforeUtc.Value > currentInstant)
        {
            return InvalidSnapshot(LicenseState.NotYetActive, "La licencia todavía no se encuentra activa.", payload);
        }

        if (payload.Plan == LicensePlan.Subscription && payload.ExpiresAtUtc is null)
        {
            return InvalidSnapshot(LicenseState.Invalid, "La licencia de suscripción no incluye fecha de vencimiento.", payload);
        }

        if (payload.ExpiresAtUtc.HasValue && payload.ExpiresAtUtc.Value <= currentInstant)
        {
            return InvalidSnapshot(LicenseState.Expired, "La licencia está vencida.", payload);
        }

        var expectedInstanceId = _options.InstanceId.Trim();
        var licensedInstanceId = payload.InstanceId.Trim();
        if (!string.IsNullOrWhiteSpace(licensedInstanceId))
        {
            if (string.IsNullOrWhiteSpace(expectedInstanceId))
            {
                return InvalidSnapshot(LicenseState.Invalid, "La licencia exige configurar Licensing:InstanceId en el despliegue.", payload);
            }

            if (!string.Equals(licensedInstanceId, expectedInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                return InvalidSnapshot(LicenseState.Invalid, "La licencia no corresponde a esta instancia desplegada.", payload);
            }
        }

        return new LicenseValidationSnapshot
        {
            State = LicenseState.Active,
            EnforcementEnabled = _options.RequireValidLicense,
            IsValid = true,
            Message = payload.Plan == LicensePlan.Perpetual
                ? "Licencia perpetua válida."
                : "Licencia por suscripción válida.",
            Payload = payload
        };
    }

    private bool VerifySignature(byte[] payloadBytes, byte[] signatureBytes)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_publicKeyPem);
            return rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private LicenseValidationSnapshot InvalidSnapshot(LicenseState state, string message, LicensePayload? payload = null)
    {
        return new LicenseValidationSnapshot
        {
            State = state,
            EnforcementEnabled = _options.RequireValidLicense,
            IsValid = false,
            Message = message,
            Payload = payload
        };
    }

    private static string ToPlanLabel(LicensePlan? plan)
    {
        return plan switch
        {
            LicensePlan.Perpetual => "Perpetua",
            LicensePlan.Subscription => "Mensual",
            _ => "Sin licencia"
        };
    }

    private static string NormalizePem(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\r", string.Empty, StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}