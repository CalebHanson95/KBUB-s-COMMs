using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KBUBComm.TimeLog.SimpleCSV
{
    public enum RolloverType
    {
        /// <summary>
        /// Keep a single file. When the row limit is reached, drop the earliest line
        /// and add the newest line, maintaining a fixed row count.
        /// </summary>
        Rollover,

        /// <summary>
        /// Append new rows indefinitely, ignoring the rollover count.
        /// </summary>
        Infinite,

        /// <summary>
        /// When the row limit is reached, rename the current file to a numbered file
        /// (e.g., <c>timelog_1.csv</c>). Keeps at most <c>MaxFiles</c> spillover logs,
        /// rotating the oldest out.
        /// </summary>
        Spillover,

        /// <summary>
        /// Same as <see cref="Spillover"/>, but does not enforce a maximum file count.
        /// New numbered files are created indefinitely.
        /// </summary>
        Spillover_Infinite
    }

    public class SimpleCSVTimelog
    {
        private readonly string _filePath;
        private readonly string _baseFileName;
        private readonly string _directory;
        private readonly int _rolloverCount;
        private readonly List<string> _headers;
        private readonly RolloverType _rolloverType;
        private readonly int _maxFiles;
        private readonly object _fileLock = new object();

        private int _currentRowCount;

        public SimpleCSVTimelog(
            string filePath,
            int rolloverCount,
            IEnumerable<string> headers,
            RolloverType rolloverType,
            int maxFiles = 5)
        {
            _filePath = filePath;
            _directory = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
            _baseFileName = Path.GetFileNameWithoutExtension(filePath);
            _rolloverCount = rolloverCount;
            _headers = new List<string> { "Timestamp" };
            _headers.AddRange(headers);
            _rolloverType = rolloverType;
            _maxFiles = maxFiles;
            
            EnsureDirectoryExists();
            InitializeFile();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
        }

        private void InitializeFile()
        {
            if (!File.Exists(_filePath))
            {
                WriteHeaders(_filePath);
                _currentRowCount = 0;
            }
            else
            {
                _currentRowCount = File.ReadLines(_filePath).Count() - 1; // minus header
            }
        }

        private void WriteHeaders(string path)
        {
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine(string.Join(",", _headers));
            }
        }

        public void AddRow<T>(IEnumerable<T> values)
        {
            lock (_fileLock)
            {
                switch (_rolloverType)
                {
                    case RolloverType.Infinite:
                        AppendRow(_filePath, values);
                        break;

                    case RolloverType.Rollover:
                        HandleRollover(values);
                        break;

                    case RolloverType.Spillover:
                        HandleSpillover(values, limited: true);
                        break;

                    case RolloverType.Spillover_Infinite:
                        HandleSpillover(values, limited: false);
                        break;
                }
            }
        }

        private void AppendRow<T>(string path, IEnumerable<T> values)
        {
            using (var writer = new StreamWriter(path, true, Encoding.UTF8))
            {
                var timestamp = DateTime.UtcNow.ToString("o"); // ISO-8601
                var line = timestamp + "," + string.Join(",", values.Select(v => v?.ToString() ?? ""));
                writer.WriteLine(line);
            }
            _currentRowCount++;
        }

        private void HandleRollover<T>(IEnumerable<T> values)
        {
            if (_currentRowCount < _rolloverCount)
            {
                AppendRow(_filePath, values);
                return;
            }

            // Read all, drop oldest row, rewrite file
            var lines = File.ReadAllLines(_filePath).ToList();
            var header = lines[0];
            lines.RemoveAt(1); // drop oldest row
            _currentRowCount = lines.Count - 1;

            File.WriteAllLines(_filePath, lines);
            AppendRow(_filePath, values);
        }

        private void HandleSpillover<T>(IEnumerable<T> values, bool limited)
        {
            if (_currentRowCount < _rolloverCount)
            {
                AppendRow(_filePath, values);
                return;
            }

            // Spill current file
            SpillFiles(limited);
            WriteHeaders(_filePath);
            _currentRowCount = 0;
            AppendRow(_filePath, values);
        }

        private void SpillFiles(bool limited)
        {
            var ext = Path.GetExtension(_filePath);
            var files = Directory.GetFiles(_directory, $"{_baseFileName}_*{ext}");

            int highestIndex = files
                .Select(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var parts = name.Split('_');
                    return int.TryParse(parts.Last(), out var idx) ? idx : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            if (limited && highestIndex >= _maxFiles)
            {
                // Rotate files down
                for (int i = 1; i < _maxFiles; i++)
                {
                    var src = Path.Combine(_directory, $"{_baseFileName}_{i + 1}{ext}");
                    var dst = Path.Combine(_directory, $"{_baseFileName}_{i}{ext}");
                    if (File.Exists(src))
                    {
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }

                var last = Path.Combine(_directory, $"{_baseFileName}_{_maxFiles}{ext}");
                if (File.Exists(last)) File.Delete(last);
                File.Move(_filePath, last);
            }
            else
            {
                var newName = Path.Combine(_directory, $"{_baseFileName}_{highestIndex + 1}{ext}");
                File.Move(_filePath, newName);
            }
        }
    }
}
