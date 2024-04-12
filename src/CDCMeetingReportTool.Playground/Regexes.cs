using System.Text.RegularExpressions;

namespace CDCMeetingReportTool.Playground;

public partial class Regexes
{
    [GeneratedRegex(
        @"матч\w* турнира (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.")]
    public static partial Regex TopicInfoRegex();
    
    [GeneratedRegex(@"^\d+\..*")]
    public static partial Regex StartsWithNumberRegex();
}