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
        public long DataOutCount { get; set; }
        public long DataInCount { get; set; }
        public long TotalIn { get; set; }
        public long TotalOut { get; set; }
		public long ConnsCountIn { get; set; }
		public long ConnsCountOut { get; set; }

        public TcpConnectionData(string name)
        {
            Address = name;

            IPHostEntry ipEntry = null;

			try {
				ipEntry = Dns.GetHostEntry(Address.Split(';')[0]);
				HostName = ipEntry.HostName;
			} catch (Exception) {
				HostName = "Unknown";
				PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteHOST_NAME_ERROR_EVENT(name);
			}

			HostName = "Unknown";

            AverageIn = 0;
            AverageOut = 0;
            MaxIn = long.MinValue;
            MaxOut = long.MinValue;
            MinIn = long.MaxValue;
            MinOut = long.MaxValue;
            DataInCount = 0;
            DataOutCount = 0;
			ConnsCountIn = 0;
			ConnsCountOut = 0;
        }

		//Add the number of out-bound bytes for this address
		public void AddOutData(long data) {
			DataOutCount++;
			AverageOut += data;

			if (data > MaxOut)
				MaxOut = data;
			if (data < MinOut)
				MinOut = data;
		}

		//Add the number of out-bound connections we had for this address
		public void AddOutCount(long count) {
			ConnsCountOut += count;
		}

		//Add the number of in-bound bytes from this address
		public void AddInData(long data) {
			DataInCount++;
			AverageIn += data;

			if (data > MaxIn)
				MaxIn = data;
			if (data < MinIn)
				MinIn = data;
		}

		//Add the number of in-bound connections we had for this address
		public void AddInCount(long count) {
			ConnsCountIn += count;
		}

		//divide the averages by the count and zero out any unedited fields
		public void CalculateStats()
        {
            TotalIn = AverageIn;
            TotalOut = AverageOut;

			if (DataInCount != 0)
				AverageIn /= DataInCount;
			if (DataOutCount != 0)
				AverageOut /= DataOutCount;

            if (MaxIn == long.MinValue)
                MaxIn = 0;
            if (MinIn == long.MaxValue)
                MinIn = 0;
            if (MaxOut == long.MinValue)
                MaxOut = 0;
            if (MinOut == long.MaxValue)
                MinOut = 0;
        }

		public override string ToString() {
			return HostName + " -- " + Address + " ::  TotalBytesIn: " + TotalIn + ", MaxBytesIn: " + 
				MaxIn + ", MinBytesIn: " + MinIn + ", AvgBytesIn: " + AverageIn + ", ConnectionsIn: " + ConnsCountIn + "," + Environment.NewLine + "TotalBytesOut: " + 
				TotalOut + ", MaxBytesOut: " + MaxOut + ", MinBytesOut: " + MinOut + ", AvgBytesOut: " + 
				AverageOut + ", ConnectionsOut: " + ConnsCountOut + Environment.NewLine + Environment.NewLine;
		}
    }
}
