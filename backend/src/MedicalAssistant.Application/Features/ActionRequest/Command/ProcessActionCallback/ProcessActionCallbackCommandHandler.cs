using MedicalAssistant.Application.Contracts.Persistence;
using MediatR;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.Features.ActionRequest.Command.ProcessActionCallback;

public class ProcessActionCallbackCommandHandler : IRequestHandler<ProcessActionCallbackCommand, Unit>
{
    private readonly IActionRequestRepository _actionRequestRepository;
    private readonly ILogger<ProcessActionCallbackCommandHandler> _logger;

    public ProcessActionCallbackCommandHandler(
        IActionRequestRepository actionRequestRepository,
        ILogger<ProcessActionCallbackCommandHandler> logger)
    {
        _actionRequestRepository = actionRequestRepository;
        _logger = logger;
    }

    private sealed class SendEmailDraft
    {
        public required List<string> To { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
    }

    private static bool TryParseSendEmailDraft(string? responsePayload, out SendEmailDraft? draft)
    {
        draft = null;
        if (string.IsNullOrWhiteSpace(responsePayload))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(responsePayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionEl))
                return false;

            var action = actionEl.GetString();
            if (!string.Equals(action, "SendEmail", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!root.TryGetProperty("to", out var toEl) || toEl.ValueKind != JsonValueKind.Array)
                return false;

            var to = new List<string>();
            foreach (var el in toEl.EnumerateArray())
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    to.Add(s);
            }

            var subject = root.TryGetProperty("subject", out var subjectEl)
                ? subjectEl.GetString()
                : null;

            var body = root.TryGetProperty("body", out var bodyEl)
                ? bodyEl.GetString()
                : null;

            if (to.Count == 0 || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
                return false;

            draft = new SendEmailDraft
            {
                To = to,
                Subject = subject!,
                Body = body!,
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TrySendEmailAsync(SendEmailDraft draft, CancellationToken cancellationToken)
    {
        // Configure via environment variables to avoid forcing appsettings changes.
        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        var smtpPortRaw = Environment.GetEnvironmentVariable("SMTP_PORT");
        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var fromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM");

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpPortRaw) ||
            string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogWarning("SMTP not configured (SMTP_HOST/SMTP_PORT/EMAIL_FROM missing). Skipping SendEmail.");
            return false;
        }

        if (!int.TryParse(smtpPortRaw, out var smtpPort))
        {
            _logger.LogWarning("Invalid SMTP_PORT value: {Value}", smtpPortRaw);
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = draft.Subject,
            Body = draft.Body,
            IsBodyHtml = false
        };

        foreach (var to in draft.To)
            message.To.Add(to);

        using var smtp = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true
        };

        if (!string.IsNullOrWhiteSpace(smtpUser) && smtpPassword != null)
        {
            smtp.Credentials = new NetworkCredential(smtpUser, smtpPassword);
        }

        // SmtpClient doesn’t accept cancellation tokens prior to newer frameworks.
        // We'll still respect cancellation by short-circuiting early.
        cancellationToken.ThrowIfCancellationRequested();

        await smtp.SendMailAsync(message);
        return true;
    }

    public async Task<Unit> Handle(ProcessActionCallbackCommand request, CancellationToken cancellationToken)
    {
        var actionRequest = await _actionRequestRepository.GetByCorrelationIdAsync(request.CorrelationId);
        if (actionRequest == null)
            return Unit.Value;

        if (actionRequest.Status is Domain.Enums.ActionRequestStatus.Completed)
            return Unit.Value;

        if (string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            var responsePayload = request.ResponsePayload ?? "{}";

            if (TryParseSendEmailDraft(responsePayload, out var draft) && draft != null)
            {
                var sent = await TrySendEmailAsync(draft, cancellationToken);
                if (!sent)
                {
                    actionRequest.MarkFailed("SendEmail skipped/failed: SMTP not configured or send error.");
                    await _actionRequestRepository.UpdateAsync(actionRequest);
                    return Unit.Value;
                }
            }

            actionRequest.MarkCompleted(responsePayload);
        }
        else if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            actionRequest.MarkFailed(request.FailureReason ?? "Action failed");
        }

        await _actionRequestRepository.UpdateAsync(actionRequest);
        return Unit.Value;
    }
}
