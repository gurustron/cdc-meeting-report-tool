using System.Globalization;
using System.Text.RegularExpressions;
using CDCMeetingReportTool.Core;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

Console.WriteLine("Hello, World!");

// (body.ToList()[427] as Paragraph).ParagraphProperties.NumberingProperties

string filepath = "/home/gurustron/Projects/cdc-meeting-report-tool/TestFiles/Протокол от 07.06.23.docx";
using (var doc = WordprocessingDocument.Open(filepath, false))
{
    var idPartPairs = doc.Parts.ToList();
    var openXmlParts = doc.GetAllParts().ToList();
    var main = doc.MainDocumentPart;
    var partPairs = main.Parts.ToList();
    var body = main.Document.Body;
    var list = body.Descendants<Paragraph>()
        .ToList();
    var list1 = list.Where(p => !string.IsNullOrEmpty(p.InnerText))
        .SkipWhile(p => !p.InnerText.Contains("ПОВЕСТКА ЗАСЕДАНИЯ:"))
        .Skip(1)
        .TakeWhile(p=> p.InnerText.Contains("РЕШЕНИЕ:"))
        .ToList();
    var regex = new Regex(@"^\d+\..*");
    var startsWithNumberList = list 
        .Where(p => regex.IsMatch(p.InnerText)).ToList();
    var nonVopros = startsWithNumberList
        .Where(p => !p.InnerText.Contains("Вопрос", StringComparison.InvariantCultureIgnoreCase)).ToList();
    var nonVOprosTexts = nonVopros.Select(p => p.InnerText).ToList();
    var nonVMatche = startsWithNumberList
        .Where(p => !p.InnerText.Contains("в матче турнира", StringComparison.InvariantCultureIgnoreCase)).ToList();
    var nonVMatcheTexts = nonVMatche.Select(p => p.InnerText).ToList();
    var topicInfoRegex = new Regex(
            @"матч\w* турнира (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.", 
            RegexOptions.Compiled);
    var list2 = startsWithNumberList.Where(p => !topicInfoRegex.IsMatch(p.InnerText))
        .ToList();

    var questionParas = startsWithNumberList
        .Where(p => topicInfoRegex.IsMatch(p.InnerText))
        .ToList();

    var questions = new List<Question>(questionParas.Count);
    foreach (var questionPara in questionParas)
    {
        var source = questionPara.InnerText;
        var match = topicInfoRegex.Match(source);
        var dateString = match.Groups["date"].Value; // 25 мая 2023
        var date = DateOnly.ParseExact(dateString, "dd MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU"));
        var question = new Question(new ParsedQuestion(
                Tournament: match.Groups["tournament"].Value,
                SourceDate: dateString,
                Date: date,
                Home: match.Groups["home"].Value,
                Away: match.Groups["away"].Value
            ),
            [source]);
        questions.Add(question);
    }

    var result = questions
        .Select(q => q.Parsed)
        .GroupBy(q => q.Tournament)
        .ToDictionary(g => g.Key,
            g => g.GroupBy(q => q.Date)
                .ToDictionary(g => g.Key,
                    g => g.ToLookup(q => $"{q.Home} - {q.Away}")));

    var decisions  = list.Where(p => !string.IsNullOrEmpty(p.InnerText))
        .SkipWhile(p=> !p.InnerText.Contains("РЕШЕНИЕ:"))
        .Skip(1)
        .TakeWhile(p => !p.InnerText.Contains("*"))
        .ToList();
    foreach (var p in decisions)
    {
        if (p.ParagraphProperties.NumberingProperties is not null)
        {
        }
        
    }

}

// ПОВЕСТКА ЗАСЕДАНИЯ:
// РЕШЕНИЕ:
// "  *		   *		*	         *		    *"
// матч\w* (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.