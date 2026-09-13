using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AzureDeadLetterMonitor.Services;

public class AzureCliAccountInfo
{
    public string? User { get; set; }
    public string? Subscription { get; set; }
    public string? Tenant { get; set; }
}

public static class AzureCliHelper
{
    public static async Task<AzureCliAccountInfo?> GetCurrentAccountAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await RunAzCommandAsync("account show --query \"{user: user.name, subscription: name, tenant: tenantDisplayName}\" -o json", ct);
            if (string.IsNullOrWhiteSpace(output)) return null;

            return JsonSerializer.Deserialize<AzureCliAccountInfo>(output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public static async Task<IReadOnlyList<string>> ListServiceBusNamespacesAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await RunAzCommandAsync("servicebus namespace list --query \"[].name\" -o json", ct);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<string>();

            var names = JsonSerializer.Deserialize<List<string>>(output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (names == null) return Array.Empty<string>();

            return names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase) ? n : $"{n}.servicebus.windows.net")
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static async Task<string> RunAzCommandAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        await process.WaitForExitAsync(cts.Token);
        var stdout = await stdoutTask;
        return process.ExitCode == 0 ? stdout : string.Empty;
    }
}
