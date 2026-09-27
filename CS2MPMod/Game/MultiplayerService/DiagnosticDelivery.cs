using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Core.Protocol.Messages;
using CS2MPMod.Core.Session;
using CS2MPMod.Game.Diagnostics;

namespace CS2MPMod.Game
{
    public sealed partial class MultiplayerService
    {
        private DiagnosticReportStore _reportOutbox, _reportInbox;
        private string _reportTarget;
        private Guid _activeReportId, _lastReportAttempt;
        private readonly HashSet<Guid> _sentReportIds = new HashSet<Guid>();
        private long _reportEpoch, _nextReportCheckpoint, _nextReportSend;

        private void ConfigureDiagnosticTarget(MultiplayerConfig config)
        {
            string target = config.Transport + "|" + (config.HostAddress ?? "").Trim().ToLowerInvariant() +
                "|" + config.Port + "|" + (config.JoinCode ?? "").Trim().ToUpperInvariant();
            using (var hash = SHA256.Create())
                _reportTarget = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(target))).Replace("-", "");
            _activeReportId = Guid.Empty;
            _lastReportAttempt = Guid.Empty;
            _sentReportIds.Clear();
            _nextReportCheckpoint = _nextReportSend = 0;
        }

        private void EnsureReportStores()
        {
            if (_reportOutbox != null) return;
            string root = Path.Combine(Colossal.PSI.Environment.EnvPath.kUserDataPath, "Logs", "CS2MP-diagnostics");
            _reportOutbox = new DiagnosticReportStore(Path.Combine(root, "outbox"));
            _reportInbox = new DiagnosticReportStore(Path.Combine(root, "received"));
        }

        private bool ReceiveDiagnosticReport(int sender, DiagnosticReportMessage report)
        {
            try
            {
                EnsureReportStores();
                bool stored = _reportInbox.StoreReceived(report);
                if (stored) SyncLog.Event(LogTopic.Session, "Stored numeric client diagnostic report " +
                    report.ReportId.ToString("N") + " from authenticated peer #" + sender +
                    " rows=" + report.Rows.Length + " epoch=" + report.Epoch + ".");
                return stored;
            }
            catch (Exception ex) { ReportDeliveryError(ex); return false; }
        }

        private void AcknowledgeDiagnosticReport(Guid id)
        {
            if (!_sentReportIds.Contains(id) || _reportTarget == null) return;
            try
            {
                EnsureReportStores();
                _reportOutbox.Acknowledge(_reportTarget, id);
                _sentReportIds.Remove(id);
                SyncLog.Event(LogTopic.Session, "Host acknowledged diagnostic report; local pending copy removed.");
            }
            catch (Exception ex) { ReportDeliveryError(ex); }
        }

        private void PumpDiagnosticReports()
        {
            if (_session.Role != SessionRole.Client || _session.Status != SessionStatus.Connected || _reportTarget == null) return;
            if (_activeReportId == Guid.Empty) _activeReportId = Guid.NewGuid();
            _reportEpoch = _session.CommandEpoch;
            if (NowMs >= _nextReportCheckpoint)
            {
                _nextReportCheckpoint = NowMs + 30000;
                SaveCurrentDiagnosticReport();
            }
            if (NowMs < _nextReportSend) return;
            _nextReportSend = NowMs + 10000;
            try
            {
                EnsureReportStores();
                // Only older, immutable reports are retried during a live session.
                var pending = _reportOutbox.Next(_reportTarget, _activeReportId, _lastReportAttempt);
                if (pending == null) return;
                SendPendingDiagnosticReport(pending);
            }
            catch (Exception ex) { ReportDeliveryError(ex); }
        }

        private void SaveCurrentDiagnosticReport()
        {
            if (_activeReportId == Guid.Empty || _reportTarget == null) return;
            try
            {
                EnsureReportStores();
                _reportOutbox.Save(_reportTarget, OperationTrace.CreateReport(_activeReportId, _reportEpoch, NowMs));
            }
            catch (Exception ex) { ReportDeliveryError(ex); }
        }

        private void FinishDiagnosticReport()
        {
            if (_activeReportId == Guid.Empty) return;
            SaveCurrentDiagnosticReport();
            Guid completed = _activeReportId;
            _activeReportId = Guid.Empty; // freeze this checkpoint before send/ack/reconnect
            try
            {
                EnsureReportStores();
                var pending = _reportOutbox.Load(_reportTarget, completed);
                if (pending != null && _session.Status == SessionStatus.Connected)
                {
                    SendPendingDiagnosticReport(pending);
                }
            }
            catch (Exception ex) { ReportDeliveryError(ex); }
        }

        private void SendPendingDiagnosticReport(DiagnosticReportMessage report)
        {
            _lastReportAttempt = report.ReportId;
            // Normally bounded by the 128-file outbox. Keep the bound even if files are removed externally.
            if (_sentReportIds.Count >= 128 && !_sentReportIds.Contains(report.ReportId))
                _sentReportIds.Clear(); // Older acknowledgements are ignored; their files remain retryable.
            _sentReportIds.Add(report.ReportId);
            _session.SendDiagnosticReport(report);
        }

        private static void ReportDeliveryError(Exception ex) => SyncLog.Warn(LogTopic.Session,
            "Diagnostic delivery/storage deferred (" + ex.GetType().Name + "). Existing pending reports are retained.");
    }
}
