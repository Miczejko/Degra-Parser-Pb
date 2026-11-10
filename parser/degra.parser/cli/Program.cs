using degra.parser;
using System.Text.Encodings.Web;
using System.Text.Json;

Console.Write("Input pdf path: ");
string path = Console.ReadLine() ?? "";

Console.Write("Input json path: ");
string jsonPath = Console.ReadLine() ?? "";

if (!File.Exists(path))
{
    Console.WriteLine("File not found. Default file chosen instead");
    path = "C:\\Users\\user\\Desktop\\Projekty\\Degra-parser\\testData\\isi5.pdf";
}
Schedule schedule = PlanParser.Parse(path);

var json = JsonSerializer.Serialize(schedule, new JsonSerializerOptions
{
    WriteIndented = true,                                    
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,       
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   
});

if (string.IsNullOrEmpty(jsonPath))
    jsonPath = "../../../../Data/data.json";

File.WriteAllText(jsonPath, json);

Console.Error.WriteLine($"Classes: {schedule.Entries.Count}");


