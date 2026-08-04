using System.Text;
using MedicalAssistant.EventBusRabbitMQ;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqForwardedHeaderSanitizerTests
{
    [Fact]
    public void Retry_and_dead_letter_headers_keep_trace_context_and_drop_phi_secret_canaries()
    {
        const string filenameCanary = "patient-smith-referral.pdf";
        const string blobPathCanary = "private://consultations/42/audio/raw.wav";
        const string transcriptCanary = "patient says chest pain started after dinner";
        const string providerBodyCanary = "Azure Speech provider body with patient transcript";
        const string secretCanary = "Bearer super-secret-token";
        var sourceHeaders = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RabbitMqTelemetryHeaders.TraceParent] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            [RabbitMqTelemetryHeaders.TraceState] = "rojo=00f067aa0ba902b7",
            ["x-original-filename"] = filenameCanary,
            ["x-blob-uri"] = Encoding.UTF8.GetBytes(blobPathCanary),
            ["x-transcript-preview"] = transcriptCanary,
            ["x-provider-error-body"] = providerBodyCanary,
            ["authorization"] = secretCanary
        };

        var sanitized = RabbitMqForwardedHeaderSanitizer.SanitizeForRetryOrDeadLetter(
            sourceHeaders,
            retryAttempt: 2);

        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            sanitized[RabbitMqTelemetryHeaders.TraceParent]);
        Assert.Equal("rojo=00f067aa0ba902b7", sanitized[RabbitMqTelemetryHeaders.TraceState]);
        Assert.Equal(2, sanitized[RabbitMqTelemetryHeaders.RetryAttempt]);
        Assert.DoesNotContain(sanitized, pair => HeaderContains(pair, filenameCanary));
        Assert.DoesNotContain(sanitized, pair => HeaderContains(pair, blobPathCanary));
        Assert.DoesNotContain(sanitized, pair => HeaderContains(pair, transcriptCanary));
        Assert.DoesNotContain(sanitized, pair => HeaderContains(pair, providerBodyCanary));
        Assert.DoesNotContain(sanitized, pair => HeaderContains(pair, secretCanary));
    }

    private static bool HeaderContains(KeyValuePair<string, object?> header, string canary)
    {
        if (header.Key.Contains(canary, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return header.Value switch
        {
            string text => text.Contains(canary, StringComparison.OrdinalIgnoreCase),
            byte[] bytes => Encoding.UTF8.GetString(bytes).Contains(canary, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
