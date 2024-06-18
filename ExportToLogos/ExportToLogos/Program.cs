using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ExportToLogos
{
    class Program
    {
        const string OldFileName = "LogoMapping.xml";
        const string NewFileName = "LogoMapping_new.xml";
        static List<Channel> chans = new List<Channel>();
        static List<Channel> chansNew = new List<Channel>();
        static List<Channel> chansOld = new List<Channel>();
        static List<Channel> newchans = new List<Channel>();
        static Channel prev = new Channel();
        static Channel curr = new Channel();
        static void Main(string[] args)
        {
            Program p = new Program();
            //p.ReadFile("");
            //p.ReadNdfXml("sat.xml");
            chansNew = p.ReadNewIndex(OldFileName);
            //chansOld =  p.ReadNewIndex("TestIndex1.xml");
            //p.ReadExportFile(@"D:\Documents and Settings\Дмитрий\Мои документы\Downloads\export36E.xml");
            //p.CompareLists(chansOld, chansNew);
            //p.DoNewWork(chansNew, chansOld);
            Console.WriteLine("Channels found in {0} (total {1}):", OldFileName, chansNew.Count);
            foreach (Channel ch in chansNew)
            {
                if (ch.Filename == string.Empty)
                    Console.WriteLine(ch.Name[0].name);
            }
            Console.WriteLine("Press any key to generate {0}", NewFileName);
            Console.ReadKey();
            p.WriteFile(chans);
        }

        void DoNewWork(List<Channel> newlist, List<Channel> oldlist)
        {
            foreach (Channel m in newlist)
            {
                string ClearNew = ClearString(m.Name[0].name).ToLower();
                string HardClearNew = HardClear(ClearNew);

                foreach (Channel n in oldlist)
                {
                    if (m.Name[0].name != n.Name[0].name)
                    {
                        string ClearOld = ClearString(n.Name[0].name).ToLower();
                        string HardClearOld = HardClear(ClearOld);
                        if (ClearNew == ClearOld)
                        {
                            m.Name.Add(n.Name[0]);
                            //newlist.Remove(newlist.Where(e => e.Name[0].name == n.Name[0].name).First());
                        }
                        else
                        {
                            if (HardClearNew == HardClearOld)
                            {
                                m.Name.Add(n.Name[0]);
                                //newlist.Remove(newlist.Where(e => e.Name[0].name == n.Name[0].name).First());
                            }
                            else
                            {
                                string translated = translit(HardClearOld);
                                if (translated == HardClearNew)
                                {
                                    m.Name.Add(n.Name[0]);
                                }

                            }
                        }
                    }

                }
                chans.Add(m);
            }
        }

        void DoWork()
        {
            List<Channel> templist = chans;
            templist = chans.OrderBy(e => e.Name[0].name).ToList();
            chans.Clear();
            chans = templist;

            for (int j = 0; j < chans.Count - 1; j++)
            {
                //curr = chans[j];
                if (prev.Name == null)
                {
                    prev = chans[j];
                    newchans.Add(prev);
                }
                else
                {
                    curr = chans[j];

                    if (curr.Name[0].name.ToLower().Replace(" ", "").Replace("-", "") == prev.Name[0].name.ToLower().Replace(" ", "").Replace("-", ""))
                    {
                        //advanced match ok
                        if (curr.Name[0].name.ToLower() == prev.Name[0].name.ToLower())
                        {
                            //full dupe - need join
                            if (prev.Name[0].sat.IndexOf(curr.Sat) == -1)
                            {
                                prev.Name[0].sat.Add(curr.Sat);
                            }
                            if (newchans.Count != 0 && newchans.LastOrDefault().Name[0].name.ToLower() == prev.Name[0].name.ToLower())
                            {
                                newchans.Last().Name[0] = prev.Name[0];
                            }
                            else
                            {
                                newchans.Add(prev); // add or update newchans with prev
                            }
                        }
                        else
                        {
                            //only difference are whitespaces - need to add new "Name" item inside channel
                            prev.Name.Add(curr.Name[0]);
                        }
                    }
                    else
                    {
                        //new item = need adding
                        newchans.Add(curr);
                        prev = chans[j];
                    }
                }
            }

        }

        /// <summary>
        /// Update new list with filenames from old list
        /// </summary>
        /// <param name="oldlist"></param>
        /// <param name="newlist"></param>
        void CompareLists(List<Channel> oldlist, List<Channel> newlist)
        {
            foreach (Channel k in newlist)
            {
                string ClearNew = ClearString(k.Name[0].name).ToLower();
                string HardClearNew = HardClear(ClearNew);
                foreach (Channel j in oldlist)
                {
                    string ClearOld = ClearString(j.Name[0].name).ToLower();
                    string HardClearOld = HardClear(ClearOld);
                    string translated = translit(HardClearOld);
                    if (HardClearNew == HardClearOld || translated == HardClearNew)
                    {
                        if (j.Filename != string.Empty && k.Filename == string.Empty)
                        {
                            k.Filename = j.Filename;
                        }
                    }
                }
                chans.Add(k);
            }
        }

        void ReadNdfXml(string file)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(file);
            XmlNodeList chanlist = doc.SelectNodes("//channel");
            foreach (XmlNode node in chanlist)
            {
                Channel ch = new Channel();
                ch.Filename = string.Empty;
                List<string> satellite = new List<string>();
                satellite.Add(node.ParentNode.ParentNode.Attributes["position"].InnerText);
                List<Name> lst = new List<Name>();
                Name name = new Name();
                name.name = node.Attributes["name"].InnerText;
                name.sat = satellite;
                    lst.Add(name);
                ch.Name = lst;
                ch.Sat = node.ParentNode.ParentNode.Attributes["position"].InnerText;
                chans.Add(ch);
            }

        }

        void ReadExportFile(string filename)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);
            XmlNodeList chanlist = doc.SelectNodes("//channel");
            foreach (XmlNode node in chanlist)
            {
                Channel ch = new Channel();
                //<channel>/<TuningDetails>/<tune Provider="" Name ="">
                ch.Sat = node.ChildNodes[1].ChildNodes[0].Attributes["Provider"].InnerText;
                ch.Name = new List<Name>();
                Name nm = new Name();
                nm.name = node.ChildNodes[1].ChildNodes[0].Attributes["Name"].InnerText;
                nm.sat = new List<string>();
                nm.sat.Add(node.ChildNodes[1].ChildNodes[0].Attributes["Provider"].InnerText);
                ch.Name.Add(nm);
                chans.Add(ch);
            }


        }

          ///<Channel>
          ///  <Item Name="2x2">
          ///    <Satellite>36E3k</Satellite>
          ///    <Satellite>60E</Satellite>
          ///    <Satellite>75E</Satellite>
          ///    <Satellite>85E</Satellite>
          ///    <Satellite>36E+</Satellite>
          ///  </Item>
          ///  <Item Name="2x2_URAL">
          ///    <Satellite>75E</Satellite>
          ///  </Item>
          ///  <File />
          ///</Channel>
        List<Channel> ReadNewIndex(string filename)
        {
            List<Channel> list = new List<Channel>();
            XmlDocument doc = new XmlDocument();
              if (!File.Exists(filename))
                  return list;
            doc.Load(filename);
            XmlNodeList chanlist = doc.SelectNodes("//Channel");
            foreach (XmlNode node in chanlist)
            {
                Channel ch = new Channel();
                ch.Filename = node.LastChild.InnerText;
                ch.Name = new List<Name>();
                ch.Sat = "multi";
                foreach (XmlNode item in node.SelectNodes("Item"))
                {
                    Name nm = new Name();
                    nm.name = item.Attributes["Name"].InnerText;
                    nm.sat = new List<string>();
                    foreach (XmlNode satellite in item.ChildNodes)
                    {
                        nm.sat.Add(satellite.InnerText);
                    }
                    ch.Name.Add(nm);
                }
                list.Add(ch);
            }
            return list;
        }

        void ReadOldIndex(string filename)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);
            XmlNodeList chanlist = doc.SelectNodes("//Channel");
            foreach (XmlNode node in chanlist)
            {
                Channel ch = new Channel();
                ch.Filename = node.LastChild.InnerText;
                int i = 0;
                List<string> satellite = new List<string>();
                satellite.Add("36E");
                List<Name> lst = new List<Name>();
                while (node.LastChild != node.ChildNodes[i])
                {
                    Name nm = new Name();
                    nm.name = node.ChildNodes[i].InnerText;
                    nm.sat = satellite;
                    i++;
                    lst.Add(nm);
                }
                ch.Name = lst;
                chansOld.Add(ch);
            }
        }

        string ClearString(string input)
        {

            return input.Split('(').First().Split('+').First().Replace(" ", "").Replace("-", "").Replace("_", "");
        }
        string HardClear(string input)
        {
            return input.ToLower().Replace("tv", "").Replace("тв", "").Replace("russia", "").Replace("channel", "").Replace("канал", "").Replace("ru", "");
        }
        public string translit(string input)
        {
            string output = input.ToLower().
                        Replace("shch", "щ").
                        Replace("sch", "ш").
                        Replace("zh", "ж").
                        Replace("ts", "ц").
                        Replace("tс", "ц").
                        Replace("tz", "ц").
                        Replace("yj", "ый").
                        Replace("ch", "ч").
                        Replace("sh", "ш").
                        Replace("yu", "ю").
                        Replace("ju", "ю").
                        Replace("yo", "е").
                        Replace("jo", "е").
                        Replace("ya", "я").
                        Replace("ja", "я").
                        Replace("ye", "е").
                        Replace("je", "е").
                        Replace("kh", "х").
                        Replace("c", "ц").
                        Replace("a", "а").
                        Replace("b", "б").
                        Replace("v", "в").
                        Replace("g", "г").
                        Replace("d", "д").
                        Replace("e", "е").
                        Replace("z", "з").
                        Replace("i", "и").
                        Replace("j", "й").
                        Replace("k", "к").
                        Replace("l", "л").
                        Replace("m", "м").
                        Replace("n", "н").
                        Replace("o", "о").
                        Replace("p", "п").
                        Replace("r", "р").
                        Replace("s", "с").
                        Replace("t", "т").
                        Replace("u", "у").
                        Replace("f", "ф").
                        Replace("h", "х").
                        Replace("'", "ь").
                        Replace("y", "ы").
                        Replace("w", "щ");

            return output;
        }
        void WriteFile(List<Channel> filetowrite)
        {
            XmlDocument doc = new XmlDocument();
            //doc.Load(@"D:\Documents and Settings\Дмитрий\Мои документы\Visual Studio 2010\Projects\LogoDownloader\LogoDownloader\Repository.xml");
            XmlTextWriter writer = new XmlTextWriter(NewFileName, Encoding.UTF8);
            writer.WriteStartDocument(true);
            writer.Formatting = Formatting.Indented;
            writer.Indentation = 2;
            writer.WriteStartElement("Mappings");
            foreach (Channel ch in filetowrite)
            {
                writer.WriteStartElement("Channel");
                foreach (Name n in ch.Name)
                {
                    writer.WriteStartElement("Item");
                    writer.WriteStartAttribute("Name");
                    writer.WriteString(n.name);
                    writer.WriteEndAttribute();
                    foreach (string sat in n.sat)
                    {
                        writer.WriteStartElement("Satellite");
                        writer.WriteString(sat);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteStartElement("File");
                writer.WriteString(ch.Filename);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Close();

        }
    }




    public class Channel
    {
        public string Filename { get; set; }
        public List<Name> Name { get; set; }
        public string Sat { get; set; }
    }
    public class Name
    {
        public string name { get; set; }
        public List<string> sat { get; set; }
    }

}
