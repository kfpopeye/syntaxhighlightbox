using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Diagnostics.Contracts;

namespace AurelienRibon.Ui.CodeBox {
	public class Highlighter {
		private static Dictionary<String, Highlighter> dictionary = new Dictionary<String, Highlighter>();
		private static bool isInitialized = false;

		public static List<String> GetSyntaxes() {
			return dictionary.Keys.ToList<String>();
		}

		public static Highlighter GetHighlighter(String syntaxName) {
			if (dictionary.ContainsKey(syntaxName))
				return dictionary[syntaxName];
			return null;
		}

		public static void Initialize() {
			if (isInitialized)
				return;

			Debug.WriteLine("--------------------------------------------------");
			Debug.WriteLine("-- codebox.Highlighter initilization");
			Debug.WriteLine("--------------------------------------------------");

			Assembly assembly = Assembly.GetExecutingAssembly();
			String[] resources = assembly.GetManifestResourceNames();

			XmlSchema schema = null;

			foreach (String res in resources) {
				if (Regex.IsMatch(res, ".*?syntaxes\\.syntax\\.xsd$")) {
					Stream stream = assembly.GetManifestResourceStream(res);
					schema = XmlSchema.Read(stream, (ValidationEventHandler)delegate(object sender, ValidationEventArgs e) {
						Debug.WriteLine("Xml schema validation error : " + e.Message);
						return;
					});
				}
			}

			if (schema == null) {
				Debug.WriteLine("No xml schema found.");
				return;
			}

			XmlReaderSettings readerSettings = new XmlReaderSettings();
			readerSettings.DtdProcessing = DtdProcessing.Parse;
			readerSettings.Schemas.Add(schema);
			readerSettings.ValidationType = ValidationType.Schema;

			foreach (String res in resources) {
				if (Regex.IsMatch(res, ".*?syntaxes\\.\\w+?\\.xml$")) {
					Stream stream = assembly.GetManifestResourceStream(res);
					Contract.Assert(stream != null);
					XmlDocument xmldoc = null;
					try {
						XmlReader reader = XmlReader.Create(stream, readerSettings);
						xmldoc = new XmlDocument();
						xmldoc.Load(reader);
					} catch (XmlSchemaValidationException ex) {
						Debug.WriteLine("Xml validation error at line " + ex.LineNumber + " for " + res + " :");
						Debug.WriteLine("Warning : if you cannot find the issue in the xml file, verify the xsd file.");
						Debug.WriteLine(ex.Message);
						return;
					} catch (Exception ex) {
						Debug.WriteLine(ex.Message);
						return;
					}

					Contract.Assert(xmldoc != null);
					XmlElement syntaxElement = xmldoc.DocumentElement;
					String name = syntaxElement.Attributes["name"].InnerText;
					dictionary.Add(name, new Highlighter(syntaxElement, name));
					Debug.WriteLine("Found valid syntax (named " + name + ") : " + res);
				}
			}

			isInitialized = true;
			Debug.WriteLine("--------------------------------------------------");
		}

		// ---------------------------------------------
		// Rule parsing
		// ---------------------------------------------

		private String syntaxName = null;
		private List<Tuple> tuples = new List<Tuple>();
		private Dictionary<Regex, Tuple> regexes = new Dictionary<Regex, Tuple>();

		private Highlighter(XmlElement syntaxElement, String syntaxName) {
			Contract.Requires(syntaxElement != null);
			Contract.Requires(syntaxElement.LocalName.Equals("Syntax"));
			Contract.Requires(syntaxElement.Attributes["name"].InnerText.Equals(syntaxName));
			Contract.Requires(!String.IsNullOrEmpty(syntaxName));
			this.syntaxName = syntaxName;
			ParseSyntax(syntaxElement);
			InitiliazeRegexes();
		}

		public String Name {
			get { return syntaxName; }
		}

		[ContractInvariantMethod]
		private void Invariant() {
			Contract.Invariant(regexes != null);
			Contract.Invariant(tuples != null);
			Contract.Invariant(!String.IsNullOrEmpty(syntaxName));
		}

		private void ParseSyntax(XmlElement syntaxElement) {
			XmlNodeList rules = syntaxElement.SelectNodes("./HighlightWordsRule");
			foreach (XmlElement rule in rules) {
				ParseHighlightWordsRule(rule);
			}

			rules = syntaxElement.SelectNodes("./AdvancedHighlightRule");
			foreach (XmlElement rule in rules) {
				ParseAdvancedHighlightRule(rule);
			}

			rules = syntaxElement.SelectNodes("./HighlightLineRule");
			foreach (XmlElement rule in rules) {
				ParseHighlightLineRule(rule);
			}
		}

		private void ParseHighlightWordsRule(XmlElement rule) {
			Contract.Requires(rule != null);
			Contract.Requires("HighlightWordsRule".Equals(rule.LocalName));

			Decoration decoration = new Decoration(rule);
			Options options = new Options(rule);
			String[] words = rule.SelectSingleNode("./Words").InnerText
				.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (String word in words) {
				Tuple tuple = new Tuple("\\b(" + Regex.Escape(word.Trim()) + ")\\b", options, decoration);
				tuples.Add(tuple);
			}
		}

		private void ParseHighlightLineRule(XmlElement rule) {
			Contract.Requires(rule != null);
			Contract.Requires("HighlightLineRule".Equals(rule.LocalName));

			Decoration decoration = new Decoration(rule);
			Options options = new Options(rule);
			String lineStart = rule.SelectSingleNode("./LineStart").InnerText;
			Tuple tuple = new Tuple("(" + Regex.Escape(lineStart.Trim()) + ".*)", options, decoration);
			tuples.Add(tuple);
		}

		private void ParseAdvancedHighlightRule(XmlElement rule) {
			Contract.Requires(rule != null);
			Contract.Requires("AdvancedHighlightRule".Equals(rule.LocalName));

			Decoration decoration = new Decoration(rule);
			Options options = new Options(rule);
			String expression = rule.SelectSingleNode("./Expression").InnerText;
			Tuple tuple = new Tuple(expression, options, decoration);
			tuples.Add(tuple);
		}

		private void InitiliazeRegexes() {
			Contract.Ensures(regexes.Count == tuples.Count);

			foreach (Tuple tuple in tuples) {
				RegexOptions ro = RegexOptions.Multiline | RegexOptions.Compiled;
				ro = tuple.Options.IgnoreCase ? ro | RegexOptions.IgnoreCase : ro;
				Regex regex = new Regex(tuple.Pattern, ro);
				regexes.Add(regex, tuple);
			}
		}

		// ---------------------------------------------
		// Highlight
		// ---------------------------------------------

		public FormattedText Highlight(FormattedText text) {
			FormattedText ret = text;
			foreach (Regex regex in regexes.Keys) {
				Tuple tuple = regexes[regex];
				Match m = regex.Match(ret.Text);
				while (m.Success) {
					ret.SetForegroundBrush(tuple.Decoration.Foreground, m.Groups[1].Index, m.Groups[1].Length);
					ret.SetFontWeight(tuple.Decoration.FontWeight, m.Groups[1].Index, m.Groups[1].Length);
					ret.SetFontStyle(tuple.Decoration.FontStyle, m.Groups[1].Index, m.Groups[1].Length);
					m = m.NextMatch();
				}
			}
			return ret;
		}

		// ---------------------------------------------
		// Tuple
		// ---------------------------------------------

		private class Tuple {
			private String pattern = null;
			private Options options = null;
			private Decoration decoration = null;

			public Tuple(String pattern, Options options, Decoration decoration) {
				this.pattern = pattern;
				this.options = options;
				this.decoration = decoration;
			}

			public String Pattern {
				get { return pattern; }
			}

			public Options Options {
				get { return options; }
			}

			public Decoration Decoration {
				get { return decoration; }
			}

			[ContractInvariantMethod]
			private void Invariant() {
				Contract.Invariant(!String.IsNullOrEmpty(pattern));
				Contract.Invariant(options != null);
				Contract.Invariant(decoration != null);
			}
		}

		// ---------------------------------------------
		// Options
		// ---------------------------------------------

		private class Options {
			private bool ignoreCase = false;

			public bool IgnoreCase {
				get { return ignoreCase; }
				set { ignoreCase = value; }
			}

			public Options(XmlElement rule) {
				Contract.Requires(rule != null);
				FindAndParseIgnoreCase(rule);
			}

			private void FindAndParseIgnoreCase(XmlElement rule) {
				Contract.Requires(rule != null);
				XmlNode elem = rule.SelectSingleNode("./IgnoreCase");
				if (elem != null)
					Boolean.TryParse(elem.InnerText, out ignoreCase);
			}
		}

		// ---------------------------------------------
		// Decorations
		// ---------------------------------------------

		private class Decoration {
			private Brush foreground = Brushes.Black;
			private FontWeight fontWeight = FontWeights.Normal;
			private FontStyle fontStyle = FontStyles.Normal;

			public Brush Foreground {
				get { return foreground; }
				set { if (value != null) foreground = value; }
			}

			public FontWeight FontWeight {
				get { return fontWeight; }
				set { if (value != null) fontWeight = value; }
			}

			public FontStyle FontStyle {
				get { return fontStyle; }
				set { if (value != null) fontStyle = value; }
			}

			public Decoration(XmlElement rule) {
				Contract.Requires(rule != null);
				FindAndParseForeground(rule);
				FindAndParseFontWeight(rule);
				FindAndParseFontStyle(rule);
			}

			[ContractInvariantMethod]
			private void Invariant() {
				Contract.Invariant(foreground != null);
				Contract.Invariant(fontWeight != null);
				Contract.Invariant(fontStyle != null);
			}

			private void FindAndParseForeground(XmlElement rule) {
				Contract.Requires(rule != null);
				XmlNode elem = rule.SelectSingleNode("./Foreground");
				if (elem != null) {
					BrushConverter conv = new BrushConverter();
					if (conv.IsValid(elem.InnerText))
						foreground = (Brush)conv.ConvertFrom(elem.InnerText);
				}
			}

			private void FindAndParseFontWeight(XmlElement rule) {
				Contract.Requires(rule != null);
				XmlNode elem = rule.SelectSingleNode("./FontWeight");
				if (elem != null) {
					FontWeightConverter conv = new FontWeightConverter();
					if (conv.IsValid(elem.InnerText))
						fontWeight = (FontWeight)conv.ConvertFrom(elem.InnerText);
				}
			}

			private void FindAndParseFontStyle(XmlElement rule) {
				Contract.Requires(rule != null);
				XmlNode elem = rule.SelectSingleNode("./FontStyle");
				if (elem != null) {
					FontStyleConverter conv = new FontStyleConverter();
					if (conv.IsValid(elem.InnerText))
						fontStyle = (FontStyle)conv.ConvertFrom(elem.InnerText);
				}
			}
		}
	}
}
