using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using PruneLibrary;
using Timer = System.Timers.Timer;

namespace Prune
{
    class Program
    {
        static void Main(string[] args)
        {
            ReporterClass reporter = new ReporterClass();
            reporter.Reporter(args);
        }
    }

    class ReporterClass : IDisposable
    {
        private readonly List<PruneProcessInstance> _instances = new List<PruneProcessInstance>();
        readonly Timer _timer = new Timer();
        private DateTime _timerStartTime;
        private string _processName;
        private long _lengthToMonitor;
        private bool _nameIsId;
        private readonly string programDataDirectory = @"C:\ProgramData\PRUNE";
        private bool _isStopping;
        private PruneLibrary.Prune.NativeMethods.EventHandler _handler;

        //Called when the program terminates during execution
        private static bool Handler(PruneLibrary.Prune.NativeMethods.CtrlType sig, Timer timer, out bool isStopping)
        {
            //Stop the timer
            timer.Stop();

            //Dispose of the ETW session
            PruneLibrary.Prune.DisposeTraceSession();

            //Signal the main thread that it should stop
            isStopping = true;

            return true;
        }

        public void Reporter(string[] args)
        {
            //Create and assign our exit/crash handler
            _handler += ((sig) => Handler(sig, _timer, out _isStopping));
            PruneLibrary.Prune.NativeMethods.SetConsoleCtrlHandler(_handler, true);

            //Check the arguments
            if (args.Length != 2)
            {
                Console.WriteLine("Incorrect usage. 2 arguments must be supplied; a Process name or PID and a length of time to monitor in seconds");
                return;
            }

            //parse the arguments
            _processName = args[0];
            try
            {
                _lengthToMonitor = long.Parse(args[1]);
            }
            catch
            {
                Console.WriteLine("Monitoring time length must be a valid integer greater than 0.");
                return;
            }

            //Verify the time to monitor is valid
            if (_lengthToMonitor < 1)
            {
                Console.WriteLine("Monitoring time length must be a valid integer greater than 0.");
                return;
            }

            //Verify the process isn't the system or idle process
            if (_processName.Equals("0") || _processName.Equals("4") ||
                _processName.Equals("idle", StringComparison.OrdinalIgnoreCase) ||
                _processName.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("The System (PID 0) and Idle (PID 4) processes cannot be monitored.");
                return;
            }

            Console.WriteLine("Initializing Prune");

            //Create the ProgramData Prune directory if it does not already exist (it should)
            Directory.CreateDirectory(programDataDirectory);

            if (_processName.Contains("module=") || _processName.Contains("Module="))
            {
                Process[] runningProcesses = Process.GetProcesses(".");

                string moduleName = _processName.Split('.')[0].Trim();
                string moduleFile = _processName.Split('=')[1].Trim();

                foreach (Process proc in runningProcesses)
                {
                    if (proc.Id != 4 && proc.Id != 0)
                    {
                        try
                        {
                            List<PruneLibrary.Prune.Module> moduleList =
                                PruneLibrary.Prune.NativeMethods.CollectModules(proc);

                            foreach (PruneLibrary.Prune.Module module in moduleList)
                            {
                                if (module.ModuleName.Equals(moduleFile, StringComparison.OrdinalIgnoreCase))
                                {
                                    _instances.Add(new PruneProcessInstance(false, proc.Id, moduleName,
                                        (uint)_lengthToMonitor, (uint)_lengthToMonitor + 10, programDataDirectory));
                                    PruneLibrary.Prune.AddEtwCounter(proc.Id);

                                    break;
                                }
                            }
                        }
                        catch
                        {
                            //IF we fail to get an error, we want to continue but we also do not want to report the error,
                            //  because the error is unimportant
                            continue;
                        }
                    }
                }
            }
            else
            {
                string tempProcName;
                //If the name is all digits, it is treated as an ID
                if (_processName.All(char.IsDigit))
                {
                    _nameIsId = true;

                    //Get the process name from the ID to ensure there is a process tied to this ID currently active
                    tempProcName =
                        PruneLibrary.Prune.GetProcessNameFromProcessId(int.Parse(_processName));

                    if (tempProcName == null)
                    {
                        //Could not find a name for the given process ID.
                        //  Assume the process is not active and skip it.
                        return;
                    }
                }
                else //Otherwise it is treated as a process name
                {
                    tempProcName = _processName;
                }

                //Get all system process objects that have the specified name
                Process[] processes =
                    PruneLibrary.Prune.GetProcessesFromProcessName(tempProcName);

                if (processes == null || processes.Length == 0)
                {
                    //Could not find any processes that have the provided name.
                    //  Assume the process is not started and skip it.
                    Console.WriteLine("Could not find process " + _processName + ".");
                    return;
                }

                //The name is all numbers, so we assume it is a process ID
                if (_nameIsId)
                {
                    //Get the name of the process from the ID
                    int procId = int.Parse(_processName);
                    _processName = PruneLibrary.Prune.GetProcessNameFromProcessId(procId);

                    //Create a new instance, add it to the list of processes, and add it to the list of processes for ETW to monitor
                    _instances.Add(new PruneProcessInstance(false, procId, _processName,
                        (uint)_lengthToMonitor, (uint)(_lengthToMonitor + 10), programDataDirectory));
                    PruneLibrary.Prune.AddEtwCounter(procId);
                }
                else
                {
                    //A process name was provided, so there may be multiple processes to monitor with the same name
                    foreach (Process proc in processes)
                    {
                        //get the ID from the process object
                        int procId = proc.Id;

                        //create a new instance, add it to the list, and add it to the ETW list
                        _instances.Add(new PruneProcessInstance(false, procId, _processName,
                            (uint)_lengthToMonitor, (uint)(_lengthToMonitor + 10), programDataDirectory));
                        PruneLibrary.Prune.AddEtwCounter(procId);
                    }
                }
            }

            //Initialize the instances
            try
            {
                foreach (PruneProcessInstance rr in _instances)
                {
                    rr.InitializeInstance();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error initializing the Prune Process instances:\n" + e.Message);
            }

            //Start eh ETW session that gathers TCP, UDP, and Disk I/O
            PruneLibrary.Prune.StartEtwSession(false);

            //create a timer that fires every second
            _timer.Interval = 1000;
            _timer.Elapsed += (sender, e) => { TimerFunction(); };
            _timerStartTime = DateTime.Now;

            Console.WriteLine("Starting monitoring");

            //start the timer
            _timer.Start();

            //Loop until execution concludes
            while (!_isStopping) { Thread.Sleep(100); }

            Console.WriteLine("Prune Finished");
        }


        //Get the data from our reporter object and store it in the data lists
        private void TimerFunction()
        {
            //get the data
            foreach (PruneProcessInstance rr in _instances)
            {
                rr.GetData();
            }

            //if the time is a multiple of 5 seconds, print a dot to show the tool is still running
            if (DateTime.Now.Subtract(_timerStartTime).Seconds % 5 == 0)
            {
                Console.Write(".");
            }

            //If it has been lengthToMonitor seconds, stop the timer
            if (DateTime.Compare(_timerStartTime.AddSeconds(_lengthToMonitor), DateTime.Now) <= 0)
            {
                _timer.Stop();
                foreach (PruneProcessInstance rr in _instances)
                {
                    rr.WriteCacheToFile();
                }
                PruneLibrary.Prune.DisposeTraceSession();
                _isStopping = true;
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
