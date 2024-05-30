using System;
using System.Drawing;
using System.IO;
using System.Xml.XPath;
using MediaPortal.ServiceImplementations;

namespace LogoManager {
    class Design {
        public Design(string pathToDesignsFolder) {
            designsPath = pathToDesignsFolder;
        }
        private readonly Effects effects = new Effects();

        private const string SettingsFileName = "design.settings";
        private readonly string designsPath;
        private string designsDir;
        private string designName;
        //public string Name { get {return designName;} }
        public void Initialize(string newDesignName) {
            Log.Info("Loading design \"{0}\"", newDesignName);
            designName = newDesignName;
            effects.Glow.Init();
            effects.OuterGlow.Init();
            effects.Resize.Init();
            designsDir = designsPath + designName + "\\";
            if (File.Exists(designsDir + SettingsFileName)) {
                Log.Debug("Found settings file for design \"{0}\"", designName);
                try {
                    XPathDocument doc = new XPathDocument(designsDir + SettingsFileName);
                    XPathNavigator nav = doc.CreateNavigator();
                    XPathExpression expr = nav.Compile("/settings/effects//effect[@enabled='yes']|/settings/effects//effect[@enabled='true']");
                    XPathNodeIterator iterator = nav.Select(expr);
                    while (iterator.MoveNext()) {
                        string effectType = iterator.Current.GetAttribute("type", "");
                        Log.Debug("Found enabled {0} effect.", effectType);
                        if (effectType == "glow") {
                            effects.Glow.Enabled = true;
                            effects.Glow.SetRadius(iterator.Current.GetAttribute("radius", ""));
                            effects.Glow.SetColor(iterator.Current.GetAttribute("color", ""));
                            effects.Glow.SetThreshold(iterator.Current.GetAttribute("lightness_threshold", ""));
                            effects.Glow.SetActivePart(iterator.Current.GetAttribute("active_part", ""));
                        } else if (effectType == "outerglow") {
                            effects.OuterGlow.Enabled = true;
                            effects.OuterGlow.SetWidth(iterator.Current.GetAttribute("width", ""));
                            effects.OuterGlow.SetColor(iterator.Current.GetAttribute("color", ""));
                            effects.OuterGlow.SetTransparency(iterator.Current.GetAttribute("transparency", ""));
                            effects.OuterGlow.SetThreshold(iterator.Current.GetAttribute("lightness_threshold", ""));
                            effects.OuterGlow.SetActivePart(iterator.Current.GetAttribute("active_part", ""));
                        }
                        else if (effectType == "resize") {
                            effects.Resize.SetMinX(iterator.Current.GetAttribute("minX", ""));
                            effects.Resize.SetMaxX(iterator.Current.GetAttribute("maxX", ""));
                            effects.Resize.SetResizedX(iterator.Current.GetAttribute("resizedX", ""));
                            effects.Resize.SetMinY(iterator.Current.GetAttribute("minY", ""));
                            effects.Resize.SetMaxY(iterator.Current.GetAttribute("maxY", ""));
                            effects.Resize.SetResizedY(iterator.Current.GetAttribute("resizedY", ""));
                            effects.Resize.SetMinXY(iterator.Current.GetAttribute("minXY", ""));
                            effects.Resize.SetMaxXY(iterator.Current.GetAttribute("maxXY", ""));
                            effects.Resize.SetResizedXY(iterator.Current.GetAttribute("resizedXY", ""));
                        }
                    }
                } catch(Exception ex) {
                    Log.Error("Error by parsing design.settings for design {0}: {1}", designName, ex.Message);
                }
            }
        }

        public bool GenerateLogoForChannel(string logoFileName, string channelName, ChannelGroupType groupType) 
        {
            try {
                string channelLogoFileName = MediaPortal.Util.Utils.MakeFileName(channelName);
                if (File.Exists(designsDir + "background.png") && File.Exists(designsDir + "overlay.png")) {
                    Bitmap backgnd = new Bitmap(designsDir + "background.png");
                    backgnd.SetResolution(96, 96);
                    Bitmap overlay = new Bitmap(designsDir + "overlay.png");
                    overlay.SetResolution(96, 96);
                    Bitmap origLogo = new Bitmap(logoFileName);
                    origLogo.SetResolution(96, 96);
                    Bitmap logo;
                    if (effects.Resize.Enabled && effects.Resize.NeedToResize(origLogo.Size))
                        logo = effects.Resize.Apply(origLogo);
                    else
                        logo = (Bitmap) origLogo.Clone();
                    logo.SetResolution(96, 96);
                    origLogo.Dispose();

                    Graphics graphics = Graphics.FromImage(backgnd);
                    graphics.PageUnit = GraphicsUnit.Pixel;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    if (effects.Glow.Enabled)
                        effects.Glow.Apply(graphics, logo, logoFileName); // first - glow
                    if (effects.OuterGlow.Enabled)
                        effects.OuterGlow.Apply(graphics, logo, logoFileName); // then - outerglow

                    graphics.DrawImage(logo, (backgnd.Width/2) - (logo.Width/2), (backgnd.Height/2) - (logo.Height/2), logo.Width, logo.Height);
                    graphics.DrawImage(overlay, new Point(0, 0));
                    backgnd.Save((groupType == ChannelGroupType.Radio ? LogoManagerPlugin.RadioLogosPath : LogoManagerPlugin.TVLogosPath) + channelLogoFileName + ".png", System.Drawing.Imaging.ImageFormat.Png);
                    backgnd.Save((groupType == ChannelGroupType.Radio ? LogoManagerPlugin.RadioLogosBase : LogoManagerPlugin.TVLogosBase) + channelLogoFileName + ".png", System.Drawing.Imaging.ImageFormat.Png);
                    graphics.Dispose();
                }
                else {
                    Log.Error("Can't generate logo from file \"{0}\": design \"{1}\" not found", logoFileName, designsDir);
                    return false;
                }
            } catch (Exception e) {
                Log.Error("Can't generate logo from file \"{0}\": {1}\n{2}", logoFileName, e.Message, e.StackTrace);
                return false;
            }
            return true;
        }

        /*
        public static Image PadImage(Image originalImage) {
            int largestDimension = Math.Max(originalImage.Height, originalImage.Width);
            Size squareSize = new Size(largestDimension, largestDimension);
            Bitmap squareImage = new Bitmap(squareSize.Width, squareSize.Height);
            using (Graphics graphics = Graphics.FromImage(squareImage)) {
                graphics.FillRectangle(Brushes.Transparent, 0, 0, squareSize.Width, squareSize.Height);
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                graphics.DrawImage(originalImage, (squareSize.Width / 2) - (originalImage.Width / 2), (squareSize.Height / 2) - (originalImage.Height / 2), originalImage.Width, originalImage.Height);
            }
            return squareImage;
        }
        */
    }
}
