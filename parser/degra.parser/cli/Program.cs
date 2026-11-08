using degra.parser;

Console.Write("Input pdf path: ");
string path = Console.ReadLine() ?? "";
if (!File.Exists(path))
{
    Console.WriteLine("File not found. Default file chosen instead");
    path = "C:\\Users\\user\\Desktop\\Projekty\\Degra-parser\\testData\\isi5.pdf";
}
PlanParser.Parse(path);

