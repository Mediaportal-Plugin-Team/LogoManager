using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using MediaPortal.GUI.Library;

namespace LogoManager {
	public class Effects {
		private enum ActivePart { undefined, above, below };
		public struct EffectfGlow {
			public bool Enabled;
			private Color color;
			private int radius;
			private bool useThreshold;
			private float threshold;
			private ActivePart activePart;
			
			public void Init() {
				Enabled = false;
				radius = 0;
				color = Color.White;
				threshold = 1;
			}
			#region setters
			public void SetRadius(string attr) {
				if (!Int32.TryParse(attr, out radius))
					radius = 1;
			}
			public void SetColor(string attr) {
				color = Color.FromName(attr);
				if (color.A == 0)
					color = Color.Silver;
			}
			public void SetThreshold(string attr) {
				if (!float.TryParse(attr, NumberStyles.Any, CultureInfo.InvariantCulture, out threshold))
					threshold = 1;
				SetUseThreshold();
			}
			public void SetActivePart(string attr) {
				if (String.Compare(attr, "below", StringComparison.OrdinalIgnoreCase) == 0)
					activePart = ActivePart.below;
				else if (String.Compare(attr, "above", StringComparison.OrdinalIgnoreCase) == 0)
					activePart = ActivePart.above;
				else
					activePart = ActivePart.undefined;
				SetUseThreshold();
			}
			private void SetUseThreshold() {
				useThreshold = ((activePart != ActivePart.undefined) && (threshold < 1));
			}
			#endregion

			public void Apply(Graphics graphics, Image logo, string logoFileName) {
				if (useThreshold) {
					bool needToApply = false;
					float imgBrightness = BitmapBrightness((Bitmap)logo);
					if (((activePart == ActivePart.above) && (imgBrightness > threshold)) ||
						((activePart == ActivePart.below) && (imgBrightness < threshold))) {
						needToApply = true;
						ApplyGlow(graphics, logo);
					}
					Log.Debug("Logo \"{0}\" has brightness = {1} -> Glow effect {2}applied.", logoFileName, imgBrightness, needToApply?"":"not ");
				} else {
					ApplyGlow(graphics, logo);
				}
			}
			private void ApplyGlow(Graphics graphics, Image logo) {
				Bitmap squareLogo = new Bitmap((int)graphics.VisibleClipBounds.Width, (int)graphics.VisibleClipBounds.Height);
				squareLogo.SetResolution(96, 96);
				squareLogo.MakeTransparent();
				Graphics g = Graphics.FromImage(squareLogo);
				g.DrawImage(logo, (squareLogo.Width / 2) - (logo.Width / 2), (squareLogo.Height / 2) - (logo.Height / 2), logo.Width, logo.Height);
				g.Dispose();
				ChangeColor(squareLogo, color);
				FastBlur(squareLogo, radius, color);
				FastBlur(squareLogo, radius, color);
				graphics.DrawImage(squareLogo, 0, 0);
				squareLogo.Dispose();
			}
		};
		
		public struct EffectfOuterGlow {
			public bool Enabled;
			private Color color;
			private int width;
			private float transparency;
			private bool useThreshold;
			private float threshold;
			private ActivePart activePart;

			public void Init() {
				Enabled = false;
				width = 0;
				color = Color.White;
				transparency = 1.0f;
				threshold = 1;
			}
			#region setters
			public void SetWidth(string attr) {
				if (!Int32.TryParse(attr, out width))
					width = 1;
			}
			public void SetColor(string attr) {
				color = Color.FromName(attr);
				if (color.A == 0)
					color = Color.Silver;
			}
			public void SetTransparency(string attr) {
				if (!float.TryParse(attr, NumberStyles.Any, CultureInfo.InvariantCulture, out transparency))
					transparency = 1.0f;
				SetUseThreshold();
			}
			public void SetThreshold(string attr) {
				if (!float.TryParse(attr, NumberStyles.Any, CultureInfo.InvariantCulture, out threshold))
					threshold = 1.0f;
				SetUseThreshold();
			}
			public void SetActivePart(string attr) {
				if (String.Compare(attr, "below", StringComparison.OrdinalIgnoreCase) == 0)
					activePart = ActivePart.below;
				else if (String.Compare(attr, "above", StringComparison.OrdinalIgnoreCase) == 0)
					activePart = ActivePart.above;
				else
					activePart = ActivePart.undefined;
				SetUseThreshold();
			}
			private void SetUseThreshold() {
				useThreshold = ((activePart != ActivePart.undefined) && (threshold < 1));
			}
			#endregion

			public void Apply(Graphics graphics, Image logo, string logoFileName) {
				if (useThreshold) {
					bool needToApply = false;
					float imgBrightness = BitmapBrightness((Bitmap)logo);
					if (((activePart == ActivePart.above) && (imgBrightness > threshold)) ||
						((activePart == ActivePart.below) && (imgBrightness < threshold))) {
						needToApply = true;
						ApplyOuterGlow(graphics, logo);
					}
					Log.Debug("Logo \"{0}\" has brightness = {1} -> OuterGlow effect {2}applied.", logoFileName, imgBrightness, needToApply?"":"not ");
				} else {
					ApplyOuterGlow(graphics, logo);
				}
			}
			private void ApplyOuterGlow(Graphics graphics, Image logo) {
				Bitmap squareLogo = new Bitmap((int)graphics.VisibleClipBounds.Width, (int)graphics.VisibleClipBounds.Height);
				squareLogo.SetResolution(96, 96);
				squareLogo.MakeTransparent();
				Graphics g = Graphics.FromImage(squareLogo);
				g.DrawImage(logo, (squareLogo.Width / 2) - (logo.Width / 2), (squareLogo.Height / 2) - (logo.Height / 2), logo.Width, logo.Height);
				g.Dispose();
				ChangeColor(squareLogo, color);
				squareLogo = Contour(squareLogo, width);
				if (width > 0)
					FastBlur(squareLogo, 1, color); // smooth outerglow
				ChangeOpacity(squareLogo, transparency);
				graphics.DrawImage(squareLogo, 0, 0);
				squareLogo.Dispose();
			}
		};

		public struct EffectResize {
			public bool Enabled;
			private int minX, maxX, resizedX;
			private int minY, maxY, resizedY;
			private Size sizeToSet;
			private bool definedX, definedY;

			public void Init() {
				Enabled = false;
				minX = maxX =  resizedX = 0;
				minY = maxY = resizedY = 0;
				definedX = definedY = false;
				sizeToSet = Size.Empty;
			}
			#region setters
			public void SetMinX(string attr) {
				if ((minX == 0) && (!Int32.TryParse(attr, out minX)))
					minX = 0;
				SetEnabled();
			}
			public void SetMaxX(string attr) {
				if ((maxX == 0) && (!Int32.TryParse(attr, out maxX)))
					maxX = 0;
				SetEnabled();
			}
			public void SetResizedX(string attr) {
				if ((resizedX == 0) && (!Int32.TryParse(attr, out resizedX)))
					resizedX = 0;
				SetEnabled();
			}

			public void SetMinY(string attr) {
				if ((minY == 0) && (!Int32.TryParse(attr, out minY)))
					minY = 0;
				SetEnabled();
			}
			public void SetMaxY(string attr) {
				if ((maxY == 0) && (!Int32.TryParse(attr, out maxY)))
					maxY = 0;
				SetEnabled();
			}
			public void SetResizedY(string attr) {
				if ((resizedY == 0) && (!Int32.TryParse(attr, out resizedY)))
					resizedY = 0;
				SetEnabled();
			}
			public void SetMinXY(string attr) {
				if (!string.IsNullOrEmpty(attr)) {
					SetMinX(attr);
					minY = minX;
					SetEnabled();
				}
			}
			public void SetMaxXY(string attr) {
				if (!string.IsNullOrEmpty(attr)) {
					SetMaxX(attr);
					maxY = maxX;
					SetEnabled();
				}
			}
			public void SetResizedXY(string attr) {
				if (!string.IsNullOrEmpty(attr)) {
					SetResizedX(attr);
					resizedY = resizedX;
					SetEnabled();
				}
			}
			#endregion

			private void SetEnabled() {
				definedX = (resizedX > 0) && ((minX > 0) || (maxX > 0));
				definedY = (resizedY > 0) && ((minY > 0) || (maxY > 0));
				Enabled = definedX || definedY;
			}
			public bool NeedToResize(Size imgSize) {
				CalculateNewSize(imgSize);
				return (imgSize != sizeToSet);
			}

			private void CalculateNewSize(Size imgSize) {
				Size requiredSize = new Size {
					Width = (definedX && ((imgSize.Width < minX) || (imgSize.Width > maxX))) ? resizedX : imgSize.Width,
					Height = (definedY && ((imgSize.Height < minY) || (imgSize.Height > maxY))) ? resizedY : imgSize.Height
				};
				const bool preserveAspectRatio = true;
				float percentWidth = (float)requiredSize.Width / (float)imgSize.Width;
				float percentHeight = (float)requiredSize.Height / (float)imgSize.Height;
				if (preserveAspectRatio) {
					float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
					sizeToSet.Width = (int)(imgSize.Width * percent);
					sizeToSet.Height = (int)(imgSize.Height * percent);
				//} else {
					//SizeToSet.Width = (int)(imgSize.Width * percentWidth);
					//SizeToSet.Height = (int)(imgSize.Height * percentHeight);
				}
			}

			public Bitmap Apply(Image img) {
				return ResizeImage(img, sizeToSet);
			}
			private static Bitmap ResizeImage(Image img, Size newSize) {
				Bitmap result = new Bitmap(newSize.Width, newSize.Height);
				result.SetResolution(96, 96);
				using (Graphics graphics = Graphics.FromImage(result)) {
					graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
					graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
					graphics.DrawImage(img, 0, 0, result.Width, result.Height);
				}
				return result;
			}
		}

		public EffectfGlow Glow;
		public EffectfOuterGlow OuterGlow;
		public EffectResize Resize;

		#region common graphic routines
		static void ChangeColor(Bitmap bmp, Color newColor) {
			Color pix;
			Color transp = Color.FromArgb(0);
			for (int x = 0; x < bmp.Width; x++) {
				for (int y = 0; y < bmp.Height; y++)
				{
					pix = bmp.GetPixel(x, y);
					bmp.SetPixel(x, y, pix.A > 0
										? Color.FromArgb(pix.A, newColor.R, newColor.G, newColor.B)
										: transp);
				}
			}
		}

		private static void FastBlur(Bitmap sourceImage, int radius, Color forcedColor) {
			// http://snippetsfor.net/Csharp/StackBlur (adopted for transparency support by Vasilich)
			// Two important aspects of the method in terms of speed are
			// A look-up table for figuring the average from a sum (this is the array 'dv[]')
			// A running-sum for averaging -- this is pre-populated, then pixels are added from the right of the radius and subtracted from the left.
			var rct = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
			var dest = new int[rct.Width * rct.Height];
			var source = new int[rct.Width * rct.Height];
			var bits = sourceImage.LockBits(rct, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			Marshal.Copy(bits.Scan0, source, 0, source.Length);
			sourceImage.UnlockBits(bits);

			if (radius < 1)
				return;

			int w = rct.Width;
			int h = rct.Height;
			int wm = w - 1;
			int hm = h - 1;
			int wh = w * h;
			int div = radius + radius + 1;
			var a = new int[wh];
			var r = new int[wh];
			var g = new int[wh];
			var b = new int[wh];
			long rsum, gsum, bsum, asum;
			int x, y, i, p1, p2, yi;
			var vmin = new int[Math.Max(w, h)];
			var vmax = new int[Math.Max(w, h)];

			var dv = new int[256 * div];
			for (i = 0; i < 256 * div; i++) {
				dv[i] = (i / div);
			}

			int yw = yi = 0;

			for (y = 0; y < h; y++) { // blur horizontal
				rsum = gsum = bsum = asum = 0;
				for (i = -radius; i <= radius; i++) {
					int p = source[yi + Math.Min(wm, Math.Max(i, 0))];
					asum += (p & 0xFF000000) >> 24;
					rsum += (p & 0x00ff0000) >> 16;
					gsum += (p & 0x0000ff00) >> 8;
					bsum += (p & 0x000000ff);
				}
				for (x = 0; x < w; x++) {
					a[yi] = dv[asum];
					r[yi] = dv[rsum];
					g[yi] = dv[gsum];
					b[yi] = dv[bsum];

					if (y == 0) {
						vmin[x] = Math.Min(x + radius + 1, wm);
						vmax[x] = Math.Max(x - radius, 0);
					}
					p1 = source[yw + vmin[x]];
					p2 = source[yw + vmax[x]];

					asum += ((p1 & 0xff000000) - (p2 & 0xff000000)) >> 24;
					rsum += ((p1 & 0x00ff0000) - (p2 & 0x00ff0000)) >> 16;
					gsum += ((p1 & 0x0000ff00) - (p2 & 0x0000ff00)) >> 8;
					bsum += ((p1 & 0x000000ff) - (p2 & 0x000000ff));
					yi++;
				}
				yw += w;
			}

			for (x = 0; x < w; x++) { // blur vertical
				rsum = gsum = bsum = asum = 0;
				int yp = -radius * w;
				for (i = -radius; i <= radius; i++) {
					yi = Math.Max(0, yp) + x;
					asum += a[yi];
					rsum += r[yi];
					gsum += g[yi];
					bsum += b[yi];
					yp += w;
				}
				yi = x;
				for (y = 0; y < h; y++) {
					if (forcedColor.IsEmpty)
						dest[yi] = (int)((uint)(dv[asum] << 24) | (uint)(dv[rsum] << 16) | (uint)(dv[gsum] << 8) | (uint)dv[bsum]);
					else
						dest[yi] = (int)((uint)(dv[asum] << 24) | (uint)(forcedColor.R << 16) | (uint)(forcedColor.G << 8) | (uint)forcedColor.B);

					if (x == 0) {
						vmin[y] = Math.Min(y + radius + 1, hm) * w;
						vmax[y] = Math.Max(y - radius, 0) * w;
					}
					p1 = x + vmin[y];
					p2 = x + vmax[y];

					asum += a[p1] - a[p2];
					rsum += r[p1] - r[p2];
					gsum += g[p1] - g[p2];
					bsum += b[p1] - b[p2];

					yi += w;
				}
			}

			// copy back to image
			var bits2 = sourceImage.LockBits(rct, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			Marshal.Copy(dest, 0, bits2.Scan0, dest.Length);
			sourceImage.UnlockBits(bits);
		}

		private static float BitmapBrightness(Bitmap bmp) {
			Color pix;
			double brightnessSum = 0.0d;
			int nonTranspPxelCount = 0;
			for (int x = 0; x < bmp.Width; x++) {
				for (int y = 0; y < bmp.Height; y++) {
					pix = bmp.GetPixel(x, y);
					if (pix.A > 127) {
						brightnessSum += pix.GetBrightness();
						nonTranspPxelCount++;
					}
				}
			}
			return (float)(brightnessSum / nonTranspPxelCount);
		}

		private static void ChangeOpacity(Bitmap bmp, float aMultiplier) {
			Color pix;
			for (int x = 0; x < bmp.Width; x++) {
				for (int y = 0; y < bmp.Height; y++) {
					pix = bmp.GetPixel(x, y);
					if ((pix.A > 0))
						bmp.SetPixel(x, y, Color.FromArgb((int)(pix.A * aMultiplier), pix.R, pix.G, pix.B));
				}
			}
		}

		private static Bitmap Contour(Bitmap srcImage, int contourWidth) {
			Bitmap newBmp = new Bitmap(srcImage.Width + 2 * contourWidth, srcImage.Height + 2 * contourWidth);
			newBmp.SetResolution(96, 96);
			if (contourWidth > 0) {
				using (Graphics g = Graphics.FromImage(newBmp)) {
					for (int x = 0; x <= contourWidth * 2; x++) {
						for (int y = 0; y <= contourWidth * 2; y++) {
							g.DrawImage(srcImage, x, y);
						}
					}
				}
			}
			return newBmp.Clone(new Rectangle(contourWidth, contourWidth, srcImage.Width, srcImage.Height), srcImage.PixelFormat);
		}
		#endregion
	}
}
