using System;

namespace CS2MPMod.Core.Protocol.Messages
{
    /// <summary>Bounded numeric-only disconnect report. No names, paths, endpoints or raw log text.</summary>
    public sealed class DiagnosticReportMessage : INetMessage
    {
        public sealed class Row
        {
            public int Player, Pending;
            public long Received, Completed, OldestSequence, OldestAgeMs;
        }
        public Guid ReportId;
        public bool Acknowledgement;
        public long Epoch, Evicted;
        public Row[] Rows = Array.Empty<Row>();
        public MessageType Type => MessageType.DiagnosticReport;

        public void Write(NetworkWriter writer)
        {
            Validate();
            writer.WriteString(ReportId.ToString("N"));
            writer.WriteBool(Acknowledgement);
            writer.WriteLong(Epoch); writer.WriteLong(Evicted); writer.WriteInt(Rows.Length);
            foreach (Row row in Rows)
            {
                writer.WriteInt(row.Player); writer.WriteInt(row.Pending);
                writer.WriteLong(row.Received); writer.WriteLong(row.Completed);
                writer.WriteLong(row.OldestSequence); writer.WriteLong(row.OldestAgeMs);
            }
        }
        public void Read(NetworkReader reader)
        {
            if (!Guid.TryParseExact(reader.ReadString(), "N", out ReportId))
                throw new ProtocolException("Invalid diagnostic report ID.");
            Acknowledgement = reader.ReadBool(); Epoch = reader.ReadLong(); Evicted = reader.ReadLong();
            int count = reader.ReadInt();
            if (count < 0 || count > 32) throw new ProtocolException("Diagnostic row limit exceeded.");
            Rows = new Row[count];
            for (int i = 0; i < count; i++) Rows[i] = new Row {
                Player = reader.ReadInt(), Pending = reader.ReadInt(), Received = reader.ReadLong(),
                Completed = reader.ReadLong(), OldestSequence = reader.ReadLong(), OldestAgeMs = reader.ReadLong()
            };
            Validate();
        }
        private void Validate()
        {
            if (ReportId == Guid.Empty || Epoch < 0 || Evicted < 0 || Rows == null || Rows.Length > 32 ||
                (Acknowledgement && (Rows.Length != 0 || Epoch != 0 || Evicted != 0)))
                throw new ProtocolException("Invalid diagnostic report.");
            foreach (Row row in Rows)
                if (row == null || row.Player < 0 || row.Pending < 0 || row.Pending > 2048 ||
                    row.Received < 0 || row.Completed < 0 || row.OldestSequence < 0 || row.OldestAgeMs < 0)
                    throw new ProtocolException("Invalid diagnostic row.");
        }
    }
}
