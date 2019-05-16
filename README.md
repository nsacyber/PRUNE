# PRUNE - Process Resource Usage Notification Engine

PRUNE monitors processes and records their resource usage, including % processor time,
private working set and private bytes, disk I/O bytes written and read, disk I/O read and write 
operation numbers, process-wide tcp bytes sent and received, process-wide udp bytes sent and received, 
and connection-specific TCP bytes sent and received. This data is stored in JSON files. The included 
powershell script, `ReportGenerator.ps1`, can be run to generate a JSON usage report of a process or 
a series of processes for a defined period of time.

The tool is divided into three parts. The library contains PRUNE's core functionality 
and is required for both the service and the command-line tool. The service is designed for long-term 
monitoring of processes and allows users to define a list of multiple processes to monitor. The 
command-line tool allows users to monitor a single process for a defined amount of time. 

## Dependencies and Requirements

PRUNE requires version 4.6 or higher of the .NET Framework. This should be installed 
before installing PRUNE.

PRUNE is compatable with Windows 7 SP1 and later and Server 2012 and later.

If building from source, Wix version 3 is required to build the installers. The Wix 
Visual Studio plugin is required to import the installer projects into Visual Studio. 

## Installation

To install the PRUNE suite, simply run the correct installer for your OS, either 
`PruneInstaller_x86.msi` for 32 bit OSes or `PruneInstaller_x64.msi` 
for 64 bit OSes. The libraries, service executable, command-line executable, and powershell 
script are placed in `C:\Program Files\Prune`. The Installer also creates 
`C:\ProgramData\Prune`, which is used to store the data files created by PRUNE 
and store the configuration files of the service.

## Prune Service

### Configuration

Both configuration files are found in `C:\ProgramData\Prune`.

`config.json` is the configuration file of the service. It contains the following configurable values:
* `calculateStatisticsInterval` - The amount of time, in seconds, before reading the data files and publishing a brief usage report to the Prune application log. The default is 86400, or 24 hours
* `writeCacheToFileInterval` - The amount of time, in seconds, between writing each data file. The default is 3600, or 1 hour
* `dataRecordingInterval` - The amount of time, in seconds, between gathering data and caching it. The default is 1. The program will fetch data every 1 second
* `whitelistCheckInterval` - The amount of time, in seconds, between readings of the whitelist. The default is 60.
* `configCheckInterval` - The amount of time, in seconds, between readings of the config file. The default is 60.

`whitelist.txt` acts as a process whitelist. Each entry should be placed on a new line. Start lines 
with `#` to create comments. This file will accept the process name (without a file extension) or 
the process ID. If a process name is provided and multiple processes of that name are running, they 
will all be monitored by PRUNE. To target a specific process in this case, provide the PID.

In addition, a module may be specified using `module=<module_name>`, where module name is the name of 
a dll or exe file, including the file extension. The service will search each currently running process 
and begin montoring any that has the specified module loaded. PRUNE will not be able to check
loaded modules of any protected process. These processes will be skipped when checking for the module.

### Usage

After installation, any needed configuration changes should be made as per the above section. 
Once configured, the process can be started. 

The PRUNE log, an application event log viewable from the Event Viewer, is used to log 
the service. The log will be created if this is the first execution of the service. 

The service will gather data every `dataRecordingInterval` seconds and store it in an internal cache. 
The cache will be written to a file every `writeCacheToFileInterval` seconds. This file has the name 
`<Process_Name>_<ProcessID>-<File_Start>-<File_End>.json` where `<Process_Name>` is the name of the 
process and <ProcessID> is the process's PID, `<Start_Time>` is the gathering time of the first data 
point in the file, and `<Finish_Time>` is the gathering time of the last data point in the file. Both 
times are in `yyyyMMdd_HHmmss` format. These files are placed in 
`C:\ProgramData\Prune\<Process_Name>\`.

Every `calculateStatisticsInterval` seconds, the service will generate a usage report for each process 
from the most recent data files and output them in the PRUNE log. These include the 
minimums, maximums, and averages for every data point that is gathered. In addition, all disk and 
network I/O have a total over the period of time. Once this report is generated and output, the data 
files will be moved to `C:\ProgramData\Prune\<Process_Name>\logged\`.

When the service shuts down, it dumps its cache to a data file. On start up, the service will look in 
the PRUNE root directory for any unlogged data files and either generate a report with that 
data immediately or record the file name for use during the next report generation.

## PRUNE Command-Line Tool

### Usage

The command-line tool has 2 required arguments:
* `<Process_Name>` - The name or ID of the process to monitor. If a name is provided and there are multiple processes with that name running, they will all be monitored. Use a process's PID to target a specific process.
* `<Monitoring_Length>` - The length of time, in seconds, to monitor the process.

The arguments should be supplied in the above order. Example: `Prune.exe <Process_Name> <Monitoring_Length>`.

The command-line tool will gather data on the specified process every second and stores it in a cache. After 
`<Monitoring_Length>` seconds have passed, the data is output in a JSON file to 
`C:\ProgramData\Prune\<Process_Name>\logged\`. Once the report is generated, the process will output that it 
has finished and exit. 

## Report Generator Powershell Script

The provided powershell script, `ReportGenerator.ps1`, can be used to generate a detailed usage report 
into a JSON file from the data files produced by the PRUNE service and command-line tool. 
This report conains the minimums, maximums, and averages for all of the gathered datapoints, the sum 
for all I/O data points, and the frequency of each data value per data point. This is output with other 
information such as the process name, report start time, report end time, and the total number of data 
points used to generate the report. 

The script has 4 arguments:
* `<processNames>` - Required - The names of the process for which to generate a report. This should always be the actual name of the process, which is the name used by the data folders. A list of process names can also be provided, where each name is comma seperated from the others.
* `<processPid>` - Optional - A specific PID to monitor. Use this if there are multiple processes with the same name and the report should only include one.
* `<startTime>` - Optional - The earliest data point to use in the report. If not provided, it defaults to 24 hours before the script's finish time. The time must be in format `yyyyMMdd_HHmmss`.
* `<finishTime>` - Optional - The latest data point to use in the report. If not provided, it defaults to the time of execution. The time must be in format `yyyyMMdd_HHmmss`.

If a list of processes is provided to `<processNames>`, then the data points will be grouped together 
by the log time across all of the processes. After the data is grouped, each data point will represent 
the respective resource's utilization at that moment in time by all of the listed processes. These sums 
will then be analyized for averages, minimums, maximums, and sums and then reported in the final report.

The file is named `Results-<processNames>_<processPid-<startTime>-<finishTime>.json` and output to 
the working directory of the script. If multiple names were provided, then `<processNames>` is 
replaces by `multiprocess`.

## License

See [LICENSE](./LICENSE.md).

## Contributing

See [CONTRIBUTING](./CONTRIBUTING.md).

## Disclaimer

See [DISCLAIMER](./DISCLAIMER.md).
