using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary {
	struct ConnectionDataStruct {
		string ipAddress;
		string domainName;
		long bytesSentTotal;
		long bytesSentMin;
		long bytesSentMax;
		long bytesSentAvg;
		long bytesRcvTotal;
		long bytesRcvMin;
		long bytesRcvMax;
		long bytesRcvAvg;

		public ConnectionDataStruct(TcpConnectionData data) {
			this.ipAddress = data.Address;
			this.domainName = data.HostName;
			this.bytesSentTotal = data.TotalOut;
			this.bytesSentMin = data.MinOut;
			this.bytesSentMax = data.MaxOut;
			this.bytesSentAvg = data.AverageOut;
			this.bytesRcvTotal = data.TotalIn;
			this.bytesRcvMin = data.MinIn;
			this.bytesRcvMax = data.MaxIn;
			this.bytesRcvAvg = data.AverageIn;
		}
	}
}
