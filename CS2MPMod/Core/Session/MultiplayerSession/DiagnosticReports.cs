using System;
using CS2MPMod.Core.Networking;
using CS2MPMod.Core.Protocol.Messages;

namespace CS2MPMod.Core.Session
{
    public sealed partial class MultiplayerSession
    {
        public Func<int, DiagnosticReportMessage, bool> StoreDiagnosticReport;
        public Action<Guid> DiagnosticReportAcknowledged;

        public void SendDiagnosticReport(DiagnosticReportMessage report)
        {
            if (Role == SessionRole.Client && Status == SessionStatus.Connected && report != null && !report.Acknowledgement)
                SendTo(ConnectionId.Server, report);
        }

        private void HandleDiagnosticReport(ConnectionId from, Peer peer, DiagnosticReportMessage report, long now)
        {
            if (peer == null || !peer.Handshaked) return;
            if (Role == SessionRole.Client)
            {
                if (from == ConnectionId.Server && report.Acknowledgement)
                {
                    try { DiagnosticReportAcknowledged?.Invoke(report.ReportId); }
                    catch (Exception ex) { LogObserverError("DiagnosticReportAcknowledged", ex); }
                }
                return;
            }
            if (Role != SessionRole.Host || report.Acknowledgement) return;
            // One disk write attempt per authenticated peer per ten seconds, including duplicate retries.
            if (peer.LastDiagnosticReportMs.HasValue && now - peer.LastDiagnosticReportMs.Value < 10000) return;
            peer.LastDiagnosticReportMs = now;
            try
            {
                if (StoreDiagnosticReport != null && StoreDiagnosticReport(peer.PlayerId, report))
                    SendTo(from, new DiagnosticReportMessage { ReportId = report.ReportId, Acknowledgement = true });
            }
            catch (Exception ex) { LogObserverError("StoreDiagnosticReport", ex); }
        }
    }
}
