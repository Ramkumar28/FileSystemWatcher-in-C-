// <copyright file="FileSystemWatcherHelper.cs" company="Trane">Copyright © Trane 2013</copyright>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using TOPBase.TLogMgr;

namespace SP.Utilities
{
    /// <summary>
    /// Helper class to manage file change events.
    /// </summary>
    public class FileSystemWatcherHelper
    {
        private const string COMPONENT_NAME = "FileSystemWatcherHelper";
        private Dictionary<string, DateTime> _lastFileEvent;
        private TimeSpan _recentTimeSpan;
        private int _interval;
        private ILogger _Logger;
        private FileChangedCallback _FileChangedCallbackMethod;
        private string _FullPath = string.Empty;
        private string _Filter = string.Empty;
        private string _FileDirectory = string.Empty;

        /// <summary>
        /// The constructor for the watcher
        /// </summary>
        /// <param name="a_logger">
        /// instance of logger
        /// </param>
        /// <param name="a_CallbackMethod">
        /// call back method
        /// </param>
        public FileSystemWatcherHelper(ILogger a_logger, FileChangedCallback a_CallbackMethod)
        {
            if (a_logger != null)
            {
                _Logger = a_logger;
            }

            if (a_CallbackMethod != null)
            {
                _FileChangedCallbackMethod = a_CallbackMethod;
            }

            _lastFileEvent = new Dictionary<string, DateTime>();
            Interval = 100;
            FilterRecentEvents = true;
            Intialize();
        }

        /// <summary>
        /// Call back of the delegate to the listner
        /// </summary>
        /// <param name="sender">
        /// this class
        /// </param>
        /// <param name="args">
        /// args of the type FileSystemEvent Args
        /// </param>
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "to be supressed")]
        [SuppressMessage("Microsoft.Design", "CA1003:UseGenericEventHandlerInstances", Justification = "to be supressed")]
        public delegate void FileChangedCallback(object sender, FileSystemWatcherChangedArgs args);

        /// <summary>
        /// Property for filter recent events
        /// </summary>
        public bool FilterRecentEvents { get; set; }

        /// <summary>
        /// Interval as given
        /// </summary>
        public int Interval
        {
            get
            {
                return _interval;
            }

            set
            {
                _interval = value;
                //// Set timespan based on the value passed
                _recentTimeSpan = new TimeSpan(0, 0, 0, 0, value);
            }
        }

        /// <summary>
        /// Triggers the event when there is a change in the file
        /// </summary>
        /// <param name="sender">
        /// this class
        /// </param>
        /// <param name="e">
        /// event args of type FileSystemEventArgs
        /// </param>
        private void _FileChanged_Event(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.FullPath))
            {
                _CreateChangedArgs(this, e);
            }
        }

        /// <summary>
        /// Triggers when a file is added/created/deleted in the corresponding folder.
        /// </summary>
        /// <param name="sender">
        /// This class.
        /// </param>
        /// <param name="e">
        /// event args of type FileSystemEventArgs
        /// </param>
        private void _DataCollectionCofigurationFolder_Changed(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.FullPath))
            {
                _CreateChangedArgs(this, e);
            }
        }

        /// <summary>
        /// This method searches the dictionary to find out when the last event occured 
        /// for a particular file. If that event occured within the specified timespan
        /// it returns true, else false
        /// </summary>
        /// <param name="a_FileName">The filename to be checked</param>
        /// <returns>True if an event has occured within the specified interval, False otherwise</returns>
        private bool HasAnotherFileEventOccuredRecently(string a_FileName)
        {
            bool retVal = false;
            try
            {
                // Check dictionary only if user wants to filter recent events otherwise return Value stays False
                if (FilterRecentEvents)
                {
                    if (_lastFileEvent != null)
                    {
                        if (_lastFileEvent.ContainsKey(a_FileName))
                        {
                            DateTime lastEventTime = _lastFileEvent[a_FileName];
                            DateTime currentTime = DateTime.Now;
                            TimeSpan timeSinceLastEvent = currentTime - lastEventTime;
                            retVal = timeSinceLastEvent < _recentTimeSpan;
                            _lastFileEvent[a_FileName] = currentTime;
                        }
                        else
                        {
                            ////This happens for first time and returns false.
                            _lastFileEvent.Add(a_FileName, DateTime.Now);
                            retVal = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.LogIf(COMPONENT_NAME, TraceLevel.Error, ex);
            }

            return retVal;
        }

        /// <summary>
        /// Created a instance of changes arguments.
        /// </summary>
        /// <param name="sender">
        /// this class.
        /// </param>
        /// <param name="e">
        /// event args of type FileSystemEventArgs
        /// </param>
        private void _CreateChangedArgs(object sender, FileSystemEventArgs e)
        {
            FileSystemWatcherChangedArgs _changedArgs = new FileSystemWatcherChangedArgs(e);
            if (_changedArgs.SettingType != SettingsType.UnknownFile)
            {
                _Logger.LogIf(COMPONENT_NAME, TraceLevel.Info, string.Format("Settings Type - {0} {1}", _changedArgs.SettingType.ToString(), _changedArgs.Name));

                if (_FileChangedCallbackMethod != null)
                {
                    this._FileChangedCallbackMethod(sender, _changedArgs);
                }
            }
        }

        /// <summary>
        /// Intialize the corresponding file system watcher.
        /// </summary>
        private void Intialize()
        {
            foreach (SettingsType a_SettingType in Enum.GetValues(typeof(SettingsType)))
            {
                try
                {
                    _FullPath = EnumeratorParser.GetStringValue(a_SettingType);
                    _FileDirectory = Path.GetDirectoryName(_FullPath);
                    _Filter = Path.GetFileName(_FullPath);

                    switch (a_SettingType)
                    {
                        case SettingsType.GatewayLogSettings:
                        case SettingsType.MessageRouterSettings:
                        case SettingsType.SiteManagerSettings:
                        case SettingsType.TunnelManagerSettings:
                        case SettingsType.DataOffloaderServiceEndpoints:
                        case SettingsType.TraneObjectMaps:
                            CreateFileWatcher(a_SettingType);
                            break;
                        case SettingsType.DataCollectionConfiguration:
                            CreateDirectoryWatcher();
                            break;
                        default:
                            ////Do nothing here,jus handled for default, for future case.
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (_Logger != null)
                    {
                        _Logger.LogIf(COMPONENT_NAME, TraceLevel.Error, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the FileSystemWatcher for the for the corresponding file of the given settings type.
        /// </summary>
        /// <param name="a_SettingsType">
        /// given with settings type.
        /// </param>
        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "to be supressed.")]
        private void CreateFileWatcher(SettingsType a_SettingsType)
        {
            try
            {
                string _Path = EnumeratorParser.GetStringValue(a_SettingsType);
                if (a_SettingsType.Equals(SettingsType.TraneObjectMaps))
                {
                    _FullPath = string.IsNullOrEmpty(ConfigurationManager.AppSettings["TraneObjectMapFolder"]) ? Path.Combine(@"C:\SPGateway\Metadata\", _Path) : ConfigurationManager.AppSettings["TraneObjectMapFolder"] + _Path;
                }
                else
                {
                    _FullPath = string.IsNullOrEmpty(ConfigurationManager.AppSettings["PersistenceDirectory"]) ? Path.Combine(@"C:\SPGateway\Settings\", _Path) : ConfigurationManager.AppSettings["PersistenceDirectory"] + _Path;
                }

                _FileDirectory = Path.GetDirectoryName(_FullPath);
                _Filter = Path.GetFileName(_FullPath);
                FileSystemWatcher a_FileWatcher = new FileSystemWatcher(_FileDirectory, _Filter);
                a_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                a_FileWatcher.Changed += _FileChanged_Event;
                a_FileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                if (_Logger != null)
                {
                    _Logger.LogIf(COMPONENT_NAME, TraceLevel.Error, ex);
                }
            }
        }

        /// <summary>
        /// Creates the watcher for the directory
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "to be supressed.")]
        private void CreateDirectoryWatcher()
        {
            try
            {
                _FileDirectory = string.IsNullOrEmpty(ConfigurationManager.AppSettings["DataCollectionConfigurationDirectory"]) ? @"C:\SPGateway\Objects\DataCollectionConfiguration\" : ConfigurationManager.AppSettings["DataCollectionConfigurationDirectory"];
                if (_FileDirectory != null)
                {
                    FileSystemWatcher a_FileWatcher = new FileSystemWatcher(_FileDirectory);
                    a_FileWatcher.Filter = "*.spo";
                    a_FileWatcher.Changed += _DataCollectionCofigurationFolder_Changed;
                    a_FileWatcher.Created += _DataCollectionCofigurationFolder_Changed;
                    a_FileWatcher.Deleted += _DataCollectionCofigurationFolder_Changed;
                    a_FileWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex)
            {
                if (_Logger != null)
                {
                    _Logger.LogIf(COMPONENT_NAME, TraceLevel.Error, ex);
                }
            }
        }
    }
}
