using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AurelienRibon.CodeBox {
	public partial class DrawingControl : FrameworkElement {
		private VisualCollection visuals = null;
		private DrawingVisual visual = null;

		public DrawingControl() {
			InitializeComponent();
			visual = new DrawingVisual();
			visuals = new VisualCollection(this);
			visuals.Add(visual);
		}

		public DrawingContext GetContext() {
			return visual.RenderOpen();
		}

		protected override int VisualChildrenCount {
			get { return visuals.Count; }
		}

		protected override Visual GetVisualChild(int index) {
			if (index < 0 || index >= visuals.Count)
				throw new ArgumentOutOfRangeException();
			return visuals[index];
		}
	}
}

