using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using MediaPortal.Util;
using TvDatabase;

namespace LogoManager
{
    public class LMChannel
    {
        public string Name;
        public int Id;
        public readonly List<string> OriginalNamesList = new List<string>();
        public ChannelGroupType Type;
    }

    public class LMChannelGroup
    {
        public string Name;
        public readonly List<LMChannel> Channels = new List<LMChannel>();
    }

    public interface ITVServiceProvider
    {
        //bool IsAvailable();
        string ReadableName();
        string GetPathToLogos(ChannelGroupType type);
        IEnumerable<string> GetGroupNames(ChannelGroupType? type);
        LMChannelGroup GetGroupForGroupNameAndType(string groupName, ChannelGroupType type);
        int GetChannelCount();
        int GetChannelCountForGroupsExceptAllChannels();
    }

    public class TVE3 : ITVServiceProvider
    {
        private readonly TvBusinessLayer layer;

        public TVE3() {
            layer = new TvBusinessLayer();
        }

        public static bool IsAvailable()
        {
            var TVPluginFullPath = Path.Combine(Config.GetSubFolder(Config.Dir.Plugins, "Windows"), "TVPlugin.dll");

            var fileExists = File.Exists(TVPluginFullPath) && PluginManager.IsPlugInEnabled("TV Plugin");
            if (!fileExists)
                return false;
            var hasHome = false;
            try {
                var assem = Assembly.LoadFrom(TVPluginFullPath);
                hasHome = assem.GetExportedTypes().Any(t => t.IsClass && t.FullName == "TvPlugin.TVHome");
            }
            catch (Exception e) {
                Log.Error(LogoManagerPlugin.ThreadName + ": Exception while checking TvPlugin interfaces: \n{0}", e.Message);
            }

            return hasHome;
                //&& !MPSettings.Instance.GetValueAsBool("plugins", "ARGUS TV", false);
        }

        public string ReadableName()
        {
            return "MediaPortal TVE3 engine";
        }

        public string GetPathToLogos(ChannelGroupType type)
        {
            switch (type) {
                case ChannelGroupType.TV:
                    return Thumbs.TVChannel + "\\";
                case ChannelGroupType.Radio:
                    return Thumbs.Radio + "\\";
                default:
                    throw new NotImplementedException();
            }
        }

        public IEnumerable<string> GetGroupNames(ChannelGroupType? type)
        {
            switch (type)
            {
                case ChannelGroupType.TV:
                    return ChannelGroup.ListAll().Select(group => group.GroupName).ToList();
                case ChannelGroupType.Radio:
                    return RadioChannelGroup.ListAll().Select(group => group.GroupName).ToList();
                default:
                    var result = ChannelGroup.ListAll().Select(group => group.GroupName).ToList();
                    result.AddRange(RadioChannelGroup.ListAll().Select(group => group.GroupName).ToList());
                    return result;
            }
        }

        public LMChannelGroup GetGroupForGroupNameAndType(string groupName, ChannelGroupType type)
        {
            IList<Channel> channels = null;
            if (type == ChannelGroupType.Radio) {
                var group = layer.GetRadioChannelGroupByName(groupName);
                if (group != null)
                    channels = layer.GetRadioGuideChannelsForGroup(group.IdGroup);
            } else {
                var group = layer.GetGroupByName(groupName);
                if (group != null)
                    channels = layer.GetChannelsInGroup(group);
            }
            var lmGroup = new LMChannelGroup {Name = groupName};
            if (channels != null)
                foreach (var channel in channels)
                    AddChannelToGroup(lmGroup, channel);
            return lmGroup;
        }

        private void AddChannelToGroup(LMChannelGroup group, Channel channel)
        {
            var lmCh = new LMChannel {
                Name = channel.DisplayName,
                Id = channel.IdChannel,
                Type = channel.IsRadio ? ChannelGroupType.Radio : ChannelGroupType.TV
            };
            foreach (var tuningDetail in channel.ReferringTuningDetail().Where(tuningDetail => tuningDetail.Name != lmCh.Name))
                lmCh.OriginalNamesList.Add(tuningDetail.Name);
            group.Channels.Add(lmCh);
        }

        public int GetChannelCount()
        {
            return layer.Channels.Count;
        }

        public int GetChannelCountForGroupsExceptAllChannels()
        {
            int result = 0;
            foreach (var channelGroup in ChannelGroup.ListAll().Where(channelGroup => channelGroup.GroupName != TvLibrary.Interfaces.TvConstants.TvGroupNames.AllChannels))
                result += layer.GetChannelsInGroup(channelGroup).Count;
            foreach (var channelGroup in RadioChannelGroup.ListAll().Where(channelGroup => channelGroup.GroupName != TvLibrary.Interfaces.TvConstants.RadioGroupNames.AllChannels))
                result += layer.GetRadioGuideChannelsForGroup(channelGroup.IdGroup).Count; // bad call, but BusinessLayer doesn't have GetChannelsInGroup for radio
            return result;
        }
    }

    public class Argus : ITVServiceProvider
    {
        private const string AllChannelsGroupName = "All Channels";
        //private const string  ArgusAPIUrl = @"http://11d.dyndns.dk:49943/ArgusTV/";
        private const string ArgusAPIUrl = @"http://localhost:49943/ArgusTV/";
        private static readonly JavaScriptSerializer js = new JavaScriptSerializer();

        #region ITVServiceProvider members

        public static bool IsAvailable()
        {
            if (!MPSettings.Instance.GetValueAsBool("plugins", "ARGUS TV", false))
                return false;
            else if (!MPSettings.Instance.GetValueAsBool("argustv", "isSingleSeat", false)) {
                Log.Error(LogoManagerPlugin.ThreadName + ": Argus multiseat setup detected which is not supported. Please ask for support on Argus forums.");
                return false;
            }
            else
                return Ping();
        }

        public string ReadableName()
        {
            return "Argus TV engine";
        }

        public string GetPathToLogos(ChannelGroupType type)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"ARGUS TV\Channel Logos\");
        }

        public IEnumerable<string> GetGroupNames(ChannelGroupType? type)
        {
            var result = new List<string> {AllChannelsGroupName};
            if (type != null) {
                result.AddRange(GetGroups((ChannelGroupType) type).Select(g => g.GroupName).ToList());
            }
            else {
                result.AddRange(GetGroups(ChannelGroupType.TV).Select(g => g.GroupName).ToList());
                result.AddRange(GetGroups(ChannelGroupType.Radio).Select(g => g.GroupName).ToList());
            }
            return result;
        }

        public LMChannelGroup GetGroupForGroupNameAndType(string groupName, ChannelGroupType type)
        {
            var lmGroup = new LMChannelGroup();
            if (string.Equals(groupName, AllChannelsGroupName))
            {
                lmGroup.Name = groupName;
                foreach (var ch in GetChannels(type))
                    AddChannelToGroup(lmGroup, ch);
            }
            else
            {
                ArgusChannelGroup group = GetGroups(type).First(g => g.GroupName == groupName);
                ArgusChannel[] channelsInGroup = GetChannelsInGroup(group.ChannelGroupId);
                lmGroup.Name = group.GroupName;
                foreach (ArgusChannel ch in channelsInGroup)
                    AddChannelToGroup(lmGroup, ch);
            }
            return lmGroup;
        }

        private void AddChannelToGroup(LMChannelGroup group, ArgusChannel channel)
        {
            var lmCh = new LMChannel {
                Name = channel.DisplayName,
                Id = channel.Id,
                Type = (ChannelGroupType)channel.ChannelType
            };
            group.Channels.Add(lmCh);
        }

        public int GetChannelCount()
        {
            return GetChannels(ChannelGroupType.TV).Length + GetChannels(ChannelGroupType.Radio).Length;
        }

        public int GetChannelCountForGroupsExceptAllChannels()
        {
            return GetGroups(ChannelGroupType.TV).Sum(gr => GetChannelsInGroup(gr.ChannelGroupId).Length)
                 + GetGroups(ChannelGroupType.Radio).Sum(gr => GetChannelsInGroup(gr.ChannelGroupId).Length);
        }

        #endregion

        #region Argus API Methods

        private ArgusChannel[] GetChannels(ChannelGroupType type)
        {
            return js.Deserialize<ArgusChannel[]>(GetArgusResponse("Scheduler/Channels/" + (int)type));
        }

        private ArgusChannelGroup[] GetGroups(ChannelGroupType type)
        {
            return js.Deserialize<ArgusChannelGroup[]>(GetArgusResponse("Scheduler/ChannelGroups/" + (int)type));
        }

        private ArgusChannel[] GetChannelsInGroup(string groupId)
        {
            return js.Deserialize<ArgusChannel[]>(GetArgusResponse("Scheduler/ChannelsInGroup/" + groupId));
        }

        private static bool Ping()
        {
            string ver;
            try
            {
                ver = GetArgusResponse("Core/Version");
            }
            catch (Exception)
            {
                return false;
            }

            return ver.Contains("2.3"); // bad code
        }

        private static string GetArgusResponse(string request)
        {
            var wc = new WebClient();
            wc.Headers.Add("Content-type: application/json");
            wc.Headers.Add("Accept: application/json");
            wc.Encoding = System.Text.Encoding.UTF8;
            var response = wc.DownloadString(ArgusAPIUrl + request);
            wc.Dispose();
            return response;
        }
        #endregion

        #pragma warning disable 169, 649
        // ReSharper disable once ClassNeverInstantiated.Local
        private class ArgusChannel
        {
          public string ChannelId; // { get; set; }
            public int Id; // { get; set; }
            public string GuideChannelId; // { get; set; }
            public int ChannelType; // { get; set; }
            public string DisplayName; // { get; set; }
            public int? LogicalChannelNumber; // { get; set; }
            public bool VisibleInGuide; // { get; set; }
            public int? DefaultPreRecordSeconds; // { get; set; }
            public int? DefaultPostRecordSeconds; // { get; set; }
            public string BroadcastStart; // { get; set; }
            public string BroadcastStop; // { get; set; }
            public int Sequence; // { get; set; }
            public int Version; // { get; set; }
            public string CombinedDisplayName; // { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class ArgusChannelGroup
        {
            public string ChannelGroupId;// { get; set; }
            public int Id;// { get; set; }
            public int ChannelType;// { get; set; }
            public string GroupName;// { get; set; }
            public bool VisibleInGuide;// { get; set; }
            public int Sequence;// { get; set; }
            public int Version;// { get; set; }
        }
        #pragma warning restore 169, 649
  }

}