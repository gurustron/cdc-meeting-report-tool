namespace CDCMeetingReportTool.Core;

public record ParsedQuestion(
    string Tournament,
    string SourceDate,
    DateOnly? Date,
    string Home,
    string Away
);
