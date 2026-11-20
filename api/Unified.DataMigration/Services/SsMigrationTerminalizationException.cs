namespace Unified.DataMigration.Services;

// The terminal transition was rolled back along with its summary. The run must be
// recovered rather than silently reported as completed or failed without a summary.
internal sealed class SsMigrationTerminalizationException()
    : InvalidOperationException(
        "SS migration could not persist its terminal state and audit summary; recovery is required."
    );
