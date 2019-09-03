using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace PruneLibrary
{
	//A static class used for general Prune static methods
	public static class Prune {

		//ETW
		private static TraceEventSession _traceSession;
		private static readonly Dictionary<int, Counters> EtwCounters = new Dictionary<int, Counters>();
		private static int _etwUsers;

		//Hardware information
		//public static string Proc1Description { get; private set; }
		//public static string Proc1Name { get; private set; }
		//public static string Proc1PhysCore { get; private set; }
		//public static string Proc1LogiCore { get; private set; }
		//public static string Proc1CoreSpeed { get; private set; }
		//public static string Proc2Description { get; private set; }
		//public static string Proc2Name { get; private set; }
		//public static string Proc2PhysCore { get; private set; }
		//public static string Proc2LogiCore { get; private set; }
		//public static string Proc2CoreSpeed { get; private set; }
		//public static string Proc3Description { get; private set; }
		//public static string Proc3Name { get; private set; }
		//public static string Proc3PhysCore { get; private set; }
		//public static string Proc3LogiCore { get; private set; }
		//public static string Proc3CoreSpeed { get; private set; }
		//public static string Proc4Description { get; private set; }
		//public static string Proc4Name { get; private set; }
		//public static string Proc4PhysCore { get; private set; }
		//public static string Proc4LogiCore { get; private set; }
		//public static string Proc4CoreSpeed { get; private set; }
		//public static string[] ProcessorDescriptions { get; private set; }
		//public static string[] ProcessorNames { get; private set; }
		//public static string[] ProcessorPhysicalCores { get; private set; }
		//public static string[] ProcessorLogicalCores { get; private set; }
		//public static string[] ProcessorCoreSpeeds { get; private set; }
		public static string Processors { get; private set; }
		public static string ComputerManufacturer { get; private set; }
		public static string ComputerModel { get; private set; }
		public static string ComputerProcessorNum { get; private set; }
		public static string Disks { get; private set; }
		//public static string[] DiskManufaturers { get; private set; }
		//public static string[] DiskModels { get; private set; }
		//public static string Disk1Manufacturer { get; private set; }
		//public static string Disk1Model { get; private set; }
		public static string RamSize { get; private set; }

		public static void HandleError(bool isService, int source, string message)
        {
            if (isService)
            {
				switch (source) {
					case 0:
						//Source of error is the library code
						PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteLIBRARY_ERROR_EVENT(message);
						break;
					case 1:
						//Souce of error is the service code
						PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteSERVICE_ERROR_EVENT(message);
						break;
				}
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        //Given a process id, get the specific instance name it is ties to
        public static string GetInstanceNameForProcessId(int processId)
        {
			string[] instances = null;

			var process = Process.GetProcessById(processId);
			string processName = Path.GetFileNameWithoutExtension(process.ProcessName);

			PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");
			instances = cat.GetInstanceNames().Where(inst => inst.StartsWith(processName)).ToArray();


            foreach (string instance in instances)
            {
                using (PerformanceCounter cnt = new PerformanceCounter("Process", "ID Process", instance, true))
                {
					int val = (int)cnt.RawValue;
					if (val == processId)
					{
						return instance;
					}
                }
            }

            return null;
        }

        //Given a process Id, get the process name
        public static string GetProcessNameFromProcessId(int procId)
        {
            try
            {
                return Process.GetProcessById(procId).ProcessName;
            }
            catch
            {
                return null;
            }

        }

        //Given the process name, get an array of process objects tied to processes with that name
        public static Process[] GetProcessesFromProcessName(string procName)
        {
            return Process.GetProcessesByName(procName);
        }

        //Given a start time and an interval length, calculate the end time of the interval
        //  The interval is auto-aligned to a "boundary"
        //  If the interval length is a day, then the end time will be calculated as midnight of the next day
        public static DateTime CalculateIntervalFinishTime(DateTime startTime, uint interval)
        {
            DateTime finishTime = startTime;

            uint days = interval / 86400;
            uint hours = (interval - (86400 * days)) / 3600;
            uint minutes = (interval - (86400 * days) - (3600 * hours)) / 60;

            //the number of periods that need to pass the current system time
            long periods;

            //calculate the first interval end after the current system time
            //Case is based on the largest time division of the log interval
            if (days > 0)
            {
                //Get the day, hour, minute, and second components of the log start time, convert to seconds, and add together, divide by log interval to know # of intervals that have passed
                // Add 1 to be the first interval after current system time
                periods = ((startTime.Day * 86400 + startTime.Hour * 3600 + startTime.Minute * 60 + startTime.Second) / interval) + 1;

                //zero out all of the components that we used
                TimeSpan subtractSpan = new TimeSpan(startTime.Day, startTime.Hour, startTime.Minute, startTime.Second);
                finishTime = finishTime.Subtract(subtractSpan);

            }
            else if (hours > 0)
            {
                periods = ((startTime.Hour * 3600 + startTime.Minute * 60 + startTime.Second) / interval) + 1;
                TimeSpan subtractSpan = new TimeSpan(0, startTime.Hour, startTime.Minute, startTime.Second);
                finishTime = finishTime.Subtract(subtractSpan);
            }
            else if (minutes > 0)
            {
                periods = ((startTime.Minute * 60 + startTime.Second) / interval) + 1;
                TimeSpan subtractSpan = new TimeSpan(0, 0, startTime.Minute, startTime.Second);
                finishTime = finishTime.Subtract(subtractSpan);
            }
            else
            {
                periods = (startTime.Second / interval) + 1;
                TimeSpan subtractSpan = new TimeSpan(0, 0, 0, startTime.Second);
                finishTime = finishTime.Subtract(subtractSpan);
            }

            //add back the interval time * the number of periods needed
            finishTime = finishTime.AddSeconds(interval * periods);

            return finishTime;
        }

        public static void StartEtwSession(bool isService)
        {
            //Increment the counter that tracks the number of processes using the ETW Session
            _etwUsers++;

            if (_traceSession == null)
            {
                lock (EtwCounters)
                {
                    foreach (Counters counter in EtwCounters.Values)
                    {
                        counter.ResetCounters();
                    }
                }

                string etwSessionName = "Prune ETW Session";

                try
                {
                    _traceSession = new TraceEventSession(etwSessionName);

                    _traceSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP |
                                                      KernelTraceEventParser.Keywords.DiskIO | 
													  KernelTraceEventParser.Keywords.Memory);

                }
                catch (Exception e)
                {
                    HandleError(isService, 0, "Error initializing ETW Session" + Environment.NewLine + e.Message);
                    return;
                }

                if (TraceEventSession.IsElevated() != true)
                {
                    HandleError(isService, 0,
                        "The service must have elevated privilages to utilize ETW. Ensure the service is running as Local System and restart the service");
                    return;
                }

				try 
				{
					using (ManagementObjectSearcher processor = new ManagementObjectSearcher("select * from Win32_Processor"),
						computerSystem = new ManagementObjectSearcher("select * from Win32_ComputerSystem"),
						disks = new ManagementObjectSearcher("select * from Win32_DiskDrive")) 
					{

						string procString = "No Processors";
						int processorCount = 1;
						foreach (ManagementObject obj in processor.Get()) 
						{
							string desc = obj["Description"].ToString().Trim();
							string name = obj["Name"].ToString().Trim();
							string physical = obj["NumberOfCores"].ToString().Trim();
							string logical = obj["NumberOfLogicalProcessors"].ToString().Trim();

							//Processor speed in GHz
							string speed = (Convert.ToInt32(obj["MaxClockSpeed"].ToString().Trim())/1000.0).ToString() + " GHz";

							string temp = "Processor " + processorCount + ": " + desc + ", " + name + ", " + "Physical cores " + physical + ", Logical cores " + logical + ", Speed " + speed + Environment.NewLine;
							
							if(processorCount == 1) {
								procString = temp;
							} else {
								procString += temp;
							}

							processorCount++;
							
						}

						Processors = procString;

						foreach (ManagementObject obj in computerSystem.Get()) 
						{
							ComputerManufacturer = obj["Manufacturer"].ToString().Trim();
							ComputerModel = obj["Model"].ToString().Trim();
							ComputerProcessorNum = obj["NumberOfProcessors"].ToString().Trim();

							//Size of Ram in GB
							RamSize = (Convert.ToUInt64(obj["TotalPhysicalMemory"].ToString().Trim())/Convert.ToUInt64(Math.Pow(1024d,3d))).ToString() + " GB";
						}

						int diskCount = 1;
						string diskString = "No Disks";
						foreach (ManagementObject obj in disks.Get()) 
						{
							string man = obj["Manufacturer"].ToString().Trim();
							string model = obj["Model"].ToString().Trim();

							string temp = "Disk " + diskCount + ": " + man + ", " + model + Environment.NewLine;

							if (diskCount == 1) {
								diskString = temp;
							} else {
								diskString += temp;
							}

							diskCount++;
						}

						Disks = diskString;
					}
				} catch(Exception e) 
				{
					HandleError(isService, 0, "Error while retrieving Hardware information: " + e.Message + Environment.NewLine + e.StackTrace);
				}

                try
                {

					//Handle memory Info
					_traceSession.Source.Kernel.MemoryProcessMemInfo += data => 
					{
						lock (EtwCounters) 
						{
							foreach (KeyValuePair<int, Counters> entry in EtwCounters) 
							{
								if (data.ProcessID == entry.Key) 
								{
									Microsoft.Diagnostics.Tracing.Parsers.Kernel.MemoryProcessMemInfoValues values = data.Values(0);

									entry.Value.WorkingBytes = values.WorkingSetPageCount * Environment.SystemPageSize;
									entry.Value.PrivateBytes = values.PrivateWorkingSetPageCount * Environment.SystemPageSize;

								}
							}
						}
					};

                    //Handle and Disk I/O read operation
                    _traceSession.Source.Kernel.DiskIORead += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.DiskReadBytes += data.TransferSize;
                                    entry.Value.DiskReadOperations++;
                                }
                            }
                        }
                    };

                    //handle any Disk I/O write operation
                    _traceSession.Source.Kernel.DiskIOWrite += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.DiskWriteBytes += data.TransferSize;
                                    entry.Value.DiskWriteOperations++;
                                }
                            }
                        }
                    };

                    //Event handler for receiving IPv4 UDP datagrams
                    _traceSession.Source.Kernel.UdpIpRecv += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.UdpReceived += data.size;
                                }
                            }
                        }
                    };

                    //event handler for receiving IPv6 UDP datagrams
                    _traceSession.Source.Kernel.UdpIpRecvIPV6 += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.UdpReceived += data.size;
                                }
                            }
                        }
                    };

                    //event handler for sending IPv4 UDP Datagrams
                    _traceSession.Source.Kernel.UdpIpSend += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.UdpSent += data.size;
                                }
                            }
                        }
                    };

                    //event handler for sending IPv6 UDP Datagrams
                    _traceSession.Source.Kernel.UdpIpSendIPV6 += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {

                                    entry.Value.UdpSent += data.size;
                                }
                            }
                        }
                    };

                    //Handle any TCP IPv4 data send event
                    _traceSession.Source.Kernel.TcpIpSend += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {
                                    entry.Value.TcpSent += data.size;
                                    string addr = data.daddr.ToString() + ";" + data.dport.ToString();

                                    if (entry.Value.ConnectionsSent.ContainsKey(addr))
                                    {
                                        entry.Value.ConnectionsSent[addr] += data.size;
                                    }
                                    else
                                    {
                                        entry.Value.ConnectionsSent.Add(addr, data.size);
                                    }
                                }
                            }
                        }
                    };

                    //handle any TCP IPv6 data send event
                    _traceSession.Source.Kernel.TcpIpSendIPV6 += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {
                                    entry.Value.TcpSent += data.size;
                                    string addr = data.daddr.ToString() + ";" + data.dport.ToString();

                                    if (entry.Value.ConnectionsSent.ContainsKey(addr))
                                    {
                                        entry.Value.ConnectionsSent[addr] += data.size;
                                    }
                                    else
                                    {
                                        entry.Value.ConnectionsSent.Add(addr, data.size);
                                    }
                                }
                            }
                        }
                    };

                    //Handle TCP IPv4 Receive event
                    _traceSession.Source.Kernel.TcpIpRecv += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {
                                    entry.Value.TcpReceived += data.size;
                                    string addr = data.daddr.ToString() + ";" + data.dport.ToString();

                                    if (entry.Value.ConnectionsReceived.ContainsKey(addr))
                                    {
                                        entry.Value.ConnectionsReceived[addr] += data.size;
                                    }
                                    else
                                    {
                                        entry.Value.ConnectionsReceived.Add(addr, data.size);
                                    }
                                }
                            }
                        }
                    };

                    //handle TCP IPv6 receive event
                    _traceSession.Source.Kernel.TcpIpRecvIPV6 += data =>
                    {
                        lock (EtwCounters)
                        {
                            foreach (KeyValuePair<int, Counters> entry in EtwCounters)
                            {
                                if (data.ProcessID == entry.Key)
                                {
                                    entry.Value.TcpReceived += data.size;
                                    string addr = data.daddr.ToString() + ";" + data.dport.ToString();

                                    if (entry.Value.ConnectionsReceived.ContainsKey(addr))
                                    {
                                        entry.Value.ConnectionsReceived[addr] += data.size;
                                    }
                                    else
                                    {
                                        entry.Value.ConnectionsReceived.Add(addr, data.size);
                                    }
                                }
                            }
                        }
                    };
                }
                catch (Exception e)
                {
                    HandleError(isService, 0, "Error creating event handlers" + Environment.NewLine + e.Message);
                }

                //Start the session in a thread
                try
                {
                    Task.Run(() => _traceSession.Source.Process());
                }
                catch (Exception e)
                {
                    HandleError(isService, 0, "Error starting ETW Thread\n" + Environment.NewLine + e.Message + "\n" + Environment.NewLine + e.StackTrace);
                }
            }
        }

        //Returns the data for the specified process
        public static Counters GetEtwDataForProcess(int procId)
        {
            Counters counterCopy;

            lock (EtwCounters)
            {
                try
                {
                    counterCopy = new Counters(EtwCounters[procId]);
                    EtwCounters[procId].ResetCounters();
                }
                catch (KeyNotFoundException)
                {
                    counterCopy = null;
                }
            }

            return counterCopy;
        }

        //Called when a process no longer needs the ETW session. 
        //If this is the only process using the session (_etwUsers == 1 when this is called)
        //  Then the session will be closed. Otherwise, the session will persist
        public static void DisposeTraceSession()
        {
            //Decrement the counter that tracks the number of processes using the ETW session
            _etwUsers--;

            //Only destroy the ETW session if no process needs it anymore
            if (_etwUsers == 0)
            {
                _traceSession.Dispose();
                _traceSession = null;
            }
        }

        //Create a new counter for a new process
        public static void AddEtwCounter(int procId)
        {
            lock (EtwCounters)
            {
                EtwCounters.Add(procId, new Counters());
                EtwCounters[procId].ResetCounters();
            }
        }

        //Remove a process from the list of processes to monitor with ETW
        public static void RemoveEtwCounter(int procId)
        {
            lock (EtwCounters)
            {
                EtwCounters.Remove(procId);
            }
        }

        public class Counters
        {
            public long UdpReceived;
            public long UdpSent;
            public long DiskReadBytes;
            public long DiskWriteBytes;
            public long DiskReadOperations;
            public long DiskWriteOperations;
            public long TcpReceived;
            public long TcpSent;
			public long WorkingBytes;
			public long PrivateBytes;

            public Dictionary<string, long> ConnectionsSent = new Dictionary<string, long>();
            public Dictionary<string, long> ConnectionsReceived = new Dictionary<string, long>();

            //Default constructor initializes all values as 0
            public Counters()
            {
                UdpReceived = 0;
                UdpSent = 0;
                DiskReadBytes = 0;
                DiskWriteBytes = 0;
                DiskReadOperations = 0;
                DiskWriteOperations = 0;
                TcpReceived = 0;
                TcpSent = 0;
				WorkingBytes = 0;
				PrivateBytes = 0;
            }

            //Copy constructor
            public Counters(Counters copyCounters)
            {
                UdpReceived = copyCounters.UdpReceived;
                UdpSent = copyCounters.UdpSent;
                DiskReadBytes = copyCounters.DiskReadBytes;
                DiskReadOperations = copyCounters.DiskReadOperations;
                DiskWriteBytes = copyCounters.DiskWriteBytes;
                DiskWriteOperations = copyCounters.DiskWriteOperations;
                TcpReceived = copyCounters.TcpReceived;
                TcpSent = copyCounters.TcpSent;
				WorkingBytes = copyCounters.WorkingBytes;
				PrivateBytes = copyCounters.PrivateBytes;

                ConnectionsSent = new Dictionary<string, long>(copyCounters.ConnectionsSent);
                ConnectionsReceived = new Dictionary<string, long>(copyCounters.ConnectionsReceived);
            }

            //Reset all values to 0 and clear out the connection information
            public void ResetCounters()
            {
                UdpReceived = 0;
                UdpSent = 0;
                TcpReceived = 0;
                TcpSent = 0;
                DiskReadOperations = 0;
                DiskReadBytes = 0;
                DiskWriteBytes = 0;
                DiskWriteOperations = 0;
				WorkingBytes = 0;
				PrivateBytes = 0;

                ConnectionsSent.Clear();
                ConnectionsReceived.Clear();
            }
        }

        public static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct ModuleInformation
            {
                public IntPtr lpBaseOfDll;
                public uint SizeOfImage;
                private readonly IntPtr EntryPoint;
            }

            internal enum ModuleFilter
            {
                ListModulesDefault = 0x0,
                ListModules32Bit = 0x01,
                ListModules64Bit = 0x02,
                ListModulesAll = 0x03
            }

            [DllImport("psapi.dll")]
            private static extern bool EnumProcessModulesEx(IntPtr hProcess,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In] [Out]
                IntPtr[] lphModule, int cb, [MarshalAs(UnmanagedType.U4)] out int lpcbNeeded, uint dwFilterFlag);

            [DllImport("psapi.dll", ThrowOnUnmappableChar = true, CharSet = CharSet.Unicode)]
            private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule,
                [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] uint nSize);

            [DllImport("psapi.dll", SetLastError = true)]
            private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule,
                out ModuleInformation lpmodinfo, uint cb);

            [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

            [DllImport("Kernel32")]
            public static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

            public delegate bool EventHandler(CtrlType sig);

            public enum CtrlType
            {
                CTRL_C_EVENT = 0,
                CTRL_BREAK_EVENT = 1,
                CTRL_CLOSE_EVENT = 2,
                CTRL_LOGOFF_EVENT = 5,
                CTRL_SHUTDOWN_EVENT = 6
            }

            //Return true if the process argument is 64 bit or false if it is 32 bit
            public static bool Is64Bit(Process process)
            {
                if (!Environment.Is64BitOperatingSystem)
                {
                    return false;
                }

                if (!IsWow64Process(process.Handle, out bool isWow64))
                    throw new System.ComponentModel.Win32Exception();
                return !isWow64;
            }

            //Collects all modules for the process argument and returns them in a list
            public static List<Module> CollectModules(Process process)
            {
                ModuleFilter filter = (Is64Bit(process) && Environment.Is64BitProcess)
                    ? ModuleFilter.ListModulesAll
                    : ModuleFilter.ListModules32Bit;

                List<Module> collectedModules = new List<Module>();

                IntPtr[] modulePointers = new IntPtr[0];

                if (!EnumProcessModulesEx(process.Handle, modulePointers, 0, out int bytesNeeded,
                    (uint)filter))
                {
                    return collectedModules;
                }

                int totalNumberOfModules = bytesNeeded / IntPtr.Size;
                modulePointers = new IntPtr[totalNumberOfModules];

                if (EnumProcessModulesEx(process.Handle, modulePointers, bytesNeeded, out bytesNeeded,
                    (uint)filter))
                {
                    for (int index = 0; index < totalNumberOfModules; index++)
                    {
                        StringBuilder moduleFilePath = new StringBuilder(1024);
                        GetModuleFileNameEx(process.Handle, modulePointers[index], moduleFilePath,
                            (uint)(moduleFilePath.Capacity));

                        string moduleName = Path.GetFileName(moduleFilePath.ToString());
                        GetModuleInformation(process.Handle, modulePointers[index], out ModuleInformation moduleInformation,
                            (uint)(IntPtr.Size * (modulePointers.Length)));

                        Module module = new Module(moduleName, moduleInformation.lpBaseOfDll, moduleInformation.SizeOfImage);
                        collectedModules.Add(module);
                    }
                }

                return collectedModules;
            }
        }

        public class Module
        {
            public Module(string moduleName, IntPtr baseAddress, uint size)
            {
                ModuleName = moduleName;
                BaseAddress = baseAddress;
                Size = size;
            }

            public string ModuleName { get; set; }
            public IntPtr BaseAddress { get; set; }
            public uint Size { get; set; }
        }
    }
}
