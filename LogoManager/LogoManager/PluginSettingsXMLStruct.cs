using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace LogoManager
{

    [XmlRoot("Configuration")]
    public class PluginConfig
    {
        public Mapping Mapping { get; set; }
        public Settings Settings { get; set; }

        public PluginConfig()
        {
            Mapping = new Mapping();
            Settings = new Settings();
        }
    }

    public class Mapping
    {
        private readonly List<Package> packages = new List<Package>();
        public List<Package> Packages
        {
            get { return packages; }
            set
            {
                packages.Clear();
                packages.AddRange(value);
            }
        }
        public string AltDesigns { get; set; }
    }


    public class Package
    {
        public string Name { get; set; }
        public string CountryName { get; set; }
        public string CountryName_Eng { get; set; }
        public string BaseURL { get; set; }
        [DefaultValue("")]
        public string Designs { get; set; }
        public string Logos_TV { get; set; }
        public string Logos_Radio { get; set; }
        [DefaultValue("")]
        public string Mapping_AltURL { get; set; }
        public bool SupportsLastModified { get; set; }
        public List<string> StringPartsToRemove { get; set; }
        public bool ShouldSerializeStringPartsToRemove()
        {
            return ((StringPartsToRemove != null) && (StringPartsToRemove.Count > 0));
        }
    }

    public class Settings
    {
        private const string DefaultConfigUpdateURL = "http://subversion.assembla.com/svn/mediaportal.LogoManager/trunk/LogoManager/LogoManager/LogoManager.config";
        [XmlIgnore]
        public string AltConfigUpdateURL = "https://raw.githubusercontent.com/Jasmeet181/LogoManager-updates/master/LogoManager.config";

        public string Group { get; set; }
        public string GroupType { get; set; }
        public string Package { get; set; }
        public string LastSelectedPackage { get; set; }
        public string Design { get; set; }
        public int AutoGrabIntervalInDays { get; set; }

        [DefaultValue(0)]
        public long LastGrab { get; set; }

        [DefaultValue(true)]
        public bool AutoUpdateConfig { get; set; }

        [DefaultValue(DefaultConfigUpdateURL)]
        public string ConfigUpdateURL { get; set; }

        public Settings()
        {
            Group = string.Empty;
            GroupType = string.Empty;
            Package = string.Empty;
            LastSelectedPackage = string.Empty;
            Design = "Simple light glow";
            AutoGrabIntervalInDays = 7;
            AutoUpdateConfig = true;
            ConfigUpdateURL = DefaultConfigUpdateURL;
        }
    }

    [Serializable]
    public enum ChannelGroupType
    {
        [XmlEnum("TV")]
        TV,
        [XmlEnum("Radio")]
        Radio
    }

    public class ChannelMap
    {
        public string ChannelGroup { get; set; }
        public ChannelGroupType ChannelType { get; set; }
        public string Package { get; set; }
    }
    

}
