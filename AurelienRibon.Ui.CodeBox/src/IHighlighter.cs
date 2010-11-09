using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace AurelienRibon.Ui.CodeBox {
	public interface IHighlighter {
		FormattedText Highlight(FormattedText input);
	}
}
