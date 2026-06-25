using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Reports;

public sealed class ReportExportResult
{
    public string ExportFolder { get; set; } = string.Empty;

    public IList<string> ExportedFiles { get; set; } = new List<string>();

    public IList<string> Errors { get; set; } = new List<string>();

    public bool Success => ExportedFiles.Count > 0 && Errors.Count == 0;
}
