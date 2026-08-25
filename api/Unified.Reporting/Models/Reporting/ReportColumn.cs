namespace Unified.Reporting.Models.Reporting;

public sealed record ReportColumn(string Key, string Label, ReportValueType Type, bool Sortable = true);