using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace PruneLibrary
{
    public class PruneProcessInstance : IDisposable
    {
        private readonly double _totalCpuThreshold; //number of logical processors

        //Performance counters
        private PerformanceCounter _cpuPc;
        private PerformanceCounter _privBytesPc;
        private PerformanceCounter _workingSetPc;

        //Process name information
        public int ProcessId;
        public string WhitelistEntry;

        //true if library called from Prune Service, false if called from Prune command line tool
        private readonly bool _isService;

        //Set to true after the process's performance counters are set to null and after it logs for the last time.
        public bool ProcessFinished { get; private set; }

        //Intervals in seconds
        private readonly uint _writeCacheInterval; //length of time before writing cache to a file
        private readonly uint _logInterval; //length of time to wait before calculating and logging usage stats

        //file and directory information
        private readonly string _rootDirectory;
        private string _fileSuffix; //Used to create the cache file names

        //collections
        private readonly List<DataPoint> _cache = new List<DataPoint>(); //List of data collected during a single _cache interval
        private readonly List<string> _unloggedFiles = new List<string>(); //Files that have been cached but not been logged yet

        //Threads
        private Thread _loggingThread;

        //Time variables
        private DateTime _cacheStart; //Start of current cache interval
        private DateTime _cacheFinish; //end of current cache interval
        private DateTime _logStart; //start of current stat logging interval
        private DateTime _logFinish = DateTime.MinValue; //end of current stat logging

		//Semaphores
		Semaphore PerfCounterSem = new Semaphore(1,1);

        public PruneProcessInstance(bool service, int procId, string whitelistEnt, uint writeInterval, uint logInterval, string root) : this(service, procId, whitelistEnt, writeInterval, logInterval, root, DateTime.MinValue)
        {
            //Empty constructor just used to call the other one    
        }

        /*
         * constructor for the base class  One instance exists for each process that is being monitored
         * @params: Process name, cache write interval, log interval, root directory for service files, and the event log to use
         */
        public PruneProcessInstance(bool service, int procId, string whitelistEnt, uint writeInterval, uint logInterval, string root, DateTime intervalStart)
        {
            ProcessId = procId;
            _writeCacheInterval = writeInterval;
            _logInterval = logInterval;
            _rootDirectory = root;
            _isService = service;
            WhitelistEntry = whitelistEnt;

            //calculate the maximum value for the CPU performance counter, number of logical processor cores times 100.
            _totalCpuThreshold = Environment.ProcessorCount * 100;

			//If something goes wrong and this value equals zero, stop this from processing
			if(_totalCpuThreshold == 0) {
				ProcessFinished = true;
			}

            _cacheStart = intervalStart;
            _logStart = intervalStart;
        }

        //Initialize the counters for the current process
        public void InitializeInstance()
        {
			if(ProcessFinished) {
				return;
			}

			//Set up the performance counters
			SetPerfCounters();

            //set the start and end times 
            if (DateTime.Equals(DateTime.MinValue, _logStart))
            {
                _logStart = DateTime.Now;
            }
            if (DateTime.Equals(DateTime.MinValue, _cacheStart))
            {
                _cacheStart = DateTime.Now;
            }

            //Different interval finishes are needed if this is run from the command line or the service
            if (_isService)
            {
                //From the service, we need to set the log and cache interval end times
                _logFinish = Prune.CalculateIntervalFinishTime(_logStart, _logInterval);
                _cacheFinish = Prune.CalculateIntervalFinishTime(_cacheStart, _writeCacheInterval);
            }
            else
            {
                //From the command line, we don't use the logFinish and cacheFinish is simply the current time plus the length to monitor
                _cacheFinish = DateTime.Now.AddSeconds(_writeCacheInterval);
            }

            //create the current file suffix for use later in the cache files
            _fileSuffix = "-" + _cacheStart.ToString("yyyyMMdd_HHmmss") + "-" + _cacheFinish.ToString("yyyyMMdd_HHmmss") + ".json";
        }

        private void SetPerfCounters()
        {            
			PerfCounterSem.WaitOne();

			//An additional check to prevent processes marked as finished from continuing
			if(ProcessFinished) {
				PerfCounterSem.Release();
				return;
			}

			string instanceName;

            //Get the instance name that corresponds to the process id
            try
            {
                instanceName = Prune.GetInstanceNameForProcessId(ProcessId);
            }
            catch (Exception e)
            {
                Prune.HandleError(_isService, 0, "Error getting instance name. Setting it to null. This error likely does not affect PRUNE's Functionality. " + Environment.NewLine + e.Message);
                instanceName = null;
            }

            //if the string is not null or empty, set up Perf Counters
            if (!String.IsNullOrWhiteSpace(instanceName))
            {
				try
                {
                    //Create the % processor time counter and start it
                    //the first next value call is required to begin gathering information on the process
                    _cpuPc = new PerformanceCounter("Process", "% Processor Time", instanceName, true);
                    _cpuPc.NextValue();
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to initialize % Processor Time performance counter" + Environment.NewLine + e.Message);
					PerfCounterSem.Release();
					return;
                }

                try
                {
                    //create the private bytes counter and start it
                    _privBytesPc = new PerformanceCounter("Process", "Private Bytes", instanceName, true);
                    _privBytesPc.NextValue();
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to initialize Private Bytes performance counter" + Environment.NewLine + e.Message);
					PerfCounterSem.Release();
					return;
                }

                try
                {
                    //create the working set counter and start it
                    _workingSetPc = new PerformanceCounter("Process", "Working Set - Private", instanceName, true);
                    _workingSetPc.NextValue();
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to initialize Private Working Set performance counter" + Environment.NewLine + e.Message);
					PerfCounterSem.Release();
					return;
                }

                //give the counters a quarter of a second to ensure they start to gather data
                Thread.Sleep(250);
            }
            else
            {
                //The procName was null or empty
				FinishMonitoring();
			}

			PerfCounterSem.Release();
		}

        //Set all performance counters to null
        public void NullPerformanceCounters()
        {
			_cpuPc = null;
            _privBytesPc = null;
            _workingSetPc = null;
        }

        //Go through the currently cached data and write it to a file
        public void WriteCacheToFile()
        {
            //Only write contents of the cache to a file if there is something in the cache
            if (_cache.Count > 0)
            {
                //create the full path and file name
                string directory = _rootDirectory + "\\" + WhitelistEntry;

                //If this is being used form the command line tool, the file should go straight into
                //      The logged sub-directory of the process directory
                if (!_isService)
                {
                    directory = directory + "\\logged";
                }

                string fileName = directory + "\\" + WhitelistEntry + "_" + ProcessId + _fileSuffix;


                if (!_isService)
                {
                    Console.WriteLine("\nWriting to file: " + fileName);
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    //convert the cache list to json format and write
                    string cacheJson = JsonConvert.SerializeObject(_cache, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

                    using (StreamWriter sw = new StreamWriter(fileName, false))
                    {
                        sw.Write(cacheJson);
                        sw.Flush();
                    }
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Error writing cache to file" + Environment.NewLine + e.Message);
                }

                //Only add the file to the unlogged file list if it was generated by the service
                //Additional code also only needed for service, as the command line tool ends after writing first file
                if (_isService)
                {
                    //add this file to the list of unlogged files
                    lock (_unloggedFiles)
                    {
                        _unloggedFiles.Add(fileName);
                    }

                    //recreate the file suffix for the next file
                    _fileSuffix = "-" + _cacheStart.ToString("yyyyMMdd_HHmmss") + "-" +
                                 _cacheFinish.ToString("yyyyMMdd_HHmmss") + ".json";

                    //clear the cache list for future use
                    _cache.Clear();
                }
            }

            //Only log from files if this is running from the service
            if (_isService)
            {
                //after the cache has been cleared, check to see if the logging interval has passed
                //  And verify that there is a file that needs logging
                if (DateTime.Compare(_logFinish, DateTime.MinValue) > 0 &&
                    DateTime.Compare(_logFinish, DateTime.Now) < 0 && _unloggedFiles.Count > 0)
                {
                    //if it has, launch a thread to process the information from the files and log the results
                    try
                    {
                        _loggingThread = new Thread(LogDataFromCacheFiles);
                        _loggingThread.Start();
                    }
                    catch (Exception e)
                    {
                        Prune.HandleError(_isService, 0, "Failed to start thread to log usage statistics" + Environment.NewLine + e.Message);
                    }
                }
            }
        }

        //read in data from the cache files and process it
        //  output is an event in the event log
        public void LogDataFromCacheFiles()
        {
            List<string> unloggedCopy;

            lock (_unloggedFiles)
            {
                //make a copy of the unlogged files list and then clear the original so more entries can be added
                unloggedCopy = new List<string>(_unloggedFiles);
                _unloggedFiles.Clear();
            }

            LogDataFromFiles(unloggedCopy, false);
        }

        public void LogDataFromFiles(List<string> list, bool loggingFromOldFiles)
        {
			//the number of data points processed
			uint dataPointCount = 0;

            //variables to hold calculations
            double averageCpu = 0;
            double maxCpu = double.MinValue;
            double minCpu = double.MaxValue;

            long averagePriv = 0;
            long maxPriv = long.MinValue;
            long minPriv = long.MaxValue;

            long averageWorking = 0;
            long maxWorking = long.MinValue;
            long minWorking = long.MaxValue;

            long averageTcpOut = 0;
            long maxTcpOut = long.MinValue;
            long minTcpOut = long.MaxValue;

            long averageTcpIn = 0;
            long maxTcpIn = long.MinValue;
            long minTcpIn = long.MaxValue;

            long averageUdpOut = 0;
            long maxUdpOut = long.MinValue;
            long minUdpOut = long.MaxValue;

            long averageUdpIn = 0;
            long maxUdpIn = long.MinValue;
            long minUdpIn = long.MaxValue;

            long averageDiskReadBytes = 0;
            long maxDiskReadBytes = long.MinValue;
            long minDiskReadBytes = long.MaxValue;

            long averageDiskWriteBytes = 0;
            long maxDiskWriteBytes = long.MinValue;
            long minDiskWriteBytes = long.MaxValue;

            long averageDiskReadOps = 0;
            long maxDiskReadOps = long.MinValue;
            long minDiskReadOps = long.MaxValue;

            long averageDiskWriteOps = 0;
            long maxDiskWriteOps = long.MinValue;
            long minDiskWriteOps = long.MaxValue;

            Dictionary<string, TcpConnectionData> connectionData = new Dictionary<string, TcpConnectionData>();

            if (list == null || list.Count == 0)
            {
                return;
            }

            //loop through all unlogged files
            foreach (string fileName in list)
            {
                List<DataPoint> fileList = new List<DataPoint>();

                try
                {
                    //parse file into an object
                    string fileText = File.ReadAllText(fileName);

                    //Doing this the hard way because the default deserialization methods produce null value errors for some reason
                    //Deserailize the array
                    dynamic o = JsonConvert.DeserializeObject(fileText);

                    //Loop through each array element
                    foreach (Newtonsoft.Json.Linq.JObject obj in (Newtonsoft.Json.Linq.JArray)o)
                    {
                        //Create a new DataPoint from each property of the object, each of which must be converted to the correct type with ToObject<>()
                        fileList.Add(new DataPoint(obj["CpuVal"].ToObject<double>(), obj["PrivBytesVal"].ToObject<long>(), obj["WorkingBytesVal"].ToObject<long>(),
                            obj["DiskBytesReadVal"].ToObject<long>(), obj["DiskBytesWriteVal"].ToObject<long>(), obj["DiskOpsReadVal"].ToObject<long>(),
                            obj["DiskOpsWriteVal"].ToObject<long>(), obj["UdpSent"].ToObject<long>(),
                            obj["UdpRecv"].ToObject<long>(), obj["TcpSent"].ToObject<long>(), obj["TcpRecv"].ToObject<long>(), obj["ConnectionsSent"].ToObject<Dictionary<string, long>>(),
                            obj["ConnectionsSentCount"].ToObject<Dictionary<string, long>>(), obj["ConnectionsReceived"].ToObject<Dictionary<string, long>>(), 
							obj["ConnectionsReceivedCount"].ToObject<Dictionary<string, long>>(), obj["LogTime"].ToObject<DateTime>())
                        );
                    }
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to read file " + fileName + " and deserialize it" + Environment.NewLine + e.Message);
                    return;
                }

                if (fileList == null || fileList.Count == 0)
                {
                    continue;
                }

                //loop through the array doing the math as normal
                //increment dataPointCount in the dataPoint loop
                foreach (DataPoint data in fileList)
                {
                    dataPointCount++;

                    double cpu = data.CpuVal;
                    long priv = data.PrivBytesVal;
                    long work = data.WorkingBytesVal;
                    long tcpIn = data.TcpRecv;
                    long tcpOut = data.TcpSent;
                    long udpIn = data.UdpRecv;
                    long udpOut = data.UdpSent;
                    long diskReadBytes = data.DiskBytesReadVal;
                    long diskWriteBytes = data.DiskBytesWriteVal;
                    long diskReadOps = data.DiskOpsReadVal;
                    long diskWriteOps = data.DiskOpsWriteVal;

                    averageCpu += cpu;
                    averagePriv += priv;
                    averageWorking += work;
                    averageTcpIn += tcpIn;
                    averageTcpOut += tcpOut;
                    averageUdpIn += udpIn;
                    averageUdpOut += udpOut;
                    averageDiskReadBytes += diskReadBytes;
                    averageDiskWriteBytes += diskWriteBytes;
                    averageDiskReadOps += diskReadOps;
                    averageDiskWriteOps += diskWriteOps;

                    if (cpu > maxCpu)
                    {
                        maxCpu = cpu;
                    }
                    if (cpu < minCpu)
                    {
                        minCpu = cpu;
                    }

                    if (priv > maxPriv)
                    {
                        maxPriv = priv;
                    }
                    if (priv < minPriv)
                    {
                        minPriv = priv;
                    }

                    if (work > maxWorking)
                    {
                        maxWorking = work;
                    }
                    if (work < minWorking)
                    {
                        minWorking = work;
                    }

                    if (tcpIn > maxTcpIn)
                    {
                        maxTcpIn = tcpIn;
                    }
                    if (tcpIn < minTcpIn)
                    {
                        minTcpIn = tcpIn;
                    }

                    if (tcpOut > maxTcpOut)
                    {
                        maxTcpOut = tcpOut;
                    }
                    if (tcpOut < minTcpOut)
                    {
                        minTcpOut = tcpOut;
                    }

                    if (udpIn > maxUdpIn)
                    {
                        maxUdpIn = udpIn;
                    }
                    if (udpIn < minUdpIn)
                    {
                        minUdpIn = udpIn;
                    }

                    if (udpOut > maxUdpOut)
                    {
                        maxUdpOut = udpOut;
                    }
                    if (udpOut < minUdpOut)
                    {
                        minUdpOut = udpOut;
                    }

                    if (diskReadBytes > maxDiskReadBytes)
                    {
                        maxDiskReadBytes = diskReadBytes;
                    }
                    if (diskReadBytes < minDiskReadBytes)
                    {
                        minDiskReadBytes = diskReadBytes;
                    }

                    if (diskWriteBytes > maxDiskWriteBytes)
                    {
                        maxDiskWriteBytes = diskWriteBytes;
                    }
                    if (diskWriteBytes < minDiskWriteBytes)
                    {
                        minDiskWriteBytes = diskWriteBytes;
                    }

                    if (diskReadOps > maxDiskReadOps)
                    {
                        maxDiskReadOps = diskReadOps;
                    }
                    if (diskReadOps < minDiskReadOps)
                    {
                        minDiskReadOps = diskReadOps;
                    }

                    if (diskWriteOps > maxDiskWriteOps)
                    {
                        maxDiskWriteOps = diskWriteOps;
                    }
                    if (diskWriteOps < minDiskWriteOps)
                    {
                        minDiskWriteOps = diskWriteOps;
                    }

					//Add connection sent bytes to our DataPoint object
					foreach (string key in data.ConnectionsSent.Keys)
                    {
						//First make sure the key is present in dictionary
						if (!connectionData.ContainsKey(key)) {
							connectionData.Add(key, new TcpConnectionData(key));
						}

						//add the data
						connectionData[key].AddOutData(data.ConnectionsSent[key]);
                    }

					//Add connection sent count to our DataPoint object
					foreach (string key in data.ConnectionsSentCount.Keys) {
						if (!connectionData.ContainsKey(key)) {
							connectionData.Add(key, new TcpConnectionData(key));
						}

						connectionData[key].AddOutCount(data.ConnectionsSentCount[key]);
					}

					//Add connection received bytes to our DataPoint object
					foreach (string key in data.ConnectionsReceived.Keys)
                    {
                        if (!connectionData.ContainsKey(key))
                        {
                            connectionData.Add(key, new TcpConnectionData(key));
                        }

						connectionData[key].AddInData(data.ConnectionsReceived[key]);
                    }

					//Add connection received count to our DataPoint object
					foreach (string key in data.ConnectionsReceivedCount.Keys) {
						if (!connectionData.ContainsKey(key)) {
							connectionData.Add(key, new TcpConnectionData(key));
						}

						connectionData[key].AddInCount(data.ConnectionsReceivedCount[key]);
					}

				}

				string[] fileNameSplit = fileName.Split('\\');
                string loggedDirectory = _rootDirectory + "\\" + WhitelistEntry + "\\logged\\";

				try
                {
                    //Move the file, that has been completely processed, into the logged subfolder
                    Directory.CreateDirectory(loggedDirectory);

                    //Move the file to the logged directory
                    File.Move(fileName, loggedDirectory + fileNameSplit[fileNameSplit.Length - 1]);
				}
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to move file " + fileName + " to " + loggedDirectory + fileNameSplit[fileNameSplit.Length - 1] + " after logging" + Environment.NewLine + e.Message);
                }
            }

            if (dataPointCount == 0)
            {
                return;
            }

            //Get a few total for the I/O related measures
            long totalTcpIn = averageTcpIn;
            long totalTcpOut = averageTcpOut;
            long totalUdpIn = averageUdpIn;
            long totalUdpOut = averageUdpOut;
            long totalDiskReadBytes = averageDiskReadBytes;
            long totalDiskWriteBytes = averageDiskWriteBytes;
            long totalDiskReadOps = averageDiskReadOps;
            long totalDiskWriteOps = averageDiskWriteOps;

            //divide the sums to get averages
            averageCpu /= dataPointCount;
            averagePriv /= dataPointCount;
            averageWorking /= dataPointCount;
            averageTcpIn /= dataPointCount;
            averageTcpOut /= dataPointCount;
            averageUdpIn /= dataPointCount;
            averageUdpOut /= dataPointCount;
            averageDiskReadBytes /= dataPointCount;
            averageDiskReadOps /= dataPointCount;
            averageDiskWriteBytes /= dataPointCount;
            averageDiskWriteOps /= dataPointCount;

			try
            {
				string Connections = "";

				foreach (TcpConnectionData data in connectionData.Values) {
					Connections += data.ToString();
				}

				if (String.IsNullOrWhiteSpace(Connections)) {
					Connections = "No Connections";
				}

				try {
					//call the event
					bool returnVal = PruneEvents.PRUNE_EVENT_PROVIDER.EventWritePROCESS_REPORT_EVENT(WhitelistEntry + "_" + ProcessId,
						dataPointCount, Prune.Processors, Prune.Disks, Prune.ComputerManufacturer, Prune.ComputerModel,
						Prune.ComputerProcessorNum, Prune.RamSize, minCpu, maxCpu,
						averageCpu, minWorking, maxWorking, averageWorking, minPriv, maxPriv, averagePriv, totalDiskReadBytes,
						minDiskReadBytes, maxDiskReadBytes, averageDiskReadBytes, totalDiskWriteBytes, minDiskWriteBytes,
						maxDiskWriteBytes, averageDiskWriteBytes, totalDiskReadOps, minDiskReadOps, maxDiskReadOps, averageDiskReadOps,
						totalDiskWriteOps, minDiskWriteOps, maxDiskWriteOps, averageDiskWriteOps, totalTcpIn, minTcpIn, maxTcpIn,
						averageTcpIn, totalTcpOut, minTcpOut, maxTcpOut, averageTcpOut, totalUdpIn, minUdpIn, maxUdpIn, averageUdpIn,
						totalUdpOut, minUdpOut, maxUdpOut, averageUdpOut, Connections);
				} catch (Exception e) {
					Prune.HandleError(_isService, 0, "Error printing data report event" + Environment.NewLine + e.Message);
				}
			}
            catch (Exception e)
            {
                Prune.HandleError(_isService, 0, "Error outputting statistics to log" + Environment.NewLine + e.Message);
            }

			//If all of the performance counters are null, then we can stop monitoring this process
			if (_cpuPc == null && _privBytesPc == null && _workingSetPc == null)
            {
                ProcessFinished = true;
            }

			//If this is not logging files left over from a previous running of the tool, then advance the log interval times
			if (!loggingFromOldFiles)
            {
                _logStart = _logFinish;
                _logFinish = _logStart.AddSeconds(_logInterval);
            }
		}

		//Log that we are closing, dump anything in the data cache, and then immediately log from the cache files since we may never come back to that data
        public void FinishMonitoring()
        {
			PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteFINISHED_EVENT(WhitelistEntry + "_" + ProcessId);
            DumpCache();
            LogDataFromCacheFiles();
			this.ProcessFinished = true;
        }

        //The service is stopping, so we need to dump the current cache to a file
        public void DumpCache()
        {
            //Only write contents of the cache to a file if there is something in the cache
            if (_cache.Count > 0)
            {
                //Create file name and full path
                string fileName = _rootDirectory + "\\" + WhitelistEntry + "\\" + WhitelistEntry + "_" + ProcessId + "-" + _cacheStart.ToString("yyyyMMdd_HHmmss") + "-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

                //Create the process directory if it does not already exist
                Directory.CreateDirectory(_rootDirectory + "\\" + WhitelistEntry + "\\");

                try
                {
                    //convert list to json
                    string cacheJson = JsonConvert.SerializeObject(_cache);

                    //write json to the file
                    using (StreamWriter sw = new StreamWriter(fileName))
                    {
                        sw.Write(cacheJson);
                        sw.Flush();
                    }

                    _unloggedFiles.Add(fileName);
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Failed to dump cache to " + fileName + " on service shutdown" + Environment.NewLine + e.Message);
                }
            }
        }

        //Record the next data point from each counter and add it to the cache list
        public bool GetData()
        {

			if(ProcessFinished) {
				return false;
			}

			//If the current cache interval is over
			if (_isService && DateTime.Compare(_cacheFinish, DateTime.Now) < 0)
            {
				//adjust the cache times to the next interval
				_cacheStart = _cacheFinish;
                _cacheFinish = _cacheStart.AddSeconds(_writeCacheInterval);

                //write the current cache to a file before adding new information to the cache
                WriteCacheToFile();

            }

			if(_cpuPc == null || _privBytesPc == null || _workingSetPc == null) {
				//The perf counters are null, so we need to try to reassign them. They may have been nulled because another instance with the same name closed and broke things
				//If the process really did exit, then they will remain null after this call
				SetPerfCounters();
			}

            //add the data inside of a DataPoint object
            //The Cpu value is divided by the total cpu threshold to convert it to a percent out of 100, where 100% is total utilization of all processor cores
            //Only do this if all of the Perf Counters are initialized properly
            if (_cpuPc != null && _privBytesPc != null && _workingSetPc != null)
            {
				try {
					if (_cpuPc.InstanceName.CompareTo(Prune.GetInstanceNameForProcessId(this.ProcessId)) != 0) {
						//The PID we are monitoring exists but does not match our the instance name used by the perf counters
						//	Therefore, we must reset them so they are correct. Nulling them will skip this data gathering but allow
						//	us to reset them on the next data call
						NullPerformanceCounters();
					}
				} catch (Exception) {
					//This happens if the current pid we are monitoring no longer exists, so we tell this instance to close and null the perf counters on all
					//	other processes that share the same name to prevent instance name confusion and errors
					FinishMonitoring();
					return false;
				}

				double cpuAdjusted;
                double privValue;
                double workingValue;

				//Get the data from the performance counters in a try catch in case one of the counters has closed
                try
                {
					cpuAdjusted = (_cpuPc.NextValue() / _totalCpuThreshold) * 100;
                    privValue = _privBytesPc.NextValue();
                    workingValue = _workingSetPc.NextValue();
                }
                catch (Exception) { 
                    if (_isService)
                    {
						PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteCANNOT_GATHER_EVENT(WhitelistEntry + "_" + ProcessId);
                    }

					FinishMonitoring();
                    return false;
                }

				//reset localEtwCounters
				Prune.Counters localEtwCounters = null;

                //Get the ETW data, which includes Disk I/O and Network I/O
                try
                {
					localEtwCounters = Prune.GetEtwDataForProcess(ProcessId);
                }
                catch (Exception e)
                {
                    Prune.HandleError(_isService, 0, "Error getting ETW Data:" + Environment.NewLine + e.Message);
                }

				//If nothing is retrieved, create a new Counter object with all values set to 0
				if (localEtwCounters == null)
                {
					localEtwCounters = new Prune.Counters();
                }

				//Add the data point to the cache
				DataPoint tempDataPoint = new DataPoint(cpuAdjusted, Convert.ToInt64(privValue), Convert.ToInt64(workingValue), localEtwCounters.DiskReadBytes, localEtwCounters.DiskWriteBytes,
                        localEtwCounters.DiskReadOperations, localEtwCounters.DiskWriteOperations, localEtwCounters.UdpSent, localEtwCounters.UdpReceived,
                        localEtwCounters.TcpSent, localEtwCounters.TcpReceived, localEtwCounters.ConnectionsSent, localEtwCounters.ConnectionsSentCount, localEtwCounters.ConnectionsReceived, 
						localEtwCounters.ConnectionsReceivedCount, DateTime.Now);
                _cache.Add(tempDataPoint);

				return true;
			} else {
				FinishMonitoring();
				return false;
			}
        }

        //Adds a file to the unlogged files list
        public void AddUnloggedCacheFile(string fileName)
        {
            _unloggedFiles.Add(fileName);
        }

        public void AddUnloggedCacheFiles(List<string> list)
        {
            foreach (string fileName in list)
            {
                AddUnloggedCacheFile(fileName);
            }
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_cpuPc != null)
                    { _cpuPc.Dispose(); }
                    if (_privBytesPc != null)
                    { _privBytesPc.Dispose(); }
                    if (_workingSetPc != null)
                    { _workingSetPc.Dispose(); }                    
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
