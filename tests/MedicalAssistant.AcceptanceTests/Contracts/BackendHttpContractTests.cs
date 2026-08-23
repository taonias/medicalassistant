using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MediatR;
using MedicalAssistant.Application.Features.ActionRequest.Command.ProcessActionCallback;
using MedicalAssistant.Application.Features.Chat.Common;
using MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;
using MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;
using MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationAudio;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDocument;
using MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;
using MedicalAssistant.Application.Features.Transcript.Command.ProcessTranscriptionCallback;
using Moq;

namespace MedicalAssistant.AcceptanceTests.Contracts;

public sealed class BackendHttpContractTests
{
    [Fact]
    public async Task Recording_upload_binds_the_existing_audio_multipart_field_names()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<UploadConsultationAudioCommand>(command =>
                    command.ConsultationId == 42 &&
                    command.DurationSeconds == 37 &&
                    command.AudioFile.Name == "audioFile" &&
                    command.AudioFile.FileName == "capture.wav" &&
                    command.AudioFile.ContentType == "audio/wav"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consultation(42));
        using var client = CreateClient(factory);
        using var content = new MultipartFormDataContent();
        using var recording = new ByteArrayContent([1, 2, 3]);
        recording.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(recording, "audioFile", "capture.wav");
        content.Add(new StringContent("37"), "durationSeconds");

        using var response = await client.PostAsync("/api/Consultation/42/audio", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.Mediator.VerifyAll();
    }

    [Fact]
    public async Task Document_upload_binds_the_existing_multipart_field_name()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<UploadConsultationDocumentCommand>(command =>
                    command.ConsultationId == 43 &&
                    command.DocumentFile.Name == "documentFile" &&
                    command.DocumentFile.FileName == "referral.pdf" &&
                    command.DocumentFile.ContentType == "application/pdf"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consultation(43));
        using var client = CreateClient(factory);
        using var content = new MultipartFormDataContent();
        using var document = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        document.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(document, "documentFile", "referral.pdf");

        using var response = await client.PostAsync("/api/Consultation/43/document", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.Mediator.VerifyAll();
    }

    [Fact]
    public async Task Recording_download_preserves_audio_range_processing_and_content_type()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<GetConsultationAudioQuery>(query => query.ConsultationId == 51),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultationAudioResult
            {
                Content = new MemoryStream(Encoding.ASCII.GetBytes("abcdef")),
                ContentType = "audio/wav",
            });
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/Consultation/51/audio");
        request.Headers.Range = new RangeHeaderValue(1, 3);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bytes 1-3/6", response.Content.Headers.ContentRange?.ToString());
        Assert.Contains("bytes", response.Headers.AcceptRanges);
        Assert.Equal("bcd", await response.Content.ReadAsStringAsync());
        factory.Mediator.VerifyAll();
    }

    [Fact]
    public async Task Document_download_preserves_attachment_filename()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<GetConsultationDocumentQuery>(query => query.ConsultationId == 52),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultationDocumentResult
            {
                Content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF")),
                ContentType = "application/pdf",
                FileName = "consultation.pdf",
            });
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/Consultation/52/document");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("consultation.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("%PDF", await response.Content.ReadAsStringAsync());
        factory.Mediator.VerifyAll();
    }

    [Fact]
    public async Task Missing_consultation_files_keep_their_not_found_status()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<GetConsultationAudioQuery>(query => query.ConsultationId == 53),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsultationAudioResult?)null);
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<GetConsultationDocumentQuery>(query => query.ConsultationId == 53),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsultationDocumentResult?)null);
        using var client = CreateClient(factory);

        using var audio = await client.GetAsync("/api/Consultation/53/audio");
        using var document = await client.GetAsync("/api/Consultation/53/document");

        Assert.Equal(HttpStatusCode.NotFound, audio.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        factory.Mediator.VerifyAll();
    }

    [Theory]
    [InlineData("/api/ai-callback/transcription", "{\"jobId\":\"job\",\"correlationId\":\"corr\",\"consultationId\":1,\"status\":\"completed\"}")]
    [InlineData("/api/ai-callback/structured-data", "{\"jobId\":\"job\",\"correlationId\":\"corr\",\"consultationId\":1,\"status\":\"completed\"}")]
    [InlineData("/api/ai-callback/action", "{\"jobId\":\"job\",\"correlationId\":\"corr\",\"status\":\"completed\"}")]
    [InlineData("/api/ai-callback/chat-progress", "{\"askId\":\"fc5f18d2-b4c8-4e51-ae15-69340217b9bb\",\"doctorId\":\"doctor-1\",\"phase\":\"retrieving\",\"message\":\"Searching\"}")]
    public async Task Legacy_AI_callbacks_reject_requests_without_the_callback_key(string route, string json)
    {
        await using var factory = new BackendContractApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsync(
            route,
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_AI_callback_generations_keep_their_routes_payloads_and_success_status()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<ProcessTranscriptionCallbackCommand>(command =>
                    command.JobId == "transcription-job" && command.ConsultationId == 61),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<ProcessStructuredDataCallbackCommand>(command =>
                    command.JobId == "structured-job" &&
                    command.SchemaVersion == "v1" &&
                    command.StructuredPayload == "{}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<ProcessActionCallbackCommand>(command =>
                    command.JobId == "action-job" && command.ResponsePayload == "{\"ok\":true}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Api-Key", BackendContractApiFactory.CallbackApiKey);

        using var transcription = await client.PostAsJsonAsync("/api/ai-callback/transcription", new
        {
            jobId = "transcription-job",
            correlationId = "correlation-1",
            consultationId = 61,
            status = "completed",
            transcript = "Current transcript contract",
        });
        using var structured = await client.PostAsJsonAsync("/api/ai-callback/structured-data", new
        {
            jobId = "structured-job",
            correlationId = "correlation-2",
            consultationId = 61,
            status = "completed",
        });
        using var action = await client.PostAsJsonAsync("/api/ai-callback/action", new
        {
            jobId = "action-job",
            correlationId = "correlation-3",
            status = "completed",
            responsePayload = "{\"ok\":true}",
        });

        Assert.Equal(HttpStatusCode.OK, transcription.StatusCode);
        Assert.Equal(HttpStatusCode.OK, structured.StatusCode);
        Assert.Equal(HttpStatusCode.OK, action.StatusCode);
        factory.Mediator.VerifyAll();
    }

    [Fact]
    public async Task Authorized_chat_progress_callback_keeps_its_route_mapping_and_success_status()
    {
        var occurredAt = new DateTimeOffset(2026, 8, 23, 12, 30, 0, TimeSpan.Zero);
        var progress = new Mock<IChatProgressNotifier>(MockBehavior.Strict);
        progress
            .Setup(notifier => notifier.NotifyAsync(
                "doctor-9",
                It.Is<ChatProgressEvent>(message =>
                    message.AskId == Guid.Parse("fc5f18d2-b4c8-4e51-ae15-69340217b9bb") &&
                    message.Phase == "retrieving" &&
                    message.Message == "Searching patient evidence" &&
                    message.OccurredAt == occurredAt),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await using var factory = new BackendContractApiFactory(progress.Object);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Api-Key", BackendContractApiFactory.CallbackApiKey);

        using var response = await client.PostAsJsonAsync("/api/ai-callback/chat-progress", new
        {
            askId = "fc5f18d2-b4c8-4e51-ae15-69340217b9bb",
            doctorId = "doctor-9",
            phase = "retrieving",
            message = "Searching patient evidence",
            occurredAt,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
        progress.VerifyAll();
    }

    [Fact]
    public async Task Legacy_stateless_chat_query_keeps_its_route_mapping_and_response_JSON()
    {
        await using var factory = new BackendContractApiFactory();
        factory.Mediator
            .Setup(mediator => mediator.Send(
                It.Is<ChatQuery>(query =>
                    query.PatientId == 71 &&
                    query.ConsultationId == 72 &&
                    query.Message == "What changed?" &&
                    query.SessionId == "legacy-session"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponseDto
            {
                Answer = "No material change.",
                Citations = ["consultation-72"],
                SuggestedActions = ["review transcript"],
            });
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/api/Chat/query", new
        {
            patientId = 71,
            consultationId = 72,
            message = "What changed?",
            sessionId = "legacy-session",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "{\"answer\":\"No material change.\",\"citations\":[\"consultation-72\"],\"suggestedActions\":[\"review transcript\"]}",
            await response.Content.ReadAsStringAsync());
        factory.Mediator.VerifyAll();
    }

    private static HttpClient CreateClient(BackendContractApiFactory factory) =>
        factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static ConsultationDto Consultation(int id) => new()
    {
        Id = id,
        DoctorId = "contract-doctor",
        ConsultationDate = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc),
        Status = "Pending",
    };
}
