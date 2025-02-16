using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace HoN_ModMan;

[DesignerGenerated]
public class frmInputbox : Form
{
	private IContainer components;

	[AccessedThroughProperty("txtPath")]
	private TextBox _txtPath;

	[AccessedThroughProperty("cmdOK")]
	private Button _cmdOK;

	[AccessedThroughProperty("cmdCancel")]
	private Button _cmdCancel;

	public string Result;

	internal virtual TextBox txtPath
	{
		get
		{
			return _txtPath;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_txtPath = value;
		}
	}

	internal virtual Button cmdOK
	{
		get
		{
			return _cmdOK;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = cmdOK_Click;
			if (_cmdOK != null)
			{
				_cmdOK.Click -= value2;
			}
			_cmdOK = value;
			if (_cmdOK != null)
			{
				_cmdOK.Click += value2;
			}
		}
	}

	internal virtual Button cmdCancel
	{
		get
		{
			return _cmdCancel;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = cmdCancel_Click;
			if (_cmdCancel != null)
			{
				_cmdCancel.Click -= value2;
			}
			_cmdCancel = value;
			if (_cmdCancel != null)
			{
				_cmdCancel.Click += value2;
			}
		}
	}

	public frmInputbox()
	{
		base.Load += frmManualPath_Load;
		InitializeComponent();
	}

	[DebuggerNonUserCode]
	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	[System.Diagnostics.DebuggerStepThrough]
	private void InitializeComponent()
	{
		this.txtPath = new System.Windows.Forms.TextBox();
		this.cmdOK = new System.Windows.Forms.Button();
		this.cmdCancel = new System.Windows.Forms.Button();
		this.SuspendLayout();
		System.Windows.Forms.TextBox textBox = this.txtPath;
		System.Drawing.Point location = new System.Drawing.Point(12, 12);
		textBox.Location = location;
		this.txtPath.Name = "txtPath";
		System.Windows.Forms.TextBox textBox2 = this.txtPath;
		System.Drawing.Size size = new System.Drawing.Size(281, 21);
		textBox2.Size = size;
		this.txtPath.TabIndex = 0;
		System.Windows.Forms.Button button = this.cmdOK;
		location = new System.Drawing.Point(75, 39);
		button.Location = location;
		this.cmdOK.Name = "cmdOK";
		System.Windows.Forms.Button button2 = this.cmdOK;
		size = new System.Drawing.Size(81, 23);
		button2.Size = size;
		this.cmdOK.TabIndex = 1;
		this.cmdOK.Text = "OK";
		this.cmdOK.UseVisualStyleBackColor = true;
		this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		System.Windows.Forms.Button button3 = this.cmdCancel;
		location = new System.Drawing.Point(162, 39);
		button3.Location = location;
		this.cmdCancel.Name = "cmdCancel";
		System.Windows.Forms.Button button4 = this.cmdCancel;
		size = new System.Drawing.Size(81, 23);
		button4.Size = size;
		this.cmdCancel.TabIndex = 2;
		this.cmdCancel.Text = "Cancel";
		this.cmdCancel.UseVisualStyleBackColor = true;
		this.AcceptButton = this.cmdOK;
		System.Drawing.SizeF sizeF = new System.Drawing.SizeF(6f, 13f);
		this.AutoScaleDimensions = sizeF;
		this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.CancelButton = this.cmdCancel;
		size = new System.Drawing.Size(305, 74);
		this.ClientSize = size;
		this.ControlBox = false;
		this.Controls.Add(this.cmdCancel);
		this.Controls.Add(this.cmdOK);
		this.Controls.Add(this.txtPath);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "frmInputbox";
		this.Text = "Enter HoN path manually:";
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	private void cmdOK_Click(object sender, EventArgs e)
	{
		Result = txtPath.Text;
		DialogResult = DialogResult.OK;
	}

	private void cmdCancel_Click(object sender, EventArgs e)
	{
		DialogResult = DialogResult.Cancel;
	}

	private void frmManualPath_Load(object sender, EventArgs e)
	{
		txtPath.Text = Result;
	}
}
