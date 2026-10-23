namespace Unified.Core.Pdf;

public interface ISchedulePdfRenderer
{
    Task<byte[]> RenderAsync(SchedulePdfRenderRequest request, CancellationToken cancellationToken = default);
}
