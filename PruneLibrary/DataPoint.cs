using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary
{
    //interal class used to store the data gathered by performance counters.
    // This structure is written in json to the cache files
    public class DataPoint
    {
        public DateTime LogTime { get; set; }          //The time the data was gathered
        public double CpuVal { get; set; }             //The value from the % cpu usage counter
        public long PrivBytesVal { get; set; }       //the value from the private bytes counter
        public long WorkingBytesVal { get; set; }    //the value from the working bytes counter
        public long DiskBytesReadVal { get; set; }   //The number of bytes read from disk per second
        public long DiskBytesWriteVal { get; set; }  //The number of bytes written to disk per second
        public long DiskOpsReadVal { get; set; }     //The number of disk read operations per second
        public long DiskOpsWriteVal { get; set; }    //the number of disk write operations per second
        public long UdpSent { get; set; }              //Number of bytes sent over UDP
        public long UdpRecv { get; set; }              //Number of bytes received over UDP


        public Dictionary<string, long> ConnectionsSent;
        public Dictionary<string, long> ConnectionsReceived;

        public long TcpSent { get; set; }
        public long TcpRecv { get; set; }

        //constructor that assigns values
        public DataPoint(double cpu, long priv, long working, long readBytes, long writeBytes, long readOps, long writeOps,
            long udpS, long udpR, long tcpS, long tcpR, Dictionary<string, long> connsSent, Dictionary<string, long> connsRecv, DateTime time)
        {
            CpuVal = cpu;
            PrivBytesVal = priv;
            WorkingBytesVal = working;
            DiskBytesReadVal = readBytes;
            DiskBytesWriteVal = writeBytes;
            DiskOpsReadVal = readOps;
            DiskOpsWriteVal = writeOps;
            UdpSent = udpS;
            UdpRecv = udpR;
            TcpSent = tcpS;
            TcpRecv = tcpR;

            ConnectionsSent = new Dictionary<string, long>(connsSent);
            ConnectionsReceived = new Dictionary<string, long>(connsRecv);

            LogTime = time;
        }
    }
}
