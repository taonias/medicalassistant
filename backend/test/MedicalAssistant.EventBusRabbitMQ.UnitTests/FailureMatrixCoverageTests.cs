namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class FailureMatrixCoverageTests
{
    private static readonly FailureMatrixScenario[] Scenarios =
    [
        new("E2E-01", "Happy audio path",
        [
            "Audio_upload_commits_consultation_and_outbox_without_request_thread_publish",
            "Relay_marks_outbox_message_published_only_after_confirmed_publish_returns",
            "HandleAsync_retrieves_audio_transcribes_and_commits_completion_unit_of_work",
            "HandleAsync_prepares_backend_session_transcript_request_without_message_text_payload",
            "CompleteAcceptedAsync_marks_inbox_completed_with_ingestion_and_document_identity"
        ]),
        new("E2E-02", "Broker outage at upload",
        [
            "Audio_upload_commits_consultation_and_outbox_without_request_thread_publish",
            "Relay_schedules_backoff_when_confirmed_publish_fails",
            "Relay_marks_outbox_message_published_only_after_confirmed_publish_returns"
        ]),
        new("E2E-03", "Worker crash before completion commit",
        [
            "ClaimAsync_reclaims_expired_in_progress_event",
            "CompleteAsync_commits_inbox_transcript_consultation_status_and_result_outbox"
        ]),
        new("E2E-04", "Worker crash after commit but before ACK",
        [
            "CompleteAsync_treats_completed_inbox_event_as_duplicate_noop",
            "HandleAsync_skips_blob_and_speech_when_inbox_event_is_already_completed"
        ]),
        new("E2E-05", "Speech transient outage, retry/DLQ, repair, controlled replay",
        [
            "Handler_failure_returns_retry_outcome",
            "Retry_queues_dead_letter_back_to_the_exchange_with_the_original_routing_key",
            "Replay_publishes_the_original_immutable_dead_letter_envelope_after_operator_approval",
            "Replay_denies_payload_editing_and_does_not_publish"
        ]),
        new("E2E-06", "Permanent invalid audio",
        [
            "TranscribeAsync_classifies_empty_transcript_as_permanent_failure",
            "HandleAsync_commits_transcription_failed_for_permanent_speech_failure",
            "FailAsync_commits_failed_transcript_consultation_inbox_and_failure_outbox"
        ]),
        new("E2E-07", "Document upload isolation",
        [
            "Document_upload_commits_document_event_without_original_filename_or_identity",
            "Worker_registration_binds_only_audio_uploaded_to_a_standard_rabbitmq_consumer",
            "Future_document_processor_topology_binds_only_document_uploaded_events"
        ]),
        new("E2E-08", "Deletion race during Speech",
        [
            "ClaimAsync_completes_and_skips_deleted_consultation_before_expensive_work",
            "CompleteAsync_marks_inbox_completed_but_ignores_deleted_consultation",
            "PrepareAsync_ignores_deleted_consultation_without_returning_transcript_text",
            "HandleAsync_deletes_blob_refs_uningests_documents_and_marks_resources_completed"
        ]),
        new("E2E-09", "Transcript correction",
        [
            "Transcript_correction_increments_revision_and_stages_transcript_ready_outbox",
            "PrepareAsync_loads_current_transcript_context_and_keeps_inbox_in_progress_until_acceptance",
            "CompleteAcceptedAsync_marks_inbox_completed_with_ingestion_and_document_identity"
        ]),
        new("E2E-10", "Backend or Clinical Knowledge outage after Transcript completion",
        [
            "PrepareAsync_loads_current_transcript_context_and_keeps_inbox_in_progress_until_acceptance",
            "HandleAsync_does_not_call_clinical_knowledge_when_preparation_skips_event",
            "Handler_failure_returns_retry_outcome"
        ]),
        new("E2E-11", "Scale-out",
        [
            "Validator_accepts_safe_scaling_defaults",
            "Worker_registration_uses_transcription_concurrency_to_bound_rabbitmq_prefetch_and_shutdown",
            "ClaimAsync_returns_duplicate_completed_without_reclaiming",
            "CompleteAsync_treats_completed_inbox_event_as_duplicate_noop"
        ]),
        new("E2E-12", "Graceful shutdown",
        [
            "Worker_registration_uses_transcription_concurrency_to_bound_rabbitmq_prefetch_and_shutdown",
            "Validator_rejects_worker_concurrency_that_can_exceed_quota_or_lease_safety",
            "Validator_rejects_consumers_without_queue_prefetch_or_shutdown_drain"
        ])
    ];

    [Fact]
    public void Failure_matrix_has_all_required_scenarios_and_no_missing_automated_evidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var backendTestRoot = Path.Combine(repositoryRoot, "backend", "test");
        var testSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(backendTestRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.Equal(12, Scenarios.Length);
        foreach (var scenario in Scenarios)
        {
            Assert.StartsWith("E2E-", scenario.Id, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(scenario.Name));
            Assert.NotEmpty(scenario.AutomatedEvidence);

            foreach (var evidence in scenario.AutomatedEvidence)
            {
                Assert.Contains(evidence, testSource, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Failure_matrix_document_tracks_the_same_scenarios_without_placeholder_outcomes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentPath = Path.Combine(
            repositoryRoot,
            "project-docs",
            "event bus implementations",
            "12-end-to-end-failure-matrix.md");
        var document = File.ReadAllText(documentPath);

        foreach (var scenario in Scenarios)
        {
            Assert.Contains(scenario.Id, document, StringComparison.Ordinal);
            Assert.Contains(scenario.Name, document, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("TBD", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TODO", document, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "test")) &&
                File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the medicalassistant repository root.");
    }

    private sealed record FailureMatrixScenario(
        string Id,
        string Name,
        IReadOnlyList<string> AutomatedEvidence);
}
