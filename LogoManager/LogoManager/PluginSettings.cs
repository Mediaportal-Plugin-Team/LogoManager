using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Xml.Serialization;
using MediaPortal.Configuration;  // path to thumbs, to plugin config
using MediaPortal.GUI.Library;    // Logging and skin properties
//using MediaPortal.Util;           // path to logo folders

namespace LogoManager
{
    public class PluginSettings
    {
        private static PluginSettings _instance;
        public PluginConfig XMLConfig = new PluginConfig();
        readonly XmlSerializer xs = new XmlSerializer(typeof(PluginConfig));
        readonly string region = System.Globalization.RegionInfo.CurrentRegion.EnglishName;
        const string PluginSubfolder = "LogoManager\\";
        private const string ConfigFileName= "LogoManager.config";
        private string configFileFullName;
        public bool ConfigRecreated;

        public void LoadSetting()
        {
            LogoManagerPlugin.TVSourcesPath = Config.GetSubFolder(Config.Dir.Thumbs, PluginSubfolder + @"sources\tv\");
            LogoManagerPlugin.RadioSourcesPath = Config.GetSubFolder(Config.Dir.Thumbs, PluginSubfolder + @"sources\radio\");
            LogoManagerPlugin.DesignsPath = Config.GetSubFolder(Config.Dir.Thumbs, PluginSubfolder + @"designs\");
            LogoManagerPlugin.TVLogosPath = Config.GetSubFolder(Config.Dir.Thumbs, PluginSubfolder + @"generated\TV\");
            LogoManagerPlugin.RadioLogosPath = Config.GetSubFolder(Config.Dir.Thumbs, PluginSubfolder + @"generated\Radio\");
            //LogoManagerPlugin.TVLogosBase      = Thumbs.TVChannel + "\\";//XMLConfig.GetSubFolder(XMLConfig.Dir.Thumbs, @"tv\logos\");
            //LogoManagerPlugin.RadioLogosBase   = Thumbs.Radio + "\\";//XMLConfig.GetSubFolder(XMLConfig.Dir.Thumbs, @"Radio\");

            //LogoManagerPlugin.TVSourcesPath    = Path.Combine(LogoManagerPlugin.TVService.GetPathToThumbs(), PluginSubfolder + @"sources\tv\");
            //LogoManagerPlugin.RadioSourcesPath = Path.Combine(LogoManagerPlugin.TVService.GetPathToThumbs(), PluginSubfolder + @"sources\radio\");
            //LogoManagerPlugin.DesignsPath      = Path.Combine(LogoManagerPlugin.TVService.GetPathToThumbs(), PluginSubfolder + @"designs\");
            //LogoManagerPlugin.TVLogosPath      = Path.Combine(LogoManagerPlugin.TVService.GetPathToThumbs(), PluginSubfolder + @"generated\TV\");
            //LogoManagerPlugin.RadioLogosPath   = Path.Combine(LogoManagerPlugin.TVService.GetPathToThumbs(), PluginSubfolder + @"generated\Radio\");
            LogoManagerPlugin.TVLogosBase = LogoManagerPlugin.TVService.GetPathToLogos(ChannelGroupType.TV);
            LogoManagerPlugin.RadioLogosBase   = LogoManagerPlugin.TVService.GetPathToLogos(ChannelGroupType.Radio);

            if (!Directory.Exists(LogoManagerPlugin.TVSourcesPath))
                Directory.CreateDirectory(LogoManagerPlugin.TVSourcesPath);
            if (!Directory.Exists(LogoManagerPlugin.RadioSourcesPath))
                Directory.CreateDirectory(LogoManagerPlugin.RadioSourcesPath);
            if (!Directory.Exists(LogoManagerPlugin.DesignsPath))
                Directory.CreateDirectory(LogoManagerPlugin.DesignsPath);
            if (!Directory.Exists(LogoManagerPlugin.TVLogosPath))
                Directory.CreateDirectory(LogoManagerPlugin.TVLogosPath);
            if (!Directory.Exists(LogoManagerPlugin.RadioLogosPath))
                Directory.CreateDirectory(LogoManagerPlugin.RadioLogosPath);
            if (!Directory.Exists(LogoManagerPlugin.TVLogosBase))
                Directory.CreateDirectory(LogoManagerPlugin.TVLogosBase);
            if (!Directory.Exists(LogoManagerPlugin.RadioLogosBase))
                Directory.CreateDirectory(LogoManagerPlugin.RadioLogosBase);
            configFileFullName = Config.GetFile(Config.Dir.Config, ConfigFileName);
            if (File.Exists(configFileFullName)) {
                FileStream fs = null;
                try {
                    fs = new FileStream(Config.GetFile(Config.Dir.Config, ConfigFileName), FileMode.Open);
                    XmlReader reader = XmlReader.Create(fs);
                    XMLConfig = xs.Deserialize(reader) as PluginConfig;
                    fs.Dispose();
                }
                catch {
                    Log.Error(LogoManagerPlugin.ThreadName + ": Error on parsing config file. LogoManager.config will be recreated with default settings.");
                }
                if (fs != null)
                    fs.Dispose();
            }
            if (XMLConfig == null)
                XMLConfig = new PluginConfig();
            if (XMLConfig.Mapping.Packages.Count == 0)
                ConfigRecreated = true;
            UpdatePackagesSettingsIfNecessary();

            LogoManagerPlugin.Map = ParseSettings();
            LogoManagerPlugin.LastSelectedPackage = XMLConfig.Settings.LastSelectedPackage;
            if (string.IsNullOrEmpty(XMLConfig.Settings.Package) && string.IsNullOrEmpty(LogoManagerPlugin.LastSelectedPackage)) {
                Package packForCountry = XMLConfig.Mapping.Packages.FirstOrDefault(e => String.Compare(e.CountryName_Eng, region, StringComparison.OrdinalIgnoreCase) == 0);
                if (packForCountry != null)
                    LogoManagerPlugin.LastSelectedPackage = packForCountry.Name;
            }
            LogoManagerPlugin.DesignType = XMLConfig.Settings.Design;
            LogoManagerPlugin.LastGrab = XMLConfig.Settings.LastGrab;
            LogoManagerPlugin.AutoUpdateInterval = (int) new TimeSpan(XMLConfig.Settings.AutoGrabIntervalInDays,0,0,0).TotalSeconds;
            //if (ConfigRecreated)
                SaveSettings();
        }

        public void InitProperties()
        {
            GUIPropertyManager.SetProperty(Props.Names.Country, region);
            GUIPropertyManager.SetProperty(Props.Names.Package,
                LogoManagerPlugin.Packages.Count > 1 ? Props.Values.Multiple : LogoManagerPlugin.LastSelectedPackage);
            GUIPropertyManager.SetProperty(Props.Names.Group,
                LogoManagerPlugin.Map.Count > 1 ? Props.Values.Multiple : (LogoManagerPlugin.Map.Count == 1 ? LogoManagerPlugin.Map[0].ChannelGroup : Props.Values.Undefined));
            GUIPropertyManager.SetProperty(Props.Names.Design, XMLConfig.Settings.Design);
        }

        public void SaveSettings()
        {
            XMLConfig.Settings.GroupType = String.Join("|", LogoManagerPlugin.Map.Select(g => g.ChannelType.ToString()).ToArray());
            XMLConfig.Settings.Group     = String.Join("|", LogoManagerPlugin.Map.Select(g => g.ChannelGroup).ToArray());
            XMLConfig.Settings.Package   = String.Join("|", LogoManagerPlugin.Map.Select(g => g.Package).ToArray());
            XMLConfig.Settings.LastSelectedPackage = LogoManagerPlugin.LastSelectedPackage;
            XMLConfig.Settings.Design = LogoManagerPlugin.DesignType;
            XMLConfig.Settings.LastGrab = LogoManagerPlugin.LastGrab;
            Stream fs = new FileStream(configFileFullName, FileMode.Create);
            var writer = new XmlTextWriter(fs, System.Text.Encoding.UTF8) {
                Formatting = Formatting.Indented,
                IndentChar = '\x09',
                Indentation = 1
            };
            var xmlnsEmpty = new XmlSerializerNamespaces();
            xmlnsEmpty.Add("", "");
            xs.Serialize(writer, XMLConfig, xmlnsEmpty);
            writer.Close();
            fs.Dispose();
        }

        public void DownloadConfig (string url)
        {

            ServicePointManager.ServerCertificateValidationCallback += (RemoteCertificateValidationCallback)((s, ce, ch, ssl) => true);
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            // Create an XmlUrlResolver 
            XmlUrlResolver resolver = new XmlUrlResolver();
            if (url.Contains("assembla"))
            {
                resolver.Credentials = new NetworkCredential("guest", "guest");
            }
            else
            {
                resolver.Credentials = CredentialCache.DefaultCredentials;
            }
            // Create the reader.
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.XmlResolver = resolver;

            var xmlReader = XmlReader.Create(url, settings);
            var remoteConfig = xs.Deserialize(xmlReader) as PluginConfig;
            if (remoteConfig != null)
            {
                XMLConfig.Mapping.Packages.Clear();
                XMLConfig.Mapping.Packages.AddRange(remoteConfig.Mapping.Packages);
                XMLConfig.Mapping.AltDesigns = remoteConfig.Mapping.AltDesigns;
            }
            xmlReader.Close();
        }


        public void UpdatePackagesSettingsIfNecessary()
        {
            if (!XMLConfig.Settings.AutoUpdateConfig && !string.IsNullOrEmpty(XMLConfig.Settings.ConfigUpdateURL)) return;
            try
            {
                DownloadConfig(XMLConfig.Settings.ConfigUpdateURL);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(LogoManagerPlugin.ThreadName + ": Updating from main url {0} failed, trying backup url {1}",
                        XMLConfig.Settings.ConfigUpdateURL, XMLConfig.Settings.AltConfigUpdateURL);
                    DownloadConfig(XMLConfig.Settings.AltConfigUpdateURL);

                }
                catch (Exception e)
                {
                    Log.Error(LogoManagerPlugin.ThreadName + ": Error while updating package settings: {0}", e.Message);

                }
            }
        }

        public bool LoadSelectedPack(string packname)
        {
            Package package = XMLConfig.Mapping.Packages.FirstOrDefault(e => e.Name == packname);
            if (package != null)
                return LoadSelectedPack(package);
            else {
                Log.Error(LogoManagerPlugin.ThreadName + ": Unknown configured pack \"{0}\".", packname);
                return false;
            }
        }

        private bool LoadSelectedPack(Package package)
        {
            LogoManagerPlugin.RepositoryBasePath = package.BaseURL;
            LogoManagerPlugin.RepositoryTVPath = LogoManagerPlugin.RepositoryBasePath + package.Logos_TV;
            LogoManagerPlugin.RepositoryRadioPath = LogoManagerPlugin.RepositoryBasePath + package.Logos_Radio;
            LogoManagerPlugin.RepositoryDesignPath = LogoManagerPlugin.RepositoryBasePath + package.Designs;
            LogoManagerPlugin.MappingURL = (!string.IsNullOrEmpty(package.Mapping_AltURL) ? package.Mapping_AltURL : LogoManagerPlugin.RepositoryBasePath + "LogoMapping.xml");
            LogoManagerPlugin.SupportsLastModified = package.SupportsLastModified;
            LogoManagerPlugin.StringsToRemove = package.StringPartsToRemove;
            return !string.IsNullOrEmpty(LogoManagerPlugin.MappingURL);
        }

        List<ChannelGroupType> StringToChannel(string input)
        {
          List<string> inputs = input.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries).ToList();
          return inputs.Select(s => s == "TV" ? ChannelGroupType.TV : ChannelGroupType.Radio).ToList();
        }

        private List<ChannelMap> ParseSettings()
        {
            List<ChannelMap> list = new List<ChannelMap>();
            LogoManagerPlugin.GroupType = StringToChannel(XMLConfig.Settings.GroupType);
            char[] delimiter = {'|'};
            LogoManagerPlugin.Packages = XMLConfig.Settings.Package.Split(delimiter, StringSplitOptions.RemoveEmptyEntries).ToList();
            LogoManagerPlugin.Groups = XMLConfig.Settings.Group.Split(delimiter, StringSplitOptions.RemoveEmptyEntries).ToList();
            // ensure we have same count of groups, group types and corresponding package elements
            int minCount = Math.Min(LogoManagerPlugin.Groups.Count, LogoManagerPlugin.GroupType.Count);
            minCount = Math.Min(minCount, LogoManagerPlugin.Packages.Count);
            LogoManagerPlugin.Groups.RemoveRange(minCount, LogoManagerPlugin.Groups.Count - minCount);
            LogoManagerPlugin.GroupType.RemoveRange(minCount, LogoManagerPlugin.GroupType.Count - minCount);
            LogoManagerPlugin.Packages.RemoveRange(minCount, LogoManagerPlugin.Packages.Count - minCount);

            for (int i = 0; i < LogoManagerPlugin.Groups.Count; i++) {
                list.Add(new ChannelMap {
                    ChannelGroup = LogoManagerPlugin.Groups[i],
                    ChannelType = LogoManagerPlugin.GroupType[i],
                    Package = LogoManagerPlugin.Packages[i]
                });
            }
            return list;
        }

        public static PluginSettings Instance()
        {
            return _instance ?? (_instance = new PluginSettings());
        }
    }
}
