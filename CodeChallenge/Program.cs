using ExcelDataReader;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace CodeChallenge
{
    class Program
    {
        static void Main(string[] args)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(static builder => builder.AddConsole());
            ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
            Console.Write("Device File: ");
            var deviceFileName = Console.ReadLine();
            Console.Write("Data File: ");
            var dataFileName = Console.ReadLine();
            if (string.IsNullOrEmpty(deviceFileName) || string.IsNullOrEmpty(dataFileName))
            {
                logger.LogWarning("Please provide both device and data file names.");
                return;
            }
            var devices = ParseDeviceCsv(deviceFileName, logger);
            if (devices == null)
            {
                logger.LogWarning("Failed to parse device file.");
                return;
            }
            var dataList = ParseDataCsv(dataFileName, logger);
            if (dataList == null)
            {
                logger.LogWarning("Failed to parse data file.");
                return;
            }
            var results = ProcessData(devices, dataList, logger);
            OutputResults(results);
        }
        static List<DeviceInfo>? ParseDeviceCsv(string fileName, ILogger logger)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                StringBuilder errMessage = new StringBuilder();
                List<DeviceInfo> deviceList = new();
                using (Stream stream = File.OpenRead(fileName))
                {
                    using (var reader = ExcelReaderFactory.CreateCsvReader(stream))
                    {
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
                            logger.LogError("Error parsing device file: {message}", errMessage.ToString());
                            return null;
                        }
                        return deviceList;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing CSV file: {message}", ex.Message);
                return null;
            }
        }
        static List<DataInfo>? ParseDataCsv(string fileName, ILogger logger)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                StringBuilder errMessage = new StringBuilder();
                List<DataInfo> dataList = new();
                using (Stream stream = File.OpenRead(fileName))
                {
                    using (var reader = ExcelReaderFactory.CreateCsvReader(stream))
                    {
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
                            logger.LogError("Error parsing data file: {message}", errMessage.ToString());
                            return null;
                        }
                        return dataList;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing CSV file: {message}", ex.Message);
                return null;
            }
        }
        static List<ResultInfo> ProcessData(List<DeviceInfo> devices, List<DataInfo> dataList, ILogger logger)
        {
            var results = new List<ResultInfo>();
            var currentDate = dataList.Last().Time;
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
                    var lastAverageVolume = lastTotalVolume / lastDeviceDataList.Count;
                }
                var deviceDataList = dataList.Where(d => d.DeviceId == device.DeviceId && (currentDate - d.Time).TotalHours <= 4).ToList();
                if (deviceDataList.Count == 0)
                {
                    logger.LogWarning($"No data found for device {device.DeviceName} ({device.DeviceId})");
                    continue;
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
                    Console.WriteLine($"\x1b[36mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
                else if (result.Average >= 10)
                {
                    Console.WriteLine($"\x1b[35mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
                else
                {
                    Console.WriteLine($"\x1b[31mDevice Id: {result.DeviceId}, Device Name: {result.DeviceName}, Average: {result.Average:0.0}, Trending: {result.Trending}\x1b[0m");
                }
            }
        }
    }
}