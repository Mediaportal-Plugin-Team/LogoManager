using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using LogoManager;

namespace LogoMappingChecker
{
	public partial class Form1 : Form
	{

		private static string configFileFullName = @"..\..\..\LogoManager\LogoManager.config";
		private static readonly Dictionary<string, string> Mapping_TVorCommon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_TVorCommon_withReplacedParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_Radio = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_Radio_withReplacedParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		public static string MappingURL;
		public static string RepositoryBasePath;
		public static string RepositoryTVPath;
		public static string RepositoryRadioPath;
		public static string RepositoryDesignPath;
		private static bool UseRadioMapping;
		private static PluginConfig XMLConfig;
		public static List<string> StringsToRemove;
		public static List<string> Packages = new List<string>();
		public static List<string> Groups = new List<string>();
		public static List<ChannelGroupType> GroupType = new List<ChannelGroupType>();
		public static List<ChannelMap> Map = new List<ChannelMap>();

		public Form1()
		{
			InitializeComponent();

		    var args = Environment.GetCommandLineArgs();
		    if (args.Length > 1)
		        configFileFullName = args[1];
			XMLConfig = new PluginConfig();
			if (File.Exists(configFileFullName)) {
				FileStream fs = null;
				try {
					fs = new FileStream(configFileFullName, FileMode.Open);
					XmlReader reader = XmlReader.Create(fs);
					XmlSerializer xs = new XmlSerializer(typeof(PluginConfig));
					XMLConfig = xs.Deserialize(reader) as PluginConfig;
					fs.Dispose();
					Log(string.Format("Parsed {0}. Found {1} packages.", configFileFullName, XMLConfig.Mapping.Packages.Count));
				}
				catch {
					MessageBox.Show(null, "Error on parsing config file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					Close();
				}
				if (fs != null)
					fs.Dispose();

				foreach (var pack in XMLConfig.Mapping.Packages)
					comboBox1.Items.Add(pack);
			}
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			CheckPackage((Package) (sender as ComboBox).SelectedItem);
		}

		private bool CheckPackage(Package package)
		{
			RepositoryTVPath = package.BaseURL + package.Logos_TV;
			RepositoryRadioPath = package.BaseURL + package.Logos_Radio;
			RepositoryDesignPath = package.BaseURL + package.Designs;
			MappingURL = (!string.IsNullOrEmpty(package.Mapping_AltURL) ? package.Mapping_AltURL : package.BaseURL + "LogoMapping.xml");
			
			Mapping_TVorCommon.Clear();
			Mapping_Radio.Clear();
			Mapping_TVorCommon_withReplacedParts.Clear();
			Mapping_Radio_withReplacedParts.Clear();
			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(MappingURL);
			} catch (Exception e) {
				Log(string.Format("Checking package \"{0}\" failed.\n", package.Name), Color.Red);
				Log(string.Format("Can't download or parse mapping file \"{0}\" of package \"{1}\": {2}", MappingURL, package.Name, e.Message), Color.DarkRed);
				return false;
			}
			Log(string.Format("\nChecking package \"{0}\"...", package.Name), Color.DarkBlue);
			// TV or common
			XmlNodeList radioFileList = doc.SelectNodes("//Radio//Channel//File");
			UseRadioMapping = (radioFileList != null) && (radioFileList.Count > 0);

			XmlNodeList fileList = doc.SelectNodes("//TV//Channel//File");
			if (fileList != null && ((fileList.Count == 0) && !UseRadioMapping))
			{
				fileList = doc.SelectNodes("//Channel//File");
				Log("Checking general section...", Color.DarkBlue);
			}
			else
			{
				Log("Checking TV section...", Color.DarkBlue);
			}

			toolStripProgressBar1.Visible = true;
			toolStripStatusLabel1.Visible = true;


			CheckFileList(package.Name, fileList, ChannelGroupType.TV);
			if (UseRadioMapping)
			{
				Log("Checking radio section...", Color.DarkBlue);
				CheckFileList(package.Name, radioFileList, ChannelGroupType.Radio);
			}

			toolStripProgressBar1.Visible = false;
			toolStripStatusLabel1.Visible = false;
			Log("Finished checking " + package.Name, Color.DarkGreen);
			return true;
		}

		private void CheckFileList(string packName, XmlNodeList fileList, ChannelGroupType channelType)
		{
			toolStripProgressBar1.Maximum = fileList.Count;
			WebClient wc = new WebClient();
			int i = 0;
			string tmpFolder = Path.Combine(Path.GetTempPath() /*Environment.GetEnvironmentVariable("Temp")*/, "LogoMappingChecker");
			foreach (XmlNode file in fileList)
			{
				toolStripStatusLabel1.Text = "checking \"" + file.InnerText + "\" ...";
				toolStripProgressBar1.Value = i;
				if (string.IsNullOrEmpty(file.InnerText))
				{
					Log(string.Format(" Error for {0}: {1}: empty file name!", packName, channelType), Color.Red);
					continue;
				}

				string repoPath = channelType == ChannelGroupType.Radio ? RepositoryRadioPath : RepositoryTVPath;
				string remoteLogo = repoPath + file.InnerText;
				string localLogo = Path.Combine(tmpFolder, file.InnerText);
				string path = Path.GetDirectoryName(localLogo);
				try
				{
					if (!Directory.Exists(path))
						Directory.CreateDirectory(path);
					wc.DownloadFile(remoteLogo, localLogo);
				}
				catch (WebException e)
				{
					Log(string.Format(" Error for {0}: {1}: {2}", packName, channelType, file.InnerText), Color.Red);
					Log(string.Format("Can't download file \"{0}\" or save it locally to \"{1}\": {2}", remoteLogo, localLogo, e.Message), Color.DarkRed);
					continue;
				}
				i++;
				Update();
			}
		}

		private void Log(string text, Color? color = null)
		{
			int length = logBox.TextLength;
			logBox.AppendText(text + "\n");
			logBox.SelectionStart = length;
			logBox.SelectionLength = text.Length;
			logBox.SelectionColor = color ?? SystemColors.WindowText;
			logBox.SelectionLength = 0;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			foreach (var package in XMLConfig.Mapping.Packages)
				CheckPackage(package);
		}
	}
}
