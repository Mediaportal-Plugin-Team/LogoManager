using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace LogoMappingUpdater
{
  internal class Program
  {
    static readonly List<Channel> chans = new List<Channel>();
    static readonly List<Channel> newchans = new List<Channel>();
    static readonly List<Channel> diff = new List<Channel>();
    static void Main()
    {
      if (File.Exists("export.xml") && File.Exists("LogoMapping.xml"))
      {
        ReadExportFile("export.xml");
        ReadLogoMapping("LogoMapping.xml");
        List<string> names = chans.Select(e => e.Name.ToLower()).ToList();
        foreach (Channel ch in newchans)
        {
          //if ( (!names.Any(s => s.Equals(ch.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
          int Idx = names.IndexOf(ch.Name.ToLower().Trim());
          if ( (Idx == -1) || ((chans[Idx].Type != ChannelType.Undefined) && (chans[Idx].Type != ch.Type) ) )
            diff.Add(ch);
        }
        WriteFile(diff);
      } 
      else
      {
        Console.WriteLine("Logo Mapping Updater utility");
        Console.WriteLine("    to use by logo pack maintainers for LogoManager plugin for MediaPortal (www.team-mediaportal.com)");
        Console.WriteLine("  Useful when you have XML file with TVServer channel export");
        Console.WriteLine("    and want to check missing mappings in LogoMapping XML file.");
        Console.WriteLine("  REQUIRES export.xml and LogoMapping.xml files in same directory.");
        Console.WriteLine("  Just run LogoMappingUpdater without parameters");
        Console.WriteLine("    and it will generate diff.xml file with channel names, ");
        Console.WriteLine("    that are present in export.xml, but missing in LogoMapping.xml.");
        Console.WriteLine("    Names will be compared with trimmed spaces and case-insensitive.");
      }
    }

    static void ReadExportFile(string filename)
    {
      XmlDocument doc = new XmlDocument();
      doc.Load(filename);
      XmlNodeList chanlist = doc.SelectNodes("//channel");
      foreach (XmlNode node in chanlist)
      {
        //<channel>/<TuningDetails>/<tune Provider="" Name ="">
        Channel ch = new Channel
        {
          Provider = node.ChildNodes[1].ChildNodes[0].Attributes["Provider"].InnerText,
          Name = node.ChildNodes[1].ChildNodes[0].Attributes["Name"].InnerText,
          Type =
            node.Attributes["IsTv"].InnerText == "True"
              ? ChannelType.TV
              : (node.Attributes["IsRadio"].InnerText == "True"
                  ? ChannelType.Radio 
                  : ChannelType.Undefined)
        };
        newchans.Add(ch);
      }
    }

    ///<Channel>
    ///  <Item Name="2x2">
    ///    <Provider>36E3k</Provider>
    ///    <Provider>60E</Provider>
    ///    <Provider>75E</Provider>
    ///    <Provider>85E</Provider>
    ///    <Provider>36E+</Provider>
    ///  </Item>
    ///  <Item Name="2x2_URAL">
    ///    <Provider>75E</Provider>
    ///  </Item>
    ///  <File />
    ///</Channel>
    static void ReadLogoMapping(string filename)
    {
      XmlDocument doc = new XmlDocument();
      doc.Load(filename);

      XmlNodeList ChannelList = doc.SelectNodes("//TV//Channel");
      bool UseTVMapping = (ChannelList.Count > 0);
      XmlNodeList RadioChannelList = doc.SelectNodes("//Radio//Channel");
      bool UseRadioMapping = (RadioChannelList.Count > 0);
      if (!UseTVMapping && !UseRadioMapping)
          ChannelList = doc.SelectNodes("//Channel");
      
      // TV or common
      foreach (XmlNode node in ChannelList) {
        foreach (XmlNode item in node.SelectNodes("Item")) {
          Channel ch = new Channel {
            Name = item.Attributes["Name"].InnerText,
            Filename = node["File"].InnerText,
            Type = UseTVMapping ? ChannelType.TV : ChannelType.Undefined
          };
          chans.Add(ch);
        }
      }
      // Radio
      if (UseRadioMapping) {
        foreach (XmlNode node in RadioChannelList) {
          foreach (XmlNode item in node.SelectNodes("Item")) {
            Channel ch = new Channel {
              Name = item.Attributes["Name"].InnerText,
              Filename = node["File"].InnerText,
              Type = ChannelType.Radio
            };
            chans.Add(ch);
          }
        }
      }
    }

    static void WriteFile(List<Channel> filetowrite)
    {
      using (XmlTextWriter writer = new XmlTextWriter("diff.xml", Encoding.UTF8) {Formatting = Formatting.Indented, IndentChar = '\t'}) {
        writer.WriteStartDocument(true);
        writer.WriteStartElement("Mappings");

        writer.WriteStartElement("TV");
        foreach (Channel ch in filetowrite.Where(ch => ch.Type == ChannelType.TV))
          WriteChannelElement(writer, ch);
        writer.WriteEndElement();

        writer.WriteStartElement("Radio");
        foreach (Channel ch in filetowrite.Where(ch => ch.Type == ChannelType.Radio))
          WriteChannelElement(writer, ch);
        writer.WriteEndElement();

        foreach (Channel ch in filetowrite.Where(ch => ch.Type == ChannelType.Undefined))
          WriteChannelElement(writer, ch);

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Close();
      }
    }

    static void WriteChannelElement(XmlTextWriter writer, Channel ch)
    {
      writer.WriteStartElement("Item");
      writer.WriteStartAttribute("Name");
      writer.WriteString(ch.Name);
      writer.WriteEndAttribute();
      if (!string.IsNullOrEmpty(ch.Provider))
      {
        writer.WriteStartElement("Provider");
        writer.WriteString(ch.Provider);
        writer.WriteEndElement();
      }
      writer.WriteEndElement();
    }

    private enum ChannelType
    {
      Undefined,
      TV,
      Radio
    }

    private class Channel
    {
      public string Name { get; set; }
      public string Filename { get; set; }
      public string Provider { get; set; }
      public ChannelType Type { get; set; }
    }

    }
}
