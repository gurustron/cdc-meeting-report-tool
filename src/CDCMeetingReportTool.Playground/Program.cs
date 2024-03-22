using System.Text.RegularExpressions;
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
    var reInfo =
        new Regex(@"матч\w* турнира (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.");
    var list2 = startsWithNumberList.Where(p => !reInfo.IsMatch(p.InnerText))
        .ToList();
    
    var decisions  = list.Where(p => !string.IsNullOrEmpty(p.InnerText))
        .SkipWhile(p=> !p.InnerText.Contains("РЕШЕНИЕ:"))
        .Skip(1)
        .ToList();
    int counter = 0;
    foreach (var p in decisions)
    {
        if (p.ParagraphProperties.NumberingProperties is not null)
        {
            counter++;
        }
        
    }

    Console.WriteLine(counter);
}

// ПОВЕСТКА ЗАСЕДАНИЯ:
// РЕШЕНИЕ:
// "  *		   *		*	         *		    *"
// матч\w* (?<tournament>.*) между командами (?<home>.*) и (?<away>.*),.* (?<date>\d{1,2} \w* \d{4}) \w*\.