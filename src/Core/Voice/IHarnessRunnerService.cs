namespace Koy.Core.Voice;

/// <summary>
/// CLI kodlama aracı (harness) yürütme servisi sözleşmesi.
/// </summary>
public interface IHarnessRunnerService
{
    Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct);
}
