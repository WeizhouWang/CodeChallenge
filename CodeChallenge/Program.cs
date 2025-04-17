using ExcelDataReader;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace CodeChallenge
{
    public class Program
    {
        static void Main()
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(static builder => builder.AddConsole());
            ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
            try
            {
                Console.Write("FTP folder (default is current folder with sample) : ");
                var folderName = Console.ReadLine();
                if (string.IsNullOrEmpty(folderName))
                {
                    folderName = Path.Combine(AppContext.BaseDirectory, "Sample");
                }
                var directory = new DirectoryInfo(folderName);
                var deviceFiles = directory.GetFiles("*.csv").Where(f => f.Name.Contains("Device", StringComparison.OrdinalIgnoreCase)).ToList();
                var devices = new List<DeviceInfo>();
                foreach (var deviceFile in deviceFiles)
                {
                    var devicesInFile = ParseDeviceCsv(deviceFile.FullName);
                    if (devicesInFile != null)
                    {
                        devices.AddRange(devicesInFile);
                    }
                }
                if (devices == null)
                {
                    logger.LogWarning("Failed to parse device file.");
                    return;
                }
                var dataFiles = directory.GetFiles("*.csv").Where(f => f.Name.Contains("Data", StringComparison.OrdinalIgnoreCase)).ToList();
                var dataList = new List<DataInfo>();
                foreach (var dataFile in dataFiles)
                {
                    var dataInFile = ParseDataCsv(dataFile.FullName);
                    if (dataInFile != null)
                    {
                        dataList.AddRange(dataInFile);
                    }
                }
                if (dataList == null)
                {
                    logger.LogWarning("Failed to parse data file.");
                    return;
                }
                var results = ProcessData(devices, dataList);
                OutputResults(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing the data.");
            }
        }
        public static List<DeviceInfo>? ParseDeviceCsv(string fileName)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var errMessage = new StringBuilder();
                List<DeviceInfo> deviceList = [];
                using Stream stream = File.OpenRead(fileName);

                using var reader = ExcelReaderFactory.CreateCsvReader(stream);
                var excelDataSet = reader.AsDataSet();
                var dataTable = excelDataSet.Tables[0];
                // Skip the first row because it contains headers
                for (int i = 1; i < dataTable.Rows.Count; i++)
                {
                    var line = i + 1;
                    if (dataTable.Rows[i] != null)
                    {
                        var columns = dataTable.Rows[i].ItemArray.Where(a => a != DBNull.Value).Cast<string>().ToArray();
                        if (int.TryParse(columns[0], out int deviceId) == false)
                        {
                            errMessage.AppendLine($"line {line}: Device Id {columns[0]} is not an integer");
                        }
                        var device = new DeviceInfo()
                        {
                            DeviceId = deviceId,
                            DeviceName = columns[1],
                            Location = columns[2]
                        };
                        deviceList.Add(device);
                    }
                }
                if (errMessage.Length > 0)
                {
                    throw new Exception(errMessage.ToString());
                }
                return deviceList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing device file: {ex.Message}");
            }
        }
        public static List<DataInfo>? ParseDataCsv(string fileName)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var errMessage = new StringBuilder();
                List<DataInfo> dataList = [];
                using Stream stream = File.OpenRead(fileName);
                using var reader = ExcelReaderFactory.CreateCsvReader(stream);
                var excelDataSet = reader.AsDataSet();
                var dataTable = excelDataSet.Tables[0];
                // Skip the first row because it contains headers
                for (int i = 1; i < dataTable.Rows.Count; i++)
                {
                    var line = i + 1;
                    if (dataTable.Rows[i] != null)
                    {
                        var columns = dataTable.Rows[i].ItemArray.Where(a => a != DBNull.Value).Cast<string>().ToArray();
                        if (int.TryParse(columns[0], out int deviceId) == false)
                        {
                            errMessage.AppendLine($"line {line}: Device Id {columns[0]} is not an integer");
                        }
                        if (DateTime.TryParseExact(columns[1], "dd/MM/yyyy H:mm", new CultureInfo("en-AU"), DateTimeStyles.None, out DateTime dt) == false)
                        {
                            errMessage.AppendLine($"line {line}: Date ({columns[1]}) not recognised, please use this format: dd/MM/yyyy HH:mm, E.g. 05/06/2020 09:00");
                        }
                        if (int.TryParse(columns[2], out int volume) == false)
                        {
                            errMessage.AppendLine($"line {line}: Device Id {columns[2]} is not an integer");
                        }
                        var item = new DataInfo()
                        {
                            DeviceId = deviceId,
                            Time = dt,
                            Volume = volume
                        };
                        dataList.Add(item);
                    }
                }
                if (errMessage.Length > 0)
                {
                    throw new Exception(errMessage.ToString());
                }
                return dataList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing data file: {ex.Message}");
            }
        }
        static List<ResultInfo> ProcessData(List<DeviceInfo> devices, List<DataInfo> dataList)
        {
            var results = new List<ResultInfo>();
            var currentDate = dataList.OrderBy(a => a.Time).Last().Time;
            foreach (var device in devices)
            {
                var result = new ResultInfo
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName
                };
                var lastDeviceDataList = dataList.Where(d => d.DeviceId == device.DeviceId && (currentDate - d.Time).TotalHours <= 8 && (currentDate - d.Time).TotalHours > 4).ToList();
                if (lastDeviceDataList.Count != 0)
                {
                    double lastTotalVolume = lastDeviceDataList.Sum(d => d.Volume);
                    result.LastAverage = lastTotalVolume / lastDeviceDataList.Count;
                }
                var deviceDataList = dataList.Where(d => d.DeviceId == device.DeviceId && (currentDate - d.Time).TotalHours <= 4).ToList();
                if (deviceDataList.Count == 0)
                {
                    throw new Exception($"No data found for device {device.DeviceName} ({device.DeviceId})");
                }
                double totalVolume = deviceDataList.Sum(d => d.Volume);
                result.Average = totalVolume / deviceDataList.Count;
                result.IsValid = deviceDataList.Any(d => d.Volume >= 30) == false;
                if (result.LastAverage != null)
                {
                    if (result.Average > result.LastAverage) result.Trending = "Increasing";
                    if (result.Average < result.LastAverage) result.Trending = "Decreasing";
                }
                results.Add(result);
            }
            return results;
        }
        static void OutputResults(List<ResultInfo> results)
        {
            foreach (var result in results)
            {
                if (!result.IsValid || result.Average >= 15)
                {
                    Console.WriteLine($"\x1b[31mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
                else if (result.Average >= 10)
                {
                    Console.WriteLine($"\x1b[38;2;255;165;0mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
                else
                {
                    Console.WriteLine($"\x1b[32mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
            }
        }
    }
}