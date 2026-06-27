namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

/// <summary>
/// Pinning and formatting for NFPA 13 code citations used throughout SprinkSnap.
/// </summary>
public static class Nfpa13Edition
{
    public const string Year = "2025";

    public const string StandardName = "NFPA 13";

    public const string ShortLabel = StandardName + " (" + Year + ")";

    public const string StandardTitle = "Standard for the Installation of Sprinkler Systems";

    public static string Section(string sectionNumber)
    {
        return ShortLabel + " Section " + sectionNumber;
    }

    public static string Table(string tableNumber)
    {
        return ShortLabel + " Table " + tableNumber;
    }

    public static string Chapter(string chapterNumber)
    {
        return ShortLabel + " Chapter " + chapterNumber;
    }

    public static class References
    {
        public static readonly string HazardClassification = Section("4.3");

        public static readonly string LightHazardOccupancy = Section("4.3.2");

        public static readonly string OrdinaryHazardGroup1 = Section("4.3.3.1");

        public static readonly string OrdinaryHazardGroup2 = Section("4.3.3.2");

        public static readonly string ExtraHazardGroup1 = Section("4.3.4.1");

        public static readonly string ExtraHazardGroup2 = Section("4.3.4.2");

        public static readonly string WaterSupplyInformation = Section("4.5");

        public static readonly string ObstructionsToDischarge = Section("9.5.5");

        public static readonly string StandardSprayObstructions = Section("10.2.8");

        public static readonly string StandardSpraySpacingTable = Table("10.2.4.2.1(a)");

        public static readonly string DesignCriteriaTable = Table("19.2.3.1.1");

        public static readonly string SinglePointDesignCriteria = Section("19.2.3.1.1");

        public static readonly string DensityAreaMethod = Section("19.2.3.2");

        public static readonly string HoseStreamAllowance = Section("19.2.3");

        public static readonly string HydraulicCalculationProcedures = Chapter("28") + " Section 28.2";

        public static readonly string HydraulicGraphSheet = Section("28.4.1.4");

        public static readonly string HazenWilliamsCFactors = Table("28.2.3.2.1");

        public static readonly string FittingEquivalentLengths = Table("28.2.3.1.1");

        public static readonly string DesignReportChapters = Chapter("4")
            + ", "
            + Chapter("9")
            + ", "
            + Chapter("10")
            + ", and "
            + Chapter("19");

        public static readonly string HydraulicReportChapter = Chapter("19") + " and " + Chapter("28");

        public static readonly string HighCeilingDesignCriteria = Section("19.2.3.2.5.2");

        public static readonly string HighCeilingSprinklerSelection = Section("19.2.3.2.5.1");
    }
}
