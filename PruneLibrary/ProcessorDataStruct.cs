using System;

namespace PruneLibrary 
{
	public struct ProcessorDataStruct {

		public int processorCount;
		public string description;
		public string name;
		public string physCore;
		public string logiCore;
		public string coreSpeed;

		public ProcessorDataStruct(int processorCount, string desc, string nam, string phys, string log, string speed) 
		{
			this.processorCount = processorCount;
			this.description = desc;
			this.name = nam;
			this.physCore = phys;
			this.logiCore = log;
			this.coreSpeed = speed;
		}

		public ProcessorDataStruct(int x)
		{
			this.processorCount = 0;
			this.description = "";
			this.name = "";
			this.physCore = "";
			this.logiCore = "";
			this.coreSpeed = "";
		}

		public override string ToString()
		{
			return "Processor " + processorCount + ": " + description + ", " + name + ", " + "Physical cores " + physCore + ", Logical cores " + logiCore + ", Speed " + coreSpeed + Environment.NewLine;
		}
	}
}
