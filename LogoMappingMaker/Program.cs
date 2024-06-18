using System.IO;
using System.Text;
using System.Xml;

namespace LogoMappingMaker
{
	static class Program
	{
		static void Main(string[] args)
		{
			string ProviderName = args.Length > 0 ? args[0] : null;
			var xmlTextWriter = new XmlTextWriter("LogoMapping.xml", Encoding.UTF8) {
				Formatting = Formatting.Indented,
				IndentChar = '\t',
				Indentation = 1
			};
			xmlTextWriter.WriteStartDocument(true);
			xmlTextWriter.WriteStartElement("Mappings");
			xmlTextWriter.WriteStartElement("TV");

			if (Directory.Exists(".\\TV")) {
				ProcessFolderContent(ProviderName, Directory.GetFiles(".\\TV", "*.png"), xmlTextWriter);
				xmlTextWriter.WriteEndElement();
				if (Directory.Exists(".\\Radio")) {
					xmlTextWriter.WriteStartElement("Radio");
					ProcessFolderContent(ProviderName, Directory.GetFiles(".\\Radio", "*.png"), xmlTextWriter);
					xmlTextWriter.WriteEndElement();
				}
			} else {
				ProcessFolderContent(ProviderName, Directory.GetFiles(".", "*.png"), xmlTextWriter);
				xmlTextWriter.WriteEndElement();
			}

			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
		}

		static void ProcessFolderContent(string ProviderName, string[] filePaths, XmlTextWriter xmlTextWriter)
		{
			foreach (var filePath in filePaths) {
				xmlTextWriter.WriteStartElement("Channel");
				xmlTextWriter.WriteStartElement("Item");
				xmlTextWriter.WriteStartAttribute("Name");
				xmlTextWriter.WriteString(Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
				xmlTextWriter.WriteEndAttribute();
				if (ProviderName != null) {
					xmlTextWriter.WriteStartElement("Provider");
					xmlTextWriter.WriteString(ProviderName);
					xmlTextWriter.WriteEndElement();
				}
				xmlTextWriter.WriteEndElement();
				xmlTextWriter.WriteStartElement("File");
				xmlTextWriter.WriteString(Path.GetFileName(filePath) ?? string.Empty);
				xmlTextWriter.WriteEndElement();
				xmlTextWriter.WriteEndElement();
			}
		}
	}
}
