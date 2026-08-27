using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Services;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientHistory;

public class GetPatientHistoryQueryHandler : IRequestHandler<GetPatientHistoryQuery, PatientHistoryDto>
{
    private readonly IUserService _userService;
    private readonly PatientHistoryAssembler _assembler;

    public GetPatientHistoryQueryHandler(IUserService userService, PatientHistoryAssembler assembler)
    {
        _userService = userService;
        _assembler = assembler;
    }

    public async Task<PatientHistoryDto> Handle(GetPatientHistoryQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        return await _assembler.AssembleAsync(request, doctorId, cancellationToken);
    }
}
