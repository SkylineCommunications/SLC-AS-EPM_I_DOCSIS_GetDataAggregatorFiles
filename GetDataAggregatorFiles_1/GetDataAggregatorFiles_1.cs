/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

02/07/2024	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace GetDataAggregatorFiles_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Skyline.DataMiner.Automation;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;
	using System.Linq;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private Dictionary<string, FiberNodeData> dictOfdmaValues = new Dictionary<string, FiberNodeData>();
		private Dictionary<string, FiberNodeData> dictUsLowSplitValues = new Dictionary<string, FiberNodeData>();
		private Dictionary<string, FiberNodeData> dictUsHighSplitValues = new Dictionary<string, FiberNodeData>();
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			var initTime = engine.GetScriptParam("initialTime").Value;
			var endTime = engine.GetScriptParam("finalTime").Value;
			var isDs = engine.GetScriptParam("isDs").Value.ToBool();

			string format = "MM/dd/yyyy HH:mm:ss";
			DateTime initDateTime;
			DateTime endDateTime;
			Dictionary<string, FiberNodeData> dictQamValues = new Dictionary<string, FiberNodeData>();
			Dictionary<string, FiberNodeData> dict31Values = new Dictionary<string, FiberNodeData>();

			string basePathDsPeak = @"C:\Skyline_Data\DataMiner_Aggregator\SessionRecords\";
			string basePathOfdmPeak = @"C:\Skyline_Data\DataMiner_Aggregator\SessionRecords\";

			if (DateTime.TryParseExact(initTime, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out initDateTime) &&
				DateTime.TryParseExact(endTime, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out endDateTime))
			{
				int minSpan = 1;
				TimeSpan span = endDateTime - initDateTime;

				var validPathsDsPeak = GetPaths(engine, isDs ? basePathDsPeak + "DS_PEAK" : basePathDsPeak + "US_PEAK", span.Days == 0 ? minSpan : span.Days, endDateTime);
				SendPathsToRead(engine, validPathsDsPeak, dictQamValues, isDs);

				var validPathsOfdmPeak = GetPaths(engine, isDs ? basePathOfdmPeak + "OFDM_PEAK" : basePathDsPeak + "OFDMA_PEAK", span.Days == 0 ? minSpan : span.Days, endDateTime);
				SendPathsToRead(engine, validPathsOfdmPeak, dict31Values, isDs);

				var fiberNodeRow = MergeDictionaries(engine, dictQamValues, dict31Values, isDs);

				engine.AddScriptOutput("Response", JsonConvert.SerializeObject(fiberNodeRow));
			}
		}

		private List<string> GetPaths(IEngine engine, string basePath, int totalDays, DateTime endDateTime)
		{
			DateTime inputDate = endDateTime;
			List<string> paths = new List<string>();
			for (int i = 0; i < totalDays; i++)
			{
				DateTime date = inputDate.AddDays(-i);
				string year = date.ToString("yyyy");
				string month = date.ToString("MM");
				string day = date.ToString("dd");
				string path = Path.Combine(basePath, year, month, day);
				paths.Add(path);
			}

			List<string> validPaths = new List<string>();

			foreach (string path in paths)
			{
				if (Directory.Exists(path))
				{
					validPaths.Add(path);
				}
				else
				{
					engine.Log($"AS GetDataAggregatorFiles | Path does not exist: {path}");
				}
			}

			return validPaths;
		}

		private void SendPathsToRead(IEngine engine, List<string> folderPaths, Dictionary<string, FiberNodeData> fiberNodeDict, bool isDs)
		{
			foreach (string path in folderPaths)
			{
				try
				{
					string[] files = Directory.GetFiles(path);
					foreach (string logFile in files)
					{
						using (Stream stream = File.Open(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
						using (StreamReader sr = new StreamReader(stream))
						{
							if (isDs)
							{
								ProcessDsFile(sr, engine, fiberNodeDict);
							}
							else
							{
								ProcessUsFile(sr, engine, logFile);
							}
						}
					}
				}
				catch (Exception ex)
				{
					engine.Log($"AS GetDsFNPeaks | Could not process path {path}. Error: {ex.Message}");
				}
			}
		}

		private void ProcessDsFile(StreamReader sr, IEngine engine, Dictionary<string, FiberNodeData> fiberNodeDict)
		{
			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine();
				string[] parts = line.Split(',');
				FillDsValues(parts, engine, fiberNodeDict);
			}
		}

		private void ProcessUsFile(StreamReader sr, IEngine engine, string logFile)
		{
			bool ofdmaFile = false;
			string line = sr.ReadLine();

			if (line.StartsWith("\"ID\",\"Fiber Node\",\"Peak Utilization\""))
			{
				ofdmaFile = true;
			}
			else if (line.StartsWith("\"ID\",\"Fiber Node\",\"Low Split Utilization\",\"High Split Utilization\""))
			{
				ofdmaFile = false;
			}

			while (!sr.EndOfStream)
			{
				line = sr.ReadLine();
				string[] parts = line.Split(',');
				if (ofdmaFile)
				{
					FillOfdmaValues(parts, engine);
				}
				else
				{
					FillUsValues(parts, engine);
				}
			}
		}

		private void FillOfdmaValues(string[] parts, IEngine engine)
		{
			if (parts.Length >= 3)
			{
				string fiberNode = parts[0].Trim('"');
				string fiberNodeName = parts[1].Trim('"');
				string utilizationStr = parts[2].Trim('"');

				if (double.TryParse(utilizationStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double utilization))
				{
					if (dictOfdmaValues.ContainsKey(fiberNode))
					{
						if (utilization > dictOfdmaValues[fiberNode].PeakUtilization)
						{
							dictOfdmaValues[fiberNode].PeakUtilization = utilization;
						}
					}
					else
					{
						var fiberNodeRow = new FiberNodeData
						{
							FnName = fiberNodeName,
							PeakUtilization = utilization,
						};

						dictOfdmaValues[fiberNode] = fiberNodeRow;
					}
				}
			}
		}


		private void FillUsValues(string[] parts, IEngine engine)
		{
			if (parts.Length >= 4)
			{
				string fiberNode = parts[0].Trim('"'); // Get FN ID
				string fiberNodeName = parts[1].Trim('"'); // Get FN Name
				string utilizationStrLow = parts[2].Trim('"'); // Get Low Peaks
				string utilizationStrHigh = parts[3].Trim('"'); // Get high Peaks

				if (double.TryParse(utilizationStrLow, NumberStyles.Float, CultureInfo.InvariantCulture, out double lowUtilization))
				{
					if (dictUsLowSplitValues.ContainsKey(fiberNode))
					{
						if (lowUtilization > dictUsLowSplitValues[fiberNode].PeakUtilization)
						{
							dictUsLowSplitValues[fiberNode].PeakUtilization = lowUtilization;
						}
					}
					else
					{
						var fiberNodeRow = new FiberNodeData
						{
							FnName = fiberNodeName,
							PeakUtilization = lowUtilization,
						};

						dictUsLowSplitValues[fiberNode] = fiberNodeRow;
					}
				}

				if (double.TryParse(utilizationStrHigh, NumberStyles.Float, CultureInfo.InvariantCulture, out double highUtilization))
				{
					if (dictUsHighSplitValues.ContainsKey(fiberNode))
					{
						if (highUtilization > dictUsHighSplitValues[fiberNode].PeakUtilization)
						{
							dictUsHighSplitValues[fiberNode].PeakUtilization = highUtilization;
						}
					}
					else
					{
						var fiberNodeRow = new FiberNodeData
						{
							FnName = fiberNodeName,
							PeakUtilization = highUtilization,
						};

						dictUsHighSplitValues[fiberNode] = fiberNodeRow;
					}
				}
			}
		}


		private void FillDsValues(string[] parts, IEngine engine, Dictionary<string, FiberNodeData> fiberNodeDict)
		{
			if (parts.Length >= 3)
			{
				string fiberNode = parts[0].Trim('"');
				string fiberNodeName = parts[1].Trim('"');
				string utilizationStr = parts[2].Trim('"');

				if (double.TryParse(utilizationStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double utilization))
				{
					if (fiberNodeDict.ContainsKey(fiberNode))
					{
						if (utilization > fiberNodeDict[fiberNode].PeakUtilization)
						{
							fiberNodeDict[fiberNode].PeakUtilization = utilization;
						}
					}
					else
					{
						var fiberNodeRow = new FiberNodeData
						{
							FnName = fiberNodeName,
							PeakUtilization = utilization,
						};

						fiberNodeDict[fiberNode] = fiberNodeRow;
					}
				}
			}
		}

		private Dictionary<string, FiberNodeRow> MergeDictionaries(IEngine engine, Dictionary<string, FiberNodeData> dictDsValues, Dictionary<string, FiberNodeData> dictOfdmValues, bool isDs)
		{
			Dictionary<string, FiberNodeRow> mergedDict = new Dictionary<string, FiberNodeRow>();

			Dictionary<string, string> mergedDictDs = dictDsValues.Concat(dictOfdmValues)
													.GroupBy(x => x.Key)
													.ToDictionary(x => x.Key, x => x.Last().Value.FnName);

			Dictionary<string, string> mergedDictUs = dictOfdmaValues.Concat(dictUsLowSplitValues)
													.GroupBy(x => x.Key)
													.ToDictionary(x => x.Key, x => x.Last().Value.FnName);

			var keys = isDs ? dictDsValues.Keys.Union(dictOfdmValues.Keys) : dictUsLowSplitValues.Keys.Union(dictOfdmaValues.Keys);

			foreach (var key in keys)
			{
				mergedDict[key] = new FiberNodeRow
				{
					FnName = isDs ? mergedDictDs[key] : mergedDictUs[key],
					DsFnUtilization = dictDsValues.ContainsKey(key) ? dictDsValues[key].PeakUtilization : -1,
					OfdmFnUtilization = dictOfdmValues.ContainsKey(key) ? dictOfdmValues[key].PeakUtilization : -1,
					UsFnLowSplitUtilization = dictUsLowSplitValues.ContainsKey(key) ? dictUsLowSplitValues[key].PeakUtilization : -1,
					UsFnHighSplitUtilization = dictUsHighSplitValues.ContainsKey(key) ? dictUsHighSplitValues[key].PeakUtilization : -1,
					OfdmaFnUtilization = dictOfdmaValues.ContainsKey(key) ? dictOfdmaValues[key].PeakUtilization : -1,
				};
			}

			return mergedDict;
		}
	}
}
