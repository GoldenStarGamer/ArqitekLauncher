using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using System;

namespace ArqitekLauncher.Views
{
	public partial class ProgressWindow : Window
	{
		public ProgressWindow(MainWindow main)
		{
			InitializeComponent();
		}

		public void UpdateProgress(double value)
		{
			ProgressBar.Value = value;
		}

		public string Text
		{
			get
			{
				return text.Text!;
			}
			
			set
			{
				text.Text = value;
			}
		}
	}
}