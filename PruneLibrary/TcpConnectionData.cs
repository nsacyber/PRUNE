using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary
{
    //Data structure used to hold information about TCP connections
    public class TcpConnectionData
    {
        public string Address { get; set; }
        public string HostName { get; set; }
        public long AverageIn { get; set; }
        public long MaxIn { get; set; }
        public long MinIn { get; set; }
        public long AverageOut { get; set; }
        public long MaxOut { get; set; }
        public long MinOut { get; set; }
        public int OutCount { get; set; }
        public int InCount { get; set; }
        public long TotalIn { get; set; }
        public long TotalOut { get; set; }

        public TcpConnectionData(string name, bool isService, string procName)
        {
            Address = name;

            IPHostEntry ipEntry = null;

            try
            {
                ipEntry = Dns.GetHostEntry(Address.Split(';')[0]);
                HostName = ipEntry.HostName;
                if (isService)
                {
                    Prune.EventLog.WriteEntry("Error resolving " + name + " to host name for process " + procName, EventLogEntryType.Information, 100);
                }
            }
            catch (Exception)
            {
                HostName = "Unknown";
            }

            AverageIn = 0;
            AverageOut = 0;
            MaxIn = long.MinValue;
            MaxOut = long.MinValue;
            MinIn = long.MaxValue;
            MinOut = long.MaxValue;
            InCount = 0;
            OutCount = 0;
        }

        //Add a data value (# of bytes) that was sent
        public void AddOutData(long data)
        {
            OutCount++;
            AverageOut += data;

            if (data > MaxOut)
                MaxOut = data;
            if (data < MinOut)
                MinOut = data;
        }

        //add a data value (# of bytes) that was received
        public void AddInData(long data)
        {
            InCount++;
            AverageIn += data;

            if (data > MaxIn)
                MaxIn = data;
            if (data < MinIn)
                MinIn = data;
        }

        //divide the averages by the count and zero out any unedited fields
        public void CalculateStats()
        {
            TotalIn = AverageIn;
            TotalOut = AverageOut;

            AverageIn /= InCount;
            AverageOut /= OutCount;

            if (MaxIn == long.MinValue)
                MaxIn = 0;
            if (MinIn == long.MaxValue)
                MinIn = 0;
            if (MaxOut == long.MinValue)
                MaxOut = 0;
            if (MinOut == long.MaxValue)
                MinOut = 0;
        }
    }
}
