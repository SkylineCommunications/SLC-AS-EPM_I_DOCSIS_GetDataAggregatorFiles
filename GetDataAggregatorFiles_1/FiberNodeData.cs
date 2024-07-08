using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetDataAggregatorFiles_1
{
	internal class FiberNodeData
	{
		public string FnName { get; set; }

		public double PeakUtilization { get; set; }

		public double HighUtilization { get; set; }

		public double LowUtilization { get; set; }
	}
}
