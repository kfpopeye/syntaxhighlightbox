using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Windows.Input;

namespace AurelienRibon.CodeBox {
	public partial class CodeBox : TextBox {
		private DrawingControl renderCanvas = null;
		private DrawingControl lineNumbersCanvas = null;
		private double lineHeight = 0.0;
		private int lineCount = 1;
		private Highlighter currentHighlighter = null;

		public double LineHeight {
			get { return lineHeight; }
			set { lineHeight = value; }
		}

		public CodeBox() {
			Highlighter.Initialize();
			InitializeComponent();
		}

		public Highlighter CurrentHighlighter {
			set { currentHighlighter = value; }
			get { return currentHighlighter; }
		}

		// -----------------------------------------------------------
		// Rendering
		// -----------------------------------------------------------

		private void Update() {
			LineHeight = FontSize * 1.2;
			TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
			TextBlock.SetLineHeight(this, lineHeight);
		}

		private void Draw() {
			DrawLineNumbersCanvas();
			DrawText();
		}

		FormattedText ft = null;
		private void DrawText() {
			if (!IsLoaded || renderCanvas == null) {
				return;
			}

			int firstVisibleLineIndex = GetIndexOfFirstVisibleLine();
			int lastVisibleLineIndex = GetIndexOfLastVisibleLine();
			Contract.Assert(lastVisibleLineIndex >= firstVisibleLineIndex);
			int firstCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, firstVisibleLineIndex);
			int lastCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, lastVisibleLineIndex + 1);
			Contract.Assert(lastCharIndex >= firstCharIndex);

			String txt = "";
			if (Text.Length > 0)
				txt = Text.Substring(firstCharIndex, lastCharIndex - firstCharIndex + 1);
			ft = GetFormattedText(txt);
			if (currentHighlighter != null)
				ft = currentHighlighter.Highlight(ft);

			DrawingContext dc = renderCanvas.GetContext();
			dc.DrawText(ft, new Point(3 - HorizontalOffset, 1 - VerticalOffset % lineHeight));
			dc.Close();
		}

		private FormattedText GetFormattedText(String text) {
			Contract.Ensures(Contract.Result<FormattedText>() != null);
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

		private void DrawLineNumbersCanvas() {
			if (!IsLoaded || lineNumbersCanvas == null) {
				return;
			}

			int firstVisibleLineIndex = GetIndexOfFirstVisibleLine();
			int lastVisibleLineIndex = GetIndexOfLastVisibleLine();
			Contract.Assert(lastVisibleLineIndex >= firstVisibleLineIndex);

			FormattedText ft = GetFormattedLineNumbers(firstVisibleLineIndex, lastVisibleLineIndex);
			DrawingContext dc = lineNumbersCanvas.GetContext();
			dc.DrawText(ft, new Point(lineNumbersCanvas.ActualWidth, 1 - VerticalOffset % lineHeight));
			dc.Close();
		}

		private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex) {
			Contract.Requires(lastIndex >= firstIndex);
			String text = "";
			for (int i = firstIndex + 1; i <= lastIndex + 1; i++)
				text += i.ToString() + Environment.NewLine;
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
		// Events
		// -----------------------------------------------------------

		private void OnLoaded(object sender, RoutedEventArgs e) {
			Contract.Ensures(renderCanvas != null);
			Contract.Ensures(lineNumbersCanvas != null);
			renderCanvas = (DrawingControl)Template.FindName("renderCanvas", this);
			lineNumbersCanvas = (DrawingControl)Template.FindName("lineNumbersCanvas", this);
			Update();
			Draw();
		}

		private void OnTextChanged(object sender, TextChangedEventArgs e) {
			lineCount = TextUtilities.GetLineCount(Text);
			Update();
			Draw();
		}

		private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
			Update();
			Draw();
		}

		// -----------------------------------------------------------
		// Utilities
		// -----------------------------------------------------------

		/// <summary>
		/// Redefined. Returns the index of the first visible text line.
		/// </summary>
		/// <returns>The index of the first visible text line</returns>
		public int GetIndexOfFirstVisibleLine() {
			Contract.Ensures(Contract.Result<int>() >= 0);
			Contract.Ensures(Contract.Result<int>() < lineCount);

			int guessedLine = (int)(VerticalOffset / lineHeight);
			return guessedLine < lineCount ? guessedLine : lineCount - 1;
		}

		/// <summary>
		/// Redefined. Returns the index of the last visible text line.
		/// </summary>
		/// <returns>The index of the last visible text line</returns>
		public int GetIndexOfLastVisibleLine() {
			Contract.Ensures(Contract.Result<int>() >= 0);
			Contract.Ensures(Contract.Result<int>() < lineCount);

			double height = VerticalOffset + ViewportHeight;
			int guessedLine = (int)(height / lineHeight);
			return guessedLine < lineCount ? guessedLine : lineCount - 1;
		}

		// -----------------------------------------------------------
		// Dependency Properties
		// -----------------------------------------------------------

		public static readonly DependencyProperty IsLineNumbersMarginVisibleProperty =
			DependencyProperty.Register("IsLineNumbersMarginVisible", typeof(bool), typeof(CodeBox),
			new PropertyMetadata(true));

		public static readonly DependencyProperty HighlightedSyntaxProperty =
			DependencyProperty.Register("HighlightedSyntax", typeof(String), typeof(CodeBox),
			new PropertyMetadata("", new PropertyChangedCallback(OnHighlightedSyntaxPropertyChanged)));

		public bool IsLineNumbersMarginVisible {
			get { return (bool)GetValue(IsLineNumbersMarginVisibleProperty); }
			set { SetValue(IsLineNumbersMarginVisibleProperty, value); }
		}

		public String HighlightedSyntax {
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
