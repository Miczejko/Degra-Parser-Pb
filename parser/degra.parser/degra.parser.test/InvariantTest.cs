namespace degra.parser.test
{
   
    using degra.parser;
    public class InvariantTest
    {
        const string TestDataPath = "../../../../testData/";
        public static TheoryData<string> AllPdfs()
        {
            var data = new TheoryData<string>();
            foreach (var f in Directory.GetFiles(TestDataPath, "*.pdf").Order())
                data.Add(Path.GetFileName(f));
            return data;
        }

        [Theory, MemberData(nameof(AllPdfs))]
        public void ParsesWithoutWarnings(string file)
        {
            var schedule = PlanParser.Parse(Path.Combine(TestDataPath, file));

            Assert.Empty(schedule.Warnings);
        }

        [Theory, MemberData(nameof(AllPdfs))]
        public void NumberOfDays(string file)
        {
            var schedule = PlanParser.Parse(Path.Combine(TestDataPath, file));

            Assert.Equal(5, schedule.Entries.Select(e => e.Day).Distinct().Count());
        }

        [Theory, MemberData(nameof(AllPdfs))]
        public void ClassNamesDefined(string file)
        {
            var schedule = PlanParser.Parse(Path.Combine(TestDataPath, file));

            Assert.DoesNotContain("Nieznane", schedule.Entries.Select(e => e.Subject));
        }
    }
}
