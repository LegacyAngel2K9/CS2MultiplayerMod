using System;
using System.Globalization;
using System.Text;

namespace CS2MPMod.Core.Diagnostics
{
    /// <summary>Local diagnostic contract v1. Null means unobserved, never success or zero latency.</summary>
    public sealed class OperationDiagnosticRecord
    {
        public string Session, Operation, Prefab, Stage, CaptureResult, ApplyResult, RecoveryReason;
        public long? Epoch, Sequence, QueueMs, ApplyMs, ElapsedLocalMs;
        public int Player;
        public ushort? Command;

        public string Format()
        {
            return "operation schema=1 session=" + Token(Session) +
                " epoch=" + Number(Epoch) + " sequence=" + Number(Sequence) + " player=" + Player.ToString(CultureInfo.InvariantCulture) +
                " command=" + Number(Command.HasValue ? (long?)Command.Value : null) +
                " operation=" + Token(Operation) + " prefab=" + Token(Prefab) +
                " stage=" + Token(Stage) + " captureResult=" + Token(CaptureResult) +
                " applyResult=" + Token(ApplyResult) + " queueMs=" + Number(QueueMs) +
                " applyMs=" + Number(ApplyMs) + " elapsedLocalMs=" + Number(ElapsedLocalMs) +
                " recoveryReason=" + Token(RecoveryReason) + " reason=" + Token(RecoveryReason);
        }

        private static string Number(long? value) => value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";

        // Escape delimiters and newlines so prefab/reason text cannot inject another field or event.
        private static string Token(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            int limit = Math.Min(value.Length, 200);
            var safe = new StringBuilder(limit);
            for (int i = 0; i < limit; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        if (i + 1 >= limit) break; // Never split a Unicode pair at the length limit.
                        safe.Append(c).Append(value[++i]);
                    }
                    else safe.Append('\uFFFD');
                }
                else safe.Append(char.IsLowSurrogate(c) ? '\uFFFD' : c);
            }
            return Uri.EscapeDataString(safe.ToString());
        }
    }
}
