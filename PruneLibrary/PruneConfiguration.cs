using System;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace PruneLibrary
{
    //Data holding class used for configurating the service
    //It is serialized to create the config file and deserialized to read the config file
    //It is not used after initial setup
    public class ServiceConfiguration
    {

        public uint CalculateStatisticsInterval { get; set; }
        public uint WriteCacheToFileInterval { get; set; }
        public uint DataRecordingInterval { get; set; }
        public uint WhitelistCheckInterval { get; set; }
        public uint ConfigCheckInterval { get; set; }

        public ServiceConfiguration(uint logInt, uint cacheInt, uint dataInt, uint whitelistInt, uint configInt)
        {
            CalculateStatisticsInterval = logInt;
            WriteCacheToFileInterval = cacheInt;
            DataRecordingInterval = dataInt;
            WhitelistCheckInterval = whitelistInt;
            ConfigCheckInterval = configInt;
        }  
    }

    interface Configuration
    {
        ServiceConfiguration ReadConfiguration();
        string[] ReadWhitelist();
    }

    public class FileConfiguration: Configuration
    {
        //Configuration defaults, with times in seconds
        private const uint LogIntervalDefault = 86400;
        private const uint CacheIntervalDefault = 3600;
        private const uint MonitorIntervalDefault = 1;
        private const uint WhitelistIntervalDefault = 60;
        private const uint ConfigIntervalDefault = 60;

        private string _configPath;
        private string _whitelistPath;

        public FileConfiguration(string ConfigPath, string WhitelistPath)
        {
            this._configPath = ConfigPath;
            this._whitelistPath = WhitelistPath;
        }

        public ServiceConfiguration ReadConfiguration()
        {
            //Check if the config file exists
            if (!File.Exists(_configPath))
            {
                //The configuration file does not exist, so we need to create it with default values
                try
                {                    
                    //Create the configuration object
                    ServiceConfiguration config = new ServiceConfiguration(LogIntervalDefault, CacheIntervalDefault, MonitorIntervalDefault, WhitelistIntervalDefault, ConfigIntervalDefault);

                    //Create the json string for the configuration object
                    string configString = JsonConvert.SerializeObject(config, Formatting.Indented);

                    //Write the config json string to the config file
                    using (StreamWriter sw = new StreamWriter(_configPath, false))
                    {
                        sw.Write(configString);
                        sw.Flush();
                    }

                    return config;
                }
                catch (Exception e)
                {
                    Prune.HandleError(true, 1, "Error while creating config file and writing default setting\n" + e.Message);
                }
            }
            else
            {
                //The config file exists, so we need to read it
                try
                {             
                    //Get the text and parse the json into a ServiceConfiguration object
                    string configFileText = File.ReadAllText(_configPath);
                    ServiceConfiguration config = JsonConvert.DeserializeObject<ServiceConfiguration>(configFileText); 

                    return config;                   
                }
                catch (Exception e)
                {
                    Prune.HandleError(true, 1, "Error reading configuration file\n" + e.Message);
                }
            }

            return null;
        }

        public string[] ReadWhitelist()
        {
            if (File.Exists(_whitelistPath))
            {
                try
                {
                    //if it does, read in all of it's lines
                    string[] lines = File.ReadAllLines(_whitelistPath);

                    return lines;
                } 
                catch (Exception e)
                {
					Prune.HandleError(true, 1, "Error while reading the whitelist file\n" + e.Message);
                }
            } 
            else 
            {
                try
                {
                    //If the whitelist file does not exist, create a blank text file for future use
                    File.CreateText(_whitelistPath);
					PruneEvents.PRUNE_EVENT_PROVIDER.EventWriteNO_WHITELIST_EVENT();

                    return null;
                }
                catch (Exception e)
                {
					Prune.HandleError(true, 1, "Error creating whitelist file\n" + e.Message);
                }
            }

            return null;
        }
    }

    public class GpoConfiguration: Configuration
    {
        private int LogIntervalGPO = 0;
        private int CacheIntervalGPO = 0;
        private int MonitorIntervalGPO = 0;
        private int WhitelistIntervalGPO = 0;
        private int ConfigIntervalGPO = 0;

        public ServiceConfiguration ReadConfiguration()
        {
        
            //Check if the group policy is configured 
            //Opening the subkey
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Prune");
     
            //if it does exist, retrieve the stored values 
            if (key != null)
            {
                //GPO settings
                if (key.GetValue("calculateStatisticsInterval") != null)
                {
                    LogIntervalGPO = (int)key.GetValue("calculateStatisticsInterval");
                }

                //WriteCacheToFileInterval
                if (key.GetValue("writeCacheToFileInterval") != null)
                {
                    CacheIntervalGPO = (int)key.GetValue("writeCacheToFileInterval");
                }

                //DataRecordingInterval
                if (key.GetValue("dataRecordingInterval") != null)
                {
                    MonitorIntervalGPO = (int)key.GetValue("dataRecordingInterval");
                }

                //WhitelistCheckInterval
                if (key.GetValue("whitelistCheckInterval") != null)
                {
                    WhitelistIntervalGPO = (int)key.GetValue("whitelistCheckInterval");
                }

                //ConfigCheckInterval
                if (key.GetValue("configCheckInterval") != null)
                {
                    ConfigIntervalGPO = (int)key.GetValue("configCheckInterval");
                }

                //Close the subkey
                key.Close();

                //Create the configuration object
                ServiceConfiguration config = new ServiceConfiguration((uint)LogIntervalGPO, (uint)CacheIntervalGPO, (uint)MonitorIntervalGPO, (uint)WhitelistIntervalGPO, (uint)ConfigIntervalGPO);

                return config;
            }

            return null;
        }
        
        public int WhitelistSupportEnabled()
        {
            //Check if the group policy is configured 
            //Opening the subkey
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Prune");

             //if it does exist, retrieve the whitelist syntax value
            if (key != null)
            {
                int whitelistSyntax = (int)key.GetValue("whitelistSyntax");

                return whitelistSyntax;
            }

            return -1;
        }

        public string[] ReadWhitelist()
        {
            //Check if the whitelist is supported
            //Opening the subkey
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Prune\Whitelist");

            //if it does exist, retrieve the whitelist processes and/or modules
            if (key != null)
            {
                string[] lines = key.GetValueNames();

                return lines;
            }

            return null;
        }
    }
}
            