using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        // Создание DI-контейнера
        var serviceProvider = new ServiceCollection()
            .AddTransient<ILogWriter, LogWriter>()
            .AddTransient<IArgumentParser, ArgumentParser>()
            .AddTransient<ILogFilter, LogFilter>()
            .BuildServiceProvider();
        var logFilter = serviceProvider.GetRequiredService<ILogFilter>();
        // Парсинг аргументов командной строки
        var parser = serviceProvider.GetRequiredService<IArgumentParser>();
        var arguments = parser.Parse(args);
        

        // Получение экземпляра ILogWriter из DI-контейнера
        var logWriter = serviceProvider.GetRequiredService<ILogWriter>();

        // Проверка существования файла
        if (!File.Exists(arguments.FileLog))
        {
            Console.WriteLine($"File '{arguments.FileLog}' does not exist.");
            return;
        }

        // Открываем файл и получаем поток
        using (FileStream fileStream = File.OpenRead(arguments.FileLog))
        {
            // Чтение логов и фильтрация данных
            var logsTask = LogReader.ReadLogsAsync(fileStream);
            var logs = await logsTask;  
            var filteredLogs = logFilter.FilterLogs(logs, arguments.AddressStart, arguments.AddressMask, arguments.TimeStart, arguments.TimeEnd);

            // Запись результатов в файл
            logWriter.WriteLogs(arguments.FileOutput, filteredLogs);
        }
    }
}
public interface IArgumentParser
{
    (string FileLog, string FileOutput, string AddressStart, string AddressMask, DateTime TimeStart, DateTime TimeEnd) Parse(string[] args);
}

public class ArgumentParser : IArgumentParser
{
    public (string FileLog, string FileOutput, string AddressStart, string AddressMask, DateTime TimeStart, DateTime TimeEnd) Parse(string[] args)
    {
        try
        {
            var parser = new Parser(config =>
            {
                config.HelpWriter = Console.Out;
            });

            string? fileLog = null;
            string? fileOutput = null;
            string? addressStart = null;
            string? addressMask = null;
            DateTime timeStart = DateTime.MinValue;
            DateTime timeEnd = DateTime.MaxValue;

            parser.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    fileLog = o.FileLog;
                    fileOutput = o.FileOutput;
                    addressStart = o.AddressStart;
                    addressMask = o.AddressMask;

                    try
                    {
                        timeStart = DateTime.ParseExact(o.TimeStart ?? throw new ArgumentException("Value cannot be null"), "dd.MM.yyyy", null);
                        timeEnd = DateTime.ParseExact(o.TimeEnd ?? throw new ArgumentException("Value cannot be null"), "dd.MM.yyyy", null);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred while parsing time: {ex.Message}");
                    }
                });

            return (fileLog!, fileOutput!, addressStart!, addressMask!, timeStart, timeEnd);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while parsing arguments: {ex.Message}");
            return (null!, null!, null!, null!, DateTime.MinValue, DateTime.MaxValue);
        }

    }

    private class Options
    {
        [Option('f', "file-log", Required = true, HelpText = "Path to the log file.")]
        public string? FileLog { get; set; }

        [Option('o', "file-output", Required = true, HelpText = "Path to the output file.")]
        public string? FileOutput { get; set; }

        [Option("address-start", HelpText = "Start address.")]
        public string? AddressStart { get; set; }

        [Option("address-mask", HelpText = "Address mask.")]
        public string? AddressMask { get; set; }

        [Option("time-start", Required = true, HelpText = "Start time.")]
        public string? TimeStart { get; set; }

        [Option("time-end", Required = true, HelpText = "End time.")]
        public string? TimeEnd { get; set; }
    }
}

public class LogReader
{
    public static async Task<Dictionary<string, List<DateTime>>> ReadLogsAsync(Stream stream)
    {
        try
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null");
            }

            using (var reader = new StreamReader(stream))
            {
                return await ReadLogsFromReaderAsync(reader);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading logs asynchronously: {ex.Message}");
            return new Dictionary<string, List<DateTime>>(); 
        }
    }

    private static async Task<Dictionary<string, List<DateTime>>> ReadLogsFromReaderAsync(StreamReader reader)
    {
        var logs = new Dictionary<string, List<DateTime>>();
        string line;

        try
        {
            while ((line = await reader.ReadLineAsync()) != null)
            {
                ProcessLogLine(line, logs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading logs from reader asynchronously: {ex.Message}");
        }

        return logs;
    }

    private static void ProcessLogLine(string line, Dictionary<string, List<DateTime>> logs)
    {
        try
        {
            if (!TryProcessLogLine(line, out var ipAddress, out var timestamp))
            {
                return;
            }

            logs.TryAdd(ipAddress ?? throw new ArgumentNullException("null"), new List<DateTime>() { timestamp });
            logs[ipAddress].Add(timestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while processing log line: {ex.Message}");
        }
    }


    private static bool TryProcessLogLine(string line, out string? ipAddress, out DateTime timestamp)
    {
        var parts = line.Split(':');
        if (parts.Length != 2)
        {
            ipAddress = null;
            timestamp = DateTime.MinValue;
            return false;
        }

        ipAddress = parts[0].Trim(); 
        try
        {
            timestamp = DateTime.ParseExact(parts[1].Trim(), "yyyy-MM-dd HH:mm:ss", null);
            return true;
        }
        catch (FormatException)
        {
            ipAddress = null;
            timestamp = DateTime.MinValue;
            return false;
        }
    }
}


public interface ILogFilter
{
    Dictionary<string, int> FilterLogs(
        Dictionary<string, List<DateTime>> logs,
        string addressStart,
        string addressMask,
        DateTime timeStart,
        DateTime timeEnd);
}
public class LogFilter : ILogFilter
{
    public Dictionary<string, int> FilterLogs(
        Dictionary<string, List<DateTime>> logs,
        string addressStart,
        string addressMask,
        DateTime timeStart,
        DateTime timeEnd)
    {
        try
        {
            addressStart = addressStart ?? "";
            addressMask = addressMask ?? "";

            return logs
                .Where(kv => IsInAddressRange(kv.Key, addressStart, addressMask))
                .ToDictionary(
                    kv => kv.Key,
                    kv => CountLogsInRange(kv.Value, timeStart, timeEnd));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while filtering logs: {ex.Message}");
            return new Dictionary<string, int>(); 
        }
    }

    private bool IsInAddressRange(string address, string start, string mask)
    {
        try
        {
            return address.CompareTo(start) >= 0 && address.CompareTo(mask) <= 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while checking address range: {ex.Message}");
            return false;
        }
    }

    private int CountLogsInRange(List<DateTime> timestamps, DateTime start, DateTime end)
    {
        try
        {
            return timestamps.Count(timestamp => timestamp >= start && timestamp <= end);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while counting logs in range: {ex.Message}");
            return 0;
        }
    }
}


public interface ILogWriter
{
    void WriteLogs(string filePath, Dictionary<string, int> filteredLogs);
}

public class LogWriter : ILogWriter
{
    public void WriteLogs(string filePath, Dictionary<string, int> filteredLogs)
    {
        try
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                foreach (var kv in filteredLogs)
                {
                    writer.WriteLine($"{kv.Key}:{kv.Value}");
                }
            }
            Console.WriteLine($"Logs have been written to '{filePath}' successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write logs to '{filePath}': {ex.Message}");
        }
    }
}
