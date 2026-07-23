using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Helpers;
using MedicalAssistant.Application.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.DeleteConsultation;

public class DeleteConsultationCommandHandler : IRequestHandler<DeleteConsultationCommand, Unit>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IConsultationProcessingPublisher _processingPublisher;
    private readonly BlobStorageSettings _blobSettings;
    private readonly IAppLogger<DeleteConsultationCommandHandler> _logger;

    public DeleteConsultationCommandHandler(
        IConsultationRepository consultationRepository,
        IUserService userService,
        IBlobStorageService blobStorageService,
        IConsultationProcessingPublisher processingPublisher,
        IOptions<BlobStorageSettings> blobSettings,
        IAppLogger<DeleteConsultationCommandHandler> logger)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
        _blobStorageService = blobStorageService;
        _processingPublisher = processingPublisher;
        _blobSettings = blobSettings.Value;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteConsultationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.Id, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.Id);

        await TryDeleteBlobAsync(
            _blobSettings.ConsultationAudioContainer,
            consultation.AudioBlobUri,
            cancellationToken);

        await TryDeleteBlobAsync(
            _blobSettings.ConsultationDocumentsContainer,
            consultation.DocumentBlobUri,
            cancellationToken);

        try
        {
            await _processingPublisher.RemovePendingForConsultationAsync(
                consultation.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to remove pending RabbitMQ messages for consultation {ConsultationId}: {Error}",
                consultation.Id,
                ex.Message);
        }

        await _consultationRepository.DeleteForDoctorAsync(consultation);
        return Unit.Value;
    }

    private async Task TryDeleteBlobAsync(
        string container,
        string? blobUri,
        CancellationToken cancellationToken)
    {
        var blobName = BlobUriHelper.TryExtractBlobName(blobUri, container);
        if (blobName is null)
            return;

        try
        {
            await _blobStorageService.DeleteAsync(container, blobName, cancellationToken);
            _logger.LogInformation(
                "Deleted blob {BlobName} from container {Container}.",
                blobName,
                container);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to delete blob {BlobName} from container {Container}: {Error}",
                blobName,
                container,
                ex.Message);
        }
    }
}
