using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Windows.Input;
using System.Collections.Generic;

namespace AurelienRibon.Ui.CodeBox {
	public partial class CodeBox : TextBox {

		// --------------------------------------------------------------------
		// Attributes
		// --------------------------------------------------------------------

		public double LineHeight {
			get { return lineHeight; }
			set {
				if (value != lineHeight) {
					lineHeight = value;
					blockHeight = MaxLineCountInBlock * value;
					TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
					TextBlock.SetLineHeight(this, lineHeight);
				}
			}
		}

		public int MaxLineCountInBlock {
			get { return maxLineCountInBlock; }
			set {
				maxLineCountInBlock = value > 0 ? value : 0;
				blockHeight = value * LineHeight;
			}
		}

		public IHighlighter CurrentHighlighter { get; set; }

		private DrawingControl renderCanvas;
		private DrawingControl lineNumbersCanvas;
		private ScrollViewer scrollViewer;
		private TextBlock debugTB;
		private double lineHeight;
		private int totalLineCount;
		private List<InnerTextBlock> blocks;
		private double blockHeight;
		private int maxLineCountInBlock;

		// --------------------------------------------------------------------
		// Ctor and event handlers
		// --------------------------------------------------------------------

		public CodeBox() {
			InitializeComponent();

			MaxLineCountInBlock = 50;
			LineHeight = FontSize * 1.3;
			totalLineCount = 1;
			blocks = new List<InnerTextBlock>();

			Loaded += (s, e) => {
				renderCanvas = (DrawingControl)Template.FindName("PART_RenderCanvas", this);
				lineNumbersCanvas = (DrawingControl)Template.FindName("PART_LineNumbersCanvas", this);
				scrollViewer = (ScrollViewer)Template.FindName("PART_ContentHost", this);
				debugTB = (TextBlock)Template.FindName("PART_DebugTB", this);

				scrollViewer.ScrollChanged += OnScrollChanged;

				InvalidateBlocks();
				InvalidateVisual();
			};

			SizeChanged += (s, e) => {
				if (e.HeightChanged == false)
					return;
				UpdateBlocks();
				InvalidateVisual();
			};

			TextChanged += (s, e) => {
				UpdateTotalLineCount();
				InvalidateBlocks();
				InvalidateVisual();
			};
		}

		protected override void OnRender(DrawingContext drawingContext) {
			DrawBlocks();
			base.OnRender(drawingContext);
		}

		private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
			if (e.VerticalChange != 0)
				UpdateBlocks();
			InvalidateVisual();
		}

		// -----------------------------------------------------------
		// Updating
		// -----------------------------------------------------------

		private void UpdateTotalLineCount() {
			totalLineCount = TextUtilities.GetLineCount(Text);
		}

		private void UpdateBlocks() {
			if (blocks.Count == 0)
				return;

			// While something is visible before first block...
			while (blocks.First().Position.Y > 0 && blocks.First().Position.Y - VerticalOffset > 0) {
				int firstLineIndex = blocks.First().LineStartIndex - maxLineCountInBlock;
				int lastLineIndex = blocks.First().LineStartIndex - 1;
				firstLineIndex = firstLineIndex >= 0 ? firstLineIndex : 0;

				int firstCharIndex = TextUtilities.GetFirstCharIndexFromLineIndex(Text, firstLineIndex); // to be optimized (backward search)
				int lastCharIndex = blocks.First().CharStartIndex - 1;

				InnerTextBlock block = new InnerTextBlock(
					firstCharIndex,
					lastCharIndex,
					firstLineIndex,
					lastLineIndex,
					LineHeight);
				block.Text = GetFormattedText(block.GetSubString(Text));
				block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
				blocks.Insert(0, block);
			}

			// While something is visible after last block...
			while (!blocks.Last().IsLast && blocks.Last().Position.Y + blockHeight - VerticalOffset < ActualHeight) {
				int firstLineIndex = blocks.Last().LineEndIndex + 1;
				int lastLineIndex = firstLineIndex + maxLineCountInBlock - 1;
				lastLineIndex = lastLineIndex <= totalLineCount - 1 ? lastLineIndex : totalLineCount - 1;

				int fisrCharIndex = blocks.Last().CharEndIndex + 1;
				int lastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(Text, lastLineIndex); // to be optimized (forward search)

				if (lastCharIndex <= fisrCharIndex) {
					blocks.Last().IsLast = true;
					return;
				}

				InnerTextBlock block = new InnerTextBlock(
					fisrCharIndex,
					lastCharIndex,
					blocks.Last().LineEndIndex + 1,
					lastLineIndex,
					LineHeight);
				block.Text = GetFormattedText(block.GetSubString(Text));
				block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
				blocks.Add(block);
			}

			SetDebugMessage("Update: " + blocks.Count);
		}

		private void InvalidateBlocks() {
			blocks.Clear();

			int fvline = GetIndexOfFirstVisibleLine();
			int lvline = GetIndexOfLastVisibleLine();
			int fvchar = TextUtilities.GetFirstCharIndexFromLineIndex(Text, fvline);
			int lvchar = TextUtilities.GetFirstCharIndexFromLineIndex(Text, lvline);

			int localLineCount = 1;
			int charStart = fvchar;
			int lineStart = fvline;
			for (int i = fvchar; i < Text.Length; i++) {
				if (Text[i] == '\n') {
					localLineCount += 1;
				}
				if (i == Text.Length - 1) {
					string blockText = Text.Substring(charStart);
					InnerTextBlock block = new InnerTextBlock(
						charStart,
						i, lineStart,
						lineStart + TextUtilities.GetLineCount(blockText) - 1,
						LineHeight);
					block.Text = GetFormattedText(block.GetSubString(Text));
					block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
					block.IsLast = true;
					blocks.Add(block);
					break;
				}
				if (localLineCount > maxLineCountInBlock) {
					InnerTextBlock block = new InnerTextBlock(
						charStart,
						i,
						lineStart,
						lineStart + maxLineCountInBlock - 1,
						LineHeight);
					block.Text = GetFormattedText(block.GetSubString(Text));
					block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
					blocks.Add(block);

					charStart = i + 1;
					lineStart += maxLineCountInBlock;
					localLineCount = 1;

					if (i > lvchar)
						break;
				}
			}

			SetDebugMessage("Invalidate: " + blocks.Count);
		}

		// -----------------------------------------------------------
		// Rendering
		// -----------------------------------------------------------

		private void SetDebugMessage(string msg) {
			if (debugTB != null)
				debugTB.Text = msg;
		}

		private void DrawBlocks() {
			if (!IsLoaded || renderCanvas == null)
				return;

			var dc = renderCanvas.GetContext();
			var dc2 = lineNumbersCanvas.GetContext();
			foreach (var block in blocks) {
				Point blockPos = block.Position;
				double top = blockPos.Y - VerticalOffset;
				double bottom = top + blockHeight;
				if (top < ActualHeight && bottom > 0) {
					dc.DrawText(block.Text, new Point(2 - HorizontalOffset, block.Position.Y - VerticalOffset));
					if (IsLineNumbersMarginVisible)
						dc2.DrawText(block.LineNumbers, new Point(lineNumbersCanvas.ActualWidth, 2 + block.Position.Y - VerticalOffset));
				}
			}
			dc.Close();
			dc2.Close();
		}

		// -----------------------------------------------------------
		// Utilities
		// -----------------------------------------------------------

		/// <summary>
		/// Redefined. Returns the index of the first visible text line.
		/// </summary>
		public int GetIndexOfFirstVisibleLine() {
			int guessedLine = (int)(VerticalOffset / lineHeight);
			return guessedLine > totalLineCount ? totalLineCount : guessedLine;
		}

		/// <summary>
		/// Redefined. Returns the index of the last visible text line.
		/// </summary>
		public int GetIndexOfLastVisibleLine() {
			double height = VerticalOffset + ViewportHeight;
			int guessedLine = (int)(height / lineHeight);
			return guessedLine > totalLineCount - 1 ? totalLineCount - 1 : guessedLine;
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

			if (CurrentHighlighter != null)
				ft = CurrentHighlighter.Highlight(ft);

			return ft;
		}

		/// <summary>
		/// Returns a string containing a list of numbers separated with newlines.
		/// </summary>
		private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex) {
			string text = "";
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

		public bool IsLineNumbersMarginVisible {
			get { return (bool)GetValue(IsLineNumbersMarginVisibleProperty); }
			set { SetValue(IsLineNumbersMarginVisibleProperty, value); }
		}

		// -----------------------------------------------------------
		// Classes
		// -----------------------------------------------------------

		private class InnerTextBlock {
			public FormattedText Text { get; set; }
			public FormattedText LineNumbers { get; set; }
			public int CharStartIndex { get; private set; }
			public int CharEndIndex { get; private set; }
			public int LineStartIndex { get; private set; }
			public int LineEndIndex { get; private set; }
			public Point Position { get { return new Point(0, LineStartIndex * lineHeight); } }
			public bool IsLast { get; set; }

			private double lineHeight;

			public InnerTextBlock(int charStart, int charEnd, int lineStart, int lineEnd, double lineHeight) {
				CharStartIndex = charStart;
				CharEndIndex = charEnd;
				LineStartIndex = lineStart;
				LineEndIndex = lineEnd;
				this.lineHeight = lineHeight;
				IsLast = false;

			}

			public string GetSubString(string text) {
				return text.Substring(CharStartIndex, CharEndIndex - CharStartIndex + 1);
			}

			public override string ToString() {
				return string.Format("L:{0}/{1} C:{2}/{3} {4}",
					LineStartIndex,
					LineEndIndex,
					CharStartIndex,
					CharEndIndex,
					Text.Text);
			}
		}
	}
}
