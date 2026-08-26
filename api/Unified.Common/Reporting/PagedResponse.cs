namespace Unified.Common.Reporting;

public abstract record PagedResponse(int Page, int PageSize, int TotalRows);