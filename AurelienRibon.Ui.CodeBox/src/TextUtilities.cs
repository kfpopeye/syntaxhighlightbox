using System;
using System.Diagnostics.Contracts;

namespace AurelienRibon.Ui.CodeBox {
	public class TextUtilities {
		/// <summary>
		/// Redefined. Returns the raw number of the current line count.
		/// </summary>
		/// <param name="text">The input text</param>
		/// <returns>The current line count</returns>
		public static int GetLineCount(String text) {
			if (String.IsNullOrEmpty(text))
				return 1;

			int lcnt = 1;
			char[] c = text.ToCharArray();
			for (int i = 0; i < c.Length; i++) {
				if ('\n'.Equals(c[i]))
					lcnt++;
			}
			return lcnt;
		}

		/// <summary>
		/// Redefined. Returns the index of the first character of the
		/// specified line. If the index is greater than the current
		/// line count, the method returns the index of the last
		/// character.
		/// </summary>
		/// <param name="lineIndex">The line index.</param>
		/// <returns>The index of the first character of the
		/// specified line</returns>
		public static int GetCharIndexFromLineIndex(string text, int lineIndex) {
			if (text == null)
				throw new ArgumentNullException("text");
			if (lineIndex <= 0)
				return 0;

			int currentLineIndex = 0;
			for (int i = 0; i < text.Length - 1; i++) {
				if (text[i] == '\n') {
					currentLineIndex++;
					if (currentLineIndex == lineIndex)
						return Math.Min(i + 1, text.Length - 1);
				}
			}

			return Math.Max(text.Length - 1, 0);
		}
	}
}
