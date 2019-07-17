using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary {
	public unsafe struct ConnectionDataStruct {
		public fixed char ipAddress[40];
		public fixed char domainName[51];
		public long bytesSentTotal;
		public long bytesSentMin;
		public long bytesSentMax;
		public long bytesSentAvg;
		public long bytesRcvTotal;
		public long bytesRcvMin;
		public long bytesRcvMax;
		public long bytesRcvAvg;

		public ConnectionDataStruct(TcpConnectionData data) {
			this.bytesSentTotal = data.TotalOut;
			this.bytesSentMin = data.MinOut;
			this.bytesSentMax = data.MaxOut;
			this.bytesSentAvg = data.AverageOut;
			this.bytesRcvTotal = data.TotalIn;
			this.bytesRcvMin = data.MinIn;
			this.bytesRcvMax = data.MaxIn;
			this.bytesRcvAvg = data.AverageIn;

			fixed(char* ipArr = ipAddress, domainArr = domainName) {
				for (int i = 0; i < 51; i++) {
					domainArr[i] = (char)0;

					if (i < 40) {
						ipArr[i] = (char)0;
					}
				}

				//Create copy size variables
				int ipSize = data.Address.Length >= 39 ? 39 : data.Address.Length;
				int nameSize = data.HostName.Length >= 50 ? 50 : data.HostName.Length;

				//Copy description
				for (int i = 0; i < nameSize; i++) {
					domainArr[i] = data.HostName[i];
				}

				for (int i = 0; i < ipSize; i++) {
					ipArr[i] = data.Address[i];
				}
			}
		}

		public ConnectionDataStruct(int x) {
			fixed (char* ipArr = ipAddress, domainArr = domainName) {
				for (int i = 0; i < 51; i++) {
					domainArr[i] = (char)0;

					if (i < 40) {
						ipArr[i] = (char)0;
					}
				}
			}

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
