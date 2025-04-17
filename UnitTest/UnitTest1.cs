namespace CodeChallenge.UnitTest
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase("wrongdateformat.csv")]
        public void Test_WrongDateFormat(string fileName)
        {
            try
            {
                fileName = Path.Combine(AppContext.BaseDirectory, fileName);
                var dataList = CodeChallenge.Program.ParseDataCsv(fileName);
                Assert.That(dataList is null);
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Does.Contain("Error parsing data file"));
            }
        }
        [TestCase("wrongdeviceformat.csv")]
        public void Test_WrongDeviceFormat(string fileName)
        {
            try
            {
                fileName = Path.Combine(AppContext.BaseDirectory, fileName);
                var deviceList = CodeChallenge.Program.ParseDeviceCsv(fileName);
                Assert.That(deviceList is null);
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Does.Contain("Error parsing device file"));
            }
        }
    }
}