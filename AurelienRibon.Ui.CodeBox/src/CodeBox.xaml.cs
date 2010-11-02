using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Windows.Input;

namespace AurelienRibon.Ui.CodeBox {
	public partial class CodeBox : TextBox {
		private DrawingControl renderCanvas = null;
		private DrawingControl lineNumbersCanvas = null;
		private double lineHeight = 0.0;
		private int lineCount = 1;
		private Highlighter currentHighlighter = null;

		public double LineHeight {
			get { return lineHeight; }
			set {
				if (value != lineHeight) {
					lineHeight = value;
					TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
					TextBlock.SetLineHeight(this, lineHeight);
				}
			}
		}

		public CodeBox() {
			Highlighter.Initialize();
			InitializeComponent();

			Loaded += (s, e) => {
				renderCanvas = (DrawingControl)Template.FindName("renderCanvas", this);
				lineNumbersCanvas = (DrawingControl)Template.FindName("lineNumbersCanvas", this);

				var scviewer = (ScrollViewer)Template.FindName("PART_ContentHost", this);
				scviewer.ScrollChanged += (ss, ee) => {
					Update();
					Draw(false);
				};

				Update();
				Draw(true);
			};

			TextChanged += (s, e) => {
				lineCount = TextUtilities.GetLineCount(Text);
				Update();
				Draw(true);
			};
		}

		public Highlighter CurrentHighlighter {
			set { currentHighlighter = value; }
			get { return currentHighlighter; }
		}

		// -----------------------------------------------------------
		// Rendering
		// -----------------------------------------------------------

		private void Update() {
			LineHeight = FontSize * 1.3;
		}

		private void Draw(bool forceRedraw) {
			int fvli = GetIndexOfFirstVisibleLine();
			int lvli = GetIndexOfLastVisibleLine();

			DrawLineNumbers(fvli, lvli);
			DrawText(forceRedraw, fvli, lvli);
		}

		private void DrawText(bool forceRedraw, int firstVisibleLineIndex, int lastVisibleLineIndex) {
			if (!IsLoaded || renderCanvas == null)
				return;

			var ft = HighlightText(forceRedraw, firstVisibleLineIndex, lastVisibleLineIndex);

			var dc = renderCanvas.GetContext();
			dc.DrawText(ft, new Point(3 - HorizontalOffset, 1 - VerticalOffset % lineHeight));
			dc.Close();
		}

		private void DrawLineNumbers(int firstVisibleLineIndex, int lastVisibleLineIndex) {
			if (!IsLoaded || lineNumbersCanvas == null)
				return;

			var ft = GetFormattedLineNumbers(firstVisibleLineIndex, lastVisibleLineIndex);
			var dc = lineNumbersCanvas.GetContext();
			dc.DrawText(ft, new Point(lineNumbersCanvas.ActualWidth, 3 - VerticalOffset % lineHeight));
			dc.Close();
		}

		private int oldFirstVisibleLineIndex = -1;
		private int oldLastVisibleLineIndex = -1;
		private FormattedText ft;
		private FormattedText HighlightText(bool forceRedraw, int firstVisibleLineIndex, int lastVisibleLineIndex) {
			if (!forceRedraw 
				&& firstVisibleLineIndex == oldFirstVisibleLineIndex 
				&& lastVisibleLineIndex == oldLastVisibleLineIndex)
				return ft;

			int firstCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, firstVisibleLineIndex);
			int lastCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, lastVisibleLineIndex + 1);

			string txt = "";
			if (Text.Length > 0)
				txt = Text.Substring(firstCharIndex, lastCharIndex - firstCharIndex + 1);

			ft = GetFormattedText(txt);
			if (currentHighlighter != null)
				ft = currentHighlighter.Highlight(ft);

			oldFirstVisibleLineIndex = firstCharIndex;
			oldLastVisibleLineIndex = lastCharIndex;

			return ft;
		}

		// -----------------------------------------------------------
		// Utilities
		// -----------------------------------------------------------

		/// <summary>
		/// Redefined. Returns the index of the first visible text line.
		/// </summary>
		public int GetIndexOfFirstVisibleLine() {
			int guessedLine = (int)(VerticalOffset / lineHeight);
			return guessedLine < lineCount ? guessedLine : lineCount - 1;
		}

		/// <summary>
		/// Redefined. Returns the index of the last visible text line.
		/// </summary>
		public int GetIndexOfLastVisibleLine() {
			double height = VerticalOffset + ViewportHeight;
			int guessedLine = (int)(height / lineHeight);
			return guessedLine < lineCount ? guessedLine : lineCount - 1;
		}

		/// <summary>
		/// Returns a formatted text object from the given string
		/// </summary>
		private FormattedText GetFormattedText(string text) {
			FormattedText ft = new FormattedText(
				text,
				System.Globalization.CultureInfo.InvariantCulture,
				FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
				FontSize,
				Brushes.Black);
			ft.Trimming = TextTrimming.None;
			ft.LineHeight = lineHeight;
			return ft;
		}

		/// <summary>
		/// Returns a string containing a list of numbers separated with newlines.
		/// </summary>
		private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex) {
			String text = "";
			for (int i = firstIndex + 1; i <= lastIndex + 1; i++)
				text += i.ToString() + "\n";
			text = text.Trim();
			FormattedText ft = new FormattedText(
				text,
				System.Globalization.CultureInfo.InvariantCulture,
				FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
				FontSize,
				new SolidColorBrush(Color.FromRgb(0x21, 0xA1, 0xD8)));
			ft.Trimming = TextTrimming.None;
			ft.LineHeight = lineHeight;
			ft.TextAlignment = TextAlignment.Right;
			return ft;
		}

		// -----------------------------------------------------------
		// Dependency Properties
		// -----------------------------------------------------------

		public static readonly DependencyProperty IsLineNumbersMarginVisibleProperty = DependencyProperty.Register(
			"IsLineNumbersMarginVisible", typeof(bool), typeof(CodeBox), new PropertyMetadata(true));

		public static readonly DependencyProperty HighlightedSyntaxProperty = DependencyProperty.Register(
			"HighlightedSyntax", typeof(string), typeof(CodeBox), new PropertyMetadata("None", new PropertyChangedCallback(OnHighlightedSyntaxPropertyChanged)));

		public bool IsLineNumbersMarginVisible {
			get { return (bool)GetValue(IsLineNumbersMarginVisibleProperty); }
			set { SetValue(IsLineNumbersMarginVisibleProperty, value); }
		}

		public string HighlightedSyntax {
			get { return (String)GetValue(HighlightedSyntaxProperty); }
			set { SetValue(HighlightedSyntaxProperty, value); }
		}

		private static void OnHighlightedSyntaxPropertyChanged(DependencyObject source,
			DependencyPropertyChangedEventArgs e) {
			CodeBox cbox = source as CodeBox;
			cbox.CurrentHighlighter = Highlighter.GetHighlighter(e.NewValue as String);
			if (cbox.CurrentHighlighter != null)
				Debug.WriteLine("CodeBox (" + cbox.GetHashCode() + ") highlighter successfully changed to " +
					cbox.CurrentHighlighter.Name);
			else
				Debug.WriteLine("CodeBox (" + cbox.GetHashCode() + ") highlighter not found");
		}
	}
}
