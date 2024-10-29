namespace GetDataAggregatorFiles_1
{
    internal class FiberNodeRow
    {
        public string FnName { get; set; }

        public double DsFnUtilization { get; set; }

        public double OfdmFnUtilization { get; set; }

        public double UsFnLowSplitUtilization { get; set; }

        public double UsFnHighSplitUtilization { get; set; }

        public double OfdmaFnUtilization { get; set; }

        public double UsLowPlusOfdmaUtilization { get; set; }
    }
}
