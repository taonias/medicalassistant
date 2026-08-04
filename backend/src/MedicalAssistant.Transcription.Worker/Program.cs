using MedicalAssistant.Application;
using MedicalAssistant.Persistence;
using MedicalAssistant.Transcription.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddTranscriptionWorkerServices(builder.Configuration);

var host = builder.Build();
await host.RunAsync();

public partial class Program;
