namespace Unified.Common.Reporting;

public abstract record PaginatableResponse(int Page, int PageSize, int TotalRows);