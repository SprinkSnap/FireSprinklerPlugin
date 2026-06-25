using System.IO;
using System.Reflection;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

internal static class TestDataPath
{
    public static string Resolve()
    {
        string outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string outputTestData = Path.Combine(outputDirectory ?? string.Empty, "TestData");
        if (Directory.Exists(outputTestData))
        {
            return outputTestData;
        }

        string projectTestData = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "SprinkSnap.Core.Tests",
            "TestData"));
        return Directory.Exists(projectTestData) ? projectTestData : outputDirectory ?? string.Empty;
    }
}
