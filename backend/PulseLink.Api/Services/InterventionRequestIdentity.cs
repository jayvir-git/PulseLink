using System.Security.Cryptography;
using System.Text.Json;
using PulseLink.Api.Dtos;

namespace PulseLink.Api.Services;

internal static class InterventionRequestIdentity
{
    public static string Fingerprint(AddInterventionRequest request) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new string?[]
        {
            "intervention-v1", request.Name.Trim(), request.Medication,
            request.Dose, request.Route, request.Notes
        })));
}
