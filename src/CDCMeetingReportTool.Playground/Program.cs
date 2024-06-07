using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CDCMeetingReportTool.Core;
using CDCMeetingReportTool.Playground;
using DocumentFormat.OpenXml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Stateless;

// TODO:
// - load all team names (duplicates - verification)
// - load all tournaments (duplicates - verification)
// - output to formatted word file
// - sort tournaments by?
// - UI

// var filepath = @"D:\Projects\TestData\Протокол от 01.03.24.docx"; 
var filepath = @"D:\Projects\cdc-meeting-report-tool\TestFiles\Протокол от 23.04.24_2.docx"; 
// var filepath = @"D:\Projects\TestData\Повестка КДК на 7 июня.docx";
// var filepath =    "..\\TestData\\Повестка КДК на 7 июня.docx";
// (body.ToList()[427] as Paragraph).ParagraphProperties.NumberingProperties

// string filepath = "/home/gurustron/Projects/cdc-meeting-report-tool/TestFiles/Протокол от 26.03.24.docx";
using (var doc = WordprocessingDocument.Open(filepath, false))
{
    var idPartPairs = doc.Parts.ToList();
    var openXmlParts = doc.GetAllParts().ToList();
    var main = doc.MainDocumentPart;
    var partPairs = main.Parts.ToList();
    var body = main.Document.Body;
    var list = body.Descendants<Paragraph>()
        .ToList();
    var questionsAndDecisionsParser = new QuestionsAndDecisionsParser();
    foreach (var par in list)
    {
        questionsAndDecisionsParser.LineEncountered(par);
        if (par.InnerText.Contains("РЕШЕНИЕ:"))
        {
            var parsingState = questionsAndDecisionsParser.State;
            break;
        }
    }
    var list1 = list.Where(p => !string.IsNullOrEmpty(p.InnerText))
        .SkipWhile(p => !p.InnerText.Contains("ПОВЕСТКА ЗАСЕДАНИЯ:"))
        .Skip(1)
        .TakeWhile(p=> !p.InnerText.Contains("РЕШЕНИЕ:"))
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

    var list2 = startsWithNumberList.Where(p => !Regexes.TopicInfoRegex().IsMatch(p.InnerText))
        .ToList();

    var questionParas = startsWithNumberList
        .Where(p => Regexes.TopicInfoRegex().IsMatch(p.InnerText))
        .ToList();   
    var non_questionParas = startsWithNumberList
        .Where(p => !Regexes.TopicInfoRegex().IsMatch(p.InnerText))
        .ToList();

    var questions = new List<Question>(questionParas.Count);
    foreach (var questionPara in questionParas)
    {
        var source = questionPara.InnerText;
        var match = Regexes.TopicInfoRegex().Match(source);
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
        .Select((q, i) => (q.Parsed, i))
        .GroupBy(q => q.Parsed.Tournament)
        .ToDictionary(g => g.Key,
            g => g.GroupBy(q => q.Parsed.Date)
                .ToDictionary(g => g.Key,
                    g => g.ToLookup(q => $"{q.Parsed.Home} - {q.Parsed.Away}")));

    var decisions  = list.Where(p => !string.IsNullOrEmpty(p.InnerText))
        .SkipWhile(p=> !p.InnerText.Contains("РЕШЕНИЕ:"))
        .Skip(1)
        .TakeWhile(p => !p.InnerText.Contains("*"))
        .ToList();
    int counter = 0;
    var parsedDecisions = Enumerable.Range(0, questions.Count)
        .Select(_ => new List<string>())
        .ToList();
    foreach (var p in decisions)
    {
        if (p.ParagraphProperties.NumberingProperties is not null)
        {
            counter++;
        }

        if (counter != 0 && !string.IsNullOrWhiteSpace(p.InnerText)
                         && counter <= parsedDecisions.Count //dirty hack for non-parsed questions
            )
        {
            parsedDecisions[counter - 1].Add(p.InnerText);
        }
    }

    var list3 = parsedDecisions.Select((l, i) => (l, i)).Where(l => l.l.Count != 2).ToList();
    var stringBuilder = new StringBuilder();
    
    foreach (var (key, value) in result.OrderBy(kvp => kvp.Key))
    {
        stringBuilder.AppendLine(key);
        stringBuilder.AppendLine();

        foreach (var (dateOnly, lookup) in value.OrderBy(v => v.Key))
        {
            stringBuilder.AppendLine(dateOnly.Value.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU")));
            stringBuilder.AppendLine();
            foreach (var valueTuples in lookup)
            {
                stringBuilder.AppendLine(valueTuples.Key);
                stringBuilder.AppendLine();
                foreach (var (parsed, i) in valueTuples)
                {
                    var toPrint = parsedDecisions[i];
                    foreach (var des in toPrint.Skip(1))
                    {
                        stringBuilder.AppendLine(des);
                        stringBuilder.AppendLine();
                    }
                }
            }
        }
        
        
    }

    var results = stringBuilder.ToString();
}

// ПОВЕСТКА ЗАСЕДАНИЯ:
// РЕШЕНИЕ:
// "  *		   *		*	         *		    *"
// матч\w* (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.




public class QuestionsAndDecisionsParser
{
    private StateMachine<ParsingState, LineType> _stateMachine;

    // private StateMachine<ParsingState,LineType>.TriggerWithParameters<string> _newQuestionTrigger;
    // private StateMachine<ParsingState,LineType>.TriggerWithParameters<string> _questionContentTrigger;

    private List<string> _currentData = [];
    private List<List<string>> _questions = [];
    private StateMachine<ParsingState,LineType>.TriggerWithParameters<Paragraph> _lineTrigger;

    public enum ParsingState
    {
        SearchingQuestionsStart, // either not started or haven't encountered first question
        QuestionsFound,
        NewQuestion,
        ReadingQuestion,
        DecisionsFound,
        ReadingDecisions,
        Processed
    }
    
    private enum LineType
    {
        Line,
        NewQuestion,
        QuestionContent,
        QuestionsFinished,
        NewDecision,
        DecisionContent,
        DecisionsFinished,
    }

    public QuestionsAndDecisionsParser()
    {
        _stateMachine = new StateMachine<ParsingState, LineType>(ParsingState.SearchingQuestionsStart);
        ConfigureStateMachine();
    }
    
    
    private void ConfigureStateMachine()
    {
        _lineTrigger = _stateMachine.SetTriggerParameters<Paragraph>(LineType.Line);
        _stateMachine.Configure(ParsingState.SearchingQuestionsStart)
            .PermitIf(_lineTrigger, ParsingState.QuestionsFound, s => s.InnerText.Contains("ПОВЕСТКА ЗАСЕДАНИЯ:"))
            .PermitReentryIf(_lineTrigger, s => !s.InnerText.Contains("ПОВЕСТКА ЗАСЕДАНИЯ:"));

        _stateMachine.Configure(ParsingState.QuestionsFound)
            .PermitIf(_lineTrigger, ParsingState.NewQuestion, p => Regexes.StartsWithNumberRegex().IsMatch(p.InnerText))
            .PermitReentryIf(_lineTrigger, p => !Regexes.StartsWithNumberRegex().IsMatch(p.InnerText));

        _stateMachine.Configure(ParsingState.NewQuestion)
            .PermitIf(_lineTrigger, ParsingState.ReadingQuestion,
                p => !Regexes.StartsWithNumberRegex().IsMatch(p.InnerText))
            .PermitReentryIf(_lineTrigger, p => Regexes.StartsWithNumberRegex().IsMatch(p.InnerText))
            .OnEntryFrom(_lineTrigger, paragraph =>
            {
                ProceedQuestion();

                _currentData.Add(paragraph.InnerText);
            });

        _stateMachine.Configure(ParsingState.ReadingQuestion)
            .PermitReentryIf(_lineTrigger, p => !Regexes.StartsWithNumberRegex().IsMatch(p.InnerText) && !p.InnerText.Contains("РЕШЕНИЕ:"))
            .PermitIf(_lineTrigger, ParsingState.NewQuestion, p => Regexes.StartsWithNumberRegex().IsMatch(p.InnerText))
            .PermitIf(_lineTrigger, ParsingState.DecisionsFound, p => p.InnerText.Contains("РЕШЕНИЕ:"));

        _stateMachine.Configure(ParsingState.QuestionsFound)
            .OnEntry(ProceedQuestion);

        // _stateMachine.Configure(ParsingState.ReadingQuestion)

        // _stateMachine.Configure(ParsingState.SearchingQuestionsStart)
        //     .Permit(LineType.NewQuestion, ParsingState.ReadingQuestion)
        //     .Permit(LineType.QuestionsFinished, ParsingState.ReadingDecisions);
        //
        // _newQuestionTrigger = _stateMachine.SetTriggerParameters<string>(LineType.NewQuestion);
        // _questionContentTrigger = _stateMachine.SetTriggerParameters<string>(LineType.QuestionContent);
        //
        // _stateMachine.Configure(ParsingState.ReadingQuestion)
        //     .OnEntryFrom(_newQuestionTrigger, NewQuestion)
        //     .PermitReentry(LineType.NewQuestion)
        //     ;
    }

    private void ProceedQuestion()
    {
        if (_currentData.Any())
        {
            _questions.Add(_currentData); // TODO - parse question
            _currentData = new List<string>();
        }
    }

    public void LineEncountered(Paragraph line)
    {
        _stateMachine.Fire(_lineTrigger, line);
    }

    public ParsingState State => _stateMachine.State;

    private void NewQuestion(string questionLine)
    {
        _currentData = [questionLine];
    }
}

