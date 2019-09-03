using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary {
	[StructLayout(LayoutKind.Sequential)]
	public struct ConnectionDataStruct {
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
		public string ipAddress;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)]
		public string domainName;

		public Int64 bytesSentTotal;
		public Int64 bytesSentMin;
		public Int64 bytesSentMax;
		public Int64 bytesSentAvg;
		public Int64 bytesRcvTotal;
		public Int64 bytesRcvMin;
		public Int64 bytesRcvMax;
		public Int64 bytesRcvAvg;

		public ConnectionDataStruct(TcpConnectionData data) {
			this.bytesSentTotal = data.TotalOut;
			this.bytesSentMin = data.MinOut;
			this.bytesSentMax = data.MaxOut;
			this.bytesSentAvg = data.AverageOut;
			this.bytesRcvTotal = data.TotalIn;
			this.bytesRcvMin = data.MinIn;
			this.bytesRcvMax = data.MaxIn;
			this.bytesRcvAvg = data.AverageIn;

			this.ipAddress = data.Address;
			this.domainName = data.HostName;
		}

		public ConnectionDataStruct(int x) {
			this.ipAddress = "";
			this.domainName = "";
			this.bytesSentTotal = 0;
			this.bytesSentMin = 0;
			this.bytesSentMax = 0;
			this.bytesSentAvg = 0;
			this.bytesRcvTotal = 0;
			this.bytesRcvMin = 0;
			this.bytesRcvMax = 0;
			this.bytesRcvAvg = 0;
		}
	}
}
