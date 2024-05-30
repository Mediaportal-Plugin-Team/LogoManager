using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Xml;
using System.ComponentModel;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
 
namespace LogoManager
{
	public static class Props
	{
		public static class Names
		{
			private const string prefix = "#LogoManager.";
			public const string Country = prefix + "Country";
			public const string Package = prefix + "Package";
			public const string Group = prefix + "Group";
			public const string Design = prefix + "Design";
			public const string State = prefix + "State";
			public const string PercentChannels = prefix + "percentChannels";
			public const string PercentGroups = prefix + "percentGroups";
		}
		public static class Values
		{
			public const string Multiple = "Multiple";
			public const string Undefined = "-";
			public const string GrabNow = "Grab now";
			public const string CancelGrab = "Cancel grabbing";
		}
	}

	public class LogoManagerPlugin : GUIWindow, ISetupForm
	{
		#region Variables
		[SkinControlAttribute(270)]
		protected GUIListControl ChannelsList;
		[SkinControlAttribute(280)]
		protected GUIButtonControl buttonDesign;
		[SkinControlAttribute(290)]
		protected GUIButtonControl buttonGrab;
		[SkinControlAttribute(300)]
		protected GUIButtonControl buttonChannelGroup;
		[SkinControlAttribute(260)]
		protected GUIButtonControl buttonPackage;
		[SkinControlAttribute(310)]
		protected GUIProgressControl ChannelProgress;
		[SkinControlAttribute(320)]
		protected GUIProgressControl GroupProgress;

		private Design design;
		private System.Timers.Timer updateTimer;
		private static readonly DateTime DateStartsFrom = new DateTime(1970, 1, 1);
		private static readonly string ChannelGroupType_TVString = GUILocalizeStrings.Get(100001); // TV
		private static readonly string ChannelGroupType_RadioString = GUILocalizeStrings.Get(100030); // Radio
		private static int PerSessionGeneratedLogos;
		private static int LastGeneratedLogos;
		private static int LastNotMappedLogos;
		private static int LastFailedLogos;
		private static int ChannelsWithSameName;
		private static BackgroundWorker GrabLogoBGWorker;
		private static BackgroundWorker FirstRunBGWorker;
		private static bool UseRadioMapping;
		private static readonly Dictionary<string, string> Mapping_TVorCommon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_TVorCommon_withReplacedParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_Radio = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> Mapping_Radio_withReplacedParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static string ChannelGroupType_ToString(ChannelGroupType gType) { return Enum.GetName(typeof(ChannelGroupType), gType); }
		private static string StringForChannelType(bool isRadio) { return isRadio ? "radio" : "TV"; }

		public const string ThreadName = "LogoManager";
		public static ITVServiceProvider TVService;
		public static int AutoUpdateInterval;
		public static long LastGrab;
		public static bool IsBackgroundGrab;
		public static string LastSelectedPackage = string.Empty;
		public static string DesignType = string.Empty;
		public static bool SupportsLastModified = true;
		public static string TVSourcesPath;
		public static string RadioSourcesPath;
		public static string DesignsPath;
		public static string TVLogosPath;
		public static string TVLogosBase;
		public static string RadioLogosPath;
		public static string RadioLogosBase;
		public static string MappingURL;
		public static string RepositoryBasePath;
		public static string RepositoryTVPath;
		public static string RepositoryRadioPath;
		public static string RepositoryDesignPath;
		public static List<string> StringsToRemove;
		public static List<string> Packages = new List<string>();
		public static List<string> Groups = new List<string>();
		public static List<ChannelGroupType> GroupType = new List<ChannelGroupType>();
		public static List<ChannelMap> Map = new List<ChannelMap>();

		public LogoManagerPlugin() { } // don't remove this empty constructor - it is used by MP pluginLoader!
		public LogoManagerPlugin(string skinFile)
			: base(skinFile) { }
		#endregion

		#region ISetupForm Members
		public string PluginName()
		{
			return "LogoManager";
		}
		public string Description()
		{
			return "TV and radio logos manager for MediaPortal";
		}
		public string Author()
		{
			return "Vasilich, Edalex";
		}
		public void ShowPlugin()
		{
		}
		public bool CanEnable()
		{
			return true;
		}
		public int GetWindowId()
		{
			return 757278;
		}
		public bool DefaultEnabled()
		{
			return true;
		}
		public bool HasSetup()
		{
			return false;
		}
		public bool GetHome(out string strButtonText, out string strButtonImage,
		  out string strButtonImageFocus, out string strPictureImage)
		{
			strButtonText = PluginName();
			strButtonImage = String.Empty;
			strButtonImageFocus = String.Empty;
			strPictureImage = "hover_LogoManager.png";
			return true;
		}
		public override int GetID
		{ 
			get { return GetWindowId(); }
		}
		#endregion

		public override bool Init()
		{
			if (Thread.CurrentThread.Name == null)
				Thread.CurrentThread.Name = ThreadName;

			if (Argus.IsAvailable())
				TVService = new Argus();
			else if (TVE3.IsAvailable())
				TVService = new TVE3();
			else {
				Log.Error(ThreadName + ": No supported TV service providers found. Exiting...");
				return false;
			}
			Log.Info(ThreadName + ": Found and initialized supported TV service provider: \"{0}\". ", TVService.ReadableName());
			PluginSettings.Instance().LoadSetting();
			design = new Design(DesignsPath);
			//if (!string.IsNullOrEmpty(DesignType))
			//    design.Initialize(DesignType);
			GUIPropertyManager.SetProperty(Props.Names.PercentChannels, string.Empty);
			GUIPropertyManager.SetProperty(Props.Names.PercentGroups, string.Empty);
			updateTimer = new System.Timers.Timer(AutoUpdateInterval * 1000);
			updateTimer.Elapsed += updateTimer_Elapsed;
			updateTimer.Start();
			CheckForUpdate();
			return Load(GUIGraphicsContext.Skin + @"\LogoManager.xml");
		}

		protected override void OnPageLoad()
		{
			if (TVService == null) {
				GUIWindowManager.ShowPreviousWindow();
				var NoTVServiceReport = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
				NoTVServiceReport.SetHeading("LogoManager");
				NoTVServiceReport.SetText("No supported TV service providers found.\nPlugin cannot start.");
				NoTVServiceReport.TimeOut = 5;
				NoTVServiceReport.DoModal(GUIWindowManager.ActiveWindow);
				NoTVServiceReport.Dispose();
				return;
			}
			PluginSettings.Instance().InitProperties();
			ChannelsList.Clear();
			GUIPropertyManager.SetProperty("#currentmodule", "LogoManager");
			GUIPropertyManager.SetProperty("#thumb", "");
			GUIPropertyManager.SetProperty(Props.Names.State, IsGrabbingNow() ? Props.Values.CancelGrab : Props.Values.GrabNow);
			if (IsGrabbingNow()) {
				if (ChannelProgress != null)
					ChannelProgress.Visible = true;
				if (GroupProgress != null)
					GroupProgress.Visible = true;
			}
			base.OnPageLoad();
		}

		protected override void OnPageDestroy(int newWindowId)
		{
			if (TVService == null) return;
			PluginSettings.Instance().SaveSettings();
			if (PerSessionGeneratedLogos > 0)
			{
				GUIDialogYesNo yesno = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
				yesno.SetHeading("LogoManager");
				yesno.SetLine(1, "Rebuild skin cache?");
				yesno.SetLine(2, "This can take long time.");
				yesno.SetDefaultToYes(true);
				yesno.DoModal(GUIWindowManager.ActiveWindow);
				if (yesno.IsConfirmed)
					RebuildSkinCache();
				yesno.Dispose();
			}
			PerSessionGeneratedLogos = 0;
			base.OnPageDestroy(newWindowId);
		}

		#region GUI handling
		protected override void OnClicked(int controlId, GUIControl control,
		  MediaPortal.GUI.Library.Action.ActionType actionType)
		{
			if (control == buttonPackage)
				ChangePack();
			else if (control == buttonChannelGroup)
				SelectChannelGroups();
			else if (control == buttonDesign)
				ChangeDesign();
			else if (control == buttonGrab)
				StartStopGrabLogos();
			base.OnClicked(controlId, control, actionType);
		}

		void ChangePack()
		{
			GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
			if (dlg == null)
				return;

			dlg.Reset();
			dlg.SetHeading("Select Package");
			foreach (Package pack in PluginSettings.Instance().XMLConfig.Mapping.Packages)
			{
				GUIListItem item = new GUIListItem {
					Label = pack.Name,
					Label2 = pack.CountryName
				};
				dlg.Add(item);
			}
			dlg.DoModal(GUIWindowManager.ActiveWindow);

			if (Map.Count > 0)
			{
				GUIDialogYesNo ask = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
				if (ask == null)
					return;
				ask.Reset();
				ask.SetHeading("Saving Pack Dialog");
				ask.SetLine(1, "Do you want to save");
				ask.SetLine(2, "channel group selection");
				ask.SetLine(3, "for current pack?");

				ask.DoModal(GUIWindowManager.ActiveWindow);
				if (!ask.IsConfirmed)
				{
					Map.Clear();
					GUIPropertyManager.SetProperty(Props.Names.Group, Props.Values.Undefined);
				}
			}
			LastSelectedPackage = dlg.SelectedLabelText;
			GUIPropertyManager.SetProperty(Props.Names.Package, dlg.SelectedLabelText);
		}

		static void SelectChannelGroups()
		{
			IEnumerable<string> tvGroupNames = TVService.GetGroupNames(ChannelGroupType.TV);
			IEnumerable<string> radioGroupNames = TVService.GetGroupNames(ChannelGroupType.Radio);
			List<ChannelMap> currentGroups = Map.Where(m => m.Package == LastSelectedPackage).ToList();

			GUICheckListDialog dlg = (GUICheckListDialog)GUIWindowManager.GetWindow(GUICheckListDialog.ID);
			if (dlg == null)
				return;

			dlg.Reset();
			dlg.SetHeading("Select Channel Group");
			foreach (string groupName in tvGroupNames) {
				GUIListItem item = new GUIListItem {
					Label = groupName,
					Label2 = ChannelGroupType_TVString
				};

				if (currentGroups.Exists(m => m.ChannelGroup == groupName && m.ChannelType == ChannelGroupType.TV))
					item.Selected = true;
				item.AlbumInfoTag = ChannelGroupType.TV;
				dlg.Add(item);
			}
			foreach (string groupName in radioGroupNames) {
				GUIListItem item = new GUIListItem {
					Label = groupName,
					Label2 = ChannelGroupType_RadioString
				};
				if (currentGroups.Exists(m => m.ChannelGroup == groupName && m.ChannelType == ChannelGroupType.Radio))
					item.Selected = true;
				item.AlbumInfoTag = ChannelGroupType.Radio;
				dlg.Add(item);
			}
			dlg.DoModal(GUIWindowManager.ActiveWindow);

			Map.RemoveAll(p => p.Package == LastSelectedPackage);
			if (LastSelectedPackage != string.Empty)
			{
				foreach (GUIListItem i in dlg.ListItems)
				{
					if (i.Selected)
					{
						ChannelMap item = new ChannelMap {
							ChannelGroup = (dlg.ShowQuickNumbers ? i.Label.Substring(i.Label.IndexOf(" ") + 1) : i.Label),
							ChannelType = (ChannelGroupType)(i.AlbumInfoTag),
							Package = LastSelectedPackage
						};
						Map.Add(item);
					}
				}
			}

			GUIPropertyManager.SetProperty(Props.Names.Group, (Map.Count > 1 ? Props.Values.Multiple : (Map.Count == 1 ? Map[0].ChannelGroup : Props.Values.Undefined)));
		}

		void ChangeDesign()
		{
			GUIDialogPreview dlg = (GUIDialogPreview)GUIWindowManager.GetWindow(GUIDialogPreview.ID);
			if (dlg == null)
				return;

			DirectoryInfo[] designs = new DirectoryInfo(DesignsPath).GetDirectories();

			dlg.Reset();
			dlg.SetHeading("Select Layer Design");
			//dlg.ShowQuickNumbers = false;
			foreach (DirectoryInfo dir in designs) {
				GUIListItem item = new GUIListItem {
					Label = dir.Name,
					IconImage = dir.FullName + @"\sample.png"
				};
				//item.PinImage = dir.FullName + @"\sample.png";
				item.OnItemSelected += OnDesignChange;
				dlg.Add(item);
			}
			dlg.PreviewFilename = designs[0].FullName + @"\sample.png";
			dlg.DoModal(GUIWindowManager.ActiveWindow);
			DesignType = dlg.SelectedLabelText;
			GUIPropertyManager.SetProperty(Props.Names.Design, dlg.SelectedLabelText);
		}

		void OnDesignChange(GUIListItem item, GUIControl parent)
		{
			GUIDialogPreview dlg = (GUIDialogPreview)GUIWindowManager.GetWindow(GUIDialogPreview.ID);
			dlg.PreviewFilename = item.IconImage;
		}
		#endregion

		#region grab logos
		private void StartStopGrabLogos()
		{
			if (IsGrabbingNow()) {
				GrabLogoBGWorker.CancelAsync();
			}
			else if ((FirstRunBGWorker != null) && (FirstRunBGWorker.IsBusy)) {
				FirstRunBGWorker.CancelAsync();
			}
			else {
				IsBackgroundGrab = false;
				ThreadedGrabLogos(0);
			}
		}

		void ThreadedGrabLogos(int delay)
		{
			if (!IsBackgroundGrab) {
				GUIWaitCursor.Init();
				GUIWaitCursor.Show();
				if (ChannelProgress != null) {
					ChannelProgress.Percentage = 0;
					ChannelProgress.Visible = true;
				}
				if (GroupProgress != null) {
					GroupProgress.Percentage = 0;
					GroupProgress.Visible = true;
				}
			}

			GrabLogoBGWorker = new BackgroundWorker {
				WorkerSupportsCancellation = true,
				WorkerReportsProgress = true
			};
			GrabLogoBGWorker.DoWork += gl_DoWork;
			GrabLogoBGWorker.ProgressChanged += gl_ProgressChanged;
			GrabLogoBGWorker.RunWorkerCompleted += gl_RunWorkerCompleted;
			GUIPropertyManager.SetProperty(Props.Names.State, Props.Values.CancelGrab);
			GrabLogoBGWorker.RunWorkerAsync(delay);
			GrabLogoBGWorker.Dispose();
		}

		public bool IsGrabbingNow()
		{
			return (GrabLogoBGWorker != null) && (GrabLogoBGWorker.IsBusy);
		}
		private void gl_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (!IsBackgroundGrab) {
				if (ChannelProgress != null)
					ChannelProgress.Percentage = e.ProgressPercentage & 0xFF;
				if (GroupProgress != null)
					GroupProgress.Percentage = (e.ProgressPercentage >> 8) & 0xFF;
				var strings = (List<string>) e.UserState;
				GUIPropertyManager.SetProperty(Props.Names.PercentChannels, strings[0]);
				GUIPropertyManager.SetProperty(Props.Names.PercentGroups, strings[1]);
			 }
		}

		void gl_DoWork(object sender, DoWorkEventArgs e)
		{
			Thread.CurrentThread.Name = ThreadName + " Grabber";
			Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
			GrabLogos(e);
		}

		void gl_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (!IsBackgroundGrab) {
				GUIWaitCursor.Hide();
				GUIWaitCursor.Dispose();
				if (ChannelProgress != null)
					ChannelProgress.Visible = false;
				if (GroupProgress != null)
					GroupProgress.Visible = false;
				GUIPropertyManager.SetProperty(Props.Names.PercentChannels, string.Empty);
				GUIPropertyManager.SetProperty(Props.Names.PercentGroups, string.Empty);
			}

			string wasCancelled = string.Empty;
			if (e.Cancelled) {
				wasCancelled = "Grabbing logos was cancelled.";
				Log.Warn(ThreadName + ": " + wasCancelled);
				wasCancelled += "\n";
			}
			Log.Info(ThreadName + ": Generated {0} logos for groups \"{1}\" from \"{2}\" using \"{3}\" design. "
					+ "{4} channel names were not found in packages, "
					+ "failed to generate {5} logos, "
					+ "skipped: {6}",
					LastGeneratedLogos,
					String.Join("|", Map.Select(g => g.ChannelGroup).ToArray()),
					String.Join("|", Map.Select(p => p.Package).ToArray()), DesignType,
					LastNotMappedLogos,
					LastFailedLogos,
					ChannelsWithSameName);

			if (!(IsBackgroundGrab && e.Cancelled)) {
				GUIDialogNotify SummaryReport = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
				SummaryReport.SetHeading("LogoManager");
				SummaryReport.SetText(string.Format(wasCancelled + "Logos generated: {0}\n"
													+ "Channels not in package(s): {1}.",
													LastGeneratedLogos, LastNotMappedLogos));
				SummaryReport.TimeOut = 5;
				SummaryReport.RememberLastFocusedControl = true;
				SummaryReport.DoModal(GUIWindowManager.ActiveWindow);
				SummaryReport.Dispose();

				if (e.Cancelled) {
					if (AutoUpdateInterval > ((long) (DateTime.UtcNow - DateStartsFrom).TotalSeconds - LastGrab))
						RescheduleUpdateTimer(AutoUpdateInterval - ((long) (DateTime.UtcNow - DateStartsFrom).TotalSeconds - LastGrab) + 1);
					else
						RescheduleUpdateTimer(30*60); // user triggered grab was cancelled, but update interval was reached. reschedule it 30 minutes later
				}
				
				else {
					LastGrab = Convert.ToInt64((DateTime.UtcNow - DateStartsFrom).TotalSeconds);
					PluginSettings.Instance().SaveSettings();
					RescheduleUpdateTimer(AutoUpdateInterval);
				}
			}
			else
				RescheduleUpdateTimer(30*60); // background grab can be cancelled only from fullscreen. reschedule it 30 minutes later

			GUIPropertyManager.SetProperty(Props.Names.State, Props.Values.GrabNow);
		}

		private void GrabLogos(DoWorkEventArgs workerArgs)
		{
			if (GUIGraphicsContext.IsFullScreenVideo/* || GUIGraphicsContext.IsPlayingVideo*/) {
				workerArgs.Cancel = true;
				return;
			}
			//Thread.Sleep((int) WorkerArgs.Argument * 1000);
			for(int i = 0; i < (int) workerArgs.Argument * 5; i++) {
				Thread.Sleep(200);
				if (GrabLogoBGWorker.CancellationPending) {
					workerArgs.Cancel = true;
					return;
				}
			}

			Log.Info("Start grabbing logos");

			design.Initialize(DesignType);
			int currentGrabGroupIndex = -1;
			int grabGroupsCount = Map.Count;
			var progressSrings = new List<string> {"", ""};
			LastGeneratedLogos = 0;
			LastNotMappedLogos = 0;
			LastFailedLogos = 0;
			ChannelsWithSameName = 0;
			List<string> downloadedFiles = new List<string>();
			List<string> generatedLogos = new List<string>();
			WebClient wc = new WebClient();
			ServicePointManager.ServerCertificateValidationCallback += (RemoteCertificateValidationCallback)((s, ce, ch, ssl) => true);
			ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
			if (!IsBackgroundGrab)
				ChannelsList.Clear();
			var packListToScan = Map.Select(p => p.Package).Distinct().ToList();
			foreach (string pack in packListToScan) {
				if (string.IsNullOrEmpty(pack))
					break;
				if (!LoadPackage(pack))
					break;
				List<ChannelMap> matchedGroups = Map.Where(m => m.Package == pack).ToList();
				//var result = Enumerable.Range(0, Packages.Count).Where(i => Packages[i] == pack).ToList();
				//List<string> MatchedGroups = Groups.Where((element, index) => result.Contains(index)).ToList();
				//List<ChannelGroupType> MatchedGroupTypes = GroupType.Where((element, index) => result.Contains(index)).ToList();
				foreach (ChannelMap matchedGroup in matchedGroups) {
					string selectedGroup = matchedGroup.ChannelGroup;
					ChannelGroupType selectedType = matchedGroup.ChannelType;

					// progressbar groups
					currentGrabGroupIndex++;
					progressSrings[0] = string.Format("Pack \"{0}\" ({1}/{2})", pack, packListToScan.IndexOf(pack)+1, packListToScan.Count);
					progressSrings[1] = string.Format("Group \"{0}\" ({1}/{2})", selectedGroup, matchedGroups.IndexOf(matchedGroup)+1, matchedGroups.Count);
					int groupPercent = 100 * currentGrabGroupIndex / grabGroupsCount;
					GrabLogoBGWorker.ReportProgress((groupPercent << 8) + 0, progressSrings);

					string pathToLocalSources = (selectedType == ChannelGroupType.TV
						? TVSourcesPath
						: RadioSourcesPath)
												+ pack + '\\';
					if (!Directory.Exists(pathToLocalSources))
						Directory.CreateDirectory(pathToLocalSources);
					//string SelectedDesign = string.Format(@"{0}{1}\",DesignsPath, DesignType);
					var lmGroup = TVService.GetGroupForGroupNameAndType(selectedGroup, selectedType);
					foreach (LMChannel ch in lmGroup.Channels) {
						if (GrabLogoBGWorker != null) {
							if (GrabLogoBGWorker.CancellationPending) {
								workerArgs.Cancel = true;
								wc.Dispose();
								return;
							}
							// progressbar channels inside group
							int channelPercent = 100 * lmGroup.Channels.IndexOf(ch) / lmGroup.Channels.Count;
							groupPercent = 100 * currentGrabGroupIndex / grabGroupsCount;
							GrabLogoBGWorker.ReportProgress((groupPercent << 8) + channelPercent, progressSrings);
						}
						string remoteFileName = MatchChannel(ch);
						if (!String.IsNullOrEmpty(remoteFileName)) {
							string remoteLogo = string.Format(@"{0}{1}", (selectedType == ChannelGroupType.Radio)
								? RepositoryRadioPath
								: RepositoryTVPath,
								Uri.EscapeUriString(remoteFileName));
							string clearName = MediaPortal.Util.Utils.MakeFileName(ch.Name);
							string downloadedLogo = pathToLocalSources + remoteFileName;
							string localLogo = (selectedType == ChannelGroupType.TV ? TVLogosPath : RadioLogosPath) + clearName + ".png";
							string mpLogo = (selectedType == ChannelGroupType.TV ? TVLogosBase : RadioLogosBase) + clearName + ".png";

							if (!downloadedFiles.Contains(remoteLogo)) {
								bool logoModified = true;
								if (File.Exists(downloadedLogo) && SupportsLastModified) {
									HttpWebRequest request = (HttpWebRequest) WebRequest.Create(remoteLogo);
									request.Credentials = (remoteLogo.Contains("assembla") ? new NetworkCredential("guest", "guest")  : CredentialCache.DefaultCredentials);
									request.IfModifiedSince = File.GetLastWriteTime(downloadedLogo);
									try {
										request.GetResponse();
										//HttpWebResponse response = (HttpWebResponse) request.GetResponse();
										//string lastModified = response.Headers["Last-Modified"].ToString();
									} catch (WebException e) {
										if (e.Response != null) {
											if (((HttpWebResponse) e.Response).StatusCode == HttpStatusCode.NotModified) {
												logoModified = false;
												Log.Debug("Server response for remote file \"{0}\": Not modified since {1}", remoteLogo, request.IfModifiedSince);
											} else
												Log.Error("Can't retrieve information for remote file \"{0}\": {1}", remoteLogo, e.Message);
										}
									}
								}

								if (!File.Exists(downloadedLogo) || logoModified)
									try
									{
										if (!Directory.Exists(Path.GetDirectoryName(downloadedLogo)))
											Directory.CreateDirectory(Path.GetDirectoryName(downloadedLogo));
										wc.DownloadFile(remoteLogo, downloadedLogo);
									} catch (WebException e) {
										Log.Error("Can't download file \"{0}\" or save it locally to \"{1}\": {2}\n{3}",
											remoteLogo, downloadedLogo, e.Message, e.InnerException);
										LastFailedLogos++;
										continue;
									}
								downloadedFiles.Add(remoteLogo);
							}

							if (generatedLogos.Contains(localLogo)) {
								Log.Info("Skipped generating logo for the channel \"{0}\" (ID={1}) from group \"{2}\": "
										+ "logo for this name was already generated in this session.",
									ch.Name, ch.Id, selectedGroup);
								ChannelsWithSameName++;
								continue;
							}

							if (design.GenerateLogoForChannel(downloadedLogo, ch.Name, selectedType)) {
								generatedLogos.Add(localLogo);

								if (!IsBackgroundGrab) {
									GUIListItem item = new GUIListItem {
										Label = ch.Name,
										PinImage = downloadedLogo,
										IconImage = localLogo
									};

									ChannelsList.Add(item);
									ChannelsList.ScrollToEnd();
								}
								Log.Info("Generated logo for the {0} channel \"{1}\" (ID={2}) from remote file \"{3}\": \"{4}\".",
									ChannelGroupType_ToString(ch.Type), ch.Name, ch.Id, remoteFileName, mpLogo);
								LastGeneratedLogos++;
							} else {
								LastFailedLogos++;
							}
						} else {
							Log.Error("No mapping entries found for channel \"{0}\" and all its variations.", ch.Name);
							LastNotMappedLogos++;
						}
					}
				}
			}
			wc.Dispose();
			PerSessionGeneratedLogos += LastGeneratedLogos;
		}

		private bool LoadPackage(string packName)
		{
			if (!PluginSettings.Instance().LoadSelectedPack(packName)) {
				Log.Error("Unknown pack \"{0}\" or its URL", packName);
				return false;
			}
			Mapping_TVorCommon.Clear();
			Mapping_Radio.Clear();
			Mapping_TVorCommon_withReplacedParts.Clear();
			Mapping_Radio_withReplacedParts.Clear();
			XmlDocument doc = new XmlDocument();
			try
			{
				doc.Load(MappingURL);
			} catch (XmlException e) {
				Log.Error("Can't download or parse mapping file \"{0}\" of package \"{1}\": {2}", MappingURL, packName, e.Message);
				return false;
			}
			// TV or common
			XmlNodeList radioChannelList = doc.SelectNodes("//Radio//Channel");
			UseRadioMapping = (radioChannelList != null) && (radioChannelList.Count > 0);

			XmlNodeList channelList = doc.SelectNodes("//TV//Channel");
			if ((channelList.Count == 0) && !UseRadioMapping)
				channelList = doc.SelectNodes("//Channel");
			foreach (XmlNode node in channelList)
			{
				var fileNode = node["File"];
				if (fileNode != null)
				{
					string filename = fileNode.InnerText;
					foreach (XmlNode item in node.SelectNodes("Item"))
					{
						if (item.Attributes != null)
						{
							string name = item.Attributes["Name"].InnerText;
							if (!Mapping_TVorCommon.ContainsKey(name))
								Mapping_TVorCommon.Add(name, filename);
							else
								Log.Warn("Duplicate mapping for name \"{0}\" to files: \"{1}\", \"{2}\"", name, filename, Mapping_TVorCommon[name]);
						}
					}
				}
			}
			if (CurrentPackageHasStringPartsToreplace()) {
				foreach (var pair in Mapping_TVorCommon) {
					string newKey = ReplacePackageStringParts(pair.Key);
					string value;
					if (!Mapping_TVorCommon_withReplacedParts.TryGetValue(newKey, out value))
						Mapping_TVorCommon_withReplacedParts.Add(newKey, pair.Value);
				}
			}
			// Radio
			if (UseRadioMapping) {
				foreach (XmlNode node in radioChannelList)
				{
					var fileNode = node["File"];
					if (fileNode != null) {
						string filename = fileNode.InnerText;
						foreach (XmlNode item in node.SelectNodes("Item")) {
							string name = item.Attributes["Name"].InnerText;
							if (!Mapping_Radio.ContainsKey(name))
								Mapping_Radio.Add(name, filename);
							else
								Log.Warn("Duplicate radio mapping for name \"{0}\" to files: \"{1}\", \"{2}\"", name, filename, Mapping_Radio[name]);
						}
					}
				}
				if (CurrentPackageHasStringPartsToreplace()) {
					foreach (var pair in Mapping_Radio) {
						string newKey = ReplacePackageStringParts(pair.Key);
						string value;
						if (!Mapping_Radio_withReplacedParts.TryGetValue(newKey, out value))
							Mapping_Radio_withReplacedParts.Add(newKey, pair.Value);
					}
				}
			}

			Log.Info("Activated package \"{0}\": {1} unique channel names found, {2} strings to replace.",
				packName, Mapping_TVorCommon.Count, (StringsToRemove == null) ? 0 : StringsToRemove.Count);
			if (UseRadioMapping)
				Log.Info("Radio section: {0} unique channel names found.", Mapping_Radio.Count);
			return true;
		}

		private bool CurrentPackageHasStringPartsToreplace() {
			return (StringsToRemove != null) && (StringsToRemove.Count > 0);
		}

		private string ReplacePackageStringParts(string channelName)
		{
			if (CurrentPackageHasStringPartsToreplace()) {
				StringsToRemove.ForEach(str => channelName = channelName.Replace(str, string.Empty));
				return channelName;
			}
			return channelName;
		}

		private string MatchString(string channelName, bool isRadio)
		{
			if (string.IsNullOrEmpty(channelName))
				return null;
			string match;
			if (isRadio && UseRadioMapping) {
			  if (Mapping_Radio.TryGetValue(channelName, out match))
				return match;
			}
			else {
				if (Mapping_TVorCommon.TryGetValue(channelName, out match))
					return match;
			}
			Log.Debug("No mapping entries found for {0} channel \"{1}\".", StringForChannelType(isRadio), channelName);
			return null;
		}

		private string MatchStringWithReplacedParts(string channelName, bool isRadio)
		{
			if (string.IsNullOrEmpty(channelName))
				return null;
			string match;
			if (isRadio && UseRadioMapping) {
				if (Mapping_Radio_withReplacedParts.TryGetValue(channelName, out match))
					return match;
			} else {
				if (Mapping_TVorCommon_withReplacedParts.TryGetValue(channelName, out match))
					return match;
			}
			Log.Debug("No mapping entries found for {0} channel \"{1}\".", StringForChannelType(isRadio), channelName);
			return null;
		}

		private string MatchAllPossibleVariations(string nameToMatch, bool isRadio)
		{
			// direct compare
			string match = MatchString(nameToMatch, isRadio);
			if (string.IsNullOrEmpty(match)) {
				// compare with trimmed argument
				string trimmedNameToMatch = nameToMatch.Trim();
				if (nameToMatch != trimmedNameToMatch)
					match = MatchString(trimmedNameToMatch, isRadio);
			}
			if (string.IsNullOrEmpty(match) && CurrentPackageHasStringPartsToreplace()) {
				// compare with argument with removed string parts
				string nameWithReplacesPartsToMatch = ReplacePackageStringParts(nameToMatch.Trim());
				if (nameToMatch != nameWithReplacesPartsToMatch)
					match = MatchStringWithReplacedParts(nameWithReplacesPartsToMatch, isRadio);
			}
			return match;
		}

		private string MatchChannel(LMChannel channel) 
		{
			string displayName = channel.Name;
			string match = MatchAllPossibleVariations(displayName, channel.Type == ChannelGroupType.Radio);
			if (string.IsNullOrEmpty(match)) {
				// check all TuningDetials
				foreach (var tuningDetailName in channel.OriginalNamesList) {
					if (tuningDetailName != displayName) {
						match = MatchAllPossibleVariations(tuningDetailName, channel.Type == ChannelGroupType.Radio);
						if (!string.IsNullOrEmpty(match)) {
							Log.Debug("Mapping entry found for {0} channel \"{1}\" with TuningDetail \"{2}\".",
								ChannelGroupType_ToString(channel.Type), displayName, tuningDetailName);
							break;
						}
					}
				}
			}
			return match;
		}

		private static void RebuildSkinCache()
		{
			File.Delete(GUIGraphicsContext.SkinCacheFolder + @"\packedgfx2.bxml");
			GUITextureManager.Clear();
			GUITextureManager.Init();
			GUIWindowManager.OnResize();
		}

		#endregion

		#region download designs
		static void DownloadDesigns()
		{
			BackgroundWorker designDownloader = new BackgroundWorker();
			designDownloader.DoWork += GetAvailableDesigns;
			designDownloader.RunWorkerAsync();
			designDownloader.Dispose();
		}

		static void GetAvailableDesigns(object sender, DoWorkEventArgs ea)
		{
			Thread.CurrentThread.Name = ThreadName + " DesignsUpdater";
			Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

			int desingsDownloaded = 0;
			WebClient wc = new WebClient();
			ServicePointManager.ServerCertificateValidationCallback += (RemoteCertificateValidationCallback)((s, ce, ch, ssl) => true);
			ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
			string designFolder = PluginSettings.Instance().XMLConfig.Mapping.AltDesigns; 
			wc.Credentials = (designFolder.Contains("assembla") ? new NetworkCredential("guest", "guest") : CredentialCache.DefaultCredentials);
			List<string> alldesigns = new List<string>();
			string rawList;

			try
			{
				rawList = wc.DownloadString(designFolder);
			}
			catch (WebException e)
			{
				Log.Error("Can't download designs \"{0}\": {1}", designFolder, e.Message);
				wc.Dispose();
				return;
			}
			string regexTemplate = (designFolder.Contains("github") ? @"primary\"" title=\""(?<name>.*?)\""" : "<a href=\"(?<name>.*)\">(?<name>.*)</a>");
			MatchCollection matches = 
				Regex.Matches(rawList, regexTemplate);
			foreach (Match m in matches)
			{
				if (m.Value.IndexOf("..") == -1 && m.Value.IndexOf("Subversion") == -1)
					alldesigns.Add(m.Groups["name"].Value);
			}

			DirectoryInfo[] designs = new DirectoryInfo(DesignsPath).GetDirectories();
			List<string> localdesigns = designs.Select(e => e.Name).ToList();
			foreach (string remotedesign in alldesigns)
			{
				if (!localdesigns.Contains(remotedesign.Replace("/", "")))
				{
					Directory.CreateDirectory(DesignsPath + remotedesign.Replace("/", "")); 
					string designfiles = wc.DownloadString(designFolder + remotedesign);
					//"<a href=\"(?<name>.*)\">(?<name>.*)</a>"
					MatchCollection files = Regex.Matches(designfiles, regexTemplate);
					foreach (Match m in files)
					{
						if (m.Value.IndexOf("..") == -1 && m.Value.IndexOf("Subversion") == -1)
						{
							if (designFolder.Contains("github"))
							{
								wc.DownloadFile(designFolder.Replace("tree", "raw") + remotedesign.Replace("/", "") + "/" + m.Groups["name"].Value,
								DesignsPath + remotedesign.Replace("/", "") + @"\" + m.Groups["name"].Value);
							}
							else
							{
								wc.DownloadFile(designFolder + remotedesign.Replace("/", "") + "/" + m.Groups["name"].Value,
								DesignsPath + remotedesign.Replace("/", "") + @"\" + m.Groups["name"].Value);
							}
						}
					}
					desingsDownloaded++;
				}
			}
			wc.Dispose();
			if (desingsDownloaded > 0)
				Log.Info("LogoManager new designs downloaded: {0}", desingsDownloaded);
		}
		#endregion

		#region Update
		void CheckForUpdate()
		{
			TimeSpan t = DateTime.UtcNow - DateStartsFrom;
			if ((t.TotalSeconds - LastGrab > AutoUpdateInterval) || (LastGrab == 0)) {
				Log.Info(ThreadName + ": Checking update...");
				IsBackgroundGrab = true;
				PluginSettings.Instance().UpdatePackagesSettingsIfNecessary();
				DownloadDesigns();
				if ((LastGrab == 0) // first run
					&& (!string.IsNullOrEmpty(LastSelectedPackage)) // pack for current country found
					)
					AutomaticFirstRun(90);
				else {
					Log.Info(ThreadName + ": scheduled grabbing logos to {0:G}", DateTime.Now.AddSeconds(150));
					ThreadedGrabLogos(150);
				}
			}
			else
				RescheduleUpdateTimer(AutoUpdateInterval - ((long)t.TotalSeconds - LastGrab) + 1);
		}

		private void AutomaticFirstRun(int delay)
		{
			Log.Info(ThreadName + ": First run: trying to detect settings for autograb...");
			FirstRunBGWorker = new BackgroundWorker {
				WorkerSupportsCancellation = true
			};
			FirstRunBGWorker.DoWork += firstrun_DoWork;
			FirstRunBGWorker.RunWorkerCompleted += firstrun_RunWorkerCompleted;
			FirstRunBGWorker.RunWorkerAsync(delay);
			FirstRunBGWorker.Dispose();
		}

		void firstrun_DoWork(object sender, DoWorkEventArgs eArgs)
		{
			const int maxChannelCountForAutoGrab = 200;
			Thread.CurrentThread.Name = ThreadName + " FirstRun";
			Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

			//Thread.Sleep((int)eArgs.Argument * 1000);
			for(int i = 0; i < (int) eArgs.Argument * 5; i++) {
				Thread.Sleep(200);
				if (FirstRunBGWorker.CancellationPending) {
					eArgs.Cancel = true;
					return;
				}
			}

			Map.Clear();
			string notifyText;
			if (TVService.GetChannelCount() < maxChannelCountForAutoGrab) {
				Map.Add(new ChannelMap {
					ChannelGroup = TvLibrary.Interfaces.TvConstants.TvGroupNames.AllChannels,
					ChannelType = ChannelGroupType.TV,
					Package = LastSelectedPackage
				});
				Map.Add(new ChannelMap {
					ChannelGroup = TvLibrary.Interfaces.TvConstants.RadioGroupNames.AllChannels,
					ChannelType = ChannelGroupType.Radio,
					Package = LastSelectedPackage
				});
				notifyText = "Grab logos for group \"All channels\"";
				ThreadedGrabLogos(0);
			}
			else {
				int countOfChannelsInGroups = TVService.GetChannelCountForGroupsExceptAllChannels();
				if (countOfChannelsInGroups <= maxChannelCountForAutoGrab) {
					foreach (string groupName in TVService.GetGroupNames(ChannelGroupType.TV).Where(groupName => groupName != TvLibrary.Interfaces.TvConstants.TvGroupNames.AllChannels))
						Map.Add(new ChannelMap {
							ChannelGroup = groupName,
							ChannelType = ChannelGroupType.TV,
							Package = LastSelectedPackage
						});
					foreach (string groupName in TVService.GetGroupNames(ChannelGroupType.Radio).Where(groupName => groupName != TvLibrary.Interfaces.TvConstants.RadioGroupNames.AllChannels))
						Map.Add(new ChannelMap {
							ChannelGroup = groupName,
							ChannelType = ChannelGroupType.Radio,
							Package = LastSelectedPackage
						});
					notifyText = "Grab logos for all configured groups.";
					ThreadedGrabLogos(0);
				} 
				else {
					notifyText = "Too many channels for autograb. Configure LogoManager manually!";
					IsBackgroundGrab = false;
					GUIPropertyManager.SetProperty(Props.Names.State, Props.Values.GrabNow);
				}
			}
			Log.Info(notifyText);
			var firstRunNotify = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
				firstRunNotify.SetHeading("LogoManager first run");
				firstRunNotify.SetText(notifyText);
				firstRunNotify.TimeOut = 5;
				firstRunNotify.DoModal(GUIWindowManager.ActiveWindow);
				firstRunNotify.Dispose();
		}

		private void firstrun_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs eArgs)
		{
			if (eArgs.Cancelled)
				Log.Warn(ThreadName + ": First run was cancelled.");
			else
				Log.Info(ThreadName + ": First run finished.");

			GUIPropertyManager.SetProperty(Props.Names.State, Props.Values.GrabNow);
			if (PluginSettings.Instance().ConfigRecreated)
				PluginSettings.Instance().SaveSettings();
		}

		void updateTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			updateTimer.Stop();
			CheckForUpdate();
		}

		private void RescheduleUpdateTimer(long newInterval)
		{
			updateTimer.Stop();
			updateTimer.Interval = newInterval * 1000;
			updateTimer.Start();
			Log.Debug(ThreadName + ": Rescheduling next update to {0:G}", DateTime.Now.AddSeconds(newInterval));
		}

		#endregion
	}
}

