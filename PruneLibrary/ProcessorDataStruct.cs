using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary {
	public unsafe struct ProcessorDataStruct {

		public fixed char description[41];
		public fixed char name[41];
		public fixed char physCore[4];
		public fixed char logiCore[4];
		public fixed char coreSpeed[10];

		public ProcessorDataStruct(string desc, string nam, string phys, string log, string speed) {
			fixed(char* descArr = description, nameArr = name, physArr = physCore, logArr = logiCore, speedArr = coreSpeed) {
				
				//zero out arrays
				for (int i=0;i<41;i++) {
					descArr[i] = (char)0;
					nameArr[i] = (char)0;

					if(i<10) {
						speedArr[i] = (char)0;
					}

					if(i<4) {
						physArr[i] = (char)0;
						logArr[i] = (char)0;
					}
				}

				//Create copy size variables
				int descSize = desc.Length >= 40 ? 40 : desc.Length;
				int nameSize = nam.Length >= 40 ? 40 : nam.Length;
				int physSize = phys.Length >= 3 ? 3 : phys.Length;
				int logSize = log.Length >= 3 ? 3 : log.Length;
				int speedSize = speed.Length >= 9 ? 9 : speed.Length;

				//Copy description
				for(int i=0; i<descSize; i++) {
					descArr[i] = desc[i];
				}

				for(int i=0; i<nameSize; i++) {
					nameArr[i] = nam[i];
				}

				for(int i=0; i<physSize; i++) {
					physArr[i] = phys[i];
				}

				for(int i=0; i<logSize; i++) {
					logArr[i] = log[i];
				}

				for(int i=0; i<speedSize; i++) {
					speedArr[i] = speed[i];
				}
			}
		}

		public ProcessorDataStruct(int x) {
			fixed (char* descArr = description, nameArr = name, physArr = physCore, logArr = logiCore, speedArr = coreSpeed) {

				//zero out arrays
				for (int i = 0; i < 41; i++) {
					descArr[i] = (char)0;
					nameArr[i] = (char)0;

					if (i < 10) {
						speedArr[i] = (char)0;
					}

					if (i < 4) {
						physArr[i] = (char)0;
						logArr[i] = (char)0;
					}
				}
			}
		}
	}
}
