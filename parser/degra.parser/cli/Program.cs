using degra.parser;
using System.Text.Encodings.Web;
using System.Text.Json;

string path = "../../../../testData/";
string jsonPath = "../../../../data/";

    Console.Write("Input pdf path: ");
path += Console.ReadLine() ?? "";

Console.Write("Input json path: ");
string jsonName = Console.ReadLine() ?? "data.json";
if(!jsonName.Contains(".json"))
    jsonName = "data.json";

jsonPath += jsonName;

if (!File.Exists(path))
{
    Console.WriteLine("File not found. Default file chosen instead");
    path += "isi5.pdf";
}
Schedule schedule = PlanParser.Parse(path);

var json = JsonSerializer.Serialize(schedule, new JsonSerializerOptions
{
    WriteIndented = true,                                    
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,       
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   
});

File.WriteAllText(jsonPath, json);

Console.Error.WriteLine($"Classes: {schedule.Entries.Count}");


