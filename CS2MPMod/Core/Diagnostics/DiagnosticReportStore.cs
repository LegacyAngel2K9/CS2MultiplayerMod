using System;
using System.IO;
using System.Linq;
using CS2MPMod.Core.Protocol;
using CS2MPMod.Core.Protocol.Messages;

namespace CS2MPMod.Core.Diagnostics
{
    /// <summary>Atomic, bounded, endpoint-scoped outbox. Only an acknowledged report is removed.</summary>
    public sealed class DiagnosticReportStore
    {
        private readonly string _root;
        private readonly MessageCodec _codec = MessageCodec.CreateDefault();
        public DiagnosticReportStore(string root) { _root = Path.GetFullPath(root); }

        private string PendingPath(string target, Guid id)
        {
            if (target == null || target.Length != 64 || target.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Invalid target hash.");
            return Path.Combine(_root, target + "-" + id.ToString("N") + ".pending");
        }
        public void Save(string target, DiagnosticReportMessage report)
        {
            if (report.Acknowledgement) throw new ArgumentException("Cannot spool acknowledgement.");
            string path = PendingPath(target, report.ReportId);
            Directory.CreateDirectory(_root);
            if (!File.Exists(path) && Directory.GetFiles(_root, "*.pending").Length >= 128)
                throw new IOException("Diagnostic outbox is full; existing reports were preserved.");
            AtomicWrite(path, _codec.Encode(report));
        }
        public DiagnosticReportMessage Next(string target, Guid current, Guid after = default(Guid))
        {
            PendingPath(target, Guid.Empty); // validate before using the glob
            if (!Directory.Exists(_root)) return null;
            string cursor = PendingPath(target, after);
            // Rotate even without an acknowledgement: one rejected report must not starve the rest.
            foreach (string path in Directory.GetFiles(_root, target + "-*.pending")
                .OrderBy(x => string.Compare(x, cursor, StringComparison.OrdinalIgnoreCase) > 0 ? 0 : 1)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (new FileInfo(path).Length > 2048) continue;
                    var report = _codec.Decode(File.ReadAllBytes(path)) as DiagnosticReportMessage;
                    if (report != null && !report.Acknowledgement && report.ReportId != current &&
                        string.Equals(path, PendingPath(target, report.ReportId), StringComparison.OrdinalIgnoreCase)) return report;
                }
                catch (ProtocolException) { /* Preserve corrupt files for local inspection; do not transmit them. */ }
                catch (IOException) { /* A locked or concurrently removed file must not block the other reports. */ }
                catch (UnauthorizedAccessException) { /* Retain inaccessible evidence and try other reports. */ }
            }
            return null;
        }
        public DiagnosticReportMessage Load(string target, Guid id)
        {
            string path = PendingPath(target, id);
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) return null;
            var report = _codec.Decode(File.ReadAllBytes(path)) as DiagnosticReportMessage;
            return report != null && !report.Acknowledgement && report.ReportId == id ? report : null;
        }
        public void Acknowledge(string target, Guid id)
        {
            string path = PendingPath(target, id);
            if (File.Exists(path)) File.Delete(path);
        }
        public bool StoreReceived(DiagnosticReportMessage report)
        {
            if (report.Acknowledgement) return false;
            Directory.CreateDirectory(_root);
            byte[] data = _codec.Encode(report);
            string path = Path.Combine(_root, report.ReportId.ToString("N") + ".report");
            if (File.Exists(path))
                return new FileInfo(path).Length <= 2048 && File.ReadAllBytes(path).SequenceEqual(data);
            if (Directory.GetFiles(_root, "*.report").Length >= 200) return false;
            AtomicWrite(path, data);
            return true;
        }
        private static void AtomicWrite(string path, byte[] data)
        {
            string temp = path + ".tmp";
            if (!File.Exists(temp) && Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length >= 128)
                throw new IOException("Diagnostic temporary-file budget reached; existing evidence retained.");
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
