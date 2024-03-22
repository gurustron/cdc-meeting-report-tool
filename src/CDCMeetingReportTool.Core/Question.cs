namespace CDCMeetingReportTool.Core;

public record Question(
    ParsedQuestion? Parsed,
    string[] SourceData);