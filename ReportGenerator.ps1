<#
.SYNOPSIS
    Shows stuff
.DESCRIPTION
    Description is here
.PARAMETER processName
    Name of the process on which to report
.PARAMETER pid
    The PID of the specific process on which to report, allow reports to be generated for a single instance of a process
.PARAMETER startTime
    The starting time of the range of data points on which to report. If this parameter is missing, it will default to 24 hours before the current time or before the specified finish time. The format for this parameter is yyyyMMdd_HHmmss
.PARAMETER finishTime
    The finish time of the range of data points on which to report. If this parameter is missing, it will default to the current system time. The format for this parameter is yyyyMMdd_HHmmss
.EXAMPLE
    example code
#>
param(
    [Parameter(Mandatory=$true)][string[]]$processNames,
    [Parameter(Mandatory=$false)][string]$processPid = "",
    [Parameter(Mandatory=$false)][string]$startTime = "",
    [Parameter(Mandatory=$false)][string]$finishTime = ""
    )

#lists of values retrieved from files generated with Resource Reporter
$cpuValues = New-Object System.Collections.Hashtable
$cpuValProcesses = New-Object System.Collections.Hashtable
$workingSetValues = New-Object System.Collections.Hashtable
$workingSetValuesProcesses = New-Object System.Collections.Hashtable
$privateSetValues = New-Object System.Collections.Hashtable
$privateSetValuesProcesses = New-Object System.Collections.Hashtable
$diskReadBytesValues = New-Object System.Collections.Hashtable
$diskWriteBytesValues = New-Object System.Collections.Hashtable
$diskReadOpsValues = New-Object System.Collections.Hashtable
$diskWriteOpsValues = New-Object System.Collections.Hashtable
$tcpInValues = New-Object System.Collections.Hashtable
$tcpOutValues = New-Object System.Collections.Hashtable
$udpInValues = New-Object System.Collections.Hashtable
$udpOutValues = New-Object System.Collections.Hashtable
$tcpConnectionsIn = New-Object System.Collections.Hashtable
$tcpConnectionsOut = New-Object System.Collections.Hashtable

#Function converts contents of file to a json object and puts all values in the correct lists for all
#   values that fall within the time frame
Function ProcessFile() {
    Param([string]$filePath)

    $filePathParts = $filePath.Split('/')
	$fileNameParts = $filePathParts[$filePathParts.Count-1].split('-')
	$procName = $fileNameParts[0]

    #Convert the file contents to JSON, resulting in an array of objects
    $fileContents = Get-Content -Path $filePath | ConvertFrom-Json

    #Loop through each object in the list
    ForEach ($f in $fileContents) {
        #Get the time this object was logged
        $logTimeParts = $f."logTime".split('T')
        $logTimeTimeParts = $logTimeParts[1].split('-')
        $newTimeString = "$($logTimeParts[0]) $($logTimeTimeParts[0])"
        $logTime = Get-Date -Date $newTimeString

        #Get the TCP connection maps
        $sentConns = $f."connectionsSent"
        $recvConns = $f."connectionsReceived"

        #If the log time of this object is within the range
        if($logTime -ge $startTimeObject -and $logTime -le $finishTimeObject) {

			$milli = $logTime.Millisecond * -1
			$logTime = $logTime.AddMilliseconds($milli)
			
            #Add values to the list
			if(-Not $cpuValProcesses.ContainsKey($logTime)) {
                $newList = New-Object System.Collections.Generic.List[string]
                $newList.Add($procName)
				$cpuValProcesses.Add($logTime, $newList)

				if($cpuValues.ContainsKey($logTime)) {
					$cpuValues[$logTime] += $f."CpuVal"
				} else {
					$cpuValues.Add($logTime, $f."CpuVal")
				}
			} else {
				if(-Not $cpuValProcess[$logTime].Contains($procName)) {
					if($cpuValues.ContainsKey($logTime)) {
						$cpuValues[$logTime] += $f."CpuVal"
					} else {
						$cpuValues.Add($logTime, $f."CpuVal")
					}
				} 
			}


			if(-Not $workingSetValuesProcesses.ContainsKey($logTime)) {
                $newList = New-Object System.Collections.Generic.List[string]
                $newList.Add($procName)
				$workingSetValuesProcesses.Add($logTime, $newList)
				$workingSetValuesProcesses[$logTime].Add($procName)

				if($workingSetValues.ContainsKey($logTime)) {
				$workingSetValues[$logTime] += $f."WorkingBytesVal"
				} else {
					$workingSetValues.Add($logTime, $f."WorkingBytesVal")
				}
			} else {
				if(-Not $workingSetValuesProcesses[$logTime].Contains($procName)) {
					if($workingSetValues.ContainsKey($logTime)) {
						$workingSetValues[$logTime] += $f."WorkingBytesVal"
					} else {
						$workingSetValues.Add($logTime, $f."WorkingBytesVal")
					}
				} 
			}
			
			if(-Not $privateSetValuesProcesses.ContainsKey($logTime)) {
                $newList = New-Object System.Collections.Generic.List[string]
                $newList.Add($procName)
				$privateSetValuesProcesses.Add($logTime, $newList)
				$cpuValProcesses[$logTime].Add($procName)

				if($privateSetValues.ContainsKey($logTime)) {
					$privateSetValues[$logTime] += $f."PrivBytesVal"
				} else {
					$privateSetValues.Add($logTime, $f."PrivBytesVal")
				}
			} else {
				if(-Not $privateSetValuesProcesses[$logTime].Contains($procName)) {
					if($privateSetValues.ContainsKey($logTime)) {
						$privateSetValues[$logTime] += $f."PrivBytesVal"
					} else {
						$privateSetValues.Add($logTime, $f."PrivBytesVal")
					}
				} 
			}
			

			if($diskReadBytesValues.ContainsKey($logTime)) {
				$diskReadBytesValues[$logTime] += $f."DiskBytesReadVal"
			} else {
				$diskReadBytesValues.Add($logTime, $f."DiskBytesReadVal")
			}

			if($diskWriteBytesValues.ContainsKey($logTime)) {
				$diskWriteBytesValues[$logTime] += $f."DiskBytesWriteVal"
			} else {
				$diskWriteBytesValues.Add($logTime, $f."DiskBytesWriteVal")
			}
			
			if($diskReadOpsValues.ContainsKey($logTime)) {
				$diskReadOpsValues[$logTime] += $f."DiskOpsReadVal"
			} else {
				$diskReadOpsValues.Add($logTime, $f."DiskOpsReadVal")
			}

			if($diskWriteOpsValues.ContainsKey($logTime)) {
				$diskWriteOpsValues[$logTime] += $f."DiskOpsWriteVal"
			} else {
				$diskWriteOpsValues.Add($logTime, $f."DiskOpsWriteVal")
			}

			if($tcpInValues.ContainsKey($logTime)) {
				$tcpInValues[$logTime] += $f."TcpRecv"
			} else {
				$tcpInValues.Add($logTime, $f."TcpRecv")
			}

			if($tcpOutValues.ContainsKey($logTime)) {
				$tcpOutValues[$logTime] += $f."TcpSent"
			} else {
				$tcpOutValues.Add($logTime, $f."TcpSent")
			}

            if($udpInValues.ContainsKey($logTime)) {
				$udpInValues[$logTime] += $f."UdpRecv"
			} else {
				$udpInValues.Add($logTime, $f."UdpRecv")
			}
            
			if($udpOutValues.ContainsKey($logTime)) {
				$udpOutValues[$logTime] += $f."UdpSent"
			} else {
				$udpOutValues.Add($logTime, $f."UdpSent")
			}


            #For each entry in the map, either create a new list for it or add the value
            #    to an existing list if it exists
            $sentConns | Get-Member -MemberType NoteProperty | foreach-object { 
                $name=$_.Name
                $value=$sentConns."$($_.Name)"
            
                if($tcpConnectionsOut.ContainsKey($name)) {
					if($tcpConnectionsOut[$name].ContainsKey($logTime)) {
						$tcpConnectionsOut[$name][$logTime] += $value
					} else {
						$tcpConnectionsOut[$name].Add($logTime, $value)
					}
                } else {
                    $table = New-Object System.Collections.Hashtable
                    $table.Add($logTime, $value)
                    $tcpConnectionsOut.Add($name, $table)
                }
            }

            #Repeat for inbound connections
            $recvConns | Get-Member -MemberType NoteProperty | ForEach-Object {
                $name =$_.Name
                $value=$recvConns."$($_.Name)"

                if($tcpConnectionsIn.ContainsKey($name)) {
					if($tcpConnectionsIn[$name].ContainsKey($logTime)) {
						$tcpConnectionsIn[$name][$logTime] += $value
					} else {
						$tcpConnectionsIn[$name].Add($logTime, $value)
					}
                } else {
                    $list = New-Object System.Collections.Hashtable
                    $list.Add($logTime, $value)
                    $tcpConnectionsIn.Add($name, $list)
                }
            }
        }
    }
}

$providedStartTime = [bool]($MyInvocation.BoundParameters.Keys -match 'starttime')
$providedFinishTime = [bool]($MyInvocation.BoundParameters.Keys -match 'finishtime')
$providedPID = [bool]($MyInvocation.BoundParameters.Keys -match 'processPid')

if($providedPID -and (processNames.Count -gt 1)) {
	Write-Output "The provided PID will not be used since a list of processes was provided"
}

try 
{
    if($providedStartTime -and $providedFinishTime) {
        $startTimeObject = [DateTime]::ParseExact($startTime, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
        $finishTimeObject = [DateTime]::ParseExact($finishTime, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
    } elseif($providedStartTime) {
        $startTimeObject = [DateTime]::ParseExact($startTime, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)

        $span = New-TimeSpan -Hours 24
        $finishTimeObject = $startTimeObject.Add($span)
    } elseif($providedFinishTime) {
        $finishTimeObject = [DateTime]::ParseExact($finishTime, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)

        $span = New-TimeSpan -Hours 24
        $startTimeObject = $finishTimeObject.Subtract($span)
    } else {
        $span = New-TimeSpan -Hours 24
        $finishTimeObject = [DateTime]::Now
        $startTimeObject = [DateTime]::Now.Subtract($span)
    }
} 
catch [System.FormatException] {
    Write-Output "Start and finish time arguments must be in 'yyyyMMdd_HHmmss' format"
    return
}

if($startTimeObject -ge $finishTimeObject) {
    Write-Output "Start time must be earlier than the finish time. If a finish time is specified, a start time mu"
    return;
}

foreach ($procName in $processNames) {
	#The two folders that may contain json result files
	$processFolderPath = "C:\ProgramData\ResourceReporter\" + $procName
	$processLoggedFolderPath = $processFolderPath + "\logged"

	if(Test-Path $processFolderPath) {
		#loop through all 
		Get-ChildItem -Path $processFolderPath -Name -Include *.json | ForEach-Object -Process {
			$fileName = $_;

			$stringArray = $filename -split "-"

			if($providedPID) {
				$nameArray = $stringArray[0] -split "_"
				if($processPid -ne [int]$nameArray[1]) {
					return
				}
			}

			$fileStartString = $stringArray[1]
			$fileEndString = $stringArray[2].Substring(0, $stringArray[2].Length-5)

			$fileStart = [DateTime]::ParseExact($fileStartString, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
			$fileEnd = [DateTime]::ParseExact($fileEndString, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)

			if($fileStart -ge $startTimeObject -and $fileEnd -le $finishTimeObject) {
				ProcessFile($processFolderPath + "\" +  $fileName)
			} elseif($fileStart -le $startTimeObject -and $fileEnd -ge $finishTimeObject) {
				ProcessFile($processFolderPath + "\" +  $fileName)
			} elseif($fileStart -lt $startTimeObject -and $fileEnd -gt $startTimeObject) {
				ProcessFile($processFolderPath + "\" +  $fileName)
			} elseif($fileEnd -gt $finishTimeOBject -and $fileStart -lt $finishTimeObject) {
				ProcessFile($processFolderPath + "\" + $fileName)
			}
		}
	}

	if(Test-Path $processLoggedFolderPath) {
		Get-ChildItem -Path $processLoggedFolderPath -Name -Include *.json | ForEach-Object -Process {
			$fileName = $_;

			$stringArray = $filename -split "-"

			if($providedPID) {
				$nameArray = $stringArray[0] -split "_"
				if($processPid -ne $nameArray[1]) {
					return
				}
			}

			$fileStartString = $stringArray[1]
			$fileEndString = $stringArray[2].Substring(0, $stringArray[2].Length-5)

			$fileStart = [DateTime]::ParseExact($fileStartString, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
			$fileEnd = [DateTime]::ParseExact($fileEndString, 'yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)

			if($fileStart -ge $startTimeObject -and $fileEnd -le $finishTimeObject) {
				ProcessFile($processLoggedFolderPath + "\" +  $fileName)
			} elseif($fileStart -le $startTimeObject -and $fileEnd -ge $finishTimeObject) {
				ProcessFile($processLoggedFolderPath + "\" +  $fileName)
			} elseif($fileStart -lt $startTimeObject -and $fileEnd -gt $startTimeObject) {
				ProcessFile($processLoggedFolderPath + "\" +  $fileName)
			} elseif($fileEnd -gt $finishTimeOBject -and $fileStart -lt $finishTimeObject) {
				ProcessFile($processLoggedFolderPath + "\" + $fileName)
			}
		}
	}
}

if($cpuValues.Count -eq 0) {
    Write-Output "No data could be found for the specified process(es)"
    return
}

$cpuStats = $cpuValues.Values | Measure-Object -Average -Maximum -Minimum
$cpuFreq = $cpuValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$workStats = $workingSetValues.Values | Measure-Object -Average -Minimum -Maximum
$workFreq = $workingSetValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$privStats = $privateSetValues.Values | Measure-Object -Average -Minimum -Maximum
$privFreq = $privateSetValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$diskReadBytesStats = $diskReadBytesValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$diskReadBytesFreq = $diskReadBytesValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$diskWriteBytesStats = $diskWriteBytesValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$diskWriteBytesFreq = $diskWriteBytesValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$diskReadOpsStats = $diskReadOpsValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$diskReadOpsFreq = $diskReadOpsValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$diskWriteOpsStats = $diskWriteOpsValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$diskWriteOpsFreq = $diskWriteOpsValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$tcpInStats = $tcpInValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$tcpInFreq = $tcpInValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$tcpOutStats = $tcpOutValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$tcpOutFreq = $tcpOutValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$udpInStats = $udpInValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$udpInFreq = $udpInValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$udpOutStats = $udpOutValues.Values | Measure-Object -Average -Minimum -Maximum -Sum
$udpOutFreq = $udpOutValues.Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

$connectionsIn = New-Object System.Collections.Generic.List[pscustomobject]
$connectionsOut = New-Object System.Collections.Generic.List[pscustomobject]

ForEach($key in $tcpConnectionsIn.Keys) {
    $connInStats = $tcpConnectionsIn[$key].Values | Measure-Object -Minimum -Maximum -Average -Sum
    $connInFreq = $tcpConnectionsIn[$key].Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

    $connObj = [pscustomobject]@{
        Endpoint = $key
		'TCP Bytes Received Total' = $connInStats.Sum;
        'TCP Bytes Received Minimum' = $connInStats.Minimum;
        'TCP Bytes Received Maximum' = $connInStats.Maximum;
        'TCP Bytes Received Average' = $connInStats.Average;
        'TCP Bytes Received Frequency' = $connInFreq;
    }

    $connectionsIn.Add($connObj)
}

ForEach($key in $tcpConnectionsOut.Keys) {
    $connOutStats = $tcpConnectionsOut[$key].Values | Measure-Object -Minimum -Maximum -Average -Sum
    $connOutFreq = $tcpConnectionsOut[$key].Values | Group-Object {[Math]::Round($_,2)} | Select-Object -Property Count,Name,@{Name='Value'; Expression={$_.Name}} -Exclude Name | Sort-Object -Property Count -Desc

    $connObj = [pscustomobject]@{
        Endpoint = $key
		'TCP Bytes Sent Total' = $connOutStats.Sum
        'TCP Bytes Sent Minimum' = $connOutStats.Minimum
        'TCP Bytes Sent Maximum' = $connOutStats.Maximum
        'TCP Bytes Sent Average' = $connOutStats.Average
        'TCP Bytes Sent Frequency' = $connOutFreq
    }

    $connectionsOut.Add($connObj)
}

if($processNames.Count -gt 1) {
	$processName = "multiprocess"
} else {
    $processName = $processNames[0]
}

$outputProcessName = if ($providedPID -and $processNames.Count -eq 1) { '{0}_{1}' -f $processName, $processPid } else {$processName}

$resultsObject = [pscustomobject]@{
    'File Name' = 'Resource usage statistics for {0}' -f $outputProcessName;
    'Process Name' = $outputProcessName
    'Start Time' = '{0:yyyyMMdd_HHmmss}' -f $startTimeObject
    'Finish Time' = '{0:yyyyMMdd_HHmmss}' -f $finishTimeObject
    'Samples' = $cpuValues.Count;
    'Processor Time % Minimum' = [Math]::Round($cpuStats.Minimum,2);
    'Processor Time % Maximum' = [Math]::Round($cpuStats.Maximum,2);
    'Processor Time % Average' = [Math]::Round($cpuStats.Average,2);
    'Processor Time % Frequency' = $cpuFreq;
    'Working Set Minimum' = $workStats.Minimum;
    'Working Set Maximum' = $workStats.Maximum;
    'Working Set Average' = [Math]::Round($workStats.Average,2);
    'Working Set Frequency' = $workFreq;
    'Private Bytes Minimum' = $privStats.Minimum;
    'Private Bytes Maximum' = $privStats.Maximum;
    'Private Bytes Average' = [Math]::Round($privStats.Average,2);
    'Private Bytes Frequency' = $privFreq;
    'Disk Bytes Read Total' = $diskReadBytesStats.Sum;
	'Disk Bytes Read Minimum' = $diskReadBytesStats.Minimum;
	'Disk Bytes Read Maximum' = $diskReadBytesStats.Maximum;
	'Disk Bytes Read Average' = [Math]::Round($diskReadBytesStats.Average,2);
	'Disk Bytes Read Frequency' = $diskReadBytesFreq;
    'Disk Bytes Written Total' = $diskWriteBytesStats.Sum;
	'Disk Bytes Written Minimum' = $diskWriteBytesStats.Minimum;
	'Disk Bytes Written Maximum' = $diskWriteBytesStats.Maximum;
	'Disk Bytes Written Average' = [Math]::Round($diskWriteBytesStats.Average,2);
	'Disk Bytes Written Frequency' = $diskWriteBytesFreq;
    'Disk Read Operation Total' = $diskReadOpsStats.Sum;
    'Disk Read Operations Minimum' = $diskReadOpsStats.Minimum;
	'Disk Read Operations Maximum' = $diskReadOpsStats.Maximum;
	'Disk Read Operations Average' = [Math]::Round($diskReadOpsStats.Average,2);
	'Disk Read Operations Frequency' = $diskReadOpsFreq;
    'Disk Write Operations Total' = $diskWriteOpsStats.Sum;
	'Disk Write Operations Minimum' = $diskWriteOpsStats.Minimum;
	'Disk Write Operations Maximum' = $diskWriteOpsStats.Maximum;
	'Disk Write Operations Average' = [Math]::Round($diskWriteOpsStats.Average,2);
	'Disk Write Operations Frequency' = $diskWriteOpsFreq;
    'TCP Bytes Received Total' = $tcpInStats.Sum;
    'TCP Bytes Received Minimum' = $tcpInStats.Minimum;
    'TCP Bytes Received Maximum' = $tcpInStats.Maximum;
    'TCP Bytes Received Average' = [Math]::Round($tcpInStats.Average,2);
    'TCP Bytes Received Frequency' = $tcpInFreq;
    'TCP Bytes Sent Total' = $tcpOutStats.Sum;
    'TCP Bytes Sent Minimum' = $tcpOutStats.Minimum;
    'TCP Bytes Sent Maximum' = $tcpOutStats.Maximum;
    'TCP Bytes Sent Average' = [Math]::Round($tcpOutStats.Average,2);
    'TCP Bytes Sent Frequency' = $tcpOutFreq;
    'UDP Bytes Received Total' = $udpInStats.Sum;
    'UDP Bytes Received Minimum' = $udpInStats.Minimum;
    'UDP Bytes Received Maximum' = $udpInStats.Maximum;
    'UDP Bytes Received Average' = [Math]::Round($udpInStats.Average,2);
    'UDP Bytes Received Frequency' = $udpInFreq;
    'UDP Bytes Sent Total' = $udpOutStats.Sum;
    'UDP Bytes Sent Minimum' = $udpOutStats.Minimum;
    'UDP Bytes Sent Maximum' = $udpOutStats.Maximum;
    'UDP Bytes Sent Average' = [Math]::Round($udpOutStats.Average,2);
    'UDP Bytes Sent Frequency' = $udpOutFreq;
    'TCP Bytes Received By Connection' = $connectionsIn
    'TCP Bytes Sent By Connection' = $connectionsOut        
}
 

$outputFileName = '.\Results-{0}-{1:yyyyMMdd_HHmmss}-{2:yyyyMMdd_HHmmss}.json' -f $outputProcessName,$startTimeObject,$finishTimeObject

$outputFilePath = [System.IO.Path]::GetFullPath($outputFileName)

$resultsObject | ConvertTo-Json -Depth 4 | Out-File -FilePath $outputFileName

Write-Output "Usage File $($outputFileName) Generated"