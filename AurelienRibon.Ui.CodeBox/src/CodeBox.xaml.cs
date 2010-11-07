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
					blockHeight = maxLineCountInBlock * value;
					TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
					TextBlock.SetLineHeight(this, lineHeight);
				}
			}
		}

		private DrawingControl renderCanvas;
		private DrawingControl lineNumbersCanvas;
		private ScrollViewer scrollViewer;
		private TextBlock debugTB;
		private double lineHeight;
		private int totalLineCount;
		private List<InnerTextBlock> blocks;
		private int maxBlockCount;
		private double blockHeight;

		private readonly int maxLineCountInBlock = 5;

		// --------------------------------------------------------------------
		// Ctor and event handlers
		// --------------------------------------------------------------------

		public CodeBox() {
			InitializeComponent();

			LineHeight = FontSize * 1.3;
			totalLineCount = 1;
			blocks = new List<InnerTextBlock>();

			Loaded += (s, e) => {
				renderCanvas = (DrawingControl)Template.FindName("PART_RenderCanvas", this);
				lineNumbersCanvas = (DrawingControl)Template.FindName("PART_LineNumbersCanvas", this);
				scrollViewer = (ScrollViewer)Template.FindName("PART_ContentHost", this);
				debugTB = (TextBlock)Template.FindName("PART_DebugTB", this);

				scrollViewer.ScrollChanged += OnScrollChanged;

				UpdateMaxBlockCount();
				InvalidateBlocks();
			};

			SizeChanged += (s, e) => {
				if (e.HeightChanged == false)
					return;
				UpdateMaxBlockCount();
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
			DrawLineNumbers(GetIndexOfFirstVisibleLine(), GetIndexOfLastVisibleLine());
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

		private void UpdateMaxBlockCount() {
			maxBlockCount = (int)Math.Ceiling(ActualHeight / (maxLineCountInBlock * LineHeight));
		}

		private void UpdateBlocks() {
			if (blocks.Count == 0)
				return;

			Point firstBlockPos = blocks.First().Position;
			Point lastBlockPos = blocks.Last().Position;

			// If something is visible before first block...
			if (firstBlockPos.Y - VerticalOffset > 0) {
				int firstLineIndex = blocks.First().LineStartIndex - maxLineCountInBlock;
				firstLineIndex = firstLineIndex >= 0 ? firstLineIndex : 0;
				int firstCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, firstLineIndex); // to be optimized (backward search)

				InnerTextBlock block = new InnerTextBlock(
					firstCharIndex,
					blocks.First().CharStartIndex - 1, 
					firstLineIndex, 
					blocks.First().LineStartIndex - 1,
					LineHeight);
				block.Text = GetFormattedText(block.GetSubString(Text));
				blocks.Add(block);
			}

			// If something is visible after last block...
			if (!blocks.Last().IsLast) {
				if (lastBlockPos.Y + blockHeight - VerticalOffset < ActualHeight) {
					int lastLineIndex = blocks.Last().LineEndIndex + maxLineCountInBlock;
					lastLineIndex = lastLineIndex <= totalLineCount ? lastLineIndex : totalLineCount;
					int lastCharIndex = TextUtilities.GetCharIndexFromLineIndex(Text, lastLineIndex); // to be optimized (forward search)

					InnerTextBlock block = new InnerTextBlock(
						blocks.Last().CharEndIndex + 1,
						lastCharIndex,
						blocks.Last().LineEndIndex + 1,
						lastLineIndex,
						LineHeight);
					block.Text = GetFormattedText(block.GetSubString(Text));
					blocks.Add(block);
				}
			}

			SetDebugMessage("Update: " + blocks.Count + " / " + maxBlockCount);
		}

		private void InvalidateBlocks() {
			blocks.Clear();

			int fvline = GetIndexOfFirstVisibleLine();
			int lvline = GetIndexOfLastVisibleLine();
			int fvchar = TextUtilities.GetCharIndexFromLineIndex(Text, fvline);
			int lvchar = TextUtilities.GetCharIndexFromLineIndex(Text, lvline);

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
					blocks.Add(block);

					charStart = i + 1;
					lineStart += maxLineCountInBlock;
					localLineCount = 1;

					if (i > lvchar)
						break;
				}
			}

			SetDebugMessage("Invalidate: " + blocks.Count + " / " + maxBlockCount);
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
			foreach (var block in blocks) {
				Point blockPos = block.Position;
				double top = blockPos.Y - VerticalOffset;
				double bottom = top + blockHeight;
				if (top < ActualHeight && bottom > 0)
					dc.DrawText(block.Text, new Point(2 - HorizontalOffset, block.Position.Y - VerticalOffset));
			}
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

		public bool IsLineNumbersMarginVisible {
			get { return (bool)GetValue(IsLineNumbersMarginVisibleProperty); }
			set { SetValue(IsLineNumbersMarginVisibleProperty, value); }
		}

		// -----------------------------------------------------------
		// Classes
		// -----------------------------------------------------------

		private class InnerTextBlock {
			public FormattedText Text { get; set; }
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
		}
	}
}
