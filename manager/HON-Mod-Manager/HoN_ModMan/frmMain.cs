using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using HoN_ModMan.My;
using HoN_ModMan.My.Resources;
using Ionic.Zip;
using Ionic.Zlib;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace HoN_ModMan;

[DesignerGenerated]
public class frmMain : Form
{
	private class Modification
	{
		public int Index;

		public int ImageListIdx;

		private bool myDisabled;

		public bool Applied;

		public string File;

		public string Name;

		public string FixedName;

		public string Description;

		public string Author;

		public string Version;

		public string WebLink;

		public Bitmap Icon;

		public string AppVersion;

		public string MMVersion;

		public Dictionary<string, string> Incompatibilities;

		public Dictionary<string, string> Requirements;

		public Dictionary<string, string> ApplyBefore;

		public Dictionary<string, string> ApplyAfter;

		public List<Modification> ApplyFirst;

		public bool Marked;

		public string UpdateCheck;

		public string UpdateDownload;

		public ModUpdater Updater;

		public bool Disabled
		{
			get
			{
				return myDisabled;
			}
			set
			{
				myDisabled = value;
			}
		}

		public bool Enabled
		{
			get
			{
				return !myDisabled;
			}
			set
			{
				myDisabled = !value;
			}
		}

		public Modification()
		{
			Incompatibilities = new Dictionary<string, string>();
			Requirements = new Dictionary<string, string>();
			ApplyBefore = new Dictionary<string, string>();
			ApplyAfter = new Dictionary<string, string>();
			ApplyFirst = new List<Modification>();
		}

		public bool IsUpdating()
		{
			if (Updater == null || Updater.Status >= ModUpdater.UpdateStatus.NoUpdateInformation)
			{
				return false;
			}
			return true;
		}
	}

	private class ModUpdater
	{
		public enum UpdateStatus
		{
			NotBegun,
			Checking,
			Downloading,
			NoUpdateInformation,
			NoUpdatePresent,
			UpdateDownloaded,
			Failed,
			Aborted
		}

		private Modification tMod;

		private UpdateStatus myStatus;

		public bool Reaped;

		private Thread myThread;

		private bool AbortRequested;

		private string NewestVersion;

		private byte[] myFile;

		private int myFileSize;

		private int myFileProgress;

		public Modification Mod => tMod;

		public UpdateStatus Status => myStatus;

		public string StatusString
		{
			get
			{
				switch (myStatus)
				{
				case UpdateStatus.NotBegun:
					return "Waiting";
				case UpdateStatus.Checking:
					return "Checking for new version";
				case UpdateStatus.Downloading:
					if (myFileSize > 0)
					{
						return "Downloaded " + Conversions.ToString(checked(myFileProgress * 100) / myFileSize) + "% of " + Conversions.ToString(myFileSize / 1024) + " KB";
					}
					return "Downloading";
				case UpdateStatus.NoUpdateInformation:
					return "Not updatable";
				case UpdateStatus.NoUpdatePresent:
					return "Already up to date";
				case UpdateStatus.UpdateDownloaded:
					return "Updated to v" + NewestVersion;
				case UpdateStatus.Failed:
					return "Failed";
				case UpdateStatus.Aborted:
					return "Aborted";
				default:
					return "";
				}
			}
		}

		public string SortKey
		{
			get
			{
				string text = "";
				switch (myStatus)
				{
				case UpdateStatus.NoUpdatePresent:
					text = "Z";
					break;
				case UpdateStatus.UpdateDownloaded:
					text = "A";
					break;
				case UpdateStatus.Failed:
					text = "B";
					break;
				case UpdateStatus.Aborted:
					text = "C";
					break;
				}
				return text + tMod.Name + tMod.Version;
			}
		}

		public bool UpdateDownloaded => myStatus == UpdateStatus.UpdateDownloaded;

		public ModUpdater(Modification tMod)
		{
			Reaped = false;
			myThread = new Thread(UpdateThread);
			AbortRequested = false;
			NewestVersion = "";
			myFile = null;
			this.tMod = tMod;
			myThread.Start();
		}

		public void Abort()
		{
			AbortRequested = true;
		}

		private static string FixHTTPURL(string URL)
		{
			if (URL.StartsWith("http://") | URL.StartsWith("https://"))
			{
				return URL;
			}
			return "http://" + URL;
		}

		private void UpdateThread()
		{
			if ((Operators.CompareString(tMod.UpdateCheck, "", TextCompare: false) == 0) | (Operators.CompareString(tMod.UpdateDownload, "", TextCompare: false) == 0))
			{
				myStatus = UpdateStatus.NoUpdateInformation;
				return;
			}
			checked
			{
				try
				{
					myStatus = UpdateStatus.Checking;
					HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(FixHTTPURL(tMod.UpdateCheck));
					httpWebRequest.UserAgent = "HoN Mod Manager 1.4.0.0";
					HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
					StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
					char[] array = new char[20];
					array = (char[])Utils.CopyArray(array, new char[streamReader.ReadBlock(array, 0, 20) - 1 + 1]);
					NewestVersion = new string(array);
					streamReader.Close();
					httpWebResponse.Close();
					if (IsNewerVersion(NewestVersion, tMod.Version))
					{
						myStatus = UpdateStatus.NoUpdatePresent;
						return;
					}
					myStatus = UpdateStatus.Downloading;
					httpWebRequest = (HttpWebRequest)WebRequest.Create(FixHTTPURL(tMod.UpdateDownload));
					httpWebRequest.UserAgent = "HoN Mod Manager 1.4.0.0";
					httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
					if (httpWebResponse.ContentLength > 26214400)
					{
						httpWebResponse.Close();
						throw new Exception();
					}
					myFileSize = Convert.ToInt32(httpWebResponse.ContentLength);
					myFile = new byte[myFileSize - 1 + 1];
					Stream responseStream = httpWebResponse.GetResponseStream();
					do
					{
						if (AbortRequested)
						{
							httpWebResponse.Close();
							myStatus = UpdateStatus.Aborted;
							return;
						}
						int num = responseStream.Read(myFile, myFileProgress, Math.Min(256, myFileSize - myFileProgress));
						myFileProgress += num;
					}
					while (myFileSize > myFileProgress);
					httpWebResponse.Close();
					if (myFileProgress != myFileSize)
					{
						throw new Exception();
					}
					FileStream fileStream = new FileStream(tMod.File, FileMode.Create);
					fileStream.Write(myFile, 0, myFileSize);
					fileStream.Close();
					myStatus = UpdateStatus.UpdateDownloaded;
				}
				catch (Exception projectError)
				{
					ProjectData.SetProjectError(projectError);
					myStatus = UpdateStatus.Failed;
					ProjectData.ClearProjectError();
				}
			}
		}
	}

	private IContainer components;

	[AccessedThroughProperty("myListView")]
	private ListView _myListView;

	[AccessedThroughProperty("myImageList")]
	private ImageList _myImageList;

	[AccessedThroughProperty("lblName")]
	private Label _lblName;

	[AccessedThroughProperty("cmdToggleDisabled")]
	private Button _cmdToggleDisabled;

	[AccessedThroughProperty("lblDescription")]
	private LinkLabel _lblDescription;

	[AccessedThroughProperty("myMenuStrip")]
	private MenuStrip _myMenuStrip;

	[AccessedThroughProperty("myStatusStrip")]
	private StatusStrip _myStatusStrip;

	[AccessedThroughProperty("OptionsToolStripMenuItem")]
	private ToolStripMenuItem _OptionsToolStripMenuItem;

	[AccessedThroughProperty("ChangeHoNPathToolStripMenuItem")]
	private ToolStripMenuItem _ChangeHoNPathToolStripMenuItem;

	[AccessedThroughProperty("myStatusLabel")]
	private ToolStripStatusLabel _myStatusLabel;

	[AccessedThroughProperty("myInfoLabel")]
	private ToolStripStatusLabel _myInfoLabel;

	[AccessedThroughProperty("myAppVerLabel")]
	private ToolStripStatusLabel _myAppVerLabel;

	[AccessedThroughProperty("FileToolStripMenuItem")]
	private ToolStripMenuItem _FileToolStripMenuItem;

	[AccessedThroughProperty("ApplyModsToolStripMenuItem")]
	private ToolStripMenuItem _ApplyModsToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem1")]
	private ToolStripSeparator _ToolStripMenuItem1;

	[AccessedThroughProperty("ExitToolStripMenuItem")]
	private ToolStripMenuItem _ExitToolStripMenuItem;

	[AccessedThroughProperty("HelpToolStripMenuItem")]
	private ToolStripMenuItem _HelpToolStripMenuItem;

	[AccessedThroughProperty("ForumThreadToolStripMenuItem")]
	private ToolStripMenuItem _ForumThreadToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem2")]
	private ToolStripSeparator _ToolStripMenuItem2;

	[AccessedThroughProperty("AboutToolStripMenuItem")]
	private ToolStripMenuItem _AboutToolStripMenuItem;

	[AccessedThroughProperty("lblDisabled")]
	private Label _lblDisabled;

	[AccessedThroughProperty("ApplyModsAndLaunchHoNToolStripMenuItem")]
	private ToolStripMenuItem _ApplyModsAndLaunchHoNToolStripMenuItem;

	[AccessedThroughProperty("EnterHoNPathmanuallyToolStripMenuItem")]
	private ToolStripMenuItem _EnterHoNPathmanuallyToolStripMenuItem;

	[AccessedThroughProperty("myContextMenu")]
	private ContextMenuStrip _myContextMenu;

	[AccessedThroughProperty("EnableDisableToolStripMenuItem")]
	private ToolStripMenuItem _EnableDisableToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem3")]
	private ToolStripSeparator _ToolStripMenuItem3;

	[AccessedThroughProperty("UpdateThisModToolStripMenuItem")]
	private ToolStripMenuItem _UpdateThisModToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem4")]
	private ToolStripSeparator _ToolStripMenuItem4;

	[AccessedThroughProperty("DeleteToolStripMenuItem")]
	private ToolStripMenuItem _DeleteToolStripMenuItem;

	[AccessedThroughProperty("OpenModFolderToolStripMenuItem")]
	private ToolStripMenuItem _OpenModFolderToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem6")]
	private ToolStripSeparator _ToolStripMenuItem6;

	[AccessedThroughProperty("ViewToolStripMenuItem")]
	private ToolStripMenuItem _ViewToolStripMenuItem;

	[AccessedThroughProperty("RefreshModDisplayToolStripMenuItem")]
	private ToolStripMenuItem _RefreshModDisplayToolStripMenuItem;

	[AccessedThroughProperty("UpdateAllModsToolStripMenuItem")]
	private ToolStripMenuItem _UpdateAllModsToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem5")]
	private ToolStripSeparator _ToolStripMenuItem5;

	[AccessedThroughProperty("UnapplyAllModsToolStripMenuItem")]
	private ToolStripMenuItem _UnapplyAllModsToolStripMenuItem;

	[AccessedThroughProperty("CancelAllUpdatesToolStripMenuItem")]
	private ToolStripMenuItem _CancelAllUpdatesToolStripMenuItem;

	[AccessedThroughProperty("myUpdatingTimer")]
	private System.Windows.Forms.Timer _myUpdatingTimer;

	[AccessedThroughProperty("ListToolStripMenuItem")]
	private ToolStripMenuItem _ListToolStripMenuItem;

	[AccessedThroughProperty("TilesToolStripMenuItem")]
	private ToolStripMenuItem _TilesToolStripMenuItem;

	[AccessedThroughProperty("SmallIconsToolStripMenuItem")]
	private ToolStripMenuItem _SmallIconsToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem7")]
	private ToolStripSeparator _ToolStripMenuItem7;

	[AccessedThroughProperty("ToolStripMenuItem8")]
	private ToolStripSeparator _ToolStripMenuItem8;

	[AccessedThroughProperty("CLArgumentsForLaunchingHoNToolStripMenuItem")]
	private ToolStripMenuItem _CLArgumentsForLaunchingHoNToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem9")]
	private ToolStripSeparator _ToolStripMenuItem9;

	[AccessedThroughProperty("ShowVersionsInMainViewToolStripMenuItem")]
	private ToolStripMenuItem _ShowVersionsInMainViewToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem10")]
	private ToolStripSeparator _ToolStripMenuItem10;

	[AccessedThroughProperty("RenameToolStripMenuItem")]
	private ToolStripMenuItem _RenameToolStripMenuItem;

	[AccessedThroughProperty("ResetNameToolStripMenuItem")]
	private ToolStripMenuItem _ResetNameToolStripMenuItem;

	[AccessedThroughProperty("CancelUpdateToolStripMenuItem")]
	private ToolStripMenuItem _CancelUpdateToolStripMenuItem;

	[AccessedThroughProperty("EnableAllToolStripMenuItem")]
	private ToolStripMenuItem _EnableAllToolStripMenuItem;

	[AccessedThroughProperty("DisableAllToolStripMenuItem")]
	private ToolStripMenuItem _DisableAllToolStripMenuItem;

	[AccessedThroughProperty("myEmptyContextMenu")]
	private ContextMenuStrip _myEmptyContextMenu;

	[AccessedThroughProperty("SelectAllToolStripMenuItem")]
	private ToolStripMenuItem _SelectAllToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem11")]
	private ToolStripSeparator _ToolStripMenuItem11;

	[AccessedThroughProperty("ExportAss2zToolStripMenuItem")]
	private ToolStripMenuItem _ExportAss2zToolStripMenuItem;

	[AccessedThroughProperty("ToolStripMenuItem12")]
	private ToolStripSeparator _ToolStripMenuItem12;

	[AccessedThroughProperty("RegisterhonmodFileExtensionToolStripMenuItem")]
	private ToolStripMenuItem _RegisterhonmodFileExtensionToolStripMenuItem;

	private const string VersionString = "1.4.0.0";

	private const int IconWidth = 48;

	private const int IconHeight = 48;

	private string GameDir;

	private string ModsDir;

	private string GameVersion;

	private string RunGameFile;

	private string RunGameArguments;

	private ZipFile[] resourceFiles;

	private List<Modification> Mods;

	private int EnabledCount;

	private Dictionary<string, string> DisplayNames;

	private List<ModUpdater> ModUpdaters;

	private bool UpdatingMode;

	private static Dictionary<string, ZipFile> OpenZIPs = new Dictionary<string, ZipFile>();

	private bool FirstActivation;

	private Dictionary<string, string> EnabledMods;

	private Dictionary<string, string> AppliedMods;

	private string AppliedGameVersion;

	internal virtual ListView myListView
	{
		get
		{
			return _myListView;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			LabelEditEventHandler value2 = myListView_AfterLabelEdit;
			KeyEventHandler value3 = myListView_KeyDown;
			EventHandler value4 = myListView_DoubleClick;
			EventHandler value5 = myListView_SelectedIndexChanged;
			if (_myListView != null)
			{
				_myListView.AfterLabelEdit -= value2;
				_myListView.KeyDown -= value3;
				_myListView.DoubleClick -= value4;
				_myListView.SelectedIndexChanged -= value5;
			}
			_myListView = value;
			if (_myListView != null)
			{
				_myListView.AfterLabelEdit += value2;
				_myListView.KeyDown += value3;
				_myListView.DoubleClick += value4;
				_myListView.SelectedIndexChanged += value5;
			}
		}
	}

	internal virtual ImageList myImageList
	{
		get
		{
			return _myImageList;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myImageList = value;
		}
	}

	internal virtual Label lblName
	{
		get
		{
			return _lblName;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_lblName = value;
		}
	}

	internal virtual Button cmdToggleDisabled
	{
		get
		{
			return _cmdToggleDisabled;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = cmdToggleDisabled_Click;
			if (_cmdToggleDisabled != null)
			{
				_cmdToggleDisabled.Click -= value2;
			}
			_cmdToggleDisabled = value;
			if (_cmdToggleDisabled != null)
			{
				_cmdToggleDisabled.Click += value2;
			}
		}
	}

	internal virtual LinkLabel lblDescription
	{
		get
		{
			return _lblDescription;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			LinkLabelLinkClickedEventHandler value2 = lblDescription_LinkClicked;
			if (_lblDescription != null)
			{
				_lblDescription.LinkClicked -= value2;
			}
			_lblDescription = value;
			if (_lblDescription != null)
			{
				_lblDescription.LinkClicked += value2;
			}
		}
	}

	internal virtual MenuStrip myMenuStrip
	{
		get
		{
			return _myMenuStrip;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myMenuStrip = value;
		}
	}

	internal virtual StatusStrip myStatusStrip
	{
		get
		{
			return _myStatusStrip;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myStatusStrip = value;
		}
	}

	internal virtual ToolStripMenuItem OptionsToolStripMenuItem
	{
		get
		{
			return _OptionsToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_OptionsToolStripMenuItem = value;
		}
	}

	internal virtual ToolStripMenuItem ChangeHoNPathToolStripMenuItem
	{
		get
		{
			return _ChangeHoNPathToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ChangeHoNPathToolStripMenuItem_Click;
			if (_ChangeHoNPathToolStripMenuItem != null)
			{
				_ChangeHoNPathToolStripMenuItem.Click -= value2;
			}
			_ChangeHoNPathToolStripMenuItem = value;
			if (_ChangeHoNPathToolStripMenuItem != null)
			{
				_ChangeHoNPathToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripStatusLabel myStatusLabel
	{
		get
		{
			return _myStatusLabel;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myStatusLabel = value;
		}
	}

	internal virtual ToolStripStatusLabel myInfoLabel
	{
		get
		{
			return _myInfoLabel;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myInfoLabel = value;
		}
	}

	internal virtual ToolStripStatusLabel myAppVerLabel
	{
		get
		{
			return _myAppVerLabel;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myAppVerLabel = value;
		}
	}

	internal virtual ToolStripMenuItem FileToolStripMenuItem
	{
		get
		{
			return _FileToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_FileToolStripMenuItem = value;
		}
	}

	internal virtual ToolStripMenuItem ApplyModsToolStripMenuItem
	{
		get
		{
			return _ApplyModsToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ApplyModsToolStripMenuItem_Click;
			if (_ApplyModsToolStripMenuItem != null)
			{
				_ApplyModsToolStripMenuItem.Click -= value2;
			}
			_ApplyModsToolStripMenuItem = value;
			if (_ApplyModsToolStripMenuItem != null)
			{
				_ApplyModsToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem1
	{
		get
		{
			return _ToolStripMenuItem1;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem1 = value;
		}
	}

	internal virtual ToolStripMenuItem ExitToolStripMenuItem
	{
		get
		{
			return _ExitToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ExitToolStripMenuItem_Click;
			if (_ExitToolStripMenuItem != null)
			{
				_ExitToolStripMenuItem.Click -= value2;
			}
			_ExitToolStripMenuItem = value;
			if (_ExitToolStripMenuItem != null)
			{
				_ExitToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem HelpToolStripMenuItem
	{
		get
		{
			return _HelpToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_HelpToolStripMenuItem = value;
		}
	}

	internal virtual ToolStripMenuItem ForumThreadToolStripMenuItem
	{
		get
		{
			return _ForumThreadToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ForumThreadToolStripMenuItem_Click;
			if (_ForumThreadToolStripMenuItem != null)
			{
				_ForumThreadToolStripMenuItem.Click -= value2;
			}
			_ForumThreadToolStripMenuItem = value;
			if (_ForumThreadToolStripMenuItem != null)
			{
				_ForumThreadToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem2
	{
		get
		{
			return _ToolStripMenuItem2;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem2 = value;
		}
	}

	internal virtual ToolStripMenuItem AboutToolStripMenuItem
	{
		get
		{
			return _AboutToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = AboutToolStripMenuItem_Click;
			if (_AboutToolStripMenuItem != null)
			{
				_AboutToolStripMenuItem.Click -= value2;
			}
			_AboutToolStripMenuItem = value;
			if (_AboutToolStripMenuItem != null)
			{
				_AboutToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual Label lblDisabled
	{
		get
		{
			return _lblDisabled;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_lblDisabled = value;
		}
	}

	internal virtual ToolStripMenuItem ApplyModsAndLaunchHoNToolStripMenuItem
	{
		get
		{
			return _ApplyModsAndLaunchHoNToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ApplyModsAndLaunchHoNToolStripMenuItem_Click;
			if (_ApplyModsAndLaunchHoNToolStripMenuItem != null)
			{
				_ApplyModsAndLaunchHoNToolStripMenuItem.Click -= value2;
			}
			_ApplyModsAndLaunchHoNToolStripMenuItem = value;
			if (_ApplyModsAndLaunchHoNToolStripMenuItem != null)
			{
				_ApplyModsAndLaunchHoNToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem EnterHoNPathmanuallyToolStripMenuItem
	{
		get
		{
			return _EnterHoNPathmanuallyToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = EnterHoNPathmanuallyToolStripMenuItem_Click;
			if (_EnterHoNPathmanuallyToolStripMenuItem != null)
			{
				_EnterHoNPathmanuallyToolStripMenuItem.Click -= value2;
			}
			_EnterHoNPathmanuallyToolStripMenuItem = value;
			if (_EnterHoNPathmanuallyToolStripMenuItem != null)
			{
				_EnterHoNPathmanuallyToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ContextMenuStrip myContextMenu
	{
		get
		{
			return _myContextMenu;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			CancelEventHandler value2 = myContextMenu_Opening;
			if (_myContextMenu != null)
			{
				_myContextMenu.Opening -= value2;
			}
			_myContextMenu = value;
			if (_myContextMenu != null)
			{
				_myContextMenu.Opening += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem EnableDisableToolStripMenuItem
	{
		get
		{
			return _EnableDisableToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = EnableDisableToolStripMenuItem_Click;
			if (_EnableDisableToolStripMenuItem != null)
			{
				_EnableDisableToolStripMenuItem.Click -= value2;
			}
			_EnableDisableToolStripMenuItem = value;
			if (_EnableDisableToolStripMenuItem != null)
			{
				_EnableDisableToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem3
	{
		get
		{
			return _ToolStripMenuItem3;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem3 = value;
		}
	}

	internal virtual ToolStripMenuItem UpdateThisModToolStripMenuItem
	{
		get
		{
			return _UpdateThisModToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = UpdateThisModToolStripMenuItem_Click;
			if (_UpdateThisModToolStripMenuItem != null)
			{
				_UpdateThisModToolStripMenuItem.Click -= value2;
			}
			_UpdateThisModToolStripMenuItem = value;
			if (_UpdateThisModToolStripMenuItem != null)
			{
				_UpdateThisModToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem4
	{
		get
		{
			return _ToolStripMenuItem4;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem4 = value;
		}
	}

	internal virtual ToolStripMenuItem DeleteToolStripMenuItem
	{
		get
		{
			return _DeleteToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = DeleteToolStripMenuItem_Click;
			if (_DeleteToolStripMenuItem != null)
			{
				_DeleteToolStripMenuItem.Click -= value2;
			}
			_DeleteToolStripMenuItem = value;
			if (_DeleteToolStripMenuItem != null)
			{
				_DeleteToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem OpenModFolderToolStripMenuItem
	{
		get
		{
			return _OpenModFolderToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = OpenModFolderToolStripMenuItem_Click;
			if (_OpenModFolderToolStripMenuItem != null)
			{
				_OpenModFolderToolStripMenuItem.Click -= value2;
			}
			_OpenModFolderToolStripMenuItem = value;
			if (_OpenModFolderToolStripMenuItem != null)
			{
				_OpenModFolderToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem6
	{
		get
		{
			return _ToolStripMenuItem6;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem6 = value;
		}
	}

	internal virtual ToolStripMenuItem ViewToolStripMenuItem
	{
		get
		{
			return _ViewToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ViewToolStripMenuItem = value;
		}
	}

	internal virtual ToolStripMenuItem RefreshModDisplayToolStripMenuItem
	{
		get
		{
			return _RefreshModDisplayToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = RefreshModDisplayToolStripMenuItem_Click;
			if (_RefreshModDisplayToolStripMenuItem != null)
			{
				_RefreshModDisplayToolStripMenuItem.Click -= value2;
			}
			_RefreshModDisplayToolStripMenuItem = value;
			if (_RefreshModDisplayToolStripMenuItem != null)
			{
				_RefreshModDisplayToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem UpdateAllModsToolStripMenuItem
	{
		get
		{
			return _UpdateAllModsToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = UpdateAllModsToolStripMenuItem_Click;
			if (_UpdateAllModsToolStripMenuItem != null)
			{
				_UpdateAllModsToolStripMenuItem.Click -= value2;
			}
			_UpdateAllModsToolStripMenuItem = value;
			if (_UpdateAllModsToolStripMenuItem != null)
			{
				_UpdateAllModsToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem5
	{
		get
		{
			return _ToolStripMenuItem5;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem5 = value;
		}
	}

	internal virtual ToolStripMenuItem UnapplyAllModsToolStripMenuItem
	{
		get
		{
			return _UnapplyAllModsToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = UnapplyAllModsToolStripMenuItem_Click;
			if (_UnapplyAllModsToolStripMenuItem != null)
			{
				_UnapplyAllModsToolStripMenuItem.Click -= value2;
			}
			_UnapplyAllModsToolStripMenuItem = value;
			if (_UnapplyAllModsToolStripMenuItem != null)
			{
				_UnapplyAllModsToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem CancelAllUpdatesToolStripMenuItem
	{
		get
		{
			return _CancelAllUpdatesToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = CancelAllUpdatesToolStripMenuItem_Click;
			if (_CancelAllUpdatesToolStripMenuItem != null)
			{
				_CancelAllUpdatesToolStripMenuItem.Click -= value2;
			}
			_CancelAllUpdatesToolStripMenuItem = value;
			if (_CancelAllUpdatesToolStripMenuItem != null)
			{
				_CancelAllUpdatesToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual System.Windows.Forms.Timer myUpdatingTimer
	{
		get
		{
			return _myUpdatingTimer;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = myUpdatingTimer_Tick;
			if (_myUpdatingTimer != null)
			{
				_myUpdatingTimer.Tick -= value2;
			}
			_myUpdatingTimer = value;
			if (_myUpdatingTimer != null)
			{
				_myUpdatingTimer.Tick += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem ListToolStripMenuItem
	{
		get
		{
			return _ListToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ListToolStripMenuItem_Click;
			if (_ListToolStripMenuItem != null)
			{
				_ListToolStripMenuItem.Click -= value2;
			}
			_ListToolStripMenuItem = value;
			if (_ListToolStripMenuItem != null)
			{
				_ListToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem TilesToolStripMenuItem
	{
		get
		{
			return _TilesToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = TilesToolStripMenuItem_Click;
			if (_TilesToolStripMenuItem != null)
			{
				_TilesToolStripMenuItem.Click -= value2;
			}
			_TilesToolStripMenuItem = value;
			if (_TilesToolStripMenuItem != null)
			{
				_TilesToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem SmallIconsToolStripMenuItem
	{
		get
		{
			return _SmallIconsToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = SmallIconsToolStripMenuItem_Click;
			if (_SmallIconsToolStripMenuItem != null)
			{
				_SmallIconsToolStripMenuItem.Click -= value2;
			}
			_SmallIconsToolStripMenuItem = value;
			if (_SmallIconsToolStripMenuItem != null)
			{
				_SmallIconsToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem7
	{
		get
		{
			return _ToolStripMenuItem7;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem7 = value;
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem8
	{
		get
		{
			return _ToolStripMenuItem8;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem8 = value;
		}
	}

	internal virtual ToolStripMenuItem CLArgumentsForLaunchingHoNToolStripMenuItem
	{
		get
		{
			return _CLArgumentsForLaunchingHoNToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = CLArgumentsForLaunchingHoNToolStripMenuItem_Click;
			if (_CLArgumentsForLaunchingHoNToolStripMenuItem != null)
			{
				_CLArgumentsForLaunchingHoNToolStripMenuItem.Click -= value2;
			}
			_CLArgumentsForLaunchingHoNToolStripMenuItem = value;
			if (_CLArgumentsForLaunchingHoNToolStripMenuItem != null)
			{
				_CLArgumentsForLaunchingHoNToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem9
	{
		get
		{
			return _ToolStripMenuItem9;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem9 = value;
		}
	}

	internal virtual ToolStripMenuItem ShowVersionsInMainViewToolStripMenuItem
	{
		get
		{
			return _ShowVersionsInMainViewToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ShowVersionsInMainViewToolStripMenuItem_Click;
			if (_ShowVersionsInMainViewToolStripMenuItem != null)
			{
				_ShowVersionsInMainViewToolStripMenuItem.Click -= value2;
			}
			_ShowVersionsInMainViewToolStripMenuItem = value;
			if (_ShowVersionsInMainViewToolStripMenuItem != null)
			{
				_ShowVersionsInMainViewToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem10
	{
		get
		{
			return _ToolStripMenuItem10;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem10 = value;
		}
	}

	internal virtual ToolStripMenuItem RenameToolStripMenuItem
	{
		get
		{
			return _RenameToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = RenameToolStripMenuItem_Click;
			if (_RenameToolStripMenuItem != null)
			{
				_RenameToolStripMenuItem.Click -= value2;
			}
			_RenameToolStripMenuItem = value;
			if (_RenameToolStripMenuItem != null)
			{
				_RenameToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem ResetNameToolStripMenuItem
	{
		get
		{
			return _ResetNameToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ResetNameToolStripMenuItem_Click;
			if (_ResetNameToolStripMenuItem != null)
			{
				_ResetNameToolStripMenuItem.Click -= value2;
			}
			_ResetNameToolStripMenuItem = value;
			if (_ResetNameToolStripMenuItem != null)
			{
				_ResetNameToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem CancelUpdateToolStripMenuItem
	{
		get
		{
			return _CancelUpdateToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = CancelUpdateToolStripMenuItem_Click;
			if (_CancelUpdateToolStripMenuItem != null)
			{
				_CancelUpdateToolStripMenuItem.Click -= value2;
			}
			_CancelUpdateToolStripMenuItem = value;
			if (_CancelUpdateToolStripMenuItem != null)
			{
				_CancelUpdateToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem EnableAllToolStripMenuItem
	{
		get
		{
			return _EnableAllToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = EnableAllToolStripMenuItem_Click;
			if (_EnableAllToolStripMenuItem != null)
			{
				_EnableAllToolStripMenuItem.Click -= value2;
			}
			_EnableAllToolStripMenuItem = value;
			if (_EnableAllToolStripMenuItem != null)
			{
				_EnableAllToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripMenuItem DisableAllToolStripMenuItem
	{
		get
		{
			return _DisableAllToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = DisableAllToolStripMenuItem_Click;
			if (_DisableAllToolStripMenuItem != null)
			{
				_DisableAllToolStripMenuItem.Click -= value2;
			}
			_DisableAllToolStripMenuItem = value;
			if (_DisableAllToolStripMenuItem != null)
			{
				_DisableAllToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ContextMenuStrip myEmptyContextMenu
	{
		get
		{
			return _myEmptyContextMenu;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_myEmptyContextMenu = value;
		}
	}

	internal virtual ToolStripMenuItem SelectAllToolStripMenuItem
	{
		get
		{
			return _SelectAllToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = SelectAllToolStripMenuItem_Click;
			if (_SelectAllToolStripMenuItem != null)
			{
				_SelectAllToolStripMenuItem.Click -= value2;
			}
			_SelectAllToolStripMenuItem = value;
			if (_SelectAllToolStripMenuItem != null)
			{
				_SelectAllToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem11
	{
		get
		{
			return _ToolStripMenuItem11;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem11 = value;
		}
	}

	internal virtual ToolStripMenuItem ExportAss2zToolStripMenuItem
	{
		get
		{
			return _ExportAss2zToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = ExportAss2zToolStripMenuItem_Click;
			if (_ExportAss2zToolStripMenuItem != null)
			{
				_ExportAss2zToolStripMenuItem.Click -= value2;
			}
			_ExportAss2zToolStripMenuItem = value;
			if (_ExportAss2zToolStripMenuItem != null)
			{
				_ExportAss2zToolStripMenuItem.Click += value2;
			}
		}
	}

	internal virtual ToolStripSeparator ToolStripMenuItem12
	{
		get
		{
			return _ToolStripMenuItem12;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			_ToolStripMenuItem12 = value;
		}
	}

	internal virtual ToolStripMenuItem RegisterhonmodFileExtensionToolStripMenuItem
	{
		get
		{
			return _RegisterhonmodFileExtensionToolStripMenuItem;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			EventHandler value2 = RegisterhonmodFileExtensionToolStripMenuItem_Click;
			if (_RegisterhonmodFileExtensionToolStripMenuItem != null)
			{
				_RegisterhonmodFileExtensionToolStripMenuItem.Click -= value2;
			}
			_RegisterhonmodFileExtensionToolStripMenuItem = value;
			if (_RegisterhonmodFileExtensionToolStripMenuItem != null)
			{
				_RegisterhonmodFileExtensionToolStripMenuItem.Click += value2;
			}
		}
	}

	public frmMain()
	{
		base.Activated += frmMain_Activated;
		base.FormClosing += frmMain_FormClosing;
		base.DragEnter += frmMain_DragEnter;
		base.DragDrop += frmMain_DragDrop;
		base.KeyDown += frmMain_KeyDown;
		base.Load += frmMain_Load;
		GameVersion = "";
		RunGameFile = "";
		RunGameArguments = "";
		resourceFiles = new ZipFile[2];
		Mods = new List<Modification>();
		DisplayNames = new Dictionary<string, string>();
		ModUpdaters = new List<ModUpdater>();
		UpdatingMode = false;
		FirstActivation = true;
		InitializeComponent();
	}

	[STAThread]
	public static void Main()
	{
		Application.Run(MyProject.Forms.frmMain);
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
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HoN_ModMan.frmMain));
		this.myListView = new System.Windows.Forms.ListView();
		this.myContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.EnableDisableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.EnableAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.DisableAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem3 = new System.Windows.Forms.ToolStripSeparator();
		this.UpdateThisModToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.CancelUpdateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem11 = new System.Windows.Forms.ToolStripSeparator();
		this.ExportAss2zToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem10 = new System.Windows.Forms.ToolStripSeparator();
		this.RenameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ResetNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem4 = new System.Windows.Forms.ToolStripSeparator();
		this.DeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.myImageList = new System.Windows.Forms.ImageList(this.components);
		this.lblName = new System.Windows.Forms.Label();
		this.cmdToggleDisabled = new System.Windows.Forms.Button();
		this.lblDescription = new System.Windows.Forms.LinkLabel();
		this.myMenuStrip = new System.Windows.Forms.MenuStrip();
		this.FileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ApplyModsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ApplyModsAndLaunchHoNToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.UnapplyAllModsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
		this.UpdateAllModsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.CancelAllUpdatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem5 = new System.Windows.Forms.ToolStripSeparator();
		this.OpenModFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem6 = new System.Windows.Forms.ToolStripSeparator();
		this.ExitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ListToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.SmallIconsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.TilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem7 = new System.Windows.Forms.ToolStripSeparator();
		this.ShowVersionsInMainViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem9 = new System.Windows.Forms.ToolStripSeparator();
		this.RefreshModDisplayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.OptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ChangeHoNPathToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.EnterHoNPathmanuallyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem8 = new System.Windows.Forms.ToolStripSeparator();
		this.CLArgumentsForLaunchingHoNToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.HelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ForumThreadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
		this.AboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.myStatusStrip = new System.Windows.Forms.StatusStrip();
		this.myStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
		this.myAppVerLabel = new System.Windows.Forms.ToolStripStatusLabel();
		this.myInfoLabel = new System.Windows.Forms.ToolStripStatusLabel();
		this.lblDisabled = new System.Windows.Forms.Label();
		this.myUpdatingTimer = new System.Windows.Forms.Timer(this.components);
		this.myEmptyContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.SelectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.ToolStripMenuItem12 = new System.Windows.Forms.ToolStripSeparator();
		this.RegisterhonmodFileExtensionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.myContextMenu.SuspendLayout();
		this.myMenuStrip.SuspendLayout();
		this.myStatusStrip.SuspendLayout();
		this.myEmptyContextMenu.SuspendLayout();
		this.SuspendLayout();
		this.myListView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.myListView.ContextMenuStrip = this.myContextMenu;
		this.myListView.HideSelection = false;
		this.myListView.LargeImageList = this.myImageList;
		System.Windows.Forms.ListView listView = this.myListView;
		System.Drawing.Point location = new System.Drawing.Point(0, 24);
		listView.Location = location;
		this.myListView.Name = "myListView";
		System.Windows.Forms.ListView listView2 = this.myListView;
		System.Drawing.Size size = new System.Drawing.Size(383, 375);
		listView2.Size = size;
		this.myListView.SmallImageList = this.myImageList;
		this.myListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
		this.myListView.TabIndex = 0;
		System.Windows.Forms.ListView listView3 = this.myListView;
		size = new System.Drawing.Size(200, 56);
		listView3.TileSize = size;
		this.myListView.UseCompatibleStateImageBehavior = false;
		this.myListView.View = System.Windows.Forms.View.List;
		this.myContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[13]
		{
			this.EnableDisableToolStripMenuItem, this.EnableAllToolStripMenuItem, this.DisableAllToolStripMenuItem, this.ToolStripMenuItem3, this.UpdateThisModToolStripMenuItem, this.CancelUpdateToolStripMenuItem, this.ToolStripMenuItem11, this.ExportAss2zToolStripMenuItem, this.ToolStripMenuItem10, this.RenameToolStripMenuItem,
			this.ResetNameToolStripMenuItem, this.ToolStripMenuItem4, this.DeleteToolStripMenuItem
		});
		this.myContextMenu.Name = "myContextMenu";
		System.Windows.Forms.ContextMenuStrip contextMenuStrip = this.myContextMenu;
		size = new System.Drawing.Size(192, 226);
		contextMenuStrip.Size = size;
		this.EnableDisableToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.EnableDisableToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.EnableDisableToolStripMenuItem.Name = "EnableDisableToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem enableDisableToolStripMenuItem = this.EnableDisableToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		enableDisableToolStripMenuItem.Size = size;
		this.EnableDisableToolStripMenuItem.Text = "En&able / Disable";
		this.EnableAllToolStripMenuItem.Name = "EnableAllToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem enableAllToolStripMenuItem = this.EnableAllToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		enableAllToolStripMenuItem.Size = size;
		this.EnableAllToolStripMenuItem.Text = "&Enable these Mods";
		this.EnableAllToolStripMenuItem.Visible = false;
		this.DisableAllToolStripMenuItem.Name = "DisableAllToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem disableAllToolStripMenuItem = this.DisableAllToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		disableAllToolStripMenuItem.Size = size;
		this.DisableAllToolStripMenuItem.Text = "&Disable these Mods";
		this.DisableAllToolStripMenuItem.Visible = false;
		this.ToolStripMenuItem3.Name = "ToolStripMenuItem3";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem = this.ToolStripMenuItem3;
		size = new System.Drawing.Size(188, 6);
		toolStripMenuItem.Size = size;
		this.UpdateThisModToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.UpdateThisModToolStripMenuItem.Name = "UpdateThisModToolStripMenuItem";
		this.UpdateThisModToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.U | System.Windows.Forms.Keys.Control;
		System.Windows.Forms.ToolStripMenuItem updateThisModToolStripMenuItem = this.UpdateThisModToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		updateThisModToolStripMenuItem.Size = size;
		this.UpdateThisModToolStripMenuItem.Text = "&Update this Mod";
		this.CancelUpdateToolStripMenuItem.Name = "CancelUpdateToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem cancelUpdateToolStripMenuItem = this.CancelUpdateToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		cancelUpdateToolStripMenuItem.Size = size;
		this.CancelUpdateToolStripMenuItem.Text = "Cancel &Update";
		this.CancelUpdateToolStripMenuItem.Visible = false;
		this.ToolStripMenuItem11.Name = "ToolStripMenuItem11";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem2 = this.ToolStripMenuItem11;
		size = new System.Drawing.Size(188, 6);
		toolStripMenuItem2.Size = size;
		this.ExportAss2zToolStripMenuItem.Name = "ExportAss2zToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem exportAss2zToolStripMenuItem = this.ExportAss2zToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		exportAss2zToolStripMenuItem.Size = size;
		this.ExportAss2zToolStripMenuItem.Text = "Export as .s2&z ...";
		this.ToolStripMenuItem10.Name = "ToolStripMenuItem10";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem3 = this.ToolStripMenuItem10;
		size = new System.Drawing.Size(188, 6);
		toolStripMenuItem3.Size = size;
		this.RenameToolStripMenuItem.Name = "RenameToolStripMenuItem";
		this.RenameToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
		System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem = this.RenameToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		renameToolStripMenuItem.Size = size;
		this.RenameToolStripMenuItem.Text = "Re&name";
		this.ResetNameToolStripMenuItem.Name = "ResetNameToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem resetNameToolStripMenuItem = this.ResetNameToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		resetNameToolStripMenuItem.Size = size;
		this.ResetNameToolStripMenuItem.Text = "&Reset Name";
		this.ResetNameToolStripMenuItem.Visible = false;
		this.ToolStripMenuItem4.Name = "ToolStripMenuItem4";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem4 = this.ToolStripMenuItem4;
		size = new System.Drawing.Size(188, 6);
		toolStripMenuItem4.Size = size;
		this.DeleteToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.DeleteToolStripMenuItem.Name = "DeleteToolStripMenuItem";
		this.DeleteToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
		System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem = this.DeleteToolStripMenuItem;
		size = new System.Drawing.Size(191, 22);
		deleteToolStripMenuItem.Size = size;
		this.DeleteToolStripMenuItem.Text = "&Delete";
		this.myImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
		System.Windows.Forms.ImageList imageList = this.myImageList;
		size = new System.Drawing.Size(48, 48);
		imageList.ImageSize = size;
		this.myImageList.TransparentColor = System.Drawing.Color.Transparent;
		this.lblName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.lblName.AutoSize = true;
		this.lblName.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Bold);
		System.Windows.Forms.Label label = this.lblName;
		location = new System.Drawing.Point(389, 30);
		label.Location = location;
		this.lblName.Name = "lblName";
		System.Windows.Forms.Label label2 = this.lblName;
		size = new System.Drawing.Size(0, 13);
		label2.Size = size;
		this.lblName.TabIndex = 1;
		this.lblName.UseMnemonic = false;
		this.cmdToggleDisabled.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		System.Windows.Forms.Button button = this.cmdToggleDisabled;
		location = new System.Drawing.Point(480, 359);
		button.Location = location;
		this.cmdToggleDisabled.Name = "cmdToggleDisabled";
		System.Windows.Forms.Button button2 = this.cmdToggleDisabled;
		size = new System.Drawing.Size(79, 35);
		button2.Size = size;
		this.cmdToggleDisabled.TabIndex = 4;
		this.cmdToggleDisabled.UseVisualStyleBackColor = true;
		this.cmdToggleDisabled.Visible = false;
		this.lblDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		System.Windows.Forms.LinkLabel linkLabel = this.lblDescription;
		location = new System.Drawing.Point(389, 46);
		linkLabel.Location = location;
		this.lblDescription.Name = "lblDescription";
		System.Windows.Forms.LinkLabel linkLabel2 = this.lblDescription;
		size = new System.Drawing.Size(170, 313);
		linkLabel2.Size = size;
		this.lblDescription.TabIndex = 2;
		this.lblDescription.UseMnemonic = false;
		System.Windows.Forms.MenuStrip menuStrip = this.myMenuStrip;
		System.Windows.Forms.Padding gripMargin = new System.Windows.Forms.Padding(2);
		menuStrip.GripMargin = gripMargin;
		this.myMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.FileToolStripMenuItem, this.ViewToolStripMenuItem, this.OptionsToolStripMenuItem, this.HelpToolStripMenuItem });
		System.Windows.Forms.MenuStrip menuStrip2 = this.myMenuStrip;
		location = new System.Drawing.Point(0, 0);
		menuStrip2.Location = location;
		this.myMenuStrip.Name = "myMenuStrip";
		System.Windows.Forms.MenuStrip menuStrip3 = this.myMenuStrip;
		size = new System.Drawing.Size(564, 24);
		menuStrip3.Size = size;
		this.myMenuStrip.TabIndex = 6;
		this.FileToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.FileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[10] { this.ApplyModsToolStripMenuItem, this.ApplyModsAndLaunchHoNToolStripMenuItem, this.UnapplyAllModsToolStripMenuItem, this.ToolStripMenuItem1, this.UpdateAllModsToolStripMenuItem, this.CancelAllUpdatesToolStripMenuItem, this.ToolStripMenuItem5, this.OpenModFolderToolStripMenuItem, this.ToolStripMenuItem6, this.ExitToolStripMenuItem });
		this.FileToolStripMenuItem.Name = "FileToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem = this.FileToolStripMenuItem;
		size = new System.Drawing.Size(35, 20);
		fileToolStripMenuItem.Size = size;
		this.FileToolStripMenuItem.Text = "&File";
		this.ApplyModsToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ApplyModsToolStripMenuItem.Name = "ApplyModsToolStripMenuItem";
		this.ApplyModsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control;
		System.Windows.Forms.ToolStripMenuItem applyModsToolStripMenuItem = this.ApplyModsToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		applyModsToolStripMenuItem.Size = size;
		this.ApplyModsToolStripMenuItem.Text = "&Apply Mods";
		this.ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = false;
		this.ApplyModsAndLaunchHoNToolStripMenuItem.Name = "ApplyModsAndLaunchHoNToolStripMenuItem";
		this.ApplyModsAndLaunchHoNToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt;
		System.Windows.Forms.ToolStripMenuItem applyModsAndLaunchHoNToolStripMenuItem = this.ApplyModsAndLaunchHoNToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		applyModsAndLaunchHoNToolStripMenuItem.Size = size;
		this.ApplyModsAndLaunchHoNToolStripMenuItem.Text = "Apply Mods and &Launch HoN";
		this.UnapplyAllModsToolStripMenuItem.Name = "UnapplyAllModsToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem unapplyAllModsToolStripMenuItem = this.UnapplyAllModsToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		unapplyAllModsToolStripMenuItem.Size = size;
		this.UnapplyAllModsToolStripMenuItem.Text = "U&napply All Mods";
		this.ToolStripMenuItem1.Name = "ToolStripMenuItem1";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem5 = this.ToolStripMenuItem1;
		size = new System.Drawing.Size(266, 6);
		toolStripMenuItem5.Size = size;
		this.UpdateAllModsToolStripMenuItem.Name = "UpdateAllModsToolStripMenuItem";
		this.UpdateAllModsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.U | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.Control;
		System.Windows.Forms.ToolStripMenuItem updateAllModsToolStripMenuItem = this.UpdateAllModsToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		updateAllModsToolStripMenuItem.Size = size;
		this.UpdateAllModsToolStripMenuItem.Text = "Download Mod &Updates";
		this.CancelAllUpdatesToolStripMenuItem.Name = "CancelAllUpdatesToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem cancelAllUpdatesToolStripMenuItem = this.CancelAllUpdatesToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		cancelAllUpdatesToolStripMenuItem.Size = size;
		this.CancelAllUpdatesToolStripMenuItem.Text = "&Cancel All Updates";
		this.CancelAllUpdatesToolStripMenuItem.Visible = false;
		this.ToolStripMenuItem5.Name = "ToolStripMenuItem5";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem6 = this.ToolStripMenuItem5;
		size = new System.Drawing.Size(266, 6);
		toolStripMenuItem6.Size = size;
		this.OpenModFolderToolStripMenuItem.Name = "OpenModFolderToolStripMenuItem";
		this.OpenModFolderToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F | System.Windows.Forms.Keys.Control;
		System.Windows.Forms.ToolStripMenuItem openModFolderToolStripMenuItem = this.OpenModFolderToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		openModFolderToolStripMenuItem.Size = size;
		this.OpenModFolderToolStripMenuItem.Text = "&Open Mod Folder";
		this.ToolStripMenuItem6.Name = "ToolStripMenuItem6";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem7 = this.ToolStripMenuItem6;
		size = new System.Drawing.Size(266, 6);
		toolStripMenuItem7.Size = size;
		this.ExitToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem = this.ExitToolStripMenuItem;
		size = new System.Drawing.Size(269, 22);
		exitToolStripMenuItem.Size = size;
		this.ExitToolStripMenuItem.Text = "E&xit";
		this.ViewToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ViewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[7] { this.ListToolStripMenuItem, this.SmallIconsToolStripMenuItem, this.TilesToolStripMenuItem, this.ToolStripMenuItem7, this.ShowVersionsInMainViewToolStripMenuItem, this.ToolStripMenuItem9, this.RefreshModDisplayToolStripMenuItem });
		this.ViewToolStripMenuItem.Name = "ViewToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem = this.ViewToolStripMenuItem;
		size = new System.Drawing.Size(41, 20);
		viewToolStripMenuItem.Size = size;
		this.ViewToolStripMenuItem.Text = "&View";
		this.ListToolStripMenuItem.Checked = true;
		this.ListToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
		this.ListToolStripMenuItem.Name = "ListToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem listToolStripMenuItem = this.ListToolStripMenuItem;
		size = new System.Drawing.Size(204, 22);
		listToolStripMenuItem.Size = size;
		this.ListToolStripMenuItem.Text = "&Vertical List";
		this.SmallIconsToolStripMenuItem.Name = "SmallIconsToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem smallIconsToolStripMenuItem = this.SmallIconsToolStripMenuItem;
		size = new System.Drawing.Size(204, 22);
		smallIconsToolStripMenuItem.Size = size;
		this.SmallIconsToolStripMenuItem.Text = "&Horizontal List";
		this.TilesToolStripMenuItem.Name = "TilesToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem tilesToolStripMenuItem = this.TilesToolStripMenuItem;
		size = new System.Drawing.Size(204, 22);
		tilesToolStripMenuItem.Size = size;
		this.TilesToolStripMenuItem.Text = "&Tiles";
		this.ToolStripMenuItem7.Name = "ToolStripMenuItem7";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem8 = this.ToolStripMenuItem7;
		size = new System.Drawing.Size(201, 6);
		toolStripMenuItem8.Size = size;
		this.ShowVersionsInMainViewToolStripMenuItem.Name = "ShowVersionsInMainViewToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem showVersionsInMainViewToolStripMenuItem = this.ShowVersionsInMainViewToolStripMenuItem;
		size = new System.Drawing.Size(204, 22);
		showVersionsInMainViewToolStripMenuItem.Size = size;
		this.ShowVersionsInMainViewToolStripMenuItem.Text = "Show Vers&ions in Main View";
		this.ToolStripMenuItem9.Name = "ToolStripMenuItem9";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem9 = this.ToolStripMenuItem9;
		size = new System.Drawing.Size(201, 6);
		toolStripMenuItem9.Size = size;
		this.RefreshModDisplayToolStripMenuItem.Name = "RefreshModDisplayToolStripMenuItem";
		this.RefreshModDisplayToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5;
		System.Windows.Forms.ToolStripMenuItem refreshModDisplayToolStripMenuItem = this.RefreshModDisplayToolStripMenuItem;
		size = new System.Drawing.Size(204, 22);
		refreshModDisplayToolStripMenuItem.Size = size;
		this.RefreshModDisplayToolStripMenuItem.Text = "&Refresh Mod Display";
		this.OptionsToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.OptionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[6] { this.ChangeHoNPathToolStripMenuItem, this.EnterHoNPathmanuallyToolStripMenuItem, this.ToolStripMenuItem8, this.CLArgumentsForLaunchingHoNToolStripMenuItem, this.ToolStripMenuItem12, this.RegisterhonmodFileExtensionToolStripMenuItem });
		this.OptionsToolStripMenuItem.Name = "OptionsToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem = this.OptionsToolStripMenuItem;
		gripMargin = new System.Windows.Forms.Padding(0);
		optionsToolStripMenuItem.Padding = gripMargin;
		System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem2 = this.OptionsToolStripMenuItem;
		size = new System.Drawing.Size(48, 20);
		optionsToolStripMenuItem2.Size = size;
		this.OptionsToolStripMenuItem.Text = "&Options";
		this.ChangeHoNPathToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ChangeHoNPathToolStripMenuItem.Name = "ChangeHoNPathToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem changeHoNPathToolStripMenuItem = this.ChangeHoNPathToolStripMenuItem;
		size = new System.Drawing.Size(244, 22);
		changeHoNPathToolStripMenuItem.Size = size;
		this.ChangeHoNPathToolStripMenuItem.Text = "Change HoN &Path ...";
		this.EnterHoNPathmanuallyToolStripMenuItem.Name = "EnterHoNPathmanuallyToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem enterHoNPathmanuallyToolStripMenuItem = this.EnterHoNPathmanuallyToolStripMenuItem;
		size = new System.Drawing.Size(244, 22);
		enterHoNPathmanuallyToolStripMenuItem.Size = size;
		this.EnterHoNPathmanuallyToolStripMenuItem.Text = "Enter HoN Path &manually ...";
		this.ToolStripMenuItem8.Name = "ToolStripMenuItem8";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem10 = this.ToolStripMenuItem8;
		size = new System.Drawing.Size(241, 6);
		toolStripMenuItem10.Size = size;
		this.CLArgumentsForLaunchingHoNToolStripMenuItem.Name = "CLArgumentsForLaunchingHoNToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem cLArgumentsForLaunchingHoNToolStripMenuItem = this.CLArgumentsForLaunchingHoNToolStripMenuItem;
		size = new System.Drawing.Size(244, 22);
		cLArgumentsForLaunchingHoNToolStripMenuItem.Size = size;
		this.CLArgumentsForLaunchingHoNToolStripMenuItem.Text = "CL Arguments for launching HoN ...";
		this.HelpToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.HelpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.ForumThreadToolStripMenuItem, this.ToolStripMenuItem2, this.AboutToolStripMenuItem });
		this.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem = this.HelpToolStripMenuItem;
		size = new System.Drawing.Size(40, 20);
		helpToolStripMenuItem.Size = size;
		this.HelpToolStripMenuItem.Text = "&Help";
		this.ForumThreadToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ForumThreadToolStripMenuItem.Name = "ForumThreadToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem forumThreadToolStripMenuItem = this.ForumThreadToolStripMenuItem;
		size = new System.Drawing.Size(213, 22);
		forumThreadToolStripMenuItem.Size = size;
		this.ForumThreadToolStripMenuItem.Text = "&Visit Website (Forum Thread)";
		this.ToolStripMenuItem2.Name = "ToolStripMenuItem2";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem11 = this.ToolStripMenuItem2;
		size = new System.Drawing.Size(210, 6);
		toolStripMenuItem11.Size = size;
		this.AboutToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem = this.AboutToolStripMenuItem;
		size = new System.Drawing.Size(213, 22);
		aboutToolStripMenuItem.Size = size;
		this.AboutToolStripMenuItem.Text = "&About";
		this.myStatusStrip.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.myStatusStrip.AutoSize = false;
		this.myStatusStrip.Dock = System.Windows.Forms.DockStyle.None;
		this.myStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.myStatusLabel, this.myAppVerLabel, this.myInfoLabel });
		System.Windows.Forms.StatusStrip statusStrip = this.myStatusStrip;
		location = new System.Drawing.Point(0, 399);
		statusStrip.Location = location;
		this.myStatusStrip.Name = "myStatusStrip";
		System.Windows.Forms.StatusStrip statusStrip2 = this.myStatusStrip;
		size = new System.Drawing.Size(564, 20);
		statusStrip2.Size = size;
		this.myStatusStrip.TabIndex = 5;
		this.myStatusLabel.AutoSize = false;
		this.myStatusLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.All;
		this.myStatusLabel.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter;
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel = this.myStatusLabel;
		gripMargin = new System.Windows.Forms.Padding(0, 2, 0, 0);
		toolStripStatusLabel.Margin = gripMargin;
		this.myStatusLabel.Name = "myStatusLabel";
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2 = this.myStatusLabel;
		size = new System.Drawing.Size(541, 18);
		toolStripStatusLabel2.Size = size;
		this.myStatusLabel.Spring = true;
		this.myStatusLabel.Text = "Ready.";
		this.myStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.myAppVerLabel.ActiveLinkColor = System.Drawing.Color.Red;
		this.myAppVerLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.All;
		this.myAppVerLabel.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter;
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel3 = this.myAppVerLabel;
		gripMargin = new System.Windows.Forms.Padding(0, 2, 0, 0);
		toolStripStatusLabel3.Margin = gripMargin;
		this.myAppVerLabel.Name = "myAppVerLabel";
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel4 = this.myAppVerLabel;
		size = new System.Drawing.Size(4, 18);
		toolStripStatusLabel4.Size = size;
		this.myInfoLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.All;
		this.myInfoLabel.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter;
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel5 = this.myInfoLabel;
		gripMargin = new System.Windows.Forms.Padding(0, 2, 0, 0);
		toolStripStatusLabel5.Margin = gripMargin;
		this.myInfoLabel.Name = "myInfoLabel";
		System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel6 = this.myInfoLabel;
		size = new System.Drawing.Size(4, 18);
		toolStripStatusLabel6.Size = size;
		this.myInfoLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.lblDisabled.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.lblDisabled.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		System.Windows.Forms.Label label3 = this.lblDisabled;
		location = new System.Drawing.Point(389, 359);
		label3.Location = location;
		this.lblDisabled.Name = "lblDisabled";
		System.Windows.Forms.Label label4 = this.lblDisabled;
		size = new System.Drawing.Size(85, 35);
		label4.Size = size;
		this.lblDisabled.TabIndex = 3;
		this.lblDisabled.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.lblDisabled.UseMnemonic = false;
		this.lblDisabled.Visible = false;
		this.myEmptyContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.SelectAllToolStripMenuItem });
		this.myEmptyContextMenu.Name = "myEmptyContextMenu";
		System.Windows.Forms.ContextMenuStrip contextMenuStrip2 = this.myEmptyContextMenu;
		size = new System.Drawing.Size(157, 26);
		contextMenuStrip2.Size = size;
		this.SelectAllToolStripMenuItem.Name = "SelectAllToolStripMenuItem";
		this.SelectAllToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.A | System.Windows.Forms.Keys.Control;
		System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem = this.SelectAllToolStripMenuItem;
		size = new System.Drawing.Size(156, 22);
		selectAllToolStripMenuItem.Size = size;
		this.SelectAllToolStripMenuItem.Text = "Select &All";
		this.ToolStripMenuItem12.Name = "ToolStripMenuItem12";
		System.Windows.Forms.ToolStripSeparator toolStripMenuItem12 = this.ToolStripMenuItem12;
		size = new System.Drawing.Size(241, 6);
		toolStripMenuItem12.Size = size;
		this.RegisterhonmodFileExtensionToolStripMenuItem.Name = "RegisterhonmodFileExtensionToolStripMenuItem";
		System.Windows.Forms.ToolStripMenuItem registerhonmodFileExtensionToolStripMenuItem = this.RegisterhonmodFileExtensionToolStripMenuItem;
		size = new System.Drawing.Size(244, 22);
		registerhonmodFileExtensionToolStripMenuItem.Size = size;
		this.RegisterhonmodFileExtensionToolStripMenuItem.Text = "Register .honmod File Extension";
		this.AllowDrop = true;
		System.Drawing.SizeF sizeF = new System.Drawing.SizeF(6f, 13f);
		this.AutoScaleDimensions = sizeF;
		this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		size = new System.Drawing.Size(564, 418);
		this.ClientSize = size;
		this.Controls.Add(this.lblDisabled);
		this.Controls.Add(this.myStatusStrip);
		this.Controls.Add(this.cmdToggleDisabled);
		this.Controls.Add(this.lblName);
		this.Controls.Add(this.myListView);
		this.Controls.Add(this.lblDescription);
		this.Controls.Add(this.myMenuStrip);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f);
		this.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		this.KeyPreview = true;
		this.MainMenuStrip = this.myMenuStrip;
		size = new System.Drawing.Size(446, 165);
		this.MinimumSize = size;
		this.Name = "frmMain";
		this.Text = "HoN Mod Manager";
		this.myContextMenu.ResumeLayout(false);
		this.myMenuStrip.ResumeLayout(false);
		this.myMenuStrip.PerformLayout();
		this.myStatusStrip.ResumeLayout(false);
		this.myStatusStrip.PerformLayout();
		this.myEmptyContextMenu.ResumeLayout(false);
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	private void EnterUpdatingMode()
	{
		RefreshModDisplayToolStripMenuItem.Enabled = false;
		ChangeHoNPathToolStripMenuItem.Enabled = false;
		EnterHoNPathmanuallyToolStripMenuItem.Enabled = false;
		ApplyModsToolStripMenuItem.Enabled = false;
		ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = false;
		DeleteToolStripMenuItem.Enabled = false;
		UpdatingMode = true;
		CancelAllUpdatesToolStripMenuItem.Visible = true;
		ForGetAllZIPs();
		myUpdatingTimer.Start();
	}

	private void LeaveUpdatingMode()
	{
		myUpdatingTimer.Stop();
		CancelAllUpdatesToolStripMenuItem.Visible = false;
		UpdatingMode = false;
		RefreshModDisplayToolStripMenuItem.Enabled = true;
		ChangeHoNPathToolStripMenuItem.Enabled = true;
		EnterHoNPathmanuallyToolStripMenuItem.Enabled = true;
		ApplyModsToolStripMenuItem.Enabled = true;
		ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = Operators.CompareString(RunGameFile, "", TextCompare: false) != 0;
		DeleteToolStripMenuItem.Enabled = true;
		myStatusLabel.Text = "Updating complete.";
		checked
		{
			if (ModUpdaters.Count > 0)
			{
				ModUpdater[] array = ModUpdaters.ToArray();
				string[] array2 = new string[ModUpdaters.Count - 1 + 1];
				int num = 0;
				foreach (ModUpdater modUpdater2 in ModUpdaters)
				{
					array2[num] = modUpdater2.SortKey;
					num++;
				}
				Array.Sort(array2, array);
				string text = "";
				string left = "";
				ModUpdater[] array3 = array;
				foreach (ModUpdater modUpdater in array3)
				{
					if (modUpdater.UpdateDownloaded)
					{
						if (Operators.CompareString(text, "", TextCompare: false) != 0)
						{
							text += Environment.NewLine;
						}
						text = text + "- " + modUpdater.Mod.Name + ": " + modUpdater.StatusString;
						continue;
					}
					if (Operators.CompareString(left, modUpdater.StatusString, TextCompare: false) != 0)
					{
						if (Operators.CompareString(text, "", TextCompare: false) != 0)
						{
							text = text + Environment.NewLine + Environment.NewLine;
						}
						text = text + modUpdater.StatusString + ":";
						left = modUpdater.StatusString;
					}
					text = text + Environment.NewLine + "- " + modUpdater.Mod.Name;
				}
				ModUpdaters.Clear();
				foreach (Modification mod in Mods)
				{
					mod.Updater = null;
				}
				MessageBox.Show(text, "Update Report", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				UpdateList();
			}
			else
			{
				MessageBox.Show("None of your mods are updatable.", "Update Report", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}
	}

	private static Bitmap UpdatingIcon(Bitmap b)
	{
		Graphics.FromImage(b).DrawImageUnscaled(Resources.updating, 0, 0);
		return b;
	}

	private static string GetRegistryEntry(string name)
	{
		string result;
		try
		{
			RegistryKey currentUser = Registry.CurrentUser;
			currentUser = currentUser.OpenSubKey("Software");
			if (currentUser == null)
			{
				result = "";
			}
			else
			{
				currentUser = currentUser.OpenSubKey("Notausgang");
				if (currentUser == null)
				{
					result = "";
				}
				else
				{
					currentUser = currentUser.OpenSubKey("HoN_ModMan");
					if (currentUser == null)
					{
						result = "";
					}
					else
					{
						object objectValue = RuntimeHelpers.GetObjectValue(currentUser.GetValue(name, ""));
						currentUser.Close();
						result = (string)objectValue;
					}
				}
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			result = "";
			ProjectData.ClearProjectError();
		}
		return result;
	}

	private static void SetRegistryEntry(string name, string value)
	{
		RegistryKey currentUser = Registry.CurrentUser;
		currentUser = currentUser.CreateSubKey("Software");
		if (currentUser == null)
		{
			return;
		}
		currentUser = currentUser.CreateSubKey("Notausgang");
		if (currentUser != null)
		{
			currentUser = currentUser.CreateSubKey("HoN_ModMan");
			if (currentUser != null)
			{
				currentUser.SetValue(name, value);
				currentUser.Close();
			}
		}
	}

	private static ZipFile GetZip(string Name)
	{
		if (OpenZIPs.ContainsKey(Name))
		{
			return OpenZIPs[Name];
		}
		ZipFile zipFile = ZipFile.Read(Name);
		if (zipFile != null)
		{
			OpenZIPs.Add(Name, zipFile);
		}
		return zipFile;
	}

	private static void ForGetZip(string Name)
	{
		if (OpenZIPs.ContainsKey(Name))
		{
			OpenZIPs[Name].Dispose();
			OpenZIPs.Remove(Name);
		}
	}

	private static void ForGetAllZIPs()
	{
		foreach (KeyValuePair<string, ZipFile> openZIP in OpenZIPs)
		{
			openZIP.Value.Dispose();
		}
		OpenZIPs.Clear();
	}

	private static Stream GetZippedFile(ZipFile z, string Filename)
	{
		ZipEntry zipEntry = z[Filename];
		if (zipEntry == null)
		{
			return null;
		}
		MemoryStream memoryStream = new MemoryStream();
		zipEntry.Extract(memoryStream);
		memoryStream.Seek(0L, SeekOrigin.Begin);
		return memoryStream;
	}

	private void initResources()
	{
		ZipFile[] array = new ZipFile[2];
		short num = 0;
		checked
		{
			while (true)
			{
				string text = Path.Combine(Path.Combine(GameDir, "game"), "resources" + Conversions.ToString(unchecked((int)num)) + ".s2z");
				if (!MyProject.Computer.FileSystem.FileExists(text))
				{
					break;
				}
				array = (ZipFile[])Utils.CopyArray(array, new ZipFile[num + 1]);
				array[num] = GetZip(text);
				num = (short)unchecked(num + 1);
			}
			resourceFiles = new ZipFile[num + 1];
			resourceFiles = array;
		}
	}

	private Stream getFileFromResources(string Filename)
	{
		Stream stream = null;
		for (int i = resourceFiles.GetUpperBound(0); i >= 0; i = checked(i + -1))
		{
			ZipFile z = resourceFiles[i];
			stream = GetZippedFile(z, Filename);
			if (stream != null)
			{
				break;
			}
		}
		return stream;
	}

	private void frmMain_KeyDown(object sender, KeyEventArgs e)
	{
		if (!((e.KeyCode == Keys.A) & e.Control))
		{
			return;
		}
		foreach (ListViewItem item in myListView.Items)
		{
			item.Selected = true;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private void frmMain_Load(object sender, EventArgs e)
	{
		GameDir = GetRegistryEntry("hondir");
		int i;
		if (Operators.CompareString(GameDir, "", TextCompare: false) != 0)
		{
			SetModsDir();
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			int upperBound = commandLineArgs.GetUpperBound(0);
			for (i = 1; i <= upperBound; i = checked(i + 1))
			{
				InstallMod(commandLineArgs[i]);
			}
		}
		try
		{
			if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
			{
				ProjectData.EndApp();
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
		}
		string registryEntry = GetRegistryEntry("showversions");
		ShowVersionsInMainViewToolStripMenuItem.Checked = Operators.CompareString(registryEntry, "yes", TextCompare: false) == 0;
		ReadDisplayNames();
		SetGameDir(DetectGameDir());
		registryEntry = GetRegistryEntry("left");
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			string s = registryEntry;
			int result = Left;
			int.TryParse(s, out result);
			Left = result;
		}
		registryEntry = GetRegistryEntry("top");
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			string s2 = registryEntry;
			int result = Top;
			int.TryParse(s2, out result);
			Top = result;
		}
		registryEntry = GetRegistryEntry("width");
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			string s3 = registryEntry;
			int result = Width;
			int.TryParse(s3, out result);
			Width = result;
		}
		registryEntry = GetRegistryEntry("height");
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			string s4 = registryEntry;
			int result = Height;
			int.TryParse(s4, out result);
			Height = result;
		}
		i = -1;
		registryEntry = GetRegistryEntry("view");
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			int.TryParse(registryEntry, out i);
		}
		switch (i)
		{
		case 3:
			myListView.View = View.List;
			ListToolStripMenuItem.Checked = true;
			TilesToolStripMenuItem.Checked = false;
			SmallIconsToolStripMenuItem.Checked = false;
			break;
		case 2:
			myListView.View = View.SmallIcon;
			ListToolStripMenuItem.Checked = false;
			TilesToolStripMenuItem.Checked = false;
			SmallIconsToolStripMenuItem.Checked = true;
			break;
		case 0:
		case 4:
			myListView.View = View.Tile;
			ListToolStripMenuItem.Checked = false;
			TilesToolStripMenuItem.Checked = true;
			SmallIconsToolStripMenuItem.Checked = false;
			break;
		}
		RunGameArguments = GetRegistryEntry("clargs");
		if ((Operators.CompareString(GameDir, "", TextCompare: false) != 0) & (Environment.OSVersion.Platform == PlatformID.Win32NT))
		{
			switch (GetRegistryEntry("fileextension"))
			{
			case "yes":
				RegisterFileExtension();
				break;
			case null:
			case "":
				if (MessageBox.Show("Do you want to associate the .honmod file extension with the HoN Mod Manager?" + Environment.NewLine + Environment.NewLine + "You can change this setting in the Options menu at any time.", "HoN_ModMan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					RegisterFileExtension();
				}
				else
				{
					SetRegistryEntry("fileextension", "no");
				}
				break;
			}
		}
		else
		{
			RegisterhonmodFileExtensionToolStripMenuItem.Enabled = false;
		}
	}

	private void frmMain_Activated(object sender, EventArgs e)
	{
		if (!FirstActivation)
		{
			return;
		}
		FirstActivation = false;
		if (((Operators.CompareString(GameVersion, "", TextCompare: false) != 0) & (Operators.CompareString(AppliedGameVersion, "", TextCompare: false) != 0) & (Operators.CompareString(GameVersion, AppliedGameVersion, TextCompare: false) != 0)) && DialogResult.Yes == MessageBox.Show("The HoN install was patched since you last applied the mods. For the mods to work correctly you need to apply them again. Do you want to do that right now?", "Game Patch Detected", MessageBoxButtons.YesNo, MessageBoxIcon.Question) && ApplyMods(SilentSuccess: true) && DialogResult.Yes == MessageBox.Show("Great Success! Launch HoN now?", "Success", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk))
		{
			try
			{
				Environment.CurrentDirectory = GameDir;
				Process.Start(RunGameFile, RunGameArguments);
				Close();
			}
			catch (Exception ex)
			{
				ProjectData.SetProjectError(ex);
				Exception ex2 = ex;
				MessageBox.Show("Could not launch HoN:" + Environment.NewLine + Environment.NewLine + ex2.Message, "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				ProjectData.ClearProjectError();
			}
		}
	}

	private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
	{
		SetRegistryEntry("hondir", GameDir);
		SetRegistryEntry("clargs", RunGameArguments);
		SetRegistryEntry("view", Convert.ToString(Convert.ToInt32((int)myListView.View)));
		if (ShowVersionsInMainViewToolStripMenuItem.Checked)
		{
			SetRegistryEntry("showversions", "yes");
		}
		else
		{
			SetRegistryEntry("showversions", "no");
		}
		StoreDisplayNames();
		if (WindowState == FormWindowState.Normal)
		{
			SetRegistryEntry("left", Left.ToString());
			SetRegistryEntry("top", Top.ToString());
			SetRegistryEntry("width", Width.ToString());
			SetRegistryEntry("height", Height.ToString());
		}
		if (e.CloseReason != CloseReason.UserClosing || DeepCompareDictionary(AppliedMods, EnabledMods))
		{
			return;
		}
		switch (MessageBox.Show("The enabled mods don't match the applied mods - do you want to apply mods now?", "Save Changes?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
		{
		case DialogResult.Yes:
			if (!ApplyMods())
			{
				e.Cancel = true;
			}
			break;
		case DialogResult.Cancel:
			e.Cancel = true;
			break;
		}
	}

	private void ReadDisplayNames()
	{
		DisplayNames.Clear();
		string[] array = GetRegistryEntry("displaynames").Split(Convert.ToChar(10));
		if (array.Length % 2 != 0)
		{
			return;
		}
		int upperBound = array.GetUpperBound(0);
		checked
		{
			for (int i = 0; i <= upperBound; i += 2)
			{
				array[i + 1] = array[i + 1].Trim();
				if (Operators.CompareString(array[i + 1], "", TextCompare: false) != 0)
				{
					DisplayNames[array[i]] = array[i + 1];
				}
			}
		}
	}

	private void StoreDisplayNames()
	{
		string text = "";
		bool flag = true;
		foreach (KeyValuePair<string, string> displayName in DisplayNames)
		{
			if (!flag)
			{
				text += Conversions.ToString(Convert.ToChar(10));
			}
			text = text + displayName.Key + Conversions.ToString(Convert.ToChar(10)) + displayName.Value;
			flag = false;
		}
		SetRegistryEntry("displaynames", text);
	}

	private void SetGameDir(string NewGameDir)
	{
		GameDir = NewGameDir;
		SetModsDir();
		GetAppliedMods();
		EnabledMods = DeepCopyDictionary(AppliedMods);
		GetGameVersion();
		UpdateList();
	}

	private void SetModsDir()
	{
		if (IsLinux())
		{
			ModsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".Heroes of Newerth/game");
		}
		else if (IsMacOS())
		{
			ModsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support/Heroes of Newerth/game");
		}
		else
		{
			ModsDir = Path.Combine(GameDir, "game");
		}
	}

	private static Dictionary<string, string> DeepCopyDictionary(Dictionary<string, string> Old)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, string> item in Old)
		{
			dictionary.Add(item.Key, item.Value);
		}
		return dictionary;
	}

	private static bool DeepCompareDictionary(Dictionary<string, string> A, Dictionary<string, string> B)
	{
		foreach (KeyValuePair<string, string> item in A)
		{
			if (!B.ContainsKey(item.Key) || Operators.CompareString(B[item.Key], item.Value, TextCompare: false) != 0)
			{
				return false;
			}
		}
		foreach (KeyValuePair<string, string> item2 in B)
		{
			if (!A.ContainsKey(item2.Key) || Operators.CompareString(A[item2.Key], item2.Value, TextCompare: false) != 0)
			{
				return false;
			}
		}
		return true;
	}

	private void GetAppliedMods()
	{
		AppliedMods = new Dictionary<string, string>();
		AppliedGameVersion = "";
		if (Operators.CompareString(GameDir, "", TextCompare: false) == 0)
		{
			return;
		}
		ZipFile zipFile;
		try
		{
			zipFile = ZipFile.Read(Path.Combine(ModsDir, "resources999.s2z"));
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
			return;
		}
		if (zipFile == null)
		{
			return;
		}
		checked
		{
			try
			{
				string[] array = zipFile.Comment.Replace(Conversions.ToString(Convert.ToChar(13)), "").Split(Convert.ToChar(10));
				if (!(array[0].StartsWith("HoN Mod Manager v") & array[0].EndsWith(" Output") & (Operators.CompareString(array[1], "", TextCompare: false) == 0)))
				{
					return;
				}
				if (array[2].StartsWith("Game Version: "))
				{
					AppliedGameVersion = array[2].Substring("Game Version: ".Length);
				}
				int i;
				for (i = 2; Operators.CompareString(array[i], "Applied Mods: ", TextCompare: false) != 0; i++)
				{
				}
				i++;
				do
				{
					int num = array[i].LastIndexOf(" (v");
					if ((num >= 0) & array[i].EndsWith(")"))
					{
						AppliedMods[FixModName(array[i].Substring(0, num))] = array[i].Substring(num + 3, array[i].Length - (num + 3) - 1);
					}
					i++;
				}
				while (i != array.Length);
			}
			catch (Exception projectError2)
			{
				ProjectData.SetProjectError(projectError2);
				ProjectData.ClearProjectError();
			}
			finally
			{
				zipFile.Dispose();
			}
		}
	}

	private void UpdateEnabledCountLabel()
	{
		if (Operators.CompareString(GameDir, "", TextCompare: false) == 0)
		{
			myInfoLabel.Text = "";
			myInfoLabel.Visible = false;
		}
		else
		{
			myInfoLabel.Text = Conversions.ToString(EnabledCount) + "/" + Mods.Count + " mods enabled";
			myInfoLabel.Visible = true;
		}
	}

	private void UnapplyAllModsToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string path = Path.Combine(ModsDir, "resources999.s2z");
		if (!File.Exists(path))
		{
			MessageBox.Show("Currently no mods are applied!", "Unapply All Mods", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		else
		{
			if (MessageBox.Show("Are you sure?", "Unapply All Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			File.Delete(path);
			AppliedMods.Clear();
			EnabledMods.Clear();
			foreach (Modification mod in Mods)
			{
				mod.Disabled = true;
				if (mod.Icon != null)
				{
					Bitmap bitmap = new Bitmap(mod.Icon);
					if (mod.Disabled)
					{
						DisableIcon(bitmap);
					}
					if (mod.IsUpdating())
					{
						UpdatingIcon(bitmap);
					}
					myImageList.Images[mod.ImageListIdx] = bitmap;
				}
			}
			foreach (ListViewItem item in myListView.Items)
			{
				if (item.ImageIndex == 1)
				{
					item.ImageIndex = 2;
				}
				if (item.ImageIndex == -1)
				{
					item.ImageIndex = 0;
				}
			}
			myListView.Refresh();
			EnabledCount = 0;
			UpdateEnabledCountLabel();
			myListView_SelectedIndexChanged(null, null);
		}
	}

	private void UpdateStatusLabel()
	{
		if (Operators.CompareString(GameDir, "", TextCompare: false) == 0)
		{
			myStatusLabel.Text = "Not attached to a valid HoN install.";
		}
		else if (UpdatingMode)
		{
			int num = 0;
			foreach (ModUpdater modUpdater in ModUpdaters)
			{
				if (modUpdater.Status >= ModUpdater.UpdateStatus.NoUpdateInformation)
				{
					num = checked(num + 1);
				}
			}
			myStatusLabel.Text = "Updating... (" + Conversions.ToString(num) + " of " + Conversions.ToString(ModUpdaters.Count) + " done)";
		}
		else
		{
			myStatusLabel.Text = "Ready.";
		}
	}

	private void UpdateStatusLabel(Modification tMod)
	{
		if (tMod.IsUpdating())
		{
			myStatusLabel.Text = tMod.Name + " v" + tMod.Version + " [Update progress: " + tMod.Updater.StatusString + "]";
		}
		else
		{
			myStatusLabel.Text = tMod.Name + " v" + tMod.Version;
		}
	}

	private void UpdateThisModToolStripMenuItem_Click(object sender, EventArgs e)
	{
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			Modification modification = (Modification)selectedItem.Tag;
			if (!((Operators.CompareString(modification.UpdateCheck, "", TextCompare: false) != 0) & (Operators.CompareString(modification.UpdateDownload, "", TextCompare: false) != 0)))
			{
				continue;
			}
			if (!UpdatingMode)
			{
				EnterUpdatingMode();
			}
			if (!(!modification.IsUpdating() & (modification.Updater == null)))
			{
				continue;
			}
			if (modification.Icon != null)
			{
				Bitmap bitmap = new Bitmap(modification.Icon);
				if (modification.Disabled)
				{
					DisableIcon(bitmap);
				}
				UpdatingIcon(bitmap);
				myImageList.Images[modification.ImageListIdx] = bitmap;
			}
			else if (modification.Disabled)
			{
				selectedItem.ImageIndex = 2;
			}
			else
			{
				selectedItem.ImageIndex = 1;
			}
			myListView.RedrawItems(selectedItem.Index, selectedItem.Index, invalidateOnly: true);
			modification.Updater = new ModUpdater(modification);
			if (myListView.SelectedItems.Count == 1)
			{
				UpdateStatusLabel(modification);
			}
			ModUpdaters.Add(modification.Updater);
		}
	}

	private void CancelUpdateToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (!UpdatingMode)
		{
			return;
		}
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			Modification modification = (Modification)selectedItem.Tag;
			if (((Operators.CompareString(modification.UpdateCheck, "", TextCompare: false) != 0) & (Operators.CompareString(modification.UpdateDownload, "", TextCompare: false) != 0)) && modification.IsUpdating())
			{
				modification.Updater.Abort();
			}
		}
	}

	private void myUpdatingTimer_Tick(object sender, EventArgs e)
	{
		if (!UpdatingMode)
		{
			return;
		}
		bool flag = false;
		foreach (ModUpdater modUpdater in ModUpdaters)
		{
			if (modUpdater.Status < ModUpdater.UpdateStatus.NoUpdateInformation)
			{
				flag = true;
			}
			else
			{
				if (modUpdater.Reaped)
				{
					continue;
				}
				Modification mod = modUpdater.Mod;
				int count = myListView.Items.Count;
				int i;
				for (i = 0; i <= count && myListView.Items[i].Tag != mod; i = checked(i + 1))
				{
				}
				if (mod.Icon != null)
				{
					Bitmap bitmap = new Bitmap(mod.Icon);
					if (mod.Disabled)
					{
						DisableIcon(bitmap);
					}
					myImageList.Images[mod.ImageListIdx] = bitmap;
				}
				else if (i < myListView.Items.Count)
				{
					if (mod.Disabled)
					{
						myListView.Items[i].ImageIndex = 0;
					}
					else
					{
						myListView.Items[i].ImageIndex = -1;
					}
				}
				if (i < myListView.Items.Count)
				{
					myListView.RedrawItems(i, i, invalidateOnly: true);
				}
				modUpdater.Reaped = true;
			}
		}
		if (!flag)
		{
			LeaveUpdatingMode();
		}
		else if (myListView.SelectedItems.Count == 1)
		{
			UpdateStatusLabel((Modification)myListView.SelectedItems[0].Tag);
		}
		else
		{
			UpdateStatusLabel();
		}
	}

	private void UpdateAllModsToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (!UpdatingMode)
		{
			EnterUpdatingMode();
		}
		foreach (Modification mod in Mods)
		{
			if (!((Operators.CompareString(mod.UpdateCheck, "", TextCompare: false) != 0) & (Operators.CompareString(mod.UpdateDownload, "", TextCompare: false) != 0) & (mod.Updater == null)))
			{
				continue;
			}
			if (mod.Icon != null)
			{
				Bitmap bitmap = new Bitmap(mod.Icon);
				if (mod.Disabled)
				{
					DisableIcon(bitmap);
				}
				UpdatingIcon(bitmap);
				myImageList.Images[mod.ImageListIdx] = bitmap;
			}
			else
			{
				int count = myListView.Items.Count;
				int i;
				for (i = 0; i <= count && myListView.Items[i].Tag != mod; i = checked(i + 1))
				{
				}
				if (mod.Disabled)
				{
					myListView.Items[i].ImageIndex = 2;
				}
				else
				{
					myListView.Items[i].ImageIndex = 1;
				}
			}
			mod.Updater = new ModUpdater(mod);
			ModUpdaters.Add(mod.Updater);
		}
		myListView.Refresh();
		UpdateStatusLabel();
	}

	private void CancelAllUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
	{
		foreach (ModUpdater modUpdater in ModUpdaters)
		{
			modUpdater.Abort();
		}
	}

	private void myListView_DoubleClick(object sender, EventArgs e)
	{
		if (cmdToggleDisabled.Visible & cmdToggleDisabled.Enabled)
		{
			cmdToggleDisabled_Click(null, null);
		}
	}

	private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
	{
		foreach (ListViewItem item in myListView.Items)
		{
			item.Selected = true;
		}
	}

	private void myListView_KeyDown(object sender, KeyEventArgs e)
	{
		if ((e.KeyCode == Keys.Return) & cmdToggleDisabled.Visible & cmdToggleDisabled.Enabled)
		{
			cmdToggleDisabled_Click(null, null);
		}
	}

	private void myListView_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (myListView.SelectedItems.Count != 1)
		{
			lblName.Text = "";
			lblDescription.Text = "";
			lblDisabled.Visible = false;
			cmdToggleDisabled.Visible = false;
			if (myListView.SelectedItems.Count == 0)
			{
				UpdateStatusLabel();
			}
			else
			{
				myStatusLabel.Text = Conversions.ToString(myListView.SelectedItems.Count) + " mods selected.";
			}
			return;
		}
		Modification modification = (Modification)myListView.SelectedItems[0].Tag;
		lblName.Text = modification.Name;
		lblDescription.Text = "";
		LinkLabel linkLabel;
		if (Operators.CompareString(modification.Author, "", TextCompare: false) != 0)
		{
			linkLabel = lblDescription;
			linkLabel.Text = linkLabel.Text + "by " + modification.Author + Environment.NewLine;
		}
		linkLabel = lblDescription;
		linkLabel.Text = linkLabel.Text + Environment.NewLine + modification.Description;
		if (Operators.CompareString(modification.WebLink, "", TextCompare: false) != 0)
		{
			linkLabel = lblDescription;
			linkLabel.Text = linkLabel.Text + Environment.NewLine + Environment.NewLine + "Visit Website";
			LinkLabel linkLabel2 = lblDescription;
			LinkArea linkArea = new LinkArea(checked(lblDescription.Text.Length - "Visit Website".Length), "Visit Website".Length);
			linkLabel2.LinkArea = linkArea;
		}
		else
		{
			LinkLabel linkLabel3 = lblDescription;
			LinkArea linkArea = new LinkArea(0, 0);
			linkLabel3.LinkArea = linkArea;
		}
		if (modification.Disabled)
		{
			lblDisabled.Text = "This mod is disabled.";
			lblDisabled.ForeColor = Color.Red;
			cmdToggleDisabled.Text = "&Enable";
		}
		else
		{
			lblDisabled.Text = "This mod is enabled.";
			lblDisabled.ForeColor = Color.Green;
			cmdToggleDisabled.Text = "&Disable";
		}
		lblDisabled.Visible = true;
		cmdToggleDisabled.Visible = true;
		UpdateStatusLabel(modification);
	}

	private void cmdToggleDisabled_Click(object sender, EventArgs e)
	{
		if (myListView.SelectedItems.Count == 1)
		{
			if (((Modification)myListView.SelectedItems[0].Tag).Disabled)
			{
				EnableSelected();
			}
			else
			{
				DisableSelected();
			}
		}
	}

	private void EnableSelected()
	{
		List<ListViewItem> list = new List<ListViewItem>();
		List<ListViewItem> list2 = new List<ListViewItem>();
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			list.Add(selectedItem);
		}
		checked
		{
			bool flag;
			do
			{
				foreach (ListViewItem item in list2)
				{
					list.Remove(item);
				}
				list2.Clear();
				flag = false;
				foreach (ListViewItem item2 in list)
				{
					Modification modification = (Modification)item2.Tag;
					if (modification.Enabled)
					{
						continue;
					}
					if (!IsNewerVersion(modification.MMVersion, "1.4.0.0"))
					{
						if (myListView.SelectedItems.Count != 1)
						{
							continue;
						}
						MessageBox.Show("This mod was written for HoN Mod Manager v" + modification.MMVersion + " or newer. You can obtain the newest version by visiting the website (see \"Help\" menu).", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
						return;
					}
					if (Operators.CompareString(GameVersion, "", TextCompare: false) != 0 && Operators.CompareString(modification.AppVersion, "", TextCompare: false) != 0 && !VersionsMatch(modification.AppVersion, GameVersion))
					{
						if (myListView.SelectedItems.Count != 1)
						{
							continue;
						}
						MessageBox.Show("This mod was written for Heroes of Newerth v" + modification.AppVersion + " and is thus not compatible with your current install.", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
						return;
					}
					bool flag2 = true;
					foreach (KeyValuePair<string, string> requirement in modification.Requirements)
					{
						flag2 = false;
						foreach (Modification mod in Mods)
						{
							if (Operators.CompareString(mod.FixedName, requirement.Key, TextCompare: false) == 0 && (mod.Enabled & VersionsMatch(requirement.Value.Substring(0, requirement.Value.IndexOf(' ')), mod.Version)))
							{
								flag2 = true;
								break;
							}
						}
						if (!flag2)
						{
							if (myListView.SelectedItems.Count == 1)
							{
								string text = requirement.Value.Substring(0, requirement.Value.IndexOf(' '));
								if (Operators.CompareString(text, "", TextCompare: false) != 0)
								{
									text = " v" + text;
								}
								MessageBox.Show("This mod requires \"" + requirement.Value.Substring(requirement.Value.IndexOf(' ') + 1) + "\"" + text + " to be present and enabled." + Environment.NewLine + "Visit this mod's website to find out where to obtain the required mod.", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
								return;
							}
							break;
						}
					}
					if (!flag2)
					{
						continue;
					}
					flag2 = false;
					foreach (KeyValuePair<string, string> incompatibility in modification.Incompatibilities)
					{
						foreach (Modification mod2 in Mods)
						{
							if ((mod2.Enabled & (Operators.CompareString(mod2.FixedName, incompatibility.Key, TextCompare: false) == 0)) && VersionsMatch(incompatibility.Value.Substring(0, incompatibility.Value.IndexOf(' ')), mod2.Version))
							{
								if (myListView.SelectedItems.Count == 1)
								{
									string text2 = incompatibility.Value.Substring(0, incompatibility.Value.IndexOf(' '));
									if (Operators.CompareString(text2, "", TextCompare: false) != 0)
									{
										text2 = " v" + text2;
									}
									MessageBox.Show("This mod is incompatible with \"" + incompatibility.Value.Substring(incompatibility.Value.IndexOf(' ') + 1) + "\"" + text2 + ". You cannot have both enabled at the same time.", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
									return;
								}
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
					flag2 = false;
					foreach (Modification mod3 in Mods)
					{
						if (!mod3.Enabled)
						{
							continue;
						}
						foreach (KeyValuePair<string, string> incompatibility2 in mod3.Incompatibilities)
						{
							if (Operators.CompareString(modification.FixedName, incompatibility2.Key, TextCompare: false) == 0 && VersionsMatch(incompatibility2.Value.Substring(0, incompatibility2.Value.IndexOf(' ')), modification.Version))
							{
								if (myListView.SelectedItems.Count == 1)
								{
									MessageBox.Show("This mod is incompatible with \"" + mod3.Name + "\" v" + mod3.Version + ". You cannot have both enabled at the same time.", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
									return;
								}
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
					modification.Enabled = true;
					Modification modification2 = FindCycle();
					modification.Enabled = false;
					if (modification2 != null)
					{
						if (myListView.SelectedItems.Count != 1)
						{
							continue;
						}
						MessageBox.Show("Enabling this mod would create a cycle in mod priorities. Try disabling other mods modifying similar aspects if you want to enable this one.", "Cannot enable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
						return;
					}
					flag = true;
					list2.Add(item2);
					EnabledMods.Add(modification.FixedName, modification.Version);
					modification.Disabled = false;
					item2.ForeColor = SystemColors.WindowText;
					if (modification.Icon != null)
					{
						Bitmap bitmap = new Bitmap(modification.Icon);
						if (modification.IsUpdating())
						{
							UpdatingIcon(bitmap);
						}
						myImageList.Images[modification.ImageListIdx] = bitmap;
					}
					else if (modification.IsUpdating())
					{
						item2.ImageIndex = 1;
					}
					else
					{
						item2.ImageIndex = -1;
					}
					if (myListView.SelectedItems.Count == 1)
					{
						lblDisabled.Text = "This mod is enabled.";
						lblDisabled.ForeColor = Color.Green;
						cmdToggleDisabled.Text = "&Disable";
					}
					EnabledCount++;
					myListView.RedrawItems(item2.Index, item2.Index, invalidateOnly: true);
				}
			}
			while (flag);
			UpdateEnabledCountLabel();
		}
	}

	private void DisableSelected()
	{
		List<ListViewItem> list = new List<ListViewItem>();
		List<ListViewItem> list2 = new List<ListViewItem>();
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			list.Add(selectedItem);
		}
		checked
		{
			bool flag;
			do
			{
				foreach (ListViewItem item in list2)
				{
					list.Remove(item);
				}
				list2.Clear();
				flag = false;
				foreach (ListViewItem item2 in list)
				{
					Modification modification = (Modification)item2.Tag;
					if (modification.Disabled)
					{
						continue;
					}
					bool flag2 = false;
					foreach (Modification mod in Mods)
					{
						if (!mod.Enabled)
						{
							continue;
						}
						foreach (KeyValuePair<string, string> requirement in mod.Requirements)
						{
							if (Operators.CompareString(modification.FixedName, requirement.Key, TextCompare: false) == 0)
							{
								if (myListView.SelectedItems.Count == 1)
								{
									MessageBox.Show("The mod \"" + mod.Name + "\" requires this mod to be enabled. Disable it first if you want to disable this one.", "Cannot disable", MessageBoxButtons.OK, MessageBoxIcon.Hand);
									return;
								}
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
					flag = true;
					list2.Add(item2);
					EnabledMods.Remove(modification.FixedName);
					modification.Disabled = true;
					item2.ForeColor = Color.Red;
					if (modification.Icon != null)
					{
						Bitmap bitmap = new Bitmap(modification.Icon);
						DisableIcon(bitmap);
						if (modification.IsUpdating())
						{
							UpdatingIcon(bitmap);
						}
						myImageList.Images[modification.ImageListIdx] = bitmap;
					}
					else if (modification.IsUpdating())
					{
						item2.ImageIndex = 2;
					}
					else
					{
						item2.ImageIndex = 0;
					}
					if (myListView.SelectedItems.Count == 1)
					{
						lblDisabled.Text = "This mod is disabled.";
						lblDisabled.ForeColor = Color.Red;
						cmdToggleDisabled.Text = "&Enable";
					}
					EnabledCount--;
					myListView.RedrawItems(item2.Index, item2.Index, invalidateOnly: true);
				}
			}
			while (flag);
			UpdateEnabledCountLabel();
		}
	}

	private void RenameToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (myListView.SelectedItems.Count == 1)
		{
			Modification modification = (Modification)myListView.SelectedItems[0].Tag;
			if (!DisplayNames.ContainsKey(modification.Name))
			{
				myListView.SelectedItems[0].Text = modification.Name;
			}
			else
			{
				myListView.SelectedItems[0].Text = DisplayNames[modification.Name];
			}
			myListView.LabelEdit = true;
			myListView.SelectedItems[0].BeginEdit();
		}
	}

	private void myListView_AfterLabelEdit(object sender, LabelEditEventArgs e)
	{
		e.CancelEdit = true;
		myListView.LabelEdit = false;
		bool flag = false;
		Modification modification = (Modification)myListView.Items[e.Item].Tag;
		if (e.Label != null)
		{
			if (Operators.CompareString(e.Label.Trim(), "", TextCompare: false) == 0)
			{
				DisplayNames.Remove(modification.Name);
			}
			else
			{
				DisplayNames[modification.Name] = e.Label.Trim();
			}
			flag = true;
		}
		if (ShowVersionsInMainViewToolStripMenuItem.Checked & (Operators.CompareString(modification.Version, "", TextCompare: false) != 0))
		{
			if (!DisplayNames.ContainsKey(modification.Name))
			{
				myListView.Items[e.Item].Text = modification.Name + " (v" + modification.Version + ")";
			}
			else
			{
				myListView.Items[e.Item].Text = DisplayNames[modification.Name] + " (v" + modification.Version + ")";
			}
		}
		else if (!DisplayNames.ContainsKey(modification.Name))
		{
			myListView.Items[e.Item].Text = modification.Name;
		}
		else
		{
			myListView.Items[e.Item].Text = DisplayNames[modification.Name];
		}
		if (flag)
		{
			myListView.Sort();
		}
	}

	private void ResetNameToolStripMenuItem_Click(object sender, EventArgs e)
	{
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			Modification modification = (Modification)selectedItem.Tag;
			if (DisplayNames.Remove(modification.Name))
			{
				if (ShowVersionsInMainViewToolStripMenuItem.Checked & (Operators.CompareString(modification.Version, "", TextCompare: false) != 0))
				{
					selectedItem.Text = modification.Name + " (v" + modification.Version + ")";
				}
				else
				{
					selectedItem.Text = modification.Name;
				}
				myListView.Sort();
			}
		}
	}

	private void myContextMenu_Opening(object sender, CancelEventArgs e)
	{
		if (myListView.SelectedItems.Count == 0)
		{
			e.Cancel = true;
			myEmptyContextMenu.Show(myContextMenu.Left, myContextMenu.Top);
			return;
		}
		if (myListView.SelectedItems.Count == 1)
		{
			Modification modification = (Modification)myListView.SelectedItems[0].Tag;
			if (modification.Disabled)
			{
				EnableDisableToolStripMenuItem.Text = "En&able";
			}
			else
			{
				EnableDisableToolStripMenuItem.Text = "Dis&able";
			}
			EnableDisableToolStripMenuItem.Visible = true;
			EnableAllToolStripMenuItem.Visible = false;
			DisableAllToolStripMenuItem.Visible = false;
			RenameToolStripMenuItem.Visible = true;
			ResetNameToolStripMenuItem.Visible = DisplayNames.ContainsKey(modification.Name);
			ResetNameToolStripMenuItem.Text = "&Reset Name";
			ExportAss2zToolStripMenuItem.Visible = modification.Enabled;
			ToolStripMenuItem11.Visible = modification.Enabled;
			if (modification.Requirements.Count > 0)
			{
				ExportAss2zToolStripMenuItem.Enabled = false;
				ExportAss2zToolStripMenuItem.Text = "Cannot Export as .s2z";
			}
			else
			{
				ExportAss2zToolStripMenuItem.Enabled = true;
				ExportAss2zToolStripMenuItem.Text = "Export as .s2&z ...";
			}
			if ((Operators.CompareString(modification.UpdateCheck, "", TextCompare: false) == 0) | (Operators.CompareString(modification.UpdateDownload, "", TextCompare: false) == 0))
			{
				UpdateThisModToolStripMenuItem.Enabled = false;
				UpdateThisModToolStripMenuItem.Text = "This mod is not updatable.";
				UpdateThisModToolStripMenuItem.Visible = true;
				CancelUpdateToolStripMenuItem.Visible = false;
				return;
			}
			if (modification.IsUpdating())
			{
				UpdateThisModToolStripMenuItem.Enabled = false;
				UpdateThisModToolStripMenuItem.Visible = false;
				CancelUpdateToolStripMenuItem.Text = "Cancel &Update";
				CancelUpdateToolStripMenuItem.Visible = true;
				return;
			}
			if (modification.Updater == null)
			{
				UpdateThisModToolStripMenuItem.Enabled = true;
				UpdateThisModToolStripMenuItem.Text = "&Update this Mod";
			}
			else
			{
				UpdateThisModToolStripMenuItem.Enabled = false;
				UpdateThisModToolStripMenuItem.Text = "Update " + modification.Updater.StatusString;
			}
			UpdateThisModToolStripMenuItem.Visible = true;
			CancelUpdateToolStripMenuItem.Visible = false;
			return;
		}
		bool flag = true;
		int count = myListView.SelectedItems.Count;
		checked
		{
			int num = default(int);
			int num2 = default(int);
			int num3 = default(int);
			int num4 = default(int);
			int num5 = default(int);
			int num6 = default(int);
			foreach (ListViewItem selectedItem in myListView.SelectedItems)
			{
				Modification modification2 = (Modification)selectedItem.Tag;
				if (DisplayNames.ContainsKey(modification2.Name))
				{
					num++;
				}
				if (modification2.Enabled)
				{
					num2++;
				}
				if (modification2.Disabled)
				{
					num3++;
				}
				if ((Operators.CompareString(modification2.UpdateDownload, "", TextCompare: false) != 0) & (Operators.CompareString(modification2.UpdateCheck, "", TextCompare: false) != 0))
				{
					num4++;
				}
				if (modification2.IsUpdating())
				{
					num5++;
				}
				if (!modification2.IsUpdating() & (modification2.Updater != null))
				{
					num6++;
				}
				if (!flag)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> requirement in modification2.Requirements)
				{
					bool flag2 = false;
					foreach (ListViewItem selectedItem2 in myListView.SelectedItems)
					{
						Modification modification3 = (Modification)selectedItem2.Tag;
						if (Operators.CompareString(requirement.Key, modification3.FixedName, TextCompare: false) == 0)
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						flag = false;
						break;
					}
				}
			}
			EnableDisableToolStripMenuItem.Visible = false;
			EnableAllToolStripMenuItem.Visible = num3 > 0;
			DisableAllToolStripMenuItem.Visible = num2 > 0;
			RenameToolStripMenuItem.Visible = false;
			ResetNameToolStripMenuItem.Visible = num > 0;
			ResetNameToolStripMenuItem.Text = "&Reset Names";
			ExportAss2zToolStripMenuItem.Visible = num3 == 0;
			ToolStripMenuItem11.Visible = num3 == 0;
			if (num3 == 0)
			{
				if (!flag)
				{
					ExportAss2zToolStripMenuItem.Enabled = false;
					ExportAss2zToolStripMenuItem.Text = "Cannot Export as .s2z";
				}
				else
				{
					ExportAss2zToolStripMenuItem.Enabled = true;
					ExportAss2zToolStripMenuItem.Text = "Export as .s2&z ...";
				}
			}
			if (num4 == 0)
			{
				UpdateThisModToolStripMenuItem.Enabled = false;
				UpdateThisModToolStripMenuItem.Text = "These mods are not updatable.";
				UpdateThisModToolStripMenuItem.Visible = true;
				CancelUpdateToolStripMenuItem.Visible = false;
				return;
			}
			if (num5 > 0)
			{
				CancelUpdateToolStripMenuItem.Text = "Cancel &Updates (" + Conversions.ToString(num5) + ")";
				CancelUpdateToolStripMenuItem.Visible = true;
			}
			else
			{
				CancelUpdateToolStripMenuItem.Visible = false;
			}
			if (num4 - num5 - num6 > 0)
			{
				UpdateThisModToolStripMenuItem.Enabled = true;
				UpdateThisModToolStripMenuItem.Text = "&Update these Mods";
				UpdateThisModToolStripMenuItem.Visible = true;
			}
			else
			{
				UpdateThisModToolStripMenuItem.Enabled = false;
				UpdateThisModToolStripMenuItem.Visible = false;
			}
		}
	}

	private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
	{
		MessageBox.Show("Heroes of Newerth Modification Manager 1.4.0.0 by Notausgang" + Environment.NewLine + Environment.NewLine + "Great game by S2 Games", "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}

	private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void ApplyModsToolStripMenuItem_Click(object sender, EventArgs e)
	{
		ApplyMods();
	}

	private void ApplyModsAndLaunchHoNToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (ApplyMods(SilentSuccess: true))
		{
			try
			{
				Environment.CurrentDirectory = GameDir;
				Process.Start(RunGameFile, RunGameArguments);
				Close();
			}
			catch (Exception ex)
			{
				ProjectData.SetProjectError(ex);
				Exception ex2 = ex;
				MessageBox.Show("Could not launch HoN:" + Environment.NewLine + Environment.NewLine + ex2.Message, "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				ProjectData.ClearProjectError();
			}
		}
	}

	private void EnableDisableToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (cmdToggleDisabled.Enabled)
		{
			cmdToggleDisabled_Click(null, null);
		}
	}

	private void EnableAllToolStripMenuItem_Click(object sender, EventArgs e)
	{
		EnableSelected();
	}

	private void DisableAllToolStripMenuItem_Click(object sender, EventArgs e)
	{
		DisableSelected();
	}

	private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
	{
		bool flag = false;
		foreach (ListViewItem selectedItem in myListView.SelectedItems)
		{
			Modification modification = (Modification)selectedItem.Tag;
			if (MessageBox.Show("Are you sure you want to permanently delete " + Path.GetFileName(modification.File) + "?", "Delete Mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				ForGetZip(modification.File);
				try
				{
					File.Delete(modification.File);
					flag = true;
				}
				catch (Exception projectError)
				{
					ProjectData.SetProjectError(projectError);
					MessageBox.Show("Could not delete " + modification.File + "!", "Delete Mod", MessageBoxButtons.OK, MessageBoxIcon.Hand);
					ProjectData.ClearProjectError();
				}
			}
		}
		if (flag)
		{
			UpdateList();
		}
	}

	private void RefreshModDisplayToolStripMenuItem_Click(object sender, EventArgs e)
	{
		UpdateList();
	}

	private void OpenModFolderToolStripMenuItem_Click(object sender, EventArgs e)
	{
		ForGetAllZIPs();
		Process.Start(Path.Combine(ModsDir, "mods"));
	}

	private void ListToolStripMenuItem_Click(object sender, EventArgs e)
	{
		myListView.View = View.List;
		ListToolStripMenuItem.Checked = true;
		TilesToolStripMenuItem.Checked = false;
		SmallIconsToolStripMenuItem.Checked = false;
	}

	private void TilesToolStripMenuItem_Click(object sender, EventArgs e)
	{
		myListView.View = View.Tile;
		ListToolStripMenuItem.Checked = false;
		TilesToolStripMenuItem.Checked = true;
		SmallIconsToolStripMenuItem.Checked = false;
	}

	private void SmallIconsToolStripMenuItem_Click(object sender, EventArgs e)
	{
		myListView.View = View.SmallIcon;
		ListToolStripMenuItem.Checked = false;
		TilesToolStripMenuItem.Checked = false;
		SmallIconsToolStripMenuItem.Checked = true;
	}

	private void ShowVersionsInMainViewToolStripMenuItem_Click(object sender, EventArgs e)
	{
		ShowVersionsInMainViewToolStripMenuItem.Checked = !ShowVersionsInMainViewToolStripMenuItem.Checked;
		UpdateList();
	}

	private void CLArgumentsForLaunchingHoNToolStripMenuItem_Click(object sender, EventArgs e)
	{
		frmInputbox frmInputbox2 = new frmInputbox();
		frmInputbox2.Text = "Command line arguments to use when launching HoN:";
		frmInputbox2.Result = RunGameArguments;
		if (frmInputbox2.ShowDialog() == DialogResult.OK)
		{
			RunGameArguments = frmInputbox2.Result;
		}
	}

	private void ExportAss2zToolStripMenuItem_Click(object sender, EventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Title = "Export as .s2z";
		saveFileDialog.Filter = "s2z archive (*.s2z)|*.s2z|All Files (*.*)|*";
		saveFileDialog.FileName = "resources_mods.s2z";
		if (saveFileDialog.ShowDialog() == DialogResult.OK)
		{
			ApplyMods(SilentSuccess: false, saveFileDialog.FileName);
		}
	}

	private void RegisterFileExtension()
	{
		try
		{
			RegistryKey registryKey = MyProject.Computer.Registry.ClassesRoot.CreateSubKey(".honmod");
			string text = registryKey.GetValue("").ToString();
			if (Operators.CompareString(text, "HoN_ModMan", TextCompare: false) != 0)
			{
				SetRegistryEntry("oldreg", text);
				registryKey.SetValue("", "HoN_ModMan", RegistryValueKind.String);
			}
			MyProject.Computer.Registry.ClassesRoot.CreateSubKey("HoN_ModMan").SetValue("", "HoN Modification", RegistryValueKind.String);
			MyProject.Computer.Registry.ClassesRoot.CreateSubKey("HoN_ModMan\\shell\\open\\command").SetValue("", Application.ExecutablePath + " \"%l\"", RegistryValueKind.String);
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
		}
		RegisterhonmodFileExtensionToolStripMenuItem.Text = "Unregister .honmod File Extension";
		SetRegistryEntry("fileextension", "yes");
	}

	private void UnregisterFileExtension()
	{
		try
		{
			RegistryKey registryKey = MyProject.Computer.Registry.ClassesRoot.CreateSubKey(".honmod");
			string left = registryKey.GetValue("").ToString();
			string registryEntry = GetRegistryEntry("oldreg");
			if (Operators.CompareString(left, "HoN_ModMan", TextCompare: false) == 0)
			{
				if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
				{
					registryKey.SetValue("", registryEntry, RegistryValueKind.String);
				}
				else
				{
					MyProject.Computer.Registry.ClassesRoot.DeleteSubKeyTree(".honmod");
				}
			}
			MyProject.Computer.Registry.ClassesRoot.DeleteSubKeyTree("HoN_ModMan");
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
		}
		RegisterhonmodFileExtensionToolStripMenuItem.Text = "Register .honmod File Extension";
		SetRegistryEntry("fileextension", "no");
	}

	private void RegisterhonmodFileExtensionToolStripMenuItem_Click(object sender, EventArgs e)
	{
		if (Operators.CompareString(RegisterhonmodFileExtensionToolStripMenuItem.Text, "Register .honmod File Extension", TextCompare: false) == 0)
		{
			RegisterFileExtension();
		}
		else
		{
			UnregisterFileExtension();
		}
	}

	private void lblDescription_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		LaunchWebBrowser(((Modification)myListView.SelectedItems[0].Tag).WebLink);
	}

	private void ForumThreadToolStripMenuItem_Click(object sender, EventArgs e)
	{
		LaunchWebBrowser("http://www.newerth.com/notausgang/HoN_ModMan");
	}

	private static void LaunchWebBrowser(string URL)
	{
		if (!URL.StartsWith("http://") & !URL.StartsWith("https://"))
		{
			URL = "http://" + URL;
		}
		try
		{
			Process.Start(URL);
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
		}
	}

	private void frmMain_DragEnter(object sender, DragEventArgs e)
	{
		if (!myListView.Enabled)
		{
			e.Effect = DragDropEffects.None;
			return;
		}
		string path = Path.Combine(ModsDir, "mods");
		if (Directory.Exists(path) & (e.Data.GetDataPresent(DataFormats.FileDrop) | e.Data.GetDataPresent("FileNameW") | e.Data.GetDataPresent("FileName")))
		{
			e.Effect = DragDropEffects.Copy;
		}
		else
		{
			e.Effect = DragDropEffects.None;
		}
	}

	private void frmMain_DragDrop(object sender, DragEventArgs e)
	{
		if (!myListView.Enabled)
		{
			return;
		}
		string path = Path.Combine(ModsDir, "mods");
		if (!Directory.Exists(path))
		{
			return;
		}
		string[] array = null;
		try
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Copy;
				array = (string[])e.Data.GetData(DataFormats.FileDrop);
			}
			else if (e.Data.GetDataPresent("FileNameW"))
			{
				e.Effect = DragDropEffects.Copy;
				array = (string[])e.Data.GetData("FileNameW");
			}
			else if (e.Data.GetDataPresent("FileName"))
			{
				e.Effect = DragDropEffects.Copy;
				array = (string[])e.Data.GetData("FileName");
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			array = null;
			ProjectData.ClearProjectError();
		}
		if (array != null)
		{
			string[] array2 = array;
			foreach (string sourceFile in array2)
			{
				InstallMod(sourceFile);
			}
			UpdateList();
		}
	}

	private void InstallMod(string SourceFile)
	{
		if (!((Operators.CompareString(Path.GetExtension(SourceFile), ".honmod", TextCompare: false) == 0) & (Operators.CompareString(Path.GetDirectoryName(SourceFile), Path.Combine(ModsDir, "mods"), TextCompare: false) != 0)))
		{
			return;
		}
		try
		{
			string text = Path.Combine(Path.Combine(ModsDir, "mods"), Path.GetFileName(SourceFile));
			if (!File.Exists(text) || MessageBox.Show(Path.GetFileName(SourceFile) + " already exists. Overwrite?", "HoN_ModMan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.No)
			{
				ForGetZip(text);
				File.Copy(SourceFile, text, overwrite: true);
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			MessageBox.Show("Could not copy the file.", "An error occured", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			ProjectData.ClearProjectError();
		}
	}

	private static string FixModName(string s)
	{
		string text = "";
		checked
		{
			int num = s.Length - 1;
			for (int i = 0; i <= num; i++)
			{
				if (char.IsLetterOrDigit(s[i]))
				{
					text += Conversions.ToString(s[i]);
				}
			}
			return text.ToLower();
		}
	}

	private static Bitmap DisableIcon(Bitmap b)
	{
		int num = 0;
		do
		{
			int num2 = 0;
			do
			{
				Color pixel = b.GetPixel(num, num2);
				int num3 = checked(Convert.ToInt32(pixel.R) + Convert.ToInt32(pixel.G) + Convert.ToInt32(pixel.B)) / 3;
				b.SetPixel(num, num2, Color.FromArgb(pixel.A, num3, num3, num3));
				num2 = checked(num2 + 1);
			}
			while (num2 <= 47);
			num = checked(num + 1);
		}
		while (num <= 47);
		Graphics.FromImage(b).DrawImageUnscaled(Resources.disabled, 0, 0);
		return b;
	}

	private void UpdateList()
	{
		lblName.Text = "";
		lblDescription.Text = "";
		lblDisabled.Visible = false;
		cmdToggleDisabled.Visible = false;
		myListView.Items.Clear();
		Mods.Clear();
		EnabledCount = 0;
		myImageList.Images.Clear();
		myImageList.Images.Add(Resources.disabled);
		myImageList.Images.Add(Resources.updating);
		myImageList.Images.Add(UpdatingIcon(new Bitmap(Resources.disabled)));
		if (Operators.CompareString(GameDir, "", TextCompare: false) == 0)
		{
			myListView.Enabled = false;
			myStatusLabel.Text = "Not attached to a valid HoN install.";
			RefreshModDisplayToolStripMenuItem.Enabled = false;
			ApplyModsToolStripMenuItem.Enabled = false;
			ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = false;
			UpdateAllModsToolStripMenuItem.Enabled = false;
			OpenModFolderToolStripMenuItem.Enabled = false;
			UnapplyAllModsToolStripMenuItem.Enabled = false;
			return;
		}
		RefreshModDisplayToolStripMenuItem.Enabled = true;
		ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = Operators.CompareString(RunGameFile, "", TextCompare: false) != 0;
		ApplyModsToolStripMenuItem.Enabled = true;
		UpdateAllModsToolStripMenuItem.Enabled = true;
		OpenModFolderToolStripMenuItem.Enabled = true;
		UnapplyAllModsToolStripMenuItem.Enabled = true;
		string path = Path.Combine(ModsDir, "mods");
		if (!Directory.Exists(path) & Directory.Exists(ModsDir))
		{
			Directory.CreateDirectory(path);
		}
		checked
		{
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path, "*.honmod");
				foreach (string text in files)
				{
					try
					{
						ZipFile zip = GetZip(text);
						if (zip == null)
						{
							continue;
						}
						Stream zippedFile = GetZippedFile(zip, "icon.png");
						Bitmap bitmap = null;
						if (zippedFile != null)
						{
							try
							{
								bitmap = new Bitmap(zippedFile);
								if ((bitmap.Width != 48) | (bitmap.Height != 48))
								{
									bitmap = null;
								}
							}
							catch (Exception projectError)
							{
								ProjectData.SetProjectError(projectError);
								ProjectData.ClearProjectError();
							}
						}
						zippedFile = GetZippedFile(zip, "mod.xml");
						if (zippedFile == null)
						{
							ForGetZip(text);
							continue;
						}
						XmlTextReader xmlTextReader = new XmlTextReader(zippedFile);
						xmlTextReader.WhitespaceHandling = WhitespaceHandling.None;
						while (!((xmlTextReader.NodeType == XmlNodeType.Element) & (Operators.CompareString(xmlTextReader.Name, "modification", TextCompare: false) == 0)))
						{
							if (!xmlTextReader.Read())
							{
								throw new Exception("Unexpected EOF in " + Path.Combine(text, "mod.xml"));
							}
						}
						if (xmlTextReader.IsEmptyElement)
						{
							throw new Exception("Empty modification element.");
						}
						if (Operators.CompareString(xmlTextReader.GetAttribute("application"), "Heroes of Newerth", TextCompare: false) != 0)
						{
							ForGetZip(text);
							continue;
						}
						Modification modification = new Modification();
						modification.File = text;
						modification.Name = xmlTextReader.GetAttribute("name");
						if (Operators.CompareString(modification.Name, "", TextCompare: false) == 0)
						{
							ForGetZip(text);
							continue;
						}
						modification.FixedName = FixModName(modification.Name);
						modification.Version = xmlTextReader.GetAttribute("version").Replace(" ", "");
						bool flag = false;
						Modification modification2 = null;
						foreach (Modification mod in Mods)
						{
							if (Operators.CompareString(mod.FixedName, modification.FixedName, TextCompare: false) == 0)
							{
								if (IsNewerVersion(mod.Version, modification.Version))
								{
									modification2 = mod;
								}
								else
								{
									flag = true;
								}
								break;
							}
						}
						if (flag)
						{
							ForGetZip(text);
							continue;
						}
						if (modification2 != null)
						{
							Mods.Remove(modification2);
						}
						modification.Description = xmlTextReader.GetAttribute("description");
						if (modification.Description != null)
						{
							modification.Description = modification.Description.Replace(Conversions.ToString(Convert.ToChar(13)), "");
						}
						modification.Author = xmlTextReader.GetAttribute("author");
						modification.WebLink = xmlTextReader.GetAttribute("weblink");
						modification.UpdateCheck = xmlTextReader.GetAttribute("updatecheckurl");
						modification.UpdateDownload = xmlTextReader.GetAttribute("updatedownloadurl");
						modification.Icon = bitmap;
						modification.AppVersion = xmlTextReader.GetAttribute("appversion");
						modification.MMVersion = xmlTextReader.GetAttribute("mmversion");
						if (Operators.CompareString(modification.MMVersion, "", TextCompare: false) == 0)
						{
							ForGetZip(text);
							continue;
						}
						modification.MMVersion = modification.MMVersion.Replace('*', '0');
						modification.Disabled = ((!EnabledMods.ContainsKey(modification.FixedName) || !IsNewerVersion(modification.MMVersion, "1.4.0.0") || (Operators.CompareString(GameVersion, "", TextCompare: false) != 0 && Operators.CompareString(modification.AppVersion, "", TextCompare: false) != 0 && !VersionsMatch(modification.AppVersion, GameVersion))) ? true : false);
						bool flag2 = false;
						while (!((xmlTextReader.NodeType == XmlNodeType.EndElement) & (Operators.CompareString(xmlTextReader.Name, "modification", TextCompare: false) == 0)))
						{
							if (!flag2 && !xmlTextReader.Read())
							{
								throw new Exception("Unexpected EOF in " + Path.Combine(text, "mod.xml"));
							}
							flag2 = false;
							if (xmlTextReader.NodeType != XmlNodeType.Element)
							{
								continue;
							}
							switch (xmlTextReader.Name)
							{
							case "incompatibility":
							{
								string text3 = xmlTextReader.GetAttribute("version");
								if (text3 != null)
								{
									text3 = text3.Replace(" ", "");
								}
								modification.Incompatibilities.Add(FixModName(xmlTextReader.GetAttribute("name")), text3 + " " + xmlTextReader.GetAttribute("name"));
								break;
							}
							case "requirement":
							{
								string text2 = xmlTextReader.GetAttribute("version");
								if (text2 != null)
								{
									text2 = text2.Replace(" ", "");
								}
								modification.Requirements.Add(FixModName(xmlTextReader.GetAttribute("name")), text2 + " " + xmlTextReader.GetAttribute("name"));
								break;
							}
							case "applybefore":
								modification.ApplyBefore.Add(FixModName(xmlTextReader.GetAttribute("name")), xmlTextReader.GetAttribute("version"));
								break;
							case "applyafter":
								modification.ApplyAfter.Add(FixModName(xmlTextReader.GetAttribute("name")), xmlTextReader.GetAttribute("version"));
								break;
							}
							if (!xmlTextReader.IsEmptyElement)
							{
								xmlTextReader.Skip();
								flag2 = true;
							}
						}
						string text4 = "";
						if ((Operators.CompareString(modification.Version, "", TextCompare: false) != 0) & ShowVersionsInMainViewToolStripMenuItem.Checked)
						{
							text4 = " (v" + modification.Version + ")";
						}
						if (modification.Icon == null)
						{
							modification.ImageListIdx = -1;
						}
						else
						{
							bitmap = new Bitmap(modification.Icon);
							if (modification.Disabled)
							{
								DisableIcon(bitmap);
							}
							myImageList.Images.Add(bitmap);
							modification.ImageListIdx = myImageList.Images.Count - 1;
						}
						ListViewItem listViewItem = (((modification.ImageListIdx == -1) & modification.Disabled) ? ((!DisplayNames.ContainsKey(modification.Name)) ? myListView.Items.Add(modification.Name + text4, 0) : myListView.Items.Add(DisplayNames[modification.Name] + text4, 0)) : ((!DisplayNames.ContainsKey(modification.Name)) ? myListView.Items.Add(modification.Name + text4, modification.ImageListIdx) : myListView.Items.Add(DisplayNames[modification.Name] + text4, modification.ImageListIdx)));
						listViewItem.Tag = modification;
						if (modification.Disabled)
						{
							listViewItem.ForeColor = Color.Red;
						}
						else
						{
							listViewItem.ForeColor = SystemColors.WindowText;
						}
						modification.Index = Mods.Count;
						Mods.Add(modification);
						if (modification.Enabled)
						{
							EnabledCount++;
						}
					}
					catch (Exception projectError2)
					{
						ProjectData.SetProjectError(projectError2);
						ProjectData.ClearProjectError();
					}
				}
			}
			FillApplyFirst();
			bool flag3;
			do
			{
				flag3 = false;
				foreach (Modification mod2 in Mods)
				{
					if (!mod2.Enabled)
					{
						continue;
					}
					foreach (KeyValuePair<string, string> requirement in mod2.Requirements)
					{
						bool flag4 = false;
						foreach (Modification mod3 in Mods)
						{
							if (Operators.CompareString(mod3.FixedName, requirement.Key, TextCompare: false) == 0 && (mod3.Enabled & VersionsMatch(requirement.Value.Substring(0, requirement.Value.IndexOf(' ')), mod3.Version)))
							{
								flag4 = true;
								break;
							}
						}
						if (!flag4)
						{
							mod2.Disabled = true;
							EnabledCount--;
							if (mod2.Icon != null)
							{
								Bitmap bitmap2 = new Bitmap(mod2.Icon);
								DisableIcon(bitmap2);
								myImageList.Images[mod2.ImageListIdx] = bitmap2;
							}
							else
							{
								myListView.Items[mod2.Index].ImageIndex = 0;
							}
							myListView.Items[mod2.Index].ForeColor = Color.Red;
							flag3 = true;
							break;
						}
					}
				}
				foreach (Modification mod4 in Mods)
				{
					if (!mod4.Enabled)
					{
						continue;
					}
					foreach (KeyValuePair<string, string> incompatibility in mod4.Incompatibilities)
					{
						bool flag5 = false;
						foreach (Modification mod5 in Mods)
						{
							if (Operators.CompareString(mod5.FixedName, incompatibility.Key, TextCompare: false) == 0 && (mod5.Enabled & VersionsMatch(incompatibility.Value.Substring(0, incompatibility.Value.IndexOf(' ')), mod5.Version)))
							{
								flag5 = true;
								break;
							}
						}
						if (flag5)
						{
							mod4.Disabled = true;
							EnabledCount--;
							if (mod4.Icon != null)
							{
								Bitmap bitmap3 = new Bitmap(mod4.Icon);
								DisableIcon(bitmap3);
								myImageList.Images[mod4.ImageListIdx] = bitmap3;
							}
							else
							{
								myListView.Items[mod4.Index].ImageIndex = 0;
							}
							myListView.Items[mod4.Index].ForeColor = Color.Red;
							flag3 = true;
							break;
						}
					}
				}
				if (flag3)
				{
					continue;
				}
				Modification modification3 = FindCycle();
				if (modification3 != null)
				{
					modification3.Disabled = true;
					EnabledCount--;
					if (modification3.Icon != null)
					{
						Bitmap bitmap4 = new Bitmap(modification3.Icon);
						DisableIcon(bitmap4);
						myImageList.Images[modification3.ImageListIdx] = bitmap4;
					}
					else
					{
						myListView.Items[modification3.Index].ImageIndex = 0;
					}
					myListView.Items[modification3.Index].ForeColor = Color.Red;
					flag3 = true;
				}
			}
			while (flag3);
			myListView.Sort();
			myStatusLabel.Text = Mods.Count + " mods loaded.";
			UpdateEnabledCountLabel();
			EnabledMods = new Dictionary<string, string>();
			foreach (Modification mod6 in Mods)
			{
				if (mod6.Enabled)
				{
					EnabledMods.Add(mod6.FixedName, mod6.Version);
				}
			}
		}
	}

	private void FillApplyFirst()
	{
		foreach (Modification mod in Mods)
		{
			foreach (KeyValuePair<string, string> requirement in mod.Requirements)
			{
				foreach (Modification mod2 in Mods)
				{
					if (Operators.CompareString(mod2.FixedName, requirement.Key, TextCompare: false) == 0 && VersionsMatch(requirement.Value.Substring(0, requirement.Value.IndexOf(' ')), mod2.Version) && !mod.ApplyFirst.Contains(mod2))
					{
						mod.ApplyFirst.Add(mod2);
					}
				}
			}
			foreach (KeyValuePair<string, string> item in mod.ApplyBefore)
			{
				foreach (Modification mod3 in Mods)
				{
					if (Operators.CompareString(mod3.FixedName, item.Key, TextCompare: false) == 0 && VersionsMatch(item.Value, mod3.Version) && !mod3.ApplyFirst.Contains(mod))
					{
						mod3.ApplyFirst.Add(mod);
					}
				}
			}
			foreach (KeyValuePair<string, string> item2 in mod.ApplyAfter)
			{
				foreach (Modification mod4 in Mods)
				{
					if (Operators.CompareString(mod4.FixedName, item2.Key, TextCompare: false) == 0 && VersionsMatch(item2.Value, mod4.Version) && !mod.ApplyFirst.Contains(mod4))
					{
						mod.ApplyFirst.Add(mod4);
					}
				}
			}
		}
	}

	private Modification FindCycle()
	{
		foreach (Modification mod in Mods)
		{
			if (!mod.Enabled)
			{
				continue;
			}
			foreach (Modification mod2 in Mods)
			{
				mod2.Marked = false;
			}
			if (TraverseApplyFirstGraph(mod))
			{
				return mod;
			}
		}
		return null;
	}

	private bool TraverseApplyFirstGraph(Modification StartingNode)
	{
		if (StartingNode.Marked)
		{
			return true;
		}
		StartingNode.Marked = true;
		foreach (Modification item in StartingNode.ApplyFirst)
		{
			if (item.Enabled && TraverseApplyFirstGraph(item))
			{
				return true;
			}
		}
		StartingNode.Marked = false;
		return false;
	}

	private bool ApplyMods(bool SilentSuccess = false, string ExportPath = null)
	{
		StringCollection stringCollection = null;
		if (Operators.CompareString(ExportPath, "", TextCompare: false) != 0)
		{
			stringCollection = new StringCollection();
			foreach (ListViewItem selectedItem in myListView.SelectedItems)
			{
				if (selectedItem.Selected)
				{
					Modification modification = (Modification)selectedItem.Tag;
					stringCollection.Add(modification.FixedName);
				}
			}
		}
		myListView.Enabled = false;
		UpdateList();
		if (Operators.CompareString(GameDir, "", TextCompare: false) == 0)
		{
			return false;
		}
		if (stringCollection != null)
		{
			StringEnumerator enumerator2 = stringCollection.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				string current = enumerator2.Current;
				foreach (ListViewItem item in myListView.Items)
				{
					Modification modification2 = (Modification)item.Tag;
					if (Operators.CompareString(modification2.FixedName, current, TextCompare: false) == 0)
					{
						item.Selected = true;
						break;
					}
				}
			}
		}
		myStatusLabel.Text = "Busy ...";
		myStatusStrip.Refresh();
		List<Modification> list = new List<Modification>();
		if (stringCollection == null)
		{
			foreach (Modification mod in Mods)
			{
				if (mod.Enabled)
				{
					list.Add(mod);
				}
			}
		}
		else
		{
			StringEnumerator enumerator5 = stringCollection.GetEnumerator();
			while (enumerator5.MoveNext())
			{
				string current3 = enumerator5.Current;
				bool flag = false;
				foreach (Modification mod2 in Mods)
				{
					if (Operators.CompareString(mod2.FixedName, current3, TextCompare: false) == 0)
					{
						list.Add(mod2);
						flag = true;
					}
				}
				if (flag)
				{
					continue;
				}
				goto IL_020f;
			}
		}
		bool result2 = default(bool);
		checked
		{
			for (int i = 0; i < list.Count; i++)
			{
				Modification modification3 = list[i];
				foreach (Modification item2 in modification3.ApplyFirst)
				{
					if (!list.Contains(item2))
					{
						continue;
					}
					bool flag2 = false;
					int num = i;
					for (int j = 0; j <= num; j++)
					{
						if (list[j] == item2)
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						list.RemoveAt(i);
						list.Add(modification3);
						i--;
						break;
					}
				}
			}
			initResources();
			string text = "";
			try
			{
				Dictionary<string, Stream> dictionary = new Dictionary<string, Stream>();
				Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
				string text2 = ((ExportPath != null) ? ("HoN Mod Manager v1.4.0.0 Export" + Environment.NewLine) : ("HoN Mod Manager v1.4.0.0 Output" + Environment.NewLine));
				if (Operators.CompareString(GameVersion, "", TextCompare: false) != 0)
				{
					text2 = text2 + Environment.NewLine + "Game Version: " + GameVersion + Environment.NewLine;
				}
				text2 = text2 + Environment.NewLine + "Applied Mods: ";
				if (unchecked(((list.Count > 0) & (Operators.CompareString(GameVersion, "", TextCompare: false) != 0)) && ExportPath == null))
				{
					bool flag3 = false;
					Stream fileFromResources = getFileFromResources("ui/main.interface");
					if (fileFromResources != null)
					{
						Encoding Encoding = null;
						string text3 = Decode(fileFromResources, ref Encoding);
						int num2 = text3.IndexOf("<panel name=\"quit_confirm\"");
						if (num2 >= 0)
						{
							num2 = text3.IndexOf("</panel>", num2);
							if (num2 >= 0)
							{
								num2 += "</panel>".Length;
								text3 = text3.Substring(0, num2) + Resources.ModsOodReminder.Replace("%%%%", GameVersion) + text3.Substring(num2);
								dictionary["ui/main.interface"] = Encode(text3, Encoding);
								flag3 = true;
							}
						}
					}
					fileFromResources = getFileFromResources("ui/fe2/main.interface");
					if (fileFromResources != null)
					{
						Encoding Encoding2 = null;
						string text4 = Decode(fileFromResources, ref Encoding2);
						text4 = text4.Replace("CallEvent('event_login',1);", "CallEvent('event_login',1); Trigger('modsood_check');");
						fileFromResources = getFileFromResources("ui/fe2/social_groups.package");
						if (fileFromResources != null)
						{
							Encoding Encoding3 = null;
							string text5 = Decode(fileFromResources, ref Encoding3);
							int num3 = text5.IndexOf("</package>");
							if (num3 >= 0)
							{
								text5 = text5.Substring(0, num3) + Resources.ModsOodReminderFE2.Replace("%%%%", GameVersion) + text5.Substring(num3);
								dictionary["ui/fe2/main.interface"] = Encode(text4, Encoding2);
								dictionary["ui/fe2/social_groups.package"] = Encode(text5, Encoding2);
								flag3 = true;
							}
						}
					}
					if (!flag3)
					{
						fileFromResources = getFileFromResources("ui/main.interface");
						if (fileFromResources != null)
						{
							Encoding Encoding4 = null;
							string text6 = Decode(fileFromResources, ref Encoding4);
							text6 = text6.Replace("CallEvent('event_login',1);", "CallEvent('event_login',1); Trigger('modsood_check');");
							fileFromResources = getFileFromResources("ui/social_groups.package");
							if (fileFromResources != null)
							{
								Encoding Encoding5 = null;
								string text7 = Decode(fileFromResources, ref Encoding5);
								int num4 = text7.IndexOf("</package>");
								if (num4 >= 0)
								{
									text7 = text7.Substring(0, num4) + Resources.ModsOodReminderFE2.Replace("%%%%", GameVersion) + text7.Substring(num4);
									dictionary["ui/main.interface"] = Encode(text6, Encoding4);
									dictionary["ui/social_groups.package"] = Encode(text7, Encoding4);
									flag3 = true;
								}
							}
						}
					}
				}
				foreach (Modification item3 in list)
				{
					text2 = text2 + Environment.NewLine + item3.Name + " (v" + item3.Version + ")";
					text = item3.Name;
					ZipFile zip = GetZip(item3.File);
					if (zip == null)
					{
						throw new Exception("Could not open " + item3.File);
					}
					string text8 = Path.Combine(item3.File, "mod.xml");
					Stream zippedFile = GetZippedFile(zip, "mod.xml");
					if (zippedFile == null)
					{
						throw new Exception("Could not open " + text8);
					}
					XmlTextReader xmlTextReader = new XmlTextReader(zippedFile);
					xmlTextReader.WhitespaceHandling = WhitespaceHandling.None;
					while (!((xmlTextReader.NodeType == XmlNodeType.Element) & (Operators.CompareString(xmlTextReader.Name, "modification", TextCompare: false) == 0)))
					{
						if (!xmlTextReader.Read())
						{
							throw new Exception(text8 + " does not contain a modification.");
						}
					}
					bool flag4 = false;
					while (!((xmlTextReader.NodeType == XmlNodeType.EndElement) & (Operators.CompareString(xmlTextReader.Name, "modification", TextCompare: false) == 0)))
					{
						if (!flag4 && !xmlTextReader.Read())
						{
							throw new Exception("Unexpected EOF at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
						}
						flag4 = false;
						if (xmlTextReader.NodeType != XmlNodeType.Element)
						{
							continue;
						}
						switch (xmlTextReader.Name)
						{
						case "copyfile":
						{
							bool flag8;
							try
							{
								flag8 = EvalCondition(xmlTextReader.GetAttribute("condition"), list);
							}
							catch (Exception projectError2)
							{
								ProjectData.SetProjectError(projectError2);
								throw new Exception("Invalid condition string at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
							}
							if (!flag8)
							{
								break;
							}
							if (!xmlTextReader.IsEmptyElement)
							{
								throw new Exception("Non-empty copyfile tag at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
							}
							string text18 = FixFilename(xmlTextReader.GetAttribute("name"));
							if (Operators.CompareString(text18, "", TextCompare: false) == 0)
							{
								throw new Exception("copyfile tag without name attribute at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
							}
							string text19 = FixFilename(xmlTextReader.GetAttribute("source"));
							if (Operators.CompareString(text19, "", TextCompare: false) == 0)
							{
								text19 = text18;
							}
							bool flag9 = dictionary.ContainsKey(text18);
							bool flag10 = false;
							string attribute2 = xmlTextReader.GetAttribute("version");
							if (flag9)
							{
								switch (xmlTextReader.GetAttribute("overwrite"))
								{
								case "newer":
									if (Operators.CompareString(dictionary2[text18], "", TextCompare: false) != 0)
									{
										flag10 = !IsNewerVersion(dictionary2[text18], attribute2);
									}
									break;
								case "no":
									flag10 = true;
									break;
								default:
									throw new Exception("File \"" + text18 + "\" already exists! Non-overwriting write issued by line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
								case "yes":
									break;
								}
							}
							if (!flag10)
							{
								if (Operators.CompareString(attribute2, "", TextCompare: false) != 0)
								{
									dictionary2[text18] = attribute2;
								}
								zippedFile = GetZippedFile(zip, text19);
								if (zippedFile == null)
								{
									throw new Exception("File \"" + text19 + "\" referenced at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8 + " not found");
								}
								dictionary[text18] = zippedFile;
							}
							break;
						}
						case "editfile":
						{
							if (xmlTextReader.IsEmptyElement)
							{
								break;
							}
							bool flag5;
							try
							{
								flag5 = EvalCondition(xmlTextReader.GetAttribute("condition"), list);
							}
							catch (Exception projectError)
							{
								ProjectData.SetProjectError(projectError);
								throw new Exception("Invalid condition string at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
							}
							if (!flag5)
							{
								xmlTextReader.Skip();
								flag4 = true;
								break;
							}
							string text9 = FixFilename(xmlTextReader.GetAttribute("name"));
							if (Operators.CompareString(text9, "", TextCompare: false) == 0)
							{
								throw new Exception("editfile tag without name attribute at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
							}
							if (dictionary.ContainsKey(text9))
							{
								zippedFile = dictionary[text9];
								zippedFile.Seek(0L, SeekOrigin.Begin);
							}
							else
							{
								zippedFile = null;
							}
							Encoding Encoding6 = null;
							string text10;
							if (zippedFile != null)
							{
								text10 = Decode(zippedFile, ref Encoding6);
							}
							else
							{
								zippedFile = getFileFromResources(text9);
								if (zippedFile == null)
								{
									throw new Exception("File \"" + text9 + "\" referenced at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8 + " not found");
								}
								text10 = Decode(zippedFile, ref Encoding6);
							}
							string text11 = "";
							int num5 = 0;
							int num6 = 0;
							while (!((xmlTextReader.NodeType == XmlNodeType.EndElement) & (Operators.CompareString(xmlTextReader.Name, "editfile", TextCompare: false) == 0)))
							{
								if (!xmlTextReader.Read())
								{
									throw new Exception("Unexpected EOF at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
								}
								if (xmlTextReader.NodeType != XmlNodeType.Element)
								{
									continue;
								}
								string name = xmlTextReader.Name;
								string attribute = xmlTextReader.GetAttribute("position");
								string text12 = FixFilename(xmlTextReader.GetAttribute("source"));
								if (Operators.CompareString(text12, "", TextCompare: false) != 0)
								{
									zippedFile = GetZippedFile(zip, text12);
									if (zippedFile == null)
									{
										throw new Exception("File \"" + text12 + "\" referenced at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8 + " not found");
									}
									Encoding Encoding7 = null;
									text12 = Decode(zippedFile, ref Encoding7);
								}
								else if (xmlTextReader.IsEmptyElement)
								{
									text12 = "";
								}
								else
								{
									text12 = "";
									bool flag6 = true;
									string text13 = null;
									if (!IsNewerVersion("1.2", item3.MMVersion))
									{
										flag6 = false;
									}
									while (!((xmlTextReader.NodeType == XmlNodeType.EndElement) & (Operators.CompareString(xmlTextReader.Name, name, TextCompare: false) == 0)))
									{
										if (!xmlTextReader.Read())
										{
											throw new Exception("Unexpected EOF at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
										}
										if (xmlTextReader.NodeType == XmlNodeType.Element)
										{
											throw new Exception(text8 + ", line " + Conversions.ToString(xmlTextReader.LineNumber) + ": Cannot have sub-elements in operation elements!");
										}
										if (xmlTextReader.NodeType == XmlNodeType.Text)
										{
											string text14 = xmlTextReader.Value.Replace(Conversions.ToString(Convert.ToChar(13)), "");
											if (Operators.CompareString(text14.Trim(), "", TextCompare: false) != 0)
											{
												flag6 = false;
											}
											text12 += text14;
										}
										if (xmlTextReader.NodeType == XmlNodeType.CDATA)
										{
											string text15 = xmlTextReader.Value.Replace(Conversions.ToString(Convert.ToChar(13)), "");
											if (text13 != null)
											{
												flag6 = false;
											}
											text13 = text15;
											text12 += text15;
										}
									}
									if (unchecked(flag6 && text13 != null))
									{
										text12 = text13;
									}
								}
								switch (name)
								{
								case "find":
								case "seek":
								case "search":
								case "findup":
								case "seekup":
								case "searchup":
								{
									bool flag7 = name.EndsWith("up");
									if (Operators.CompareString(text11, "", TextCompare: false) != 0)
									{
										throw new Exception("findall operation not followed by insert, add, replace or delete at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
									}
									if (Operators.CompareString(text12, "", TextCompare: false) != 0)
									{
										if (flag7)
										{
											num5--;
											if (num5 >= 0)
											{
												num5 = text10.LastIndexOf(text12, num5 - 1);
											}
										}
										else
										{
											num5 = text10.IndexOf(text12, num6);
										}
										if (num5 < 0)
										{
											string text16 = "";
											string[] array = text12.Split(Convert.ToChar(10));
											for (int k = 0; k < array.Length; k++)
											{
												string text17 = array[k];
												text17 = text17.Trim();
												if (Operators.CompareString(text17, "", TextCompare: false) != 0)
												{
													text16 = text17;
													break;
												}
											}
											throw new Exception("Could not find string starting with \"" + text16 + "\" as sought by line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8 + Environment.NewLine + Environment.NewLine + "This may be caused by the mod being outdated or by an incompatibility with another enabled mod.");
										}
										num6 = num5 + text12.Length;
										break;
									}
									switch (attribute)
									{
									case "before":
									case "begin":
									case "start":
									case "head":
										num5 = 0;
										num6 = 0;
										break;
									case "after":
									case "end":
									case "tail":
									case "eof":
										num5 = text10.Length;
										num6 = num5;
										break;
									case null:
									case "":
										throw new Exception("find operation without parameters at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
									default:
									{
										if (!int.TryParse(attribute, out var result))
										{
											throw new Exception("find operation with invalid position parameter at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
										}
										num5 = ((!flag7) ? (num5 + result) : (num5 - result));
										if (num5 < 0)
										{
											num5 = 0;
										}
										if (num5 > text10.Length)
										{
											num5 = text10.Length;
										}
										num6 = ((!flag7) ? (num6 + result) : (num6 - result));
										if (num6 < 0)
										{
											num6 = 0;
										}
										if (num6 > text10.Length)
										{
											num6 = text10.Length;
										}
										break;
									}
									}
									break;
								}
								case "insert":
								case "add":
									if (Operators.CompareString(text11, "", TextCompare: false) == 0)
									{
										if (Operators.CompareString(attribute, "before", TextCompare: false) == 0)
										{
											text10 = text10.Substring(0, num5) + text12 + text10.Substring(num5);
											num6 = num5 + text12.Length;
											break;
										}
										if (Operators.CompareString(attribute, "after", TextCompare: false) == 0)
										{
											text10 = text10.Substring(0, num6) + text12 + text10.Substring(num6);
											num5 = num6;
											num6 = num5 + text12.Length;
											break;
										}
										throw new Exception("insert operation without position attribute at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
									}
									if (Operators.CompareString(attribute, "before", TextCompare: false) == 0)
									{
										text10 = text10.Replace(text11, text12 + text11);
									}
									else
									{
										if (Operators.CompareString(attribute, "after", TextCompare: false) != 0)
										{
											throw new Exception("insert operation without position attribute at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
										}
										text10 = text10.Replace(text11, text11 + text12);
									}
									text11 = "";
									num5 = 0;
									num6 = 0;
									break;
								case "replace":
									if (Operators.CompareString(text11, "", TextCompare: false) == 0)
									{
										text10 = text10.Substring(0, num5) + text12 + text10.Substring(num6);
										num6 = num5 + text12.Length;
										break;
									}
									text10 = text10.Replace(text11, text12);
									text11 = "";
									num5 = 0;
									num6 = 0;
									break;
								case "delete":
									if (Operators.CompareString(text11, "", TextCompare: false) == 0)
									{
										text10 = text10.Substring(0, num5) + text10.Substring(num6);
										num6 = num5;
										break;
									}
									text10 = text10.Replace(text11, "");
									text11 = "";
									num5 = 0;
									num6 = 0;
									break;
								case "findall":
									if (Operators.CompareString(text11, "", TextCompare: false) != 0)
									{
										throw new Exception("findall operation not followed by insert, add, replace or delete at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
									}
									if (Operators.CompareString(text12, "", TextCompare: false) == 0)
									{
										throw new Exception("findall operation without search term at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
									}
									text11 = text12;
									break;
								default:
									throw new Exception("Unknown operation \"" + name + "\" at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
								}
							}
							dictionary[text9] = Encode(text10, Encoding6);
							break;
						}
						case "editxmlfile":
							throw new Exception("editxmlfile as issued by line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8 + " is not yet implemented!");
						default:
							throw new Exception("Unknown element \"" + xmlTextReader.Name + "\" at line " + Conversions.ToString(xmlTextReader.LineNumber) + " of " + text8);
						case "requirement":
						case "incompatibility":
						case "applybefore":
						case "applyafter":
							break;
						}
					}
				}
				text = "";
				ZipFile zipFile = new ZipFile();
				zipFile.CompressionLevel = CompressionLevel.BestCompression;
				foreach (KeyValuePair<string, Stream> item4 in dictionary)
				{
					item4.Value.Seek(0L, SeekOrigin.Begin);
					zipFile.AddEntry(item4.Key, null, item4.Value);
				}
				zipFile.Comment = text2;
				if (ExportPath == null)
				{
					zipFile.Save(Path.Combine(ModsDir, "resources999.s2z"));
					AppliedMods = DeepCopyDictionary(EnabledMods);
					AppliedGameVersion = GameVersion;
				}
				else
				{
					zipFile.Save(ExportPath);
				}
				myStatusLabel.Text = "Great Success!";
				if (!SilentSuccess)
				{
					MessageBox.Show("Great Success!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				}
				result2 = true;
			}
			catch (Exception ex)
			{
				ProjectData.SetProjectError(ex);
				Exception ex2 = ex;
				if (Operators.CompareString(text, "", TextCompare: false) == 0)
				{
					MessageBox.Show("A problem occurred: " + Environment.NewLine + Environment.NewLine + ex2.Message, "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
				else
				{
					MessageBox.Show("\"" + text + "\" caused a problem: " + Environment.NewLine + Environment.NewLine + ex2.Message, "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
				result2 = false;
				ProjectData.ClearProjectError();
			}
			finally
			{
				myStatusLabel.Text = Mods.Count + " mods loaded.";
				myListView.Enabled = true;
			}
			goto IL_16d9;
		}
		IL_020f:
		MessageBox.Show("A mod vanished!", "HoN_ModMan", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		myStatusLabel.Text = Mods.Count + " mods loaded.";
		goto IL_16d9;
		IL_16d9:
		return result2;
	}

	private static bool EvalCondition(string Condition, List<Modification> Mods)
	{
		if (Operators.CompareString(Condition, "", TextCompare: false) == 0)
		{
			return true;
		}
		Condition = Condition.TrimStart();
		bool flag = false;
		if (Condition.StartsWith("not", ignoreCase: true, null))
		{
			flag = true;
			Condition = Condition.Substring("not".Length).TrimStart();
		}
		checked
		{
			bool flag2;
			if (Condition.StartsWith("'"))
			{
				int num = Condition.IndexOf('\'', 1);
				string text = Condition.Substring(1, num - 1);
				string requirement = "*";
				if (text.EndsWith("]"))
				{
					int num2 = text.LastIndexOf("[v");
					if (num2 >= 0)
					{
						requirement = text.Substring(num2 + 2, text.Length - (num2 + 2) - 1);
						text = text.Substring(0, num2);
					}
				}
				flag2 = false;
				text = FixModName(text);
				foreach (Modification Mod in Mods)
				{
					if ((Operators.CompareString(Mod.FixedName, text, TextCompare: false) == 0) & VersionsMatch(requirement, Mod.Version))
					{
						flag2 = true;
						break;
					}
				}
				Condition = Condition.Substring(num + 1);
			}
			else
			{
				if (!Condition.StartsWith("("))
				{
					throw new Exception();
				}
				int num3 = 1;
				bool flag3 = false;
				int length = Condition.Length;
				int i;
				for (i = 1; i <= length; i++)
				{
					switch (Condition[i])
					{
					case '(':
						if (!flag3)
						{
							num3++;
						}
						continue;
					case ')':
						if (!flag3)
						{
							num3--;
						}
						if (num3 != 0)
						{
							continue;
						}
						break;
					case '\'':
						flag3 = !flag3;
						continue;
					default:
						continue;
					}
					break;
				}
				flag2 = EvalCondition(Condition.Substring(1, i - 1), Mods);
				Condition = Condition.Substring(i + 1);
			}
			flag2 ^= flag;
			Condition = Condition.TrimStart();
			if (Operators.CompareString(Condition, "", TextCompare: false) == 0)
			{
				return flag2;
			}
			if (Condition.StartsWith("and", ignoreCase: true, null))
			{
				if (!flag2)
				{
					return false;
				}
				return EvalCondition(Condition.Substring("and".Length), Mods);
			}
			if (Condition.StartsWith("or", ignoreCase: true, null))
			{
				if (flag2)
				{
					return true;
				}
				return EvalCondition(Condition.Substring("or".Length), Mods);
			}
			bool result = default(bool);
			return result;
		}
	}

	private static string FixFilename(string s)
	{
		if (s == null)
		{
			return "";
		}
		string text = s.Trim().Replace('\\', '/');
		if (text.StartsWith("/"))
		{
			text = text.Substring(1);
		}
		return text;
	}

	private static string Decode(Stream Data, ref Encoding Encoding)
	{
		StreamReader streamReader = new StreamReader(Data);
		string text = streamReader.ReadToEnd();
		Encoding = streamReader.CurrentEncoding;
		return text.Replace(Conversions.ToString(Convert.ToChar(13)), "");
	}

	private static Stream Encode(string Data, Encoding Encoding)
	{
		MemoryStream memoryStream = new MemoryStream();
		StreamWriter streamWriter = new StreamWriter(memoryStream, Encoding);
		streamWriter.Write(Data);
		streamWriter.Flush();
		return memoryStream;
	}

	public static bool IsWindows()
	{
		return Environment.OSVersion.Platform == PlatformID.Win32NT;
	}

	public static bool IsLinux()
	{
		if (Environment.OSVersion.Platform != PlatformID.Unix)
		{
			return false;
		}
		bool result;
		try
		{
			result = !Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support"));
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			result = true;
			ProjectData.ClearProjectError();
		}
		return result;
	}

	public static bool IsMacOS()
	{
		if (Environment.OSVersion.Platform == PlatformID.MacOSX)
		{
			return true;
		}
		if (Environment.OSVersion.Platform != PlatformID.Unix)
		{
			return false;
		}
		bool result;
		try
		{
			result = Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support"));
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			result = false;
			ProjectData.ClearProjectError();
		}
		return result;
	}

	private void GetGameVersion()
	{
		GameVersion = "";
		checked
		{
			if (Operators.CompareString(GameDir, "", TextCompare: false) != 0)
			{
				try
				{
					if (File.Exists(Path.Combine(GameDir, "hon.exe")))
					{
						byte[] array = ReadFile(Path.Combine(GameDir, "hon.exe"));
						int num = FindInByteStream(array, new byte[20]
						{
							67, 0, 85, 0, 82, 0, 69, 0, 32, 0,
							67, 0, 82, 0, 84, 0, 93, 0, 0, 0
						});
						if (num >= 0)
						{
							num += 20;
							int num2;
							do
							{
								num2 = array[num] + 256 * array[num + 1];
								if (num2 > 0)
								{
									GameVersion += Conversions.ToString(Convert.ToChar(num2));
								}
								num += 2;
							}
							while (!((num2 == 0) | (GameVersion.Length >= 10)));
						}
						RunGameFile = Path.Combine(GameDir, "hon.exe");
						ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = true;
					}
					else if (File.Exists(Path.Combine(GameDir, "hon-x86")) | File.Exists(Path.Combine(GameDir, "hon-x86_64")))
					{
						byte[] array = ((!File.Exists(Path.Combine(GameDir, "hon-x86"))) ? ReadFile(Path.Combine(GameDir, "hon-x86_64")) : ReadFile(Path.Combine(GameDir, "hon-x86")));
						int num3 = FindInByteStream(array, new byte[40]
						{
							91, 0, 0, 0, 85, 0, 0, 0, 78, 0,
							0, 0, 73, 0, 0, 0, 67, 0, 0, 0,
							79, 0, 0, 0, 68, 0, 0, 0, 69, 0,
							0, 0, 93, 0, 0, 0, 0, 0, 0, 0
						});
						if (num3 >= 0)
						{
							num3 += 40;
							int num4;
							do
							{
								num4 = array[num3] + 256 * (array[num3 + 1] + 256 * (array[num3 + 2] + 256 * array[num3 + 3]));
								if (num4 > 0)
								{
									GameVersion += Conversions.ToString(Convert.ToChar(num4));
								}
								num3 += 4;
							}
							while (!((num4 == 0) | (GameVersion.Length >= 10)));
						}
						RunGameFile = Path.Combine(GameDir, "hon.sh");
						ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = true;
					}
					else if (File.Exists(Path.Combine(GameDir, "HoN")))
					{
						byte[] array = ReadFile(Path.Combine(GameDir, "HoN"));
						int num5 = FindInByteStream(array, new byte[40]
						{
							91, 0, 0, 0, 85, 0, 0, 0, 78, 0,
							0, 0, 73, 0, 0, 0, 67, 0, 0, 0,
							79, 0, 0, 0, 68, 0, 0, 0, 69, 0,
							0, 0, 93, 0, 0, 0, 0, 0, 0, 0
						});
						if (num5 >= 0)
						{
							num5 += 40;
							int num6;
							do
							{
								num6 = array[num5] + 256 * (array[num5 + 1] + 256 * (array[num5 + 2] + 256 * array[num5 + 3]));
								if (num6 > 0)
								{
									GameVersion += Conversions.ToString(Convert.ToChar(num6));
								}
								num5 += 4;
							}
							while (!((num6 == 0) | (GameVersion.Length >= 10)));
						}
						RunGameFile = GameDir;
						ApplyModsAndLaunchHoNToolStripMenuItem.Enabled = true;
					}
				}
				catch (Exception projectError)
				{
					ProjectData.SetProjectError(projectError);
					ProjectData.ClearProjectError();
				}
				if (Operators.CompareString(GameVersion, "", TextCompare: false) == 0)
				{
					MessageBox.Show("Could not detect Heroes of Newerth version. Version checks have been disabled." + Environment.NewLine + Environment.NewLine + "Always close HoN before running the mod manager.", "HoN Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					myAppVerLabel.Text = "";
					myAppVerLabel.Visible = false;
				}
				else
				{
					myAppVerLabel.Text = "Game Version: " + GameVersion;
					myAppVerLabel.Visible = true;
				}
			}
			else
			{
				myAppVerLabel.Text = "";
				myAppVerLabel.Visible = false;
			}
		}
	}

	private static byte[] ReadFile(string Path)
	{
		FileStream fileStream = new FileStream(Path, FileMode.Open);
		byte[] array = new byte[checked(Convert.ToInt32(fileStream.Length) - 1 + 1)];
		fileStream.Read(array, 0, array.Length);
		fileStream.Close();
		return array;
	}

	private static int FindInByteStream(byte[] Haystack, byte[] Needle)
	{
		checked
		{
			int num = Haystack.Length - Needle.Length;
			int num2 = num;
			for (int i = 0; i <= num2; i++)
			{
				int num3 = Needle.Length - 1;
				int j;
				for (j = 0; j <= num3 && Haystack[i + j] == Needle[j]; j++)
				{
				}
				if (j >= Needle.Length)
				{
					return i;
				}
			}
			return -1;
		}
	}

	private static bool VersionsMatch(string Requirement, string Version)
	{
		if (Operators.CompareString(Requirement, "", TextCompare: false) == 0)
		{
			Requirement = "*";
		}
		if (Operators.CompareString(Version, "", TextCompare: false) == 0)
		{
			Version = "0";
		}
		string[] array = Requirement.Split('-');
		if (array.Length > 2)
		{
			return false;
		}
		int[] array2 = StrArrayToIntArray(Version.Split('.'));
		checked
		{
			if (array.Length == 1)
			{
				int[] array3 = StrArrayToIntArray(array[0].Split('.'));
				int num = Math.Min(array2.Length - 1, array3.Length - 1);
				for (int i = 0; i <= num; i++)
				{
					if ((array3[i] != int.MinValue) & (array3[i] != array2[i]))
					{
						return false;
					}
				}
				if (array3.Length > array2.Length)
				{
					int num2 = array2.Length;
					int num3 = array3.Length - 1;
					for (int j = num2; j <= num3; j++)
					{
						if (array3[j] != 0)
						{
							return false;
						}
					}
				}
				return true;
			}
			if (Operators.CompareString(array[0], "", TextCompare: false) == 0)
			{
				array[0] = "*";
			}
			if (Operators.CompareString(array[1], "", TextCompare: false) == 0)
			{
				array[1] = "*";
			}
			int[] array4 = StrArrayToIntArray(array[0].Split('.'));
			int num4 = Math.Min(array2.Length - 1, array4.Length - 1);
			for (int k = 0; k <= num4; k++)
			{
				if (array4[k] != int.MinValue)
				{
					if (array4[k] > array2[k])
					{
						return false;
					}
					if (array4[k] < array2[k])
					{
						break;
					}
				}
			}
			array4 = StrArrayToIntArray(array[1].Split('.'));
			int num5 = Math.Min(array2.Length - 1, array4.Length - 1);
			for (int l = 0; l <= num5; l++)
			{
				if (array4[l] != int.MinValue)
				{
					if (array4[l] < array2[l])
					{
						return false;
					}
					if (array4[l] > array2[l])
					{
						break;
					}
				}
			}
			return true;
		}
	}

	private static bool IsNewerVersion(string Old, string New)
	{
		if (Operators.CompareString(Old, "", TextCompare: false) == 0)
		{
			Old = "0";
		}
		if (Operators.CompareString(New, "", TextCompare: false) == 0)
		{
			New = "0";
		}
		int[] array = StrArrayToIntArray(Old.Split('.'));
		int[] array2 = StrArrayToIntArray(New.Split('.'));
		checked
		{
			int num = Math.Min(array.Length - 1, array2.Length - 1);
			for (int i = 0; i <= num; i++)
			{
				if (array[i] != array2[i])
				{
					return array2[i] > array[i];
				}
			}
			if (array.Length != array2.Length)
			{
				return array2.Length > array.Length;
			}
			return true;
		}
	}

	private static int[] StrArrayToIntArray(string[] s)
	{
		checked
		{
			int[] array = new int[s.Length - 1 + 1];
			int num = s.Length - 1;
			for (int i = 0; i <= num; i++)
			{
				if (int.TryParse(RemoveNonDigits(s[i]), out var result))
				{
					array[i] = result;
				}
				else
				{
					array[i] = 0;
				}
			}
			return array;
		}
	}

	private static string RemoveNonDigits(string s)
	{
		if (Operators.CompareString(s, "*", TextCompare: false) == 0)
		{
			return int.MinValue.ToString();
		}
		string text = "";
		checked
		{
			int num = s.Length - 1;
			for (int i = 0; i <= num; i++)
			{
				if ((s[i] >= '0') & (s[i] <= '9'))
				{
					text += Conversions.ToString(s[i]);
				}
			}
			return text;
		}
	}

	private static bool IsValidGameDir(string Path)
	{
		if (Operators.CompareString(Path, "", TextCompare: false) == 0)
		{
			return false;
		}
		bool result;
		try
		{
			if (!Directory.Exists(System.IO.Path.Combine(Path, "game")))
			{
				result = false;
				goto IL_0066;
			}
			FileInfo fileInfo = new FileInfo(System.IO.Path.Combine(Path, System.IO.Path.Combine("game", "resources0.s2z")));
			if (fileInfo.Length < 1048576)
			{
				result = false;
				goto IL_0066;
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			result = false;
			ProjectData.ClearProjectError();
			goto IL_0066;
		}
		return true;
		IL_0066:
		return result;
	}

	private static string DetectGameDir()
	{
		string registryEntry = GetRegistryEntry("hondir");
		if (IsValidGameDir(registryEntry))
		{
			return registryEntry;
		}
		if (IsLinux())
		{
			registryEntry = GetGameDirLinux();
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			try
			{
				registryEntry = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Heroes of Newerth");
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			catch (Exception projectError)
			{
				ProjectData.SetProjectError(projectError);
				ProjectData.ClearProjectError();
			}
			try
			{
				registryEntry = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/HoN";
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			catch (Exception projectError2)
			{
				ProjectData.SetProjectError(projectError2);
				ProjectData.ClearProjectError();
			}
		}
		else if (IsMacOS())
		{
			try
			{
				registryEntry = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Heroes of Newerth");
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			catch (Exception projectError3)
			{
				ProjectData.SetProjectError(projectError3);
				ProjectData.ClearProjectError();
			}
			registryEntry = "/applications/heroes of newerth.app";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			registryEntry = "/Applications/Heroes of Newerth.app";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
		}
		else
		{
			registryEntry = GetGameDirWinReg();
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			try
			{
				registryEntry = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Heroes of Newerth");
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			catch (Exception projectError4)
			{
				ProjectData.SetProjectError(projectError4);
				registryEntry = "";
				ProjectData.ClearProjectError();
			}
			if (registryEntry.ToLower().StartsWith("c:\\"))
			{
				registryEntry = "d:\\" + registryEntry.Substring(3);
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
				registryEntry = "e:\\" + registryEntry.Substring(3);
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			try
			{
				registryEntry = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
				if ((Operators.CompareString(registryEntry, null, TextCompare: false) == 0) | (Operators.CompareString(registryEntry, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), TextCompare: false) == 0))
				{
					registryEntry = "";
				}
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			catch (Exception projectError5)
			{
				ProjectData.SetProjectError(projectError5);
				registryEntry = "";
				ProjectData.ClearProjectError();
			}
			if (registryEntry.ToLower().StartsWith("c:\\"))
			{
				registryEntry = "d:\\" + registryEntry.Substring(3);
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
				registryEntry = "e:\\" + registryEntry.Substring(3);
				if (IsValidGameDir(registryEntry))
				{
					return registryEntry;
				}
			}
			registryEntry = "D:\\Heroes of Newerth";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			registryEntry = "E:\\Heroes of Newerth";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			registryEntry = "C:\\Games\\Heroes of Newerth";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			registryEntry = "D:\\Games\\Heroes of Newerth";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
			registryEntry = "E:\\Games\\Heroes of Newerth";
			if (IsValidGameDir(registryEntry))
			{
				return registryEntry;
			}
		}
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "I couldn't find your HoN install, please point me to the folder containing the binary!" + Environment.NewLine + "Press Cancel to enter the path manually.";
		if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
		{
			registryEntry = folderBrowserDialog.SelectedPath;
		}
		else
		{
			frmInputbox frmInputbox2 = new frmInputbox();
			frmInputbox2.Text = "Enter HoN path manually:";
			if (frmInputbox2.ShowDialog() != DialogResult.OK)
			{
				return "";
			}
			registryEntry = frmInputbox2.Result;
		}
		if (IsValidGameDir(registryEntry))
		{
			return registryEntry;
		}
		registryEntry = TryFixUserPath(registryEntry);
		if (Operators.CompareString(registryEntry, "", TextCompare: false) != 0)
		{
			return registryEntry;
		}
		return "";
	}

	private static string GetGameDirWinReg()
	{
		string result;
		try
		{
			RegistryKey localMachine = Registry.LocalMachine;
			if (localMachine == null)
			{
				result = "";
			}
			else
			{
				localMachine = localMachine.OpenSubKey("SOFTWARE");
				if (localMachine == null)
				{
					result = "";
				}
				else
				{
					localMachine = localMachine.OpenSubKey("Microsoft");
					if (localMachine == null)
					{
						result = "";
					}
					else
					{
						localMachine = localMachine.OpenSubKey("Windows");
						if (localMachine == null)
						{
							result = "";
						}
						else
						{
							localMachine = localMachine.OpenSubKey("CurrentVersion");
							if (localMachine == null)
							{
								result = "";
							}
							else
							{
								localMachine = localMachine.OpenSubKey("Uninstall");
								if (localMachine == null)
								{
									result = "";
								}
								else
								{
									localMachine = localMachine.OpenSubKey("hon");
									if (localMachine == null)
									{
										result = "";
									}
									else
									{
										string text = (string)localMachine.GetValue("InstallLocation", "");
										if (Operators.CompareString(text, "", TextCompare: false) == 0)
										{
											result = "";
										}
										else if (Directory.Exists(text))
										{
											string text2 = text.Replace(" Test Client", "");
											result = ((!((Operators.CompareString(text, text2, TextCompare: false) != 0) & Directory.Exists(text2))) ? text : text2);
										}
										else
										{
											result = "";
										}
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			result = "";
			ProjectData.ClearProjectError();
		}
		return result;
	}

	private static string GetGameDirLinux()
	{
		try
		{
			StreamReader streamReader = new StreamReader(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/applications/s2games_com-HoN_1.desktop");
			do
			{
				string text = streamReader.ReadLine();
				if (text.StartsWith("Exec=") && text.EndsWith("/hon.sh"))
				{
					return text.Substring("Exec=".Length, checked(text.Length - "Exec=".Length - "/hon.sh".Length));
				}
			}
			while (!streamReader.EndOfStream);
		}
		catch (Exception projectError)
		{
			ProjectData.SetProjectError(projectError);
			ProjectData.ClearProjectError();
		}
		return "";
	}

	private static string TryFixUserPath(string Path)
	{
		checked
		{
			if (IsWindows())
			{
				Path = Path.ToLower().Replace('/', '\\');
				if (Path.EndsWith("\\"))
				{
					Path = Path.Substring(0, Path.Length - 1);
				}
				if (Path.EndsWith("mods"))
				{
					Path = Path.Substring(0, Path.Length - 5);
					if (IsValidGameDir(Path))
					{
						return Path;
					}
				}
				if (Path.EndsWith("game"))
				{
					Path = Path.Substring(0, Path.Length - 5);
					if (IsValidGameDir(Path))
					{
						return Path;
					}
				}
			}
			else
			{
				Path = Path.ToLower().Replace('\\', '/');
				if (Path.EndsWith("/"))
				{
					Path = Path.Substring(0, Path.Length - 1);
				}
				if (Path.EndsWith("mods"))
				{
					Path = Path.Substring(0, Path.Length - 5);
					if (IsValidGameDir(Path))
					{
						return Path;
					}
				}
				if (Path.EndsWith("game"))
				{
					Path = Path.Substring(0, Path.Length - 5);
					if (IsValidGameDir(Path))
					{
						return Path;
					}
				}
			}
			return "";
		}
	}

	private void ChangeHoNPathToolStripMenuItem_Click(object sender, EventArgs e)
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "Choose a HoN install - point to the folder containing the binary!";
		folderBrowserDialog.SelectedPath = GameDir;
		folderBrowserDialog.ShowNewFolderButton = false;
		if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
		{
			string text = folderBrowserDialog.SelectedPath;
			if (!IsValidGameDir(text))
			{
				text = TryFixUserPath(text);
			}
			if (Operators.CompareString(text, "", TextCompare: false) == 0)
			{
				MessageBox.Show("Invalid path. The path you specified either does not point to a Heroes of Newerth install or is inaccessible to this application.", "Error verifying HoN install", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			else
			{
				SetGameDir(text);
			}
		}
	}

	private void EnterHoNPathmanuallyToolStripMenuItem_Click(object sender, EventArgs e)
	{
		frmInputbox frmInputbox2 = new frmInputbox();
		frmInputbox2.Text = "Enter HoN path manually:";
		frmInputbox2.Result = GameDir;
		if (frmInputbox2.ShowDialog() == DialogResult.OK)
		{
			string text = frmInputbox2.Result;
			if (!IsValidGameDir(text))
			{
				text = TryFixUserPath(text);
			}
			if (Operators.CompareString(text, "", TextCompare: false) == 0)
			{
				MessageBox.Show("Invalid path. The path you specified either does not point to a Heroes of Newerth install or is inaccessible to this application.", "Error verifying HoN install", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			else
			{
				SetGameDir(text);
			}
		}
	}
}
