﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

public delegate void DiceRollerOpenHandler(Object sender);
public delegate void DiceRollerOpenIntHandler(Chummer.Character objCharacter, int intDice);

namespace Chummer
{
	public partial class frmCareer : Form
	{
		// Set the default culture to en-US so we work with decimals correctly.
		private Character _objCharacter;
		private MainController _objController;

		private CharacterOptions _objOptions;
		private CommonFunctions _objFunctions;
		private bool _blnSkipRefresh = false;
		private bool _blnSkipUpdate = false;
		private bool _blnLoading = false;
		private bool _blnIsDirty = false;
		private bool _blnSkipToolStripRevert = false;
		private bool _blnReapplyImprovements = false;
		private int _intDragLevel = 0;
		private MouseButtons _objDragButton = new MouseButtons();
		private bool _blnDraggingGear = false;

		private readonly ListViewColumnSorter _lvwKarmaColumnSorter;
		private readonly ListViewColumnSorter _lvwNuyenColumnSorter;

		public event DiceRollerOpenHandler DiceRollerOpened;
		public event DiceRollerOpenIntHandler DiceRollerOpenedInt;
		
		// Create the XmlManager that will handle finding all of the XML files.
		private ImprovementManager _objImprovementManager;

		#region Form Events
		public frmCareer(Character objCharacter)
		{
			_objCharacter = objCharacter;
			_objOptions = _objCharacter.Options;
			_objFunctions = new CommonFunctions(_objCharacter);
			_objImprovementManager = new ImprovementManager(_objCharacter);
			_objController = new MainController(_objCharacter);
			InitializeComponent();

			// Add EventHandlers for the MAG and RES enabled events and tab enabled events.
			_objCharacter.MAGEnabledChanged += objCharacter_MAGEnabledChanged;
			_objCharacter.RESEnabledChanged += objCharacter_RESEnabledChanged;
			_objCharacter.AdeptTabEnabledChanged += objCharacter_AdeptTabEnabledChanged;
			_objCharacter.MagicianTabEnabledChanged += objCharacter_MagicianTabEnabledChanged;
			_objCharacter.TechnomancerTabEnabledChanged += objCharacter_TechnomancerTabEnabledChanged;
			_objCharacter.CritterTabEnabledChanged += objCharacter_CritterTabEnabledChanged;
			_objCharacter.BlackMarketEnabledChanged += objCharacter_BlackMarketChanged;
			_objCharacter.UneducatedChanged += objCharacter_UneducatedChanged;
			_objCharacter.UncouthChanged += objCharacter_UncouthChanged;
			_objCharacter.InfirmChanged += objCharacter_InfirmChanged;
			GlobalOptions.Instance.MRUChanged += PopulateMRU;

			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);

			// Update the text in the Menus so they can be merged with frmMain properly.
			foreach (ToolStripMenuItem objItem in mnuCreateMenu.Items.OfType<ToolStripMenuItem>())
			{
				if (objItem.Tag != null)
				{
					objItem.Text = LanguageManager.Instance.GetString(objItem.Tag.ToString());
				}
			}

			_lvwKarmaColumnSorter = new ListViewColumnSorter();
			_lvwKarmaColumnSorter.SortColumn = 0;
			_lvwKarmaColumnSorter.Order = SortOrder.Descending;
			lstKarma.ListViewItemSorter = _lvwKarmaColumnSorter;
			_lvwNuyenColumnSorter = new ListViewColumnSorter();
			_lvwNuyenColumnSorter.SortColumn = 0;
			_lvwNuyenColumnSorter.Order = SortOrder.Descending;
			lstNuyen.ListViewItemSorter = _lvwNuyenColumnSorter;

			SetTooltips();
			MoveControls();
		}

		/// <summary>
		/// Set the form to Loading mode so that certain events do not fire while data is being populated.
		/// </summary>
		public bool Loading
		{
			set
			{
				_blnLoading = value;
			}
		}

		private void TreeView_MouseDown(object sender, MouseEventArgs e)
		{
			// Generic event for all TreeViews to allow right-clicking to select a TreeNode so the proper ContextMenu is shown.
			//if (e.Button == System.Windows.Forms.MouseButtons.Right)
			//{
				TreeView objTree = (TreeView)sender;
				objTree.SelectedNode = objTree.HitTest(e.X, e.Y).Node;
			//}
		}

		private void frmCareer_Load(object sender, EventArgs e)
		{
			_blnLoading = true;

			// Remove the Magician, Adept, and Technomancer tabs since they are not in use until the appropriate Quality is selected.
			if (!_objCharacter.MagicianEnabled)
				tabCharacterTabs.TabPages.Remove(tabMagician);
			if (!_objCharacter.AdeptEnabled)
				tabCharacterTabs.TabPages.Remove(tabAdept);
			if (!_objCharacter.TechnomancerEnabled)
				tabCharacterTabs.TabPages.Remove(tabTechnomancer);
			if (!_objCharacter.CritterEnabled)
				tabCharacterTabs.TabPages.Remove(tabCritter);

			mnuSpecialAddBiowareSuite.Visible = _objCharacter.Options.AllowBiowareSuites;

			// Remove the Improvements Tab.
			//tabCharacterTabs.TabPages.Remove(tabImprovements);

			// Remove the Initiation tab if the character does not have access to MAG or RES.
			if (!_objCharacter.MAGEnabled && !_objCharacter.RESEnabled)
				tabCharacterTabs.TabPages.Remove(tabInitiation);
			else
			{
				if (_objCharacter.MAGEnabled)
				{
					tabInitiation.Text = LanguageManager.Instance.GetString("Tab_Initiation");
					lblInitiateGradeLabel.Text = LanguageManager.Instance.GetString("Label_InitiationGrade");
					cmdAddMetamagic.Text = LanguageManager.Instance.GetString("Button_AddMetamagic");
					chkInitiationGroup.Text = LanguageManager.Instance.GetString("Checkbox_GroupInitiation");
					chkInitiationOrdeal.Text = LanguageManager.Instance.GetString("Checkbox_InitiationOrdeal");
					chkJoinGroup.Text = LanguageManager.Instance.GetString("Checkbox_JoinedGroup");
					chkJoinGroup.Checked = _objCharacter.GroupMember;
					txtGroupName.Text = _objCharacter.GroupName;
					txtGroupNotes.Text = _objCharacter.GroupNotes;
					string strInitTip = LanguageManager.Instance.GetString("Tip_ImproveInitiateGrade").Replace("{0}", (_objCharacter.InitiateGrade + 1).ToString()).Replace("{1}", (10 + ((_objCharacter.InitiateGrade + 1) * _objOptions.KarmaInitiation)).ToString());
					tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
				}
				else
				{
					tabInitiation.Text = LanguageManager.Instance.GetString("Tab_Submersion");
					lblInitiateGradeLabel.Text = LanguageManager.Instance.GetString("Label_SubmersionGrade");
					cmdAddMetamagic.Text = LanguageManager.Instance.GetString("Button_AddEcho");
					chkInitiationGroup.Text = LanguageManager.Instance.GetString("Checkbox_NetworkSubmersion");
					chkInitiationOrdeal.Text = LanguageManager.Instance.GetString("Checkbox_SubmersionTask");
					chkJoinGroup.Text = LanguageManager.Instance.GetString("Checkbox_JoinedNetwork");
					chkJoinGroup.Checked = _objCharacter.GroupMember;
					txtGroupName.Text = _objCharacter.GroupName;
					txtGroupNotes.Text = _objCharacter.GroupNotes;
					string strInitTip = LanguageManager.Instance.GetString("Tip_ImproveSubmersionGrade").Replace("{0}", (_objCharacter.SubmersionGrade + 1).ToString()).Replace("{1}", (10 + ((_objCharacter.SubmersionGrade + 1) * _objOptions.KarmaInitiation)).ToString());
					tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
				}
			}

			// If the character has a mugshot, decode it and put it in the PictureBox.
			if (_objCharacter.Mugshot != "")
			{
				byte[] bytImage = Convert.FromBase64String(_objCharacter.Mugshot);
				MemoryStream objStream = new MemoryStream(bytImage, 0, bytImage.Length);
				objStream.Write(bytImage, 0, bytImage.Length);
				Image imgMugshot = Image.FromStream(objStream, true);
				picMugshot.Image = imgMugshot;
			}

			// Populate character information fields.
			XmlDocument objMetatypeDoc = new XmlDocument();
			XmlNode objMetatypeNode;
			string strMetatype = "";
			string strBook = "";
			string strPage = "";
			
			objMetatypeDoc = XmlManager.Instance.Load("metatypes.xml");
			{
				objMetatypeNode = objMetatypeDoc.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + _objCharacter.Metatype + "\"]");
				if (objMetatypeNode == null)
					objMetatypeDoc = XmlManager.Instance.Load("critters.xml");
				objMetatypeNode = objMetatypeDoc.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + _objCharacter.Metatype + "\"]");

				if (objMetatypeNode["translate"] != null)
					strMetatype = objMetatypeNode["translate"].InnerText;
				else
					strMetatype = _objCharacter.Metatype;

				strBook = _objOptions.LanguageBookShort(objMetatypeNode["source"].InnerText);
				if (objMetatypeNode["altpage"] != null)
					strPage = objMetatypeNode["altpage"].InnerText;
				else
					strPage = objMetatypeNode["page"].InnerText;

				if (_objCharacter.Metavariant != "")
				{
					objMetatypeNode = objMetatypeNode.SelectSingleNode("metavariants/metavariant[name = \"" + _objCharacter.Metavariant + "\"]");

					if (objMetatypeNode["translate"] != null)
						strMetatype += " (" + objMetatypeNode["translate"].InnerText + ")";
					else
						strMetatype += " (" + _objCharacter.Metavariant + ")";

					strBook = _objOptions.LanguageBookShort(objMetatypeNode["source"].InnerText);
					if (objMetatypeNode["altpage"] != null)
						strPage = objMetatypeNode["altpage"].InnerText;
					else
						strPage = objMetatypeNode["page"].InnerText;
				}
			}
			lblMetatype.Text = strMetatype;
			lblMetatypeSource.Text = strBook + " " + strPage;
			if (_objCharacter.Possessed)
				lblPossessed.Text = LanguageManager.Instance.GetString("String_Possessed");
			else
				lblPossessed.Visible = false;
			tipTooltip.SetToolTip(lblMetatypeSource, _objOptions.LanguageBookLong(objMetatypeNode["source"].InnerText) + " " + LanguageManager.Instance.GetString("String_Page") + " " + strPage);

			txtCharacterName.Text = _objCharacter.Name;
			txtSex.Text = _objCharacter.Sex;
			txtAge.Text = _objCharacter.Age;
			txtEyes.Text = _objCharacter.Eyes;
			txtHeight.Text = _objCharacter.Height;
			txtWeight.Text = _objCharacter.Weight;
			txtSkin.Text = _objCharacter.Skin;
			txtHair.Text = _objCharacter.Hair;
			txtDescription.Text = _objCharacter.Description;
			txtBackground.Text = _objCharacter.Background;
			txtConcept.Text = _objCharacter.Concept;
			txtNotes.Text = _objCharacter.Notes;
			txtAlias.Text = _objCharacter.Alias;
			txtPlayerName.Text = _objCharacter.PlayerName;
			txtGameNotes.Text = _objCharacter.GameNotes;
			nudStreetCred.Value = _objCharacter.StreetCred;
			nudNotoriety.Value = _objCharacter.Notoriety;
			nudPublicAware.Value = _objCharacter.PublicAwareness;

			// Check for Special Attributes.
			lblMAGLabel.Enabled = _objCharacter.MAGEnabled;
			lblMAGAug.Enabled = _objCharacter.MAGEnabled;
			lblMAG.Enabled = _objCharacter.MAGEnabled;
			lblMAGMetatype.Enabled = _objCharacter.MAGEnabled;
			lblFoci.Visible = _objCharacter.MAGEnabled;
			treFoci.Visible = _objCharacter.MAGEnabled;
			cmdCreateStackedFocus.Visible = _objCharacter.MAGEnabled;

			lblRESLabel.Enabled = _objCharacter.RESEnabled;
			lblRESAug.Enabled = _objCharacter.RESEnabled;
			lblRES.Enabled = _objCharacter.RESEnabled;
			lblRESMetatype.Enabled = _objCharacter.RESEnabled;

			// Define the XML objects that will be used.
			XmlDocument objXmlDocument = new XmlDocument();

			// Populate the Qualities list.
			foreach (Quality objQuality in _objCharacter.Qualities)
			{
				TreeNode objNode = new TreeNode();
				objNode.Text = objQuality.DisplayName;
				objNode.Tag = objQuality.InternalId;
				objNode.ContextMenuStrip = cmsQuality;

				if (objQuality.Notes != string.Empty)
					objNode.ForeColor = Color.SaddleBrown;
				else
				{
					if (objQuality.OriginSource == QualitySource.Metatype || objQuality.OriginSource == QualitySource.MetatypeRemovable)
						objNode.ForeColor = SystemColors.GrayText;
				}
				objNode.ToolTipText = objQuality.Notes;

				if (objQuality.Type == QualityType.Positive)
				{
					treQualities.Nodes[0].Nodes.Add(objNode);
					treQualities.Nodes[0].Expand();
				}
				else
				{
					treQualities.Nodes[1].Nodes.Add(objNode);
					treQualities.Nodes[1].Expand();
				}
			}

			// Populate the Magician Traditions list.
			objXmlDocument = XmlManager.Instance.Load("traditions.xml");
			List<ListItem> lstTraditions = new List<ListItem>();
			ListItem objBlank = new ListItem();
			objBlank.Value = "";
			objBlank.Name = "";
			lstTraditions.Add(objBlank);
			foreach (XmlNode objXmlTradition in objXmlDocument.SelectNodes("/chummer/traditions/tradition[" + _objOptions.BookXPath() + "]"))
			{
				ListItem objItem = new ListItem();
				objItem.Value = objXmlTradition["name"].InnerText;
				if (objXmlTradition["translate"] != null)
					objItem.Name = objXmlTradition["translate"].InnerText;
				else
					objItem.Name = objXmlTradition["name"].InnerText;
				lstTraditions.Add(objItem);
			}
			SortListItem objSort = new SortListItem();
			lstTraditions.Sort(objSort.Compare);
			cboTradition.ValueMember = "Value";
			cboTradition.DisplayMember = "Name";
			cboTradition.DataSource = lstTraditions;

			// Populate the Technomancer Streams list.
			objXmlDocument = XmlManager.Instance.Load("streams.xml");
			List<ListItem> lstStreams = new List<ListItem>();
			lstStreams.Add(objBlank);
			foreach (XmlNode objXmlTradition in objXmlDocument.SelectNodes("/chummer/traditions/tradition[" + _objOptions.BookXPath() + "]"))
			{
				ListItem objItem = new ListItem();
				objItem.Value = objXmlTradition["name"].InnerText;
				if (objXmlTradition["translate"] != null)
					objItem.Name = objXmlTradition["translate"].InnerText;
				else
					objItem.Name = objXmlTradition["name"].InnerText;
				lstStreams.Add(objItem);
			}
			lstStreams.Sort(objSort.Compare);
			cboStream.ValueMember = "Value";
			cboStream.DisplayMember = "Name";
			cboStream.DataSource = lstStreams;

			// Load the Metatype information before going anywhere else. Doing this later causes the Attributes to get messed up because of calls
			// to UpdateCharacterInformation();
			MetatypeSelected();

			// If the character is a Mystic Adept, set the values for the Mystic Adept NUD.
			int intCharacterMAG = _objCharacter.MAG.TotalValue;
			if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
			{
				nudMysticAdeptMAGMagician.Maximum = _objCharacter.MAG.TotalValue;
				nudMysticAdeptMAGMagician.Value = _objCharacter.MAGMagician;
				lblMysticAdeptMAGAdept.Text = _objCharacter.MAGAdept.ToString();
				intCharacterMAG = _objCharacter.MAGMagician;

				lblMysticAdeptAssignment.Visible = true;
				lblMysticAdeptAssignmentAdept.Visible = true;
				lblMysticAdeptAssignmentMagician.Visible = true;
				lblMysticAdeptMAGAdept.Visible = true;
				nudMysticAdeptMAGMagician.Visible = true;
			}

			// Load the Skills information.
			objXmlDocument = XmlManager.Instance.Load("skills.xml");

			List<ListItem> lstComplexFormSkills = new List<ListItem>();

			// Populate the Skills Controls.
			XmlNodeList objXmlNodeList = objXmlDocument.SelectNodes("/chummer/skills/skill[" + _objCharacter.Options.BookXPath() + "]");
			// Counter to keep track of the number of Controls that have been added to the Panel so we can determine their vertical positioning.
			int i = -1;
			foreach (Skill objSkill in _objCharacter.Skills)
			{
				if (!objSkill.KnowledgeSkill && !objSkill.ExoticSkill)
				{
					i++;
					SkillControl objSkillControl = new SkillControl();
					objSkillControl.SkillObject = objSkill;

					// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
					objSkillControl.RatingChanged += objActiveSkill_RatingChanged;
					objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
					objSkillControl.SpecializationLeave += objSkill_SpecializationLeave;
					objSkillControl.SkillKarmaClicked += objSkill_KarmaClicked;
					objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

					objSkillControl.SkillName = objSkill.Name;
					objSkillControl.SkillCategory = objSkill.SkillCategory;
					objSkillControl.SkillGroup = objSkill.SkillGroup;
					objSkillControl.SkillRatingMaximum = objSkill.RatingMaximum;
					objSkillControl.SkillRating = objSkill.Rating;
					objSkillControl.SkillSpec = objSkill.Specialization;

					XmlNode objXmlSkill = objXmlDocument.SelectSingleNode("/chummer/skills/skill[name = \"" + objSkill.Name + "\"]");
					// Populate the Skill's Specializations (if any).
					foreach (XmlNode objXmlSpecialization in objXmlSkill.SelectNodes("specs/spec"))
					{
						if (objXmlSpecialization.Attributes["translate"] != null)
							objSkillControl.AddSpec(objXmlSpecialization.Attributes["translate"].InnerText);
						else
							objSkillControl.AddSpec(objXmlSpecialization.InnerText);
					}

					// Set the control's vertical position and add it to the Skills Panel.
					objSkillControl.Width = 510;
					objSkillControl.AutoScroll = false;
					panActiveSkills.Controls.Add(objSkillControl);

					// Determine if this Skill should be added to the list of Skills for Comlex Form Tests.
					bool blnAddSkill = true;
					if (objSkill.Attribute == "MAG" || objSkill.SkillCategory == "Magical Active")
						blnAddSkill = false;

					if (blnAddSkill)
					{
						ListItem objItem = new ListItem();
						objItem.Value = objSkill.Name;
						objItem.Name = objSkill.DisplayName;
						lstComplexFormSkills.Add(objItem);
					}
				}
			}

			if (_objOptions.AlternateMatrixAttribute)
			{
				List<ListItem> lstComplexFormAttributes = new List<ListItem>();

				ListItem objBOD = new ListItem();
				objBOD.Value = "BOD";
				objBOD.Name = LanguageManager.Instance.GetString("String_AttributeBODShort");
				lstComplexFormAttributes.Add(objBOD);

				ListItem objAGI = new ListItem();
				objAGI.Value = "AGI";
				objAGI.Name = LanguageManager.Instance.GetString("String_AttributeAGIShort");
				lstComplexFormAttributes.Add(objAGI);

				ListItem objREA = new ListItem();
				objREA.Value = "REA";
				objREA.Name = LanguageManager.Instance.GetString("String_AttributeREAShort");
				lstComplexFormAttributes.Add(objREA);

				ListItem objSTR = new ListItem();
				objSTR.Value = "STR";
				objSTR.Name = LanguageManager.Instance.GetString("String_AttributeSTRShort");
				lstComplexFormAttributes.Add(objSTR);

				ListItem objCHA = new ListItem();
				objCHA.Value = "CHA";
				objCHA.Name = LanguageManager.Instance.GetString("String_AttributeCHAShort");
				lstComplexFormAttributes.Add(objCHA);

				ListItem objINT = new ListItem();
				objINT.Value = "INT";
				objINT.Name = LanguageManager.Instance.GetString("String_AttributeINTShort");
				lstComplexFormAttributes.Add(objINT);

				ListItem objLOG = new ListItem();
				objLOG.Value = "LOG";
				objLOG.Name = LanguageManager.Instance.GetString("String_AttributeLOGShort");
				lstComplexFormAttributes.Add(objLOG);

				ListItem objWIL = new ListItem();
				objWIL.Value = "WIL";
				objWIL.Name = LanguageManager.Instance.GetString("String_AttributeWILShort");
				lstComplexFormAttributes.Add(objWIL);

				ListItem objRES = new ListItem();
				objRES.Value = "RES";
				objRES.Name = LanguageManager.Instance.GetString("String_AttributeRESShort");
				lstComplexFormAttributes.Add(objRES);

				cboComplexFormAttribute.ValueMember = "Value";
				cboComplexFormAttribute.DisplayMember = "Name";
				cboComplexFormAttribute.DataSource = lstComplexFormAttributes;
				cboComplexFormAttribute.SelectedValue = "LOG";
			}

			// Populate the list of Complex Form Skills.
			cboComplexFormSkill.ValueMember = "Value";
			cboComplexFormSkill.DisplayMember = "Name";
			cboComplexFormSkill.DataSource = lstComplexFormSkills;

			// Exotic Skills.
			foreach (Skill objSkill in _objCharacter.Skills)
			{
				if (objSkill.ExoticSkill)
				{
					i++;
					SkillControl objSkillControl = new SkillControl();
					objSkillControl.SkillObject = objSkill;

					// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
					objSkillControl.RatingChanged += objActiveSkill_RatingChanged;
					objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
					objSkillControl.SkillKarmaClicked += objSkill_KarmaClicked;
					objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

					objSkillControl.SkillName = objSkill.Name;
					objSkillControl.SkillCategory = objSkill.SkillCategory;
					objSkillControl.SkillGroup = objSkill.SkillGroup;
					objSkillControl.SkillRatingMaximum = objSkill.RatingMaximum;
					objSkillControl.SkillRating = objSkill.Rating;
					objSkillControl.SkillSpec = objSkill.Specialization;

					XmlNode objXmlSkill = objXmlDocument.SelectSingleNode("/chummer/skills/skill[name = \"" + objSkill.Name + "\"]");
					// Populate the Skill's Specializations (if any).
					foreach (XmlNode objXmlSpecialization in objXmlSkill.SelectNodes("specs/spec"))
					{
						if (objXmlSpecialization.Attributes["translate"] != null)
							objSkillControl.AddSpec(objXmlSpecialization.Attributes["translate"].InnerText);
						else
							objSkillControl.AddSpec(objXmlSpecialization.InnerText);
					}

					// Look through the Weapons file and grab the names of items that are part of the appropriate Exotic Category or use the matching Exoctic Skill.
					XmlDocument objXmlWeaponDocument = XmlManager.Instance.Load("weapons.xml");
					XmlNodeList objXmlWeaponList = objXmlWeaponDocument.SelectNodes("/chummer/weapons/weapon[category = \"" + objSkill.Name + "s\" or useskill = \"" + objSkill.Name + "\"]");
					foreach (XmlNode objXmlWeapon in objXmlWeaponList)
					{
						if (objXmlWeapon["translate"] != null)
							objSkillControl.AddSpec(objXmlWeapon["translate"].InnerText);
						else
							objSkillControl.AddSpec(objXmlWeapon["name"].InnerText);
					}

					// Set the control's vertical position and add it to the Skills Panel.
					objSkillControl.Top = i * objSkillControl.Height;
					objSkillControl.Width = 510;
					objSkillControl.AutoScroll = false;
					panActiveSkills.Controls.Add(objSkillControl);
				}
			}

			// Populate the Skill Groups list.
			i = -1;
			foreach (SkillGroup objGroup in _objCharacter.SkillGroups)
			{
				i++;
				SkillGroupControl objGroupControl = new SkillGroupControl(_objCharacter.Options, true);
				objGroupControl.SkillGroupObject = objGroup;

				// Attach an EventHandler for the GetRatingChanged Event.
				objGroupControl.GroupRatingChanged += objGroup_RatingChanged;
				objGroupControl.GroupKarmaClicked += objGroup_KarmaClicked;

				// Populate the control, set its vertical position and add it to the Skill Groups Panel. A Skill Group cannot start with a Rating higher than 4.
				objGroupControl.GroupName = objGroup.Name;
				if (objGroup.Rating > objGroup.RatingMaximum)
					objGroup.RatingMaximum = objGroup.Rating;
				objGroupControl.GroupRatingMaximum = objGroup.RatingMaximum;
				objGroupControl.GroupRating = objGroup.Rating;
				objGroupControl.Top = i * objGroupControl.Height;
				objGroupControl.Width = 250;

				if (_objCharacter.Uneducated)
				{
					objGroupControl.IsEnabled = !objGroup.HasTechnicalSkills;
				}

				if (_objCharacter.Uncouth)
				{
					objGroupControl.IsEnabled = !objGroup.HasSocialSkills;
				}

				if (_objCharacter.Infirm)
				{
					objGroupControl.IsEnabled = !objGroup.HasPhysicalSkills;
				}

				panSkillGroups.Controls.Add(objGroupControl);
			}

			// Populate Knowledge Skills.
			i = -1;
			foreach (Skill objSkill in _objCharacter.Skills)
			{
				if (objSkill.KnowledgeSkill)
				{
					i++;
					SkillControl objSkillControl = new SkillControl();
					objSkillControl.SkillObject = objSkill;

					// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
					objSkillControl.RatingChanged += objKnowledgeSkill_RatingChanged;
					objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
					objSkillControl.SpecializationLeave += objSkill_SpecializationLeave;
					objSkillControl.DeleteSkill += objKnowledgeSkill_DeleteSkill;
					objSkillControl.SkillKarmaClicked += objKnowledgeSkill_KarmaClicked;
					objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

					objSkillControl.KnowledgeSkill = true;
					objSkillControl.SkillCategory = objSkill.SkillCategory;
					objSkillControl.AllowDelete = true;
					objSkillControl.SkillRatingMaximum = objSkill.RatingMaximum;
					objSkillControl.SkillRating = objSkill.Rating;
					objSkillControl.SkillName = objSkill.Name;
					objSkillControl.SkillSpec = objSkill.Specialization;
					objSkillControl.Top = i * objSkillControl.Height;
					objSkillControl.AutoScroll = false;
					panKnowledgeSkills.Controls.Add(objSkillControl);
				}
			}

			// Populate Contacts and Enemies.
			int intContact = -1;
			int intEnemy = -1;
			foreach (Contact objContact in _objCharacter.Contacts)
			{
				if (objContact.EntityType == ContactType.Contact)
				{
					intContact++;
					ContactControl objContactControl = new ContactControl();
					// Attach an EventHandler for the ConnectionRatingChanged, LoyaltyRatingChanged, DeleteContact, and FileNameChanged Events.
					objContactControl.ConnectionRatingChanged += objContact_ConnectionRatingChanged;
					objContactControl.ConnectionGroupRatingChanged += objContact_ConnectionGroupRatingChanged;
					objContactControl.LoyaltyRatingChanged += objContact_LoyaltyRatingChanged;
					objContactControl.DeleteContact += objContact_DeleteContact;
					objContactControl.FileNameChanged += objContact_FileNameChanged;
					
					objContactControl.ContactObject = objContact;
					objContactControl.ContactName = objContact.Name;
					objContactControl.ConnectionRating = objContact.Connection;
					objContactControl.LoyaltyRating = objContact.Loyalty;
					objContactControl.EntityType = objContact.EntityType;
					objContactControl.BackColor = objContact.Colour;

					objContactControl.Top = intContact * objContactControl.Height;
					panContacts.Controls.Add(objContactControl);
				}
				if (objContact.EntityType == ContactType.Enemy)
				{
					intEnemy++;
					ContactControl objContactControl = new ContactControl();
					// Attach an EventHandler for the ConnectioNRatingChanged, LoyaltyRatingChanged, DeleteContact, and FileNameChanged Events.
					objContactControl.ConnectionRatingChanged += objEnemy_ConnectionRatingChanged;
					objContactControl.ConnectionGroupRatingChanged += objEnemy_ConnectionGroupRatingChanged;
					objContactControl.LoyaltyRatingChanged += objEnemy_LoyaltyRatingChanged;
					objContactControl.DeleteContact += objEnemy_DeleteContact;
					objContactControl.FileNameChanged += objEnemy_FileNameChanged;

					objContactControl.ContactObject = objContact;
					objContactControl.ContactName = objContact.Name;
					objContactControl.ConnectionRating = objContact.Connection;
					objContactControl.LoyaltyRating = objContact.Loyalty;
					objContactControl.EntityType = objContact.EntityType;
					objContactControl.BackColor = objContact.Colour;

					objContactControl.Top = intEnemy * objContactControl.Height;
					panEnemies.Controls.Add(objContactControl);
				}
				if (objContact.EntityType == ContactType.Pet)
				{
					PetControl objContactControl = new PetControl();
					// Attach an EventHandler for the DeleteContact and FileNameChanged Events.
					objContactControl.DeleteContact += objPet_DeleteContact;
					objContactControl.FileNameChanged += objPet_FileNameChanged;

					objContactControl.ContactObject = objContact;
					objContactControl.ContactName = objContact.Name;
					objContactControl.BackColor = objContact.Colour;

					panPets.Controls.Add(objContactControl);
				}
			}

			// Populate Armor.
			// Start by populating Locations.
			foreach (string strLocation in _objCharacter.ArmorBundles)
			{
				TreeNode objLocation = new TreeNode();
				objLocation.Tag = strLocation;
				objLocation.Text = strLocation;
				objLocation.ContextMenuStrip = cmsArmorLocation;
				treArmor.Nodes.Add(objLocation);
			}
			foreach (Armor objArmor in _objCharacter.Armor)
			{
				_objFunctions.CreateArmorTreeNode(objArmor, treArmor, cmsArmor, cmsArmorMod, cmsArmorGear);
			}

			// Populate Weapons.
			// Start by populating Locations.
			foreach (string strLocation in _objCharacter.WeaponLocations)
			{
				TreeNode objLocation = new TreeNode();
				objLocation.Tag = strLocation;
				objLocation.Text = strLocation;
				objLocation.ContextMenuStrip = cmsWeaponLocation;
				treWeapons.Nodes.Add(objLocation);
			}
			foreach (Weapon objWeapon in _objCharacter.Weapons)
			{
				_objFunctions.CreateWeaponTreeNode(objWeapon, treWeapons.Nodes[0], cmsWeapon, cmsWeaponMod, cmsWeaponAccessory, cmsWeaponAccessoryGear);
			}

			PopulateCyberware();

			// Populate Spell list.
			foreach (Spell objSpell in _objCharacter.Spells)
			{
				TreeNode objNode = new TreeNode();
				objNode.Text = objSpell.DisplayName;
				objNode.Tag = objSpell.InternalId;
				objNode.ContextMenuStrip = cmsSpell;
				if (objSpell.Notes != string.Empty)
					objNode.ForeColor = Color.SaddleBrown;
				objNode.ToolTipText = objSpell.Notes;

				switch (objSpell.Category)
				{
					case "Combat":
						treSpells.Nodes[0].Nodes.Add(objNode);
						treSpells.Nodes[0].Expand();
						break;
					case "Detection":
						treSpells.Nodes[1].Nodes.Add(objNode);
						treSpells.Nodes[1].Expand();
						break;
					case "Health":
						treSpells.Nodes[2].Nodes.Add(objNode);
						treSpells.Nodes[2].Expand();
						break;
					case "Illusion":
						treSpells.Nodes[3].Nodes.Add(objNode);
						treSpells.Nodes[3].Expand();
						break;
					case "Manipulation":
						treSpells.Nodes[4].Nodes.Add(objNode);
						treSpells.Nodes[4].Expand();
						break;
					case "Geomancy Ritual":
						treSpells.Nodes[5].Nodes.Add(objNode);
						treSpells.Nodes[5].Expand();
						break;
				}
			}

			// Populate Adept Powers.
			i = -1;
			foreach (Power objPower in _objCharacter.Powers)
			{
				i++;
				PowerControl objPowerControl = new PowerControl();
				objPowerControl.PowerObject = objPower;

				// Attach an EventHandler for the PowerRatingChanged Event.
				objPowerControl.PowerRatingChanged += objPower_PowerRatingChanged;
				objPowerControl.DeletePower += objPower_DeletePower;

				objPowerControl.PowerName = objPower.Name;
				objPowerControl.Extra = objPower.Extra;
				objPowerControl.PointsPerLevel = objPower.PointsPerLevel;
				objPowerControl.LevelEnabled = objPower.LevelsEnabled;
				if (objPower.MaxLevels > 0)
					objPowerControl.MaxLevels = objPower.MaxLevels;
				objPowerControl.RefreshMaximum(_objCharacter.MAG.TotalValue);
				if (objPower.Rating < 1)
					objPower.Rating = 1;
				objPowerControl.PowerLevel = Convert.ToInt32(objPower.Rating);
				if (objPower.DiscountedAdeptWay)
					objPowerControl.DiscountedByAdeptWay = true;
				if (objPower.DiscountedGeas)
					objPowerControl.DiscountedByGeas = true;

				objPowerControl.Top = i * objPowerControl.Height;
				panPowers.Controls.Add(objPowerControl);
			}

			// Populate Magician Spirits.
			i = -1;
			foreach (Spirit objSpirit in _objCharacter.Spirits)
			{
				if (objSpirit.EntityType == SpiritType.Spirit)
				{
					i++;
					SpiritControl objSpiritControl = new SpiritControl(true);
					objSpiritControl.SpiritObject = objSpirit;

					// Attach an EventHandler for the ServicesOwedChanged Event.
					objSpiritControl.ServicesOwedChanged += objSpirit_ServicesOwedChanged;
					objSpiritControl.ForceChanged += objSpirit_ForceChanged;
					objSpiritControl.BoundChanged += objSpirit_BoundChanged;
					objSpiritControl.DeleteSpirit += objSpirit_DeleteSpirit;
					objSpiritControl.FileNameChanged += objSpirit_FileNameChanged;

					objSpiritControl.SpiritName = objSpirit.Name;
					objSpiritControl.ServicesOwed = objSpirit.ServicesOwed;
					if (_objOptions.SpiritForceBasedOnTotalMAG)
						objSpiritControl.ForceMaximum = _objCharacter.MAG.TotalValue * 2;
					else
						objSpiritControl.ForceMaximum = intCharacterMAG * 2;
					objSpiritControl.CritterName = objSpirit.CritterName;
					objSpiritControl.Force = objSpirit.Force;
					objSpiritControl.Bound = objSpirit.Bound;
					objSpiritControl.RebuildSpiritList(_objCharacter.MagicTradition);

					objSpiritControl.Top = i * objSpiritControl.Height;
					panSpirits.Controls.Add(objSpiritControl);
				}
			}

			// Populate Technomancer Sprites.
			i = -1;
			foreach (Spirit objSpirit in _objCharacter.Spirits)
			{
				if (objSpirit.EntityType == SpiritType.Sprite)
				{
					i++;
					SpiritControl objSpiritControl = new SpiritControl(true);
					objSpiritControl.SpiritObject = objSpirit;
					objSpiritControl.EntityType = SpiritType.Sprite;

					// Attach an EventHandler for the ServicesOwedChanged Event.
					objSpiritControl.ServicesOwedChanged += objSprite_ServicesOwedChanged;
					objSpiritControl.ForceChanged += objSprite_ForceChanged;
					objSpiritControl.BoundChanged += objSprite_BoundChanged;
					objSpiritControl.DeleteSpirit += objSprite_DeleteSpirit;
					objSpiritControl.FileNameChanged += objSprite_FileNameChanged;

					objSpiritControl.SpiritName = objSpirit.Name;
					objSpiritControl.ServicesOwed = objSpirit.ServicesOwed;
					objSpiritControl.ForceMaximum = _objCharacter.RES.TotalValue * 2;
					objSpiritControl.CritterName = objSpirit.CritterName;
					objSpiritControl.Force = objSpirit.Force;
					objSpiritControl.Bound = objSpirit.Bound;
					objSpiritControl.RebuildSpiritList(_objCharacter.TechnomancerStream);

					objSpiritControl.Top = i * objSpiritControl.Height;
					panSprites.Controls.Add(objSpiritControl);
				}
			}

			// Populate Technomancer Complex Forms/Programs.
            foreach (ComplexForm objProgram in _objCharacter.ComplexForms)
			{
				TreeNode objNode = new TreeNode();
				objNode.Text = objProgram.DisplayName;
				objNode.Tag = objProgram.InternalId;
				if (objProgram.Notes != string.Empty)
					objNode.ForeColor = Color.SaddleBrown;
				objNode.ToolTipText = objProgram.Notes;
				treComplexForms.Nodes[0].Nodes.Add(objNode);
				treComplexForms.Nodes[0].Expand();
			}

			// Populate Martial Arts.
			foreach (MartialArt objMartialArt in _objCharacter.MartialArts)
			{
				TreeNode objMartialArtNode = new TreeNode();
				objMartialArtNode.Text = objMartialArt.DisplayName;
				objMartialArtNode.Tag = objMartialArt.Name;
				objMartialArtNode.ContextMenuStrip = cmsMartialArts;
				if (objMartialArt.Notes != string.Empty)
					objMartialArtNode.ForeColor = Color.SaddleBrown;
				objMartialArtNode.ToolTipText = objMartialArt.Notes;

				foreach (MartialArtAdvantage objAdvantage in objMartialArt.Advantages)
				{
					TreeNode objAdvantageNode = new TreeNode();
					objAdvantageNode.Text = objAdvantage.DisplayName;
					objAdvantageNode.Tag = objAdvantage.InternalId;
					objMartialArtNode.Nodes.Add(objAdvantageNode);
					objMartialArtNode.Expand();
				}

				treMartialArts.Nodes[0].Nodes.Add(objMartialArtNode);
				treMartialArts.Nodes[0].Expand();
			}

			// Populate Martial Art Maneuvers.
			foreach (MartialArtManeuver objManeuver in _objCharacter.MartialArtManeuvers)
			{
				TreeNode objManeuverNode = new TreeNode();
				objManeuverNode.Text = objManeuver.DisplayName;
				objManeuverNode.Tag = objManeuver.InternalId;
				objManeuverNode.ContextMenuStrip = cmsMartialArtManeuver;
				if (objManeuver.Notes != string.Empty)
					objManeuverNode.ForeColor = Color.SaddleBrown;
				objManeuverNode.ToolTipText = objManeuver.Notes;

				treMartialArts.Nodes[1].Nodes.Add(objManeuverNode);
				treMartialArts.Nodes[1].Expand();
			}

			// Populate Lifestyles.
			foreach (Lifestyle objLifestyle in _objCharacter.Lifestyles)
			{
				TreeNode objLifestyleNode = new TreeNode();
				objLifestyleNode.Text = objLifestyle.DisplayName;
				objLifestyleNode.Tag = objLifestyle.InternalId;
				if (objLifestyle.BaseLifestyle != "")
					objLifestyleNode.ContextMenuStrip = cmsAdvancedLifestyle;
				else
					objLifestyleNode.ContextMenuStrip = cmsLifestyleNotes;
				if (objLifestyle.Notes != string.Empty)
					objLifestyleNode.ForeColor = Color.SaddleBrown;
				objLifestyleNode.ToolTipText = objLifestyle.Notes;
				treLifestyles.Nodes[0].Nodes.Add(objLifestyleNode);
			}
			treLifestyles.Nodes[0].Expand();

			PopulateGearList();

			// Populate Foci.
			_objController.PopulateFocusList(treFoci);

			// Populate Vehicles.
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				_objFunctions.CreateVehicleTreeNode(objVehicle, treVehicles, cmsVehicle, cmsVehicleLocation, cmsVehicleWeapon, cmsVehicleWeaponMod, cmsVehicleWeaponAccessory, cmsVehicleWeaponAccessoryGear, cmsVehicleGear);
			}

			// Populate Initiation/Submersion information.
			if (_objCharacter.InitiateGrade > 0 || _objCharacter.SubmersionGrade > 0)
			{
				foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
				{
					TreeNode objNode = new TreeNode();
					objNode.Text = objMetamagic.DisplayName;
					objNode.Tag = objMetamagic.InternalId;
					objNode.ContextMenuStrip = cmsMetamagic;
					if (objMetamagic.Notes != string.Empty)
						objNode.ForeColor = Color.SaddleBrown;
					objNode.ToolTipText = objMetamagic.Notes;
					treMetamagic.Nodes.Add(objNode);
				}

				if (_objCharacter.InitiateGrade > 0)
					lblInitiateGrade.Text = _objCharacter.InitiateGrade.ToString();
				else
					lblInitiateGrade.Text = _objCharacter.SubmersionGrade.ToString();
			}

			if (_objCharacter.MagicTradition != "")
			{
				objXmlDocument = XmlManager.Instance.Load("traditions.xml");
				XmlNode objXmlTradition = objXmlDocument.SelectSingleNode("/chummer/traditions/tradition[name = \"" + _objCharacter.MagicTradition + "\"]");
				lblDrainAttributes.Text = objXmlTradition["drain"].InnerText;

				// Update the Drain Attribute Value.
				try
				{
					XPathNavigator nav = objXmlDocument.CreateNavigator();
					string strDrain = lblDrainAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), _objCharacter.BOD.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), _objCharacter.AGI.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), _objCharacter.REA.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"),_objCharacter.STR.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), _objCharacter.CHA.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), _objCharacter.INT.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), _objCharacter.LOG.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), _objCharacter.WIL.Value.ToString());
					strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeMAGShort"), _objCharacter.MAG.TotalValue.ToString());
					XPathExpression xprDrain = nav.Compile(strDrain);
					int intDrain = Convert.ToInt32(nav.Evaluate(xprDrain).ToString());
					intDrain += _objImprovementManager.ValueOf(Improvement.ImprovementType.DrainResistance);
					lblDrainAttributesValue.Text = intDrain.ToString();
				}
				catch
				{
				}
			}

			if (_objCharacter.TechnomancerStream != "")
			{
				objXmlDocument = XmlManager.Instance.Load("streams.xml");
				XmlNode objXmlTradition = objXmlDocument.SelectSingleNode("/chummer/traditions/tradition[name = \"" + _objCharacter.TechnomancerStream + "\"]");
				lblFadingAttributes.Text = objXmlTradition["drain"].InnerText;

				// Update the Fading Attribute Value.
				try
				{
					XPathNavigator nav = objXmlDocument.CreateNavigator();
					string strFading = lblFadingAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), _objCharacter.BOD.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), _objCharacter.AGI.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), _objCharacter.REA.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), _objCharacter.STR.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), _objCharacter.CHA.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), _objCharacter.INT.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), _objCharacter.LOG.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), _objCharacter.WIL.Value.ToString());
					strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeRESShort"), _objCharacter.RES.TotalValue.ToString());
					XPathExpression xprFading = nav.Compile(strFading);
					int intFading = Convert.ToInt32(nav.Evaluate(xprFading).ToString());
					intFading += _objImprovementManager.ValueOf(Improvement.ImprovementType.FadingResistance);
					lblFadingAttributesValue.Text = intFading.ToString();
				}
				catch
				{
				}
			}

			// Populate Critter Powers.
			foreach (CritterPower objPower in _objCharacter.CritterPowers)
			{
				TreeNode objNode = new TreeNode();
				objNode.Text = objPower.DisplayName;
				objNode.Tag = objPower.InternalId;
				objNode.ContextMenuStrip = cmsCritterPowers;
				if (objPower.Notes != string.Empty)
					objNode.ForeColor = Color.SaddleBrown;
				objNode.ToolTipText = objPower.Notes;

				if (objPower.Category != "Weakness")
				{
					treCritterPowers.Nodes[0].Nodes.Add(objNode);
					treCritterPowers.Nodes[0].Expand();
				}
				else
				{
					treCritterPowers.Nodes[1].Nodes.Add(objNode);
					treCritterPowers.Nodes[1].Expand();
				}
			}

			_blnLoading = false;

			// Select the Magician's Tradition.
			if (_objCharacter.MagicTradition != "")
				cboTradition.SelectedValue = _objCharacter.MagicTradition;

			// Select the Technomancer's Stream.
			if (_objCharacter.TechnomancerStream != "")
				cboStream.SelectedValue = _objCharacter.TechnomancerStream;

			// Clear the Dirty flag which gets set when creating a new Character.
			_blnIsDirty = false;
			UpdateWindowTitle();
			if (_objCharacter.AdeptEnabled)
				CalculatePowerPoints();

			treGear.ItemDrag += treGear_ItemDrag;
			treGear.DragEnter += treGear_DragEnter;
			treGear.DragDrop += treGear_DragDrop;

			treLifestyles.ItemDrag += treLifestyles_ItemDrag;
			treLifestyles.DragEnter += treLifestyles_DragEnter;
			treLifestyles.DragDrop += treLifestyles_DragDrop;

			treArmor.ItemDrag += treArmor_ItemDrag;
			treArmor.DragEnter += treArmor_DragEnter;
			treArmor.DragDrop += treArmor_DragDrop;

			treWeapons.ItemDrag += treWeapons_ItemDrag;
			treWeapons.DragEnter += treWeapons_DragEnter;
			treWeapons.DragDrop += treWeapons_DragDrop;

			treVehicles.ItemDrag += treVehicles_ItemDrag;
			treVehicles.DragEnter += treVehicles_DragEnter;
			treVehicles.DragDrop += treVehicles_DragDrop;

			treImprovements.ItemDrag += treImprovements_ItemDrag;
			treImprovements.DragEnter += treImprovements_DragEnter;
			treImprovements.DragDrop += treImprovements_DragDrop;

			// Merge the ToolStrips.
			ToolStripManager.RevertMerge("toolStrip");
			ToolStripManager.Merge(toolStrip, "toolStrip");

			// If this is a Sprite, re-label the Mental Attribute Labels.
			if (_objCharacter.Metatype.EndsWith("Sprite"))
			{
				lblBODLabel.Enabled = false;
				lblAGILabel.Enabled = false;
				lblREALabel.Enabled = false;
				lblSTRLabel.Enabled = false;
				lblCHALabel.Text = LanguageManager.Instance.GetString("String_AttributePilot");
				lblINTLabel.Text = LanguageManager.Instance.GetString("String_AttributeResponse");
				lblLOGLabel.Text = LanguageManager.Instance.GetString("String_AttributeFirewall");
				lblWILLabel.Enabled = false;
			}
			else if (_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients")
			{
				lblRatingLabel.Visible = true;
				lblRating.Visible = true;
				lblSystemLabel.Visible = true;
				lblSystem.Visible = true;
				lblFirewallLabel.Visible = true;
				lblFirewall.Visible = true;
				lblResponseLabel.Visible = true;
				nudResponse.Visible = true;
				nudResponse.Enabled = true;
				nudResponse.Value = _objCharacter.Response;
				lblSignalLabel.Visible = true;
				nudSignal.Visible = true;
				nudSignal.Enabled = true;
				nudSignal.Value = _objCharacter.Signal;
			}

			mnuSpecialConvertToFreeSprite.Visible = _objCharacter.IsSprite;

			// Run through all of the Skills and Enable/Disable them as needed.
			foreach (SkillControl objSkillControl in panActiveSkills.Controls)
			{
				if (objSkillControl.Attribute == "MAG")
					objSkillControl.Enabled = _objCharacter.MAGEnabled;
				if (objSkillControl.Attribute == "RES")
					objSkillControl.Enabled = _objCharacter.RESEnabled;
			}
			// Run through all of the Skill Groups and Disable them if all of their Skills are currently inaccessible.
			foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
			{
				bool blnEnabled = false;
				foreach (Skill objSkill in _objCharacter.Skills)
				{
					if (objSkill.SkillGroup == objSkillGroupControl.GroupName)
					{
						if (objSkill.Attribute == "MAG" || objSkill.Attribute == "RES")
						{
							if (objSkill.Attribute == "MAG" && _objCharacter.MAGEnabled)
								blnEnabled = true;
							if (objSkill.Attribute == "RES" && _objCharacter.RESEnabled)
								blnEnabled = true;
						}
						else
							blnEnabled = true;
					}
				}
				objSkillGroupControl.IsEnabled = blnEnabled;
				if (!blnEnabled)
					objSkillGroupControl.GroupRating = 0;
			}

			// Populate the Skill Filter DropDown.
			List<ListItem> lstFilter = new List<ListItem>();
			ListItem itmAll = new ListItem();
			itmAll.Value = "0";
			itmAll.Name = LanguageManager.Instance.GetString("String_SkillFilterAll");
			ListItem itmRatingAboveZero = new ListItem();
			itmRatingAboveZero.Value = "1";
			itmRatingAboveZero.Name = LanguageManager.Instance.GetString("String_SkillFilterRatingAboveZero");
			ListItem itmTotalRatingAboveZero = new ListItem();
			itmTotalRatingAboveZero.Value = "2";
			itmTotalRatingAboveZero.Name = LanguageManager.Instance.GetString("String_SkillFilterTotalRatingAboveZero");
			ListItem itmRatingEqualZero = new ListItem();
			itmRatingEqualZero.Value = "3";
			itmRatingEqualZero.Name = LanguageManager.Instance.GetString("String_SkillFilterRatingZero");
			lstFilter.Add(itmAll);
			lstFilter.Add(itmRatingAboveZero);
			lstFilter.Add(itmTotalRatingAboveZero);
			lstFilter.Add(itmRatingEqualZero);

			objXmlDocument = XmlManager.Instance.Load("skills.xml");
			objXmlNodeList = objXmlDocument.SelectNodes("/chummer/categories/category[@type = \"active\"]");
			foreach (XmlNode objNode in objXmlNodeList)
			{
				ListItem objItem = new ListItem();
				objItem.Value = objNode.InnerText;
				if (objNode.Attributes["translate"] != null)
					objItem.Name = LanguageManager.Instance.GetString("Label_Category") + " " + objNode.Attributes["translate"].InnerText;
				else
					objItem.Name = LanguageManager.Instance.GetString("Label_Category") + " " + objNode.InnerText;
				lstFilter.Add(objItem);
			}

			// Add items for Attributes.
			ListItem itmBOD = new ListItem();
			itmBOD.Value = "BOD";
			itmBOD.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeBODShort");
			ListItem itmAGI = new ListItem();
			itmAGI.Value = "AGI";
			itmAGI.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeAGIShort");
			ListItem itmREA = new ListItem();
			itmREA.Value = "REA";
			itmREA.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeREAShort");
			ListItem itmSTR = new ListItem();
			itmSTR.Value = "STR";
			itmSTR.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeSTRShort");
			ListItem itmCHA = new ListItem();
			itmCHA.Value = "CHA";
			itmCHA.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeCHAShort");
			ListItem itmINT = new ListItem();
			itmINT.Value = "INT";
			itmINT.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeINTShort");
			ListItem itmLOG = new ListItem();
			itmLOG.Value = "LOG";
			itmLOG.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeLOGShort");
			ListItem itmWIL = new ListItem();
			itmWIL.Value = "WIL";
			itmWIL.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeWILShort");
			ListItem itmMAG = new ListItem();
			itmMAG.Value = "MAG";
			itmMAG.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeMAGShort");
			ListItem itmRES = new ListItem();
			itmRES.Value = "RES";
			itmRES.Name = LanguageManager.Instance.GetString("String_ExpenseAttribute") + ": " + LanguageManager.Instance.GetString("String_AttributeRESShort");
			lstFilter.Add(itmBOD);
			lstFilter.Add(itmAGI);
			lstFilter.Add(itmREA);
			lstFilter.Add(itmSTR);
			lstFilter.Add(itmCHA);
			lstFilter.Add(itmINT);
			lstFilter.Add(itmLOG);
			lstFilter.Add(itmWIL);
			lstFilter.Add(itmMAG);
			lstFilter.Add(itmRES);

			// Add Skill Groups to the filter.
			objXmlNodeList = objXmlDocument.SelectNodes("/chummer/categories/category[@type = \"active\"]");
			foreach (SkillGroup objGroup in _objCharacter.SkillGroups)
			{
				ListItem itmGroup = new ListItem();
				itmGroup.Value = "GROUP:" + objGroup.Name;
				itmGroup.Name = LanguageManager.Instance.GetString("String_ExpenseSkillGroup") + ": " + objGroup.DisplayName;
				lstFilter.Add(itmGroup);
			}

			cboSkillFilter.DataSource = lstFilter;
			cboSkillFilter.ValueMember = "Value";
			cboSkillFilter.DisplayMember = "Name";
			cboSkillFilter.SelectedIndex = 0;
			cboSkillFilter_SelectedIndexChanged(null, null);

			if (_objCharacter.MetatypeCategory == "Cyberzombie")
				mnuSpecialCyberzombie.Visible = false;

			// Determine if the Critter should have access to the Possession menu item.
			bool blnAllowPossession = false;
			foreach (CritterPower objCritterPower in _objCharacter.CritterPowers)
			{
				if (objCritterPower.Name == "Inhabitation" || objCritterPower.Name == "Possession")
				{
					blnAllowPossession = true;
					break;
				}
			}
			mnuSpecialPossess.Visible = blnAllowPossession;

			// Set the visibility of the Armor Degradation buttons.
			cmdArmorDecrease.Visible = _objOptions.ArmorDegradation;
			cmdArmorIncrease.Visible = _objOptions.ArmorDegradation;

			_objFunctions.SortTree(treCyberware);
			_objFunctions.SortTree(treSpells);
			_objFunctions.SortTree(treComplexForms);
			_objFunctions.SortTree(treQualities);
			_objFunctions.SortTree(treCritterPowers);
			_objFunctions.SortTree(treMartialArts);
			UpdateMentorSpirits();
			UpdateInitiationGradeList();
			PopulateCalendar();
			RefreshImprovements();

			UpdateCharacterInfo();

			_blnIsDirty = false;
			UpdateWindowTitle(false);
			RefreshPasteStatus();

			// Stupid hack to get the MDI icon to show up properly.
			this.Icon = this.Icon.Clone() as System.Drawing.Icon;
		}

		private void frmCareer_FormClosing(object sender, FormClosingEventArgs e)
		{
			// If there are unsaved changes to the character, as the user if they would like to save their changes.
			if (_blnIsDirty)
			{
				string strCharacterName = _objCharacter.Alias;
				if (_objCharacter.Alias.Trim() == string.Empty)
					strCharacterName = LanguageManager.Instance.GetString("String_UnnamedCharacter");
				DialogResult objResult = MessageBox.Show(LanguageManager.Instance.GetString("Message_UnsavedChanges").Replace("{0}", strCharacterName), LanguageManager.Instance.GetString("MessageTitle_UnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if (objResult == DialogResult.Yes)
				{
					// Attempt to save the Character. If the user cancels the Save As dialogue that may open, cancel the closing event so that changes are not lost.
					bool blnResult = SaveCharacter();
					if (!blnResult)
						e.Cancel = true;
				}
				else if (objResult == DialogResult.Cancel)
				{
					e.Cancel = true;
				}
			}
			// Reset the ToolStrip so the Save button is removed for the currently closing window.
			if (!e.Cancel)
			{
				if (!_blnSkipToolStripRevert)
					ToolStripManager.RevertMerge("toolStrip");

				// Unsubscribe from events.
				_objCharacter.MAGEnabledChanged -= objCharacter_MAGEnabledChanged;
				_objCharacter.RESEnabledChanged -= objCharacter_RESEnabledChanged;
				_objCharacter.AdeptTabEnabledChanged -= objCharacter_AdeptTabEnabledChanged;
				_objCharacter.MagicianTabEnabledChanged -= objCharacter_MagicianTabEnabledChanged;
				_objCharacter.TechnomancerTabEnabledChanged -= objCharacter_TechnomancerTabEnabledChanged;
				_objCharacter.CritterTabEnabledChanged -= objCharacter_CritterTabEnabledChanged;
				_objCharacter.BlackMarketEnabledChanged -= objCharacter_BlackMarketChanged;
				_objCharacter.UneducatedChanged -= objCharacter_UneducatedChanged;
				_objCharacter.UncouthChanged -= objCharacter_UncouthChanged;
				_objCharacter.InfirmChanged -= objCharacter_InfirmChanged;
				GlobalOptions.Instance.MRUChanged -= PopulateMRU;

				treGear.ItemDrag -= treGear_ItemDrag;
				treGear.DragEnter -= treGear_DragEnter;
				treGear.DragDrop -= treGear_DragDrop;

				treLifestyles.ItemDrag -= treLifestyles_ItemDrag;
				treLifestyles.DragEnter -= treLifestyles_DragEnter;
				treLifestyles.DragDrop -= treLifestyles_DragDrop;

				treArmor.ItemDrag -= treArmor_ItemDrag;
				treArmor.DragEnter -= treArmor_DragEnter;
				treArmor.DragDrop -= treArmor_DragDrop;

				treWeapons.ItemDrag -= treWeapons_ItemDrag;
				treWeapons.DragEnter -= treWeapons_DragEnter;
				treWeapons.DragDrop -= treWeapons_DragDrop;

				treVehicles.ItemDrag -= treVehicles_ItemDrag;
				treVehicles.DragEnter -= treVehicles_DragEnter;
				treVehicles.DragDrop -= treVehicles_DragDrop;

				treImprovements.ItemDrag -= treImprovements_ItemDrag;
				treImprovements.DragEnter -= treImprovements_DragEnter;
				treImprovements.DragDrop -= treImprovements_DragDrop;

				// Remove events from all UserControls.
				foreach (SkillControl objSkillControl in panSkillGroups.Controls.OfType<SkillControl>())
				{
					objSkillControl.RatingChanged -= objActiveSkill_RatingChanged;
					objSkillControl.SpecializationChanged -= objSkill_SpecializationChanged;
					objSkillControl.SpecializationLeave -= objSkill_SpecializationLeave;
					objSkillControl.SkillKarmaClicked -= objSkill_KarmaClicked;
					objSkillControl.DiceRollerClicked -= objSkill_DiceRollerClicked;
				}

				foreach (SkillGroupControl objGroupControl in panSkillGroups.Controls.OfType<SkillGroupControl>())
				{
					objGroupControl.GroupRatingChanged -= objGroup_RatingChanged;
					objGroupControl.GroupKarmaClicked -= objGroup_KarmaClicked;
				}

				foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls.OfType<SkillControl>())
				{
					objSkillControl.RatingChanged -= objKnowledgeSkill_RatingChanged;
					objSkillControl.SpecializationChanged -= objSkill_SpecializationChanged;
					objSkillControl.SpecializationLeave -= objSkill_SpecializationLeave;
					objSkillControl.DeleteSkill -= objKnowledgeSkill_DeleteSkill;
					objSkillControl.SkillKarmaClicked -= objKnowledgeSkill_KarmaClicked;
					objSkillControl.DiceRollerClicked -= objSkill_DiceRollerClicked;
				}

				foreach (ContactControl objContactControl in panContacts.Controls.OfType<ContactControl>())
				{
					objContactControl.ConnectionRatingChanged -= objContact_ConnectionRatingChanged;
					objContactControl.ConnectionGroupRatingChanged -= objContact_ConnectionGroupRatingChanged;
					objContactControl.LoyaltyRatingChanged -= objContact_LoyaltyRatingChanged;
					objContactControl.DeleteContact -= objContact_DeleteContact;
					objContactControl.FileNameChanged -= objContact_FileNameChanged;
				}

				foreach (ContactControl objContactControl in panEnemies.Controls.OfType<ContactControl>())
				{
					objContactControl.ConnectionRatingChanged -= objEnemy_ConnectionRatingChanged;
					objContactControl.ConnectionGroupRatingChanged -= objEnemy_ConnectionGroupRatingChanged;
					objContactControl.LoyaltyRatingChanged -= objEnemy_LoyaltyRatingChanged;
					objContactControl.DeleteContact -= objEnemy_DeleteContact;
					objContactControl.FileNameChanged -= objEnemy_FileNameChanged;
				}

				foreach (PetControl objContactControl in panPets.Controls.OfType<PetControl>())
				{
					objContactControl.DeleteContact -= objPet_DeleteContact;
					objContactControl.FileNameChanged -= objPet_FileNameChanged;
				}

				foreach (PowerControl objPowerControl in panPowers.Controls.OfType<PowerControl>())
				{
					objPowerControl.PowerRatingChanged -= objPower_PowerRatingChanged;
					objPowerControl.DeletePower -= objPower_DeletePower;
				}

				foreach (SpiritControl objSpiritControl in panSpirits.Controls.OfType<SpiritControl>())
				{
					objSpiritControl.ServicesOwedChanged -= objSpirit_ServicesOwedChanged;
					objSpiritControl.ForceChanged -= objSpirit_ForceChanged;
					objSpiritControl.BoundChanged -= objSpirit_BoundChanged;
					objSpiritControl.DeleteSpirit -= objSpirit_DeleteSpirit;
					objSpiritControl.FileNameChanged -= objSpirit_FileNameChanged;
				}

				foreach (SpiritControl objSpiritControl in panSprites.Controls.OfType<SpiritControl>())
				{
					objSpiritControl.ServicesOwedChanged -= objSprite_ServicesOwedChanged;
					objSpiritControl.ForceChanged -= objSprite_ForceChanged;
					objSpiritControl.BoundChanged -= objSprite_BoundChanged;
					objSpiritControl.DeleteSpirit -= objSprite_DeleteSpirit;
					objSpiritControl.FileNameChanged -= objSprite_FileNameChanged;
				}

				// Trash the global variables and dispose of the Form.
				_objOptions = null;
				_objCharacter = null;
				_objImprovementManager = null;
				this.Dispose(true);
			}
		}

		private void frmCareer_Activated(object sender, EventArgs e)
		{
			// Merge the ToolStrips.
			ToolStripManager.RevertMerge("toolStrip");
			ToolStripManager.Merge(toolStrip, "toolStrip");
		}

		private void frmCareer_Shown(object sender, EventArgs e)
		{
			// Clear all of the placeholder Labels.
			foreach (Label objLabel in tabCommon.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabMartialArts.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabMagician.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabTechnomancer.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabCyberware.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabLifestyle.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabArmor.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabWeapons.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabGear.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabVehicles.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabInitiation.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabCritter.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			foreach (Label objLabel in tabImprovements.Controls.OfType<Label>())
			{
				if (objLabel.Text.StartsWith("["))
					objLabel.Text = "";
			}

			frmCareer_Resize(sender, e);
		}

		private void frmCareer_Resize(object sender, EventArgs e)
		{
			TabPage objPage = tabCharacterTabs.SelectedTab;
			// Reseize the form elements with the form.

			// Character Info Tab.
			int intHeight = ((objPage.Height - lblDescription.Top) / 4 - 20);
			txtDescription.Height = intHeight;
			lblBackground.Top = txtDescription.Top + txtDescription.Height + 3;
			txtBackground.Top = lblBackground.Top + lblBackground.Height + 3;
			txtBackground.Height = intHeight;
			lblConcept.Top = txtBackground.Top + txtBackground.Height + 3;
			txtConcept.Top = lblConcept.Top + lblConcept.Height + 3;
			txtConcept.Height = intHeight;
			lblNotes.Top = txtConcept.Top + txtConcept.Height + 3;
			txtNotes.Top = lblNotes.Top + lblNotes.Height + 3;
			txtNotes.Height = intHeight;
		}
		#endregion

		#region Character Events
		private void objCharacter_MAGEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of MAG being enabled.
			lblMAGLabel.Enabled = _objCharacter.MAGEnabled;
			lblMAGAug.Enabled = _objCharacter.MAGEnabled;
			lblMAG.Enabled = _objCharacter.MAGEnabled;
			lblMAGMetatype.Enabled = _objCharacter.MAGEnabled;

			lblFoci.Visible = _objCharacter.MAGEnabled;
			treFoci.Visible = _objCharacter.MAGEnabled;
			cmdCreateStackedFocus.Visible = _objCharacter.MAGEnabled;

			if (_objCharacter.MAGEnabled)
			{
				// Show the Initiation Tab.
				if (!tabCharacterTabs.TabPages.Contains(tabInitiation))
				{
					tabCharacterTabs.TabPages.Insert(3, tabInitiation);
					tabInitiation.Text = LanguageManager.Instance.GetString("Tab_Initiation");
					lblInitiateGradeLabel.Text = LanguageManager.Instance.GetString("Label_InitiationGrade");
					cmdAddMetamagic.Text = LanguageManager.Instance.GetString("Button_AddMetamagic");
					chkInitiationGroup.Text = LanguageManager.Instance.GetString("Checkbox_GroupInitiation");
					chkInitiationOrdeal.Text = LanguageManager.Instance.GetString("Checkbox_InitiationOrdeal");
					chkJoinGroup.Text = LanguageManager.Instance.GetString("Checkbox_JoinedGroup");
				}
			}
			else
			{
				ClearInitiationTab();
				tabCharacterTabs.TabPages.Remove(tabInitiation);
			}

			// Run through all of the Skills and Enable/Disable them as needed.
			foreach (SkillControl objSkillControl in panActiveSkills.Controls)
			{
				if (objSkillControl.Attribute == "MAG")
				{
					objSkillControl.Enabled = _objCharacter.MAGEnabled;
					if (!objSkillControl.Enabled)
						objSkillControl.SkillRating = 0;
				}
			}
			// Run through all of the Skill Groups and Disable them if all of their Skills are currently inaccessible.
			foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
			{
				bool blnEnabled = false;
				foreach (Skill objSkill in _objCharacter.Skills)
				{
					if (objSkill.SkillGroup == objSkillGroupControl.GroupName)
					{
						if (objSkill.Attribute == "MAG" || objSkill.Attribute == "RES")
						{
							if (objSkill.Attribute == "MAG" && _objCharacter.MAGEnabled)
								blnEnabled = true;
							if (objSkill.Attribute == "RES" && _objCharacter.RESEnabled)
								blnEnabled = true;
						}
						else
							blnEnabled = true;
					}
				}
				objSkillGroupControl.IsEnabled = blnEnabled;
				if (!blnEnabled)
					objSkillGroupControl.GroupRating = 0;
			}
		}

		private void objCharacter_RESEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of RES being enabled.
			lblRESLabel.Enabled = _objCharacter.RESEnabled;
			lblRESAug.Enabled = _objCharacter.RESEnabled;
			lblRES.Enabled = _objCharacter.RESEnabled;
			lblRESMetatype.Enabled = _objCharacter.RESEnabled;

			if (_objCharacter.RESEnabled)
			{
				// Show the Initiation Tab.
				if (!tabCharacterTabs.TabPages.Contains(tabInitiation))
				{
					tabCharacterTabs.TabPages.Insert(3, tabInitiation);
					tabInitiation.Text = LanguageManager.Instance.GetString("Tab_Submersion");
					lblInitiateGradeLabel.Text = LanguageManager.Instance.GetString("Label_SubmersionGrade");
					cmdAddMetamagic.Text = LanguageManager.Instance.GetString("Button_AddEcho");
					chkInitiationGroup.Text = LanguageManager.Instance.GetString("Checkbox_NetworkSubmersion");
					chkInitiationOrdeal.Text = LanguageManager.Instance.GetString("Checkbox_SubmersionTask");
					chkJoinGroup.Text = LanguageManager.Instance.GetString("Checkbox_JoinedNetwork");
				}
			}
			else
			{
				ClearInitiationTab();
				tabCharacterTabs.TabPages.Remove(tabInitiation);
			}

			// Run through all of the Skills and Enable/Disable them as needed.
			foreach (SkillControl objSkillControl in panActiveSkills.Controls)
			{
				if (objSkillControl.Attribute == "RES")
				{
					objSkillControl.Enabled = _objCharacter.RESEnabled;
					if (!objSkillControl.Enabled)
						objSkillControl.SkillRating = 0;
				}
			}
			// Run through all of the Skill Groups and Disable them if all of their Skills are currently inaccessible.
			foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
			{
				bool blnEnabled = false;
				foreach (Skill objSkill in _objCharacter.Skills)
				{
					if (objSkill.SkillGroup == objSkillGroupControl.GroupName)
					{
						if (objSkill.Attribute == "MAG" || objSkill.Attribute == "RES")
						{
							if (objSkill.Attribute == "MAG" && _objCharacter.MAGEnabled)
								blnEnabled = true;
							if (objSkill.Attribute == "RES" && _objCharacter.RESEnabled)
								blnEnabled = true;
						}
						else
							blnEnabled = true;
					}
				}
				objSkillGroupControl.IsEnabled = blnEnabled;
				if (!blnEnabled)
					objSkillGroupControl.GroupRating = 0;
			}
		}

		private void objCharacter_AdeptTabEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Adept being enabled.
			if (_objCharacter.AdeptEnabled)
			{
				if (!tabCharacterTabs.TabPages.Contains(tabAdept))
					tabCharacterTabs.TabPages.Insert(3, tabAdept);

				CalculatePowerPoints();
			}
			else
			{
				ClearAdeptTab();
				tabCharacterTabs.TabPages.Remove(tabAdept);
			}

			// Show the Mystic Adept control if the character is a Mystic Adept, otherwise hide them.
			if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
			{
				lblMysticAdeptAssignment.Visible = true;
				lblMysticAdeptAssignmentAdept.Visible = true;
				lblMysticAdeptAssignmentMagician.Visible = true;
				lblMysticAdeptMAGAdept.Visible = true;
				nudMysticAdeptMAGMagician.Visible = true;
				nudMysticAdeptMAGMagician.Maximum = _objCharacter.MAG.TotalValue;
			}
			else
			{
				lblMysticAdeptAssignment.Visible = false;
				lblMysticAdeptAssignmentAdept.Visible = false;
				lblMysticAdeptAssignmentMagician.Visible = false;
				lblMysticAdeptMAGAdept.Visible = false;
				nudMysticAdeptMAGMagician.Visible = false;
			}
		}

		private void objCharacter_MagicianTabEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Magician being enabled.
			if (_objCharacter.MagicianEnabled)
			{
				if (!tabCharacterTabs.TabPages.Contains(tabMagician))
					tabCharacterTabs.TabPages.Insert(3, tabMagician);
			}
			else
			{
				ClearSpellTab();
				tabCharacterTabs.TabPages.Remove(tabMagician);
			}

			// Show the Mystic Adept control if the character is a Mystic Adept, otherwise hide them.
			if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
			{
				lblMysticAdeptAssignment.Visible = true;
				lblMysticAdeptAssignmentAdept.Visible = true;
				lblMysticAdeptAssignmentMagician.Visible = true;
				lblMysticAdeptMAGAdept.Visible = true;
				nudMysticAdeptMAGMagician.Visible = true;
				nudMysticAdeptMAGMagician.Maximum = _objCharacter.MAG.TotalValue;
			}
			else
			{
				lblMysticAdeptAssignment.Visible = false;
				lblMysticAdeptAssignmentAdept.Visible = false;
				lblMysticAdeptAssignmentMagician.Visible = false;
				lblMysticAdeptMAGAdept.Visible = false;
				nudMysticAdeptMAGMagician.Visible = false;
			}
		}

		private void objCharacter_TechnomancerTabEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Technomancer being enabled.
			if (_objCharacter.TechnomancerEnabled)
			{
				if (!tabCharacterTabs.TabPages.Contains(tabTechnomancer))
					tabCharacterTabs.TabPages.Insert(3, tabTechnomancer);
			}
			else
			{
				ClearTechnomancerTab();
				tabCharacterTabs.TabPages.Remove(tabTechnomancer);
			}
		}

		private void objCharacter_CritterTabEnabledChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change the status of Critter being enabled.
			if (_objCharacter.CritterEnabled)
			{
				if (!tabCharacterTabs.TabPages.Contains(tabCritter))
					tabCharacterTabs.TabPages.Insert(3, tabCritter);
			}
			else
			{
				// Remove all Critter Powers.
				ClearCritterTab();
				tabCharacterTabs.TabPages.Remove(tabCritter);
			}
		}

		private void objCharacter_BlackMarketChanged(object sender)
		{
		}

		private void objCharacter_UneducatedChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Uneducated being enabled.
			if (_objCharacter.Uneducated)
			{
				// If Uneducated is being added, run through all of the Technical Active Skills and disable them.
				// Do not break SkillGroups as these will be used if this is ever removed.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasTechnicalSkills)
					{
						objSkillGroupControl.GroupRating = 0;
						objSkillGroupControl.IsEnabled = false;
					}
				}
			}
			else
			{
				// If Uneducated is being removed, run through all of the Technical Active Skills and re-enable them.
				// If they were a part of a SkillGroup, set their Rating back.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasTechnicalSkills)
					{
						objSkillGroupControl.IsEnabled = true;
					}
				}
			}
		}

		private void objCharacter_UncouthChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Uncouth being enabled.
			if (_objCharacter.Uncouth)
			{
				// If Uncouth is being added, run through all of the Social Active Skills and disable them.
				// Do not break SkillGroups as these will be used if this is ever removed.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasSocialSkills)
					{
						objSkillGroupControl.GroupRating = 0;
						objSkillGroupControl.IsEnabled = false;
					}
				}
			}
			else
			{
				// If Uncouth is being removed, run through all of the Social Active Skills and re-enable them.
				// If they were a part of a SkillGroup, set their Rating back.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasSocialSkills)
					{
						objSkillGroupControl.IsEnabled = true;
					}
				}
			}
		}

		private void objCharacter_InfirmChanged(object sender)
		{
			if (_blnReapplyImprovements)
				return;

			// Change to the status of Infirm being enabled.
			if (_objCharacter.Infirm)
			{
				// If Infirm is being added, run through all of the Physical Active Skills and disable them.
				// Do not break SkillGroups as these will be used if this is ever removed.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasPhysicalSkills)
					{
						objSkillGroupControl.GroupRating = 0;
						objSkillGroupControl.IsEnabled = false;
					}
				}
			}
			else
			{
				// If Infirm is being removed, run through all of the Physical Active Skills and re-enable them.
				// If they were a part of a SkillGroup, set their Rating back.
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					if (objSkillGroupControl.HasPhysicalSkills)
					{
						objSkillGroupControl.IsEnabled = true;
					}
				}
			}
		}
		#endregion

		#region Menu Events
		private void mnuFileSave_Click(object sender, EventArgs e)
		{
			SaveCharacter();
		}

		private void mnuFileSaveAs_Click(object sender, EventArgs e)
		{
			SaveCharacterAs();
		}

		private void tsbSave_Click(object sender, EventArgs e)
		{
			mnuFileSave_Click(sender, e);
		}

		private void tsbPrint_Click(object sender, EventArgs e)
		{
			_objCharacter.Print(false);
		}

		private void mnuFileClose_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void mnuFilePrint_Click(object sender, EventArgs e)
		{
			_objCharacter.Print(false);
		}

		private void mnuFileExport_Click(object sender, EventArgs e)
		{
			// Write the Character information to a MemoryStream so we don't need to create any files.
			MemoryStream objStream = new MemoryStream();
			XmlTextWriter objWriter = new XmlTextWriter(objStream, Encoding.UTF8);

			// Being the document.
			objWriter.WriteStartDocument();

			// </characters>
			objWriter.WriteStartElement("characters");

			_objCharacter.PrintToStream(objStream, objWriter);

			// </characters>
			objWriter.WriteEndElement();

			// Finish the document and flush the Writer and Stream.
			objWriter.WriteEndDocument();
			objWriter.Flush();
			objStream.Flush();

			// Read the stream.
			StreamReader objReader = new StreamReader(objStream);
			objStream.Position = 0;
			XmlDocument objCharacterXML = new XmlDocument();

			// Put the stream into an XmlDocument and send it off to the Viewer.
			string strXML = objReader.ReadToEnd();
			objCharacterXML.LoadXml(strXML);

			objWriter.Close();
			objStream.Close();

			frmExport frmExportCharacter = new frmExport();
			frmExportCharacter.CharacterXML = objCharacterXML;
			frmExportCharacter.ShowDialog(this);
		}

		private void mnuSpecialCyberzombie_Click(object sender, EventArgs e)
		{
			bool blnEssence = true;
			bool blnCyberware = false;
			string strMessage = LanguageManager.Instance.GetString("Message_CyberzombieRequirements");

			// Make sure the character has an Essence lower than 0.
			if (_objCharacter.Essence >= 0)
			{
				strMessage += "\n\t" + LanguageManager.Instance.GetString("Message_CyberzombieRequirementsEssence");
				blnEssence = false;
			}

			// Make sure the character has an Invoked Memory Stimulator.
			foreach (Cyberware objCyberware in _objCharacter.Cyberware)
			{
				if (objCyberware.Name == "Invoked Memory Stimulator")
					blnCyberware = true;
			}

			if (!blnCyberware)
				strMessage += "\n\t" + LanguageManager.Instance.GetString("Message_CyberzombieRequirementsStimulator");

			if (!blnEssence || !blnCyberware)
			{
				MessageBox.Show(strMessage, LanguageManager.Instance.GetString("MessageTitle_CyberzombieRequirements"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_CyberzombieConfirm"), LanguageManager.Instance.GetString("MessageTitle_CyberzombieConfirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				return;

			// Convert the character.
			// Characters lose access to Resonance.
			_objCharacter.RESEnabled = false;

			// Gain MAG that is permanently set to 1.
			_objCharacter.MAGEnabled = true;
			_objCharacter.MAG.MetatypeMinimum = 1;
			_objCharacter.MAG.MetatypeMaximum = 1;
			_objCharacter.MAG.Value = 1;

			// Add the Cyberzombie Lifestyle if it is not already taken.
			bool blnHasLifestyle = false;
			foreach (Lifestyle objLifestyle in _objCharacter.Lifestyles)
			{
				if (objLifestyle.Name == "Cyberzombie Lifestyle Addition")
					blnHasLifestyle = true;
			}
			if (!blnHasLifestyle)
			{
				XmlDocument objXmlLifestyleDocument = XmlManager.Instance.Load("lifestyles.xml");
				XmlNode objXmlLifestyle = objXmlLifestyleDocument.SelectSingleNode("/chummer/lifestyles/lifestyle[name = \"Cyberzombie Lifestyle Addition\"]");

				TreeNode objLifestyleNode = new TreeNode();
				Lifestyle objLifestyle = new Lifestyle(_objCharacter);
				objLifestyle.Create(objXmlLifestyle, objLifestyleNode);
				_objCharacter.Lifestyles.Add(objLifestyle);

				treLifestyles.Nodes[0].Nodes.Add(objLifestyleNode);
				treLifestyles.Nodes[0].Expand();
			}

			// Change the MetatypeCategory to Cyberzombie.
			_objCharacter.MetatypeCategory = "Cyberzombie";

			// Gain access to Critter Powers.
			_objCharacter.CritterEnabled = true;

			// Gain the Dual Natured Critter Power if it does not yet exist.
			bool blnHasPower = false;
			foreach (CritterPower objPower in _objCharacter.CritterPowers)
			{
				if (objPower.Name == "Dual Natured")
					blnHasPower = true;
			}
			if (!blnHasPower)
			{
				XmlDocument objXmlPowerDocument = XmlManager.Instance.Load("critterpowers.xml");
				XmlNode objXmlPowerNode = objXmlPowerDocument.SelectSingleNode("/chummer/powers/power[name = \"Dual Natured\"]");

				TreeNode objNode = new TreeNode();
				CritterPower objCritterPower = new CritterPower(_objCharacter);
				objCritterPower.Create(objXmlPowerNode, _objCharacter, objNode);
				_objCharacter.CritterPowers.Add(objCritterPower);

				treCritterPowers.Nodes[0].Nodes.Add(objNode);
				treCritterPowers.Nodes[0].Expand();
			}

			// Gain the Immunity (Normal Weapons) Critter Power if it does not yet exist.
			blnHasPower = false;
			foreach (CritterPower objPower in _objCharacter.CritterPowers)
			{
				if (objPower.Name == "Immunity" && objPower.Extra == "Normal Weapons")
					blnHasPower = true;
			}
			if (!blnHasPower)
			{
				XmlDocument objXmlPowerDocument = XmlManager.Instance.Load("critterpowers.xml");
				XmlNode objXmlPowerNode = objXmlPowerDocument.SelectSingleNode("/chummer/powers/power[name = \"Immunity\"]");

				TreeNode objNode = new TreeNode();
				CritterPower objCritterPower = new CritterPower(_objCharacter);
				objCritterPower.Create(objXmlPowerNode, _objCharacter, objNode, 0, "Normal Weapons");
				_objCharacter.CritterPowers.Add(objCritterPower);

				treCritterPowers.Nodes[0].Nodes.Add(objNode);
				treCritterPowers.Nodes[0].Expand();
			}

			mnuSpecialCyberzombie.Visible = false;

			_blnIsDirty = true;
			UpdateWindowTitle();

			UpdateCharacterInfo();
		}

		private void mnuSpecialReduceAttribute_Click(object sender, EventArgs e)
		{
			// Display the Select Attribute window and record which Skill was selected.
			frmSelectAttribute frmPickAttribute = new frmSelectAttribute();
			frmPickAttribute.Description = LanguageManager.Instance.GetString("String_CyberzombieReduceAttribute");
			if (_objCharacter.MAGEnabled)
				frmPickAttribute.AddMAG();
			if (_objCharacter.RESEnabled)
				frmPickAttribute.AddRES();
			frmPickAttribute.ShowMetatypeMaximum = true;
			frmPickAttribute.ShowDialog(this);

			if (frmPickAttribute.DialogResult == DialogResult.Cancel)
				return;

			// Create an Improvement to reduce the Attribute's Metatype Maximum.
			if (!frmPickAttribute.DoNotAffectMetatypeMaximum)
				_objImprovementManager.CreateImprovement(frmPickAttribute.SelectedAttribute, Improvement.ImprovementSource.AttributeLoss, "Attribute Loss", Improvement.ImprovementType.Attribute, "", 0, 1, 0, -1);
			// Permanently reduce the Attribute's value.
			_objCharacter.GetAttribute(frmPickAttribute.SelectedAttribute).Value -= 1;

			_blnIsDirty = true;
			UpdateWindowTitle();

			UpdateCharacterInfo();
		}

		private void Menu_DropDownOpening(object sender, EventArgs e)
		{
			foreach (ToolStripMenuItem objItem in ((ToolStripMenuItem)sender).DropDownItems.OfType<ToolStripMenuItem>())
			{
				if (objItem.Tag != null)
				{
					objItem.Text = LanguageManager.Instance.GetString(objItem.Tag.ToString());
				}
			}
		}

		private void mnuSpecialCloningMachine_Click(object sender, EventArgs e)
		{
			frmSelectNumber frmPickNumber = new frmSelectNumber();
			frmPickNumber.Description = LanguageManager.Instance.GetString("String_CloningMachineNumber");
			frmPickNumber.Minimum = 1;
			frmPickNumber.ShowDialog(this);

			if (frmPickNumber.DialogResult == DialogResult.Cancel)
				return;

			int intClones = 0;
			try
			{
				intClones = Convert.ToInt32(frmPickNumber.SelectedValue);
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CloningMachineNumberRequired"), LanguageManager.Instance.GetString("MessageTitle_CloningMachineNumberRequired"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			for (int i = 1; i <= intClones; i++)
				GlobalOptions.Instance.MainForm.LoadCharacter(_objCharacter.FileName, false, _objCharacter.Alias + " " + i.ToString(), true);
		}

		private void mnuSpecialReapplyImprovements_Click(object sender, EventArgs e)
		{
			// This only re-applies the Improvements for everything the character has. If a match is not found in the data files, the current Improvement information is left as-is.
			// Verify that the user wants to go through with it.
			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_ConfirmReapplyImprovements"), LanguageManager.Instance.GetString("MessageTitle_ConfirmReapplyImprovements"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				return;

			// Record the status of any flags that normally trigger character events.
			bool blnMAGEnabled = _objCharacter.MAGEnabled;
			bool blnRESEnabled = _objCharacter.RESEnabled;
			bool blnUneducated = _objCharacter.Uneducated;
			bool blnUncouth = _objCharacter.Uncouth;
			bool blnInfirm = _objCharacter.Infirm;

			_blnReapplyImprovements = true;

			// Refresh Qualities.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("qualities.xml");
			foreach (Quality objQuality in _objCharacter.Qualities)
			{
				string strSelected = objQuality.Extra;

				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objQuality.Name + "\"]");
				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);
					if (objNode["bonus"] != null)
					{
						_objImprovementManager.ForcedValue = strSelected;
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId, objNode["bonus"], false, 1, objQuality.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objQuality.Extra = _objImprovementManager.SelectedValue;

						for (int i = 0; i <= 1; i++)
						{
							foreach (TreeNode objTreeNode in treQualities.Nodes[i].Nodes)
							{
								if (objTreeNode.Tag.ToString() == objQuality.InternalId)
								{
									objTreeNode.Text = objQuality.DisplayName;
									break;
								}
							}
						}
					}
				}
			}

			// Refresh Martial Art Advantages.
			objXmlDocument = XmlManager.Instance.Load("martialarts.xml");
			foreach (MartialArt objMartialArt in _objCharacter.MartialArts)
			{
				foreach (MartialArtAdvantage objAdvantage in objMartialArt.Advantages)
				{
                    XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/martialarts/martialart[name = \"" + objMartialArt.Name + "\"]/techniques/technique[name = \"" + objAdvantage.Name + "\"]");
					if (objNode != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.MartialArtAdvantage, objAdvantage.InternalId);
						if (objNode["bonus"] != null)
						{
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.MartialArtAdvantage, objAdvantage.InternalId, objNode["bonus"], false, 1, objAdvantage.DisplayNameShort);
						}
					}
				}
			}

			// Refresh Spells.
			objXmlDocument = XmlManager.Instance.Load("spells.xml");
			foreach (Spell objSpell in _objCharacter.Spells)
			{
				string strSelected = objSpell.Extra;

				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/spells/spell[name = \"" + objSpell.Name + "\"]");
				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Spell, objSpell.InternalId);
					if (objNode["bonus"] != null)
					{
						_objImprovementManager.ForcedValue = strSelected;
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Spell, objSpell.InternalId, objNode["bonus"], false, 1, objSpell.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objSpell.Extra = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treSpells.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								if (objChildNode.Tag.ToString() == objSpell.InternalId)
								{
									objChildNode.Text = objSpell.DisplayName;
									break;
								}
							}
						}
					}
				}
			}

			// Refresh Adept Powers.
			objXmlDocument = XmlManager.Instance.Load("powers.xml");
			foreach (Power objPower in _objCharacter.Powers)
			{
				string strSelected = objPower.Extra;

				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + objPower.Name + "\"]");
				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Power, objPower.InternalId);
					if (objNode["bonus"] != null)
					{
						_objImprovementManager.ForcedValue = strSelected;
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Power, objPower.InternalId, objNode["bonus"], false, Convert.ToInt32(objPower.Rating), objPower.DisplayNameShort);
					}
				}
			}

			// Refresh Critter Powers.
			objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
			foreach (CritterPower objPower in _objCharacter.CritterPowers)
			{
				string strSelected = objPower.Extra;

				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + objPower.Name + "\"]");

				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.CritterPower, objPower.InternalId);
					if (objNode["bonus"] != null)
					{
						int intRating = 0;
						try
						{
							intRating = Convert.ToInt32(strSelected);
						}
						catch
						{
							_objImprovementManager.ForcedValue = strSelected;
						}
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.CritterPower, objPower.InternalId, objNode["bonus"], false, intRating, objPower.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objPower.Extra = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treCritterPowers.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								if (objChildNode.Tag.ToString() == objPower.InternalId)
								{
									objChildNode.Text = objPower.DisplayName;
									break;
								}
							}
						}
					}
				}
			}

			// Refresh Metamagics and Echoes.
			foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
			{
				if (objMetamagic.SourceType == Improvement.ImprovementSource.Metamagic)
				{
					objXmlDocument = XmlManager.Instance.Load("metamagic.xml");
					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/metamagics/metamagic[name = \"" + objMetamagic.Name + "\"]");

					if (objNode != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId);
						if (objNode["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId, objNode["bonus"], false, 1, objMetamagic.DisplayNameShort);
					}
				}
				else
				{
					objXmlDocument = XmlManager.Instance.Load("echoes.xml");
					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/echoes/echo[name = \"" + objMetamagic.Name + "\"]");

					if (objNode != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId);
						if (objNode["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId, objNode["bonus"], false, 1, objMetamagic.DisplayNameShort);
					}
				}
			}

			// Refresh Cyberware and Bioware.
			foreach (Cyberware objCyberware in _objCharacter.Cyberware)
			{
				if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
				{
					objXmlDocument = XmlManager.Instance.Load("cyberware.xml");
					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/cyberwares/cyberware[name = \"" + objCyberware.Name + "\"]");

					if (objNode != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberware, objCyberware.InternalId);
						if (objNode["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Cyberware, objCyberware.InternalId, objNode["bonus"], false, objCyberware.Rating, objCyberware.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objCyberware.Location = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treCyberware.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								if (objChildNode.Tag.ToString() == objCyberware.InternalId)
								{
									objChildNode.Text = objCyberware.DisplayName;
									break;
								}
							}
						}
					}

					foreach (Cyberware objPlugin in objCyberware.Children)
					{
						XmlNode objChild = objXmlDocument.SelectSingleNode("/chummer/cyberwares/cyberware[name = \"" + objPlugin.Name + "\"]");

						if (objChild != null)
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberware, objPlugin.InternalId);
							if (objChild["bonus"] != null)
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Cyberware, objPlugin.InternalId, objChild["bonus"], false, objPlugin.Rating, objPlugin.DisplayNameShort);
						}
					}
				}
				else
				{
					objXmlDocument = XmlManager.Instance.Load("bioware.xml");
					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/biowares/bioware[name = \"" + objCyberware.Name + "\"]");

					if (objNode != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Bioware, objCyberware.InternalId);
						if (objNode["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Bioware, objCyberware.InternalId, objNode["bonus"], false, objCyberware.Rating, objCyberware.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objCyberware.Location = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treCyberware.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								if (objChildNode.Tag.ToString() == objCyberware.InternalId)
								{
									objChildNode.Text = objCyberware.DisplayName;
									break;
								}
							}
						}
					}

					foreach (Cyberware objPlugin in objCyberware.Children)
					{
						XmlNode objChild = objXmlDocument.SelectSingleNode("/chummer/biowares/bioware[name = \"" + objPlugin.Name + "\"]");

						if (objChild != null)
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Bioware, objPlugin.InternalId);
							if (objChild["bonus"] != null)
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Bioware, objPlugin.InternalId, objChild["bonus"], false, objPlugin.Rating, objPlugin.DisplayNameShort);
						}
					}
				}
			}

			// Refresh Armors.
			foreach (Armor objArmor in _objCharacter.Armor)
			{
				objXmlDocument = XmlManager.Instance.Load("armor.xml");
				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/armors/armor[name = \"" + objArmor.Name + "\"]");

				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);
					if (objNode["bonus"] != null)
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId, objNode["bonus"], false, 1, objArmor.DisplayNameShort);
					if (_objImprovementManager.SelectedValue != "")
						objArmor.Extra = _objImprovementManager.SelectedValue;

					foreach (TreeNode objParentNode in treArmor.Nodes)
					{
						foreach (TreeNode objChildNode in objParentNode.Nodes)
						{
							if (objChildNode.Tag.ToString() == objArmor.InternalId)
							{
								objChildNode.Text = objArmor.DisplayName;
								break;
							}
						}
					}
				}

				foreach (ArmorMod objMod in objArmor.ArmorMods)
				{
					XmlNode objChild = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + objMod.Name + "\"]");

					if (objChild != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
						if (objChild["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId, objChild["bonus"], false, 1, objMod.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objMod.Extra = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treArmor.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								foreach (TreeNode objPluginNode in objChildNode.Nodes)
								{
									if (objPluginNode.Tag.ToString() == objMod.InternalId)
									{
										objPluginNode.Text = objMod.DisplayName;
										break;
									}
								}
							}
						}
					}
				}

				foreach (Gear objGear in objArmor.Gear)
				{
					objXmlDocument = XmlManager.Instance.Load("gear.xml");
					XmlNode objChild = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objGear.Name + "\" and category = \"" + objGear.Category + "\"]");

					if (objChild != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
						if (objChild["bonus"] != null)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objChild["bonus"], false, objGear.Rating, objGear.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objGear.Extra = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treArmor.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								foreach (TreeNode objPluginNode in objChildNode.Nodes)
								{
									if (objPluginNode.Tag.ToString() == objGear.InternalId)
									{
										objPluginNode.Text = objGear.DisplayName;
										break;
									}
								}
							}
						}
					}
				}
			}

			// Refresh Gear.
			objXmlDocument = XmlManager.Instance.Load("gear.xml");
			foreach (Gear objGear in _objCharacter.Gear)
			{
				string strSelected = objGear.Extra;
				XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objGear.Name + "\" and category = \"" + objGear.Category + "\"]");

				if (objNode != null)
				{
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
					if (objNode["bonus"] != null)
					{
						_objImprovementManager.ForcedValue = strSelected;
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objNode["bonus"], false, objGear.Rating, objGear.DisplayNameShort);
						if (_objImprovementManager.SelectedValue != "")
							objGear.Extra = _objImprovementManager.SelectedValue;

						foreach (TreeNode objParentNode in treGear.Nodes)
						{
							foreach (TreeNode objChildNode in objParentNode.Nodes)
							{
								if (objChildNode.Tag.ToString() == objGear.InternalId)
								{
									objChildNode.Text = objGear.DisplayName;
									break;
								}
							}
						}
					}
				}

				foreach (Gear objPlugin in objGear.Children)
				{
					string strPluginSelected = objPlugin.Extra;
					XmlNode objChild = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objPlugin.Name + "\" and category = \"" + objPlugin.Category + "\"]");

					if (objChild != null)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objPlugin.InternalId);
						if (objChild["bonus"] != null)
						{
							_objImprovementManager.ForcedValue = strPluginSelected;
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objPlugin.InternalId, objChild["bonus"], false, objPlugin.Rating, objPlugin.DisplayNameShort);
							if (_objImprovementManager.SelectedValue != "")
								objPlugin.Extra = _objImprovementManager.SelectedValue;

							foreach (TreeNode objParentNode in treGear.Nodes)
							{
								foreach (TreeNode objChildNode in objParentNode.Nodes)
								{
									foreach (TreeNode objPluginNode in objChildNode.Nodes)
									{
										if (objPluginNode.Tag.ToString() == objPlugin.InternalId)
										{
											objPluginNode.Text = objPlugin.DisplayName;
											break;
										}
									}
								}
							}
						}
					}

					foreach (Gear objSubPlugin in objPlugin.Children)
					{
						string strSubPluginSelected = objSubPlugin.Extra;
						XmlNode objSubChild = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSubPlugin.Name + "\" and category = \"" + objSubPlugin.Category + "\"]");

						if (objSubChild != null)
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objSubPlugin.InternalId);
							if (objSubChild["bonus"] != null)
							{
								_objImprovementManager.ForcedValue = strSubPluginSelected;
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objSubPlugin.InternalId, objSubChild["bonus"], false, objSubPlugin.Rating, objSubPlugin.DisplayNameShort);
								if (_objImprovementManager.SelectedValue != "")
									objSubPlugin.Extra = _objImprovementManager.SelectedValue;

								foreach (TreeNode objParentNode in treGear.Nodes)
								{
									foreach (TreeNode objChildNode in objParentNode.Nodes)
									{
										foreach (TreeNode objPluginNode in objChildNode.Nodes)
										{
											foreach (TreeNode objSubPluginNode in objPluginNode.Nodes)
											{
												if (objSubPluginNode.Tag.ToString() == objSubPlugin.InternalId)
												{
													objSubPluginNode.Text = objSubPlugin.DisplayName;
													break;
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}

			_blnReapplyImprovements = false;

			// If the status of any Character Event flags has changed, manually trigger those events.
			if (blnMAGEnabled != _objCharacter.MAGEnabled)
				objCharacter_MAGEnabledChanged(this);
			if (blnRESEnabled != _objCharacter.RESEnabled)
				objCharacter_RESEnabledChanged(this);
			if (blnUneducated != _objCharacter.Uneducated)
				objCharacter_UneducatedChanged(this);
			if (blnUncouth != _objCharacter.Uncouth)
				objCharacter_UncouthChanged(this);
			if (blnInfirm != _objCharacter.Infirm)
				objCharacter_InfirmChanged(this);

			_blnIsDirty = true;
			UpdateWindowTitle();
			UpdateCharacterInfo();
		}

		private void mnuSpecialPossess_Click(object sender, EventArgs e)
		{
			// Make sure the Spirit has been saved first.
			if (_blnIsDirty)
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_PossessionSave"), LanguageManager.Instance.GetString("MessageTitle_Possession"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
					return;
			}

			// Prompt the user to select a save file to possess.
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "Chummer5 Files (*.chum5)|*.chum5|All Files (*.*)|*.*";

			if (openFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				Character objVessel = new Character();
				objVessel.FileName = openFileDialog.FileName;
				objVessel.Load();
				// Make sure the Vessel is in Career Mode.
				if (!objVessel.Created)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_VesselInCareerMode"), LanguageManager.Instance.GetString("MessageTitle_Possession"), MessageBoxButtons.OK, MessageBoxIcon.Error);
					objVessel = null;
					return;
				}

				// Load the Spirit's save file into a new Merge character.
				Character objMerge = new Character();
				objMerge.FileName = _objCharacter.FileName;
				objMerge.Load();
				objMerge.Possessed = true;
				objMerge.Alias = objVessel.Alias + " (" + LanguageManager.Instance.GetString("String_Possessed") + ")";

				// Give the Critter the Immunity to Normal Weapons Power if they don't already have it.
				bool blnHasImmunity = false;
				foreach (CritterPower objCritterPower in objMerge.CritterPowers)
				{
					if (objCritterPower.Name == "Immunity" && objCritterPower.Extra == "Normal Weapons")
					{
						blnHasImmunity = true;
						break;
					}
				}
				if (!blnHasImmunity)
				{
					XmlDocument objPowerDoc = new XmlDocument();
					objPowerDoc = XmlManager.Instance.Load("critterpowers.xml");
					XmlNode objPower = objPowerDoc.SelectSingleNode("/chummer/powers/power[name = \"Immunity\"]");

					CritterPower objCritterPower = new CritterPower(objMerge);
					TreeNode objDummy = new TreeNode();
					objCritterPower.Create(objPower, objMerge, objDummy, 0, "Normal Weapons");
					objMerge.CritterPowers.Add(objCritterPower);
				}

				// Add the Vessel's Physical Attributes to the Spirit's Force.
				objMerge.BOD.MetatypeMaximum = objVessel.BOD.Value + objMerge.MAG.TotalValue;
				objMerge.BOD.Value = objVessel.BOD.Value + objMerge.MAG.TotalValue;
				objMerge.AGI.MetatypeMaximum = objVessel.AGI.Value + objMerge.MAG.TotalValue;
				objMerge.AGI.Value = objVessel.AGI.Value + objMerge.MAG.TotalValue;
				objMerge.REA.MetatypeMaximum = objVessel.REA.Value + objMerge.MAG.TotalValue;
				objMerge.REA.Value = objVessel.REA.Value + objMerge.MAG.TotalValue;
				objMerge.STR.MetatypeMaximum = objVessel.STR.Value + objMerge.MAG.TotalValue;
				objMerge.STR.Value = objVessel.STR.Value + objMerge.MAG.TotalValue;

				// Copy any Lifestyles the Vessel has.
				foreach (Lifestyle objLifestyle in objVessel.Lifestyles)
					objMerge.Lifestyles.Add(objLifestyle);

				// Copy any Armor the Vessel has.
				foreach (Armor objArmor in objVessel.Armor)
				{
					objMerge.Armor.Add(objArmor);
					CopyArmorImprovements(objVessel, objMerge, objArmor);
				}

				// Copy any Gear the Vessel has.
				foreach (Gear objGear in objVessel.Gear)
				{
					objMerge.Gear.Add(objGear);
					CopyGearImprovements(objVessel, objMerge, objGear);
				}

				// Copy any Cyberware/Bioware the Vessel has.
				foreach (Cyberware objCyberware in objVessel.Cyberware)
				{
					objMerge.Cyberware.Add(objCyberware);
					CopyCyberwareImprovements(objVessel, objMerge, objCyberware);
				}

				// Copy any Weapons the Vessel has.
				foreach (Weapon objWeapon in objVessel.Weapons)
					objMerge.Weapons.Add(objWeapon);

				// Copy and Vehicles the Vessel has.
				foreach (Vehicle objVehicle in objVessel.Vehicles)
					objMerge.Vehicles.Add(objVehicle);

				// Copy the character info.
				objMerge.Sex = objVessel.Sex;
				objMerge.Age = objVessel.Age;
				objMerge.Eyes = objVessel.Eyes;
				objMerge.Hair = objVessel.Hair;
				objMerge.Height = objVessel.Height;
				objMerge.Weight = objVessel.Weight;
				objMerge.Skin = objVessel.Skin;
				objMerge.Name = objVessel.Name;
				objMerge.StreetCred = objVessel.StreetCred;
				objMerge.BurntStreetCred = objVessel.BurntStreetCred;
				objMerge.Notoriety = objVessel.Notoriety;
				objMerge.PublicAwareness = objVessel.PublicAwareness;
				objMerge.Mugshot = objVessel.Mugshot;

				// Now that everything is done, save the merged character and open them.
				SaveFileDialog saveFileDialog = new SaveFileDialog();
				saveFileDialog.Filter = "Chummer5 Files (*.chum5)|*.chum5|All Files (*.*)|*.*";

				string strShowFileName = "";
				string[] strFile = _objCharacter.FileName.Split(Path.DirectorySeparatorChar);
				strShowFileName = strFile[strFile.Length - 1];

				if (strShowFileName == "")
					strShowFileName = _objCharacter.Alias;
				strShowFileName = strShowFileName.Replace(".chum5", string.Empty);

				strShowFileName += " (" + LanguageManager.Instance.GetString("String_Possessed") + ")";

				saveFileDialog.FileName = strShowFileName;

				if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
				{
					objMerge.FileName = saveFileDialog.FileName;
					objMerge.Save();

					// Get the name of the file and destroy the references to the Vessel and the merged character.
					string strOpenFile = objMerge.FileName;
					objMerge = null;
					objVessel = null;

					GlobalOptions.Instance.MainForm.LoadCharacter(strOpenFile);
				}
				else
				{
					// The save process was canceled, so drop everything.
					objMerge = null;
					objVessel = null;
				}
			}
		}

		private void mnuSpecialPossessInanimate_Click(object sender, EventArgs e)
		{
			// Make sure the Spirit has been saved first.
			if (_blnIsDirty)
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_PossessionSave"), LanguageManager.Instance.GetString("MessageTitle_Possession"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
					return;
			}

			// Prompt the user to select an inanimate Vessel.
			XmlDocument objVesselDoc = XmlManager.Instance.Load("vessels.xml");
			XmlNodeList objXmlMetatypeList = objVesselDoc.SelectNodes("/chummer/metatypes/metatype");
			List<ListItem> lstMetatype = new List<ListItem>();
			foreach (XmlNode objXmlMetatype in objXmlMetatypeList)
			{
				ListItem objItem = new ListItem();
				objItem.Value = objXmlMetatype["name"].InnerText;
				if (objXmlMetatype["translate"] != null)
					objItem.Name = objXmlMetatype["translate"].InnerText;
				else
					objItem.Name = objXmlMetatype["name"].InnerText;
				lstMetatype.Add(objItem);
			}

			frmSelectItem frmSelectVessel = new frmSelectItem();
			frmSelectVessel.GeneralItems = lstMetatype;
			frmSelectVessel.ShowDialog(this);

			if (frmSelectVessel.DialogResult == DialogResult.Cancel)
				return;

			// Load the Spirit's save file into a new Merge character.
			Character objMerge = new Character();
			objMerge.FileName = _objCharacter.FileName;
			objMerge.Load();
			objMerge.Possessed = true;
			objMerge.Alias = frmSelectVessel.SelectedItem + " (" + LanguageManager.Instance.GetString("String_Possessed") + ")";

			// Get the Node for the selected Vessel.
			XmlNode objSelected = objVesselDoc.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + frmSelectVessel.SelectedItem + "\"]");

			// Get the Attribute Modifiers for the Vessel.
			int intBOD = Convert.ToInt32(objSelected["bodmin"].InnerText);
			int intAGI = Convert.ToInt32(objSelected["agimin"].InnerText);
			int intREA = Convert.ToInt32(objSelected["reamin"].InnerText);
			int intSTR = Convert.ToInt32(objSelected["strmin"].InnerText);

			// Add the Attribute modifiers, making sure that none of them go below 1.
			int intSetBOD = objMerge.MAG.TotalValue + intBOD;
			int intSetAGI = objMerge.MAG.TotalValue + intAGI;
			int intSetREA = objMerge.MAG.TotalValue + intREA;
			int intSetSTR = objMerge.MAG.TotalValue + intSTR;

			objMerge.BOD.MetatypeMinimum += intBOD;
			if (objMerge.BOD.MetatypeMinimum < 1)
				objMerge.BOD.MetatypeMinimum = 1;
			objMerge.BOD.MetatypeMaximum += intBOD;
			if (objMerge.BOD.MetatypeMaximum < 1)
				objMerge.BOD.MetatypeMaximum = 1;
			objMerge.BOD.Value = intSetBOD;
			if (objMerge.BOD.Value < 1)
				objMerge.BOD.Value = 1;

			objMerge.AGI.MetatypeMinimum += intAGI;
			if (objMerge.AGI.MetatypeMinimum < 1)
				objMerge.AGI.MetatypeMinimum = 1;
			objMerge.AGI.MetatypeMaximum += intAGI;
			if (objMerge.AGI.MetatypeMaximum < 1)
				objMerge.AGI.MetatypeMaximum = 1;
			objMerge.AGI.Value = intSetAGI;
			if (objMerge.AGI.Value < 1)
				objMerge.AGI.Value = 1;

			objMerge.REA.MetatypeMinimum += intREA;
			if (objMerge.REA.MetatypeMinimum < 1)
				objMerge.REA.MetatypeMinimum = 1;
			objMerge.REA.MetatypeMaximum += intREA;
			if (objMerge.REA.MetatypeMaximum < 1)
				objMerge.REA.MetatypeMaximum = 1;
			objMerge.REA.Value = intSetREA;
			if (objMerge.REA.Value < 1)
				objMerge.REA.Value = 1;

			objMerge.STR.MetatypeMinimum += intSTR;
			if (objMerge.STR.MetatypeMinimum < 1)
				objMerge.STR.MetatypeMinimum = 1;
			objMerge.STR.MetatypeMaximum += intSTR;
			if (objMerge.STR.MetatypeMaximum < 1)
				objMerge.STR.MetatypeMaximum = 1;
			objMerge.STR.Value = intSetSTR;
			if (objMerge.STR.Value < 1)
				objMerge.STR.Value = 1;

			// Update the Movement if the Vessel has one.
			if (objSelected["movement"] != null)
				objMerge.Movement = objSelected["movement"].InnerText;

			// Add any additional Critter Powers the Vessel grants.
			if (objSelected["powers"] != null)
			{
				XmlDocument objXmlPowerDoc = XmlManager.Instance.Load("critterpowers.xml");
				foreach (XmlNode objXmlPower in objSelected.SelectNodes("powers/power"))
				{
					XmlNode objXmlCritterPower = objXmlPowerDoc.SelectSingleNode("/chummer/powers/power[name = \"" + objXmlPower.InnerText + "\"]");
					CritterPower objPower = new CritterPower(objMerge);
					string strSelect = "";
					int intRating = 0;
					if (objXmlPower.Attributes["select"] != null)
						strSelect = objXmlPower.Attributes["select"].InnerText;
					if (objXmlPower.Attributes["rating"] != null)
						intRating = Convert.ToInt32(objXmlPower.Attributes["rating"].InnerText);

					TreeNode objDummy = new TreeNode();
					objPower.Create(objXmlCritterPower, objMerge, objDummy, intRating, strSelect);

					objMerge.CritterPowers.Add(objPower);
				}
			}

			// Give the Critter the Immunity to Normal Weapons Power if they don't already have it.
			bool blnHasImmunity = false;
			foreach (CritterPower objCritterPower in objMerge.CritterPowers)
			{
				if (objCritterPower.Name == "Immunity" && objCritterPower.Extra == "Normal Weapons")
				{
					blnHasImmunity = true;
					break;
				}
			}
			if (!blnHasImmunity)
			{
				XmlDocument objPowerDoc = new XmlDocument();
				objPowerDoc = XmlManager.Instance.Load("critterpowers.xml");
				XmlNode objPower = objPowerDoc.SelectSingleNode("/chummer/powers/power[name = \"Immunity\"]");

				CritterPower objCritterPower = new CritterPower(objMerge);
				TreeNode objDummy = new TreeNode();
				objCritterPower.Create(objPower, objMerge, objDummy, 0, "Normal Weapons");
				objMerge.CritterPowers.Add(objCritterPower);
			}

			// Add any Improvements the Vessel grants.
			if (objSelected["bonus"] != null)
			{
				ImprovementManager objMergeManager = new ImprovementManager(objMerge);
				objMergeManager.CreateImprovements(Improvement.ImprovementSource.Metatype, frmSelectVessel.SelectedItem, objSelected["bonus"], false, 1, frmSelectVessel.SelectedItem);
			}

			// Now that everything is done, save the merged character and open them.
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "Chummer5 Files (*.chum5)|*.chum5|All Files (*.*)|*.*";

			string strShowFileName = "";
			string[] strFile = _objCharacter.FileName.Split(Path.DirectorySeparatorChar);
			strShowFileName = strFile[strFile.Length - 1];

			if (strShowFileName == "")
				strShowFileName = _objCharacter.Alias;
			strShowFileName = strShowFileName.Replace(".chum5", string.Empty);

			strShowFileName += " (" + LanguageManager.Instance.GetString("String_Possessed") + ")";

			saveFileDialog.FileName = strShowFileName;

			if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				objMerge.FileName = saveFileDialog.FileName;
				objMerge.Save();

				// Get the name of the file and destroy the references to the Vessel and the merged character.
				string strOpenFile = objMerge.FileName;
				objMerge = null;

				GlobalOptions.Instance.MainForm.LoadCharacter(strOpenFile);
			}
			else
			{
				// The save process was canceled, so drop everything.
				objMerge = null;
			}
		}

		private void mnuEditCopy_Click(object sender, EventArgs e)
		{
			if (tabCharacterTabs.SelectedTab == tabStreetGear)
			{
				// Lifestyle Tab.
				if (tabStreetGearTabs.SelectedTab == tabLifestyle)
				{
					try
					{
						// Copy the selected Lifestyle.
						Lifestyle objCopyLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);

						if (objCopyLifestyle == null)
							return;

						MemoryStream objStream = new MemoryStream();
						XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
						objWriter.Formatting = Formatting.Indented;
						objWriter.Indentation = 1;
						objWriter.IndentChar = '\t';

						objWriter.WriteStartDocument();

						// </characters>
						objWriter.WriteStartElement("character");

						objCopyLifestyle.Save(objWriter);

						// </characters>
						objWriter.WriteEndElement();

						// Finish the document and flush the Writer and Stream.
						objWriter.WriteEndDocument();
						objWriter.Flush();
						objStream.Flush();

						// Read the stream.
						StreamReader objReader = new StreamReader(objStream);
						objStream.Position = 0;
						XmlDocument objCharacterXML = new XmlDocument();

						// Put the stream into an XmlDocument.
						string strXML = objReader.ReadToEnd();
						objCharacterXML.LoadXml(strXML);

						objWriter.Close();
						objStream.Close();

						GlobalOptions.Instance.Clipboard = objCharacterXML;
						GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Lifestyle;
						//Clipboard.SetText(objCharacterXML.OuterXml);
					}
					catch
					{
					}
				}

				// Armor Tab.
				if (tabStreetGearTabs.SelectedTab == tabArmor)
				{
					try
					{
						// Copy the selected Armor.
						Armor objCopyArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);

						if (objCopyArmor != null)
						{
							MemoryStream objStream = new MemoryStream();
							XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
							objWriter.Formatting = Formatting.Indented;
							objWriter.Indentation = 1;
							objWriter.IndentChar = '\t';

							objWriter.WriteStartDocument();

							// </characters>
							objWriter.WriteStartElement("character");

							objCopyArmor.Save(objWriter);

							// </characters>
							objWriter.WriteEndElement();

							// Finish the document and flush the Writer and Stream.
							objWriter.WriteEndDocument();
							objWriter.Flush();
							objStream.Flush();

							// Read the stream.
							StreamReader objReader = new StreamReader(objStream);
							objStream.Position = 0;
							XmlDocument objCharacterXML = new XmlDocument();

							// Put the stream into an XmlDocument.
							string strXML = objReader.ReadToEnd();
							objCharacterXML.LoadXml(strXML);

							objWriter.Close();
							objStream.Close();

							GlobalOptions.Instance.Clipboard = objCharacterXML;
							GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Armor;

							RefreshPasteStatus();
							return;
						}

						// Attempt to copy Gear.
						Gear objCopyGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objCopyArmor);

						if (objCopyGear != null)
						{
							MemoryStream objStream = new MemoryStream();
							XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
							objWriter.Formatting = Formatting.Indented;
							objWriter.Indentation = 1;
							objWriter.IndentChar = '\t';

							objWriter.WriteStartDocument();

							// </characters>
							objWriter.WriteStartElement("character");

							if (objCopyGear.GetType() == typeof(Commlink))
							{
								Commlink objCommlink = (Commlink)objCopyGear;
								objCommlink.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Commlink;
							}
							else
							{
								objCopyGear.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Gear;
							}

							if (objCopyGear.WeaponID != Guid.Empty.ToString())
							{
								// Copy any Weapon that comes with the Gear.
								Weapon objCopyWeapon = _objFunctions.FindWeapon(objCopyGear.WeaponID, _objCharacter.Weapons);
								objCopyWeapon.Save(objWriter);
							}

							// </characters>
							objWriter.WriteEndElement();

							// Finish the document and flush the Writer and Stream.
							objWriter.WriteEndDocument();
							objWriter.Flush();
							objStream.Flush();

							// Read the stream.
							StreamReader objReader = new StreamReader(objStream);
							objStream.Position = 0;
							XmlDocument objCharacterXML = new XmlDocument();

							// Put the stream into an XmlDocument.
							string strXML = objReader.ReadToEnd();
							objCharacterXML.LoadXml(strXML);

							objWriter.Close();
							objStream.Close();

							GlobalOptions.Instance.Clipboard = objCharacterXML;

							RefreshPasteStatus();
							return;
						}
					}
					catch
					{
					}
				}

				// Weapons Tab.
				if (tabStreetGearTabs.SelectedTab == tabWeapons)
				{
					try
					{
						// Copy the selected Weapon.
						Weapon objCopyWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

						if (objCopyWeapon != null)
						{
							// Do not let the user copy Gear or Cyberware Weapons.
							if (objCopyWeapon.Category == "Gear" || objCopyWeapon.Category.StartsWith("Cyberware"))
								return;

							MemoryStream objStream = new MemoryStream();
							XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
							objWriter.Formatting = Formatting.Indented;
							objWriter.Indentation = 1;
							objWriter.IndentChar = '\t';

							objWriter.WriteStartDocument();

							// </characters>
							objWriter.WriteStartElement("character");

							objCopyWeapon.Save(objWriter);

							// </characters>
							objWriter.WriteEndElement();

							// Finish the document and flush the Writer and Stream.
							objWriter.WriteEndDocument();
							objWriter.Flush();
							objStream.Flush();

							// Read the stream.
							StreamReader objReader = new StreamReader(objStream);
							objStream.Position = 0;
							XmlDocument objCharacterXML = new XmlDocument();

							// Put the stream into an XmlDocument.
							string strXML = objReader.ReadToEnd();
							objCharacterXML.LoadXml(strXML);

							objWriter.Close();
							objStream.Close();

							GlobalOptions.Instance.Clipboard = objCharacterXML;
							GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Weapon;

							RefreshPasteStatus();
							return;
						}

						WeaponAccessory objAccessory = new WeaponAccessory(_objCharacter);
						Gear objCopyGear = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objAccessory);

						if (objCopyGear != null)
						{
							MemoryStream objStream = new MemoryStream();
							XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
							objWriter.Formatting = Formatting.Indented;
							objWriter.Indentation = 1;
							objWriter.IndentChar = '\t';

							objWriter.WriteStartDocument();

							// </characters>
							objWriter.WriteStartElement("character");

							if (objCopyGear.GetType() == typeof(Commlink))
							{
								Commlink objCommlink = (Commlink)objCopyGear;
								objCommlink.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Commlink;
							}
							else
							{
								objCopyGear.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Gear;
							}

							if (objCopyGear.WeaponID != Guid.Empty.ToString())
							{
								// Copy any Weapon that comes with the Gear.
								Weapon objCopyGearWeapon = _objFunctions.FindWeapon(objCopyGear.WeaponID, _objCharacter.Weapons);
								objCopyGearWeapon.Save(objWriter);
							}

							// </characters>
							objWriter.WriteEndElement();

							// Finish the document and flush the Writer and Stream.
							objWriter.WriteEndDocument();
							objWriter.Flush();
							objStream.Flush();

							// Read the stream.
							StreamReader objReader = new StreamReader(objStream);
							objStream.Position = 0;
							XmlDocument objCharacterXML = new XmlDocument();

							// Put the stream into an XmlDocument.
							string strXML = objReader.ReadToEnd();
							objCharacterXML.LoadXml(strXML);

							objWriter.Close();
							objStream.Close();

							GlobalOptions.Instance.Clipboard = objCharacterXML;

							RefreshPasteStatus();
							return;
						}
					}
					catch
					{
					}
				}

				// Gear Tab.
				if (tabStreetGearTabs.SelectedTab == tabGear)
				{
					try
					{
						// Copy the selected Gear.
						Gear objCopyGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);

						if (objCopyGear == null)
							return;

						MemoryStream objStream = new MemoryStream();
						XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
						objWriter.Formatting = Formatting.Indented;
						objWriter.Indentation = 1;
						objWriter.IndentChar = '\t';

						objWriter.WriteStartDocument();

						// </characters>
						objWriter.WriteStartElement("character");

						if (objCopyGear.GetType() == typeof(Commlink))
						{
							Commlink objCommlink = (Commlink)objCopyGear;
							objCommlink.Save(objWriter);
							GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Commlink;
						}
						else
						{
							objCopyGear.Save(objWriter);
							GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Gear;
						}

						if (objCopyGear.WeaponID != Guid.Empty.ToString())
						{
							// Copy any Weapon that comes with the Gear.
							Weapon objCopyWeapon = _objFunctions.FindWeapon(objCopyGear.WeaponID, _objCharacter.Weapons);
							objCopyWeapon.Save(objWriter);
						}

						// </characters>
						objWriter.WriteEndElement();

						// Finish the document and flush the Writer and Stream.
						objWriter.WriteEndDocument();
						objWriter.Flush();
						objStream.Flush();

						// Read the stream.
						StreamReader objReader = new StreamReader(objStream);
						objStream.Position = 0;
						XmlDocument objCharacterXML = new XmlDocument();

						// Put the stream into an XmlDocument.
						string strXML = objReader.ReadToEnd();
						objCharacterXML.LoadXml(strXML);

						objWriter.Close();
						objStream.Close();

						GlobalOptions.Instance.Clipboard = objCharacterXML;
						//Clipboard.SetText(objCharacterXML.OuterXml);
					}
					catch
					{
					}
				}
			}

			// Vehicles Tab.
			if (tabCharacterTabs.SelectedTab == tabVehicles)
			{
				try
				{
					if (treVehicles.SelectedNode.Level == 1)
					{
						// Copy the selected Vehicle.
						Vehicle objCopyVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

						MemoryStream objStream = new MemoryStream();
						XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
						objWriter.Formatting = Formatting.Indented;
						objWriter.Indentation = 1;
						objWriter.IndentChar = '\t';

						objWriter.WriteStartDocument();

						// </characters>
						objWriter.WriteStartElement("character");

						objCopyVehicle.Save(objWriter);

						// </characters>
						objWriter.WriteEndElement();

						// Finish the document and flush the Writer and Stream.
						objWriter.WriteEndDocument();
						objWriter.Flush();
						objStream.Flush();

						// Read the stream.
						StreamReader objReader = new StreamReader(objStream);
						objStream.Position = 0;
						XmlDocument objCharacterXML = new XmlDocument();

						// Put the stream into an XmlDocument.
						string strXML = objReader.ReadToEnd();
						objCharacterXML.LoadXml(strXML);

						objWriter.Close();
						objStream.Close();

						GlobalOptions.Instance.Clipboard = objCharacterXML;
						GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Vehicle;
						//Clipboard.SetText(objCharacterXML.OuterXml);
					}
					else
					{
						Vehicle objVehicle = new Vehicle(_objCharacter);
						Gear objCopyGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);

						if (objCopyGear != null)
						{
							MemoryStream objStream = new MemoryStream();
							XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
							objWriter.Formatting = Formatting.Indented;
							objWriter.Indentation = 1;
							objWriter.IndentChar = '\t';

							objWriter.WriteStartDocument();

							// </characters>
							objWriter.WriteStartElement("character");

							if (objCopyGear.GetType() == typeof(Commlink))
							{
								Commlink objCommlink = (Commlink)objCopyGear;
								objCommlink.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Commlink;
							}
							else
							{
								objCopyGear.Save(objWriter);
								GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Gear;
							}

							if (objCopyGear.WeaponID != Guid.Empty.ToString())
							{
								// Copy any Weapon that comes with the Gear.
								Weapon objCopyWeapon = _objFunctions.FindWeapon(objCopyGear.WeaponID, _objCharacter.Weapons);
								objCopyWeapon.Save(objWriter);
							}

							// </characters>
							objWriter.WriteEndElement();

							// Finish the document and flush the Writer and Stream.
							objWriter.WriteEndDocument();
							objWriter.Flush();
							objStream.Flush();

							// Read the stream.
							StreamReader objReader = new StreamReader(objStream);
							objStream.Position = 0;
							XmlDocument objCharacterXML = new XmlDocument();

							// Put the stream into an XmlDocument.
							string strXML = objReader.ReadToEnd();
							objCharacterXML.LoadXml(strXML);

							objWriter.Close();
							objStream.Close();

							GlobalOptions.Instance.Clipboard = objCharacterXML;

							RefreshPasteStatus();
							return;
						}

						foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
						{
							foreach (VehicleMod objMod in objCharacterVehicle.Mods)
							{
								Weapon objCopyWeapon = _objFunctions.FindWeapon(treVehicles.SelectedNode.Tag.ToString(), objMod.Weapons);
								if (objCopyWeapon != null)
								{
									// Do not let the user copy Gear or Cyberware Weapons.
									if (objCopyWeapon.Category == "Gear" || objCopyWeapon.Category.StartsWith("Cyberware"))
										return;

									MemoryStream objStream = new MemoryStream();
									XmlTextWriter objWriter = new XmlTextWriter(objStream, System.Text.Encoding.Unicode);
									objWriter.Formatting = Formatting.Indented;
									objWriter.Indentation = 1;
									objWriter.IndentChar = '\t';

									objWriter.WriteStartDocument();

									// </characters>
									objWriter.WriteStartElement("character");

									objCopyWeapon.Save(objWriter);

									// </characters>
									objWriter.WriteEndElement();

									// Finish the document and flush the Writer and Stream.
									objWriter.WriteEndDocument();
									objWriter.Flush();
									objStream.Flush();

									// Read the stream.
									StreamReader objReader = new StreamReader(objStream);
									objStream.Position = 0;
									XmlDocument objCharacterXML = new XmlDocument();

									// Put the stream into an XmlDocument.
									string strXML = objReader.ReadToEnd();
									objCharacterXML.LoadXml(strXML);

									objWriter.Close();
									objStream.Close();

									GlobalOptions.Instance.Clipboard = objCharacterXML;
									GlobalOptions.Instance.ClipboardContentType = ClipboardContentType.Weapon;

									RefreshPasteStatus();
									return;
								}
							}
						}
					}
				}
				catch
				{
				}
			}
			RefreshPasteStatus();
		}

		private void tsbCopy_Click(object sender, EventArgs e)
		{
			mnuEditCopy_Click(sender, e);
		}

		private void mnuSpecialConvertToFreeSprite_Click(object sender, EventArgs e)
		{
			XmlDocument objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
			XmlNode objXmlPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"Denial\"]");
			TreeNode objNode = new TreeNode();
			CritterPower objPower = new CritterPower(_objCharacter);
			objPower.Create(objXmlPower, _objCharacter, objNode);
			objPower.CountTowardsLimit = false;
			objNode.ContextMenuStrip = cmsCritterPowers;
			if (objPower.InternalId == Guid.Empty.ToString())
				return;

			_objCharacter.CritterPowers.Add(objPower);

			treCritterPowers.Nodes[0].Nodes.Add(objNode);
			treCritterPowers.Nodes[0].Expand();

			_objCharacter.MetatypeCategory = "Free Sprite";
			mnuSpecialConvertToFreeSprite.Visible = false;

			_objFunctions.SortTree(treCritterPowers);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void mnuSpecialAddCyberwareSuite_Click(object sender, EventArgs e)
		{
			AddCyberwareSuite(Improvement.ImprovementSource.Cyberware);
		}

		private void mnuSpecialAddBiowareSuite_Click(object sender, EventArgs e)
		{
			AddCyberwareSuite(Improvement.ImprovementSource.Bioware);
		}
		#endregion

		#region Attribute Events
		private void nudMysticAdeptMAGMagician_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.MAGMagician = Convert.ToInt32(nudMysticAdeptMAGMagician.Value);
			_objCharacter.MAGAdept = Convert.ToInt32(_objCharacter.MAG.Value - nudMysticAdeptMAGMagician.Value - _objCharacter.EssencePenalty);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdBurnEdge_Click(object sender, EventArgs e)
		{
			// Edge cannot go below 1.
			if (_objCharacter.EDG.Value == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotBurnEdge"), LanguageManager.Instance.GetString("MessageTitle_CannotBurnEdge"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}

			// Verify that the user wants to Burn a point of Edge.
			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_BurnEdge"), LanguageManager.Instance.GetString("MessageTitle_BurnEdge"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				return;

			_objCharacter.EDG.Value -= 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveBOD_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.BOD.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeBODShort")).Replace("{1}", (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;
			
			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeBODShort") + " " + (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "BOD");
			objExpense.Undo = objUndo;
			
			_objCharacter.BOD.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveAGI_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.AGI.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeAGIShort")).Replace("{1}", (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeAGIShort") + " " + (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "AGI");
			objExpense.Undo = objUndo;

			_objCharacter.AGI.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveREA_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.REA.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeREAShort")).Replace("{1}", (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeREAShort") + " " + (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "REA");
			objExpense.Undo = objUndo;

			_objCharacter.REA.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveSTR_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.STR.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeSTRShort")).Replace("{1}", (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeSTRShort") + " " + (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "STR");
			objExpense.Undo = objUndo;

			_objCharacter.STR.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveCHA_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.CHA.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeCHAShort")).Replace("{1}", (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeCHAShort") + " " + (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "CHA");
			objExpense.Undo = objUndo;

			_objCharacter.CHA.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveINT_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.INT.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeINTShort")).Replace("{1}", (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeINTShort") + " " + (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "INT");
			objExpense.Undo = objUndo;

			_objCharacter.INT.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveLOG_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.LOG.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeLOGShort")).Replace("{1}", (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeLOGShort") + " " + (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "LOG");
			objExpense.Undo = objUndo;

			_objCharacter.LOG.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveWIL_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.WIL.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeWILShort")).Replace("{1}", (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeWILShort") + " " + (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "WIL");
			objExpense.Undo = objUndo;

			_objCharacter.WIL.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveEDG_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			if (_objOptions.AlternateMetatypeAttributeKarma)
				intKarmaCost -= (_objCharacter.EDG.MetatypeMinimum - 1) * _objOptions.KarmaAttribute;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeEDGShort")).Replace("{1}", (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeEDGShort") + " " + (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers).ToString() + " -> " + (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "EDG");
			objExpense.Undo = objUndo;

			_objCharacter.EDG.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveMAG_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = 0;
			if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
				intKarmaCost = (_objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			else
				intKarmaCost = (_objCharacter.MAG.Value - _objCharacter.EssencePenalty + 1) * _objOptions.KarmaAttribute;

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			int intFromValue = 0;
			if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
				intFromValue = _objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers;
			else
				intFromValue = _objCharacter.MAG.Value - _objCharacter.EssencePenalty;

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeMAGShort")).Replace("{1}", (intFromValue + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeMAGShort") + " " + (intFromValue).ToString() + " -> " + (intFromValue + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "MAG");
			objExpense.Undo = objUndo;

			_objCharacter.MAG.Value += 1;
			nudMysticAdeptMAGMagician.Maximum = _objCharacter.MAG.TotalValue;

			if (_objCharacter.Metatype == "Free Spirit")
			{
				// MAG determines the Metatype Maximum for Free Spirit, so change the Metatype Maximum for all other Attributes.
				_objCharacter.BOD.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.AGI.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.REA.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.STR.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.CHA.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.INT.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.LOG.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.WIL.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.EDG.MetatypeMaximum = _objCharacter.MAG.Value;
				_objCharacter.ESS.MetatypeMaximum = _objCharacter.MAG.Value;
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdImproveRES_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = 0;
			if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
				intKarmaCost = (_objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute;
			else
				intKarmaCost = (_objCharacter.RES.Value - _objCharacter.EssencePenalty + 1) * _objOptions.KarmaAttribute;

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			int intFromValue = 0;
			if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
				intFromValue = _objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers;
			else
				intFromValue = _objCharacter.RES.Value - _objCharacter.EssencePenalty;

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_AttributeRESShort")).Replace("{1}", (intFromValue + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAttribute") + " " + LanguageManager.Instance.GetString("String_AttributeRESShort") + " " + (intFromValue).ToString() + " -> " + (intFromValue + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, "RES");
			objExpense.Undo = objUndo;

			_objCharacter.RES.Value += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void nudResponse_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.Response = Convert.ToInt32(nudResponse.Value);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void nudSignal_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.Signal = Convert.ToInt32(nudSignal.Value);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region SkillControl Events
		private void objActiveSkill_RatingChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objKnowledgeSkill_RatingChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSkill_SpecializationChanged(Object sender)
		{
			// Handle the SpecializationChanged event for the SkillControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSkill_DiceRollerClicked(Object sender)
		{
			DiceRollerOpened(sender);
		}

		private void objSkill_KarmaClicked(Object sender)
		{
			SkillControl objSkillControl = (SkillControl)sender;

			// Make sure the character has enough Karma to improve the Skill Group.
			int intKarmaCost = 0;
			if (objSkillControl.SkillRating == 0)
				intKarmaCost = _objOptions.KarmaNewActiveSkill;
			else
			{
				if (objSkillControl.SkillRating >= 6)
					// The cost for raising an Active Skill from 6 to 7 (thanks to Aptitude) is doubled.
					intKarmaCost = ((objSkillControl.SkillRating + 1) * _objOptions.KarmaImproveActiveSkill * 2);
				else
					intKarmaCost = (objSkillControl.SkillRating + 1) * _objOptions.KarmaImproveActiveSkill;
			}

			// If the character is Uneducated and the Skill is a Technical Active Skill, Uncouth and a Social Active Skill or Infirm and a Physical Active Skill, double its cost.
			if ((_objCharacter.Uneducated && objSkillControl.SkillCategory == "Technical Active") || (_objCharacter.Uncouth && objSkillControl.SkillCategory == "Social Active") || (_objCharacter.Infirm && objSkillControl.SkillCategory == "Physical Active"))
				intKarmaCost *= 2;

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", objSkillControl.SkillObject.DisplayName).Replace("{1}", (objSkillControl.SkillRating + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			SkillGroup objSkillGroup = new SkillGroup();
			foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
			{
				if (objSkillGroupControl.GroupName == objSkillControl.SkillGroup)
				{
					objSkillGroup = objSkillGroupControl.SkillGroupObject;
					break;
				}
			}

			// If the Skill is Grouped, verify that the user wants to break the Group.
			if (objSkillControl.IsGrouped)
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_BreakSkillGroup").Replace("{0}", objSkillGroup.DisplayName), LanguageManager.Instance.GetString("MessageTitle_BreakSkillGroup"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					return;
				else
				{
					string strSkillGroup = objSkillControl.SkillGroup;
					int intRating = 0;

					// Break the Skill Group itself.
					foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
					{
						if (objSkillGroupControl.GroupName == strSkillGroup)
						{
							intRating = objSkillGroupControl.GroupRating;
							objSkillGroupControl.Broken = true;
							break;
						}
					}

					// Remove all of the Active Skills from the Skill Group being broken.
					string strGroup = objSkillControl.SkillGroup;
					foreach (SkillControl objActiveSkilll in panActiveSkills.Controls)
					{
						if (objActiveSkilll.IsGrouped && objActiveSkilll.SkillGroup == strGroup)
						{
							objActiveSkilll.SkillRating = intRating;
							objActiveSkilll.IsGrouped = false;
						}
					}
				}
			}
			else
			{
				// If the Skill is not Grouped, the Group should still be broken since a Skill from it has been advanced on its own.
				if (objSkillControl.SkillGroup != "")
				{
					// Break the Skill Group.
					foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
					{
						if (objSkillGroupControl.GroupName == objSkillControl.SkillGroup)
						{
							objSkillGroupControl.Broken = true;
							break;
						}
					}
				}
			}

			// Create the Karma Expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseActiveSkill") + " " + objSkillControl.SkillObject.DisplayName + " " + objSkillControl.SkillRating.ToString() + " -> " + (objSkillControl.SkillRating + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);

			ExpenseUndo objUndo = new ExpenseUndo();
			string strSkill = objSkillControl.SkillName;
			if (objSkillControl.SkillName.Contains("Exotic"))
				strSkill += " (" + objSkillControl.SkillSpec + ")";
			objUndo.CreateKarma(KarmaExpenseType.ImproveSkill, strSkill);
			objExpense.Undo = objUndo;

			_objCharacter.Karma -= intKarmaCost;

			objSkillControl.SkillRating += 1;

			// If the option to re-group Skill Groups is enabled, run through the Skill Groups and see if they can be re-enabled.
			if (_objOptions.AllowSkillRegrouping)
			{
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					bool blnBroken = false;
					int intRating = -1;
					if (objSkillGroupControl.Broken)
					{
						foreach (SkillControl objControl in panActiveSkills.Controls)
						{
							if (objControl.SkillGroup == objSkillGroupControl.GroupName)
							{
								if (objControl.SkillRating > 5)
									blnBroken = true;
								if (intRating == -1)
									intRating = objControl.SkillRating;
								if (objControl.SkillRating != intRating)
									blnBroken = true;
								if (objControl.SkillSpec != string.Empty)
									blnBroken = true;
							}
						}
						if (!blnBroken)
						{
							objSkillGroupControl.Broken = false;
							objSkillGroupControl.GroupRating = intRating;
						}
					}
				}
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objKnowledgeSkill_KarmaClicked(Object sender)
		{
			SkillControl objSkillControl = (SkillControl)sender;

			// Make sure the character has enough Karma to improve the Skill Group.
			int intKarmaCost = 0;
			if (objSkillControl.SkillRating == 0)
				intKarmaCost = _objOptions.KarmaNewKnowledgeSkill;
			else
				intKarmaCost = (objSkillControl.SkillRating + 1) * _objOptions.KarmaImproveKnowledgeSkill;

			// If the character is Uneducated and the Skill is an Academic or Professional Skill, double its cost.
			if (_objCharacter.Uneducated && (objSkillControl.SkillCategory == "Academic" || objSkillControl.SkillCategory == "Professional"))
				intKarmaCost *= 2;

			// The Karma Cost for improving a Language Knowledge Skill to Rating 1 is free for characters with the Linguistics Adept Power.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.AdeptLinguistics) > 0 && objSkillControl.SkillCategory == "Language" && objSkillControl.SkillRating == 0)
				intKarmaCost = 0;

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (intKarmaCost > 0)
			{
				if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", objSkillControl.SkillObject.DisplayName).Replace("{1}", (objSkillControl.SkillRating + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
					return;
			}

			// Create the Karma Expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseKnowledgeSkill") + " " + objSkillControl.SkillObject.DisplayName + " " + objSkillControl.SkillRating.ToString() + " -> " + (objSkillControl.SkillRating + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveSkill, objSkillControl.SkillName);
			objExpense.Undo = objUndo;

			_objCharacter.Karma -= intKarmaCost;

			objSkillControl.SkillRating += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSkill_SpecializationLeave(Object sender)
		{
			if (_blnSkipRefresh)
				return;

			_blnSkipRefresh = true;
			SkillControl objSkillControl = (SkillControl)sender;

			// Make sure the character has enough Karma to select the Specialization.
			int intKarmaCost = _objOptions.KarmaSpecialization;

			if (intKarmaCost > _objCharacter.Karma)
			{
				objSkillControl.SkillSpec = objSkillControl.OldSpecialization;
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				_blnSkipRefresh = false;
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpecialization").Replace("{0}", objSkillControl.SkillSpec).Replace("{1}", intKarmaCost.ToString())))
			{
				objSkillControl.SkillSpec = objSkillControl.OldSpecialization;
				_blnSkipRefresh = false;
				return;
			}

			// If the Skill is Grouped, verify that the user wants to break the Group.
			if (objSkillControl.IsGrouped)
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_BreakSkillGroup").Replace("{0}", objSkillControl.SkillObject.DisplayName), LanguageManager.Instance.GetString("MessageTitle_BreakSkillGroup"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				{
					objSkillControl.SkillSpec = objSkillControl.OldSpecialization;
					_blnSkipRefresh = false;
					return;
				}
				else
				{
					string strSkillGroup = objSkillControl.SkillGroup;
					int intRating = 0;

					// Break the Skill Group itself.
					foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
					{
						if (objSkillGroupControl.GroupName == strSkillGroup)
						{
							intRating = objSkillGroupControl.GroupRating;
							objSkillGroupControl.Broken = true;
							break;
						}
					}

					// Remove all of the Active Skills from the Skill Group being broken.
					string strGroup = objSkillControl.SkillGroup;
					foreach (SkillControl objActiveSkilll in panActiveSkills.Controls)
					{
						if (objActiveSkilll.IsGrouped && objActiveSkilll.SkillGroup == strGroup)
						{
							objActiveSkilll.SkillRating = intRating;
							objActiveSkilll.IsGrouped = false;
						}
					}
				}
			}
			else
			{
				// If the Skill is not Grouped, the Group should still be broken since a Skill from it has been advanced on its own.
				if (objSkillControl.SkillGroup != "")
				{
					// Break the Skill Group.
					foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
					{
						if (objSkillGroupControl.GroupName == objSkillControl.SkillGroup)
						{
							objSkillGroupControl.Broken = true;
							break;
						}
					}
				}
			}

			// Create the Karma Expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, objSkillControl.SkillObject.DisplayName + " " + LanguageManager.Instance.GetString("String_ExpenseSpecialization") + " -> " + objSkillControl.SkillSpec, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.SkillSpec, objSkillControl.SkillName);
			objExpense.Undo = objUndo;

			_objCharacter.Karma -= intKarmaCost;

			// If the option to re-group Skill Groups is enabled, run through the Skill Groups and see if they can be re-enabled.
			if (_objOptions.AllowSkillRegrouping)
			{
				foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
				{
					bool blnBroken = false;
					int intRating = -1;
					if (objSkillGroupControl.Broken)
					{
						foreach (SkillControl objControl in panActiveSkills.Controls)
						{
							if (objControl.SkillGroup == objSkillGroupControl.GroupName)
							{
								if (objControl.SkillRating > 5)
									blnBroken = true;
								if (intRating == -1)
									intRating = objControl.SkillRating;
								if (objControl.SkillRating != intRating)
									blnBroken = true;
								if (objControl.SkillSpec != string.Empty)
									blnBroken = true;
							}
						}
						if (!blnBroken)
						{
							objSkillGroupControl.Broken = false;
							objSkillGroupControl.GroupRating = intRating;
						}
					}
				}
			}

			_blnSkipRefresh = false;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objKnowledgeSkill_DeleteSkill(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteKnowledgeSkill")))
				return;

			// Handle the DeleteSkill event for the SkillControl object.
			SkillControl objSender = (SkillControl)sender;
			bool blnFound = false;
			foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls)
			{
				// Set the flag to show that we have found the Skill.
				if (objSkillControl == objSender)
				{
					blnFound = true;
					_objCharacter.Skills.Remove(objSkillControl.SkillObject);
				}

				// Once the Skill has been found, all of the other SkillControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
					objSkillControl.Top -= 23;
			}
			// Remove the SkillControl that raised the Event.
			panKnowledgeSkills.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objGroup_KarmaClicked(Object sender)
		{
			SkillGroupControl objGroupControl = (SkillGroupControl)sender;

			// Make sure the character has enough Karma to improve the Skill Group.
			int intKarmaCost = 0;
			if (objGroupControl.GroupRating == 0)
				intKarmaCost = _objOptions.KarmaNewSkillGroup;
			else
				intKarmaCost = (objGroupControl.GroupRating + 1) * _objOptions.KarmaImproveSkillGroup;

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", objGroupControl.SkillGroupObject.DisplayName).Replace("{1}", (objGroupControl.GroupRating + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Karma Expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseSkillGroup") + " " + objGroupControl.SkillGroupObject.DisplayName + " " + objGroupControl.GroupRating.ToString() + " -> " + (objGroupControl.GroupRating + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveSkillGroup, objGroupControl.GroupName);
			objExpense.Undo = objUndo;

			_objCharacter.Karma -= intKarmaCost;

			objGroupControl.GroupRating += 1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objGroup_RatingChanged(Object sender)
		{
			// Handle the GroupRatingChanged event for the SkillGroupControl object.
			SkillGroupControl objGroupControl = (SkillGroupControl)sender;
			XmlDocument objXmlDocument = XmlManager.Instance.Load("skills.xml");

			if (objGroupControl.Broken)
				return;

			// Retrieve the list of Skills that are associated with the Skill Group.
			XmlNodeList objXmlSkillList = objXmlDocument.SelectNodes("/chummer/skills/skill[skillgroup = \"" + objGroupControl.GroupName + "\"]");

			foreach (XmlNode objXmlSkill in objXmlSkillList)
			{
				// Run through all of the Skills in the Active Skill Panel and update the ones that match the Skills in the Skill Group.
				foreach (SkillControl objSkillControl in panActiveSkills.Controls)
				{
					if (objSkillControl.SkillName == objXmlSkill["name"].InnerText)
					{
						if (objGroupControl.GroupRating > 0)
						{
							// Setting a Group's Rating above 0 should place the Skill in the Group and disable the SkillControl.
							if (objGroupControl.GroupRating > objSkillControl.SkillRatingMaximum)
								objSkillControl.SkillRatingMaximum = objGroupControl.GroupRating;
							objSkillControl.SkillRating = objGroupControl.GroupRating;
							objSkillControl.IsGrouped = true;
						}
						else
						{
							// Returning a Group's Rating back to 0 should release the Skill from the Group and re-enable the SkillControl.
							objSkillControl.SkillRating = 0;
							objSkillControl.IsGrouped = false;
						}
					}
				}
			}

			if (!_blnLoading)
			{
				// Refresh the list of shown Active Skills after the Group Rating changes since this may cause new Skills to become visible.
				if (cboSkillFilter.SelectedValue.ToString() != "0")
				{
					EventArgs e = new EventArgs();
					cboSkillFilter_SelectedIndexChanged(sender, e);
				}
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region ContactControl Events
		private void objContact_ConnectionRatingChanged(Object sender)
		{
			// Handle the ConnectionRatingChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objContact_ConnectionGroupRatingChanged(Object sender)
		{
			// Handle the ConnectionGroupRatingChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objContact_LoyaltyRatingChanged(Object sender)
		{
			// Handle the LoyaltyRatingChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objContact_DeleteContact(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteContact")))
				return;

			// Handle the DeleteContact Event for the ContactControl object.
			ContactControl objSender = (ContactControl)sender;
			bool blnFound = false;
			foreach (ContactControl objContactControl in panContacts.Controls)
			{
				// Set the flag to show that we have found the Contact.
				if (objContactControl == objSender)
					blnFound = true;

				// Once the Contact has been found, all of the other ContactControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
				{
					_objCharacter.Contacts.Remove(objContactControl.ContactObject);
					objContactControl.Top -= 25;
				}
			}
			// Remove the ContactControl that raised the Event.
			panContacts.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objContact_FileNameChanged(Object sender)
		{
			// Handle the FileNameChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region EnemyControl Events
		private void objEnemy_ConnectionRatingChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objEnemy_ConnectionGroupRatingChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objEnemy_LoyaltyRatingChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objEnemy_DeleteContact(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteEnemy")))
				return;

			// Determine the Karam cost to remove the Enemy.
			ContactControl objSender = (ContactControl)sender;
			int intKarmaCost = (objSender.ConnectionRating + objSender.LoyaltyRating + objSender.GroupRating) * _objOptions.KarmaQuality;

			bool blnKarmaExpense = ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseEnemy").Replace("{0}", intKarmaCost.ToString()));

			if (blnKarmaExpense)
			{
				// Make sure the character has enough Karma.
				if (intKarmaCost > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseRemoveEnemy") + " " + objSender.ContactName, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaCost;
			}

			// Handle the DeleteContact Event for the ContactControl object.
			bool blnFound = false;
			foreach (ContactControl objContactControl in panEnemies.Controls)
			{
				// Set the flag to show that we have found the contact.
				if (objContactControl == objSender)
					blnFound = true;

				// Once the Enemy has been found, all of the other ContactControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
				{
					_objCharacter.Contacts.Remove(objContactControl.ContactObject);
					objContactControl.Top -= 25;
				}
			}
			// Remove the ContactControl that raised the Event.
			panEnemies.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objEnemy_FileNameChanged(Object sender)
		{
			// Handle the FileNameChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region PetControl Events
		private void objPet_DeleteContact(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteContact")))
				return;

			// Handle the DeleteContact Event for the ContactControl object.
			PetControl objSender = (PetControl)sender;
			bool blnFound = false;
			foreach (PetControl objContactControl in panPets.Controls)
			{
				// Set the flag to show that we have found the Contact.
				if (objContactControl == objSender)
					blnFound = true;

				// Once the Contact has been found, all of the other ContactControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
					_objCharacter.Contacts.Remove(objContactControl.ContactObject);
			}
			// Remove the ContactControl that raised the Event.
			panPets.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objPet_FileNameChanged(Object sender)
		{
			// Handle the FileNameChanged Event for the ContactControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region SpiritControl Events
		private void objSpirit_ForceChanged(Object sender)
		{
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSpirit_BoundChanged(Object sender)
		{
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSpirit_ServicesOwedChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSpirit_DeleteSpirit(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteSpirit")))
				return;

			// Handle the DeleteSpirit Event for the SpiritControl object.
			SpiritControl objSender = (SpiritControl)sender;
			bool blnFound = false;
			foreach (SpiritControl objSpiritControl in panSpirits.Controls)
			{
				// Set the flag to show that we have found the Spirit.
				if (objSpiritControl == objSender)
					blnFound = true;

				// Once the Spirit has been found, all of the other SpiritControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
				{
					_objCharacter.Spirits.Remove(objSpiritControl.SpiritObject);
					objSpiritControl.Top -= 25;
				}
			}
			// Remove the SpiritControl that raised the Event.
			panSpirits.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSpirit_FileNameChanged(Object sender)
		{
			// Handle the FileNameChanged Event for the SpritControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region SpriteControl (SpiritControl) Events
		private void objSprite_ForceChanged(Object sender)
		{
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSprite_BoundChanged(Object sender)
		{
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSprite_ServicesOwedChanged(Object sender)
		{
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSprite_DeleteSpirit(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteSprite")))
				return;

			// Handle the DeleteSpirit Event for the SpiritControl object.
			SpiritControl objSender = (SpiritControl)sender;
			bool blnFound = false;
			foreach (SpiritControl objSpriteControl in panSprites.Controls)
			{
				// Set the flag to show that we have found the Sprite.
				if (objSpriteControl == objSender)
					blnFound = true;

				// Once the Spirit has been found, all of the other SpiritControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
				{
					_objCharacter.Spirits.Remove(objSpriteControl.SpiritObject);
					objSpriteControl.Top -= 25;
				}
			}
			// Remove the SpiritControl that raised the Event.
			panSprites.Controls.Remove(objSender);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objSprite_FileNameChanged(Object sender)
		{
			// Handle the FileNameChanged Event for the SpritControl object.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region PowerControl Events
		private void objPower_PowerRatingChanged(Object sender)
		{
			// Handle the PowerRatingChange Event for the PowerControl object.
			PowerControl objPowerControl = (PowerControl)sender;
			if (objPowerControl.PowerLevel > _objCharacter.MAG.TotalValue)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_PowerLevel"), LanguageManager.Instance.GetString("MessageTitle_PowerLevel"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				objPowerControl.PowerLevel = _objCharacter.MAG.TotalValue;
			}
			else
			{
				// If the Bonus contains "Rating", remove the existing Improvements and create new ones.
				if (objPowerControl.PowerObject.Bonus != null)
				{
					if (objPowerControl.PowerObject.Bonus.InnerXml.Contains("Rating"))
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Power, objPowerControl.PowerObject.InternalId);
						_objImprovementManager.ForcedValue = objPowerControl.Extra;
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Power, objPowerControl.PowerObject.InternalId, objPowerControl.PowerObject.Bonus, false, Convert.ToInt32(objPowerControl.PowerObject.Rating), objPowerControl.PowerObject.DisplayNameShort);
					}
				}
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void objPower_DeletePower(Object sender)
		{
			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeletePower")))
				return;

			// Handle the DeletePower Event for the PowerControl.
			PowerControl objSender = (PowerControl)sender;
			bool blnFound = false;
			foreach (PowerControl objPowerControl in panPowers.Controls)
			{
				// Set the flag to show that we have found the Power.
				if (objPowerControl == objSender)
					blnFound = true;

				// Once the Power has been found, all of the other PowerControls on the Panel should move up 25 pixels to fill in the gap that deleting this one will cause.
				if (blnFound)
					objPowerControl.Top -= 25;
			}

			// Remove the Improvements that were created by the Power.
			_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Power, objSender.PowerObject.InternalId);

			// Remove the Power.
			_objCharacter.Powers.Remove(objSender.PowerObject);

			// Update the Attribute label.
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			// Remove the PowerControl that raised the Event.
			panPowers.Controls.Remove(objSender);
			CalculatePowerPoints();
		}
		#endregion

		#region Martial Tab Control Events
		private void treMartialArts_AfterSelect(object sender, TreeViewEventArgs e)
		{
			try
			{
				// The Rating NUD is only enabled if a Martial Art is currently selected.
				if (treMartialArts.SelectedNode.Level == 1 && treMartialArts.SelectedNode.Parent == treMartialArts.Nodes[0])
				{
					MartialArt objMartialArt = _objFunctions.FindMartialArt(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts);

					_blnSkipRefresh = true;
					if (objMartialArt.Rating < 4)
					{
						string strTip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (objMartialArt.Rating + 1).ToString()).Replace("{1}", (((objMartialArt.Rating + 1) * 5 * _objOptions.KarmaQuality) - ((objMartialArt.Rating) * 5 * _objOptions.KarmaQuality)).ToString());
						tipTooltip.SetToolTip(cmdImproveMartialArtsRating, strTip);
						cmdImproveMartialArtsRating.Enabled = true;
					}
					else
						cmdImproveMartialArtsRating.Enabled = false;
					lblMartialArtsRating.Text = objMartialArt.Rating.ToString();
					string strBook = _objOptions.LanguageBookShort(objMartialArt.Source);
					string strPage = objMartialArt.Page;
					lblMartialArtSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblMartialArtSource, _objOptions.LanguageBookLong(objMartialArt.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objMartialArt.Page);
					_blnSkipRefresh = false;
				}
				else
					cmdImproveMartialArtsRating.Enabled = false;

				// Display the Martial Art Advantage information.
				if (treMartialArts.SelectedNode.Level == 2)
				{
					MartialArt objMartialArt = new MartialArt(_objCharacter);
					MartialArtAdvantage objAdvantage = _objFunctions.FindMartialArtAdvantage(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts, out objMartialArt);

					string strBook = _objOptions.LanguageBookShort(objMartialArt.Source);
					string strPage = objMartialArt.Page;
					lblMartialArtSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblMartialArtSource, _objOptions.LanguageBookLong(objMartialArt.Source) + " page " + objMartialArt.Page);
				}

				// Display the Maneuver information.
				if (treMartialArts.SelectedNode.Level == 1 && treMartialArts.SelectedNode.Parent == treMartialArts.Nodes[1])
				{
					MartialArtManeuver objManeuver = _objFunctions.FindMartialArtManeuver(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArtManeuvers);

					string strBook = _objOptions.LanguageBookShort(objManeuver.Source);
					string strPage = objManeuver.Page;
					lblMartialArtSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblMartialArtSource, _objOptions.LanguageBookLong(objManeuver.Source) + " page " + objManeuver.Page);
				}
			}
			catch
			{
				cmdImproveMartialArtsRating.Enabled = false;
			}
		}

		private void cmdImproveMartialArtsRating_Click(object sender, EventArgs e)
		{
			// Locate the selected Martial Art.
			MartialArt objMartialArt = _objFunctions.FindMartialArt(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts);

			// Make sure the character has enough Karma.
			int intKarmaCost = ((objMartialArt.Rating + 1) * 5 * _objOptions.KarmaQuality) - ((objMartialArt.Rating) * 5 * _objOptions.KarmaQuality);

			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", objMartialArt.DisplayNameShort).Replace("{1}", (objMartialArt.Rating + 1).ToString()).Replace("{2}", intKarmaCost.ToString())))
				return;

			// Create the Expense Log Entry.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseMartialArt") + " " + objMartialArt.DisplayNameShort + " " + objMartialArt.Rating + " -> " + (objMartialArt.Rating + 1).ToString(), ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ImproveMartialArt, objMartialArt.Name);
			objExpense.Undo = objUndo;

			objMartialArt.Rating += 1;
			lblMartialArtsRating.Text = objMartialArt.Rating.ToString();

			if (objMartialArt.Rating < 4)
			{
				string strTip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (objMartialArt.Rating + 1).ToString()).Replace("{1}", (((objMartialArt.Rating + 1) * 5 * _objOptions.KarmaQuality) - ((objMartialArt.Rating) * 5 * _objOptions.KarmaQuality)).ToString());
				tipTooltip.SetToolTip(cmdImproveMartialArtsRating, strTip);
				cmdImproveMartialArtsRating.Enabled = true;
			}
			else
				cmdImproveMartialArtsRating.Enabled = false;
			
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Button Events
		private void cmdAddKnowledgeSkill_Click(object sender, EventArgs e)
		{
			int i = panKnowledgeSkills.Controls.Count;
			Skill objSkill = new Skill(_objCharacter);
			objSkill.Attribute = "LOG";
			objSkill.SkillCategory = "Academic";
			if (_objCharacter.MaxSkillRating > 0)
				objSkill.RatingMaximum = _objCharacter.MaxSkillRating;

			SkillControl objSkillControl = new SkillControl();
			objSkillControl.SkillObject = objSkill;

			// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
			objSkillControl.RatingChanged += objKnowledgeSkill_RatingChanged;
			objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
			objSkillControl.SpecializationLeave += objSkill_SpecializationLeave;
			objSkillControl.DeleteSkill += objKnowledgeSkill_DeleteSkill;
			objSkillControl.SkillKarmaClicked += objKnowledgeSkill_KarmaClicked;
			objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

			objSkillControl.KnowledgeSkill = true;
			objSkillControl.AllowDelete = true;
			objSkillControl.SkillRatingMaximum = 6;
			// Set the SkillControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
			objSkillControl.Location = new Point(0, objSkillControl.Height * i + panKnowledgeSkills.AutoScrollPosition.Y);
			panKnowledgeSkills.Controls.Add(objSkillControl);

			_objCharacter.Skills.Add(objSkill);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddContact_Click(object sender, EventArgs e)
		{
			Contact objContact = new Contact(_objCharacter);
			_objCharacter.Contacts.Add(objContact);

			int i = panContacts.Controls.Count;
			ContactControl objContactControl = new ContactControl();
			objContactControl.ContactObject = objContact;
			objContactControl.EntityType = ContactType.Contact;

			// Attach an EventHandler for the ConnectionRatingChanged, LoyaltyRatingChanged, DeleteContact, and FileNameChanged Events.
			objContactControl.ConnectionRatingChanged += objContact_ConnectionRatingChanged;
			objContactControl.ConnectionGroupRatingChanged += objContact_ConnectionGroupRatingChanged;
			objContactControl.LoyaltyRatingChanged += objContact_LoyaltyRatingChanged;
			objContactControl.DeleteContact += objContact_DeleteContact;
			objContactControl.FileNameChanged += objContact_FileNameChanged;

			// Set the ContactControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
			objContactControl.Location = new Point(0, objContactControl.Height * i + panContacts.AutoScrollPosition.Y);
			panContacts.Controls.Add(objContactControl);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddEnemy_Click(object sender, EventArgs e)
		{
			// Handle the ConnectionRatingChanged Event for the ContactControl object.
			Contact objContact = new Contact(_objCharacter);
			_objCharacter.Contacts.Add(objContact);

			int i = panEnemies.Controls.Count;
			ContactControl objContactControl = new ContactControl();
			objContactControl.ContactObject = objContact;
			objContactControl.EntityType = ContactType.Enemy;

			// Attach an EventHandler for the ConnectioNRatingChanged, LoyaltyRatingChanged, DeleteContact, and FileNameChanged Events.
			objContactControl.ConnectionRatingChanged += objEnemy_ConnectionRatingChanged;
			objContactControl.ConnectionGroupRatingChanged += objEnemy_ConnectionGroupRatingChanged;
			objContactControl.LoyaltyRatingChanged += objEnemy_LoyaltyRatingChanged;
			objContactControl.DeleteContact += objEnemy_DeleteContact;
			objContactControl.FileNameChanged += objEnemy_FileNameChanged;

			// Set the ContactControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
			objContactControl.Location = new Point(0, objContactControl.Height * i + panEnemies.AutoScrollPosition.Y);
			panEnemies.Controls.Add(objContactControl);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddSpell_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma before letting them select a Spell.
			if (_objCharacter.Karma < _objOptions.KarmaSpell)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Run through the list of Active Skills and pick out the two applicable ones.
			int intSkillValue = 0;
			foreach (SkillControl objSkillControl in panActiveSkills.Controls)
			{
				if ((objSkillControl.SkillName == "Spellcasting" || objSkillControl.SkillName == "Ritual Spellcasting") && objSkillControl.SkillRating > intSkillValue)
					intSkillValue = objSkillControl.SkillRating;
			}

			frmSelectSpell frmPickSpell = new frmSelectSpell(_objCharacter);
			frmPickSpell.ShowDialog(this);
			// Make sure the dialogue window was not canceled.
			if (frmPickSpell.DialogResult == DialogResult.Cancel)
				return;

			// Open the Spells XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("spells.xml");

			XmlNode objXmlSpell = objXmlDocument.SelectSingleNode("/chummer/spells/spell[name = \"" + frmPickSpell.SelectedSpell + "\"]");

			Spell objSpell = new Spell(_objCharacter);
			TreeNode objNode = new TreeNode();
			objSpell.Create(objXmlSpell, _objCharacter, objNode, "", frmPickSpell.Limited, frmPickSpell.Extended);
			objNode.ContextMenuStrip = cmsSpell;
			if (objSpell.InternalId == Guid.Empty.ToString())
				return;

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpend").Replace("{0}", objSpell.DisplayName).Replace("{1}", _objOptions.KarmaSpell.ToString())))
				return;

			_objCharacter.Spells.Add(objSpell);

			switch (objSpell.Category)
			{
				case "Combat":
					treSpells.Nodes[0].Nodes.Add(objNode);
					treSpells.Nodes[0].Expand();
					break;
				case "Detection":
					treSpells.Nodes[1].Nodes.Add(objNode);
					treSpells.Nodes[1].Expand();
					break;
				case "Health":
					treSpells.Nodes[2].Nodes.Add(objNode);
					treSpells.Nodes[2].Expand();
					break;
				case "Illusion":
					treSpells.Nodes[3].Nodes.Add(objNode);
					treSpells.Nodes[3].Expand();
					break;
				case "Manipulation":
					treSpells.Nodes[4].Nodes.Add(objNode);
					treSpells.Nodes[4].Expand();
					break;
				case "Geomancy Ritual":
					treSpells.Nodes[5].Nodes.Add(objNode);
					treSpells.Nodes[5].Expand();
					break;
			}

			treSpells.SelectedNode = objNode;

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(_objOptions.KarmaSpell * -1, LanguageManager.Instance.GetString("String_ExpenseLearnSpell") + " " + objSpell.Name, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objEntry);
			_objCharacter.Karma -= _objOptions.KarmaSpell;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.AddSpell, objSpell.InternalId);
			objEntry.Undo = objUndo;

			_objFunctions.SortTree(treSpells);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickSpell.AddAgain)
				cmdAddSpell_Click(sender, e);
		}

		private void cmdDeleteSpell_Click(object sender, EventArgs e)
		{
			// Delete the selected Spell.
			try
			{
				if (treSpells.SelectedNode.Level > 0)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteSpell")))
						return;

					// Locate the Spell that is selected in the tree.
					Spell objSpell = _objFunctions.FindSpell(treSpells.SelectedNode.Tag.ToString(), _objCharacter.Spells);

					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Spell, objSpell.InternalId);

					_objCharacter.Spells.Remove(objSpell);
					treSpells.SelectedNode.Remove();
				}
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdAddSpirit_Click(object sender, EventArgs e)
		{
			int i = panSpirits.Controls.Count;

			Spirit objSpirit = new Spirit(_objCharacter);
			_objCharacter.Spirits.Add(objSpirit);

			SpiritControl objSpiritControl = new SpiritControl(true);
			objSpiritControl.SpiritObject = objSpirit;
			objSpiritControl.EntityType = SpiritType.Spirit;

			// Attach an EventHandler for the ServicesOwedChanged Event.
			objSpiritControl.ServicesOwedChanged += objSpirit_ServicesOwedChanged;
			objSpiritControl.ForceChanged += objSpirit_ForceChanged;
			objSpiritControl.BoundChanged += objSpirit_BoundChanged;
			objSpiritControl.DeleteSpirit += objSpirit_DeleteSpirit;
			objSpiritControl.FileNameChanged += objSpirit_FileNameChanged;

			int intMAG = Convert.ToInt32(_objCharacter.MAG.TotalValue);
			if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
			{
				intMAG = _objCharacter.MAGMagician;
			}
			if (_objOptions.SpiritForceBasedOnTotalMAG)
			{
				objSpiritControl.ForceMaximum = _objCharacter.MAG.TotalValue * 2;
				objSpiritControl.Force = _objCharacter.MAG.TotalValue;
			}
			else
			{
				if (intMAG == 0)
					intMAG = 1;
				objSpiritControl.ForceMaximum = intMAG * 2;
				objSpiritControl.Force = intMAG;
			}
			objSpiritControl.RebuildSpiritList(_objCharacter.MagicTradition);

			objSpiritControl.Top = i * objSpiritControl.Height;
			panSpirits.Controls.Add(objSpiritControl);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddSprite_Click(object sender, EventArgs e)
		{
			int i = panSprites.Controls.Count;

			Spirit objSprite = new Spirit(_objCharacter);
			_objCharacter.Spirits.Add(objSprite);

			SpiritControl objSpriteControl = new SpiritControl(true);
			objSpriteControl.SpiritObject = objSprite;
			objSpriteControl.EntityType = SpiritType.Sprite;

			// Attach an EventHandler for the ServicesOwedChanged Event.
			objSpriteControl.ServicesOwedChanged += objSprite_ServicesOwedChanged;
			objSpriteControl.ForceChanged += objSprite_ForceChanged;
			objSpriteControl.BoundChanged += objSprite_BoundChanged;
			objSpriteControl.DeleteSpirit += objSprite_DeleteSpirit;
			objSpriteControl.FileNameChanged += objSprite_FileNameChanged;

			objSpriteControl.ForceMaximum = _objCharacter.RES.TotalValue * 2;
			objSpriteControl.Force = Convert.ToInt32(_objCharacter.RES.Value);
			objSpriteControl.RebuildSpiritList(_objCharacter.TechnomancerStream);

			objSpriteControl.Top = i * objSpriteControl.Height;
			panSprites.Controls.Add(objSpriteControl);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddPower_Click(object sender, EventArgs e)
		{
			frmSelectPower frmPickPower = new frmSelectPower(_objCharacter);
			frmPickPower.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickPower.DialogResult == DialogResult.Cancel)
				return;

			int i = panPowers.Controls.Count;

			Power objPower = new Power(_objCharacter);
			_objCharacter.Powers.Add(objPower);

			PowerControl objPowerControl = new PowerControl();
			objPowerControl.PowerObject = objPower;

			// Attach an EventHandler for the PowerRatingChanged Event.
			objPowerControl.PowerRatingChanged += objPower_PowerRatingChanged;
			objPowerControl.DeletePower += objPower_DeletePower;

			objPowerControl.PowerName = frmPickPower.SelectedPower;
			objPowerControl.PointsPerLevel = frmPickPower.PointsPerLevel;
			objPowerControl.LevelEnabled = frmPickPower.LevelEnabled;
			if (frmPickPower.MaxLevels() > 0)
				objPowerControl.MaxLevels = frmPickPower.MaxLevels();

			// Open the Cyberware XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("powers.xml");

			XmlNode objXmlPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + frmPickPower.SelectedPower + "\"]");

			objPower.Source = objXmlPower["source"].InnerText;
			objPower.Page = objXmlPower["page"].InnerText;
			if (objXmlPower["doublecost"] != null)
				objPower.DoubleCost = false;

			if (objXmlPower.InnerXml.Contains("bonus"))
			{
				objPower.Bonus = objXmlPower["bonus"];
				if (!_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Power, objPower.InternalId, objPower.Bonus, false, Convert.ToInt32(objPower.Rating), objPower.DisplayNameShort))
				{
					_objCharacter.Powers.Remove(objPower);
					return;
				}
				objPowerControl.Extra = _objImprovementManager.SelectedValue;
			}

			// Set the control's Maximum.
			objPowerControl.RefreshMaximum(_objCharacter.MAG.TotalValue);
			objPowerControl.Top = i * objPowerControl.Height;
			objPowerControl.RefreshTooltip();
			panPowers.Controls.Add(objPowerControl);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickPower.AddAgain)
				cmdAddPower_Click(sender, e);
		}

		private void cmdAddCyberware_Click(object sender, EventArgs e)
		{
			// Select the root Cyberware node then open the Select Cyberware window.
			treCyberware.SelectedNode = treCyberware.Nodes[0];
			bool blnAddAgain = PickCyberware();
			if (blnAddAgain)
				cmdAddCyberware_Click(sender, e);
		}

		private void cmdDeleteCyberware_Click(object sender, EventArgs e)
		{
			try
			{
				if (treCyberware.SelectedNode.Level > 0)
				{
					Cyberware objCyberware = new Cyberware(_objCharacter);
					Cyberware objParent = new Cyberware(_objCharacter);
					bool blnFound = false;
					// Locate the piece of Cyberware that is selected in the tree.
					objCyberware = _objFunctions.FindCyberware(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware);
					if (objCyberware != null)
					{
						blnFound = true;
						objParent = objCyberware.Parent;
					}

					if (blnFound)
					{
						if (objCyberware.Capacity == "[*]" && treCyberware.SelectedNode.Level == 2)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveCyberware"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}

						if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
						{
							if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteCyberware")))
								return;
						}
						if (objCyberware.SourceType == Improvement.ImprovementSource.Bioware)
						{
							if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteBioware")))
								return;
						}

						// Run through the Cyberware's child elements and remove any Improvements and Cyberweapons.
						foreach (Cyberware objChildCyberware in objCyberware.Children)
						{
							_objImprovementManager.RemoveImprovements(objCyberware.SourceType, objChildCyberware.InternalId);
							if (objChildCyberware.WeaponID != Guid.Empty.ToString())
							{
								// Remove the Weapon from the TreeView.
								TreeNode objRemoveNode = new TreeNode();
								foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
								{
									if (objWeaponNode.Tag.ToString() == objChildCyberware.WeaponID)
										objRemoveNode = objWeaponNode;
								}
								treWeapons.Nodes.Remove(objRemoveNode);

								// Remove the Weapon from the Character.
								Weapon objRemoveWeapon = new Weapon(_objCharacter);
								foreach (Weapon objWeapon in _objCharacter.Weapons)
								{
									if (objWeapon.InternalId == objChildCyberware.WeaponID)
										objRemoveWeapon = objWeapon;
								}
								_objCharacter.Weapons.Remove(objRemoveWeapon);
							}
						}
						// Remove the Children.
						objCyberware.Children.Clear();

						// Remove the Cyberweapon created by the Cyberware if applicable.
						if (objCyberware.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objCyberware.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objCyberware.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);

							// Remove any Gear attached to the Cyberware.
							foreach (Gear objGear in objCyberware.Gear)
								_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
						}

						// Remove any Gear attached to the Cyberware.
						foreach (Gear objGear in objCyberware.Gear)
							_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);

						// Remove any Improvements created by the piece of Cyberware.
						_objImprovementManager.RemoveImprovements(objCyberware.SourceType, objCyberware.InternalId);
						_objCharacter.Cyberware.Remove(objCyberware);
					}
					else
					{
						// Find and remove the selected piece of Gear.
						Gear objGear = _objFunctions.FindCyberwareGear(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware, out objCyberware);
						if (objGear.Parent == null)
							objCyberware.Gear.Remove(objGear);
						else
							objGear.Parent.Children.Remove(objGear);
						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					}

					// Remove the item from the TreeView.
					treCyberware.Nodes.Remove(treCyberware.SelectedNode);

					// If the Parent is populated, remove the item from its Parent.
					if (objParent != null)
						objParent.Children.Remove(objCyberware);
				}
				RefreshSelectedCyberware();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
				return;
			}

			UpdateCharacterInfo();
		}

		private void cmdAddComplexForm_Click(object sender, EventArgs e)
		{
			// Let the user select a Program.
			frmSelectProgram frmPickProgram = new frmSelectProgram(_objCharacter);
			frmPickProgram.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickProgram.DialogResult == DialogResult.Cancel)
				return;

			int intKarmaCost = _objOptions.KarmaNewComplexForm;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("complexforms.xml");

            XmlNode objXmlProgram = objXmlDocument.SelectSingleNode("/chummer/complexforms/complexform[name = \"" + frmPickProgram.SelectedProgram + "\"]");

			TreeNode objNode = new TreeNode();
            ComplexForm objProgram = new ComplexForm(_objCharacter);
			objProgram.Create(objXmlProgram, _objCharacter, objNode);
			if (objProgram.InternalId == Guid.Empty.ToString())
				return;

            _objCharacter.ComplexForms.Add(objProgram);

			// If using the optional rule for costing the same as Spells, change the Karma cost.
			if (_objOptions.AlternateComplexFormCost)
				intKarmaCost = _objOptions.KarmaSpell;

			// Make sure the character has enough Karma before letting them select a Complex Form.
			if (_objCharacter.Karma < intKarmaCost)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				// Remove the Improvements created by the Complex Form.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ComplexForm, objProgram.InternalId);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpend").Replace("{0}", objProgram.DisplayNameShort).Replace("{1}", intKarmaCost.ToString())))
			{
				// Remove the Improvements created by the Complex Form.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ComplexForm, objProgram.InternalId);
				return;
			}

			treComplexForms.Nodes[0].Nodes.Add(objNode);
			treComplexForms.Nodes[0].Expand();

			// Create the Expense Log Entry.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseLearnComplexForm") + " " + objProgram.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.AddComplexForm, objProgram.InternalId);
			objExpense.Undo = objUndo;

			_objFunctions.SortTree(treComplexForms);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickProgram.AddAgain)
				cmdAddComplexForm_Click(sender, e);
		}

		private void cmdAddArmor_Click(object sender, EventArgs e)
		{
			frmSelectArmor frmPickArmor = new frmSelectArmor(_objCharacter, true);
			frmPickArmor.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickArmor.DialogResult == DialogResult.Cancel)
				return;

			// Open the Armor XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("armor.xml");

			XmlNode objXmlArmor = objXmlDocument.SelectSingleNode("/chummer/armors/armor[name = \"" + frmPickArmor.SelectedArmor + "\"]");

			TreeNode objNode = new TreeNode();
			Armor objArmor = new Armor(_objCharacter);
			objArmor.Create(objXmlArmor, objNode, cmsArmorMod);
			if (objArmor.InternalId == Guid.Empty.ToString())
				return;

			int intCost = objArmor.TotalCost;
			// Apply a markup if applicable.
			if (frmPickArmor.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickArmor.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objArmor.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objArmor.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickArmor.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					// Remove the Improvements created by the Armor.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);
					if (frmPickArmor.AddAgain)
						cmdAddArmor_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseArmor") + " " + objArmor.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddArmor, objArmor.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			_objCharacter.Armor.Add(objArmor);

			objNode.ContextMenuStrip = cmsArmor;
			treArmor.Nodes[0].Nodes.Add(objNode);
			treArmor.Nodes[0].Expand();
			treArmor.SelectedNode = objNode;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickArmor.AddAgain)
				cmdAddArmor_Click(sender, e);
		}

		private void cmdDeleteArmor_Click(object sender, EventArgs e)
		{
			// Delete the selected piece of Armor.
			try
			{
				if (treArmor.SelectedNode.Level == 0)
				{
					if (treArmor.SelectedNode.Text == LanguageManager.Instance.GetString("Node_SelectedArmor"))
						return;

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteArmorLocation")))
						return;

					// Move all of the child nodes in the current parent to the Selected Armor parent node.
					foreach (TreeNode objNode in treArmor.SelectedNode.Nodes)
					{
						Armor objArmor = _objFunctions.FindArmor(objNode.Tag.ToString(), _objCharacter.Armor);

						// Change the Location for the Armor.
						objArmor.Location = "";

						TreeNode nodNewNode = new TreeNode();
						nodNewNode.Text = objNode.Text;
						nodNewNode.Tag = objNode.Tag;
						nodNewNode.ContextMenuStrip = cmsArmor;

						// Add child nodes.
						foreach (ArmorMod objChild in objArmor.ArmorMods)
						{
							TreeNode nodChildNode = new TreeNode();
							nodChildNode.Text = objChild.DisplayName;
							nodChildNode.Tag = objChild.InternalId;
							nodChildNode.ContextMenuStrip = cmsArmorMod;
							nodNewNode.Nodes.Add(nodChildNode);
							nodNewNode.Expand();
						}

						foreach (Gear objChild in objArmor.Gear)
						{
							TreeNode nodChildNode = new TreeNode();
							nodChildNode.Text = objChild.DisplayName;
							nodChildNode.Tag = objChild.InternalId;
							nodChildNode.ContextMenuStrip = cmsArmorGear;
							nodNewNode.Nodes.Add(nodChildNode);
							nodNewNode.Expand();
						}

						treArmor.Nodes[0].Nodes.Add(nodNewNode);
						treArmor.Nodes[0].Expand();
					}

					// Remove the Location from the character, then remove the selected node.
					_objCharacter.ArmorBundles.Remove(treArmor.SelectedNode.Text);
					treArmor.SelectedNode.Remove();
					return;
				}

				if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteArmor")))
					return;

				if (treArmor.SelectedNode.Level == 1)
				{
					Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
					if (objArmor == null)
						return;

					// Remove any Improvements created by the Armor and its children.
					foreach (ArmorMod objMod in objArmor.ArmorMods)
					{
						// Remove the Cyberweapon created by the Mod if applicable.
						if (objMod.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objMod.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objMod.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);
						}

						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
					}
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);

					// Remove any Improvements created by the Armor's Gear.
					foreach (Gear objGear in objArmor.Gear)
						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);

					_objCharacter.Armor.Remove(objArmor);
					treArmor.SelectedNode.Remove();
				}
				else if (treArmor.SelectedNode.Level == 2)
				{
					bool blnIsMod = false;
					ArmorMod objMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
					if (objMod != null)
						blnIsMod = true;

					if (blnIsMod)
					{
						// Remove the Cyberweapon created by the Mod if applicable.
						if (objMod.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objMod.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objMod.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);
						}

						// Remove any Improvements created by the ArmorMod.
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
						objMod.Parent.ArmorMods.Remove(objMod);
					}
					else
					{
						Armor objSelectedArmor = new Armor(_objCharacter);
						Gear objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objSelectedArmor);
						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
						objSelectedArmor.Gear.Remove(objGear);
					}
					treArmor.SelectedNode.Remove();
				}
				else if (treArmor.SelectedNode.Level > 2)
				{
					Armor objSelectedArmor = new Armor(_objCharacter);
					Gear objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objSelectedArmor);
					objGear.Parent.Children.Remove(objGear);
					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					objSelectedArmor.Gear.Remove(objGear);
					treArmor.SelectedNode.Remove();
				}
				UpdateCharacterInfo();
				RefreshSelectedArmor();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdAddBioware_Click(object sender, EventArgs e)
		{
			// Select the root Bioware node then open the Select Cyberware window.
			treCyberware.SelectedNode = treCyberware.Nodes[1];
			bool blnAddAgain = PickCyberware(Improvement.ImprovementSource.Bioware);
			if (blnAddAgain)
				cmdAddBioware_Click(sender, e);
		}

		private void cmdAddWeapon_Click(object sender, EventArgs e)
		{
			frmSelectWeapon frmPickWeapon = new frmSelectWeapon(_objCharacter, true);
			frmPickWeapon.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Open the Weapons XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + frmPickWeapon.SelectedWeapon + "\"]");

			TreeNode objNode = new TreeNode();
			Weapon objWeapon = new Weapon(_objCharacter);
			objWeapon.Create(objXmlWeapon, _objCharacter, objNode, cmsWeapon, cmsWeaponAccessory, cmsWeaponMod);

			int intCost = objWeapon.TotalCost;
			// Apply a markup if applicable.
			if (frmPickWeapon.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeapon.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickWeapon.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeapon.AddAgain)
						cmdAddWeapon_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeapon, objWeapon.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			_objCharacter.Weapons.Add(objWeapon);

			objNode.ContextMenuStrip = cmsWeapon;
			treWeapons.Nodes[0].Nodes.Add(objNode);
			treWeapons.Nodes[0].Expand();
			treWeapons.SelectedNode = objNode;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickWeapon.AddAgain)
				cmdAddWeapon_Click(sender, e);
		}

		private void cmdDeleteWeapon_Click(object sender, EventArgs e)
		{
			// Delete the selected Weapon.
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
				{
					if (treWeapons.SelectedNode.Text == LanguageManager.Instance.GetString("Node_SelectedWeapons"))
						return;

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteWeaponLocation")))
						return;

					// Move all of the child nodes in the current parent to the Selected Weapons parent node.
					foreach (TreeNode objNode in treWeapons.SelectedNode.Nodes)
					{
						Weapon objWeapon = new Weapon(_objCharacter);
						objWeapon = _objFunctions.FindWeapon(objNode.Tag.ToString(), _objCharacter.Weapons);

						// Change the Location for the Weapon.
						objWeapon.Location = "";
					}

					List<TreeNode> lstMoveNodes = new List<TreeNode>();
					foreach (TreeNode objNode in treWeapons.SelectedNode.Nodes)
						lstMoveNodes.Add(objNode);

					foreach (TreeNode objNode in lstMoveNodes)
					{
						treWeapons.SelectedNode.Nodes.Remove(objNode);
						treWeapons.Nodes[0].Nodes.Add(objNode);
					}

					// Remove the Weapon Location from the character, then remove the selected node.
					_objCharacter.WeaponLocations.Remove(treWeapons.SelectedNode.Text);
					treWeapons.SelectedNode.Remove();
				}

				if (treWeapons.SelectedNode.Level > 0)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteWeapon")))
						return;

					if (treWeapons.SelectedNode.Level == 1)
					{
						// Locate the Weapon that is selected in the tree.
						Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

						// Cyberweapons cannot be removed through here and must be done by removing the piece of Cyberware.
						if (objWeapon.Category.StartsWith("Cyberware"))
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveCyberweapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveCyberweapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
						if (objWeapon.Category == "Gear")
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveGearWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveGearWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
						if (objWeapon.Category.StartsWith("Quality"))
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveQualityWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveQualityWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}

						foreach (WeaponAccessory objDelAccessory in objWeapon.WeaponAccessories)
						{
							foreach (Gear objGear in objDelAccessory.Gear)
								_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
						}
						if (objWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
							{
								foreach (WeaponAccessory objDelAccessory in objUnderbarrelWeapon.WeaponAccessories)
								{
									foreach (Gear objGear in objDelAccessory.Gear)
										_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
								}
							}
						}

						_objCharacter.Weapons.Remove(objWeapon);
						treWeapons.SelectedNode.Remove();
					}
					else
					{
						bool blnAccessory = false;
						// Locate the selected Underbarrel Weapon if applicable.
						foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
						{
							if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
							{
								foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
								{
									if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
									{
										objCharacterWeapon.UnderbarrelWeapons.Remove(objUnderbarrelWeapon);
										treWeapons.SelectedNode.Remove();
										return;
									}
								}
							}
						}

						Weapon objWeapon = new Weapon(_objCharacter);
						// Locate the Weapon that is selected in the tree.
						foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
						{
							if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Parent.Tag.ToString())
							{
								objWeapon = objCharacterWeapon;
								break;
							}
							if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
							{
								foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
								{
									if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Parent.Tag.ToString())
									{
										objWeapon = objUnderbarrelWeapon;
										break;
									}
								}
							}
						}

						// Locate the Accessory that is selected in the tree.
						WeaponAccessory objAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
						if (objAccessory != null)
						{
							foreach (Gear objGear in objAccessory.Gear)
								_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
							objWeapon.WeaponAccessories.Remove(objAccessory);
							treWeapons.SelectedNode.Remove();
							blnAccessory = true;
						}

						if (!blnAccessory)
						{
							// Locate the Mod that is selected in the tree.
							bool blnMod = false;
							WeaponMod objMod = _objFunctions.FindWeaponMod(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
							if (objMod != null)
								blnMod = true;

							if (blnMod)
							{
								objWeapon.WeaponMods.Remove(objMod);
								treWeapons.SelectedNode.Remove();
							}
							else
							{
								// Find the selected Gear.
								Gear objGear = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objAccessory);
								_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
								if (objGear.Parent == null)
									objAccessory.Gear.Remove(objGear);
								else
									objGear.Parent.Children.Remove(objGear);
								treWeapons.SelectedNode.Remove();
							}
						}
					}
				}
				UpdateCharacterInfo();
				RefreshSelectedWeapon();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdAddLifestyle_Click(object sender, EventArgs e)
		{
			Lifestyle objLifestyle = new Lifestyle(_objCharacter);
			frmSelectLifestyle frmPickLifestyle = new frmSelectLifestyle(objLifestyle, _objCharacter);
			frmPickLifestyle.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
				return;

			objLifestyle.Months = 0;
			_objCharacter.Lifestyles.Add(objLifestyle);

			TreeNode objNode = new TreeNode();
			objNode.Text = objLifestyle.DisplayName;
			objNode.Tag = objLifestyle.InternalId;
			objNode.ContextMenuStrip = cmsLifestyleNotes;
			treLifestyles.Nodes[0].Nodes.Add(objNode);
			treLifestyles.Nodes[0].Expand();
			treLifestyles.SelectedNode = objNode;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickLifestyle.AddAgain)
				cmdAddLifestyle_Click(sender, e);
		}

		private void cmdDeleteLifestyle_Click(object sender, EventArgs e)
		{
			// Delete the selected Lifestyle.
			try
			{
				if (treLifestyles.SelectedNode.Level > 0)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteLifestyle")))
						return;

					Lifestyle objLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);
					if (objLifestyle == null)
						return;

					_objCharacter.Lifestyles.Remove(objLifestyle);
					treLifestyles.SelectedNode.Remove();
				}
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdAddGear_Click(object sender, EventArgs e)
		{
			// Select the root Gear node then open the Select Gear window.
			treGear.SelectedNode = treGear.Nodes[0];
			bool blnAddAgain = PickGear();
			if (blnAddAgain)
				cmdAddGear_Click(sender, e);
			_objController.PopulateFocusList(treFoci);
		}

		private void cmdDeleteGear_Click(object sender, EventArgs e)
		{
			// Delete the selected Gear.
			try
			{
				if (treGear.SelectedNode.Level == 0)
				{
					if (treGear.SelectedNode.Text == LanguageManager.Instance.GetString("Node_SelectedGear"))
						return;

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteGearLocation")))
						return;

					// Move all of the child nodes in the current parent to the Selected Gear parent node.
					foreach (TreeNode objNode in treGear.SelectedNode.Nodes)
					{
						Gear objGear = new Gear(_objCharacter);
						objGear = _objFunctions.FindGear(objNode.Tag.ToString(), _objCharacter.Gear);

						// Change the Location for the Gear.
						objGear.Location = "";
					}

					List<TreeNode> lstMoveNodes = new List<TreeNode>();
					foreach (TreeNode objNode in treGear.SelectedNode.Nodes)
						lstMoveNodes.Add(objNode);

					foreach (TreeNode objNode in lstMoveNodes)
					{
						treGear.SelectedNode.Nodes.Remove(objNode);
						treGear.Nodes[0].Nodes.Add(objNode);
					}

					// Remove the Location from the character, then remove the selected node.
					_objCharacter.Locations.Remove(treGear.SelectedNode.Text);
					treGear.SelectedNode.Remove();
				}
				if (treGear.SelectedNode.Level > 0)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteGear")))
						return;

					Gear objGear = new Gear(_objCharacter);
					objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
					Gear objParent = new Gear(_objCharacter);
					objParent = _objFunctions.FindGear(treGear.SelectedNode.Parent.Tag.ToString(), _objCharacter.Gear);

					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);

					_objCharacter.Gear.Remove(objGear);
					treGear.SelectedNode.Remove();

					// If the Parent is populated, remove the item from its Parent.
					if (objParent != null)
						objParent.Children.Remove(objGear);
				}
				_objController.PopulateFocusList(treFoci);
				UpdateCharacterInfo();
				RefreshSelectedGear();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdAddVehicle_Click(object sender, EventArgs e)
		{
			frmSelectVehicle frmPickVehicle = new frmSelectVehicle(_objCharacter, true);
			frmPickVehicle.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickVehicle.DialogResult == DialogResult.Cancel)
				return;

			// Open the Vehicles XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");

			XmlNode objXmlVehicle = objXmlDocument.SelectSingleNode("/chummer/vehicles/vehicle[name = \"" + frmPickVehicle.SelectedVehicle + "\"]");

			TreeNode objNode = new TreeNode();
			Vehicle objVehicle = new Vehicle(_objCharacter);
			objVehicle.Create(objXmlVehicle, objNode, cmsVehicle, cmsVehicleGear, cmsVehicleWeapon, cmsVehicleWeaponAccessory, cmsVehicleWeaponMod);
			// Update the Used Vehicle information if applicable.
			if (frmPickVehicle.UsedVehicle)
			{
				objVehicle.Avail = frmPickVehicle.UsedAvail;
				objVehicle.Cost = frmPickVehicle.UsedCost.ToString();
			}

			int intCost = objVehicle.TotalCost;
			// Apply a markup if applicable.
			if (frmPickVehicle.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickVehicle.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objVehicle.CalculatedAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objVehicle.CalculatedAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickVehicle.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickVehicle.AddAgain)
						cmdAddVehicle_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicle") + " " + objVehicle.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicle, objVehicle.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			_objCharacter.Vehicles.Add(objVehicle);

			objNode.ContextMenuStrip = cmsVehicle;
			treVehicles.Nodes[0].Nodes.Add(objNode);
			treVehicles.Nodes[0].Expand();
			treVehicles.SelectedNode = objNode;

			UpdateCharacterInfo();
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickVehicle.AddAgain)
				cmdAddVehicle_Click(sender, e);
		}

		private void cmdDeleteVehicle_Click(object sender, EventArgs e)
		{
			// Delete the selected Vehicle.
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			if (treVehicles.SelectedNode.Level != 2)
			{
				if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteVehicle")))
					return;
			}

			if (treVehicles.SelectedNode.Level == 1)
			{
				// Locate the Vehicle that is selected in the tree.
				Vehicle objVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

				// Remove any Gear Improvements from the character (primarily those provided by an Emotitoy).
				foreach (Gear objGear in objVehicle.Gear)
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);

				_objCharacter.Vehicles.Remove(objVehicle);
				treVehicles.SelectedNode.Remove();
			}
			else if (treVehicles.SelectedNode.Level == 2)
			{
				bool blnFound = false;
				// Locate the VehicleMod that is selected in the tree.
				Vehicle objFoundVehicle = new Vehicle(_objCharacter);
				VehicleMod objMod = _objFunctions.FindVehicleMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
				if (objMod != null)
				{
					blnFound = true;

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteVehicle")))
						return;

					// Check for Improved Sensor bonus.
					if (objMod.Bonus != null)
					{
						if (objMod.Bonus["improvesensor"] != null)
						{
							ChangeVehicleSensor(objFoundVehicle, false);
						}
					}

					// If this is the Obsolete Mod, the user must select a percentage. This will create an Expense that costs X% of the Vehicle's base cost to remove the special Obsolete Mod.
					if (objMod.Name == "Obsolete" || (objMod.Name == "Obsolescent" && _objOptions.AllowObsolescentUpgrade))
					{
						frmSelectNumber frmModPercent = new frmSelectNumber();
						frmModPercent.Minimum = 0;
						frmModPercent.Maximum = 1000;
						frmModPercent.Description = LanguageManager.Instance.GetString("String_Retrofit");
						frmModPercent.ShowDialog(this);

						if (frmModPercent.DialogResult == DialogResult.Cancel)
							return;

						int intPercentage = frmModPercent.SelectedValue;
						int intVehicleCost = Convert.ToInt32(objFoundVehicle.Cost);
						
						// Make sure the character has enough Nuyen for the expense.
						int intCost = Convert.ToInt32(Convert.ToDouble(intVehicleCost, GlobalOptions.Instance.CultureInfo) * (Convert.ToDouble(intPercentage, GlobalOptions.Instance.CultureInfo) / 100.0), GlobalOptions.Instance.CultureInfo);
						if (intCost > _objCharacter.Nuyen)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}

						// Create a Vehicle Mod for the Retrofit.
						VehicleMod objRetrofit = new VehicleMod(_objCharacter);

						XmlDocument objVehiclesDoc = XmlManager.Instance.Load("vehicles.xml");
						XmlNode objXmlNode = objVehiclesDoc.SelectSingleNode("/chummer/mods/mod[name = \"Retrofit\"]");
						TreeNode objTreeNode = new TreeNode();
						objRetrofit.Create(objXmlNode, objTreeNode, 0);
						objRetrofit.Cost = intCost.ToString();
						objFoundVehicle.Mods.Add(objRetrofit);
						treVehicles.SelectedNode.Parent.Nodes.Add(objTreeNode);

						// Create an Expense Log Entry for removing the Obsolete Mod.
						ExpenseLogEntry objEntry = new ExpenseLogEntry();
						objEntry.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpenseVehicleRetrofit").Replace("{0}", objFoundVehicle.DisplayName), ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objEntry);

						// Adjust the character's Nuyen total.
						_objCharacter.Nuyen += intCost * -1;
					}

					objFoundVehicle.Mods.Remove(objMod);
					treVehicles.SelectedNode.Remove();
				}

				if (!blnFound)
				{
					// Locate the Sensor or Ammunition that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (Gear objGear in objCharacterVehicle.Gear)
						{
							if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteVehicle")))
									return;

								// Remove the Gear Weapon created by the Gear if applicable.
								if (objGear.WeaponID != Guid.Empty.ToString())
								{
									// Remove the Weapon from the TreeView.
									foreach (TreeNode objWeaponNode in treVehicles.SelectedNode.Parent.Nodes)
									{
										if (objWeaponNode.Tag.ToString() == objGear.WeaponID)
										{
											treVehicles.SelectedNode.Parent.Nodes.Remove(objWeaponNode);
											break;
										}
									}

									// Remove the Weapon from the Vehicle.
									Weapon objRemoveWeapon = new Weapon(_objCharacter);
									foreach (Weapon objWeapon in objCharacterVehicle.Weapons)
									{
										if (objWeapon.InternalId == objGear.WeaponID)
											objRemoveWeapon = objWeapon;
									}
									objCharacterVehicle.Weapons.Remove(objRemoveWeapon);
								}

								objCharacterVehicle.Gear.Remove(objGear);
								treVehicles.SelectedNode.Remove();
								blnFound = true;
								break;
							}
						}
					}
				}

				if (!blnFound)
				{
					// Locate the Weapon that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (Weapon objWeapon in objCharacterVehicle.Weapons)
						{
							if (objWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								blnFound = true;
								MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveGearWeaponVehicle"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveGearWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
								break;
							}
						}
					}
				}

				if (!blnFound)
				{
					// This must be a Location, so find it.
					TreeNode objVehicleNode = treVehicles.SelectedNode.Parent;
					Vehicle objVehicle = _objFunctions.FindVehicle(objVehicleNode.Tag.ToString(), _objCharacter.Vehicles);

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteVehicleLocation")))
						return;

					// Change the Location of the Gear.
					foreach (Gear objGear in objVehicle.Gear)
					{
						if (objGear.Location == treVehicles.SelectedNode.Text)
							objGear.Location = "";
					}

					// Move all of the child nodes in the current parent to the Vehicle.
					List<TreeNode> lstMoveNodes = new List<TreeNode>();
					foreach (TreeNode objNode in treVehicles.SelectedNode.Nodes)
						lstMoveNodes.Add(objNode);

					foreach (TreeNode objNode in lstMoveNodes)
					{
						treVehicles.SelectedNode.Nodes.Remove(objNode);
						objVehicleNode.Nodes.Add(objNode);
					}

					// Remove the Location from the Vehicle, then remove the selected node.
					objVehicle.Locations.Remove(treVehicles.SelectedNode.Text);
					treVehicles.SelectedNode.Remove();
				}
			}
			else if (treVehicles.SelectedNode.Level == 3)
			{
				bool blnFound = false;
				// Locate the selected VehicleWeapon that is selected in the tree.
				foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
				{
					foreach (VehicleMod objMod in objCharacterVehicle.Mods)
					{
						foreach (Weapon objWeapon in objMod.Weapons)
						{
							if (objWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								objMod.Weapons.Remove(objWeapon);
								treVehicles.SelectedNode.Remove();
								blnFound = true;
								break;
							}
						}
					}
				}

				if (!blnFound)
				{
					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						blnFound = true;
						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();
					}
				}

				if (!blnFound)
				{
					// Locate the selected Cyberware.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							foreach (Cyberware objCyberware in objMod.Cyberware)
							{
								if (objCyberware.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									// Remove the Cyberweapon created by the Cyberware if applicable.
									if (objCyberware.WeaponID != Guid.Empty.ToString())
									{
										// Remove the Weapon from the TreeView.
										TreeNode objRemoveNode = new TreeNode();
										foreach (TreeNode objWeaponNode in treVehicles.SelectedNode.Parent.Parent.Nodes)
										{
											if (objWeaponNode.Tag.ToString() == objCyberware.WeaponID)
												objRemoveNode = objWeaponNode;
										}
										treWeapons.Nodes.Remove(objRemoveNode);

										// Remove the Weapon from the Vehicle.
										Weapon objRemoveWeapon = new Weapon(_objCharacter);
										foreach (Weapon objWeapon in objCharacterVehicle.Weapons)
										{
											if (objWeapon.InternalId == objCyberware.WeaponID)
												objRemoveWeapon = objWeapon;
										}
										objCharacterVehicle.Weapons.Remove(objRemoveWeapon);
									}

									objMod.Cyberware.Remove(objCyberware);
									treVehicles.SelectedNode.Remove();
									break;
								}
							}
						}
					}
				}
			}
			else if (treVehicles.SelectedNode.Level == 4)
			{
				bool blnFound = false;
				// Locate the selected WeaponAccessory or VehicleWeaponMod that is selected in the tree.
				foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
				{
					foreach (VehicleMod objMod in objCharacterVehicle.Mods)
					{
						foreach (Weapon objWeapon in objMod.Weapons)
						{
							foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
							{
								if (objAccessory.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon.WeaponAccessories.Remove(objAccessory);
									treVehicles.SelectedNode.Remove();
									blnFound = true;
									break;
								}
							}
							if (!blnFound)
							{
								foreach (WeaponMod objWeaponMod in objWeapon.WeaponMods)
								{
									if (objWeaponMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
									{
										objWeapon.WeaponMods.Remove(objWeaponMod);
										treVehicles.SelectedNode.Remove();
										blnFound = true;
										break;
									}
								}
							}
							if (!blnFound)
							{
								// Remove the Underbarrel Weapon if the selected item it is one.
								if (objWeapon.UnderbarrelWeapons.Count > 0)
								{
									foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
									{
										if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
										{
											objWeapon.UnderbarrelWeapons.Remove(objUnderbarrelWeapon);
											treVehicles.SelectedNode.Remove();
											break;
										}
									}
								}
							}
						}
					}
				}

				if (!blnFound)
				{
					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						blnFound = true;
						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();

						_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);
					}
				}
			}
			else if (treVehicles.SelectedNode.Level == 5)
			{
				// Locate the selected WeaponAccessory or VehicleWeaponMod that is selected in the tree.
				bool blnFound = false;
				foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
				{
					foreach (VehicleMod objMod in objCharacterVehicle.Mods)
					{
						foreach (Weapon objWeapon in objMod.Weapons)
						{
							if (objWeapon.UnderbarrelWeapons.Count > 0)
							{
								foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
								{
									foreach (WeaponAccessory objAccessory in objUnderbarrelWeapon.WeaponAccessories)
									{
										if (objAccessory.InternalId == treVehicles.SelectedNode.Tag.ToString())
										{
											objUnderbarrelWeapon.WeaponAccessories.Remove(objAccessory);
											treVehicles.SelectedNode.Remove();
											blnFound = true;
											break;
										}
									}
									if (!blnFound)
									{
										foreach (WeaponMod objWeaponMod in objUnderbarrelWeapon.WeaponMods)
										{
											if (objWeaponMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
											{
												objUnderbarrelWeapon.WeaponMods.Remove(objWeaponMod);
												treVehicles.SelectedNode.Remove();
												blnFound = true;
												break;
											}
										}
									}
								}
							}
						}
					}
				}

				if (!blnFound)
				{
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						blnFound = true;
						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();

						_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);
					}
				}
			}
			else if (treVehicles.SelectedNode.Level > 5)
			{
				Vehicle objFoundVehicle = new Vehicle(_objCharacter);
				Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
				if (objGear != null)
				{
					objGear.Parent.Children.Remove(objGear);
					treVehicles.SelectedNode.Remove();

					_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);
				}
			}

			UpdateCharacterInfo();
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddMartialArt_Click(object sender, EventArgs e)
		{
			int intKarmaCost = 5 * _objOptions.KarmaQuality;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectMartialArt frmPickMartialArt = new frmSelectMartialArt(_objCharacter);
			frmPickMartialArt.ShowDialog(this);

			if (frmPickMartialArt.DialogResult == DialogResult.Cancel)
				return;

			// Open the Martial Arts XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("martialarts.xml");

			XmlNode objXmlArt = objXmlDocument.SelectSingleNode("/chummer/martialarts/martialart[name = \"" + frmPickMartialArt.SelectedMartialArt + "\"]");

			TreeNode objNode = new TreeNode();
			MartialArt objMartialArt = new MartialArt(_objCharacter);
			objMartialArt.Create(objXmlArt, objNode, _objCharacter);
			_objCharacter.MartialArts.Add(objMartialArt);

			objNode.ContextMenuStrip = cmsMartialArts;

			// Create the Expense Log Entry.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseLearnMartialArt") + " " + frmPickMartialArt.SelectedMartialArt, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.AddMartialArt, objMartialArt.Name);
			objExpense.Undo = objUndo;

			treMartialArts.Nodes[0].Nodes.Add(objNode);
			treMartialArts.Nodes[0].Expand();

			treMartialArts.SelectedNode = objNode;

			_objFunctions.SortTree(treMartialArts);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdDeleteMartialArt_Click(object sender, EventArgs e)
		{
			try
			{
				if (treMartialArts.SelectedNode.Level == 0)
					return;

				if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteMartialArt")))
					return;

				if (treMartialArts.SelectedNode.Level == 1)
				{
					if (treMartialArts.SelectedNode.Parent == treMartialArts.Nodes[0])
					{
						// Characters may only have 2 Maneuvers per Martial Art Rating (start at -2 since we're potentially removing one).
						int intTotalRating = -2;
						foreach (MartialArt objCharacterMartialArt in _objCharacter.MartialArts)
							intTotalRating += objCharacterMartialArt.Rating * 2;

						if (treMartialArts.Nodes[1].Nodes.Count > intTotalRating)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_MartialArtManeuverLimit"), LanguageManager.Instance.GetString("MessageTitle_MartialArtManeuverLimit"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}

						// Delete the selected Martial Art.
						MartialArt objMartialArt = _objFunctions.FindMartialArt(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts);

						// Remove the Improvements for any Advantages for the Martial Art that is being removed.
						foreach (MartialArtAdvantage objAdvantage in objMartialArt.Advantages)
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.MartialArtAdvantage, objAdvantage.InternalId);
						}

						_objCharacter.MartialArts.Remove(objMartialArt);
						treMartialArts.SelectedNode.Remove();
					}
					else
					{
						// Delete the selected Martial Art Maenuver.
						MartialArtManeuver objManeuver = _objFunctions.FindMartialArtManeuver(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArtManeuvers);

						_objCharacter.MartialArtManeuvers.Remove(objManeuver);
						treMartialArts.SelectedNode.Remove();
					}

					UpdateCharacterInfo();

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
				if (treMartialArts.SelectedNode.Level == 2)
				{
					// Find the selected Advantage object.
					MartialArt objSelectedMartialArt = new MartialArt(_objCharacter);
					MartialArtAdvantage objSelectedAdvantage = _objFunctions.FindMartialArtAdvantage(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts, out objSelectedMartialArt);

					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.MartialArtAdvantage, objSelectedAdvantage.InternalId);
					treMartialArts.SelectedNode.Remove();

					objSelectedMartialArt.Advantages.Remove(objSelectedAdvantage);

					UpdateCharacterInfo();

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}
			catch
			{
			}
		}

		private void cmdAddManeuver_Click(object sender, EventArgs e)
		{
			// Characters may only have 2 Maneuvers per Martial Art Rating.
			int intTotalRating = 0;
			foreach (MartialArt objMartialArt in _objCharacter.MartialArts)
				intTotalRating += objMartialArt.Rating * 2;

			if (treMartialArts.Nodes[1].Nodes.Count >= intTotalRating)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_MartialArtManeuverLimit"), LanguageManager.Instance.GetString("MessageTitle_MartialArtManeuverLimit"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure the character has enough Karma.
			int intKarmaCost = _objOptions.KarmaManeuver;


			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectMartialArtManeuver frmPickMartialArtManeuver = new frmSelectMartialArtManeuver(_objCharacter);
			frmPickMartialArtManeuver.ShowDialog(this);

			if (frmPickMartialArtManeuver.DialogResult == DialogResult.Cancel)
				return;

			// Open the Martial Arts XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("martialarts.xml");

			XmlNode objXmlManeuver = objXmlDocument.SelectSingleNode("/chummer/maneuvers/maneuver[name = \"" + frmPickMartialArtManeuver.SelectedManeuver + "\"]");

			TreeNode objNode = new TreeNode();
			MartialArtManeuver objManeuver = new MartialArtManeuver(_objCharacter);
			objManeuver.Create(objXmlManeuver, objNode);
			objNode.ContextMenuStrip = cmsMartialArtManeuver;
			_objCharacter.MartialArtManeuvers.Add(objManeuver);

			treMartialArts.Nodes[1].Nodes.Add(objNode);
			treMartialArts.Nodes[1].Expand();

			treMartialArts.SelectedNode = objNode;

			// Create the Expense Log Entry.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseLearnManeuver") + " " + objManeuver.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.AddMartialArtManeuver, objManeuver.InternalId);
			objExpense.Undo = objUndo;

			_objFunctions.SortTree(treMartialArts);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddMugshot_Click(object sender, EventArgs e)
		{
			// Prompt the user to select an image to associate with this character.
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "All Files (*.*)|*.*";

			if (openFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				MemoryStream objStream = new MemoryStream();
				// Convert the image to a string usinb Base64.
				Image imgMugshot = new Bitmap(openFileDialog.FileName);
				imgMugshot.Save(objStream, imgMugshot.RawFormat);
				string strResult = Convert.ToBase64String(objStream.ToArray());

				_objCharacter.Mugshot = strResult;
				picMugshot.Image = imgMugshot;

				objStream.Close();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
		}

		private void cmdDeleteMugshot_Click(object sender, EventArgs e)
		{
			_objCharacter.Mugshot = "";
			picMugshot.Image = null;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddMetamagic_Click(object sender, EventArgs e)
		{
			// Character can only have a number of Metamagics/Echoes equal to their Initiate Grade. Additional ones cost Karma.
			bool blnPayWithKarma = false;
			int intCount = 0;
			string strType = "";

			// Count the number of free Metamagics the character has and compare it to their Initiate Grade. If they have reached their limit, a new one is added for Karma.
			foreach (Metamagic objCharacterMetamagic in _objCharacter.Metamagics)
			{
				if (!objCharacterMetamagic.PaidWithKarma)
					intCount++;
			}

			try
			{
				// Make sure the character has Initiated.
				if (Convert.ToInt32(lblInitiateGrade.Text) == 0)
				{
					if (_objCharacter.MAGEnabled)
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSelectMetamagic"), LanguageManager.Instance.GetString("MessageTitle_CannotSelectMetamagic"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					else if (_objCharacter.RESEnabled)
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSelectEcho"), LanguageManager.Instance.GetString("MessageTitle_CannotSelectEcho"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				if (_objCharacter.MAGEnabled)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSelectMetamagic"), LanguageManager.Instance.GetString("MessageTitle_CannotSelectMetamagic"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				else if (_objCharacter.RESEnabled)
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSelectEcho"), LanguageManager.Instance.GetString("MessageTitle_CannotSelectEcho"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Character can only have MAG/RES + Grade Metamagics. Do not let them go beyond this limit.
			if (intCount + 1 > Convert.ToInt32(lblInitiateGrade.Text))
			{
				string strMessage = "";
				string strTitle = "";
				int intGrade = Convert.ToInt32(lblInitiateGrade.Text);
				int intAttribute = 0;

				if (_objCharacter.MAGEnabled)
					intAttribute = _objCharacter.MAG.TotalValue;
				else
					intAttribute = _objCharacter.RES.TotalValue;

				if (intCount + 1 > intAttribute + intGrade)
				{
					if (_objCharacter.MAGEnabled)
					{
						strMessage = LanguageManager.Instance.GetString("Message_AdditionalMetamagicLimit");
						strTitle = LanguageManager.Instance.GetString("MessageTitle_AdditionalMetamagic");
					}
					else if (_objCharacter.RESEnabled)
					{
						strMessage = LanguageManager.Instance.GetString("Message_AdditionalEchoLimit");
						strTitle = LanguageManager.Instance.GetString("MessageTitle_CannotSelectEcho");
					}

					MessageBox.Show(strMessage, strTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
			}

			// Additional Metamagics beyond the standard 1 per Grade cost additional Karma, so ask if the user wants to spend the additional Karma.
			if (intCount >= Convert.ToInt32(lblInitiateGrade.Text))
			{
				string strMessage = "";
				string strTitle = "";
				if (_objCharacter.MAGEnabled)
				{
					strMessage = LanguageManager.Instance.GetString("Message_AdditionalMetamagic").Replace("{0}", _objOptions.KarmaMetamagic.ToString());
					strTitle = LanguageManager.Instance.GetString("MessageTitle_AdditionalMetamagic");
					strType = LanguageManager.Instance.GetString("String_ExpenseLearnMetamagic");
				}
				else if (_objCharacter.RESEnabled)
				{
					strMessage = LanguageManager.Instance.GetString("Message_AdditionalEcho").Replace("{0}", _objOptions.KarmaMetamagic.ToString());
					strTitle = LanguageManager.Instance.GetString("MessageTitle_AdditionalEcho");
					strType = LanguageManager.Instance.GetString("String_ExpenseLearnEcho");
				}

				if (MessageBox.Show(strMessage, strTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					return;
				else
					blnPayWithKarma = true;
			}

			if (blnPayWithKarma && _objCharacter.Karma < _objOptions.KarmaMetamagic)
			{
				// Make sure the Karma expense would not put them over the limit.
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectMetamagic frmPickMetamagic = new frmSelectMetamagic(_objCharacter);
			if (_objCharacter.RESEnabled)
				frmPickMetamagic.WindowMode = frmSelectMetamagic.Mode.Echo;
			frmPickMetamagic.ShowDialog(this);

			// Make sure a value was selected.
			if (frmPickMetamagic.DialogResult == DialogResult.Cancel)
				return;

			string strMetamagic = frmPickMetamagic.SelectedMetamagic;

			XmlDocument objXmlDocument = new XmlDocument();
			XmlNode objXmlMetamagic;

			TreeNode objNode = new TreeNode();
			Metamagic objMetamagic = new Metamagic(_objCharacter);
			Improvement.ImprovementSource objSource;

			if (_objCharacter.MAGEnabled)
			{
				objXmlDocument = XmlManager.Instance.Load("metamagic.xml");
				objXmlMetamagic = objXmlDocument.SelectSingleNode("/chummer/metamagics/metamagic[name = \"" + strMetamagic + "\"]");
				objSource = Improvement.ImprovementSource.Metamagic;
			}
			else
			{
				objXmlDocument = XmlManager.Instance.Load("echoes.xml");
				objXmlMetamagic = objXmlDocument.SelectSingleNode("/chummer/echoes/echo[name = \"" + strMetamagic + "\"]");
				objSource = Improvement.ImprovementSource.Echo;
			}

			objMetamagic.Create(objXmlMetamagic, _objCharacter, objNode, objSource);
			objNode.ContextMenuStrip = cmsMetamagic;
			if (objMetamagic.InternalId == Guid.Empty.ToString())
				return;

			_objCharacter.Metamagics.Add(objMetamagic);

			if (blnPayWithKarma)
			{
				// Create the Expense Log Entry.
				ExpenseLogEntry objEntry = new ExpenseLogEntry();
				objEntry.Create(_objOptions.KarmaMetamagic * -1, strType + " " + frmPickMetamagic.SelectedMetamagic, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objEntry);

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.AddMetamagic, objMetamagic.InternalId);
				objEntry.Undo = objUndo;

				// Adjust the character's Karma total.
				_objCharacter.Karma -= _objOptions.KarmaMetamagic;
			}

			treMetamagic.Nodes.Add(objNode);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickMetamagic.AddAgain)
				cmdAddMetamagic_Click(sender, e);
		}

		private void cmdDeleteMetamagic_Click(object sender, EventArgs e)
		{
			try
			{
				if (treMetamagic.SelectedNode.Level == 0)
				{
					string strMessage = "";
					if (_objCharacter.MAGEnabled)
						strMessage = LanguageManager.Instance.GetString("Message_DeleteMetamagic");
					else if (_objCharacter.RESEnabled)
						strMessage = LanguageManager.Instance.GetString("Message_DeleteEcho");
					if (!_objFunctions.ConfirmDelete(strMessage))
						return;

					// Locate the selected Metamagic.
					Metamagic objMetamagic = _objFunctions.FindMetamagic(treMetamagic.SelectedNode.Tag.ToString(), _objCharacter.Metamagics);

					// Remove the Improvements created by the Metamagic.
					_objImprovementManager.RemoveImprovements(objMetamagic.SourceType, objMetamagic.InternalId);

					// Remove the Metamagic from the character.
					_objCharacter.Metamagics.Remove(objMetamagic);

					treMetamagic.SelectedNode.Remove();

					UpdateCharacterInfo();

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}
			catch
			{
			}
		}

		private void cmdKarmaGained_Click(object sender, EventArgs e)
		{
			frmExpense frmNewExpense = new frmExpense();
			frmNewExpense.ShowDialog(this);

			if (frmNewExpense.DialogResult == DialogResult.Cancel)
				return;

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(frmNewExpense.Amount, frmNewExpense.strReason, ExpenseType.Karma, frmNewExpense.SelectedDate, frmNewExpense.Refund);
			_objCharacter.ExpenseEntries.Add(objEntry);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ManualAdd, "");
			objEntry.Undo = objUndo;

			// Adjust the character's Karma total.
			_objCharacter.Karma += frmNewExpense.Amount;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdKarmaSpent_Click(object sender, EventArgs e)
		{
			frmExpense frmNewExpense = new frmExpense();
			frmNewExpense.ShowDialog(this);

			if (frmNewExpense.DialogResult == DialogResult.Cancel)
				return;

			// Make sure the Karma expense would not put the character's remaining Karma amount below 0.
			if (_objCharacter.Karma - frmNewExpense.Amount < 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(frmNewExpense.Amount * -1, frmNewExpense.strReason, ExpenseType.Karma, frmNewExpense.SelectedDate, frmNewExpense.Refund);
			_objCharacter.ExpenseEntries.Add(objEntry);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.ManualSubtract, "");
			objEntry.Undo = objUndo;

			// Adjust the character's Karma total.
			_objCharacter.Karma += frmNewExpense.Amount * -1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdKarmaEdit_Click(object sender, EventArgs e)
		{
			lstKarma_DoubleClick(sender, e);
		}

		private void cmdNuyenGained_Click(object sender, EventArgs e)
		{
			frmExpense frmNewExpense = new frmExpense();
			frmNewExpense.Mode = ExpenseType.Nuyen;
			frmNewExpense.ShowDialog(this);

			if (frmNewExpense.DialogResult == DialogResult.Cancel)
				return;

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(frmNewExpense.Amount, frmNewExpense.strReason, ExpenseType.Nuyen, frmNewExpense.SelectedDate);
			objEntry.Refund = frmNewExpense.Refund;
			_objCharacter.ExpenseEntries.Add(objEntry);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateNuyen(NuyenExpenseType.ManualAdd, "");
			objEntry.Undo = objUndo;

			// Adjust the character's Nuyen total.
			_objCharacter.Nuyen += frmNewExpense.Amount;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdNuyenSpent_Click(object sender, EventArgs e)
		{
			frmExpense frmNewExpense = new frmExpense();
			frmNewExpense.Mode = ExpenseType.Nuyen;
			frmNewExpense.ShowDialog(this);

			if (frmNewExpense.DialogResult == DialogResult.Cancel)
				return;

			// Make sure the Nuyen expense would not put the character's remaining Nuyen amount below 0.
			if (_objCharacter.Nuyen - frmNewExpense.Amount < 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(frmNewExpense.Amount * -1, frmNewExpense.strReason, ExpenseType.Nuyen, frmNewExpense.SelectedDate);
			_objCharacter.ExpenseEntries.Add(objEntry);

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateNuyen(NuyenExpenseType.ManualSubtract, "");
			objEntry.Undo = objUndo;

			// Adjust the character's Nuyen total.
			_objCharacter.Nuyen += frmNewExpense.Amount * -1;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdNuyenEdit_Click(object sender, EventArgs e)
		{
			lstNuyen_DoubleClick(sender, e);
		}

		private void cmdDecreaseLifestyleMonths_Click(object sender, EventArgs e)
		{
			try
			{
				if (treLifestyles.SelectedNode == null)
					return;
			}
			catch
			{
				return;
			}

			// Locate the selected Lifestyle.
			Lifestyle objLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);
			if (objLifestyle == null)
				return;

			objLifestyle.Months -= 1;
			lblLifestyleMonths.Text = objLifestyle.Months.ToString();

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdIncreaseLifestyleMonths_Click(object sender, EventArgs e)
		{
			try
			{
				if (treLifestyles.SelectedNode == null)
					return;
			}
			catch
			{
				return;
			}

			// Locate the selected Lifestyle.
			Lifestyle objLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);
			if (objLifestyle == null)
				return;

			// Create the Expense Log Entry.
			int intAmount = objLifestyle.TotalMonthlyCost;
			if (intAmount > _objCharacter.Nuyen)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intAmount * -1, LanguageManager.Instance.GetString("String_ExpenseLifestyle") + " " + objLifestyle.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Nuyen -= intAmount;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateNuyen(NuyenExpenseType.IncreaseLifestyle, objLifestyle.Name);
			objExpense.Undo = objUndo;

			objLifestyle.Months += 1;
			lblLifestyleMonths.Text = objLifestyle.Months.ToString();

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddExoticSkill_Click(object sender, EventArgs e)
		{
			frmSelectExoticSkill frmPickExoticSkill = new frmSelectExoticSkill();
			frmPickExoticSkill.ShowDialog(this);

			if (frmPickExoticSkill.DialogResult == DialogResult.Cancel)
				return;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("skills.xml");

			XmlNode nodSkill = objXmlDocument.SelectSingleNode("/chummer/skills/skill[name = \"" + frmPickExoticSkill.SelectedExoticSkill + "\"]");

			int i = panActiveSkills.Controls.Count;
			Skill objSkill = new Skill(_objCharacter);
			objSkill.Attribute = nodSkill["attribute"].InnerText;
			if (_objCharacter.MaxSkillRating > 0)
				objSkill.RatingMaximum = _objCharacter.MaxSkillRating;

			SkillControl objSkillControl = new SkillControl();
			objSkillControl.SkillObject = objSkill;
			objSkillControl.Width = 510;

			// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
			objSkillControl.RatingChanged += objActiveSkill_RatingChanged;
			objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
			objSkillControl.SkillKarmaClicked += objSkill_KarmaClicked;
			objSkillControl.SkillName = frmPickExoticSkill.SelectedExoticSkill;
			objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

			objSkillControl.SkillCategory = nodSkill["category"].InnerText;
			if (nodSkill["default"].InnerText == "Yes")
				objSkill.Default = true;
			else
				objSkill.Default = false;

			objSkill.ExoticSkill = true;
			_objCharacter.Skills.Add(objSkill);

			// Populate the Skill's Specializations (if any).
			foreach (XmlNode objXmlSpecialization in nodSkill.SelectNodes("specs/spec"))
			{
				if (objXmlSpecialization.Attributes["translate"] != null)
					objSkillControl.AddSpec(objXmlSpecialization.Attributes["translate"].InnerText);
				else
					objSkillControl.AddSpec(objXmlSpecialization.InnerText);
			}

			// Look through the Weapons file and grab the names of items that are part of the appropriate Exotic Category or use the matching Exoctic Skill.
			XmlDocument objXmlWeaponDocument = XmlManager.Instance.Load("weapons.xml");
			XmlNodeList objXmlWeaponList = objXmlWeaponDocument.SelectNodes("/chummer/weapons/weapon[category = \"" + frmPickExoticSkill.SelectedExoticSkill + "s\" or useskill = \"" + frmPickExoticSkill.SelectedExoticSkill + "s\"]");
			foreach (XmlNode objXmlWeapon in objXmlWeaponList)
			{
				if (objXmlWeapon["translate"] != null)
					objSkillControl.AddSpec(objXmlWeapon["translate"].InnerText);
				else
					objSkillControl.AddSpec(objXmlWeapon["name"].InnerText);
			}

			objSkillControl.SkillRatingMaximum = 6;
			// Set the SkillControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
			objSkillControl.Location = new Point(0, objSkillControl.Height * i + panActiveSkills.AutoScrollPosition.Y);
			panActiveSkills.Controls.Add(objSkillControl);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddCritterPower_Click(object sender, EventArgs e)
		{
			// Make sure the Critter is allowed to have Optional Powers.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("critters.xml");
			XmlNode objXmlCritter = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + _objCharacter.Metatype + "\"]");

			if (objXmlCritter == null)
			{
				objXmlDocument = XmlManager.Instance.Load("metatypes.xml");
				objXmlCritter = objXmlDocument.SelectSingleNode("/chummer/metatypes/metatype[name = \"" + _objCharacter.Metatype + "\"]");
			}

			frmSelectCritterPower frmPickCritterPower = new frmSelectCritterPower(_objCharacter);
			frmPickCritterPower.ShowDialog(this);

			if (frmPickCritterPower.DialogResult == DialogResult.Cancel)
				return;

			objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
			XmlNode objXmlPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + frmPickCritterPower.SelectedPower + "\"]");
			TreeNode objNode = new TreeNode();
			CritterPower objPower = new CritterPower(_objCharacter);
			objPower.Create(objXmlPower, _objCharacter, objNode, frmPickCritterPower.SelectedRating);
			objPower.PowerPoints = frmPickCritterPower.PowerPoints;
			objNode.ContextMenuStrip = cmsCritterPowers;
			if (objPower.InternalId == Guid.Empty.ToString())
				return;

			_objCharacter.CritterPowers.Add(objPower);

			if (objPower.Category != "Weakness")
			{
				treCritterPowers.Nodes[0].Nodes.Add(objNode);
				treCritterPowers.Nodes[0].Expand();
			}
			else
			{
				treCritterPowers.Nodes[1].Nodes.Add(objNode);
				treCritterPowers.Nodes[1].Expand();
			}

			// Determine if the Critter should have access to the Possession menu item.
			bool blnAllowPossession = false;
			foreach (CritterPower objCritterPower in _objCharacter.CritterPowers)
			{
				if (objCritterPower.Name == "Inhabitation" || objCritterPower.Name == "Possession")
				{
					blnAllowPossession = true;
					break;
				}
			}
			mnuSpecialPossess.Visible = blnAllowPossession;

			_objFunctions.SortTree(treCritterPowers);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickCritterPower.AddAgain)
				cmdAddCritterPower_Click(sender, e);
		}

		private void cmdDeleteCritterPower_Click(object sender, EventArgs e)
		{
			try
			{
				if (treCritterPowers.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteCritterPower")))
				return;

			// Locate the selected Critter Power.
			CritterPower objPower = _objFunctions.FindCritterPower(treCritterPowers.SelectedNode.Tag.ToString(), _objCharacter.CritterPowers);

			// Remove any Improvements that were created by the Critter Power.
			_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.CritterPower, objPower.InternalId);

			_objCharacter.CritterPowers.Remove(objPower);
			treCritterPowers.SelectedNode.Remove();

			// Determine if the Critter should have access to the Possession menu item.
			bool blnAllowPossession = false;
			foreach (CritterPower objCritterPower in _objCharacter.CritterPowers)
			{
				if (objCritterPower.Name == "Inhabitation" || objCritterPower.Name == "Possession")
				{
					blnAllowPossession = true;
					break;
				}
			}
			mnuSpecialPossess.Visible = blnAllowPossession;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdDeleteComplexForm_Click(object sender, EventArgs e)
		{
			// Delete the selected Complex Form.
			try
			{
				if (treComplexForms.SelectedNode.Level == 1)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteComplexForm")))
						return;

					// Locate the Program that is selected in the tree.
                    ComplexForm objProgram = _objFunctions.FindComplexForm(treComplexForms.SelectedNode.Tag.ToString(), _objCharacter.ComplexForms);

					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ComplexForm, objProgram.InternalId);

                    _objCharacter.ComplexForms.Remove(objProgram);
					treComplexForms.SelectedNode.Remove();
				}
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdImproveComplexForm_Click(object sender, EventArgs e)
		{
			if (treComplexForms.SelectedNode.Level == 1)
			{
				// Locate the Program that is selected in the tree.
                ComplexForm objProgram = _objFunctions.FindComplexForm(treComplexForms.SelectedNode.Tag.ToString(), _objCharacter.ComplexForms);

				// Make sure the character has enough Karma.
				int intKarmaCost = _objOptions.KarmaImproveComplexForm;

				if (intKarmaCost > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

                if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpend").Replace("{0}", intKarmaCost.ToString()).Replace("{1}", objProgram.DisplayNameShort)))
					return;

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseComplexForm") + " " + objProgram.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaCost;

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.ImproveComplexForm, objProgram.InternalId);
				objExpense.Undo = objUndo;

				treComplexForms.SelectedNode.Text = objProgram.DisplayName;
			}

            UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdGearReduceQty_Click(object sender, EventArgs e)
		{
			Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			Gear objParent = _objFunctions.FindGear(treGear.SelectedNode.Parent.Tag.ToString(), _objCharacter.Gear);

			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_ReduceQty")))
				return;
				
			objGear.Quantity -= 1;

			if (objGear.Quantity > 0)
			{
				treGear.SelectedNode.Text = objGear.DisplayName;
				RefreshSelectedGear();
			}
			else
			{
				// Remove the Gear if its quantity has been reduced to 0.
				if (objParent == null)
				{
					_objCharacter.Gear.Remove(objGear);
					treGear.SelectedNode.Remove();
				}
				else
				{
					objParent.Children.Remove(objGear);
					treGear.SelectedNode.Remove();
				}

				// Remove any Weapons that came with it.
				if (objGear.WeaponID != Guid.Empty.ToString())
				{
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						if (objWeapon.InternalId == objGear.WeaponID)
						{
							_objCharacter.Weapons.Remove(objWeapon);
							break;
						}
					}
				}
				foreach (TreeNode nodWeapon in treWeapons.Nodes[0].Nodes)
				{
					if (nodWeapon.Tag.ToString() == objGear.WeaponID)
					{
						nodWeapon.Remove();
						break;
					}
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdGearSplitQty_Click(object sender, EventArgs e)
		{
			// This can only be done with the first level of Nodes.
			try
			{
				if (treGear.SelectedNode.Level != 1)
					return;
			}
			catch
			{
				return;
			}

			Gear objSelectedGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);

			// Cannot split a stack of 1 item.
			if (objSelectedGear.Quantity == 1)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSplitGear"), LanguageManager.Instance.GetString("MessageTitle_CannotSplitGear"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			frmSelectNumber frmPickNumber = new frmSelectNumber();
			frmPickNumber.Minimum = 1;
			frmPickNumber.Maximum = objSelectedGear.Quantity - 1;
			frmPickNumber.Description = LanguageManager.Instance.GetString("String_SplitGear");
			frmPickNumber.ShowDialog(this);

			if (frmPickNumber.DialogResult == DialogResult.Cancel)
				return;

			// Create a new piece of Gear.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSelectedGear.Name + "\" and category = \"" + objSelectedGear.Category + "\"]");
			
			TreeNode objGearNode = new TreeNode();
			List<Weapon> lstWeapons = new List<Weapon>();
			List<TreeNode> lstWeaponNodes = new List<TreeNode>();
			Gear objGear = new Gear(_objCharacter);
			if (objSelectedGear.GetType() == typeof(Commlink))
			{
				Commlink objCommlink = new Commlink(_objCharacter);
				objCommlink.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);
				objGear = objCommlink;
			}
			else
				objGear.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);
			
			objGear.Quantity = frmPickNumber.SelectedValue;
			objGear.Equipped = objSelectedGear.Equipped;
			objGear.Location = objSelectedGear.Location;
			objGear.Notes = objSelectedGear.Notes;
			objGearNode.Text = objGear.DisplayName;
			objGearNode.ContextMenuStrip = treGear.SelectedNode.ContextMenuStrip;

			// Update the selected item.
			objSelectedGear.Quantity -= frmPickNumber.SelectedValue;
			treGear.SelectedNode.Text = objSelectedGear.DisplayName;

			treGear.SelectedNode.Parent.Nodes.Add(objGearNode);
			_objCharacter.Gear.Add(objGear);

			// Create any Weapons that came with this Gear.
			foreach (Weapon objWeapon in lstWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in lstWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeapon;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdGearMergeQty_Click(object sender, EventArgs e)
		{
			Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			List<Gear> lstGear = new List<Gear>();

			foreach (Gear objCharacterGear in _objCharacter.Gear)
			{
				bool blnMatch = false;
				// Matches must happen on Name, Category, Rating, and Extra, plus all plugins.
				if (objCharacterGear.Name == objGear.Name && objCharacterGear.Category == objGear.Category && objCharacterGear.Rating == objGear.Rating && objCharacterGear.Extra == objGear.Extra && objCharacterGear.InternalId != objGear.InternalId)
				{
					blnMatch = true;
					if (objCharacterGear.Children.Count == objGear.Children.Count)
					{
						for (int i = 0; i <= objCharacterGear.Children.Count - 1; i++)
						{
							if (objCharacterGear.Children[i].Name != objGear.Children[i].Name || objCharacterGear.Children[i].Extra != objGear.Children[i].Extra || objCharacterGear.Children[i].Rating != objGear.Children[i].Rating)
							{
								blnMatch = false;
								break;
							}
						}
					}
					else
						blnMatch = false;
				}

				if (blnMatch)
					lstGear.Add(objCharacterGear);
			}

			// If there were no matches, don't try to merge anything.
			if (lstGear.Count == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotMergeGear"), LanguageManager.Instance.GetString("MessageTitle_CannotMergeGear"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Show the Select Item window.
			frmSelectItem frmPickItem = new frmSelectItem();
			frmPickItem.Gear = lstGear;
			frmPickItem.ShowDialog(this);

			if (frmPickItem.DialogResult == DialogResult.Cancel)
				return;

			Gear objSelectedGear = _objFunctions.FindGear(frmPickItem.SelectedItem, _objCharacter.Gear);

			frmSelectNumber frmPickNumber = new frmSelectNumber();
			frmPickNumber.Minimum = 1;
			frmPickNumber.Maximum = objGear.Quantity;
			frmPickNumber.Description = LanguageManager.Instance.GetString("String_MergeGear");
			frmPickNumber.ShowDialog(this);

			if (frmPickNumber.DialogResult == DialogResult.Cancel)
				return;

			// Increase the quantity for the selected item.
			objSelectedGear.Quantity += frmPickNumber.SelectedValue;
			// Located the item in the Tree and update its display information.
			foreach (TreeNode objParent in treGear.Nodes)
			{
				foreach (TreeNode objNode in objParent.Nodes)
				{
					if (objNode.Tag.ToString() == objSelectedGear.InternalId)
					{
						objNode.Text = objSelectedGear.DisplayName;
						break;
					}
				}
			}

			// Reduce the quantity for the selected item.
			objGear.Quantity -= frmPickNumber.SelectedValue;
			// If the quantity has reached 0, delete the item and any Weapons it created.
			if (objGear.Quantity == 0)
			{
				// Remove the Gear Weapon created by the Gear if applicable.
				if (objGear.WeaponID != Guid.Empty.ToString())
				{
					// Remove the Weapon from the TreeView.
					TreeNode objRemoveNode = new TreeNode();
					foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
					{
						if (objWeaponNode.Tag.ToString() == objGear.WeaponID)
							objRemoveNode = objWeaponNode;
					}
					treWeapons.Nodes.Remove(objRemoveNode);

					// Remove the Weapon from the Character.
					Weapon objRemoveWeapon = new Weapon(_objCharacter);
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						if (objWeapon.InternalId == objGear.WeaponID)
							objRemoveWeapon = objWeapon;
					}
					_objCharacter.Weapons.Remove(objRemoveWeapon);
				}

				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);

				// Remove the Gear from the character.
				_objCharacter.Gear.Remove(objGear);
				treGear.SelectedNode.Remove();
			}
			else
				treGear.SelectedNode.Text = objGear.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdGearMoveToVehicle_Click(object sender, EventArgs e)
		{
			frmSelectItem frmPickItem = new frmSelectItem();
			frmPickItem.Vehicles = _objCharacter.Vehicles;
			frmPickItem.ShowDialog(this);

			if (frmPickItem.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected Vehicle.
			Vehicle objVehicle = new Vehicle(_objCharacter);
			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				if (objCharacterVehicle.InternalId == frmPickItem.SelectedItem)
				{
					objVehicle = objCharacterVehicle;
					break;
				}
			}

			Gear objSelectedGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			int intMove = 0;
			if (objSelectedGear.Quantity == 1)
				intMove = 1;
			else
			{
				frmSelectNumber frmPickNumber = new frmSelectNumber();
				frmPickNumber.Minimum = 1;
				frmPickNumber.Maximum = objSelectedGear.Quantity;
				frmPickNumber.Description = LanguageManager.Instance.GetString("String_MoveGear");
				frmPickNumber.ShowDialog(this);

				if (frmPickNumber.DialogResult == DialogResult.Cancel)
					return;

				intMove = frmPickNumber.SelectedValue;
			}

			// See if the Vehicle already has a matching piece of Gear.
			bool blnMatch = false;
			Gear objFoundGear = new Gear(_objCharacter);
			foreach (Gear objVehicleGear in objVehicle.Gear)
			{
				if (objVehicleGear.Name == objSelectedGear.Name && objVehicleGear.Category == objSelectedGear.Category && objVehicleGear.Rating == objSelectedGear.Rating && objVehicleGear.Extra == objSelectedGear.Extra && objVehicleGear.GearName == objSelectedGear.GearName && objVehicleGear.Notes == objSelectedGear.Notes)
				{
					blnMatch = true;
					objFoundGear = objVehicleGear;
					if (objVehicleGear.Children.Count == objSelectedGear.Children.Count)
					{
						for (int i = 0; i <= objVehicleGear.Children.Count - 1; i++)
						{
							if (objVehicleGear.Children[i].Name != objSelectedGear.Children[i].Name || objVehicleGear.Children[i].Extra != objSelectedGear.Children[i].Extra || objVehicleGear.Children[i].Rating != objSelectedGear.Children[i].Rating)
							{
								blnMatch = false;
								break;
							}
						}
					}
					else
						blnMatch = false;
				}
			}

			if (!blnMatch)
			{
				// Create a new piece of Gear.
				TreeNode objGearNode = new TreeNode();
				List<Weapon> lstWeapons = new List<Weapon>();
				List<TreeNode> lstWeaponNodes = new List<TreeNode>();
				Gear objGear = new Gear(_objCharacter);
				if (objSelectedGear.GetType() == typeof(Commlink))
				{
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);
					objGear = objCommlink;
				}
				else
					objGear.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);

				objGear.Parent = null;
				objGear.Quantity = intMove;
				objGear.Location = string.Empty;
				objGearNode.Text = objGear.DisplayName;
				objGearNode.ContextMenuStrip = cmsVehicleGear;

				// Locate the Node for the selected Vehicle.
				TreeNode nodParent = new TreeNode();
				foreach (TreeNode nodNode in treVehicles.Nodes[0].Nodes)
				{
					if (nodNode.Tag.ToString() == objVehicle.InternalId)
					{
						nodParent = nodNode;
						break;
					}
				}

				nodParent.Nodes.Add(objGearNode);
				objVehicle.Gear.Add(objGear);
			}
			else
			{
				// Everything matches up, so just increase the quantity.
				objFoundGear.Quantity += intMove;
				foreach (TreeNode nodVehicle in treVehicles.Nodes[0].Nodes)
				{
					if (nodVehicle.Tag.ToString() == objVehicle.InternalId)
					{
						foreach (TreeNode nodGear in nodVehicle.Nodes)
						{
							if (nodGear.Tag.ToString() == objFoundGear.InternalId)
								nodGear.Text = objFoundGear.DisplayName;
						}
					}
				}
			}

			// Update the selected item.
			objSelectedGear.Quantity -= intMove;
			if (objSelectedGear.Quantity == 0)
			{
				if (objSelectedGear.Parent != null)
					objSelectedGear.Parent.Children.Remove(objSelectedGear);
				else
					_objCharacter.Gear.Remove(objSelectedGear);
				_objFunctions.DeleteGear(objSelectedGear, treWeapons, _objImprovementManager);
				treGear.SelectedNode.Remove();
				UpdateCharacterInfo();
			}
			else
			{
				treGear.SelectedNode.Text = objSelectedGear.DisplayName;
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdVehicleMoveToInventory_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			bool blnFound = false;
			Weapon objWeapon = new Weapon(_objCharacter);
			Vehicle objVehicle = new Vehicle(_objCharacter);
			VehicleMod objMod = new VehicleMod(_objCharacter);

			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				foreach (Weapon objVehicleWeapon in objCharacterVehicle.Weapons)
				{
					if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
					{
						objWeapon = objVehicleWeapon;
						objVehicle = objCharacterVehicle;
						blnFound = true;
						break;
					}
				}
				foreach (VehicleMod objVehicleMod in objCharacterVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objVehicleMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							objVehicle = objCharacterVehicle;
							objMod = objVehicleMod;
							blnFound = true;
							break;
						}
					}
				}
			}

			if (blnFound){
				// Move the Weapons from the Vehicle Mod (or Vehicle) to the character.
				if (objMod.InternalId != Guid.Empty.ToString())
					objMod.Weapons.Remove(objWeapon);
				else
					objVehicle.Weapons.Remove(objWeapon);

				_objCharacter.Weapons.Add(objWeapon);

				TreeNode objNode = new TreeNode();
				objNode = treVehicles.SelectedNode;

				treVehicles.SelectedNode.Remove();
				treWeapons.Nodes[0].Nodes.Add(objNode);
				objWeapon.VehicleMounted = false;
				objNode.Expand();
			}
			else
			{
				// Locate the selected Gear.
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				Gear objSelectedGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);

				int intMove = 0;
				if (objSelectedGear.Quantity == 1)
					intMove = 1;
				else
				{
					frmSelectNumber frmPickNumber = new frmSelectNumber();
					frmPickNumber.Minimum = 1;
					frmPickNumber.Maximum = objSelectedGear.Quantity;
					frmPickNumber.Description = LanguageManager.Instance.GetString("String_MoveGear");
					frmPickNumber.ShowDialog(this);

					if (frmPickNumber.DialogResult == DialogResult.Cancel)
						return;

					intMove = frmPickNumber.SelectedValue;
				}

				// See if the character already has a matching piece of Gear.
				bool blnMatch = false;
				Gear objFoundGear = new Gear(_objCharacter);
				foreach (Gear objCharacterGear in _objCharacter.Gear)
				{
					if (objCharacterGear.Name == objSelectedGear.Name && objCharacterGear.Category == objSelectedGear.Category && objCharacterGear.Rating == objSelectedGear.Rating && objCharacterGear.Extra == objSelectedGear.Extra && objCharacterGear.GearName == objSelectedGear.GearName && objCharacterGear.Notes == objSelectedGear.Notes)
					{
						blnMatch = true;
						objFoundGear = objCharacterGear;
						if (objCharacterGear.Children.Count == objSelectedGear.Children.Count)
						{
							for (int i = 0; i <= objCharacterGear.Children.Count - 1; i++)
							{
								if (objCharacterGear.Children[i].Name != objSelectedGear.Children[i].Name || objCharacterGear.Children[i].Extra != objSelectedGear.Children[i].Extra || objCharacterGear.Children[i].Rating != objSelectedGear.Children[i].Rating)
								{
									blnMatch = false;
									break;
								}
							}
						}
						else
							blnMatch = false;
					}
				}

				if (!blnMatch)
				{
					// Create a new piece of Gear.
					TreeNode objGearNode = new TreeNode();
					List<Weapon> lstWeapons = new List<Weapon>();
					List<TreeNode> lstWeaponNodes = new List<TreeNode>();
					Gear objGear = new Gear(_objCharacter);
					if (objSelectedGear.GetType() == typeof(Commlink))
					{
						Commlink objCommlink = new Commlink(_objCharacter);
						objCommlink.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);
						objGear = objCommlink;
					}
					else
						objGear.Copy(objSelectedGear, objGearNode, lstWeapons, lstWeaponNodes);

					objGear.Parent = null;
					objGear.Quantity = intMove;
					objGearNode.Text = objGear.DisplayName;
					objGearNode.ContextMenuStrip = cmsGear;

					treGear.Nodes[0].Nodes.Add(objGearNode);
					_objCharacter.Gear.Add(objGear);

					// Create any Weapons that came with this Gear.
					foreach (Weapon objGearWeapon in lstWeapons)
						_objCharacter.Weapons.Add(objGearWeapon);

					foreach (TreeNode objWeaponNode in lstWeaponNodes)
					{
						objWeaponNode.ContextMenuStrip = cmsWeapon;
						treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
						treWeapons.Nodes[0].Expand();
					}

					AddGearImprovements(objGear);
					UpdateCharacterInfo();
				}
				else
				{
					// Everything matches up, so just increase the quantity.
					objFoundGear.Quantity += intMove;
					foreach (TreeNode nodGear in treGear.Nodes[0].Nodes)
					{
						if (nodGear.Tag.ToString() == objFoundGear.InternalId)
							nodGear.Text = objFoundGear.DisplayName;
					}
				}

				// Update the selected item.
				objSelectedGear.Quantity -= intMove;
				if (objSelectedGear.Quantity == 0)
				{
					// The quantity has reached 0, so remove it entirely.
					treVehicles.SelectedNode.Remove();
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
						objCharacterVehicle.Gear.Remove(objSelectedGear);
				}
				else
					treVehicles.SelectedNode.Text = objSelectedGear.DisplayName;
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		
		private void cmdGearIncreaseQty_Click(object sender, EventArgs e)
		{
			Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);

			// Select the root Gear node then open the Select Gear window.
			bool blnAddAgain = PickGear(true, objGear);
			if (blnAddAgain)
				cmdGearIncreaseQty_Click(sender, e);
			_objController.PopulateFocusList(treFoci);
		}

		private void cmdVehicleGearReduceQty_Click(object sender, EventArgs e)
		{
			Gear objGear = new Gear(_objCharacter);
			Gear objParent = new Gear(_objCharacter);
			Vehicle objVehicle = new Vehicle(_objCharacter);
			// Locate the currently selected piece of Gear.
			objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);
			objParent = objGear.Parent;

			if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_ReduceQty")))
				return;

			objGear.Quantity -= 1;

			if (objGear.Quantity > 0)
			{
				treVehicles.SelectedNode.Text = objGear.DisplayName;
				RefreshSelectedVehicle();
			}
			else
			{
				// Remove the Gear if its quantity has been reduced to 0.
				if (objParent == null)
				{
					objVehicle.Gear.Remove(objGear);
					treVehicles.SelectedNode.Remove();
				}
				else
				{
					objParent.Children.Remove(objGear);
					treVehicles.SelectedNode.Remove();
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddQuality_Click(object sender, EventArgs e)
		{
			frmSelectQuality frmPickQuality = new frmSelectQuality(_objCharacter);
			frmPickQuality.ShowDialog(this);

			// Don't do anything else if the form was canceled.
			if (frmPickQuality.DialogResult == DialogResult.Cancel)
				return;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("qualities.xml");
			XmlNode objXmlQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + frmPickQuality.SelectedQuality + "\"]");

			TreeNode objNode = new TreeNode();
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			Quality objQuality = new Quality(_objCharacter);

			objQuality.Create(objXmlQuality, _objCharacter, QualitySource.Selected, objNode, objWeapons, objWeaponNodes);
			objNode.ContextMenuStrip = cmsQuality;
			if (objQuality.InternalId == Guid.Empty.ToString())
				return;

			if (frmPickQuality.FreeCost)
				objQuality.BP = 0;

			bool blnAddItem = true;
			int intKarmaCost = objQuality.BP * _objOptions.KarmaQuality;

			// Make sure the character has enough Karma to pay for the Quality.
			if (objQuality.Type == QualityType.Positive)
			{
				if (intKarmaCost > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					blnAddItem = false;
				}

				if (blnAddItem && !frmPickQuality.FreeCost)
				{
					if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpend").Replace("{0}", objQuality.DisplayNameShort).Replace("{1}", intKarmaCost.ToString())))
						blnAddItem = false;
				}

				if (blnAddItem)
				{
					// Create the Karma expense.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseAddPositiveQuality") + " " + objQuality.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Karma -= intKarmaCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateKarma(KarmaExpenseType.AddQuality, objQuality.InternalId);
					objExpense.Undo = objUndo;
				}
			}
			else
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_AddNegativeQuality"), LanguageManager.Instance.GetString("MessageTitle_AddNegativeQuality"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					blnAddItem = false;

				if (blnAddItem)
				{
					// Create a Karma Expense for the Negative Quality.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(0, LanguageManager.Instance.GetString("String_ExpenseAddNegativeQuality") + " " + objQuality.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateKarma(KarmaExpenseType.AddQuality, objQuality.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			if (blnAddItem)
			{
				// Add the Quality to the appropriate parent node.
				if (objQuality.Type == QualityType.Positive)
				{
					treQualities.Nodes[0].Nodes.Add(objNode);
					treQualities.Nodes[0].Expand();
				}
				else
				{
					treQualities.Nodes[1].Nodes.Add(objNode);
					treQualities.Nodes[1].Expand();
				}
				_objCharacter.Qualities.Add(objQuality);

				// Add any created Weapons to the character.
				foreach (Weapon objWeapon in objWeapons)
					_objCharacter.Weapons.Add(objWeapon);

				// Create the Weapon Node if one exists.
				foreach (TreeNode objWeaponNode in objWeaponNodes)
				{
					objWeaponNode.ContextMenuStrip = cmsWeapon;
					treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
					treWeapons.Nodes[0].Expand();
				}

				// Add any additional Qualities that are forced on the character.
				if (objXmlQuality.SelectNodes("addqualities/addquality").Count > 0)
				{
					foreach (XmlNode objXmlAddQuality in objXmlQuality.SelectNodes("addqualities/addquality"))
					{
						XmlNode objXmlSelectedQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlAddQuality.InnerText + "\"]");
						string strForceValue = "";
						if (objXmlAddQuality.Attributes["select"] != null)
							strForceValue = objXmlAddQuality.Attributes["select"].InnerText;
						bool blnAddQuality = true;

						// Make sure the character does not yet have this Quality.
						foreach (Quality objCharacterQuality in _objCharacter.Qualities)
						{
							if (objCharacterQuality.Name == objXmlAddQuality.InnerText && objCharacterQuality.Extra == strForceValue)
							{
								blnAddQuality = false;
								break;
							}
						}

						if (blnAddQuality)
						{
							TreeNode objAddQualityNode = new TreeNode();
							List<Weapon> objAddWeapons = new List<Weapon>();
							List<TreeNode> objAddWeaponNodes = new List<TreeNode>();
							Quality objAddQuality = new Quality(_objCharacter);
							objAddQuality.Create(objXmlSelectedQuality, _objCharacter, QualitySource.Selected, objAddQualityNode, objAddWeapons, objAddWeaponNodes, strForceValue);

							if (objAddQuality.Type == QualityType.Positive)
							{
								treQualities.Nodes[0].Nodes.Add(objAddQualityNode);
								treQualities.Nodes[0].Expand();
							}
							else
							{
								treQualities.Nodes[1].Nodes.Add(objAddQualityNode);
								treQualities.Nodes[1].Expand();
							}
							_objCharacter.Qualities.Add(objAddQuality);

							// Add any created Weapons to the character.
							foreach (Weapon objWeapon in objAddWeapons)
								_objCharacter.Weapons.Add(objWeapon);

							// Create the Weapon Node if one exists.
							foreach (TreeNode objWeaponNode in objAddWeaponNodes)
							{
								objWeaponNode.ContextMenuStrip = cmsWeapon;
								treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
								treWeapons.Nodes[0].Expand();
							}
						}
					}
				}

				// Add any Critter Powers that are gained through the Quality (Infected).
				if (objXmlQuality.SelectNodes("powers/power").Count > 0)
				{
					objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
					foreach (XmlNode objXmlPower in objXmlQuality.SelectNodes("powers/power"))
					{
						XmlNode objXmlCritterPower = objXmlDocument.SelectSingleNode("/chummer/powers/power[name = \"" + objXmlPower.InnerText + "\"]");
						TreeNode objPowerNode = new TreeNode();
						CritterPower objPower = new CritterPower(_objCharacter);
						string strForcedValue = "";
						int intRating = 0;

						if (objXmlPower.Attributes["rating"] != null)
							intRating = Convert.ToInt32(objXmlPower.Attributes["rating"].InnerText);
						if (objXmlPower.Attributes["select"] != null)
							strForcedValue = objXmlPower.Attributes["select"].InnerText;

						objPower.Create(objXmlCritterPower, _objCharacter, objPowerNode, intRating, strForcedValue);
						_objCharacter.CritterPowers.Add(objPower);

						if (objPower.Category != "Weakness")
						{
							treCritterPowers.Nodes[0].Nodes.Add(objPowerNode);
							treCritterPowers.Nodes[0].Expand();
						}
						else
						{
							treCritterPowers.Nodes[1].Nodes.Add(objPowerNode);
							treCritterPowers.Nodes[1].Expand();
						}
					}
				}
			}
			else
			{
				// Remove the Improvements created by the Create method.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);
			}

			_objFunctions.SortTree(treQualities);
			UpdateMentorSpirits();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickQuality.AddAgain)
				cmdAddQuality_Click(sender, e);
		}

		private void cmdDeleteQuality_Click(object sender, EventArgs e)
		{
			bool blnMetatypeQuality = false;

			// Locate the selected Quality.
			try
			{
				if (treQualities.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			Quality objQuality = _objFunctions.FindQuality(treQualities.SelectedNode.Tag.ToString(), _objCharacter.Qualities);

			XmlDocument objXmlDocument = XmlManager.Instance.Load("qualities.xml");

			// Qualities that come from a Metatype cannot be removed.
			if (objQuality.OriginSource == QualitySource.Metatype)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_MetavariantQuality"), LanguageManager.Instance.GetString("MessageTitle_MetavariantQuality"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else if (objQuality.OriginSource == QualitySource.MetatypeRemovable)
			{
				// Look up the cost of the Quality.
				XmlNode objXmlMetatypeQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objQuality.Name + "\"]");
				int intBP = Convert.ToInt32(objXmlMetatypeQuality["bp"].InnerText) * -1;
				int intShowBP = intBP * _objOptions.KarmaQuality;
				string strBP = intShowBP.ToString() + " " + LanguageManager.Instance.GetString("String_Karma");

				if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteMetatypeQuality").Replace("{0}", strBP)))
					return;

				blnMetatypeQuality = true;
			}

			if (objQuality.Type == QualityType.Positive)
			{
				if (!blnMetatypeQuality)
				{
					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeletePositiveQualityCareer")))
						return;
				}

				// Remove the Improvements that were created by the Quality.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);

				_objCharacter.Qualities.Remove(objQuality);
				treQualities.SelectedNode.Remove();
			}
			else
			{
				// Make sure the character has enough Karma to buy off the Quality.
				int intKarmaCost = (objQuality.BP * _objOptions.KarmaQuality) * -1;
				if (intKarmaCost > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				if (!blnMetatypeQuality)
				{
					if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseRemove").Replace("{0}", objQuality.DisplayNameShort).Replace("{1}", intKarmaCost.ToString())))
						return;
				}

				// Create the Karma expense.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseRemoveNegativeQuality") + " " + objQuality.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaCost;

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.RemoveQuality, objQuality.Name);
				objUndo.Extra = objQuality.Extra;
				objExpense.Undo = objUndo;

				// Remove the Improvements that were created by the Quality.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);

				XmlNode objXmlDeleteQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objQuality.Name + "\"]");

				// Remove any Critter Powers that are gained through the Quality (Infected).
				if (objXmlDeleteQuality.SelectNodes("powers/power").Count > 0)
				{
					objXmlDocument = XmlManager.Instance.Load("critterpowers.xml");
					foreach (XmlNode objXmlPower in objXmlDeleteQuality.SelectNodes("powers/power"))
					{
						string strExtra = "";
						if (objXmlPower.Attributes["select"] != null)
							strExtra = objXmlPower.Attributes["select"].InnerText;

						foreach (CritterPower objPower in _objCharacter.CritterPowers)
						{
							if (objPower.Name == objXmlPower.InnerText && objPower.Extra == strExtra)
							{
								// Remove any Improvements created by the Critter Power.
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.CritterPower, objPower.InternalId);

								// Remove the Critter Power from the character.
								_objCharacter.CritterPowers.Remove(objPower);

								// Remove the Critter Power from the Tree.
								foreach (TreeNode objNode in treCritterPowers.Nodes[0].Nodes)
								{
									if (objNode.Tag.ToString() == objPower.InternalId)
									{
										objNode.Remove();
										break;
									}
								}
								foreach (TreeNode objNode in treCritterPowers.Nodes[1].Nodes)
								{
									if (objNode.Tag.ToString() == objPower.InternalId)
									{
										objNode.Remove();
										break;
									}
								}
								break;
							}
						}
					}
				}

				_objCharacter.Qualities.Remove(objQuality);
				treQualities.SelectedNode.Remove();
			}

			// Remove any Weapons created by the Quality if applicable.
			if (objQuality.WeaponID != Guid.Empty.ToString())
			{
				// Remove the Weapon from the TreeView.
				TreeNode objRemoveNode = new TreeNode();
				foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
				{
					if (objWeaponNode.Tag.ToString() == objQuality.WeaponID)
						objRemoveNode = objWeaponNode;
				}
				treWeapons.Nodes.Remove(objRemoveNode);

				// Remove the Weapon from the Character.
				Weapon objRemoveWeapon = new Weapon(_objCharacter);
				foreach (Weapon objWeapon in _objCharacter.Weapons)
				{
					if (objWeapon.InternalId == objQuality.WeaponID)
						objRemoveWeapon = objWeapon;
				}
				_objCharacter.Weapons.Remove(objRemoveWeapon);
			}

			UpdateMentorSpirits();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdSwapQuality_Click(object sender, EventArgs e)
		{
			// Locate the selected Quality.
			try
			{
				if (treQualities.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			Quality objQuality = _objFunctions.FindQuality(treQualities.SelectedNode.Tag.ToString(), _objCharacter.Qualities);

			// Qualities that come from a Metatype cannot be removed.
			if (objQuality.OriginSource == QualitySource.Metatype)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_MetavariantQualitySwap"), LanguageManager.Instance.GetString("MessageTitle_MetavariantQualitySwap"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectQuality frmPickQuality = new frmSelectQuality(_objCharacter);
			frmPickQuality.ForceCategory = objQuality.Type.ToString();
			frmPickQuality.IgnoreQuality = objQuality.Name;
			frmPickQuality.ShowDialog(this);

			// Don't do anything else if the form was canceled.
			if (frmPickQuality.DialogResult == DialogResult.Cancel)
				return;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("qualities.xml");
			XmlNode objXmlQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + frmPickQuality.SelectedQuality + "\"]");

			TreeNode objNode = new TreeNode();
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			Quality objNewQuality = new Quality(_objCharacter);

			objNewQuality.Create(objXmlQuality, _objCharacter, QualitySource.Selected, objNode, objWeapons, objWeaponNodes);
			objNode.ContextMenuStrip = cmsQuality;
			if (objQuality.InternalId == Guid.Empty.ToString())
				return;

			bool blnAddItem = true;
			int intKarmaCost = (objNewQuality.BP - objQuality.BP) * _objOptions.KarmaQuality;

			// Make sure the character has enough Karma to pay for the Quality.
			if (objNewQuality.Type == QualityType.Positive)
			{
				if (intKarmaCost > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					blnAddItem = false;
				}

				if (blnAddItem)
				{
					if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_QualitySwap").Replace("{0}", objQuality.DisplayNameShort).Replace("{1}", objNewQuality.DisplayNameShort)))
						blnAddItem = false;
				}

				if (blnAddItem)
				{
					// Create the Karma expense.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseSwapPositiveQuality").Replace("{0}", objQuality.DisplayNameShort).Replace("{1}", objNewQuality.DisplayNameShort), ExpenseType.Karma, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Karma -= intKarmaCost;
				}
			}
			else
			{
				// This should only happen when a character is trading up to a less-costly Quality.
				if (intKarmaCost > 0)
				{
					if (intKarmaCost > _objCharacter.Karma)
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						blnAddItem = false;
					}

					if (blnAddItem)
					{
						if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_QualitySwap").Replace("{0}", objQuality.Name).Replace("{1}", objNewQuality.Name)))
							blnAddItem = false;
					}

					if (blnAddItem)
					{
						// Create the Karma expense.
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseSwapNegativeQuality").Replace("{0}", objQuality.DisplayNameShort).Replace("{1}", objNewQuality.DisplayNameShort), ExpenseType.Karma, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Karma -= intKarmaCost;
					}
				}
			}

			if (blnAddItem)
			{
				// Add the Quality to the appropriate parent node.
				treQualities.SelectedNode.Remove();
				if (objNewQuality.Type == QualityType.Positive)
				{
					treQualities.Nodes[0].Nodes.Add(objNode);
					treQualities.Nodes[0].Expand();
				}
				else
				{
					treQualities.Nodes[1].Nodes.Add(objNode);
					treQualities.Nodes[1].Expand();
				}

				// Add any created Weapons to the character.
				foreach (Weapon objWeapon in objWeapons)
					_objCharacter.Weapons.Add(objWeapon);

				// Create the Weapon Node if one exists.
				foreach (TreeNode objWeaponNode in objWeaponNodes)
				{
					objWeaponNode.ContextMenuStrip = cmsWeapon;
					treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
					treWeapons.Nodes[0].Expand();
				}

				// If the new Quality is linked to a Latent source, see if the Quality that is being swapped out is the same as the one the new Quality is linked to.
				// If so, set the character's OverrideSpecialAttributeEssenceLoss to true so that they always start with a Special Attribute value of 1.
				if (objXmlQuality["latentsource"] != null)
				{
					if (objXmlQuality["latentsource"].InnerText == objQuality.Name)
						_objCharacter.OverrideSpecialAttributeEssenceLoss = true;
				}

				// Add any additional Qualities that are forced on the character.
				if (objXmlQuality.SelectNodes("addqualities/addquality").Count > 0)
				{
					foreach (XmlNode objXmlAddQuality in objXmlQuality.SelectNodes("addqualities/addquality"))
					{
						XmlNode objXmlSelectedQuality = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objXmlAddQuality.InnerText + "\"]");
						string strForceValue = "";
						if (objXmlAddQuality.Attributes["select"] != null)
							strForceValue = objXmlAddQuality.Attributes["select"].InnerText;
						bool blnAddQuality = true;

						// Make sure the character does not yet have this Quality.
						foreach (Quality objCharacterQuality in _objCharacter.Qualities)
						{
							if (objCharacterQuality.Name == objXmlAddQuality.InnerText && objCharacterQuality.Extra == strForceValue)
							{
								blnAddQuality = false;
								break;
							}
						}

						if (blnAddQuality)
						{
							TreeNode objAddQualityNode = new TreeNode();
							List<Weapon> objAddWeapons = new List<Weapon>();
							List<TreeNode> objAddWeaponNodes = new List<TreeNode>();
							Quality objAddQuality = new Quality(_objCharacter);
							objAddQuality.Create(objXmlSelectedQuality, _objCharacter, QualitySource.Selected, objAddQualityNode, objWeapons, objWeaponNodes, strForceValue);

							if (objAddQuality.Type == QualityType.Positive)
							{
								treQualities.Nodes[0].Nodes.Add(objAddQualityNode);
								treQualities.Nodes[0].Expand();
							}
							else
							{
								treQualities.Nodes[1].Nodes.Add(objAddQualityNode);
								treQualities.Nodes[1].Expand();
							}
							_objCharacter.Qualities.Add(objAddQuality);

							// Add any created Weapons to the character.
							foreach (Weapon objWeapon in objAddWeapons)
								_objCharacter.Weapons.Add(objWeapon);

							// Create the Weapon Node if one exists.
							foreach (TreeNode objWeaponNode in objAddWeaponNodes)
							{
								objWeaponNode.ContextMenuStrip = cmsWeapon;
								treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
								treWeapons.Nodes[0].Expand();
							}
						}
					}
				}

				// Remove any Improvements for the old Quality.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);
				_objCharacter.Qualities.Remove(objQuality);

				// Remove any Weapons created by the old Quality if applicable.
				if (objQuality.WeaponID != Guid.Empty.ToString())
				{
					// Remove the Weapon from the TreeView.
					TreeNode objRemoveNode = new TreeNode();
					foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
					{
						if (objWeaponNode.Tag.ToString() == objQuality.WeaponID)
							objRemoveNode = objWeaponNode;
					}
					treWeapons.Nodes.Remove(objRemoveNode);

					// Remove the Weapon from the Character.
					Weapon objRemoveWeapon = new Weapon(_objCharacter);
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						if (objWeapon.InternalId == objQuality.WeaponID)
							objRemoveWeapon = objWeapon;
					}
					_objCharacter.Weapons.Remove(objRemoveWeapon);
				}

				// Add the new Quality to the character.
				_objCharacter.Qualities.Add(objNewQuality);
			}

			UpdateMentorSpirits();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddLocation_Click(object sender, EventArgs e)
		{
			// Add a new location to the Gear Tree.
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel || frmPickText.SelectedValue == "")
				return;

			string strLocation = frmPickText.SelectedValue;
			_objCharacter.Locations.Add(strLocation);

			TreeNode objLocation = new TreeNode();
			objLocation.Tag = strLocation;
			objLocation.Text = strLocation;
			objLocation.ContextMenuStrip = cmsGearLocation;
			treGear.Nodes.Add(objLocation);
			UpdateWindowTitle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddWeaponLocation_Click(object sender, EventArgs e)
		{
			// Add a new location to the Gear Tree.
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel || frmPickText.SelectedValue == "")
				return;

			string strLocation = frmPickText.SelectedValue;
			_objCharacter.WeaponLocations.Add(strLocation);

			TreeNode objLocation = new TreeNode();
			objLocation.Tag = strLocation;
			objLocation.Text = strLocation;
			objLocation.ContextMenuStrip = cmsWeaponLocation;
			treWeapons.Nodes.Add(objLocation);
			UpdateWindowTitle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddWeek_Click(object sender, EventArgs e)
		{
			CalendarWeek objWeek = new CalendarWeek();
			try
			{
				objWeek.Year = _objCharacter.Calendar.Last().Year;
				objWeek.Week = _objCharacter.Calendar.Last().Week;
				objWeek.Week++;
				if (objWeek.Week > 52)
				{
					objWeek.Week = 1;
					objWeek.Year++;
				}
			}
			catch
			{
				objWeek = new CalendarWeek();
				frmSelectCalendarStart frmPickStart = new frmSelectCalendarStart();
				frmPickStart.ShowDialog(this);

				if (frmPickStart.DialogResult == DialogResult.Cancel)
					return;

				objWeek.Year = frmPickStart.SelectedYear;
				objWeek.Week = frmPickStart.SelectedWeek;
			}

			_objCharacter.Calendar.Add(objWeek);

			PopulateCalendar();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdEditWeek_Click(object sender, EventArgs e)
		{
			try
			{
				ListViewItem objTest = lstCalendar.SelectedItems[0];
			}
			catch
			{
				return;
			}

			CalendarWeek objWeek = new CalendarWeek();
			ListViewItem objItem = lstCalendar.SelectedItems[0];

			// Find the selected Calendar Week.
			foreach (CalendarWeek objCharacterWeek in _objCharacter.Calendar)
			{
				if (objCharacterWeek.InternalId == objItem.SubItems[2].Text)
				{
					objWeek = objCharacterWeek;
					break;
				}
			}

			frmNotes frmItemNotes = new frmNotes();
			frmItemNotes.Notes = objWeek.Notes;
			string strOldValue = objWeek.Notes;
			frmItemNotes.ShowDialog(this);

			if (frmItemNotes.DialogResult == DialogResult.OK)
			{
				objWeek.Notes = frmItemNotes.Notes;
				if (objWeek.Notes != strOldValue)
				{
					_blnIsDirty = true;
					UpdateWindowTitle();
					PopulateCalendar();
				}
			}
		}

		private void cmdChangeStartWeek_Click(object sender, EventArgs e)
		{
			// Find the first date.
			CalendarWeek objStart;
			try
			{
				 objStart = _objCharacter.Calendar.First();
			}
			catch
			{
				return;
			}

			frmSelectCalendarStart frmPickStart = new frmSelectCalendarStart(objStart);
			frmPickStart.ShowDialog(this);

			if (frmPickStart.DialogResult == DialogResult.Cancel)
				return;

			// Determine the difference between the starting value and selected values for year and week.
			int intYear = frmPickStart.SelectedYear;
			int intWeek = frmPickStart.SelectedWeek;
			int intYearDiff = intYear - objStart.Year;
			int intWeekDiff = intWeek - objStart.Week;

			// Update each of the CalendarWeek entries for the character.
			foreach (CalendarWeek objWeek in _objCharacter.Calendar)
			{
				objWeek.Week += intWeekDiff;
				objWeek.Year += intYearDiff;

				// If the date range goes outside of 52 weeks, increase or decrease the year as necessary.
				if (objWeek.Week < 1)
				{
					objWeek.Year--;
					objWeek.Week += 52;
				}
				if (objWeek.Week > 52)
				{
					objWeek.Year++;
					objWeek.Week -= 52;
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
			PopulateCalendar();
		}

		private void cmdAddImprovement_Click(object sender, EventArgs e)
		{
			frmCreateImprovement frmPickImprovement = new frmCreateImprovement(_objCharacter);
			frmPickImprovement.ShowDialog(this);
			
			if (frmPickImprovement.DialogResult == DialogResult.Cancel)
				return;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdCreateStackedFocus_Click(object sender, EventArgs e)
		{
			int intFree = 0;
			List<Gear> lstGear = new List<Gear>();
			List<Gear> lstStack = new List<Gear>();

			// Run through all of the Foci the character has and count the un-Bonded ones.
			foreach (Gear objGear in _objCharacter.Gear)
			{
				if (objGear.Category == "Foci" || objGear.Category == "Metamagic Foci")
				{
					if (!objGear.Bonded)
					{
						intFree++;
						lstGear.Add(objGear);
					}
				}
			}

			// If the character does not have at least 2 un-Bonded Foci, display an error and leave.
			if (intFree < 2)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotStackFoci"), LanguageManager.Instance.GetString("MessageTitle_CannotStackFoci"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectItem frmPickItem = new frmSelectItem();

			// Let the character select the Foci they'd like to stack, stopping when they either click Cancel or there are no more items left in the list.
			do
			{
				frmPickItem.Gear = lstGear;
				frmPickItem.AllowAutoSelect = false;
				frmPickItem.Description = LanguageManager.Instance.GetString("String_SelectItemFocus");
				frmPickItem.ShowDialog(this);

				if (frmPickItem.DialogResult == DialogResult.OK)
				{
					// Move the item from the Gear list to the Stack list.
					foreach (Gear objGear in lstGear)
					{
						if (objGear.InternalId == frmPickItem.SelectedItem)
						{
							objGear.Bonded = true;
							lstStack.Add(objGear);
							lstGear.Remove(objGear);
							break;
						}
					}
				}
			} while (lstGear.Count > 0 && frmPickItem.DialogResult != DialogResult.Cancel);

			// Make sure at least 2 Foci were selected.
			if (lstStack.Count < 2)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_StackedFocusMinimum"), LanguageManager.Instance.GetString("MessageTitle_CannotStackFoci"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure the combined Force of the Foci do not exceed 6.
			if (!_objOptions.AllowHigherStackedFoci)
			{
				int intCombined = 0;
				foreach (Gear objGear in lstStack)
					intCombined += objGear.Rating;
				if (intCombined > 6)
				{
					foreach (Gear objGear in lstStack)
						objGear.Bonded = false;
					MessageBox.Show(LanguageManager.Instance.GetString("Message_StackedFocusForce"), LanguageManager.Instance.GetString("MessageTitle_CannotStackFoci"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}

			// Create the Stacked Focus.
			StackedFocus objStack = new StackedFocus(_objCharacter);
			objStack.Gear = lstStack;
			_objCharacter.StackedFoci.Add(objStack);

			// Remove the Gear from the character and replace it with a Stacked Focus item.
			int intCost = 0;
			foreach (Gear objGear in lstStack)
			{
				intCost += objGear.TotalCost;
				_objCharacter.Gear.Remove(objGear);

				// Remove the TreeNode from Gear.
				foreach (TreeNode nodRoot in treGear.Nodes)
				{
					foreach (TreeNode nodItem in nodRoot.Nodes)
					{
						if (nodItem.Tag.ToString() == objGear.InternalId)
						{
							nodRoot.Nodes.Remove(nodItem);
							break;
						}
					}
				}
			}

			Gear objStackItem = new Gear(_objCharacter);
			objStackItem.Category = "Stacked Focus";
			objStackItem.Name = "Stacked Focus: " + objStack.Name;
			objStackItem.MinRating = 0;
			objStackItem.MaxRating = 0;
			objStackItem.Source = "SM";
			objStackItem.Page = "84";
			objStackItem.Cost = intCost.ToString();
			objStackItem.Avail = "0";

			TreeNode nodStackNode = new TreeNode();
			nodStackNode.Text = objStackItem.DisplayNameShort;
			nodStackNode.Tag = objStackItem.InternalId;

			treGear.Nodes[0].Nodes.Add(nodStackNode);

			_objCharacter.Gear.Add(objStackItem);

			objStack.GearId = objStackItem.InternalId;

			_blnIsDirty = true;
			_objController.PopulateFocusList(treFoci);
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void cmdBurnStreetCred_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(LanguageManager.Instance.GetString("Message_BurnStreetCred"), LanguageManager.Instance.GetString("MessageTitle_BurnStreetCred"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				return;

			_objCharacter.BurntStreetCred += 2;
			
			_blnIsDirty = true;
			UpdateWindowTitle();
			UpdateReputation();
		}

		private void cmdEditImprovement_Click(object sender, EventArgs e)
		{
			treImprovements_DoubleClick(sender, e);
		}

		private void cmdDeleteImprovement_Click(object sender, EventArgs e)
		{
			try
			{
				if (treImprovements.SelectedNode.Level == 0)
				{
					if (treImprovements.SelectedNode.Text == LanguageManager.Instance.GetString("Node_SelectedImprovements"))
						return;

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteImprovementGroup")))
						return;

					// Move all of the child nodes in the current parent to the Selected Improvements parent node.
					foreach (TreeNode objNode in treImprovements.SelectedNode.Nodes)
					{
						Improvement objImprovement = new Improvement();
						foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
						{
							if (objCharacterImprovement.CustomGroup == treImprovements.SelectedNode.Text)
							{
								objImprovement = objCharacterImprovement;
								break;
							}
						}

						// Change the Location for the Armor.
						objImprovement.CustomGroup = "";

						TreeNode nodNewNode = new TreeNode();
						nodNewNode.Text = objNode.Text;
						nodNewNode.Tag = objNode.Tag;

						treImprovements.Nodes[0].Nodes.Add(nodNewNode);
						treImprovements.Nodes[0].Expand();
					}

					// Remove the Group from the character, then remove the selected node.
					_objCharacter.ImprovementGroups.Remove(treImprovements.SelectedNode.Text);
					treImprovements.SelectedNode.Remove();
					return;
				}
				if (treImprovements.SelectedNode.Level > 0)
				{
					Improvement objImprovement = new Improvement();
					foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
					{
						if (objCharacterImprovement.SourceName == treImprovements.SelectedNode.Tag.ToString())
						{
							objImprovement = objCharacterImprovement;
							break;
						}
					}

					if (!_objFunctions.ConfirmDelete(LanguageManager.Instance.GetString("Message_DeleteImprovement")))
						return;

					// Remove the Improvement from the character.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Custom, objImprovement.SourceName);
					UpdateCharacterInfo();

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}
			catch
			{
			}
		}

		private void cmdAddArmorBundle_Click(object sender, EventArgs e)
		{
			// Add a new location to the Armor Tree.
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel || frmPickText.SelectedValue == "")
				return;

			string strLocation = frmPickText.SelectedValue;
			_objCharacter.ArmorBundles.Add(strLocation);

			TreeNode objLocation = new TreeNode();
			objLocation.Tag = strLocation;
			objLocation.Text = strLocation;
			objLocation.ContextMenuStrip = cmsArmorLocation;
			treArmor.Nodes.Add(objLocation);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdArmorEquipAll_Click(object sender, EventArgs e)
		{
			// Equip all of the Armor in the Armor Bundle.
			foreach (Armor objArmor in _objCharacter.Armor)
			{
				if (objArmor.Location == treArmor.SelectedNode.Tag.ToString() || (treArmor.SelectedNode == treArmor.Nodes[0] && objArmor.Location == ""))
				{
					objArmor.Equipped = true;
					// Add the Armor's Improevments to the character.
					if (objArmor.Bonus != null)
						_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId, objArmor.Bonus, false, 1, objArmor.DisplayNameShort);
					// Add the Improvements from any Armor Mods in the Armor.
					foreach (ArmorMod objMod in objArmor.ArmorMods)
					{
						if (objMod.Bonus != null && objMod.Equipped)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId, objMod.Bonus, false, objMod.Rating, objMod.DisplayNameShort);
					}
					// Add the Improvements from any Gear in the Armor.
					foreach (Gear objGear in objArmor.Gear)
					{
						if (objGear.Bonus != null && objGear.Equipped)
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objGear.Bonus, false, objGear.Rating, objGear.DisplayNameShort);
					}
				}
			}
			RefreshSelectedArmor();

			_blnIsDirty = true;
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void cmdArmorUnEquipAll_Click(object sender, EventArgs e)
		{
			// Un-equip all of the Armor in the Armor Bundle.
			foreach (Armor objArmor in _objCharacter.Armor)
			{
				if (objArmor.Location == treArmor.SelectedNode.Tag.ToString() || (treArmor.SelectedNode == treArmor.Nodes[0] && objArmor.Location == ""))
				{
					objArmor.Equipped = false;
					// Remove any Improvements the Armor created.
					if (objArmor.Bonus != null)
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);
					// Remove any Improvements from any Armor Mods in the Armor.
					foreach (ArmorMod objMod in objArmor.ArmorMods)
					{
						if (objMod.Bonus != null)
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
					}
					// Remove any Improvements from any Gear in the Armor.
					foreach (Gear objGear in objArmor.Gear)
					{
						if (objGear.Bonus != null)
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
					}
				}
			}
			RefreshSelectedArmor();

			_blnIsDirty = true;
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void cmdImprovementsEnableAll_Click(object sender, EventArgs e)
		{
			// Enable all of the Improvements in the Improvement Group.
			try
			{
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.CustomGroup == treImprovements.SelectedNode.Tag.ToString() || (treImprovements.SelectedNode == treImprovements.Nodes[0] && objImprovement.CustomGroup == ""))
						objImprovement.Enabled = true;
				}
			}
			catch
			{
			}

			_blnIsDirty = true;
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void cmdImprovementsDisableAll_Click(object sender, EventArgs e)
		{
			// Disable all of the Improvements in the Improvement Group.
			try
			{
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.CustomGroup == treImprovements.SelectedNode.Tag.ToString() || (treImprovements.SelectedNode == treImprovements.Nodes[0] && objImprovement.CustomGroup == ""))
						objImprovement.Enabled = false;
				}
			}
			catch
			{
			}

			_blnIsDirty = true;
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void cmdRollSpell_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblSpellDicePool.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdRollDrain_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblDrainAttributesValue.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdRollComplexForm_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblComplexFormDicePool.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdRollFading_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblFadingAttributesValue.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdRollWeapon_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblWeaponDicePool.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdRollVehicleWeapon_Click(object sender, EventArgs e)
		{
			int intDice = 0;
			try
			{
				intDice = Convert.ToInt32(lblVehicleWeaponDicePool.Text);
			}
			catch
			{
			}
			DiceRollerOpenedInt(_objCharacter, intDice);
		}

		private void cmdAddVehicleLocation_Click(object sender, EventArgs e)
		{
			// Make sure a Vehicle is selected.
			Vehicle objVehicle = new Vehicle(_objCharacter);
			try
			{
				if (treVehicles.SelectedNode.Level == 1)
				{
					objVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicleLocation"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicleLocation"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Add a new location to the selected Vehicle.
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel || frmPickText.SelectedValue == "")
				return;

			string strLocation = frmPickText.SelectedValue;
			objVehicle.Locations.Add(strLocation);

			TreeNode objLocation = new TreeNode();
			objLocation.Tag = strLocation;
			objLocation.Text = strLocation;
			objLocation.ContextMenuStrip = cmsVehicleLocation;
			treVehicles.SelectedNode.Nodes.Add(objLocation);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdAddPet_Click(object sender, EventArgs e)
		{
			Contact objContact = new Contact(_objCharacter);
			objContact.EntityType = ContactType.Pet;
			_objCharacter.Contacts.Add(objContact);

			PetControl objContactControl = new PetControl();
			objContactControl.ContactObject = objContact;

			// Attach an EventHandler for the DeleteContact and FileNameChanged Events.
			objContactControl.DeleteContact += objPet_DeleteContact;
			objContactControl.FileNameChanged += objPet_FileNameChanged;

			// Add the control to the Panel.
			panPets.Controls.Add(objContactControl);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdQuickenSpell_Click(object sender, EventArgs e)
		{
			try
			{
				if (treSpells.SelectedNode.Level != 1)
					return;
			}
			catch
			{
				return;
			}

			frmSelectNumber frmPickNumber = new frmSelectNumber();
			frmPickNumber.Description = LanguageManager.Instance.GetString("String_QuickeningKarma").Replace("{0}", treSpells.SelectedNode.Text);
			frmPickNumber.Minimum = 1;
			frmPickNumber.ShowDialog(this);

			if (frmPickNumber.DialogResult == DialogResult.Cancel)
				return;

			// Make sure the character has enough Karma to improve the Attribute.
			int intKarmaCost = frmPickNumber.SelectedValue;
			if (intKarmaCost > _objCharacter.Karma)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseQuickeningMetamagic").Replace("{0}", intKarmaCost.ToString()).Replace("{1}", treSpells.SelectedNode.Text)))
				return;

			// Create the Karma expense.
			ExpenseLogEntry objExpense = new ExpenseLogEntry();
			objExpense.Create(intKarmaCost * -1, LanguageManager.Instance.GetString("String_ExpenseQuickenMetamagic") + " " + treSpells.SelectedNode.Text, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objExpense);
			_objCharacter.Karma -= intKarmaCost;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.QuickeningMetamagic, "");
			objExpense.Undo = objUndo;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region ContextMenu Events
		private void ContextMenu_Opening(object sender, CancelEventArgs e)
		{
			foreach (ToolStripItem objItem in ((ContextMenuStrip)sender).Items)
			{
				if (objItem.Tag != null)
					objItem.Text = LanguageManager.Instance.GetString(objItem.Tag.ToString());
			}
		}

		private void ContextMenu_DropDownOpening(object sender, EventArgs e)
		{
			foreach (ToolStripItem objItem in ((ToolStripDropDownItem)sender).DropDownItems)
			{
				if (objItem.Tag != null)
					objItem.Text = LanguageManager.Instance.GetString(objItem.Tag.ToString());
			}
		}

		private void tsCyberwareAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Cyberware window.
			try
			{
				if (treCyberware.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectCyberware"), LanguageManager.Instance.GetString("MessageTitle_SelectCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectCyberware"), LanguageManager.Instance.GetString("MessageTitle_SelectCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treCyberware.SelectedNode.Parent == treCyberware.Nodes[1])
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectCyberware"), LanguageManager.Instance.GetString("MessageTitle_SelectCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			bool blnAddAgain = PickCyberware();
			if (blnAddAgain)
			{
				treCyberware.SelectedNode = treCyberware.SelectedNode.Parent;
				tsCyberwareAddAsPlugin_Click(sender, e);
			}
		}

		private void tsWeaponAddAccessory_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponAccessory"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponAccessory"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Locate the Weapon that is selected in the Tree.
			Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			// Accessories cannot be added to Cyberweapons.
			if (objWeapon.Category.StartsWith("Cyberware"))
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CyberweaponNoAccessory"), LanguageManager.Instance.GetString("MessageTitle_CyberweaponNoAccessory"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Open the Weapons XML file and locate the selected Weapon.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + objWeapon.Name + "\"]");

			frmSelectWeaponAccessory frmPickWeaponAccessory = new frmSelectWeaponAccessory(_objCharacter, true);

			if (objXmlWeapon == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			// Make sure the Weapon allows Accessories to be added to it.
			if (!Convert.ToBoolean(objXmlWeapon["allowaccessory"].InnerText))
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else
			{
				XmlNodeList objXmlMountList = objXmlWeapon.SelectNodes("accessorymounts/mount");
				string strMounts = "";
				foreach (XmlNode objXmlMount in objXmlMountList)
					strMounts += objXmlMount.InnerText + "/";
				
				frmPickWeaponAccessory.AllowedMounts = strMounts;
			}

			frmPickWeaponAccessory.WeaponCost = objWeapon.Cost;
			frmPickWeaponAccessory.AccessoryMultiplier = objWeapon.AccessoryMultiplier;
			frmPickWeaponAccessory.ShowDialog();

			if (frmPickWeaponAccessory.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected piece.
			objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/accessories/accessory[name = \"" + frmPickWeaponAccessory.SelectedAccessory + "\"]");

			TreeNode objNode = new TreeNode();
			WeaponAccessory objAccessory = new WeaponAccessory(_objCharacter);
			objAccessory.Create(objXmlWeapon, objNode, frmPickWeaponAccessory.SelectedMount);
			objAccessory.Parent = objWeapon;

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objWeapon.TotalCost;
			objWeapon.WeaponAccessories.Add(objAccessory);

			int intCost = objWeapon.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickWeaponAccessory.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeaponAccessory.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objAccessory.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objAccessory.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickWeaponAccessory.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objWeapon.WeaponAccessories.Remove(objAccessory);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeaponAccessory.AddAgain)
						tsWeaponAddAccessory_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponAccessory") + " " + objAccessory.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponAccessory, objAccessory.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsWeaponAccessory;
			treWeapons.SelectedNode.Nodes.Add(objNode);
			treWeapons.SelectedNode.Expand();

			UpdateCharacterInfo();
			RefreshSelectedWeapon();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickWeaponAccessory.AddAgain)
				tsWeaponAddAccessory_Click(sender, e);
		}

		private void tsWeaponAddModification_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Locate the Weapon that is selected in the Tree.
			Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			frmSelectVehicleMod frmPickVehicleMod = new frmSelectVehicleMod(_objCharacter, true);

			// Make sure the Weapon allows Modifications to be added to it.
			// Open the Weapons XML file and locate the selected Weapon.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");
			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + objWeapon.Name + "\"]");

			if (objXmlWeapon == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (objXmlWeapon["allowmod"] != null)
			{
				if (objXmlWeapon["allowmod"].InnerText == "false")
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}

			// Set the Weapon properties for the window.
			frmPickVehicleMod.WeaponCost = objWeapon.Cost;
			frmPickVehicleMod.TotalWeaponCost = objWeapon.TotalCost;
			frmPickVehicleMod.ModMultiplier = objWeapon.ModMultiplier;
			frmPickVehicleMod.InputFile = "weapons";

			frmPickVehicleMod.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickVehicleMod.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected piece.
			XmlNode objXmlMod = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + frmPickVehicleMod.SelectedMod + "\"]");

			TreeNode objNode = new TreeNode();
			WeaponMod objMod = new WeaponMod(_objCharacter);
			objMod.Create(objXmlMod, objNode);
			objMod.Rating = frmPickVehicleMod.SelectedRating;
			objMod.Parent = objWeapon;

			// Do not allow the user to add a new Weapon Mod if the Weapon's Capacity has been reached.
			if (_objOptions.EnforceCapacity && objWeapon.SlotsRemaining - objMod.Slots < 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				if (frmPickVehicleMod.AddAgain)
					tsWeaponAddModification_Click(sender, e);
				return;
			}

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objWeapon.TotalCost;
			objWeapon.WeaponMods.Add(objMod);

			int intCost = objWeapon.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickVehicleMod.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickVehicleMod.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickVehicleMod.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objWeapon.WeaponMods.Remove(objMod);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickVehicleMod.AddAgain)
						tsWeaponAddModification_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponMod, objMod.InternalId);
					objExpense.Undo = objUndo;
				}
			}
			
			objNode.Text = objMod.DisplayName;
			objNode.ContextMenuStrip = cmsWeaponMod;

			treWeapons.SelectedNode.Nodes.Add(objNode);
			treWeapons.SelectedNode.Expand();

			UpdateCharacterInfo();
			RefreshSelectedWeapon();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickVehicleMod.AddAgain)
				tsWeaponAddModification_Click(sender, e);
		}

		private void tsAddArmorMod_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treArmor.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treArmor.SelectedNode.Level > 1)
				treArmor.SelectedNode = treArmor.SelectedNode.Parent;

			// Locate the Armor that is selected in the tree.
			Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);

			// Open the Armor XML file and locate the selected Armor.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("armor.xml");

			XmlNode objXmlArmor = objXmlDocument.SelectSingleNode("/chummer/armors/armor[name = \"" + objArmor.Name + "\"]");

			frmSelectArmorMod frmPickArmorMod = new frmSelectArmorMod(_objCharacter, true);
			frmPickArmorMod.ArmorCost = objArmor.Cost;
			frmPickArmorMod.AllowedCategories = objArmor.Category + "," + objArmor.Name;
			frmPickArmorMod.CapacityDisplayStyle = objArmor.CapacityDisplayStyle;
			if (objXmlArmor.InnerXml.Contains("<addmodcategory>"))
				frmPickArmorMod.AllowedCategories += "," + objXmlArmor["addmodcategory"].InnerText;

			frmPickArmorMod.ShowDialog(this);

			if (frmPickArmorMod.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected piece.
			objXmlArmor = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + frmPickArmorMod.SelectedArmorMod + "\"]");

			TreeNode objNode = new TreeNode();
			ArmorMod objMod = new ArmorMod(_objCharacter);
			List<Weapon> lstWeapons = new List<Weapon>();
			List<TreeNode> lstWeaponNodes = new List<TreeNode>();
			int intRating = 0;
			if (Convert.ToInt32(objXmlArmor["maxrating"].InnerText) > 1)
				intRating = frmPickArmorMod.SelectedRating;

			objMod.Create(objXmlArmor, objNode, intRating, lstWeapons, lstWeaponNodes);
			objMod.Parent = objArmor;
			objNode.ContextMenuStrip = cmsArmorMod;
			if (objMod.InternalId == Guid.Empty.ToString())
				return;

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objArmor.TotalCost;
			objArmor.ArmorMods.Add(objMod);

			// Do not allow the user to add a new piece of Armor if its Capacity has been reached.
			if (_objOptions.EnforceCapacity && objArmor.CapacityRemaining < 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				objArmor.ArmorMods.Remove(objMod);
				if (frmPickArmorMod.AddAgain)
					tsAddArmorMod_Click(sender, e);
				return;
			}

			int intCost = objArmor.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickArmorMod.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickArmorMod.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickArmorMod.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objArmor.ArmorMods.Remove(objMod);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					// Remove the Improvements created by the Armor Mod.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
					if (frmPickArmorMod.AddAgain)
						tsAddArmorMod_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseArmorMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddArmorMod, objMod.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			treArmor.SelectedNode.Nodes.Add(objNode);
			treArmor.SelectedNode.Expand();
			treArmor.SelectedNode = objNode;

			// Add any Weapons created by the Mod.
			foreach (Weapon objWeapon in lstWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in lstWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeapon;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			UpdateCharacterInfo();
			RefreshSelectedArmor();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickArmorMod.AddAgain)
				tsAddArmorMod_Click(sender, e);
		}

		private void tsGearAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treGear.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			bool blnAddAgain = PickGear();
			if (blnAddAgain)
				tsGearAddAsPlugin_Click(sender, e);
		}

		private void tsVehicleAddMod_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Vehicle Mod window.
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treVehicles.SelectedNode.Level > 1)
				treVehicles.SelectedNode = treVehicles.SelectedNode.Parent;

			Vehicle objSelectedVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmSelectVehicleMod frmPickVehicleMod = new frmSelectVehicleMod(_objCharacter, true);
			// Set the Vehicle properties for the window.
			frmPickVehicleMod.VehicleCost = Convert.ToInt32(objSelectedVehicle.Cost);
			frmPickVehicleMod.Body = objSelectedVehicle.Body;
			frmPickVehicleMod.Speed = objSelectedVehicle.Speed;
			frmPickVehicleMod.Accel = objSelectedVehicle.Accel;
			frmPickVehicleMod.DeviceRating = objSelectedVehicle.DeviceRating;
			frmPickVehicleMod.HasModularElectronics = objSelectedVehicle.HasModularElectronics();

			frmPickVehicleMod.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickVehicleMod.DialogResult == DialogResult.Cancel)
				return;

			// Open the Vehicles XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");
			
			XmlNode objXmlMod = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + frmPickVehicleMod.SelectedMod + "\"]");

			TreeNode objNode = new TreeNode();
			VehicleMod objMod = new VehicleMod(_objCharacter);
			objMod.Create(objXmlMod, objNode, frmPickVehicleMod.SelectedRating);

			// Make sure that the Armor Rating does not exceed the maximum allowed by the Vehicle.
			if (objMod.Name.StartsWith("Armor"))
			{
				if (objMod.Rating > objSelectedVehicle.MaxArmor)
				{
					objMod.Rating = objSelectedVehicle.MaxArmor;
					objNode.Text = objMod.DisplayName;
				}
			}

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objSelectedVehicle.TotalCost;
			objSelectedVehicle.Mods.Add(objMod);

			// Do not allow the user to add a new Vehicle Mod if the Vehicle's Capacity has been reached.
			if (_objOptions.EnforceCapacity && objSelectedVehicle.Slots < objSelectedVehicle.SlotsUsed)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				objSelectedVehicle.Mods.Remove(objMod);
				if (frmPickVehicleMod.AddAgain)
					tsVehicleAddMod_Click(sender, e);
				return;
			}

			int intCost = objSelectedVehicle.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickVehicleMod.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickVehicleMod.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickVehicleMod.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objSelectedVehicle.Mods.Remove(objMod);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickVehicleMod.AddAgain)
						tsVehicleAddMod_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleMod, objMod.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsVehicle;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();
			RefreshSelectedVehicle();

			// Check for Improved Sensor bonus.
			if (objMod.Bonus != null)
			{
				if (objMod.Bonus["selecttext"] != null)
				{
					frmSelectText frmPickText = new frmSelectText();
					frmPickText.Description = LanguageManager.Instance.GetString("String_Improvement_SelectText").Replace("{0}", objMod.DisplayNameShort);
					frmPickText.ShowDialog(this);
					objMod.Extra = frmPickText.SelectedValue;
					objNode.Text = objMod.DisplayName;
				}
				if (objMod.Bonus["improvesensor"] != null)
				{
					ChangeVehicleSensor(objSelectedVehicle, true);
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickVehicleMod.AddAgain)
				tsVehicleAddMod_Click(sender, e);
		}

		private void tsVehicleAddWeaponWeapon_Click(object sender, EventArgs e)
		{
			VehicleMod objMod = new VehicleMod(_objCharacter);

			// Make sure that a Weapon Mount has been selected.
			try
			{
				// Attempt to locate the selected VehicleMod.
				Vehicle objFoundVehicle = new Vehicle(_objCharacter);
				objMod = _objFunctions.FindVehicleMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);

				if (!objMod.Name.StartsWith("Weapon Mount") && !objMod.Name.StartsWith("Mechanical Arm"))
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotAddWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotAddWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotAddWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotAddWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectWeapon frmPickWeapon = new frmSelectWeapon(_objCharacter, true);
			frmPickWeapon.ShowDialog();

			if (frmPickWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Open the Weapons XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + frmPickWeapon.SelectedWeapon + "\"]");

			TreeNode objNode = new TreeNode();
			Weapon objWeapon = new Weapon(_objCharacter);
			objWeapon.Create(objXmlWeapon, _objCharacter, objNode, cmsVehicleWeapon, cmsVehicleWeaponAccessory, cmsVehicleWeaponMod);
			objWeapon.VehicleMounted = true;

			int intCost = objWeapon.TotalCost;
			// Apply a markup if applicable.
			if (frmPickWeapon.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeapon.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickWeapon.FreeCost)
			{
				// Check the item's Cost and make sure the character can afford it.
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeapon.AddAgain)
						tsVehicleAddWeaponWeapon_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleWeapon, objWeapon.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objMod.Weapons.Add(objWeapon);

			objNode.ContextMenuStrip = cmsVehicleWeapon;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			if (frmPickWeapon.AddAgain)
				tsVehicleAddWeaponWeapon_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleAddWeaponAccessory_Click(object sender, EventArgs e)
		{
			// Attempt to locate the selected VehicleWeapon.
			bool blnWeaponFound = false;
			Vehicle objFoundVehicle = new Vehicle(_objCharacter);
			Weapon objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
			if (objWeapon != null)
				blnWeaponFound = true;

			if (!blnWeaponFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_VehicleWeaponAccessories"), LanguageManager.Instance.GetString("MessageTitle_VehicleWeaponAccessories"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Open the Weapons XML file and locate the selected Weapon.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + treVehicles.SelectedNode.Text + "\"]");

			frmSelectWeaponAccessory frmPickWeaponAccessory = new frmSelectWeaponAccessory(_objCharacter, true);

			if (objXmlWeapon == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure the Weapon allows Accessories to be added to it.
			if (!Convert.ToBoolean(objXmlWeapon["allowaccessory"].InnerText))
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else
			{
				XmlNodeList objXmlMountList = objXmlWeapon.SelectNodes("accessorymounts/mount");
				string strMounts = "";
				foreach (XmlNode objXmlMount in objXmlMountList)
				{
					// Run through the Weapon's currenct Accessories and filter out any used up Mount points.
					bool blnFound = false;
					foreach (WeaponAccessory objCurrentAccessory in objWeapon.WeaponAccessories)
					{
						if (objCurrentAccessory.Mount == objXmlMount.InnerText)
							blnFound = true;
					}
					if (!blnFound)
						strMounts += objXmlMount.InnerText + "/";
				}
				frmPickWeaponAccessory.AllowedMounts = strMounts;
			}

			frmPickWeaponAccessory.WeaponCost = objWeapon.Cost;
			frmPickWeaponAccessory.AccessoryMultiplier = objWeapon.AccessoryMultiplier;
			frmPickWeaponAccessory.ShowDialog();

			if (frmPickWeaponAccessory.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected piece.
			objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/accessories/accessory[name = \"" + frmPickWeaponAccessory.SelectedAccessory + "\"]");

			TreeNode objNode = new TreeNode();
			WeaponAccessory objAccessory = new WeaponAccessory(_objCharacter);
			objAccessory.Create(objXmlWeapon, objNode, frmPickWeaponAccessory.SelectedMount);
			objAccessory.Parent = objWeapon;

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objWeapon.TotalCost;
			objWeapon.WeaponAccessories.Add(objAccessory);

			int intCost = objWeapon.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickWeaponAccessory.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeaponAccessory.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objAccessory.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objAccessory.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickWeaponAccessory.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objWeapon.WeaponAccessories.Remove(objAccessory);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeaponAccessory.AddAgain)
						tsVehicleAddWeaponAccessory_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleWeaponAccessory") + " " + objAccessory.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleWeaponAccessory, objAccessory.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsVehicleWeaponAccessory;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			if (frmPickWeaponAccessory.AddAgain)
				tsVehicleAddWeaponAccessory_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleAddWeaponModification_Click(object sender, EventArgs e)
		{
			// Attempt to locate the selected VehicleWeapon.
			bool blnFound = false;
			Vehicle objFoundVehicle = new Vehicle(_objCharacter);
			Weapon objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
			if (objWeapon != null)
				blnFound = true;

			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_VehicleWeaponMods"), LanguageManager.Instance.GetString("MessageTitle_VehicleWeaponMods"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectVehicleMod frmPickVehicleMod = new frmSelectVehicleMod(_objCharacter, true);

			// Make sure the Weapon allows Modifications to be added to it.
			// Open the Weapons XML file and locate the selected Weapon.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");
			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + objWeapon.Name + "\"]");

			if (objXmlWeapon == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (objXmlWeapon["allowmod"] != null)
			{
				if (objXmlWeapon["allowmod"].InnerText == "false")
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotModifyWeaponMod"), LanguageManager.Instance.GetString("MessageTitle_CannotModifyWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}

			// Set the Weapon properties for the window.
			frmPickVehicleMod.WeaponCost = objWeapon.Cost;
			frmPickVehicleMod.TotalWeaponCost = objWeapon.TotalCost;
			frmPickVehicleMod.ModMultiplier = objWeapon.ModMultiplier;
			frmPickVehicleMod.InputFile = "weapons";

			frmPickVehicleMod.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickVehicleMod.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected piece.
			XmlNode objXmlMod = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + frmPickVehicleMod.SelectedMod + "\"]");

			TreeNode objNode = new TreeNode();
			WeaponMod objMod = new WeaponMod(_objCharacter);
			objMod.Create(objXmlMod, objNode);
			objMod.Rating = frmPickVehicleMod.SelectedRating;
			objMod.Parent = objWeapon;

			// Do not allow the user to add a new piece of Cyberware if its Capacity has been reached.
			if (_objOptions.EnforceCapacity && objWeapon.SlotsRemaining - objMod.Slots < 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				if (frmPickVehicleMod.AddAgain)
					tsVehicleAddWeaponModification_Click(sender, e);
				return;
			}

			// Check the item's Cost and make sure the character can afford it.
			int intOriginalCost = objWeapon.TotalCost;
			objWeapon.WeaponMods.Add(objMod);

			int intCost = objWeapon.TotalCost - intOriginalCost;
			// Apply a markup if applicable.
			if (frmPickVehicleMod.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickVehicleMod.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objMod.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			if (!frmPickVehicleMod.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					objWeapon.WeaponMods.Remove(objMod);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickVehicleMod.AddAgain)
						tsVehicleAddWeaponModification_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleWeaponMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleWeaponMod, objMod.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objNode.Text = objMod.DisplayName;
			objNode.ContextMenuStrip = cmsVehicleWeaponMod;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			if (frmPickVehicleMod.AddAgain)
				tsVehicleAddWeaponModification_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleAddUnderbarrelWeapon_Click(object sender, EventArgs e)
		{
			// Attempt to locate the selected VehicleWeapon.
			bool blnFound = false;
			Vehicle objFoundVehicle = new Vehicle(_objCharacter);
			Weapon objSelectedWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
			if (objSelectedWeapon != null)
				blnFound = true;

			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_VehicleWeaponUnderbarrel"), LanguageManager.Instance.GetString("MessageTitle_VehicleWeaponUnderbarrel"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectWeapon frmPickWeapon = new frmSelectWeapon(_objCharacter, true);
			frmPickWeapon.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Open the Weapons XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + frmPickWeapon.SelectedWeapon + "\"]");

			TreeNode objNode = new TreeNode();
			Weapon objWeapon = new Weapon(_objCharacter);
			objWeapon.Create(objXmlWeapon, _objCharacter, objNode, cmsWeapon, cmsWeaponAccessory, cmsWeapon);
			objWeapon.VehicleMounted = true;
			objWeapon.IsUnderbarrelWeapon = true;

			int intCost = objWeapon.TotalCost;
			// Apply a markup if applicable.
			if (frmPickWeapon.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeapon.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickWeapon.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeapon.AddAgain)
						cmdAddWeapon_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleWeapon, objWeapon.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objSelectedWeapon.UnderbarrelWeapons.Add(objWeapon);

			objNode.ContextMenuStrip = cmsVehicleWeapon;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleAddWeaponAccessoryAlt_Click(object sender, EventArgs e)
		{
			tsVehicleAddWeaponAccessory_Click(sender, e);
		}

		private void tsVehicleAddWeaponModificationAlt_Click(object sender, EventArgs e)
		{
			tsVehicleAddWeaponModification_Click(sender, e);
		}

		private void tsVehicleAddUnderbarrelWeaponAlt_Click(object sender, EventArgs e)
		{
			tsVehicleAddUnderbarrelWeapon_Click(sender, e);
		}

		private void tsMartialArtsAddAdvantage_Click(object sender, EventArgs e)
		{
			try
			{
				// Select the Martial Arts node if we're currently on a child.
				if (treMartialArts.SelectedNode.Level > 1)
					treMartialArts.SelectedNode = treMartialArts.SelectedNode.Parent;

				MartialArt objMartialArt = _objFunctions.FindMartialArt(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts);

				// Make sure the user is not trying to add more Advantages than they are allowed (1 per Rating for the selected Martial Art).
				if (objMartialArt.Advantages.Count >= objMartialArt.Rating && !_objCharacter.IgnoreRules)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_MartialArtAdvantageLimit").Replace("{0}", objMartialArt.DisplayNameShort), LanguageManager.Instance.GetString("MessageTitle_MartialArtAdvantageLimit"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				frmSelectMartialArtAdvantage frmPickMartialArtAdvantage = new frmSelectMartialArtAdvantage(_objCharacter);
				frmPickMartialArtAdvantage.MartialArt = objMartialArt.Name;
				frmPickMartialArtAdvantage.ShowDialog(this);

				if (frmPickMartialArtAdvantage.DialogResult == DialogResult.Cancel)
					return;

				// Open the Martial Arts XML file and locate the selected piece.
				XmlDocument objXmlDocument = XmlManager.Instance.Load("martialarts.xml");

                XmlNode objXmlAdvantage = objXmlDocument.SelectSingleNode("/chummer/martialarts/martialart[name = \"" + objMartialArt.Name + "\"]/techniques/technique[name = \"" + frmPickMartialArtAdvantage.SelectedAdvantage + "\"]");

				// Create the Improvements for the Advantage if there are any.
				TreeNode objNode = new TreeNode();
				MartialArtAdvantage objAdvantage = new MartialArtAdvantage(_objCharacter);
				objAdvantage.Create(objXmlAdvantage, _objCharacter, objNode);
				if (objAdvantage.InternalId == Guid.Empty.ToString())
					return;

				objMartialArt.Advantages.Add(objAdvantage);

				treMartialArts.SelectedNode.Nodes.Add(objNode);
				treMartialArts.SelectedNode.Expand();

				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectMartialArtAdvantage"), LanguageManager.Instance.GetString("MessageTitle_SelectMartialArtAdvantage"), MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void tsVehicleAddGear_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectGearVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectGearVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treVehicles.SelectedNode.Level > 1)
				treVehicles.SelectedNode = treVehicles.SelectedNode.Parent;

			// Locate the selected Vehicle.
			Vehicle objSelectedVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			frmPickGear.ShowPositiveCapacityOnly = true;
			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			// Open the Gear XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Gear objGear = new Gear(_objCharacter);

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, false);
					objCommlink.Quantity = frmPickGear.SelectedQty;

					objGear = objCommlink;
					break;
				default:
					Gear objNewGear = new Gear(_objCharacter);
					objNewGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, false, true, frmPickGear.Aerodynamic);
					objNewGear.Quantity = frmPickGear.SelectedQty;

					objGear = objNewGear;
					break;
			}

			if (objGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objGear.Cost = (Convert.ToDouble(objGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objGear.Cost != "")
					objGear.Cost = "(" + objGear.Cost + ") * 0.1";
				if (objGear.Cost3 != "")
					objGear.Cost3 = "(" + objGear.Cost3 + ") * 0.1";
				if (objGear.Cost6 != "")
					objGear.Cost6 = "(" + objGear.Cost6 + ") * 0.1";
				if (objGear.Cost10 != "")
					objGear.Cost10 = "(" + objGear.Cost10 + ") * 0.1";
				if (objGear.Extra == "")
					objGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			objGear.Quantity = frmPickGear.SelectedQty;
			objNode.Text = objGear.DisplayName;

			// Change the cost of the Sensor itself to 0.
			if (frmPickGear.SelectedCategory == "Sensors")
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleAddGear_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleGear, objGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsVehicleGear;

			bool blnMatchFound = false;
			// If this is Ammunition, see if the character already has it on them.
			if (objGear.Category == "Ammunition")
			{
				foreach (Gear objVehicleGear in objSelectedVehicle.Gear)
				{
					if (objVehicleGear.Name == objGear.Name && objVehicleGear.Category == objGear.Category && objVehicleGear.Rating == objGear.Rating && objVehicleGear.Extra == objGear.Extra)
					{
						// A match was found, so increase the quantity instead.
						objVehicleGear.Quantity += objGear.Quantity;
						blnMatchFound = true;

						foreach (TreeNode objGearNode in treVehicles.SelectedNode.Nodes)
						{
							if (objVehicleGear.InternalId == objGearNode.Tag.ToString())
							{
								objGearNode.Text = objVehicleGear.DisplayName;
								break;
							}
						}

						break;
					}
				}
			}

			if (!blnMatchFound)
			{
				treVehicles.SelectedNode.Nodes.Add(objNode);
				treVehicles.SelectedNode.Expand();

				// Add the Gear to the Vehicle.
				objSelectedVehicle.Gear.Add(objGear);
			}

			if (frmPickGear.AddAgain)
				tsVehicleAddGear_Click(sender, e);

			UpdateCharacterInfo();
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleSensorAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treVehicles.SelectedNode.Level < 2)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_ModifyVehicleGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_ModifyVehicleGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treVehicles.SelectedNode.Level > 3)
				treVehicles.SelectedNode = treVehicles.SelectedNode.Parent;

			// Locate the Vehicle Sensor Gear.
			bool blnFound = false;
			Vehicle objVehicle = new Vehicle(_objCharacter);
			Gear objSensor = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);
			if (objSensor != null)
				blnFound = true;

			// Make sure the Sensor was found.
			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_ModifyVehicleGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSensor.Name + "\" and category = \"" + objSensor.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			//frmPickGear.ShowNegativeCapacityOnly = true;

			if (objXmlGear != null)
			{
				if (objXmlGear.InnerXml.Contains("<addoncategory>"))
				{
					string strCategories = "";
					foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
						strCategories += objXmlCategory.InnerText + ",";
					// Remove the trailing comma.
					strCategories = strCategories.Substring(0, strCategories.Length - 1);
					frmPickGear.AddCategory(strCategories);
				}
			}

			if (frmPickGear.AllowedCategories != "")
				frmPickGear.AllowedCategories += objSensor.Category + ",";

			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			// Open the Gear XML file and locate the selected piece.
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Gear objGear = new Gear(_objCharacter);

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, false);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objGear = objCommlink;
					break;
				default:
					Gear objNewGear = new Gear(_objCharacter);
					objNewGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, false, true, frmPickGear.Aerodynamic);
					objNewGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objNewGear.DisplayName;

					objGear = objNewGear;
					break;
			}

			if (objGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objGear.Cost = (Convert.ToDouble(objGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objGear.Cost != "")
					objGear.Cost = "(" + objGear.Cost + ") * 0.1";
				if (objGear.Cost3 != "")
					objGear.Cost3 = "(" + objGear.Cost3 + ") * 0.1";
				if (objGear.Cost6 != "")
					objGear.Cost6 = "(" + objGear.Cost6 + ") * 0.1";
				if (objGear.Cost10 != "")
					objGear.Cost10 = "(" + objGear.Cost10 + ") * 0.1";
				if (objGear.Extra == "")
					objGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			objNode.Text = objGear.DisplayName;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleSensorAddAsPlugin_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleGear, objGear.InternalId, frmPickGear.SelectedQty);
					objExpense.Undo = objUndo;
				}
			}

			objGear.Parent = objSensor;
			objNode.ContextMenuStrip = cmsVehicleGear;

			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			objSensor.Children.Add(objGear);

			if (frmPickGear.AddAgain)
				tsVehicleSensorAddAsPlugin_Click(sender, e);

			UpdateCharacterInfo();
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleGearAddAsPlugin_Click(object sender, EventArgs e)
		{
			tsVehicleSensorAddAsPlugin_Click(sender, e);
		}

		private void tsVehicleGearNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Vehicle objVehicle = new Vehicle(_objCharacter);
				Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);
				if (objGear != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objGear.Notes;
					string strOldValue = objGear.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objGear.Notes = frmItemNotes.Notes;
						if (objGear.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objGear.Notes != string.Empty)
						treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
					treVehicles.SelectedNode.ToolTipText = objGear.Notes;
				}
			}
			catch
			{
			}
		}

		private void cmsAmmoSingleShot_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				objWeapon.AmmoRemaining -= 1;
				lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsAmmoShortBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= 3)
				{
					objWeapon.AmmoRemaining -= 3;
				}
				else
				{
					if (objWeapon.AmmoRemaining == 1)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSingleShot"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
				}
				lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsAmmoLongBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= 6)
				{
					objWeapon.AmmoRemaining -= 6;
				}
				else
				{
					if (objWeapon.AmmoRemaining == 1)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSingleShot"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else if (objWeapon.AmmoRemaining > 3)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoLongBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else if (objWeapon.AmmoRemaining == 3)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurst"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
				}
				lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsAmmoFullBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= objWeapon.FullBurst)
				{
					objWeapon.AmmoRemaining -= objWeapon.FullBurst;
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoFullBurst"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsAmmoSuppressiveFire_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= objWeapon.Suppressive)
				{
					objWeapon.AmmoRemaining -= objWeapon.Suppressive;
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSuppressiveFire"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsVehicleAmmoSingleShot_Click(object sender, EventArgs e)
		{
			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}
						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				objWeapon.AmmoRemaining -= 1;
				lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsVehicleAmmoShortBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}
						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= 3)
				{
					objWeapon.AmmoRemaining -= 3;
				}
				else
				{
					if (objWeapon.AmmoRemaining == 1)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSingleShot"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
				}
				lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsVehicleAmmoLongBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}
						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= 6)
				{
					objWeapon.AmmoRemaining -= 6;
				}
				else
				{
					if (objWeapon.AmmoRemaining == 1)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSingleShot"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else if (objWeapon.AmmoRemaining > 3)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoLongBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else if (objWeapon.AmmoRemaining == 3)
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurst"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
					else
					{
						if (MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoShortBurstShort"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
							objWeapon.AmmoRemaining = 0;
					}
				}
				lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsVehicleAmmoFullBurst_Click(object sender, EventArgs e)
		{
			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}
						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= objWeapon.FullBurst)
				{
					objWeapon.AmmoRemaining -= objWeapon.FullBurst;
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoFullBurst"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmsVehicleAmmoSuppressiveFire_Click(object sender, EventArgs e)
		{
			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}
						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			if (objWeapon.AmmoRemaining == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmo"), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			else
			{
				if (objWeapon.AmmoRemaining >= objWeapon.Suppressive)
				{
					objWeapon.AmmoRemaining -= objWeapon.Suppressive;
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughAmmoSuppressiveFire"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsCyberwareSell_Click(object sender, EventArgs e)
		{
			try
			{
				if (treCyberware.SelectedNode.Level > 0)
				{
					bool blnFound = false;
					Cyberware objCyberware = new Cyberware(_objCharacter);
					Cyberware objParent = new Cyberware(_objCharacter);
					// Locate the piece of Cyberware that is selected in the tree.
					foreach (Cyberware objCharacterCyberware in _objCharacter.Cyberware)
					{
						if (objCharacterCyberware.InternalId == treCyberware.SelectedNode.Tag.ToString())
						{
							objCyberware = objCharacterCyberware;
							blnFound = true;
							break;
						}
						foreach (Cyberware objChildCyberware in objCharacterCyberware.Children)
						{
							if (objChildCyberware.InternalId == treCyberware.SelectedNode.Tag.ToString())
							{
								objCyberware = objChildCyberware;
								objParent = objCharacterCyberware;
								blnFound = true;
								break;
							}
						}
					}

					if (blnFound)
					{
						if (objCyberware.Capacity == "[*]" && treCyberware.SelectedNode.Level == 2)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveCyberware"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}

						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(objCyberware.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						string strEntry = "";
						if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
							strEntry = LanguageManager.Instance.GetString("String_ExpenseSoldCyberware");
						else
							strEntry = LanguageManager.Instance.GetString("String_ExpenseSoldBioware");
						objExpense.Create(intAmount, strEntry + " " + objCyberware.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;

						// Run through the Cyberware's child elements and remove any Improvements and Cyberweapons.
						foreach (Cyberware objChildCyberware in objCyberware.Children)
						{
							_objImprovementManager.RemoveImprovements(objCyberware.SourceType, objChildCyberware.InternalId);
							if (objChildCyberware.WeaponID != Guid.Empty.ToString())
							{
								// Remove the Weapon from the TreeView.
								TreeNode objRemoveNode = new TreeNode();
								foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
								{
									if (objWeaponNode.Tag.ToString() == objChildCyberware.WeaponID)
										objRemoveNode = objWeaponNode;
								}
								treWeapons.Nodes.Remove(objRemoveNode);

								// Remove the Weapon from the Character.
								Weapon objRemoveWeapon = new Weapon(_objCharacter);
								foreach (Weapon objWeapon in _objCharacter.Weapons)
								{
									if (objWeapon.InternalId == objChildCyberware.WeaponID)
										objRemoveWeapon = objWeapon;
								}
								_objCharacter.Weapons.Remove(objRemoveWeapon);
							}
						}
						// Remove the Children.
						objCyberware.Children.Clear();

						// Remove the Cyberweapon created by the Cyberware if applicable.
						if (objCyberware.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objCyberware.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objCyberware.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);
						}

						// Remove any Improvements created by the piece of Cyberware.
						_objImprovementManager.RemoveImprovements(objCyberware.SourceType, objCyberware.InternalId);
						_objCharacter.Cyberware.Remove(objCyberware);

						// Remove the item from the TreeView.
						treCyberware.Nodes.Remove(treCyberware.SelectedNode);

						// If the Parent is populated, remove the item from its Parent.
						objParent.Children.Remove(objCyberware);
					}
					else
					{
						// Locate the selected piece of Gear.
						Gear objGear = _objFunctions.FindCyberwareGear(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware, out objCyberware);

						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(objGear.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						string strEntry = LanguageManager.Instance.GetString("String_ExpenseSoldCyberwareGear");
						objExpense.Create(intAmount, strEntry + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;

						if (objGear.Parent == null)
							objCyberware.Gear.Remove(objGear);
						else
							objGear.Parent.Children.Remove(objGear);

						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
						treCyberware.SelectedNode.Remove();
					}

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
				RefreshSelectedCyberware();
			}
			catch
			{
			}

			UpdateCharacterInfo();
		}

		private void tsArmorSell_Click(object sender, EventArgs e)
		{
			// Delete the selected piece of Armor.
			try
			{
				if (treArmor.SelectedNode.Level == 1)
				{
					// Locate the piece of Armor that is selected in the tree.
					Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);

					frmSellItem frmSell = new frmSellItem();
					frmSell.ShowDialog(this);

					if (frmSell.DialogResult == DialogResult.Cancel)
						return;

					// Create the Expense Log Entry for the sale.
					int intAmount = Convert.ToInt32(Convert.ToDouble(objArmor.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldArmor") + " " + objArmor.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen += intAmount;

					// Remove any Improvements created by the Armor and its children.
					foreach (ArmorMod objMod in objArmor.ArmorMods)
					{
						// Remove the Cyberweapon created by the Mod if applicable.
						if (objMod.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objMod.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objMod.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);
						}

						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
					}
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);

					_objCharacter.Armor.Remove(objArmor);
					treArmor.SelectedNode.Remove();
				}
				else if (treArmor.SelectedNode.Level == 2)
				{
					// Locate the ArmorMod that is selected in the tree.
					bool blnIsMod = false;
					ArmorMod objMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
					if (objMod != null)
						blnIsMod = true;

					if (blnIsMod)
					{
						// Record the cost of the Armor with the ArmorMod.
						int intOriginal = objMod.Parent.TotalCost;

						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Remove the Cyberweapon created by the Mod if applicable.
						if (objMod.WeaponID != Guid.Empty.ToString())
						{
							// Remove the Weapon from the TreeView.
							TreeNode objRemoveNode = new TreeNode();
							foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
							{
								if (objWeaponNode.Tag.ToString() == objMod.WeaponID)
									objRemoveNode = objWeaponNode;
							}
							treWeapons.Nodes.Remove(objRemoveNode);

							// Remove the Weapon from the Character.
							Weapon objRemoveWeapon = new Weapon(_objCharacter);
							foreach (Weapon objWeapon in _objCharacter.Weapons)
							{
								if (objWeapon.InternalId == objMod.WeaponID)
									objRemoveWeapon = objWeapon;
							}
							_objCharacter.Weapons.Remove(objRemoveWeapon);
						}

						// Remove any Improvements created by the ArmorMod.
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);

						objMod.Parent.ArmorMods.Remove(objMod);
						treArmor.SelectedNode.Remove();

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objMod.Parent.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldArmorMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
					else
					{
						Armor objArmor = new Armor(_objCharacter);
						Gear objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objArmor);

						// Record the cost of the Armor with the ArmorMod.
						int intOriginal = objArmor.TotalCost;

						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Remove any Improvements created by the Gear.
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);

						objArmor.Gear.Remove(objGear);
						treArmor.SelectedNode.Remove();

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objArmor.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldArmorGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;

						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					}
				}
				else if (treArmor.SelectedNode.Level > 2)
				{
					Armor objArmor = new Armor(_objCharacter);
					Gear objGear = new Gear(_objCharacter);
					Gear objParent = new Gear(_objCharacter);
					objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objArmor);
					objParent = objGear.Parent;

					// Record the cost of the Armor with the ArmorMod.
					int intOriginal = objArmor.TotalCost;

					frmSellItem frmSell = new frmSellItem();
					frmSell.ShowDialog(this);

					if (frmSell.DialogResult == DialogResult.Cancel)
						return;

					// Remove any Improvements created by the Gear.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);

					objParent.Children.Remove(objGear);
					treArmor.SelectedNode.Remove();

					// Create the Expense Log Entry for the sale.
					int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objArmor.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldArmorGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen += intAmount;

					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
				}
				UpdateCharacterInfo();
				RefreshSelectedArmor();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void tsWeaponSell_Click(object sender, EventArgs e)
		{
			// Delete the selected Weapon.
			try
			{
				if (treWeapons.SelectedNode.Level == 1)
				{
					Weapon objWeapon = new Weapon(_objCharacter);
					// Locate the Weapon that is selected in the tree.
					foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
					{
						if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objCharacterWeapon;
							break;
						}
					}

					// Cyberweapons cannot be removed through here and must be done by removing the piece of Cyberware.
					if (objWeapon.Category.StartsWith("Cyberware"))
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveCyberweapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveCyberweapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						return;
					}
					if (objWeapon.Category == "Gear")
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveGearWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveGearWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						return;
					}
					if (objWeapon.Category.StartsWith("Quality"))
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveQualityWeapon"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveQualityWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						return;
					}

					frmSellItem frmSell = new frmSellItem();
					frmSell.ShowDialog(this);

					if (frmSell.DialogResult == DialogResult.Cancel)
						return;

					// Create the Expense Log Entry for the sale.
					int intAmount = Convert.ToInt32(Convert.ToDouble(objWeapon.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen += intAmount;

					_objCharacter.Weapons.Remove(objWeapon);
					treWeapons.SelectedNode.Remove();
				}
				else if (treWeapons.SelectedNode.Level > 1)
				{
					Weapon objWeapon = new Weapon(_objCharacter);
					// Locate the Weapon that is selected in the tree.
					foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
					{
						if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Parent.Tag.ToString())
						{
							objWeapon = objCharacterWeapon;
							break;
						}
					}

					WeaponAccessory objAccessory = new WeaponAccessory(_objCharacter);
					// Locate the Accessory that is selected in the tree.
					foreach (WeaponAccessory objCharacterAccessory in objWeapon.WeaponAccessories)
					{
						if (objCharacterAccessory.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objAccessory = objCharacterAccessory;
							break;
						}
					}

					if (objAccessory.Name != "")
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the Weapon's original cost.
						int intOriginal = objWeapon.TotalCost;

						objWeapon.WeaponAccessories.Remove(objAccessory);
						treWeapons.SelectedNode.Remove();

						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objWeapon.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldWeaponAccessory") + " " + objAccessory.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}

					WeaponMod objMod = new WeaponMod(_objCharacter);
					// Locate the Mod that is selected in the tree.
					foreach (WeaponMod objCharacterMod in objWeapon.WeaponMods)
					{
						if (objCharacterMod.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objMod = objCharacterMod;
							break;
						}
					}

					if (objMod.Name != "")
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the Weapon's original cost.
						int intOriginal = objWeapon.TotalCost;

						objWeapon.WeaponMods.Remove(objMod);
						treWeapons.SelectedNode.Remove();

						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objWeapon.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldWeaponMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
					else
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Find the selected Gear.
						Gear objGear = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objAccessory);
						_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
						if (objGear.Parent == null)
							objAccessory.Gear.Remove(objGear);
						else
							objGear.Parent.Children.Remove(objGear);
						treWeapons.SelectedNode.Remove();

						int intAmount = Convert.ToInt32(Convert.ToDouble(objGear.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldWeaponGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
				}
				UpdateCharacterInfo();
				RefreshSelectedWeapon();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void sellItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			// Delete the selected Gear.
			try
			{
				if (treGear.SelectedNode.Level > 0)
				{
					Gear objGear = new Gear(_objCharacter);
					Gear objParent = new Gear(_objCharacter);
					// Locate the piece of Gear that is selected in the tree.
					foreach (Gear objCharacterGear in _objCharacter.Gear)
					{
						if (objCharacterGear.InternalId == treGear.SelectedNode.Tag.ToString())
						{
							objGear = objCharacterGear;
							break;
						}
						foreach (Gear objChildGear in objCharacterGear.Children)
						{
							if (objChildGear.InternalId == treGear.SelectedNode.Tag.ToString())
							{
								objGear = objChildGear;
								objParent = objCharacterGear;
								break;
							}
						}
					}

					frmSellItem frmSell = new frmSellItem();
					frmSell.ShowDialog(this);

					if (frmSell.DialogResult == DialogResult.Cancel)
						return;

					// Create the Expense Log Entry for the sale.
					int intAmount = Convert.ToInt32(Convert.ToDouble(objGear.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen += intAmount;

					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);

					_objCharacter.Gear.Remove(objGear);
					treGear.SelectedNode.Remove();

					// If the Parent is populated, remove the item from its Parent.
					objParent.Children.Remove(objGear);
				}
				_objController.PopulateFocusList(treFoci);
				UpdateCharacterInfo();
				RefreshSelectedGear();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void tsVehicleSell_Click(object sender, EventArgs e)
		{
			// Delete the selected Vehicle.
			try
			{
				if (treVehicles.SelectedNode.Level == 1)
				{
					Vehicle objVehicle = new Vehicle(_objCharacter);
					// Locate the Vehicle that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						if (objCharacterVehicle.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objVehicle = objCharacterVehicle;
							break;
						}
					}

					frmSellItem frmSell = new frmSellItem();
					frmSell.ShowDialog(this);

					if (frmSell.DialogResult == DialogResult.Cancel)
						return;

					// Create the Expense Log Entry for the sale.
					int intAmount = Convert.ToInt32(Convert.ToDouble(objVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicle") + " " + objVehicle.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen += intAmount;

					_objCharacter.Vehicles.Remove(objVehicle);
					treVehicles.SelectedNode.Remove();
				}
				else if (treVehicles.SelectedNode.Level == 2)
				{
					// Locate the VehicleMod that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							if (objMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								// Do not allow VehicleMods that come with a Vehicle to be removed.
								if (objMod.IncludedInVehicle)
								{
									MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveVehicleMod"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveVehicleMod"), MessageBoxButtons.OK, MessageBoxIcon.Information);
									return;
								}
								else
								{
									frmSellItem frmSell = new frmSellItem();
									frmSell.ShowDialog(this);

									if (frmSell.DialogResult == DialogResult.Cancel)
										return;

									// Record the original value of the Vehicle.
									int intOriginal = objCharacterVehicle.TotalCost;

									// Check for Improved Sensor bonus.
									if (objMod.Bonus != null)
									{
										if (objMod.Bonus["improvesensor"] != null)
										{
											ChangeVehicleSensor(objCharacterVehicle, false);
										}
									}

									objCharacterVehicle.Mods.Remove(objMod);
									treVehicles.SelectedNode.Remove();

									// Create the Expense Log Entry for the sale.
									int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
									ExpenseLogEntry objExpense = new ExpenseLogEntry();
									objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
									_objCharacter.ExpenseEntries.Add(objExpense);
									_objCharacter.Nuyen += intAmount;
								}
								break;
							}
						}
					}

					// Locate the Sensor or Ammunition that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (Gear objGear in objCharacterVehicle.Gear)
						{
							if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								frmSellItem frmSell = new frmSellItem();
								frmSell.ShowDialog(this);

								if (frmSell.DialogResult == DialogResult.Cancel)
									return;

								// Record the original value of the vehicle.
								int intOriginal = objCharacterVehicle.TotalCost;

								// Remove the Gear Weapon created by the Gear if applicable.
								if (objGear.WeaponID != Guid.Empty.ToString())
								{
									// Remove the Weapon from the TreeView.
									foreach (TreeNode objWeaponNode in treVehicles.SelectedNode.Parent.Nodes)
									{
										if (objWeaponNode.Tag.ToString() == objGear.WeaponID)
										{
											treVehicles.SelectedNode.Parent.Nodes.Remove(objWeaponNode);
											break;
										}
									}

									// Remove the Weapon from the Vehicle.
									Weapon objRemoveWeapon = new Weapon(_objCharacter);
									foreach (Weapon objWeapon in objCharacterVehicle.Weapons)
									{
										if (objWeapon.InternalId == objGear.WeaponID)
											objRemoveWeapon = objWeapon;
									}
									objCharacterVehicle.Weapons.Remove(objRemoveWeapon);
								}

								objCharacterVehicle.Gear.Remove(objGear);
								treVehicles.SelectedNode.Remove();

								// Create the Expense Log Entry for the sale.
								int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
								ExpenseLogEntry objExpense = new ExpenseLogEntry();
								objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
								_objCharacter.ExpenseEntries.Add(objExpense);
								_objCharacter.Nuyen += intAmount;

								break;
							}
						}
					}

					// Locate the Weapon that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (Weapon objWeapon in objCharacterVehicle.Weapons)
						{
							if (objWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
							{
								MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRemoveGearWeaponVehicle"), LanguageManager.Instance.GetString("MessageTitle_CannotRemoveGearWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
								break;
							}
						}
					}
				}
				else if (treVehicles.SelectedNode.Level == 3)
				{
					// Locate the selected VehicleWeapon that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								if (objWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									frmSellItem frmSell = new frmSellItem();
									frmSell.ShowDialog(this);

									if (frmSell.DialogResult == DialogResult.Cancel)
										return;

									// Record the original value of the Vehicle.
									int intOriginal = objCharacterVehicle.TotalCost;

									objMod.Weapons.Remove(objWeapon);
									treVehicles.SelectedNode.Remove();

									// Create the Expense Log Entry for the sale.
									int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
									ExpenseLogEntry objExpense = new ExpenseLogEntry();
									objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
									_objCharacter.ExpenseEntries.Add(objExpense);
									_objCharacter.Nuyen += intAmount;

									break;
								}
							}
						}
					}

					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the original value of the Vehicle.
						int intOriginal = objFoundVehicle.TotalCost;

						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objFoundVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}

					// Locate the selected Cyberware.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							foreach (Cyberware objCyberware in objMod.Cyberware)
							{
								if (objCyberware.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									frmSellItem frmSell = new frmSellItem();
									frmSell.ShowDialog(this);

									if (frmSell.DialogResult == DialogResult.Cancel)
										return;

									// Record the original value of the Vehicle.
									int intOriginal = objCharacterVehicle.TotalCost;

									objMod.Cyberware.Remove(objCyberware);
									treVehicles.SelectedNode.Remove();

									// Create the Expense Log Entry for the sale.
									int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
									ExpenseLogEntry objExpense = new ExpenseLogEntry();
									objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleCyberware") + " " + objCyberware.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
									_objCharacter.ExpenseEntries.Add(objExpense);
									_objCharacter.Nuyen += intAmount;

									break;
								}
							}
						}
					}
				}
				else if (treVehicles.SelectedNode.Level == 4)
				{
					// Locate the selected WeaponAccessory or VehicleWeaponMod that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
								{
									if (objAccessory.InternalId == treVehicles.SelectedNode.Tag.ToString())
									{
										frmSellItem frmSell = new frmSellItem();
										frmSell.ShowDialog(this);

										if (frmSell.DialogResult == DialogResult.Cancel)
											return;

										// Record the original value of the Vehicle.
										int intOriginal = objCharacterVehicle.TotalCost;

										objWeapon.WeaponAccessories.Remove(objAccessory);
										treVehicles.SelectedNode.Remove();

										// Create the Expense Log Entry for the sale.
										int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
										ExpenseLogEntry objExpense = new ExpenseLogEntry();
										objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeaponAccessory") + " " + objAccessory.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
										_objCharacter.ExpenseEntries.Add(objExpense);
										_objCharacter.Nuyen += intAmount;

										break;
									}
								}
								foreach (WeaponMod objWeaponMod in objWeapon.WeaponMods)
								{
									if (objWeaponMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
									{
										frmSellItem frmSell = new frmSellItem();
										frmSell.ShowDialog(this);

										if (frmSell.DialogResult == DialogResult.Cancel)
											return;

										// Record tthe original value of the Vehicle.
										int intOriginal = objCharacterVehicle.TotalCost;

										objWeapon.WeaponMods.Remove(objWeaponMod);
										treVehicles.SelectedNode.Remove();

										// Create the Expense Log Entry for the sale.
										int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
										ExpenseLogEntry objExpense = new ExpenseLogEntry();
										objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeaponMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
										_objCharacter.ExpenseEntries.Add(objExpense);
										_objCharacter.Nuyen += intAmount;

										break;
									}
								}
								if (objWeapon.UnderbarrelWeapons.Count > 0)
								{
									foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
									{
										if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
										{
											frmSellItem frmSell = new frmSellItem();
											frmSell.ShowDialog(this);

											if (frmSell.DialogResult == DialogResult.Cancel)
												return;

											// Record the original value of the Vehicle.
											int intOriginal = objCharacterVehicle.TotalCost;

											objWeapon.UnderbarrelWeapons.Remove(objUnderbarrelWeapon);
											treVehicles.SelectedNode.Remove();

											// Create the Expense Log Entry for the sale.
											int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
											ExpenseLogEntry objExpense = new ExpenseLogEntry();
											objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeapon") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
											_objCharacter.ExpenseEntries.Add(objExpense);
											_objCharacter.Nuyen += intAmount;

											break;
										}
									}
								}
							}
						}
					}

					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the original value of the Vehicle.
						int intOriginal = objFoundVehicle.TotalCost;

						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();
						_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objFoundVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
				}
				else if (treVehicles.SelectedNode.Level == 5)
				{
					// Locate the selected WeaponAccessory or VehicleWeaponMod that is selected in the tree.
					foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objCharacterVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								if (objWeapon.UnderbarrelWeapons.Count > 0)
								{
									foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
									{
										foreach (WeaponAccessory objAccessory in objUnderbarrelWeapon.WeaponAccessories)
										{
											if (objAccessory.InternalId == treVehicles.SelectedNode.Tag.ToString())
											{
												frmSellItem frmSell = new frmSellItem();
												frmSell.ShowDialog(this);

												if (frmSell.DialogResult == DialogResult.Cancel)
													return;

												// Record the original value of the Vehicle.
												int intOriginal = objCharacterVehicle.TotalCost;

												objUnderbarrelWeapon.WeaponAccessories.Remove(objAccessory);
												treVehicles.SelectedNode.Remove();

												// Create the Expense Log Entry for the sale.
												int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
												ExpenseLogEntry objExpense = new ExpenseLogEntry();
												objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeaponAccessory") + " " + objAccessory.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
												_objCharacter.ExpenseEntries.Add(objExpense);
												_objCharacter.Nuyen += intAmount;

												break;
											}
										}
										foreach (WeaponMod objWeaponMod in objUnderbarrelWeapon.WeaponMods)
										{
											if (objWeaponMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
											{
												frmSellItem frmSell = new frmSellItem();
												frmSell.ShowDialog(this);

												if (frmSell.DialogResult == DialogResult.Cancel)
													return;

												// Record tthe original value of the Vehicle.
												int intOriginal = objCharacterVehicle.TotalCost;

												objUnderbarrelWeapon.WeaponMods.Remove(objWeaponMod);
												treVehicles.SelectedNode.Remove();

												// Create the Expense Log Entry for the sale.
												int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objCharacterVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
												ExpenseLogEntry objExpense = new ExpenseLogEntry();
												objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleWeaponMod") + " " + objMod.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
												_objCharacter.ExpenseEntries.Add(objExpense);
												_objCharacter.Nuyen += intAmount;

												break;
											}
										}
									}
								}
							}
						}
					}

					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the original value of the Vehicle.
						int intOriginal = objFoundVehicle.TotalCost;

						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();
						_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objFoundVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
				}
				else if (treVehicles.SelectedNode.Level > 5)
				{
					// Locate the selected Sensor Plugin.
					// Locate the Sensor that is selected in the tree.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objGear != null)
					{
						frmSellItem frmSell = new frmSellItem();
						frmSell.ShowDialog(this);

						if (frmSell.DialogResult == DialogResult.Cancel)
							return;

						// Record the original value of the Vehicle.
						int intOriginal = objFoundVehicle.TotalCost;

						objGear.Parent.Children.Remove(objGear);
						treVehicles.SelectedNode.Remove();
						_objFunctions.DeleteVehicleGear(objGear, treVehicles, objFoundVehicle);

						// Create the Expense Log Entry for the sale.
						int intAmount = Convert.ToInt32(Convert.ToDouble(intOriginal - objFoundVehicle.TotalCost, GlobalOptions.Instance.CultureInfo) * frmSell.SellPercent);
						ExpenseLogEntry objExpense = new ExpenseLogEntry();
						objExpense.Create(intAmount, LanguageManager.Instance.GetString("String_ExpenseSoldVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
						_objCharacter.ExpenseEntries.Add(objExpense);
						_objCharacter.Nuyen += intAmount;
					}
				}
				UpdateCharacterInfo();
				RefreshSelectedVehicle();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void tsAdvancedLifestyle_Click(object sender, EventArgs e)
		{
			Lifestyle objNewLifestyle = new Lifestyle(_objCharacter);
			frmSelectAdvancedLifestyle frmPickLifestyle = new frmSelectAdvancedLifestyle(objNewLifestyle, _objCharacter);
			frmPickLifestyle.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
				return;

			objNewLifestyle.Months = 0;
			objNewLifestyle.StyleType = LifestyleType.Advanced;
			_objCharacter.Lifestyles.Add(objNewLifestyle);

			TreeNode objNode = new TreeNode();
			objNode.Text = objNewLifestyle.Name;
			objNode.Tag = objNewLifestyle.InternalId;
			objNode.ContextMenuStrip = cmsAdvancedLifestyle;
			treLifestyles.Nodes[0].Nodes.Add(objNode);
			treLifestyles.Nodes[0].Expand();

			if (frmPickLifestyle.AddAgain)
				tsAdvancedLifestyle_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsBoltHole_Click(object sender, EventArgs e)
		{
			Lifestyle objNewLifestyle = new Lifestyle(_objCharacter);
			frmSelectAdvancedLifestyle frmPickLifestyle = new frmSelectAdvancedLifestyle(objNewLifestyle, _objCharacter);
			frmPickLifestyle.StyleType = LifestyleType.BoltHole;
			frmPickLifestyle.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
				return;

			objNewLifestyle.Months = 0;
			_objCharacter.Lifestyles.Add(objNewLifestyle);

			TreeNode objNode = new TreeNode();
			objNode.Text = objNewLifestyle.Name;
			objNode.Tag = objNewLifestyle.InternalId;
			objNode.ContextMenuStrip = cmsAdvancedLifestyle;
			treLifestyles.Nodes[0].Nodes.Add(objNode);
			treLifestyles.Nodes[0].Expand();

			if (frmPickLifestyle.AddAgain)
				tsAdvancedLifestyle_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsSafehouse_Click(object sender, EventArgs e)
		{
			Lifestyle objNewLifestyle = new Lifestyle(_objCharacter);
			frmSelectAdvancedLifestyle frmPickLifestyle = new frmSelectAdvancedLifestyle(objNewLifestyle, _objCharacter);
			frmPickLifestyle.StyleType = LifestyleType.Safehouse;
			frmPickLifestyle.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
				return;

			objNewLifestyle.Months = 0;
			_objCharacter.Lifestyles.Add(objNewLifestyle);

			TreeNode objNode = new TreeNode();
			objNode.Text = objNewLifestyle.Name;
			objNode.Tag = objNewLifestyle.InternalId;
			objNode.ContextMenuStrip = cmsAdvancedLifestyle;
			treLifestyles.Nodes[0].Nodes.Add(objNode);
			treLifestyles.Nodes[0].Expand();

			if (frmPickLifestyle.AddAgain)
				tsAdvancedLifestyle_Click(sender, e);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsWeaponName_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponName"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponName"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treWeapons.SelectedNode.Level > 1)
				treWeapons.SelectedNode = treWeapons.SelectedNode.Parent;

			// Get the information for the currently selected Weapon.
			Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
			if (objWeapon == null)
				return;

			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_WeaponName");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			objWeapon.WeaponName = frmPickText.SelectedValue;
			treWeapons.SelectedNode.Text = objWeapon.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsGearName_Click(object sender, EventArgs e)
		{
			try
			{
				if (treGear.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearName"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearName"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Get the information for the currently selected Gear.
			Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			if (objGear == null)
				return;

			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_GearName");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			objGear.GearName = frmPickText.SelectedValue;
			treGear.SelectedNode.Text = objGear.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsWeaponAddUnderbarrel_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponAccessory"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectWeaponAccessory"), LanguageManager.Instance.GetString("MessageTitle_SelectWeapon"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treWeapons.SelectedNode.Level > 1)
				treWeapons.SelectedNode = treWeapons.SelectedNode.Parent;

			// Get the information for the currently selected Weapon.
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (treWeapons.SelectedNode.Tag.ToString() == objCharacterWeapon.InternalId)
				{
					if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
					{
						if (objCharacterWeapon.Category.StartsWith("Cyberware"))
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CyberwareUnderbarrel"), LanguageManager.Instance.GetString("MessageTitle_WeaponUnderbarrel"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
					}
				}
			}

			// Locate the Weapon that is selected in the tree.
			Weapon objSelectedWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
			if (objSelectedWeapon == null)
				return;

			frmSelectWeapon frmPickWeapon = new frmSelectWeapon(_objCharacter, true);
			frmPickWeapon.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Open the Weapons XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");

			XmlNode objXmlWeapon = objXmlDocument.SelectSingleNode("/chummer/weapons/weapon[name = \"" + frmPickWeapon.SelectedWeapon + "\"]");

			TreeNode objNode = new TreeNode();
			Weapon objWeapon = new Weapon(_objCharacter);
			objWeapon.Create(objXmlWeapon, _objCharacter, objNode, cmsWeapon, cmsWeaponAccessory, cmsWeapon);
			objWeapon.IsUnderbarrelWeapon = true;

			int intCost = objWeapon.TotalCost;
			// Apply a markup if applicable.
			if (frmPickWeapon.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickWeapon.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objWeapon.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickWeapon.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickWeapon.AddAgain)
						cmdAddWeapon_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeapon") + " " + objWeapon.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeapon, objWeapon.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			objSelectedWeapon.UnderbarrelWeapons.Add(objWeapon);

			objNode.ContextMenuStrip = cmsWeapon;
			treWeapons.SelectedNode.Nodes.Add(objNode);
			treWeapons.SelectedNode.Expand();
			treWeapons.SelectedNode = objNode;//

			UpdateCharacterInfo();
			RefreshSelectedWeapon();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsGearAddNexus_Click(object sender, EventArgs e)
		{
			treGear.SelectedNode = treGear.Nodes[0];

			frmSelectNexus frmPickNexus = new frmSelectNexus(_objCharacter);
			frmPickNexus.ShowDialog(this);

			if (frmPickNexus.DialogResult == DialogResult.Cancel)
				return;

			Gear objGear = new Gear(_objCharacter);
			objGear = frmPickNexus.SelectedNexus;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickNexus.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddGear, objGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			TreeNode nodNexus = new TreeNode();
			nodNexus.Text = objGear.Name;
			nodNexus.Tag = objGear.InternalId;
			nodNexus.ContextMenuStrip = cmsGear;

			foreach (Gear objChild in objGear.Children)
			{
				TreeNode nodModule = new TreeNode();
				nodModule.Text = objChild.Name;
				nodModule.Tag = objChild.InternalId;
				nodModule.ContextMenuStrip = cmsGear;
				nodNexus.Nodes.Add(nodModule);
				nodNexus.Expand();
			}

			treGear.Nodes[0].Nodes.Add(nodNexus);
			treGear.Nodes[0].Expand();

			_objCharacter.Gear.Add(objGear);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsGearButtonAddAccessory_Click(object sender, EventArgs e)
		{
			tsGearAddAsPlugin_Click(sender, e);
		}

		private void tsVehicleAddNexus_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectGearVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectGearVehicle"), LanguageManager.Instance.GetString("MessageTitle_SelectGearVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treVehicles.SelectedNode.Level > 1)
				treVehicles.SelectedNode = treVehicles.SelectedNode.Parent;

			// Attempt to locate the selected Vehicle.
			Vehicle objSelectedVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmSelectNexus frmPickNexus = new frmSelectNexus(_objCharacter, true);
			frmPickNexus.ShowDialog(this);

			if (frmPickNexus.DialogResult == DialogResult.Cancel)
				return;

			Gear objGear = new Gear(_objCharacter);
			objGear = frmPickNexus.SelectedNexus;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickNexus.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleGear, objGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			TreeNode nodNexus = new TreeNode();
			nodNexus.Text = objGear.Name;
			nodNexus.Tag = objGear.InternalId;
			nodNexus.ContextMenuStrip = cmsVehicleGear;

			foreach (Gear objChild in objGear.Children)
			{
				TreeNode nodModule = new TreeNode();
				nodModule.Text = objChild.Name;
				nodModule.Tag = objChild.InternalId;
				nodModule.ContextMenuStrip = cmsVehicleGear;
				nodNexus.Nodes.Add(nodModule);
				nodNexus.Expand();
			}

			treVehicles.SelectedNode.Nodes.Add(nodNexus);
			treVehicles.SelectedNode.Expand();

			objSelectedVehicle.Gear.Add(objGear);

			UpdateCharacterInfo();
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsUndoKarmaExpense_Click(object sender, EventArgs e)
		{
			ListViewItem objItem = new ListViewItem();

			try
			{
				objItem = lstKarma.SelectedItems[0];
			}
			catch
			{
				return;
			}

			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objItem = lstKarma.SelectedItems[0];

			// Find the selected Karma Expense.
			foreach (ExpenseLogEntry objCharacterEntry in _objCharacter.ExpenseEntries)
			{
				if (objCharacterEntry.InternalId == objItem.SubItems[3].Text)
				{
					objEntry = objCharacterEntry;
					break;
				}
			}

			if (objEntry.Undo == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_UndoNoHistory"), LanguageManager.Instance.GetString("MessageTitle_NoUndoHistory"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_UndoExpense"), LanguageManager.Instance.GetString("MessageTitle_UndoExpense"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					return;
			}

			switch (objEntry.Undo.KarmaType)
			{
				case KarmaExpenseType.ImproveAttribute:
					_objCharacter.GetAttribute(objEntry.Undo.ObjectId).Value -= 1;
					if (_objCharacter.GetAttribute(objEntry.Undo.ObjectId).Abbrev == "MAG")
						nudMysticAdeptMAGMagician.Maximum = _objCharacter.MAG.TotalValue;
					break;
				case KarmaExpenseType.AddQuality:
					// Locate the Quality that was added.
					foreach (Quality objQuality in _objCharacter.Qualities)
					{
						if (objQuality.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove any Improvements that it created.
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Quality, objQuality.InternalId);
							
							// Remove the Quality from thc character.
							_objCharacter.Qualities.Remove(objQuality);

							// Remove any Weapons created by the Quality if applicable.
							if (objQuality.WeaponID != Guid.Empty.ToString())
							{
								// Remove the Weapon from the TreeView.
								TreeNode objRemoveNode = new TreeNode();
								foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
								{
									if (objWeaponNode.Tag.ToString() == objQuality.WeaponID)
										objRemoveNode = objWeaponNode;
								}
								treWeapons.Nodes.Remove(objRemoveNode);

								// Remove the Weapon from the Character.
								Weapon objRemoveWeapon = new Weapon(_objCharacter);
								foreach (Weapon objWeapon in _objCharacter.Weapons)
								{
									if (objWeapon.InternalId == objQuality.WeaponID)
										objRemoveWeapon = objWeapon;
								}
								_objCharacter.Weapons.Remove(objRemoveWeapon);
							}

							// Remove the Quality from the Tree.
							foreach (TreeNode objNode in treQualities.Nodes[0].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							foreach (TreeNode objNode in treQualities.Nodes[1].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.AddSpell:
					// Locate the Spell that was added.
					foreach (Spell objSpell in _objCharacter.Spells)
					{
						if (objSpell.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove any Improvements that it created.
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Spell, objSpell.InternalId);

							// Remove the Spell from the character.
							_objCharacter.Spells.Remove(objSpell);

							// Remove the Spell from the Tree.
							for (int i = 0; i <= 4; i++)
							{
								foreach (TreeNode objNode in treSpells.Nodes[i].Nodes)
								{
									if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
									{
										objNode.Remove();
										break;
									}
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.SkillSpec:
					// Locate the Skill that was affected.
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillName == objEntry.Undo.ObjectId)
						{
							objSkillControl.SkillSpec = "";
							break;
						}
					}
					foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls)
					{
						if (objSkillControl.SkillName == objEntry.Undo.ObjectId)
						{
							objSkillControl.SkillSpec = "";
							break;
						}
					}
					break;
				case KarmaExpenseType.ImproveSkillGroup:
					// Locate the Skill Group that was affected.
					foreach (SkillGroupControl objSkillGroupControl in panSkillGroups.Controls)
					{
						if (objSkillGroupControl.GroupName == objEntry.Undo.ObjectId)
						{
							objSkillGroupControl.GroupRating--;
							break;
						}
					}
					break;
				case KarmaExpenseType.ImproveSkill:
					// Locate the Skill that was affected.
					string strSkillGroup = "";
					int intRating = 0;
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillName == objEntry.Undo.ObjectId)
						{
							objSkillControl.SkillRating--;
							intRating = objSkillControl.SkillRating;
							strSkillGroup = objSkillControl.SkillGroup;
							break;
						}
					}
					foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls)
					{
						if (objSkillControl.SkillName == objEntry.Undo.ObjectId)
						{
							objSkillControl.SkillRating--;
							break;
						}
					}

					// Look at the Skill Group the Skill is associated with. If the option to allow Skill Groups to be re-created is enabled and all Skills have the same Rating, or if all of the Skills
					// have Rating 0, then re-enable the Group.
					if (strSkillGroup != string.Empty)
					{
						bool blnAllRatingsMatch = true;
						foreach (SkillControl objSkillControl in panActiveSkills.Controls)
						{
							if (objSkillControl.SkillGroup == strSkillGroup)
							{
								if (objSkillControl.SkillRating != intRating)
									blnAllRatingsMatch = false;
							}
						}

						if (blnAllRatingsMatch && (intRating == 0 || _objOptions.AllowSkillRegrouping))
						{
							// All of the Ratings match and we're allow to re-create the Skill Group, so re-enable the Skill Group's Improve button.
							foreach (SkillGroupControl objGroupControl in panSkillGroups.Controls)
							{
								if (objGroupControl.GroupName == strSkillGroup)
								{
									objGroupControl.Broken = false;
									objGroupControl.Enabled = true;
									objGroupControl.IsEnabled = true;
									objGroupControl.GroupRating = intRating;
									break;
								}
							}
						}
					}
					break;
				case KarmaExpenseType.AddMetamagic:
					// Locate the Metamagic that was affected.
					foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
					{
						if (objMetamagic.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove any Improvements created by the Metamagic.
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId);
							
							// Remove the Metamagic from the character.
							_objCharacter.Metamagics.Remove(objMetamagic);

							// Remove the Metamagic from the Tree.
							foreach (TreeNode objNode in treMetamagic.Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.ImproveInitiateGrade:
					// Locate the Initiate Grade that was affected.
					foreach (InitiationGrade objGrade in _objCharacter.InitiationGrades)
					{
						if (objGrade.InternalId == objEntry.Undo.ObjectId)
						{
							if (_objCharacter.MAGEnabled)
							{
								// Remove any Improvements created by the Grade.
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Initiation, objGrade.InternalId);

								// Remove the Grade from the character.
								_objCharacter.InitiationGrades.Remove(objGrade);
								_objCharacter.InitiateGrade--;
								lblInitiateGrade.Text = _objCharacter.InitiateGrade.ToString();

								// Update any Metamagic Improvements the character might have.
								foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
								{
									if (objMetamagic.Bonus != null)
									{
										// If the Bonus contains "Rating", remove the existing Improvement and create new ones.
										if (objMetamagic.Bonus.InnerXml.Contains("Rating"))
										{
											_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId);
											_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId, objMetamagic.Bonus, false, _objCharacter.InitiateGrade, objMetamagic.DisplayNameShort);
										}
									}
								}
							}
							else
							{
								// Remove any Improvements created by the Grade.
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Submersion, objGrade.InternalId);

								// Remove the Grade from the character.
								_objCharacter.InitiationGrades.Remove(objGrade);
								_objCharacter.SubmersionGrade--;
								lblInitiateGrade.Text = _objCharacter.SubmersionGrade.ToString();

								// Update any Echo Improvements the character might have.
								foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
								{
									if (objMetamagic.Bonus != null)
									{
										// If the Bonus contains "Rating", remove the existing Improvement and create new ones.
										if (objMetamagic.Bonus.InnerXml.Contains("Rating"))
										{
											_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId);
											_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId, objMetamagic.Bonus, false, _objCharacter.SubmersionGrade, objMetamagic.DisplayNameShort);
										}
									}
								}
							}

							// Refresh the Initiation Grade List.
							UpdateInitiationGradeList();
							break;
						}
					}
					break;
				case KarmaExpenseType.AddMartialArt:
					// Locate the Martial Art that was affected.
					foreach (MartialArt objMartialArt in _objCharacter.MartialArts)
					{
						if (objMartialArt.Name == objEntry.Undo.ObjectId)
						{
							// Remove the Martial Art from the character.
							_objCharacter.MartialArts.Remove(objMartialArt);

							// Remove the Martial Art from the Tree.
							foreach (TreeNode objNode in treMartialArts.Nodes[0].Nodes)
							{
								if (objNode.Text == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.ImproveMartialArt:
					// Locate the Martial Art that was affected.
					foreach (MartialArt objMartialArt in _objCharacter.MartialArts)
					{
						if (objMartialArt.Name == objEntry.Undo.ObjectId)
						{
							objMartialArt.Rating--;
							try
							{
								if (treMartialArts.SelectedNode.Tag.ToString() == objMartialArt.Name)
								{
									lblMartialArtsRating.Text = objMartialArt.Rating.ToString();
								}
							}
							catch
							{
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.AddMartialArtManeuver:
					// Locate the Martial Art Maneuver that was affected.
					foreach (MartialArtManeuver objManeuver in _objCharacter.MartialArtManeuvers)
					{
						if (objManeuver.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove the Maneuver from the character.
							_objCharacter.MartialArtManeuvers.Remove(objManeuver);

							// Remove the Maneuver from the Tree.
							foreach (TreeNode objNode in treMartialArts.Nodes[1].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.AddComplexForm:
					// Locate the Complex Form that was affected.
                    foreach (ComplexForm objProgram in _objCharacter.ComplexForms)
					{
						if (objProgram.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove any Improvements created by the Complex Form.
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ComplexForm, objProgram.InternalId);

							// Remove the Complex Form from the character.
                            _objCharacter.ComplexForms.Remove(objProgram);

							// Remove the Complex Form from the Tree.
							foreach (TreeNode objParent in treComplexForms.Nodes)
							{
								foreach (TreeNode objNode in objParent.Nodes)
								{
									if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
									{
										objNode.Remove();
										break;
									}
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.BindFocus:
					// Locate the Focus that was bound.
					foreach (Focus objFocus in _objCharacter.Foci)
					{
						if (objFocus.GearId == objEntry.Undo.ObjectId)
						{
							foreach (TreeNode objNode in treFoci.Nodes)
							{
								if (objFocus.GearId == objNode.Tag.ToString())
								{
									_blnSkipRefresh = true;
									objNode.Checked = false;
									_blnSkipRefresh = false;
									break;
								}
							}
							_objCharacter.Foci.Remove(objFocus);
							break;
						}
					}

					// Locate the Stacked Focus that was bound.
					foreach (StackedFocus objStack in _objCharacter.StackedFoci)
					{
						if (objStack.InternalId == objEntry.Undo.ObjectId)
						{
							foreach (TreeNode objNode in treFoci.Nodes)
							{
								if (objStack.InternalId == objNode.Tag.ToString())
								{
									_blnSkipRefresh = true;
									objNode.Checked = false;
									objStack.Bonded = false;
									_blnSkipRefresh = false;
									break;
								}
							}
							break;
						}
					}
					break;
				case KarmaExpenseType.JoinGroup:
					// Remove the character from their Group.
					_blnSkipRefresh = true;
					chkJoinGroup.Checked = false;
					_objCharacter.GroupMember = false;
					_blnSkipRefresh = false;
					break;
				case KarmaExpenseType.LeaveGroup:
					// Put the character back in their Group.
					_blnSkipRefresh = true;
					chkJoinGroup.Checked = true;
					_objCharacter.GroupMember = true;
					_blnSkipRefresh = false;
					break;
				case KarmaExpenseType.RemoveQuality:
					// Add the Quality back to the character.
					TreeNode objQualityNode = new TreeNode();
					List<Weapon> objWeapons = new List<Weapon>();
					List<TreeNode> objWeaponNodes = new List<TreeNode>();

					Quality objAddQuality = new Quality(_objCharacter);
					XmlDocument objXmlQualityDocument = XmlManager.Instance.Load("qualities.xml");
					XmlNode objXmlQualityNode = objXmlQualityDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + objEntry.Undo.ObjectId + "\"]");
					objAddQuality.Create(objXmlQualityNode, _objCharacter, QualitySource.Selected, objQualityNode, objWeapons, objWeaponNodes, objEntry.Undo.Extra);

					objQualityNode.ContextMenuStrip = cmsQuality;

					// Add the Quality to the appropriate parent node.
					if (objAddQuality.Type == QualityType.Positive)
					{
						treQualities.Nodes[0].Nodes.Add(objQualityNode);
						treQualities.Nodes[0].Expand();
					}
					else
					{
						treQualities.Nodes[1].Nodes.Add(objQualityNode);
						treQualities.Nodes[1].Expand();
					}
					_objCharacter.Qualities.Add(objAddQuality);

					// Add any created Weapons to the character.
					foreach (Weapon objWeapon in objWeapons)
						_objCharacter.Weapons.Add(objWeapon);

					// Create the Weapon Node if one exists.
					foreach (TreeNode objWeaponNode in objWeaponNodes)
					{
						objWeaponNode.ContextMenuStrip = cmsWeapon;
						treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
						treWeapons.Nodes[0].Expand();
					}

					_objFunctions.SortTree(treQualities);
					break;
				case KarmaExpenseType.ManualAdd:
				case KarmaExpenseType.ManualSubtract:
				case KarmaExpenseType.QuickeningMetamagic:
					break;
			}
			// Refund the Karma amount and remove the Expense Entry.
			_objCharacter.Karma -= objEntry.Amount;
			_objCharacter.ExpenseEntries.Remove(objEntry);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsUndoNuyenExpense_Click(object sender, EventArgs e)
		{
			ListViewItem objItem = new ListViewItem();

			try
			{
				objItem = lstNuyen.SelectedItems[0];
			}
			catch
			{
				return;
			}

			CommonFunctions objCommon = new CommonFunctions(_objCharacter);

			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objItem = lstNuyen.SelectedItems[0];

			// Find the selected Nuyen Expense.
			foreach (ExpenseLogEntry objCharacterEntry in _objCharacter.ExpenseEntries)
			{
				if (objCharacterEntry.InternalId == objItem.SubItems[3].Text)
				{
					objEntry = objCharacterEntry;
					break;
				}
			}

			if (objEntry.Undo == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_UndoNoHistory"), LanguageManager.Instance.GetString("MessageTitle_NoUndoHistory"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_UndoExpense"), LanguageManager.Instance.GetString("MessageTitle_UndoExpense"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					return;
			}

			switch (objEntry.Undo.NuyenType)
			{
				case NuyenExpenseType.AddCyberware:
					// Locate the Cyberware that was added.
					int intOldPenalty = 0;
					int intNewPenalty = 0;
					foreach (Cyberware objCyberware in _objCharacter.Cyberware)
					{
						if (objCyberware.InternalId == objEntry.Undo.ObjectId)
						{
							foreach (Cyberware objChild in objCyberware.Children)
							{
								// Remove the Improvements created by child items.
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberware, objChild.InternalId);
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Bioware, objChild.InternalId);
							}
							// Remove the Improvements created by the item.
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberware, objCyberware.InternalId);
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Bioware, objCyberware.InternalId);

							// Determine the character's Essence penalty before removing the Cyberware.
							intOldPenalty = _objCharacter.EssencePenalty;
							// Remove the Cyberware.
							_objCharacter.Cyberware.Remove(objCyberware);
							// Determine the character's Essence penalty after removing the Cyberware.
							intNewPenalty = _objCharacter.EssencePenalty;

							// Restore the character's MAG/RES if they have it.
							//if (!_objCharacter.OverrideSpecialAttributeEssenceLoss && !_objCharacter.OverrideSpecialAttributeEssenceLoss)
							//{
							//    if (intOldPenalty != intNewPenalty)
							//    {
							//        if (_objCharacter.MAGEnabled)
							//            _objCharacter.MAG.Value += (intOldPenalty - intNewPenalty);
							//        if (_objCharacter.RESEnabled)
							//            _objCharacter.RES.Value += (intOldPenalty - intNewPenalty);
							//    }
							//}

							// Remove the item from the Tree.
							foreach (TreeNode objNode in treCyberware.Nodes[0].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							foreach (TreeNode objNode in treCyberware.Nodes[1].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}

							// Remove any Weapon that the Cyberware created.
							if (objCyberware.WeaponID != Guid.Empty.ToString())
							{
								foreach (Weapon objWeapon in _objCharacter.Weapons)
								{
									if (objWeapon.InternalId == objCyberware.WeaponID)
									{
										_objCharacter.Weapons.Remove(objWeapon);
										break;
									}
								}

								// Remove the TreeNode for the Weapon.
								foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
								{
									if (objWeaponNode.Tag.ToString() == objCyberware.WeaponID)
									{
										treWeapons.Nodes[0].Nodes.Remove(objWeaponNode);
										break;
									}
								}
							}
							break;
						}
						else
						{
							foreach (Cyberware objChild in objCyberware.Children)
							{
								if (objChild.InternalId == objEntry.Undo.ObjectId)
								{
									// Remove the Improvements created by the item.
									_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberware, objChild.InternalId);
									objCyberware.Children.Remove(objChild);

									// Remove the item from the Tree.
									foreach (TreeNode objNode in treCyberware.Nodes[0].Nodes)
									{
										foreach (TreeNode objChildNode in objNode.Nodes)
										{
											if (objChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
											{
												objChildNode.Remove();
												break;
											}
										}
									}
									foreach (TreeNode objNode in treCyberware.Nodes[1].Nodes)
									{
										foreach (TreeNode objChildNode in objNode.Nodes)
										{
											if (objChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
											{
												objNode.Remove();
												break;
											}
										}
									}
									break;
								}

								// Remove any Weapon that the Cyberware created.
								if (objChild.WeaponID != Guid.Empty.ToString())
								{
									foreach (Weapon objWeapon in _objCharacter.Weapons)
									{
										if (objWeapon.InternalId == objChild.WeaponID)
										{
											_objCharacter.Weapons.Remove(objWeapon);
											break;
										}
									}

									// Remove the TreeNode for the Weapon.
									foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
									{
										if (objWeaponNode.Tag.ToString() == objChild.WeaponID)
										{
											treWeapons.Nodes[0].Nodes.Remove(objWeaponNode);
											break;
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddGear:
					// Locate the Gear that was added.
					Gear objGear = objCommon.FindGear(objEntry.Undo.ObjectId, _objCharacter.Gear);
					objGear.Quantity -= objEntry.Undo.Qty;

					if (objGear.Quantity <= 0)
					{
						if (objGear.Parent != null)
							objGear.Parent.Children.Remove(objGear);
						else
							_objCharacter.Gear.Remove(objGear);

						objCommon.DeleteGear(objGear, treWeapons, _objImprovementManager);
						TreeNode objNode = objCommon.FindNode(objGear.InternalId, treGear);
						objNode.Remove();
					}
					else
					{
						TreeNode objNode = objCommon.FindNode(objGear.InternalId, treGear);
						objNode.Text = objGear.DisplayName;
					}

					_objController.PopulateFocusList(treFoci);
					break;
				case NuyenExpenseType.AddVehicle:
					// Locate the Vehicle that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						if (objVehicle.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove the Vehicle.
							_objCharacter.Vehicles.Remove(objVehicle);
							foreach (TreeNode objNode in treVehicles.Nodes[0].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case NuyenExpenseType.AddVehicleMod:
					// Locate the Vehicle Mod that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objVehicle.Mods)
						{
							if (objMod.InternalId == objEntry.Undo.ObjectId)
							{
								// Check for Improved Sensor bonus.
								if (objMod.Bonus != null)
								{
									if (objMod.Bonus["improvesensor"] != null)
									{
										ChangeVehicleSensor(objVehicle, false);
									}
								}

								// Remove the Vehicle Mod.
								objVehicle.Mods.Remove(objMod);

								// Remove the Vehicle Mod from the tree.
								foreach (TreeNode objNode in treVehicles.Nodes[0].Nodes)
								{
									foreach (TreeNode objChild in objNode.Nodes)
									{
										if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
										{
											objChild.Remove();
											break;
										}
									}
								}
								break;
							}
						}
					}
					break;
				case NuyenExpenseType.AddVehicleGear:
					// Locate the Gear that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (Gear objVehicleGear in objVehicle.Gear)
						{
							if (objVehicleGear.InternalId == objEntry.Undo.ObjectId)
							{
								// Deduct the Qty from the Gear.
								objVehicleGear.Quantity -= objEntry.Undo.Qty;
								foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
								{
									foreach (TreeNode objNode in objVNode.Nodes)
									{
										if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
										{
											objNode.Text = objVehicleGear.DisplayName;
											// Remove the Node if its Qty has been reduced to 0.
											if (objVehicleGear.Quantity <= 0)
												objNode.Remove();
											break;
										}
									}
								}

								// Remove the Gear if its Qty has been reduced to 0.
								if (objVehicleGear.Quantity <= 0)
								{
									objVehicle.Gear.Remove(objVehicleGear);
								}

								break;
							}
							else
							{
								// Look in child items.
								foreach (Gear objChild in objVehicleGear.Children)
								{
									if (objChild.InternalId == objEntry.Undo.ObjectId)
									{
										// Deduct the Qty from the Gear.
										objChild.Quantity -= objEntry.Undo.Qty;
										foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
										{
											foreach (TreeNode objNode in objVNode.Nodes)
											{
												foreach (TreeNode objChildNode in objNode.Nodes)
												{
													if (objChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
													{
														objChildNode.Text = objChild.DisplayName;
														// Remove the Node if its Qty has been reduced to 0.
														if (objChild.Quantity <= 0)
														{
															objChildNode.Remove();
														}
														break;
													}
												}
											}
										}

										// Remove the Gear if its Qty has been reduce to 0.
										if (objChild.Quantity <= 0)
										{
											objVehicleGear.Children.Remove(objChild);
										}

										break;
									}
									else
									{
										foreach (Gear objSubChild in objChild.Children)
										{
											if (objSubChild.InternalId == objEntry.Undo.ObjectId)
											{
												// Deduct the Qty from the Gear.
												objSubChild.Quantity -= objEntry.Undo.Qty;
												foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
												{
													foreach (TreeNode objNode in objVNode.Nodes)
													{
														foreach (TreeNode objChildNode in objNode.Nodes)
														{
															foreach (TreeNode objSubChildNode in objChildNode.Nodes)
															{
																if (objSubChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
																{
																	objSubChildNode.Text = objSubChild.DisplayName;
																	// Remove the Node if its Qty has been reduced to 0.
																	if (objSubChild.Quantity <= 0)
																	{
																		objSubChildNode.Remove();
																	}
																	break;
																}
															}
														}
													}
												}

												// Remove the Gear if its Qty has been reduce to 0.
												if (objSubChild.Quantity <= 0)
												{
													objChild.Children.Remove(objSubChild);
												}

												break;
											}
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddVehicleWeapon:
					// Locate the Weapon that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								if (objWeapon.InternalId == objEntry.Undo.ObjectId)
								{
									// Remove the Weapon.
									objMod.Weapons.Remove(objWeapon);

									// Remove the Weapon from the Tree.
									foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
									{
										foreach (TreeNode objNode in objVNode.Nodes)
										{
											foreach (TreeNode objChild in objNode.Nodes)
											{
												if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
												{
													objChild.Remove();
													break;
												}
											}
										}
									}
									break;
								}
								else
								{
									if (objWeapon.UnderbarrelWeapons.Count > 0)
									{
										foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
										{
											if (objUnderbarrelWeapon.InternalId == objEntry.Undo.ObjectId)
											{
												// Remove the Underbarrel Weapon.
												objWeapon.UnderbarrelWeapons.Remove(objUnderbarrelWeapon);

												// Remove the Underbarrel Weapon from the Tree.
												foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
												{
													foreach (TreeNode objNode in objVNode.Nodes)
													{
														foreach (TreeNode objChild in objNode.Nodes)
														{
															foreach (TreeNode objSubChild in objChild.Nodes)
															{
																if (objSubChild.Tag.ToString() == objEntry.Undo.ObjectId)
																{
																	objSubChild.Remove();
																	break;
																}
															}
														}
													}
												}
												break;
											}
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddVehicleWeaponAccessory:
					// Locate the Weapon Accessory that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
								{
									if (objAccessory.InternalId == objEntry.Undo.ObjectId)
									{
										// Remove the Weapon Accessory.
										objWeapon.WeaponAccessories.Remove(objAccessory);

										// Remove the Weapon Accessory from the Tree.
										foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
										{
											foreach (TreeNode objNode in objVNode.Nodes)
											{
												foreach (TreeNode objWNode in objNode.Nodes)
												{
													foreach (TreeNode objChild in objWNode.Nodes)
													{
														if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
														{
															objChild.Remove();
															break;
														}
													}
												}
											}
										}
										break;
									}
								}
								if (objWeapon.UnderbarrelWeapons.Count > 0)
								{
									foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
									{
										foreach (WeaponAccessory objAccessory in objUnderbarrelWeapon.WeaponAccessories)
										{
											if (objAccessory.InternalId == objEntry.Undo.ObjectId)
											{
												// Remove the Weapon Accessory.
												objUnderbarrelWeapon.WeaponAccessories.Remove(objAccessory);

												// Remove the Weapon Accessory from the Tree.
												foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
												{
													foreach (TreeNode objNode in objVNode.Nodes)
													{
														foreach (TreeNode objWNode in objNode.Nodes)
														{
															foreach (TreeNode objChild in objWNode.Nodes)
															{
																foreach (TreeNode objSubChild in objChild.Nodes)
																{
																	if (objSubChild.Tag.ToString() == objEntry.Undo.ObjectId)
																	{
																		objSubChild.Remove();
																		break;
																	}
																}
															}
														}
													}
												}
												break;
											}
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddVehicleWeaponMod:
					// Locate the Weapon Mod that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objVehicle.Mods)
						{
							foreach (Weapon objWeapon in objMod.Weapons)
							{
								foreach (WeaponMod objWMod in objWeapon.WeaponMods)
								{
									if (objMod.InternalId == objEntry.Undo.ObjectId)
									{
										// Remove the Weapon Mod.
										objWeapon.WeaponMods.Remove(objWMod);

										// Remove the Weapon Mod from the Tree.
										foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
										{
											foreach (TreeNode objNode in objVNode.Nodes)
											{
												foreach (TreeNode objWNode in objNode.Nodes)
												{
													foreach (TreeNode objChild in objWNode.Nodes)
													{
														if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
														{
															objChild.Remove();
															break;
														}
													}
												}
											}
										}
										break;
									}
								}
								if (objWeapon.UnderbarrelWeapons.Count > 0)
								{
									foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
									{
										foreach (WeaponMod objWMod in objUnderbarrelWeapon.WeaponMods)
										{
											if (objWMod.InternalId == objEntry.Undo.ObjectId)
											{
												// Remove the Weapon Mod.
												objUnderbarrelWeapon.WeaponMods.Remove(objWMod);

												// Remove the Weapon Mod from the Tree.
												foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
												{
													foreach (TreeNode objNode in objVNode.Nodes)
													{
														foreach (TreeNode objWNode in objNode.Nodes)
														{
															foreach (TreeNode objChild in objWNode.Nodes)
															{
																foreach (TreeNode objSubChild in objChild.Nodes)
																{
																	if (objSubChild.Tag.ToString() == objEntry.Undo.ObjectId)
																	{
																		objSubChild.Remove();
																		break;
																	}
																}
															}
														}
													}
												}
												break;
											}
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddArmor:
					// Locate the Armor that was added.
					Armor objArmor = objCommon.FindArmor(objEntry.Undo.ObjectId, _objCharacter.Armor);

					if (objArmor != null)
					{
						// Remove the Improvements for any child items.
						foreach (ArmorMod objMod in objArmor.ArmorMods)
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);

						// Remove the Improvements for the Armor.
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);

						// Remove the Armor from the character.
						_objCharacter.Armor.Remove(objArmor);

						// Remove the Armor from the Tree.
						TreeNode objArmorNode = objCommon.FindNode(objEntry.Undo.ObjectId, treArmor);
						objArmorNode.Remove();
					}

					break;
				case NuyenExpenseType.AddArmorMod:
					// Locate the Armor Mod that was added.
					foreach (Armor objFoundArmor in _objCharacter.Armor)
					{
						foreach (ArmorMod objMod in objFoundArmor.ArmorMods)
						{
							if (objMod.InternalId == objEntry.Undo.ObjectId)
							{
								// Remove the Improtements for the Armor Mod.
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);

								// Remove the Armor Mod from the Armor.
								objFoundArmor.ArmorMods.Remove(objMod);

								// Remove the Cyberweapon created by the Mod if applicable.
								if (objMod.WeaponID != Guid.Empty.ToString())
								{
									// Remove the Weapon from the TreeView.
									TreeNode objRemoveNode = new TreeNode();
									foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
									{
										if (objWeaponNode.Tag.ToString() == objMod.WeaponID)
											objRemoveNode = objWeaponNode;
									}
									treWeapons.Nodes.Remove(objRemoveNode);

									// Remove the Weapon from the Character.
									Weapon objRemoveWeapon = new Weapon(_objCharacter);
									foreach (Weapon objWeapon in _objCharacter.Weapons)
									{
										if (objWeapon.InternalId == objMod.WeaponID)
											objRemoveWeapon = objWeapon;
									}
									_objCharacter.Weapons.Remove(objRemoveWeapon);
								}

								// Remove the Armor Mod from the Tree.
								TreeNode objNode = objCommon.FindNode(objMod.InternalId, treArmor.Nodes[0]);
								objNode.Remove();
								break;
							}
						}
					}
					break;
				case NuyenExpenseType.AddWeapon:
					// Locate the Weapon that was added.
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						if (objWeapon.InternalId == objEntry.Undo.ObjectId)
						{
							// Remove the Weapn from the character.
							_objCharacter.Weapons.Remove(objWeapon);

							// Remove the Weapon from the Tree.
							foreach (TreeNode objNode in treWeapons.Nodes[0].Nodes)
							{
								if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
								{
									objNode.Remove();
									break;
								}
							}
							break;
						}
					}
					break;
				case NuyenExpenseType.AddWeaponAccessory:
					// Locate the Weapon Accessory that was added.
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
						{
							if (objAccessory.InternalId == objEntry.Undo.ObjectId)
							{
								// Remove the Weapon Accessory.
								objWeapon.WeaponAccessories.Remove(objAccessory);

								// Remove the Weapon Accessory from the tree.
								foreach (TreeNode objNode in treWeapons.Nodes[0].Nodes)
								{
									foreach (TreeNode objChild in objNode.Nodes)
									{
										if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
										{
											objChild.Remove();
											break;
										}
									}
								}
								break;
							}
						}
					}
					break;
				case NuyenExpenseType.AddWeaponMod:
					// Locate the Weapon Mod that was added.
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						foreach (WeaponMod objMod in objWeapon.WeaponMods)
						{
							if (objMod.InternalId == objEntry.Undo.ObjectId)
							{
								// Remove the Weapon Mod.
								objWeapon.WeaponMods.Remove(objMod);

								// Remove the Weapon Mod from the tree.
								foreach (TreeNode objNode in treWeapons.Nodes[0].Nodes)
								{
									foreach (TreeNode objChild in objNode.Nodes)
									{
										if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
										{
											objChild.Remove();
											break;
										}
									}
								}
								break;
							}
						}
					}
					break;
				case NuyenExpenseType.IncreaseLifestyle:
					// Locate the Lifestyle that was increased.
					foreach (Lifestyle objLifestyle in _objCharacter.Lifestyles)
					{
						if (objLifestyle.Name == objEntry.Undo.ObjectId)
						{
							objLifestyle.Months--;
							RefreshSelectedLifestyle();
							break;
						}
					}
					break;
				case NuyenExpenseType.AddArmorGear:
					// Locate the Armor Gear that was added.
					foreach (Armor objFoundArmor in _objCharacter.Armor)
					{
						foreach (Gear objArmorGear in objFoundArmor.Gear)
						{
							if (objArmorGear.InternalId == objEntry.Undo.ObjectId)
							{
								// Deduct the Qty from the Gear.
								objArmorGear.Quantity -= objEntry.Undo.Qty;
								foreach (TreeNode objArmorNode in treArmor.Nodes[0].Nodes)
								{
									foreach (TreeNode objNode in objArmorNode.Nodes)
									{
										if (objNode.Tag.ToString() == objEntry.Undo.ObjectId)
										{
											objNode.Text = objArmorGear.DisplayName;
											// Remove the Node if its Qty has been reduced to 0.
											if (objArmorGear.Quantity <= 0)
												objNode.Remove();
											break;
										}
									}
								}

								// Remove the Gear if its Qty has been reduced to 0.
								if (objArmorGear.Quantity <= 0)
								{
									// Remove any Improvements created by the Gear.
									_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objArmorGear.InternalId);
									objFoundArmor.Gear.Remove(objArmorGear);

									// Remove any Weapons created by the Gear.
									foreach (Weapon objWeapon in _objCharacter.Weapons)
									{
										if (objWeapon.InternalId == objArmorGear.WeaponID)
										{
											_objCharacter.Weapons.Remove(objWeapon);
											foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
											{
												if (objWeaponNode.Tag.ToString() == objArmorGear.WeaponID)
												{
													objWeaponNode.Remove();
													break;
												}
											}
											break;
										}
									}
								}

								break;
							}
							else
							{
								// Look in child items.
								foreach (Gear objChild in objArmorGear.Children)
								{
									if (objChild.InternalId == objEntry.Undo.ObjectId)
									{
										// Deduct the Qty from the Gear.
										objChild.Quantity -= objEntry.Undo.Qty;
										foreach (TreeNode objArmorNode in treArmor.Nodes[0].Nodes)
										{
											foreach (TreeNode objNode in objArmorNode.Nodes)
											{
												foreach (TreeNode objChildNode in objNode.Nodes)
												{
													if (objChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
													{
														objChildNode.Text = objChild.DisplayName;
														// Remove the Node if its Qty has been reduced to 0.
														if (objChild.Quantity <= 0)
														{
															objChildNode.Remove();

															// Remove any Weapons created by the Gear.
															foreach (Weapon objWeapon in _objCharacter.Weapons)
															{
																if (objWeapon.InternalId == objChild.WeaponID)
																{
																	_objCharacter.Weapons.Remove(objWeapon);
																	foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
																	{
																		if (objWeaponNode.Tag.ToString() == objChild.WeaponID)
																		{
																			objWeaponNode.Remove();
																			break;
																		}
																	}
																	break;
																}
															}
														}
														break;
													}
												}
											}
										}

										// Remove the Gear if its Qty has been reduce to 0.
										if (objChild.Quantity <= 0)
										{
											// Remove any Improvements created by the Gear.
											_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objChild.InternalId);
											objArmorGear.Children.Remove(objChild);
										}

										break;
									}
									else
									{
										foreach (Gear objSubChild in objChild.Children)
										{
											if (objSubChild.InternalId == objEntry.Undo.ObjectId)
											{
												// Deduct the Qty from the Gear.
												objSubChild.Quantity -= objEntry.Undo.Qty;
												foreach (TreeNode objArmorNode in treArmor.Nodes[0].Nodes)
												{
													foreach (TreeNode objNode in objArmorNode.Nodes)
													{
														foreach (TreeNode objChildNode in objNode.Nodes)
														{
															foreach (TreeNode objSubChildNode in objChildNode.Nodes)
															{
																if (objSubChildNode.Tag.ToString() == objEntry.Undo.ObjectId)
																{
																	objSubChildNode.Text = objSubChild.DisplayName;
																	// Remove the Node if its Qty has been reduced to 0.
																	if (objSubChild.Quantity <= 0)
																	{
																		objSubChildNode.Remove();

																		// Remove any Weapons created by the Gear.
																		foreach (Weapon objWeapon in _objCharacter.Weapons)
																		{
																			if (objWeapon.InternalId == objSubChild.WeaponID)
																			{
																				_objCharacter.Weapons.Remove(objWeapon);
																				foreach (TreeNode objWeaponNode in treWeapons.Nodes[0].Nodes)
																				{
																					if (objWeaponNode.Tag.ToString() == objSubChild.WeaponID)
																					{
																						objWeaponNode.Remove();
																						break;
																					}
																				}
																				break;
																			}
																		}
																	}
																	break;
																}
															}
														}
													}
												}

												// Remove the Gear if its Qty has been reduce to 0.
												if (objSubChild.Quantity <= 0)
												{
													// Remove any Improvements created by the Gear.
													_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objSubChild.InternalId);
													objChild.Children.Remove(objSubChild);
												}

												break;
											}
										}
									}
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddVehicleModCyberware:
					// Locate the Cyberware that was added.
					foreach (Vehicle objVehicle in _objCharacter.Vehicles)
					{
						foreach (VehicleMod objMod in objVehicle.Mods)
						{
							foreach (Cyberware objCyberware in objMod.Cyberware)
							{
								if (objCyberware.InternalId == objEntry.Undo.ObjectId)
								{
									// Remove the Cyberware.
									objMod.Cyberware.Remove(objCyberware);

									// Remove the Cyberware from the Tree.
									foreach (TreeNode objVNode in treVehicles.Nodes[0].Nodes)
									{
										foreach (TreeNode objNode in objVNode.Nodes)
										{
											foreach (TreeNode objChild in objNode.Nodes)
											{
												if (objChild.Tag.ToString() == objEntry.Undo.ObjectId)
												{
													objChild.Remove();
													break;
												}
											}
										}
									}
									break;
								}
							}
						}
					}
					break;
				case NuyenExpenseType.AddCyberwareGear:
					// Locate the Gear that was added.
					Cyberware objFoundCyberware = new Cyberware(_objCharacter);
					Gear objFoundGear = _objFunctions.FindCyberwareGear(objEntry.Undo.ObjectId, _objCharacter.Cyberware, out objFoundCyberware);
					_objFunctions.DeleteGear(objFoundGear, treWeapons, _objImprovementManager);
					if (objFoundGear.Parent == null)
						objFoundCyberware.Gear.Remove(objFoundGear);
					else
						objFoundGear.Parent.Children.Remove(objFoundGear);
					TreeNode objFoundNode = _objFunctions.FindNode(objFoundGear.InternalId, treCyberware);
					objFoundNode.Remove();
					break;
				case NuyenExpenseType.AddWeaponGear:
					// Locate the Gear that was added.
					WeaponAccessory objFoundAccessory = new WeaponAccessory(_objCharacter);
					Gear objFoundAccGear = _objFunctions.FindWeaponGear(objEntry.Undo.ObjectId, _objCharacter.Weapons, out objFoundAccessory);
					_objFunctions.DeleteGear(objFoundAccGear, treWeapons, _objImprovementManager);
					if (objFoundAccGear.Parent == null)
						objFoundAccessory.Gear.Remove(objFoundAccGear);
					else
						objFoundAccGear.Parent.Children.Remove(objFoundAccGear);
					TreeNode objFoundAccNode = _objFunctions.FindNode(objFoundAccGear.InternalId, treWeapons);
					objFoundAccNode.Remove();
					break;
				case NuyenExpenseType.ManualAdd:
				case NuyenExpenseType.ManualSubtract:
					break;
			}
			// Refund the Nuyen amount and remove the Expense Entry.
			_objCharacter.Nuyen -= objEntry.Amount;
			_objCharacter.ExpenseEntries.Remove(objEntry);

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsEditNuyenExpense_Click(object sender, EventArgs e)
		{
			cmdNuyenEdit_Click(sender, e);
		}

		private void tsEditKarmaExpense_Click(object sender, EventArgs e)
		{
			cmdKarmaEdit_Click(sender, e);
		}

		private void tsAddArmorGear_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treArmor.SelectedNode.Level != 1)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Select the root Gear node then open the Select Gear window.
			bool blnAddAgain = PickArmorGear(true);
			if (blnAddAgain)
				tsAddArmorGear_Click(sender, e);
		}

		private void tsArmorGearAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treArmor.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure the selected item is another piece of Gear.
			bool blnFound = false;
			Armor objFoundArmor = new Armor(_objCharacter);
			Gear objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objFoundArmor);
			if (objGear != null)
				blnFound = true;

			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmor"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			bool blnAddAgain = PickArmorGear();
			if (blnAddAgain)
				tsArmorGearAddAsPlugin_Click(sender, e);
		}

		private void tsArmorNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
				if (objArmor != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objArmor.Notes;
					string strOldValue = objArmor.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objArmor.Notes = frmItemNotes.Notes;
						if (objArmor.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objArmor.Notes != string.Empty)
						treArmor.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treArmor.SelectedNode.ForeColor = SystemColors.WindowText;
					treArmor.SelectedNode.ToolTipText = objArmor.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsArmorModNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				ArmorMod objArmorMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
				if (objArmorMod != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objArmorMod.Notes;
					string strOldValue = objArmorMod.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objArmorMod.Notes = frmItemNotes.Notes;
						if (objArmorMod.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objArmorMod.Notes != string.Empty)
						treArmor.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treArmor.SelectedNode.ForeColor = SystemColors.WindowText;
					treArmor.SelectedNode.ToolTipText = objArmorMod.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsArmorGearNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Armor objFoundArmor = new Armor(_objCharacter);
				Gear objArmorGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objFoundArmor);
				if (objArmorGear != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objArmorGear.Notes;
					string strOldValue = objArmorGear.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objArmorGear.Notes = frmItemNotes.Notes;
						if (objArmorGear.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objArmorGear.Notes != string.Empty)
						treArmor.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treArmor.SelectedNode.ForeColor = SystemColors.WindowText;
					treArmor.SelectedNode.ToolTipText = objArmorGear.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsWeaponNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
				if (objWeapon != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objWeapon.Notes;
					string strOldValue = objWeapon.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objWeapon.Notes = frmItemNotes.Notes;
						if (objWeapon.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objWeapon.Notes != string.Empty)
						treWeapons.SelectedNode.ForeColor = Color.SaddleBrown;
					else
					{
						if (objWeapon.Category.StartsWith("Cyberware") || objWeapon.Category == "Gear")
							treWeapons.SelectedNode.ForeColor = SystemColors.GrayText;
						else
							treWeapons.SelectedNode.ForeColor = SystemColors.WindowText;
					}
					treWeapons.SelectedNode.ToolTipText = objWeapon.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsWeaponModNotes_Click(object sender, EventArgs e)
		{
			WeaponMod objMod = _objFunctions.FindWeaponMod(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			frmNotes frmItemNotes = new frmNotes();
			frmItemNotes.Notes = objMod.Notes;
			string strOldValue = objMod.Notes;
			frmItemNotes.ShowDialog(this);

			if (frmItemNotes.DialogResult == DialogResult.OK)
			{
				objMod.Notes = frmItemNotes.Notes;
				if (objMod.Notes != strOldValue)
				{
					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}

			if (objMod.Notes != string.Empty)
				treWeapons.SelectedNode.ForeColor = Color.SaddleBrown;
			else
				treWeapons.SelectedNode.ForeColor = SystemColors.WindowText;
			treWeapons.SelectedNode.ToolTipText = objMod.Notes;
		}

		private void tsWeaponAccessoryNotes_Click(object sender, EventArgs e)
		{
			WeaponAccessory objAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			frmNotes frmItemNotes = new frmNotes();
			frmItemNotes.Notes = objAccessory.Notes;
			string strOldValue = objAccessory.Notes;
			frmItemNotes.ShowDialog(this);

			if (frmItemNotes.DialogResult == DialogResult.OK)
			{
				objAccessory.Notes = frmItemNotes.Notes;
				if (objAccessory.Notes != strOldValue)
				{
					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}

			if (objAccessory.Notes != string.Empty)
				treWeapons.SelectedNode.ForeColor = Color.SaddleBrown;
			else
				treWeapons.SelectedNode.ForeColor = SystemColors.WindowText;
			treWeapons.SelectedNode.ToolTipText = objAccessory.Notes;
		}

		private void tsCyberwareNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Cyberware objCyberware = _objFunctions.FindCyberware(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware);
				if (objCyberware != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objCyberware.Notes;
					string strOldValue = objCyberware.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objCyberware.Notes = frmItemNotes.Notes;
						if (objCyberware.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objCyberware.Notes != string.Empty)
						treCyberware.SelectedNode.ForeColor = Color.SaddleBrown;
					else
					{
						if (objCyberware.Capacity == "[*]")
							treCyberware.SelectedNode.ForeColor = SystemColors.GrayText;
						else
							treCyberware.SelectedNode.ForeColor = SystemColors.WindowText;
					}
					treCyberware.SelectedNode.ToolTipText = objCyberware.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsQualityNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Quality objQuality = _objFunctions.FindQuality(treQualities.SelectedNode.Tag.ToString(), _objCharacter.Qualities);
				if (objQuality != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objQuality.Notes;
					string strOldValue = objQuality.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objQuality.Notes = frmItemNotes.Notes;
						if (objQuality.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objQuality.Notes != string.Empty)
						treQualities.SelectedNode.ForeColor = Color.SaddleBrown;
					else
					{
						if (objQuality.OriginSource == QualitySource.Metatype || objQuality.OriginSource == QualitySource.MetatypeRemovable)
							treQualities.SelectedNode.ForeColor = SystemColors.GrayText;
						else
							treQualities.SelectedNode.ForeColor = SystemColors.WindowText;
					}
					treQualities.SelectedNode.ToolTipText = objQuality.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsMartialArtsNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				MartialArt objMartialArt = _objFunctions.FindMartialArt(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArts);
				if (objMartialArt != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objMartialArt.Notes;
					string strOldValue = objMartialArt.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objMartialArt.Notes = frmItemNotes.Notes;
						if (objMartialArt.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objMartialArt.Notes != string.Empty)
						treMartialArts.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treMartialArts.SelectedNode.ForeColor = SystemColors.WindowText;
					treMartialArts.SelectedNode.ToolTipText = objMartialArt.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsMartialArtManeuverNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				MartialArtManeuver objMartialArtManeuver = _objFunctions.FindMartialArtManeuver(treMartialArts.SelectedNode.Tag.ToString(), _objCharacter.MartialArtManeuvers);
				if (objMartialArtManeuver != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objMartialArtManeuver.Notes;
					string strOldValue = objMartialArtManeuver.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objMartialArtManeuver.Notes = frmItemNotes.Notes;
						if (objMartialArtManeuver.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objMartialArtManeuver.Notes != string.Empty)
						treMartialArts.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treMartialArts.SelectedNode.ForeColor = SystemColors.WindowText;
					treMartialArts.SelectedNode.ToolTipText = objMartialArtManeuver.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsSpellNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Spell objSpell = _objFunctions.FindSpell(treSpells.SelectedNode.Tag.ToString(), _objCharacter.Spells);
				if (objSpell != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objSpell.Notes;
					string strOldValue = objSpell.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objSpell.Notes = frmItemNotes.Notes;
						if (objSpell.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objSpell.Notes != string.Empty)
						treSpells.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treSpells.SelectedNode.ForeColor = SystemColors.WindowText;
					treSpells.SelectedNode.ToolTipText = objSpell.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsComplexFormNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
                ComplexForm objComplexForm = _objFunctions.FindComplexForm(treComplexForms.SelectedNode.Tag.ToString(), _objCharacter.ComplexForms);
				if (objComplexForm != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objComplexForm.Notes;
					string strOldValue = objComplexForm.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objComplexForm.Notes = frmItemNotes.Notes;
						if (objComplexForm.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objComplexForm.Notes != string.Empty)
						treComplexForms.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treComplexForms.SelectedNode.ForeColor = SystemColors.WindowText;
					treComplexForms.SelectedNode.ToolTipText = objComplexForm.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsCritterPowersNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				CritterPower objCritterPower = _objFunctions.FindCritterPower(treCritterPowers.SelectedNode.Tag.ToString(), _objCharacter.CritterPowers);
				if (objCritterPower != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objCritterPower.Notes;
					string strOldValue = objCritterPower.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objCritterPower.Notes = frmItemNotes.Notes;
						if (objCritterPower.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objCritterPower.Notes != string.Empty)
						treCritterPowers.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treCritterPowers.SelectedNode.ForeColor = SystemColors.WindowText;
					treCritterPowers.SelectedNode.ToolTipText = objCritterPower.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsMetamagicNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Metamagic objMetamagic = _objFunctions.FindMetamagic(treMetamagic.SelectedNode.Tag.ToString(), _objCharacter.Metamagics);
				if (objMetamagic != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objMetamagic.Notes;
					string strOldValue = objMetamagic.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objMetamagic.Notes = frmItemNotes.Notes;
						if (objMetamagic.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objMetamagic.Notes != string.Empty)
						treMetamagic.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treMetamagic.SelectedNode.ForeColor = SystemColors.WindowText;
					treMetamagic.SelectedNode.ToolTipText = objMetamagic.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsGearNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
				if (objGear != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objGear.Notes;
					string strOldValue = objGear.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objGear.Notes = frmItemNotes.Notes;
						if (objGear.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objGear.Notes != string.Empty)
						treGear.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treGear.SelectedNode.ForeColor = SystemColors.WindowText;
					treGear.SelectedNode.ToolTipText = objGear.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsGearPluginNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Gear objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
				if (objGear != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objGear.Notes;
					string strOldValue = objGear.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objGear.Notes = frmItemNotes.Notes;
						if (objGear.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objGear.Notes != string.Empty)
						treGear.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treGear.SelectedNode.ForeColor = SystemColors.WindowText;
					treGear.SelectedNode.ToolTipText = objGear.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsVehicleNotes_Click(object sender, EventArgs e)
		{
			Vehicle objVehicle = new Vehicle(_objCharacter);
			VehicleMod objMod = new VehicleMod(_objCharacter);
			bool blnFoundVehicle = false;
			bool blnFoundMod = false;
			try
			{
				foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
				{
					if (objCharacterVehicle.InternalId == treVehicles.SelectedNode.Tag.ToString())
					{
						objVehicle = objCharacterVehicle;
						blnFoundVehicle = true;
						break;
					}
					foreach (VehicleMod objVehicleMod in objCharacterVehicle.Mods)
					{
						if (objVehicleMod.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objMod = objVehicleMod;
							blnFoundMod = true;
							break;
						}
					}
				}

				if (blnFoundVehicle)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objVehicle.Notes;
					string strOldValue = objVehicle.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objVehicle.Notes = frmItemNotes.Notes;
						if (objVehicle.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objVehicle.Notes != string.Empty)
						treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
					treVehicles.SelectedNode.ToolTipText = objVehicle.Notes;
				}
				if (blnFoundMod)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objMod.Notes;
					string strOldValue = objMod.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objMod.Notes = frmItemNotes.Notes;
						if (objMod.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objMod.Notes != string.Empty)
						treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
					else
					{
						if (objMod.IncludedInVehicle)
							treVehicles.SelectedNode.ForeColor = SystemColors.GrayText;
						else
							treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
					}
					treVehicles.SelectedNode.ToolTipText = objMod.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsLifestyleNotes_Click(object sender, EventArgs e)
		{
			try
			{
				bool blnFound = false;
				Lifestyle objLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);
				if (objLifestyle != null)
					blnFound = true;

				if (blnFound)
				{
					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objLifestyle.Notes;
					string strOldValue = objLifestyle.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objLifestyle.Notes = frmItemNotes.Notes;
						if (objLifestyle.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objLifestyle.Notes != string.Empty)
						treLifestyles.SelectedNode.ForeColor = Color.SaddleBrown;
					else
						treLifestyles.SelectedNode.ForeColor = SystemColors.WindowText;
					treLifestyles.SelectedNode.ToolTipText = objLifestyle.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsVehicleWeaponNotes_Click(object sender, EventArgs e)
		{
			bool blnFound = false;
			Vehicle objFoundVehicle = new Vehicle(_objCharacter);
			Weapon objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
			if (objWeapon != null)
				blnFound = true;

			if (blnFound)
			{
				frmNotes frmItemNotes = new frmNotes();
				frmItemNotes.Notes = objWeapon.Notes;
				string strOldValue = objWeapon.Notes;
				frmItemNotes.ShowDialog(this);

				if (frmItemNotes.DialogResult == DialogResult.OK)
				{
					objWeapon.Notes = frmItemNotes.Notes;
					if (objWeapon.Notes != strOldValue)
					{
						_blnIsDirty = true;
						UpdateWindowTitle();
					}
				}

				if (objWeapon.Notes != string.Empty)
					treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
				else
				{
					if (objWeapon.Category.StartsWith("Cyberware") || objWeapon.Category == "Gear")
						treVehicles.SelectedNode.ForeColor = SystemColors.GrayText;
					else
						treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
				}
				treVehicles.SelectedNode.ToolTipText = objWeapon.Notes;
			}
		}

		private void tsVehicleName_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected.
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicleName"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectVehicleName"), LanguageManager.Instance.GetString("MessageTitle_SelectVehicle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			while (treVehicles.SelectedNode.Level > 1)
			{
				treVehicles.SelectedNode = treVehicles.SelectedNode.Parent;
			}

			// Get the information for the currently selected Vehicle.
			Vehicle objVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_VehicleName");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			objVehicle.VehicleName = frmPickText.SelectedValue;
			treVehicles.SelectedNode.Text = objVehicle.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsVehicleAddCyberware_Click(object sender, EventArgs e)
		{
			Vehicle objVehicle = new Vehicle(_objCharacter);
			VehicleMod objMod = _objFunctions.FindVehicleMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);

			if (objMod == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_VehicleCyberwarePlugin"), LanguageManager.Instance.GetString("MessageTitle_NoCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (!objMod.AllowCyberware)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_VehicleCyberwarePlugin"), LanguageManager.Instance.GetString("MessageTitle_NoCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectCyberware frmPickCyberware = new frmSelectCyberware(_objCharacter);
			frmPickCyberware.SetGrade = "Standard";
			frmPickCyberware.LockGrade();
			frmPickCyberware.ShowOnlySubsystems = true;
			frmPickCyberware.Subsystems = objMod.Subsystems;
			frmPickCyberware.AllowModularPlugins = objMod.AllowModularPlugins;
			frmPickCyberware.ShowDialog(this);

			if (frmPickCyberware.DialogResult == DialogResult.Cancel)
				return;

			// Open the Cyberware XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("cyberware.xml");

			XmlNode objXmlCyberware = objXmlDocument.SelectSingleNode("/chummer/cyberwares/cyberware[name = \"" + frmPickCyberware.SelectedCyberware + "\"]");

			// Create the Cyberware object.
			Cyberware objCyberware = new Cyberware(_objCharacter);
			List<Weapon> objWeapons = new List<Weapon>();
			TreeNode objNode = new TreeNode();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			objCyberware.Create(objXmlCyberware, _objCharacter, frmPickCyberware.SelectedGrade, Improvement.ImprovementSource.Cyberware, frmPickCyberware.SelectedRating, objNode, objWeapons, objWeaponNodes, false);
			if (objCyberware.InternalId == Guid.Empty.ToString())
				return;

			if (frmPickCyberware.FreeCost)
				objCyberware.Cost = "0";

			int intCost = objCyberware.TotalCost;

			// Multiply the cost if applicable.
			if (objCyberware.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objCyberware.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickCyberware.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					string strEntry = "";
					strEntry = LanguageManager.Instance.GetString("String_ExpensePurchaseVehicleCyberware");
					objExpense.Create(intCost * -1, strEntry + " " + objCyberware.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddVehicleModCyberware, objCyberware.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();
			objMod.Cyberware.Add(objCyberware);

			foreach (Weapon objWeapon in objWeapons)
			{
				objWeapon.VehicleMounted = true;
				objVehicle.Weapons.Add(objWeapon);
			}

			// Create the Weapon Node if one exists.
			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsVehicleWeapon;
				treVehicles.SelectedNode.Parent.Nodes.Add(objWeaponNode);
				treVehicles.SelectedNode.Parent.Expand();
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();

			if (frmPickCyberware.AddAgain)
				tsVehicleAddCyberware_Click(sender, e);
		}

		private void tsArmorName_Click(object sender, EventArgs e)
		{
			// Make sure a parent item is selected, then open the Select Accessory window.
			try
			{
				if (treArmor.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmorName"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectArmorName"), LanguageManager.Instance.GetString("MessageTitle_SelectArmor"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treArmor.SelectedNode.Level > 1)
				treArmor.SelectedNode = treArmor.SelectedNode.Parent;

			// Get the information for the currently selected Armor.
			Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);

			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_ArmorName");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			objArmor.ArmorName = frmPickText.SelectedValue;
			treArmor.SelectedNode.Text = objArmor.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsEditAdvancedLifestyle_Click(object sender, EventArgs e)
		{
			treLifestyles_DoubleClick(sender, e);
		}

		private void tsAdvancedLifestyleNotes_Click(object sender, EventArgs e)
		{
			tsLifestyleNotes_Click(sender, e);
		}

		private void tsEditLifestyle_Click(object sender, EventArgs e)
		{
			treLifestyles_DoubleClick(sender, e);
		}

		private void tsLifestyleName_Click(object sender, EventArgs e)
		{
			try
			{
				if (treLifestyles.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectLifestyleName"), LanguageManager.Instance.GetString("MessageTitle_SelectLifestyle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectLifestyleName"), LanguageManager.Instance.GetString("MessageTitle_SelectLifestyle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Get the information for the currently selected Lifestyle.
			Lifestyle objLifestyle = new Lifestyle(_objCharacter);
			foreach (Lifestyle objSelectedLifestyle in _objCharacter.Lifestyles)
			{
				if (objSelectedLifestyle.InternalId == treLifestyles.SelectedNode.Tag.ToString())
				{
					objLifestyle = objSelectedLifestyle;
					break;
				}
			}

			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_LifestyleName");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			objLifestyle.LifestyleName = frmPickText.SelectedValue;
			treLifestyles.SelectedNode.Text = objLifestyle.DisplayName;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsGearRenameLocation_Click(object sender, EventArgs e)
		{
			string strNewLocation = "";
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			strNewLocation = frmPickText.SelectedValue;

			int i = -1;
			foreach (string strLocation in _objCharacter.Locations)
			{
				i++;
				if (strLocation == treGear.SelectedNode.Text)
				{
					foreach (Gear objGear in _objCharacter.Gear)
					{
						if (objGear.Location == strLocation)
							objGear.Location = strNewLocation;
					}

					_objCharacter.Locations[i] = strNewLocation;
					treGear.SelectedNode.Text = strNewLocation;
					break;
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsWeaponRenameLocation_Click(object sender, EventArgs e)
		{
			string strNewLocation = "";
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			strNewLocation = frmPickText.SelectedValue;

			int i = -1;
			foreach (string strLocation in _objCharacter.WeaponLocations)
			{
				i++;
				if (strLocation == treWeapons.SelectedNode.Text)
				{
					foreach (Weapon objWeapon in _objCharacter.Weapons)
					{
						if (objWeapon.Location == strLocation)
							objWeapon.Location = strNewLocation;
					}

					_objCharacter.WeaponLocations[i] = strNewLocation;
					treWeapons.SelectedNode.Text = strNewLocation;
					break;
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsCreateSpell_Click(object sender, EventArgs e)
		{
			// Make sure the character has enough Karma before letting them select a Spell.
			if (_objCharacter.Karma < _objOptions.KarmaSpell)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Run through the list of Active Skills and pick out the two applicable ones.
			int intSkillValue = 0;
			foreach (SkillControl objSkillControl in panActiveSkills.Controls)
			{
				if ((objSkillControl.SkillName == "Spellcasting" || objSkillControl.SkillName == "Ritual Spellcasting") && objSkillControl.SkillRating > intSkillValue)
					intSkillValue = objSkillControl.SkillRating;
			}

			// The character is still allowed to add Spells, so show the Create Spell window.
			frmCreateSpell frmSpell = new frmCreateSpell(_objCharacter);
			frmSpell.ShowDialog(this);

			if (frmSpell.DialogResult == DialogResult.Cancel)
				return;

			Spell objSpell = frmSpell.SelectedSpell;
			TreeNode objNode = new TreeNode();
			objNode.Text = objSpell.DisplayName;
			objNode.Tag = objSpell.InternalId;
			objNode.ContextMenuStrip = cmsSpell;

			if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseSpend").Replace("{0}", objSpell.DisplayName).Replace("{1}", _objOptions.KarmaSpell.ToString())))
				return;

			_objCharacter.Spells.Add(objSpell);

			switch (objSpell.Category)
			{
				case "Combat":
					treSpells.Nodes[0].Nodes.Add(objNode);
					treSpells.Nodes[0].Expand();
					break;
				case "Detection":
					treSpells.Nodes[1].Nodes.Add(objNode);
					treSpells.Nodes[1].Expand();
					break;
				case "Health":
					treSpells.Nodes[2].Nodes.Add(objNode);
					treSpells.Nodes[2].Expand();
					break;
				case "Illusion":
					treSpells.Nodes[3].Nodes.Add(objNode);
					treSpells.Nodes[3].Expand();
					break;
				case "Manipulation":
					treSpells.Nodes[4].Nodes.Add(objNode);
					treSpells.Nodes[4].Expand();
					break;
				case "Geomancy Ritual":
					treSpells.Nodes[5].Nodes.Add(objNode);
					treSpells.Nodes[5].Expand();
					break;
			}

			treSpells.SelectedNode = objNode;

			// Create the Expense Log Entry.
			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			objEntry.Create(_objOptions.KarmaSpell * -1, LanguageManager.Instance.GetString("String_ExpenseLearnSpell") + " " + objSpell.Name, ExpenseType.Karma, DateTime.Now);
			_objCharacter.ExpenseEntries.Add(objEntry);
			_objCharacter.Karma -= _objOptions.KarmaSpell;

			ExpenseUndo objUndo = new ExpenseUndo();
			objUndo.CreateKarma(KarmaExpenseType.AddSpell, objSpell.InternalId);
			objEntry.Undo = objUndo;


			_objFunctions.SortTree(treSpells);
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsImprovementNotes_Click(object sender, EventArgs e)
		{
			try
			{
				if (treImprovements.SelectedNode.Level > 0)
				{
					Improvement objImprovement = new Improvement();
					foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
					{
						if (objCharacterImprovement.SourceName == treImprovements.SelectedNode.Tag.ToString())
						{
							objImprovement = objCharacterImprovement;
							break;
						}
					}

					frmNotes frmItemNotes = new frmNotes();
					frmItemNotes.Notes = objImprovement.Notes;
					string strOldValue = objImprovement.Notes;
					frmItemNotes.ShowDialog(this);

					if (frmItemNotes.DialogResult == DialogResult.OK)
					{
						objImprovement.Notes = frmItemNotes.Notes;
						if (objImprovement.Notes != strOldValue)
						{
							_blnIsDirty = true;
							UpdateWindowTitle();
						}
					}

					if (objImprovement.Notes != string.Empty)
					{
						if (objImprovement.Enabled)
							treImprovements.SelectedNode.ForeColor = Color.SaddleBrown;
						else
							treImprovements.SelectedNode.ForeColor = Color.SandyBrown;
					}
					else
					{
						if (objImprovement.Enabled)
							treImprovements.SelectedNode.ForeColor = SystemColors.WindowText;
						else
							treImprovements.SelectedNode.ForeColor = SystemColors.GrayText;
					}
					treImprovements.SelectedNode.ToolTipText = objImprovement.Notes;
				}
			}
			catch
			{
			}
		}

		private void tsArmorRenameLocation_Click(object sender, EventArgs e)
		{
			string strNewLocation = "";
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			strNewLocation = frmPickText.SelectedValue;

			int i = -1;
			foreach (string strLocation in _objCharacter.ArmorBundles)
			{
				i++;
				if (strLocation == treArmor.SelectedNode.Text)
				{
					foreach (Armor objArmor in _objCharacter.Armor)
					{
						if (objArmor.Location == strLocation)
							objArmor.Location = strNewLocation;
					}

					_objCharacter.ArmorBundles[i] = strNewLocation;
					treArmor.SelectedNode.Text = strNewLocation;
					break;
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsImprovementRenameLocation_Click(object sender, EventArgs e)
		{
			string strNewLocation = "";
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			strNewLocation = frmPickText.SelectedValue;

			int i = -1;
			foreach (string strLocation in _objCharacter.ImprovementGroups)
			{
				i++;
				if (strLocation == treImprovements.SelectedNode.Text)
				{
					foreach (Improvement objImprovement in _objCharacter.Improvements)
					{
						if (objImprovement.CustomGroup == strLocation)
							objImprovement.CustomGroup = strNewLocation;
					}

					_objCharacter.ImprovementGroups[i] = strNewLocation;
					treImprovements.SelectedNode.Text = strNewLocation;
					break;
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsCyberwareAddGear_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treCyberware.SelectedNode.Level == 0)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectCyberware"), LanguageManager.Instance.GetString("MessageTitle_SelectCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_SelectCyberware"), LanguageManager.Instance.GetString("MessageTitle_SelectCyberware"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			Cyberware objCyberware = new Cyberware(_objCharacter);
			foreach (Cyberware objCharacterCyberware in _objCharacter.Cyberware)
			{
				if (objCharacterCyberware.InternalId == treCyberware.SelectedNode.Tag.ToString())
				{
					objCyberware = objCharacterCyberware;
					break;
				}

				foreach (Cyberware objChild in objCharacterCyberware.Children)
				{
					if (objChild.InternalId == treCyberware.SelectedNode.Tag.ToString())
					{
						objCyberware = objChild;
						break;
					}
				}
			}

			// Make sure the Cyberware is allowed to accept Gear.
			if (objCyberware.AllowGear == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CyberwareGear"), LanguageManager.Instance.GetString("MessageTitle_CyberwareGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			string strCategories = "";
			foreach (XmlNode objXmlCategory in objCyberware.AllowGear)
				strCategories += objXmlCategory.InnerText + ",";
			frmPickGear.AllowedCategories = strCategories;
			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			TreeNode objNode = new TreeNode();

			// Open the Gear XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			Gear objNewGear = new Gear(_objCharacter);
			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objNewGear = objCommlink;
					break;
				default:
					Gear objGear = new Gear(_objCharacter);
					objGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, true, true, frmPickGear.Aerodynamic);
					objGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objGear.DisplayName;

					objNewGear = objGear;
					break;
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objNewGear.Cost = (Convert.ToDouble(objNewGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objNewGear.Cost != "")
					objNewGear.Cost = "(" + objNewGear.Cost + ") * 0.1";
				if (objNewGear.Cost3 != "")
					objNewGear.Cost3 = "(" + objNewGear.Cost3 + ") * 0.1";
				if (objNewGear.Cost6 != "")
					objNewGear.Cost6 = "(" + objNewGear.Cost6 + ") * 0.1";
				if (objNewGear.Cost10 != "")
					objNewGear.Cost10 = "(" + objNewGear.Cost10 + ") * 0.1";
				if (objNewGear.Extra == "")
					objNewGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objNewGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objNewGear.Cost = "0";
				objNewGear.Cost3 = "0";
				objNewGear.Cost6 = "0";
				objNewGear.Cost10 = "0";
			}

			int intCost = objNewGear.TotalCost;

			// Multiply the cost if applicable.
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objNewGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsCyberwareAddGear_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseCyberwearGear") + " " + objNewGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddCyberwareGear, objNewGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			// Create any Weapons that came with this Gear.
			foreach (Weapon objWeapon in objWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeapon;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			objCyberware.Gear.Add(objNewGear);

			objNode.ContextMenuStrip = cmsCyberwareGear;
			treCyberware.SelectedNode.Nodes.Add(objNode);
			treCyberware.SelectedNode.Expand();

			UpdateCharacterInfo();

			if (frmPickGear.AddAgain)
				tsCyberwareAddGear_Click(sender, e);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsCyberwareGearMenuAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Make sure a parent items is selected, then open the Select Gear window.
			try
			{
				if (treCyberware.SelectedNode.Level < 2)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}
			catch
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (treCyberware.SelectedNode.Level > 3)
				treCyberware.SelectedNode = treCyberware.SelectedNode.Parent;

			// Locate the Vehicle Sensor Gear.
			bool blnFound = false;
			Cyberware objFoundCyber = new Cyberware(_objCharacter);
			Gear objSensor = _objFunctions.FindCyberwareGear(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware, out objFoundCyber);
			if (objSensor != null)
				blnFound = true;

			// Make sure the Gear was found.
			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSensor.Name + "\" and category = \"" + objSensor.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			//frmPickGear.ShowNegativeCapacityOnly = true;

			if (objXmlGear.InnerXml.Contains("<addoncategory>"))
			{
				string strCategories = "";
				foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
					strCategories += objXmlCategory.InnerText + ",";
				// Remove the trailing comma.
				strCategories = strCategories.Substring(0, strCategories.Length - 1);
				frmPickGear.AddCategory(strCategories);
			}

			if (frmPickGear.AllowedCategories != "")
				frmPickGear.AllowedCategories += objSensor.Category + ",";

			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			// Open the Gear XML file and locate the selected piece.
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Gear objGear = new Gear(_objCharacter);

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objGear = objCommlink;
					break;
				default:
					Gear objNewGear = new Gear(_objCharacter);
					objNewGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, true, true, frmPickGear.Aerodynamic);
					objNewGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objNewGear.DisplayName;

					objGear = objNewGear;
					break;
			}

			if (objGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objGear.Cost = (Convert.ToDouble(objGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objGear.Cost != "")
					objGear.Cost = "(" + objGear.Cost + ") * 0.1";
				if (objGear.Cost3 != "")
					objGear.Cost3 = "(" + objGear.Cost3 + ") * 0.1";
				if (objGear.Cost6 != "")
					objGear.Cost6 = "(" + objGear.Cost6 + ") * 0.1";
				if (objGear.Cost10 != "")
					objGear.Cost10 = "(" + objGear.Cost10 + ") * 0.1";
				if (objGear.Extra == "")
					objGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			objNode.Text = objGear.DisplayName;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleSensorAddAsPlugin_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseCyberwearGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddCyberwareGear, objGear.InternalId, frmPickGear.SelectedQty);
					objExpense.Undo = objUndo;
				}
			}

			if (treCyberware.SelectedNode.Level < 3)
				objNode.ContextMenuStrip = cmsCyberwareGear;

			treCyberware.SelectedNode.Nodes.Add(objNode);
			treCyberware.SelectedNode.Expand();

			objGear.Parent = objSensor;
			objSensor.Children.Add(objGear);

			if (frmPickGear.AddAgain)
				tsCyberwareGearMenuAddAsPlugin_Click(sender, e);

			UpdateCharacterInfo();
			RefreshSelectedCyberware();
		}

		private void tsWeaponAccessoryAddGear_Click(object sender, EventArgs e)
		{
			WeaponAccessory objAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			// Make sure the Weapon Accessory is allowed to accept Gear.
			if (objAccessory.AllowGear == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_WeaponGear"), LanguageManager.Instance.GetString("MessageTitle_CyberwareGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			string strCategories = "";
			foreach (XmlNode objXmlCategory in objAccessory.AllowGear)
				strCategories += objXmlCategory.InnerText + ",";
			frmPickGear.AllowedCategories = strCategories;
			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			TreeNode objNode = new TreeNode();

			// Open the Gear XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			Gear objNewGear = new Gear(_objCharacter);
			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objNewGear = objCommlink;
					break;
				default:
					Gear objGear = new Gear(_objCharacter);
					objGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, true, true, frmPickGear.Aerodynamic);
					objGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objGear.DisplayName;

					objNewGear = objGear;
					break;
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objNewGear.Cost = (Convert.ToDouble(objNewGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objNewGear.Cost != "")
					objNewGear.Cost = "(" + objNewGear.Cost + ") * 0.1";
				if (objNewGear.Cost3 != "")
					objNewGear.Cost3 = "(" + objNewGear.Cost3 + ") * 0.1";
				if (objNewGear.Cost6 != "")
					objNewGear.Cost6 = "(" + objNewGear.Cost6 + ") * 0.1";
				if (objNewGear.Cost10 != "")
					objNewGear.Cost10 = "(" + objNewGear.Cost10 + ") * 0.1";
				if (objNewGear.Extra == "")
					objNewGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objNewGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objNewGear.Cost = "0";
				objNewGear.Cost3 = "0";
				objNewGear.Cost6 = "0";
				objNewGear.Cost10 = "0";
			}

			int intCost = objNewGear.TotalCost;

			// Multiply the cost if applicable.
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objNewGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsWeaponAccessoryAddGear_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponGear") + " " + objNewGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponGear, objNewGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			// Create any Weapons that came with this Gear.
			foreach (Weapon objWeapon in objWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeaponAccessoryGear;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			objAccessory.Gear.Add(objNewGear);

			objNode.ContextMenuStrip = cmsWeaponAccessoryGear;
			treWeapons.SelectedNode.Nodes.Add(objNode);
			treWeapons.SelectedNode.Expand();

			UpdateCharacterInfo();

			if (frmPickGear.AddAgain)
				tsWeaponAccessoryAddGear_Click(sender, e);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void tsWeaponAccessoryGearMenuAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Locate the Vehicle Sensor Gear.
			bool blnFound = false;
			WeaponAccessory objFoundAccessory = new WeaponAccessory(_objCharacter);
			Gear objSensor = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objFoundAccessory);
			if (objSensor != null)
				blnFound = true;

			// Make sure the Gear was found.
			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSensor.Name + "\" and category = \"" + objSensor.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			//frmPickGear.ShowNegativeCapacityOnly = true;

			if (objXmlGear.InnerXml.Contains("<addoncategory>"))
			{
				string strCategories = "";
				foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
					strCategories += objXmlCategory.InnerText + ",";
				// Remove the trailing comma.
				strCategories = strCategories.Substring(0, strCategories.Length - 1);
				frmPickGear.AddCategory(strCategories);
			}

			if (frmPickGear.AllowedCategories != "")
				frmPickGear.AllowedCategories += objSensor.Category + ",";

			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			// Open the Gear XML file and locate the selected piece.
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Gear objGear = new Gear(_objCharacter);

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objGear = objCommlink;
					break;
				default:
					Gear objNewGear = new Gear(_objCharacter);
					objNewGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, true, true, frmPickGear.Aerodynamic);
					objNewGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objNewGear.DisplayName;

					objGear = objNewGear;
					break;
			}

			if (objGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objGear.Cost = (Convert.ToDouble(objGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objGear.Cost != "")
					objGear.Cost = "(" + objGear.Cost + ") * 0.1";
				if (objGear.Cost3 != "")
					objGear.Cost3 = "(" + objGear.Cost3 + ") * 0.1";
				if (objGear.Cost6 != "")
					objGear.Cost6 = "(" + objGear.Cost6 + ") * 0.1";
				if (objGear.Cost10 != "")
					objGear.Cost10 = "(" + objGear.Cost10 + ") * 0.1";
				if (objGear.Extra == "")
					objGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			objNode.Text = objGear.DisplayName;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleSensorAddAsPlugin_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponGear, objGear.InternalId, frmPickGear.SelectedQty);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsCyberwareGear;

			treWeapons.SelectedNode.Nodes.Add(objNode);
			treWeapons.SelectedNode.Expand();

			objGear.Parent = objSensor;
			objSensor.Children.Add(objGear);

			if (frmPickGear.AddAgain)
				tsWeaponAccessoryGearMenuAddAsPlugin_Click(sender, e);

			UpdateCharacterInfo();
			RefreshSelectedWeapon();
		}

		private void tsVehicleRenameLocation_Click(object sender, EventArgs e)
		{
			string strNewLocation = "";
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel)
				return;

			// Determine if this is a Location.
			TreeNode objVehicleNode = treVehicles.SelectedNode;
			do
			{
				objVehicleNode = objVehicleNode.Parent;
			} while (objVehicleNode.Level > 1);

			// Get a reference to the affected Vehicle.
			Vehicle objVehicle = new Vehicle(_objCharacter);
			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				if (objCharacterVehicle.InternalId == objVehicleNode.Tag.ToString())
				{
					objVehicle = objCharacterVehicle;
					break;
				}
			}

			strNewLocation = frmPickText.SelectedValue;

			int i = -1;
			foreach (string strLocation in objVehicle.Locations)
			{
				i++;
				if (strLocation == treVehicles.SelectedNode.Text)
				{
					foreach (Gear objGear in objVehicle.Gear)
					{
						if (objGear.Location == strLocation)
							objGear.Location = strNewLocation;
					}

					objVehicle.Locations[i] = strNewLocation;
					treVehicles.SelectedNode.Text = strNewLocation;
					break;
				}
			}
		}

		private void tsCreateNaturalWeapon_Click(object sender, EventArgs e)
		{
			frmNaturalWeapon frmCreateNaturalWeapon = new frmNaturalWeapon(_objCharacter);
			frmCreateNaturalWeapon.ShowDialog(this);

			if (frmCreateNaturalWeapon.DialogResult == DialogResult.Cancel)
				return;

			Weapon objWeapon = frmCreateNaturalWeapon.SelectedWeapon;
			_objCharacter.Weapons.Add(objWeapon);
			_objFunctions.CreateWeaponTreeNode(objWeapon, treWeapons.Nodes[0], cmsWeapon, cmsWeaponMod, cmsWeaponAccessory, cmsWeaponAccessoryGear);

			_blnIsDirty = true;
			UpdateCharacterInfo();
			UpdateWindowTitle();
		}

		private void tsVehicleWeaponAccessoryNotes_Click(object sender, EventArgs e)
		{
			WeaponAccessory objAccessory = _objFunctions.FindVehicleWeaponAccessory(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmNotes frmItemNotes = new frmNotes();
			frmItemNotes.Notes = objAccessory.Notes;
			string strOldValue = objAccessory.Notes;
			frmItemNotes.ShowDialog(this);

			if (frmItemNotes.DialogResult == DialogResult.OK)
			{
				objAccessory.Notes = frmItemNotes.Notes;
				if (objAccessory.Notes != strOldValue)
				{
					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}

			if (objAccessory.Notes != string.Empty)
				treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
			else
				treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
			treVehicles.SelectedNode.ToolTipText = objAccessory.Notes;
		}

		private void tsVehicleWeaponModNotes_Click(object sender, EventArgs e)
		{
			WeaponMod objMod = _objFunctions.FindVehicleWeaponMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			frmNotes frmItemNotes = new frmNotes();
			frmItemNotes.Notes = objMod.Notes;
			string strOldValue = objMod.Notes;
			frmItemNotes.ShowDialog(this);

			if (frmItemNotes.DialogResult == DialogResult.OK)
			{
				objMod.Notes = frmItemNotes.Notes;
				if (objMod.Notes != strOldValue)
				{
					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}

			if (objMod.Notes != string.Empty)
				treVehicles.SelectedNode.ForeColor = Color.SaddleBrown;
			else
				treVehicles.SelectedNode.ForeColor = SystemColors.WindowText;
			treVehicles.SelectedNode.ToolTipText = objMod.Notes;
		}

		private void tsVehicleWeaponAccessoryGearMenuAddAsPlugin_Click(object sender, EventArgs e)
		{
			// Locate the Vehicle Sensor Gear.
			bool blnFound = false;
			Vehicle objFoundVehicle = new Vehicle(_objCharacter);
			Gear objSensor = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
			if (objSensor != null)
				blnFound = true;

			// Make sure the Gear was found.
			if (!blnFound)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_ModifyVehicleGear"), LanguageManager.Instance.GetString("MessageTitle_SelectGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSensor.Name + "\" and category = \"" + objSensor.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			//frmPickGear.ShowNegativeCapacityOnly = true;

			if (objXmlGear.InnerXml.Contains("<addoncategory>"))
			{
				string strCategories = "";
				foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
					strCategories += objXmlCategory.InnerText + ",";
				// Remove the trailing comma.
				strCategories = strCategories.Substring(0, strCategories.Length - 1);
				frmPickGear.AddCategory(strCategories);
			}

			if (frmPickGear.AllowedCategories != "")
				frmPickGear.AllowedCategories += objSensor.Category + ",";

			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			// Open the Gear XML file and locate the selected piece.
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Gear objGear = new Gear(_objCharacter);

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, false);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objGear = objCommlink;
					break;
				default:
					Gear objNewGear = new Gear(_objCharacter);
					objNewGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, false, true, frmPickGear.Aerodynamic);
					objNewGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objNewGear.DisplayName;

					objGear = objNewGear;
					break;
			}

			if (objGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objGear.Cost = (Convert.ToDouble(objGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objGear.Cost != "")
					objGear.Cost = "(" + objGear.Cost + ") * 0.1";
				if (objGear.Cost3 != "")
					objGear.Cost3 = "(" + objGear.Cost3 + ") * 0.1";
				if (objGear.Cost6 != "")
					objGear.Cost6 = "(" + objGear.Cost6 + ") * 0.1";
				if (objGear.Cost10 != "")
					objGear.Cost10 = "(" + objGear.Cost10 + ") * 0.1";
				if (objGear.Extra == "")
					objGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objGear.Cost = "0";
				objGear.Cost3 = "0";
				objGear.Cost6 = "0";
				objGear.Cost10 = "0";
			}

			objNode.Text = objGear.DisplayName;

			int intCost = objGear.TotalCost;

			// Multiply the cost if applicable.
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleSensorAddAsPlugin_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponGear") + " " + objGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponGear, objGear.InternalId, frmPickGear.SelectedQty);
					objExpense.Undo = objUndo;
				}
			}

			objNode.ContextMenuStrip = cmsCyberwareGear;

			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			objGear.Parent = objSensor;
			objSensor.Children.Add(objGear);

			if (frmPickGear.AddAgain)
				tsVehicleWeaponAccessoryGearMenuAddAsPlugin_Click(sender, e);

			UpdateCharacterInfo();
			RefreshSelectedVehicle();
		}

		private void tsVehicleWeaponAccessoryAddGear_Click(object sender, EventArgs e)
		{
			WeaponAccessory objAccessory = _objFunctions.FindVehicleWeaponAccessory(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);

			// Make sure the Weapon Accessory is allowed to accept Gear.
			if (objAccessory.AllowGear == null)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_WeaponGear"), LanguageManager.Instance.GetString("MessageTitle_CyberwareGear"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			string strCategories = "";
			foreach (XmlNode objXmlCategory in objAccessory.AllowGear)
				strCategories += objXmlCategory.InnerText + ",";
			frmPickGear.AllowedCategories = strCategories;
			frmPickGear.ShowDialog(this);

			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return;

			TreeNode objNode = new TreeNode();

			// Open the Gear XML file and locate the selected piece.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			Gear objNewGear = new Gear(_objCharacter);
			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, false);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objNewGear = objCommlink;
					break;
				default:
					Gear objGear = new Gear(_objCharacter);
					objGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", frmPickGear.Hacked, frmPickGear.InherentProgram, false, true, frmPickGear.Aerodynamic);
					objGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objGear.DisplayName;

					objNewGear = objGear;
					break;
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objNewGear.Cost = (Convert.ToDouble(objNewGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();
			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objNewGear.Cost != "")
					objNewGear.Cost = "(" + objNewGear.Cost + ") * 0.1";
				if (objNewGear.Cost3 != "")
					objNewGear.Cost3 = "(" + objNewGear.Cost3 + ") * 0.1";
				if (objNewGear.Cost6 != "")
					objNewGear.Cost6 = "(" + objNewGear.Cost6 + ") * 0.1";
				if (objNewGear.Cost10 != "")
					objNewGear.Cost10 = "(" + objNewGear.Cost10 + ") * 0.1";
				if (objNewGear.Extra == "")
					objNewGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objNewGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}
			// If the item was marked as free, change its cost.
			if (frmPickGear.FreeCost)
			{
				objNewGear.Cost = "0";
				objNewGear.Cost3 = "0";
				objNewGear.Cost6 = "0";
				objNewGear.Cost10 = "0";
			}

			int intCost = objNewGear.TotalCost;

			// Multiply the cost if applicable.
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					_objFunctions.DeleteGear(objNewGear, treWeapons, _objImprovementManager);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					if (frmPickGear.AddAgain)
						tsVehicleWeaponAccessoryAddGear_Click(sender, e);

					return;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseWeaponGear") + " " + objNewGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddWeaponGear, objNewGear.InternalId, 1);
					objExpense.Undo = objUndo;
				}
			}

			objAccessory.Gear.Add(objNewGear);

			objNode.ContextMenuStrip = cmsVehicleWeaponAccessoryGear;
			treVehicles.SelectedNode.Nodes.Add(objNode);
			treVehicles.SelectedNode.Expand();

			UpdateCharacterInfo();

			if (frmPickGear.AddAgain)
				tsVehicleWeaponAccessoryAddGear_Click(sender, e);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Additional Common Tab Control Events
		private void treQualities_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Locate the selected Quality.
			lblQualitySource.Text = "";
			tipTooltip.SetToolTip(lblQualitySource, null);
			try
			{
				if (treQualities.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			Quality objQuality = _objFunctions.FindQuality(treQualities.SelectedNode.Tag.ToString(), _objCharacter.Qualities);

			string strBook = _objOptions.LanguageBookShort(objQuality.Source);
			string strPage = objQuality.Page;
			lblQualitySource.Text = strBook + " " + strPage;
			tipTooltip.SetToolTip(lblQualitySource, _objOptions.LanguageBookLong(objQuality.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objQuality.Page);
			lblQualityBP.Text = (objQuality.BP * _objOptions.KarmaQuality).ToString() + " " + LanguageManager.Instance.GetString("String_Karma");
		}
		#endregion

		#region Additional Cyberware Tab Control Events
		private void treCyberware_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedCyberware();
		}
		#endregion

		#region Additional Street Gear Tab Control Events
		private void treWeapons_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedWeapon();
			RefreshPasteStatus();
		}

		private void treWeapons_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (treWeapons.SelectedNode.Level != 1 && treWeapons.SelectedNode.Level != 0)
					return;

				// Do not allow the root element to be moved.
				if (treWeapons.SelectedNode.Tag.ToString() == "Node_SelectedWeapons")
					return;
			}
			catch
			{
				return;
			}
			_intDragLevel = treWeapons.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treWeapons_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treWeapons_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treWeapons.Nodes[treWeapons.Nodes.Count - 1].Nodes.Count;
				nodDestination = treWeapons.Nodes[treWeapons.Nodes.Count - 1];
			}

			if (treWeapons.SelectedNode.Level == 1)
				_objController.MoveWeaponNode(intNewIndex, nodDestination, treWeapons);
			else
				_objController.MoveWeaponRoot(intNewIndex, nodDestination, treWeapons);

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treWeapons, null);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treWeapons_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (objNode.Level <= _intDragLevel)
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treWeapons, objNode);
		}

		private void treArmor_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (treArmor.SelectedNode.Level == 0)
			{
				cmdArmorEquipAll.Visible = true;
				cmdArmorUnEquipAll.Visible = true;
			}
			else
			{
				cmdArmorEquipAll.Visible = false;
				cmdArmorUnEquipAll.Visible = false;
			}

			if (treArmor.SelectedNode.Level == 1)
			{
				cmdArmorDecrease.Enabled = true;
				cmdArmorIncrease.Enabled = true;
			}
			else if (treArmor.SelectedNode.Level == 2)
			{
				cmdArmorDecrease.Enabled = false;
				cmdArmorIncrease.Enabled = false;
			}

			RefreshSelectedArmor();
			RefreshPasteStatus();
		}

		private void treArmor_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (treArmor.SelectedNode.Level != 1)
					return;
			}
			catch
			{
				return;
			}
			_intDragLevel = treArmor.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treArmor_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treArmor_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treArmor.Nodes[treArmor.Nodes.Count - 1].Nodes.Count;
				nodDestination = treArmor.Nodes[treArmor.Nodes.Count - 1];
			}

			_objController.MoveArmorNode(intNewIndex, nodDestination, treArmor);

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treArmor, null);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treArmor_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (objNode.Level <= _intDragLevel)
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treArmor, objNode);
		}

		private void treLifestyles_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedLifestyle();
			RefreshPasteStatus();
		}

		private void treLifestyles_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				if (treLifestyles.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			// Locate the selected Lifestyle.
			Lifestyle objLifestyle = new Lifestyle(_objCharacter);
			string strGuid = "";
			int intMonths = 0;
			int intPosition = -1;
			foreach (Lifestyle objCharacterLifestyle in _objCharacter.Lifestyles)
			{
				intPosition++;
				if (objCharacterLifestyle.InternalId == treLifestyles.SelectedNode.Tag.ToString())
				{
					objLifestyle = objCharacterLifestyle;
					strGuid = objLifestyle.InternalId;
					intMonths = objLifestyle.Months;
					break;
				}
			}

			Lifestyle objNewLifestyle = new Lifestyle(_objCharacter);
			if (objLifestyle.BaseLifestyle != "")
			{
				// Edit Advanced Lifestyle.
				frmSelectAdvancedLifestyle frmPickLifestyle = new frmSelectAdvancedLifestyle(objNewLifestyle, _objCharacter);
				frmPickLifestyle.SetLifestyle(objLifestyle);
				frmPickLifestyle.ShowDialog(this);

				if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
					return;

				// Update the selected Lifestyle and refresh the list.
				objLifestyle = frmPickLifestyle.SelectedLifestyle;
				objLifestyle.SetInternalId(strGuid);
				objLifestyle.Months = intMonths;
				_objCharacter.Lifestyles[intPosition] = objLifestyle;
				treLifestyles.SelectedNode.Text = objLifestyle.DisplayNameShort;
				RefreshSelectedLifestyle();
				UpdateCharacterInfo();
			}
			else
			{
				// Edit Basic Lifestyle.
				frmSelectLifestyle frmPickLifestyle = new frmSelectLifestyle(objNewLifestyle, _objCharacter);
				frmPickLifestyle.SetLifestyle(objLifestyle);
				frmPickLifestyle.ShowDialog(this);

				if (frmPickLifestyle.DialogResult == DialogResult.Cancel)
					return;

				// Update the selected Lifestyle and refresh the list.
				objLifestyle = frmPickLifestyle.SelectedLifestyle;
				objLifestyle.SetInternalId(strGuid);
				objLifestyle.Months = intMonths;
				_objCharacter.Lifestyles[intPosition] = objLifestyle;
				treLifestyles.SelectedNode.Text = objLifestyle.DisplayName;
				RefreshSelectedLifestyle();
				UpdateCharacterInfo();
			}
		}

		private void treLifestyles_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (treLifestyles.SelectedNode.Level != 1)
					return;
			}
			catch
			{
				return;
			}
			_intDragLevel = treLifestyles.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treLifestyles_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treLifestyles_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treLifestyles.Nodes[treLifestyles.Nodes.Count - 1].Nodes.Count;
				nodDestination = treLifestyles.Nodes[treLifestyles.Nodes.Count - 1];
			}

			_objController.MoveLifestyleNode(intNewIndex, nodDestination, treLifestyles);

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treLifestyles, null);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treLifestyles_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (objNode.Level <= _intDragLevel)
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treLifestyles, objNode);
		}

		private void treGear_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedGear();
			RefreshPasteStatus();
		}

		private void chkArmorEquipped_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			// Locate the selected Armor or Armor Mod.
			try
			{
				if (treArmor.SelectedNode.Level == 1)
				{
					Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
					if (objArmor != null)
					{
						objArmor.Equipped = chkArmorEquipped.Checked;
						if (chkArmorEquipped.Checked)
						{
							// Add the Armor's Improevments to the character.
							if (objArmor.Bonus != null)
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId, objArmor.Bonus, false, 1, objArmor.DisplayNameShort);
							// Add the Improvements from any Armor Mods in the Armor.
							foreach (ArmorMod objMod in objArmor.ArmorMods)
							{
								if (objMod.Bonus != null && objMod.Equipped)
									_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId, objMod.Bonus, false, objMod.Rating, objMod.DisplayNameShort);
							}
							// Add the Improvements from any Gear in the Armor.
							foreach (Gear objGear in objArmor.Gear)
							{
								if (objGear.Bonus != null && objGear.Equipped)
									_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objGear.Bonus, false, objGear.Rating, objGear.DisplayNameShort);
							}
						}
						else
						{
							// Remove any Improvements the Armor created.
							if (objArmor.Bonus != null)
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Armor, objArmor.InternalId);
							// Remove any Improvements from any Armor Mods in the Armor.
							foreach (ArmorMod objMod in objArmor.ArmorMods)
							{
								if (objMod.Bonus != null)
									_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
							}
							// Remove any Improvements from any Gear in the Armor.
							foreach (Gear objGear in objArmor.Gear)
							{
								if (objGear.Bonus != null)
									_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
							}
						}
					}
				}
				else if (treArmor.SelectedNode.Level > 1)
				{
					ArmorMod objMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
					if (objMod != null)
					{
						objMod.Equipped = chkArmorEquipped.Checked;
						if (chkArmorEquipped.Checked)
						{
							// Add the Mod's Improevments to the character.
							if (objMod.Bonus != null && objMod.Parent.Equipped)
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId, objMod.Bonus, false, objMod.Rating, objMod.DisplayNameShort);
						}
						else
						{
							// Remove any Improvements the Mod created.
							if (objMod.Bonus != null)
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorMod, objMod.InternalId);
						}
					}

					Armor objFoundArmor = new Armor(_objCharacter);
					Gear objGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objFoundArmor);
					if (objGear != null)
					{
						objGear.Equipped = chkArmorEquipped.Checked;
						if (chkArmorEquipped.Checked)
						{
							// Add the Gear's Improevments to the character.
							if (objGear.Bonus != null && objFoundArmor.Equipped)
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objGear.Bonus, false, objGear.Rating, objGear.DisplayNameShort);
						}
						else
						{
							// Remove any Improvements the Gear created.
							if (objGear.Bonus != null)
								_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
						}
					}
				}
				RefreshSelectedArmor();
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cmdFireWeapon_Click(object sender, EventArgs e)
		{
			// "Click" the first menu item available.
			if (cmsAmmoSingleShot.Enabled)
				cmsAmmoSingleShot_Click(sender, e);
			else
			{
				if (cmsAmmoShortBurst.Enabled)
					cmsAmmoShortBurst_Click(sender, e);
				else
					cmsAmmoLongBurst_Click(sender, e);
			}
		}

		private void cmdReloadWeapon_Click(object sender, EventArgs e)
		{
			List<Gear> lstAmmo = new List<Gear>();
			List<string> lstCount = new List<string>();
			bool blnExternalSource = false;

			Gear objExternalSource = new Gear(_objCharacter);
			objExternalSource.Name = "External Source";

			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			// Determine which loading methods are available to the Weapon.
			if (objWeapon.CalculatedAmmo(true).Contains(" or ") || objWeapon.CalculatedAmmo(true).Contains("x") || objWeapon.CalculatedAmmo(true).Contains("Special") || objWeapon.CalculatedAmmo(true).Contains("+"))
			{
				string strWeaponAmmo = objWeapon.CalculatedAmmo(true).ToLower();
				if (strWeaponAmmo.Contains("external source"))
					blnExternalSource = true;
				// Get rid of external source, special, or belt, and + energy.
				strWeaponAmmo = strWeaponAmmo.Replace("external source", "100");
				strWeaponAmmo = strWeaponAmmo.Replace("special", "100");
				strWeaponAmmo = strWeaponAmmo.Replace(" + energy", "");
				strWeaponAmmo = strWeaponAmmo.Replace(" or belt", " or 250(belt)");

				string[] strSplit = new string[] { " or " };
				string[] strAmmos = strWeaponAmmo.Split(strSplit, StringSplitOptions.RemoveEmptyEntries);

				foreach (string strAmmo in strAmmos)
				{
					string strThisAmmo = strAmmo;
					if (strThisAmmo.StartsWith("2x") || strThisAmmo.StartsWith("3x") || strThisAmmo.StartsWith("4x"))
						strThisAmmo = strThisAmmo.Substring(2, strThisAmmo.Length - 2);
					if (strThisAmmo.EndsWith("x2") || strThisAmmo.EndsWith("x3") || strThisAmmo.EndsWith("x4"))
						strThisAmmo = strThisAmmo.Substring(0, strThisAmmo.Length - 2);

					if (strThisAmmo.Contains("("))
						strThisAmmo = strThisAmmo.Substring(0, strThisAmmo.IndexOf("("));

					lstCount.Add(strThisAmmo);
				}
			}
			else
			{
				// Nothing weird in the ammo string, so just use the number given.
				string strAmmo = objWeapon.CalculatedAmmo(true);
				if (strAmmo.Contains("("))
					strAmmo = strAmmo.Substring(0, strAmmo.IndexOf("("));
				lstCount.Add(strAmmo);
			}

			// Find all of the Ammo for the current Weapon that the character is carrying.
			if (objWeapon.AmmoCategory != "Grenade Launchers" && objWeapon.AmmoCategory != "Missile Launchers" && objWeapon.AmmoCategory != "Mortar Launchers")
			{
				// This is a standard Weapon, so consume traditional Ammunition.
				foreach (Gear objAmmo in _objCharacter.Gear)
				{
					if (objAmmo.Quantity > 0)
					{
						if (objAmmo.Category == "Ammunition" && objAmmo.Extra == objWeapon.AmmoCategory)
							lstAmmo.Add(objAmmo);
					}
					foreach (Gear objChild in objAmmo.Children)
					{
						if (objChild.Quantity > 0)
						{
							if (objChild.Category == "Ammunition" && objChild.Extra == objWeapon.AmmoCategory)
								lstAmmo.Add(objChild);
						}
					}
				}
			}
			else
			{
				if (objWeapon.AmmoCategory == "Grenade Launchers")
				{
					// Grenade Launchers can only use Grenades.
					foreach (Gear objAmmo in _objCharacter.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && objAmmo.Name.StartsWith("Minigrenade:"))
								lstAmmo.Add(objAmmo);
						}
						foreach (Gear objChild in objAmmo.Children)
						{
							if (objChild.Quantity > 0)
							{
								if (objChild.Category == "Ammunition" && objChild.Name.StartsWith("Minigrenade:"))
									lstAmmo.Add(objChild);
							}
						}
					}
				}
				if (objWeapon.AmmoCategory == "Missile Launchers")
				{
					// Missile Launchers can only use Missiles and Rockets.
					foreach (Gear objAmmo in _objCharacter.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && (objAmmo.Name.StartsWith("Missile:") || objAmmo.Name.StartsWith("Rocket:")))
								lstAmmo.Add(objAmmo);
						}
						foreach (Gear objChild in objAmmo.Children)
						{
							if (objChild.Quantity > 0)
							{
								if (objChild.Category == "Ammunition" && (objChild.Name.StartsWith("Missile:") || objChild.Name.StartsWith("Rocket:")))
									lstAmmo.Add(objChild);
							}
						}
					}
				}
				if (objWeapon.AmmoCategory == "Mortar Launchers")
				{
					// Mortar Launchers can only use Mortars.
					foreach (Gear objAmmo in _objCharacter.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && objAmmo.Name.StartsWith("Mortar Round:"))
								lstAmmo.Add(objAmmo);
						}
						foreach (Gear objChild in objAmmo.Children)
						{
							if (objChild.Quantity > 0)
							{
								if (objChild.Category == "Ammunition" && objChild.Name.StartsWith("Mortar Round:"))
									lstAmmo.Add(objChild);
							}
						}
					}
				}
			}

			// If the Weapon is allowed to use an External Source, put in an External Source item.
			if (blnExternalSource)
				lstAmmo.Add(objExternalSource);

			// Make sure the character has some form of Ammunition for this Weapon.
			if (lstAmmo.Count == 0 && objWeapon.RequireAmmo)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmoType").Replace("{0}", objWeapon.DisplayAmmoCategory), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}

			if (!objWeapon.RequireAmmo)
			{
				// If the Weapon does not require Ammo, clear the Ammo list and just use External Source.
				lstAmmo.Clear();
				lstAmmo.Add(objExternalSource);
			}

			// Show the Ammunition Selection window.
			frmReload frmReloadWeapon = new frmReload();
			frmReloadWeapon.Ammo = lstAmmo;
			frmReloadWeapon.Count = lstCount;
			frmReloadWeapon.ShowDialog(this);

			if (frmReloadWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Return any unspent rounds to the Ammo.
			if (objWeapon.AmmoRemaining > 0)
			{
				bool blnBreak = false;
				foreach (Gear objAmmo in _objCharacter.Gear)
				{
					if (objAmmo.InternalId == objWeapon.AmmoLoaded)
					{
						objAmmo.Quantity += objWeapon.AmmoRemaining;

						// Refresh the Gear tree.
						foreach (TreeNode objNode in treGear.Nodes[0].Nodes)
						{
							if (objAmmo.InternalId == objNode.Tag.ToString())
							{
								objNode.Text = objAmmo.DisplayName;
								break;
							}
						}

						break;
					}
					foreach (Gear objChild in objAmmo.Children)
					{
						if (objChild.InternalId == objWeapon.AmmoLoaded)
						{
							// If this is a plugin for a Spare Clip, move any extra rounds to the character instead of messing with the Clip amount.
							if (objChild.Parent.Name.StartsWith("Spare Clip"))
							{
								TreeNode objNewNode = new TreeNode();
								List<Weapon> lstWeapons = new List<Weapon>();
								List<TreeNode> lstWeaponNodes = new List<TreeNode>();
								Gear objNewGear = new Gear(_objCharacter);
								objNewGear.Copy(objChild, objNewNode, lstWeapons, lstWeaponNodes);
								objNewGear.Quantity = objWeapon.AmmoRemaining;
								objNewNode.Text = objNewGear.DisplayName;
								_objCharacter.Gear.Add(objNewGear);
								treGear.Nodes[0].Nodes.Add(objNewNode);
								blnBreak = true;
								break;
							}
							else
								objChild.Quantity += objWeapon.AmmoRemaining;

							// Refresh the Gear tree.
							foreach (TreeNode objNode in treGear.Nodes[0].Nodes)
							{
								foreach (TreeNode objChildNode in objNode.Nodes)
								{
									if (objChild.InternalId == objChildNode.Tag.ToString())
									{
										objChildNode.Text = objChild.DisplayName;
										break;
									}
								}
							}
							break;
						}
					}
					if (blnBreak)
						break;
				}
			}

			Gear objSelectedAmmo = new Gear(_objCharacter);
			int intQty = frmReloadWeapon.SelectedCount;
			// If an External Source is not being used, consume ammo.
			if (frmReloadWeapon.SelectedAmmo != objExternalSource.InternalId)
			{
				foreach (Gear objGear in _objCharacter.Gear)
				{
					if (objGear.InternalId == frmReloadWeapon.SelectedAmmo)
					{
						objSelectedAmmo = objGear;
						break;
					}
					foreach (Gear objChild in objGear.Children)
					{
						if (objChild.InternalId == frmReloadWeapon.SelectedAmmo)
						{
							objSelectedAmmo = objChild;
							break;
						}
					}
				}

				if (objSelectedAmmo.Quantity == intQty && objSelectedAmmo.Parent != null)
				{
					// If the Ammo is coming from a Spare Clip, reduce the container quantity instead of the plugin quantity.
					if (objSelectedAmmo.Parent.Name.StartsWith("Spare Clip"))
					{
						if (objSelectedAmmo.Parent.Quantity > 0)
							objSelectedAmmo.Parent.Quantity--;
						TreeNode objNode = _objFunctions.FindNode(objSelectedAmmo.Parent.InternalId, treGear);
						objNode.Text = objSelectedAmmo.Parent.DisplayName;
					}
				}
				else
				{
					// Deduct the ammo qty from the ammo. If there isn't enough remaining, use whatever is left.
					if (objSelectedAmmo.Quantity > intQty)
						objSelectedAmmo.Quantity -= intQty;
					else
					{
						intQty = objSelectedAmmo.Quantity;
						objSelectedAmmo.Quantity = 0;
					}
				}
				
				// Refresh the Gear tree.
				foreach (TreeNode objNode in treGear.Nodes[0].Nodes)
				{
					if (objSelectedAmmo.InternalId == objNode.Tag.ToString())
					{
						objNode.Text = objSelectedAmmo.DisplayName;
						break;
					}
					foreach (TreeNode objChildNode in objNode.Nodes)
					{
						if (objSelectedAmmo.InternalId == objChildNode.Tag.ToString())
						{
							objChildNode.Text = objSelectedAmmo.DisplayName;
							break;
						}
					}
				}
			}
			else
			{
				objSelectedAmmo = objExternalSource;
			}

			objWeapon.AmmoRemaining = intQty;
			objWeapon.AmmoLoaded = objSelectedAmmo.InternalId;
			lblWeaponAmmoRemaining.Text = intQty.ToString();

			RefreshSelectedWeapon();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkWeaponAccessoryInstalled_CheckedChanged(object sender, EventArgs e)
		{
			bool blnAccessory = false;

			// Locate the selected Weapon Accessory or Modification.
			WeaponAccessory objAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
			if (objAccessory != null)
				blnAccessory = true;

			if (blnAccessory)
			{
				objAccessory.Installed = chkWeaponAccessoryInstalled.Checked;
			}
			else
			{
				// Locate the selected Weapon Modification.
				bool blnMod = false;
				WeaponMod objMod = _objFunctions.FindWeaponMod(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
				if (objMod != null)
					blnMod = true;

				if (blnMod)
					objMod.Installed = chkWeaponAccessoryInstalled.Checked;
				else
				{
					// Determine if this is an Underbarrel Weapon.
					bool blnUnderbarrel = false;
					Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
					if (objWeapon != null)
					{
						objWeapon.Installed = chkWeaponAccessoryInstalled.Checked;
						blnUnderbarrel = true;
					}

					if (!blnUnderbarrel)
					{
						// Find the selected Gear.
						Gear objSelectedGear = new Gear(_objCharacter);

						try
						{
							objSelectedGear = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objAccessory);
							objSelectedGear.Equipped = chkWeaponAccessoryInstalled.Checked;

							_objController.ChangeGearEquippedStatus(objSelectedGear, chkWeaponAccessoryInstalled.Checked);

							UpdateCharacterInfo();
						}
						catch
						{
						}
					}
				}

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
		}

		private void chkIncludedInWeapon_CheckedChanged(object sender, EventArgs e)
		{
			bool blnAccessory = false;

			// Locate the selected Weapon Accessory or Modification.
			WeaponAccessory objAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
			if (objAccessory != null)
				blnAccessory = true;

			if (blnAccessory)
			{
				objAccessory.IncludedInWeapon = chkIncludedInWeapon.Checked;
			}
			else
			{
				// Locate the selected Weapon Modification.
				WeaponMod objMod = _objFunctions.FindWeaponMod(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
				if (objMod != null)
					objMod.IncludedInWeapon = chkIncludedInWeapon.Checked;

				_blnIsDirty = true;
				UpdateWindowTitle();
				UpdateCharacterInfo();
			}
		}

		private void treGear_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (e.Button == MouseButtons.Left)
				{
					if (treGear.SelectedNode.Level != 1 && treGear.SelectedNode.Level != 0)
						return;
					_objDragButton = MouseButtons.Left;
				}
				else
				{
					if (treGear.SelectedNode.Level == 0)
						return;
					_objDragButton = MouseButtons.Right;
				}

				// Do not allow the root element to be moved.
				if (treGear.SelectedNode.Tag.ToString() == "Node_SelectedGear")
					return;
			}
			catch
			{
				return;
			}
			_intDragLevel = treGear.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treGear_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treGear_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treGear.Nodes[treGear.Nodes.Count - 1].Nodes.Count;
				nodDestination = treGear.Nodes[treGear.Nodes.Count - 1];
			}

			// If the item was moved using the left mouse button, change the order of things.
			if (_objDragButton == MouseButtons.Left)
			{
				if (treGear.SelectedNode.Level == 1)
					_objController.MoveGearNode(intNewIndex, nodDestination, treGear);
				else
					_objController.MoveGearRoot(intNewIndex, nodDestination, treGear);
			}
			if (_objDragButton == MouseButtons.Right)
				_objController.MoveGearParent(intNewIndex, nodDestination, treGear, cmsGear);

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treGear, null);

			_objDragButton = MouseButtons.None;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treGear_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (_objDragButton == MouseButtons.Left)
			{
				if (objNode.Level <= _intDragLevel)
					objNode.BackColor = SystemColors.ControlDark;
			}
			else
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treGear, objNode);
		}

		private void chkGearEquipped_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			Gear objSelectedGear = new Gear(_objCharacter);

			// Attempt to locate the selected piece of Gear.
			try
			{
				objSelectedGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
				objSelectedGear.Equipped = chkGearEquipped.Checked;

				_objController.ChangeGearEquippedStatus(objSelectedGear, chkGearEquipped.Checked);

				RefreshSelectedGear();
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}

		private void cboWeaponAmmo_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			if (_blnSkipRefresh)
				return;

			Weapon objWeapon = new Weapon(_objCharacter);
			objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);

			objWeapon.ActiveAmmoSlot = Convert.ToInt32(cboWeaponAmmo.SelectedValue.ToString());
			RefreshSelectedWeapon();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkGearHomeNode_CheckedChanged(object sender, EventArgs e)
		{
			Gear objGear = new Gear(_objCharacter);
			objGear = (Gear)_objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			objGear.HomeNode = chkGearHomeNode.Checked;
			RefreshSelectedGear();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdWeaponBuyAmmo_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
				if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
				{
					foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
					{
						if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
						{
							objWeapon = objUnderbarrelWeapon;
							break;
						}
					}
				}
			}

			bool blnAddAgain = PickGear(true, null, objWeapon.AmmoCategory);
			if (blnAddAgain)
				cmdWeaponBuyAmmo_Click(sender, e);
		}

		private void cmdWeaponMoveToVehicle_Click(object sender, EventArgs e)
		{
			// Locate the selected Weapon.
			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
			{
				if (objCharacterWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
				{
					objWeapon = objCharacterWeapon;
					break;
				}
			}

			List<Vehicle> lstVehicles = new List<Vehicle>();
			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objVehicleMod in objCharacterVehicle.Mods)
				{
					// Only add a Vehicle to the list if it has a Weapon Mount or Mechanical Arm.
					if (objVehicleMod.Name.StartsWith("Weapon Mount") || objVehicleMod.Name.StartsWith("Mechanical Arm"))
					{
						lstVehicles.Add(objCharacterVehicle);
						break;
					}
				}
			}

			// Cannot continue if there are no Vehicles with a Weapon Mount or Mechanical Arm.
			if (lstVehicles.Count == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotMoveWeapons"), LanguageManager.Instance.GetString("MessageTitle_CannotMoveWeapons"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			frmSelectItem frmPickItem = new frmSelectItem();
			frmPickItem.Vehicles = lstVehicles;
			frmPickItem.ShowDialog(this);

			if (frmPickItem.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected Vehicle.
			Vehicle objVehicle = new Vehicle(_objCharacter);
			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				if (objCharacterVehicle.InternalId == frmPickItem.SelectedItem)
				{
					objVehicle = objCharacterVehicle;
					break;
				}
			}

			// Now display a list of the acceptable mounting points for the Weapon.
			List<VehicleMod> lstMods = new List<VehicleMod>();
			foreach (VehicleMod objVehicleMod in objVehicle.Mods)
			{
				if (objVehicleMod.Name.StartsWith("Weapon Mount") || objVehicleMod.Name.StartsWith("Mechanical Arm"))
					lstMods.Add(objVehicleMod);
			}

			frmPickItem.VehicleMods = lstMods;
			frmPickItem.ShowDialog(this);

			if (frmPickItem.DialogResult == DialogResult.Cancel)
				return;

			// Locate the selected Vehicle Mod.
			VehicleMod objMod = new VehicleMod(_objCharacter);
			foreach (VehicleMod objCharacterMod in objVehicle.Mods)
			{
				if (objCharacterMod.InternalId == frmPickItem.SelectedItem)
				{
					objMod = objCharacterMod;
					break;
				}
			}

			// Remove the Weapon from the character and add it to the Vehicle Mod.
			_objCharacter.Weapons.Remove(objWeapon);
			objMod.Weapons.Add(objWeapon);
			objWeapon.VehicleMounted = true;
			objWeapon.Location = string.Empty;

			// Move the TreeNode to the Vehicle Mod.
			TreeNode objNode = new TreeNode();
			objNode = treWeapons.SelectedNode;
			treWeapons.SelectedNode.Remove();

			foreach (TreeNode objVehicleNode in treVehicles.Nodes[0].Nodes)
			{
				foreach (TreeNode objModNode in objVehicleNode.Nodes)
				{
					if (objModNode.Tag.ToString() == objMod.InternalId)
					{
						objModNode.Nodes.Add(objNode);
						objModNode.Expand();
						objNode.Expand();
						break;
					}
				}
			}

			// Remove any Improvements from the Character.
			foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
			{
				foreach (Gear objGear in objAccessory.Gear)
					_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
			}
			if (objWeapon.UnderbarrelWeapons.Count > 0)
			{
				foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
				{
					foreach (WeaponAccessory objAccessory in objUnderbarrelWeapon.WeaponAccessories)
					{
						foreach (Gear objGear in objAccessory.Gear)
							_objFunctions.DeleteGear(objGear, treWeapons, _objImprovementManager);
					}
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdArmorIncrease_Click(object sender, EventArgs e)
		{
			Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
			if (objArmor == null)
				return;

			objArmor.ArmorDamage--;
			RefreshSelectedArmor();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdArmorDecrease_Click(object sender, EventArgs e)
		{
			Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
			if (objArmor == null)
				return;

			objArmor.ArmorDamage++;
			RefreshSelectedArmor();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkIncludedInArmor_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			// Locate the selected Armor Modification.
			ArmorMod objMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
			if (objMod != null)
				objMod.IncludedInArmor = chkIncludedInArmor.Checked;

			_blnIsDirty = true;
			UpdateWindowTitle();
			UpdateCharacterInfo();
		}

		private void chkCommlinks_CheckedChanged(object sender, EventArgs e)
		{
			PopulateGearList();
		}

		private void chkActiveCommlink_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			Gear objSelectedGear = new Gear(_objCharacter);

			// Attempt to locate the selected piece of Gear.
			try
			{
				objSelectedGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);

				if (objSelectedGear.GetType() != typeof(Commlink))
					return;

				Commlink objCommlink = (Commlink)objSelectedGear;
				objCommlink.IsActive = chkActiveCommlink.Checked;

				ChangeActiveCommlink(objCommlink);

				RefreshSelectedGear();
				UpdateCharacterInfo();

				_blnIsDirty = true;
				UpdateWindowTitle();
			}
			catch
			{
			}
		}
		#endregion

		#region Additional Vehicle Tab Control Events
		private void treVehicles_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedVehicle();
			RefreshPasteStatus();
		}

		private void treVehicles_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				if (treVehicles.SelectedNode.Level != 1)
				{
					// Determine if this is a piece of Gear. If not, don't let the user drag the Node.
					Vehicle objVehicle = new Vehicle(_objCharacter);
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);
					if (objGear != null)
					{
						_objDragButton = e.Button;
						_blnDraggingGear = true;
					}
					else
					{
						return;
					}
				}
			}
			catch
			{
				return;
			}
			_intDragLevel = treVehicles.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treVehicles_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treVehicles_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treVehicles.Nodes[treVehicles.Nodes.Count - 1].Nodes.Count;
				nodDestination = treVehicles.Nodes[treVehicles.Nodes.Count - 1];
			}

			if (!_blnDraggingGear)
				_objController.MoveVehicleNode(intNewIndex, nodDestination, treVehicles);
			else
			{
				if (_objDragButton == MouseButtons.Left)
					return;
				else
					_objController.MoveVehicleGearParent(intNewIndex, nodDestination, treVehicles, cmsVehicleGear);
			}

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treVehicles, null);

			_blnDraggingGear = false;

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treVehicles_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (_objDragButton == MouseButtons.Left)
			{
				if (objNode.Level <= _intDragLevel)
					objNode.BackColor = SystemColors.ControlDark;
			}
			else
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treVehicles, objNode);
		}

		private void cmdFireVehicleWeapon_Click(object sender, EventArgs e)
		{
			// "Click" the first menu item available.
			if (cmsVehicleAmmoSingleShot.Enabled)
				cmsVehicleAmmoSingleShot_Click(sender, e);
			else
			{
				if (cmsVehicleAmmoShortBurst.Enabled)
					cmsVehicleAmmoShortBurst_Click(sender, e);
				else
					cmsVehicleAmmoLongBurst_Click(sender, e);
			}
		}

		private void cmdReloadVehicleWeapon_Click(object sender, EventArgs e)
		{
			Vehicle objVehicle = new Vehicle(_objCharacter);

			List<Gear> lstAmmo = new List<Gear>();
			List<string> lstCount = new List<string>();
			bool blnExternalSource = false;

			Gear objExternalSource = new Gear(_objCharacter);
			objExternalSource.Name = "External Source";

			// Locate the selected Vehicle Weapon.
			Weapon objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objVehicle);

			// Determine which loading methods are available to the Weapon.
			if (objWeapon.CalculatedAmmo().Contains(" or ") || objWeapon.CalculatedAmmo().Contains("x") || objWeapon.CalculatedAmmo().Contains("Special") || objWeapon.CalculatedAmmo().Contains("+"))
			{
				string strWeaponAmmo = objWeapon.CalculatedAmmo().ToLower();
				if (strWeaponAmmo.Contains("external source"))
					blnExternalSource = true;
				// Get rid of external source, special, or belt, and + energy.
				strWeaponAmmo = strWeaponAmmo.Replace("external source", "100");
				strWeaponAmmo = strWeaponAmmo.Replace("special", "100");
				strWeaponAmmo = strWeaponAmmo.Replace(" + energy", "");
				strWeaponAmmo = strWeaponAmmo.Replace(" or belt", "");

				string[] strSplit = new string[] { " or " };
				string[] strAmmos = strWeaponAmmo.Split(strSplit, StringSplitOptions.RemoveEmptyEntries);

				foreach (string strAmmo in strAmmos)
				{
					string strThisAmmo = strAmmo;
					if (strThisAmmo.StartsWith("2x") || strThisAmmo.StartsWith("3x") || strThisAmmo.StartsWith("4x"))
						strThisAmmo = strThisAmmo.Substring(2, strThisAmmo.Length - 2);
					if (strThisAmmo.EndsWith("x2") || strThisAmmo.EndsWith("x3") || strThisAmmo.EndsWith("x4"))
						strThisAmmo = strThisAmmo.Substring(0, strThisAmmo.Length - 2);

					if (strThisAmmo.Contains("("))
						strThisAmmo = strThisAmmo.Substring(0, strThisAmmo.IndexOf("("));

					lstCount.Add(strThisAmmo);
				}
			}
			else
			{
				// Nothing weird in the ammo string, so just use the number given.
				string strAmmo = objWeapon.CalculatedAmmo();
				if (strAmmo.Contains("("))
					strAmmo = strAmmo.Substring(0, strAmmo.IndexOf("("));
				lstCount.Add(strAmmo);
			}

			// Find all of the Ammo for the current Weapon that the character is carrying.
			if (objWeapon.AmmoCategory != "Grenade Launchers" && objWeapon.AmmoCategory != "Missile Launchers" && objWeapon.AmmoCategory != "Mortar Launchers")
			{
				// This is a standard Weapon, so consume traditional Ammunition.
				foreach (Gear objAmmo in objVehicle.Gear)
				{
					if (objAmmo.Quantity > 0)
					{
						if (objAmmo.Category == "Ammunition" && objAmmo.Extra == objWeapon.AmmoCategory)
							lstAmmo.Add(objAmmo);
					}
				}
			}
			else
			{
				if (objWeapon.AmmoCategory == "Grenade Launchers")
				{
					// Grenade Launchers can only use Grenades.
					foreach (Gear objAmmo in objVehicle.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && objAmmo.Name.StartsWith("Minigrenade:"))
								lstAmmo.Add(objAmmo);
						}
					}
				}
				if (objWeapon.AmmoCategory == "Missile Launchers")
				{
					// Missile Launchers can only use Missiles and Rockets.
					foreach (Gear objAmmo in objVehicle.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && (objAmmo.Name.StartsWith("Missile:") || objAmmo.Name.StartsWith("Rocket:")))
								lstAmmo.Add(objAmmo);
						}
					}
				}
				if (objWeapon.AmmoCategory == "Mortar Launchers")
				{
					// Mortar Launchers can only use Mortars.
					foreach (Gear objAmmo in objVehicle.Gear)
					{
						if (objAmmo.Quantity > 0)
						{
							if (objAmmo.Category == "Ammunition" && objAmmo.Name.StartsWith("Mortar Round:"))
								lstAmmo.Add(objAmmo);
						}
					}
				}
			}

			// If the Weapon is allowed to use an External Source, put in an External Source item.
			if (blnExternalSource)
				lstAmmo.Add(objExternalSource);

			// Make sure the character has some form of Ammunition for this Weapon.
			if (lstAmmo.Count == 0 && objWeapon.RequireAmmo)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OutOfAmmoType").Replace("{0}", objWeapon.DisplayAmmoCategory), LanguageManager.Instance.GetString("MessageTitle_OutOfAmmo"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}

			if (!objWeapon.RequireAmmo)
			{
				// If the Weapon does not require Ammo, clear the Ammo list and just use External Source.
				lstAmmo.Clear();
				lstAmmo.Add(objExternalSource);
			}

			// Show the Ammunition Selection window.
			frmReload frmReloadWeapon = new frmReload();
			frmReloadWeapon.Ammo = lstAmmo;
			frmReloadWeapon.Count = lstCount;
			frmReloadWeapon.ShowDialog(this);

			if (frmReloadWeapon.DialogResult == DialogResult.Cancel)
				return;

			// Return any unspent rounds to the Ammo.
			if (objWeapon.AmmoRemaining > 0)
			{
				foreach (Gear objAmmo in objVehicle.Gear)
				{
					if (objAmmo.InternalId == objWeapon.AmmoLoaded)
					{
						objAmmo.Quantity += objWeapon.AmmoRemaining;

						// Refresh the Vehicle tree.
						foreach (TreeNode objVehicleNode in treVehicles.Nodes[0].Nodes)
						{
							if (objVehicle.InternalId == objVehicleNode.Tag.ToString())
							{
								foreach (TreeNode objNode in objVehicleNode.Nodes)
								{
									if (objAmmo.InternalId == objNode.Tag.ToString())
									{
										objNode.Text = objAmmo.DisplayName;
										break;
									}
								}
							}
						}

						break;
					}
				}
			}

			Gear objSelectedAmmo = new Gear(_objCharacter);
			int intQty = frmReloadWeapon.SelectedCount;
			// If an External Source is not being used, consume ammo.
			if (frmReloadWeapon.SelectedAmmo != objExternalSource.InternalId)
			{
				foreach (Gear objGear in objVehicle.Gear)
				{
					if (objGear.InternalId == frmReloadWeapon.SelectedAmmo)
					{
						objSelectedAmmo = objGear;
						break;
					}
				}

				// Deduct the ammo qty from the ammo. If there isn't enough remaining, use whatever is left.
				if (objSelectedAmmo.Quantity > intQty)
					objSelectedAmmo.Quantity -= intQty;
				else
				{
					intQty = objSelectedAmmo.Quantity;
					objSelectedAmmo.Quantity = 0;
				}

				// Refresh the Vehicle tree.
				foreach (TreeNode objVehicleNode in treVehicles.Nodes[0].Nodes)
				{
					if (objVehicle.InternalId == objVehicleNode.Tag.ToString())
					{
						foreach (TreeNode objNode in objVehicleNode.Nodes)
						{
							if (objSelectedAmmo.InternalId == objNode.Tag.ToString())
							{
								objNode.Text = objSelectedAmmo.DisplayName;
								break;
							}
						}
					}
				}
			}
			else
			{
				objSelectedAmmo = objExternalSource;
			}

			objWeapon.AmmoRemaining = intQty;
			objWeapon.AmmoLoaded = objSelectedAmmo.InternalId;
			lblVehicleWeaponAmmoRemaining.Text = intQty.ToString();
			
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkVehicleWeaponAccessoryInstalled_CheckedChanged(object sender, EventArgs e)
		{
			bool blnAccessory = false;

			// Locate the the Selected Vehicle Weapon Accessory of Modification.
			WeaponAccessory objAccessory = _objFunctions.FindVehicleWeaponAccessory(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
			if (objAccessory != null)
				blnAccessory = true;

			if (blnAccessory)
				objAccessory.Installed = chkVehicleWeaponAccessoryInstalled.Checked;
			else
			{
				bool blnWeaponMod = false;
				// Locate the selected Vehicle Weapon Modification.
				WeaponMod objMod = _objFunctions.FindVehicleWeaponMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
				if (objMod != null)
					blnWeaponMod = true;

				if (blnWeaponMod)
					objMod.Installed = chkVehicleWeaponAccessoryInstalled.Checked;
				else
				{
					// If this isn't a Weapon Mod, then it must be a Vehicle Mod.
					Vehicle objFoundVehicle = new Vehicle(_objCharacter);
					VehicleMod objVehicleMod = _objFunctions.FindVehicleMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
					if (objVehicleMod != null)
						objVehicleMod.Installed = chkVehicleWeaponAccessoryInstalled.Checked;
					else
					{
						// If everything else has failed, we're left with a Vehicle Weapon.
						Weapon objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objFoundVehicle);
						objWeapon.Installed = chkVehicleWeaponAccessoryInstalled.Checked;
					}
				}
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cboVehicleWeaponAmmo_SelectedIndexChanged(object sender, EventArgs e)
		{

			try
			{
				if (treVehicles.SelectedNode.Level < 2)
					return;
			}
			catch
			{
				return;
			}

			if (_blnSkipRefresh)
				return;

			Weapon objWeapon = new Weapon(_objCharacter);
			foreach (Vehicle objVehicle in _objCharacter.Vehicles)
			{
				foreach (VehicleMod objVehicleMod in objVehicle.Mods)
				{
					foreach (Weapon objVehicleWeapon in objVehicleMod.Weapons)
					{
						if (objVehicleWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							objWeapon = objVehicleWeapon;
							break;
						}

						if (objVehicleWeapon.UnderbarrelWeapons.Count > 0)
						{
							foreach (Weapon objUnderbarrelWeapon in objVehicleWeapon.UnderbarrelWeapons)
							{
								if (objUnderbarrelWeapon.InternalId == treVehicles.SelectedNode.Tag.ToString())
								{
									objWeapon = objUnderbarrelWeapon;
									break;
								}
							}
						}
					}
				}
			}

			objWeapon.ActiveAmmoSlot = Convert.ToInt32(cboVehicleWeaponAmmo.SelectedValue.ToString());
			RefreshSelectedVehicle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkVehicleHomeNode_CheckedChanged(object sender, EventArgs e)
		{
			if (treVehicles.SelectedNode.Level == 1)
			{
				Vehicle objVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
				if (objVehicle == null)
					return;

				objVehicle.HomeNode = chkVehicleHomeNode.Checked;
			}
			else
			{
				Commlink objGear = new Commlink(_objCharacter);
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				objGear = (Commlink)_objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);
				objGear.HomeNode = chkVehicleHomeNode.Checked;
			}

			RefreshSelectedVehicle();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Additional Spells and Spirits Tab Control Events
		private void treSpells_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (treSpells.SelectedNode.Level > 0)
			{
				_blnSkipRefresh = true;

				// Locate the selected Spell.
				Spell objSpell = _objFunctions.FindSpell(e.Node.Tag.ToString(), _objCharacter.Spells);

				lblSpellDescriptors.Text = objSpell.DisplayDescriptors;
				lblSpellCategory.Text = objSpell.DisplayCategory;
				lblSpellType.Text = objSpell.DisplayType;
				lblSpellRange.Text = objSpell.DisplayRange;
				lblSpellDamage.Text = objSpell.DisplayDamage;
				lblSpellDuration.Text = objSpell.DisplayDuration;
				lblSpellDV.Text = objSpell.DisplayDV;
				string strBook = _objOptions.LanguageBookShort(objSpell.Source);
				string strPage = objSpell.Page;
				lblSpellSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblSpellSource, _objOptions.LanguageBookLong(objSpell.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSpell.Page);

				// Determine the size of the Spellcasting Dice Pool.
				lblSpellDicePool.Text = objSpell.DicePool.ToString();
				tipTooltip.SetToolTip(lblSpellDicePool, objSpell.DicePoolTooltip);

				// Build the DV tooltip.
				tipTooltip.SetToolTip(lblSpellDV, objSpell.DVTooltip);

				// Update the Drain Attribute Value.
				if (_objCharacter.MAGEnabled && lblDrainAttributes.Text != "")
				{
					try
					{
						string strTip = "";
						XmlDocument objXmlDocument = new XmlDocument();
						XPathNavigator nav = objXmlDocument.CreateNavigator();

						objXmlDocument = new XmlDocument();
						nav = objXmlDocument.CreateNavigator();
						string strDrain = lblDrainAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), _objCharacter.BOD.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), _objCharacter.AGI.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), _objCharacter.REA.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), _objCharacter.STR.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), _objCharacter.CHA.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), _objCharacter.INT.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), _objCharacter.LOG.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), _objCharacter.WIL.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeMAGShort"), _objCharacter.MAG.TotalValue.ToString());
						XPathExpression xprDrain = nav.Compile(strDrain);
						int intDrain = Convert.ToInt32(nav.Evaluate(xprDrain).ToString());
						intDrain += _objImprovementManager.ValueOf(Improvement.ImprovementType.DrainResistance);

						strTip = lblDrainAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), LanguageManager.Instance.GetString("String_AttributeBODShort") + " (" + _objCharacter.BOD.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), LanguageManager.Instance.GetString("String_AttributeAGIShort") + " (" + _objCharacter.AGI.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), LanguageManager.Instance.GetString("String_AttributeREAShort") + " (" + _objCharacter.REA.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), LanguageManager.Instance.GetString("String_AttributeSTRShort") + " (" + _objCharacter.STR.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), LanguageManager.Instance.GetString("String_AttributeCHAShort") + " (" + _objCharacter.CHA.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), LanguageManager.Instance.GetString("String_AttributeINTShort") + " (" + _objCharacter.INT.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), LanguageManager.Instance.GetString("String_AttributeLOGShort") + " (" + _objCharacter.LOG.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), LanguageManager.Instance.GetString("String_AttributeWILShort") + " (" + _objCharacter.WIL.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeMAGShort"), LanguageManager.Instance.GetString("String_AttributeMAGShort") + " (" + _objCharacter.CHA.TotalValue.ToString() + ")");

						if (_objImprovementManager.ValueOf(Improvement.ImprovementType.DrainResistance) != 0)
							strTip += " + " + LanguageManager.Instance.GetString("Tip_Skill_DicePoolModifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.DrainResistance).ToString() + ")";
						if (objSpell.Limited)
						{
							intDrain += 2;
							strTip += " + " + LanguageManager.Instance.GetString("String_SpellLimited") + " (2)";
						}
						lblDrainAttributesValue.Text = intDrain.ToString();
						tipTooltip.SetToolTip(lblDrainAttributesValue, strTip);
					}
					catch
					{
					}
				}

				_blnSkipRefresh = false;
			}
			else
			{
				lblSpellDescriptors.Text = "";
				lblSpellCategory.Text = "";
				lblSpellType.Text = "";
				lblSpellRange.Text = "";
				lblSpellDamage.Text = "";
				lblSpellDuration.Text = "";
				lblSpellDV.Text = "";
				lblSpellSource.Text = "";
				lblSpellDicePool.Text = "";
				tipTooltip.SetToolTip(lblSpellSource, null);
				tipTooltip.SetToolTip(lblSpellDV, null);
			}
		}

		private void treFoci_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Checked)
			{
				// Locate the Focus that is being touched.
				Gear objSelectedFocus = new Gear(_objCharacter);
				objSelectedFocus = _objFunctions.FindGear(e.Node.Tag.ToString(), _objCharacter.Gear);

				if (objSelectedFocus != null)
				{

					Focus objFocus = new Focus();
					objFocus.Name = e.Node.Text;
					objFocus.Rating = objSelectedFocus.Rating;
					objFocus.GearId = e.Node.Tag.ToString();
					_objCharacter.Foci.Add(objFocus);

					// Mark the Gear and Bonded and create an Improvements.
					objSelectedFocus.Bonded = true;
					if (objSelectedFocus.Equipped)
					{
						if (objSelectedFocus.Bonus != null)
						{
							if (objSelectedFocus.Extra != "")
								_objImprovementManager.ForcedValue = objSelectedFocus.Extra;
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objSelectedFocus.InternalId, objSelectedFocus.Bonus, false, objSelectedFocus.Rating, objSelectedFocus.DisplayNameShort);
						}
					}
				}
				else
				{
					// This is a Stacked Focus.
					StackedFocus objStack = new StackedFocus(_objCharacter);
					foreach (StackedFocus objCharacterFocus in _objCharacter.StackedFoci)
					{
						if (e.Node.Tag.ToString() == objCharacterFocus.InternalId)
						{
							objStack = objCharacterFocus;
							break;
						}
					}

					objStack.Bonded = true;
					Gear objStackGear = _objFunctions.FindGear(objStack.GearId, _objCharacter.Gear);
					if (objStackGear.Equipped)
					{
						foreach (Gear objGear in objStack.Gear)
						{
							if (objGear.Bonus != null)
							{
								if (objGear.Extra != "")
									_objImprovementManager.ForcedValue = objGear.Extra;
								_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.StackedFocus, objStack.InternalId, objGear.Bonus, false, objGear.Rating, objGear.DisplayNameShort);
							}
						}
					}
				}
			}
			else
			{
				Focus objFocus = new Focus();
				foreach (Focus objCharacterFocus in _objCharacter.Foci)
				{
					if (objCharacterFocus.GearId == e.Node.Tag.ToString())
					{
						objFocus = objCharacterFocus;
						break;
					}
				}

				// Mark the Gear as not Bonded and remove any Improvements.
				Gear objGear = new Gear(_objCharacter);
				objGear = _objFunctions.FindGear(objFocus.GearId, _objCharacter.Gear);

				if (objGear != null)
				{
					objGear.Bonded = false;
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId);
					_objCharacter.Foci.Remove(objFocus);
				}
				else
				{
					// This is a Stacked Focus.
					StackedFocus objStack = new StackedFocus(_objCharacter);
					foreach (StackedFocus objCharacterFocus in _objCharacter.StackedFoci)
					{
						if (e.Node.Tag.ToString() == objCharacterFocus.InternalId)
						{
							objStack = objCharacterFocus;
							break;
						}
					}

					objStack.Bonded = false;
					foreach (Gear objFocusGear in objStack.Gear)
					{
						_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.StackedFocus, objStack.InternalId);
					}
				}
			}

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treFoci_BeforeCheck(object sender, TreeViewCancelEventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			// If the item is being unchecked, confirm that the user wants to un-bind the Focus.
			if (e.Node.Checked)
			{
				if (MessageBox.Show(LanguageManager.Instance.GetString("Message_UnbindFocus"), LanguageManager.Instance.GetString("MessageTitle_UnbindFocus"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					e.Cancel = true;
				return;
			}

			// Locate the Focus that is being touched.
			Gear objSelectedFocus = new Gear(_objCharacter);
			objSelectedFocus = _objFunctions.FindGear(e.Node.Tag.ToString(), _objCharacter.Gear);

			// Set the Focus count to 1 and get its current Rating (Force). This number isn't used in the following loops because it isn't yet checked or unchecked.
			int intFociCount = 1;
			int intFociTotal = 0;

			if (objSelectedFocus != null)
				intFociTotal = objSelectedFocus.Rating;
			else
			{
				// This is a Stacked Focus.
				StackedFocus objStack = new StackedFocus(_objCharacter);
				foreach (StackedFocus objCharacterFocus in _objCharacter.StackedFoci)
				{
					if (e.Node.Tag.ToString() == objCharacterFocus.InternalId)
					{
						objStack = objCharacterFocus;
						break;
					}
				}
				intFociTotal = objStack.TotalForce;
			}

			// Run through the list of items. Count the number of Foci the character would have bonded including this one, plus the total Force of all checked Foci.
			foreach (TreeNode objNode in treFoci.Nodes)
			{
				if (objNode.Checked)
				{
					intFociCount++;
					foreach (Gear objCharacterFocus in _objCharacter.Gear)
					{
						if (objNode.Tag.ToString() == objCharacterFocus.InternalId)
						{
							intFociTotal += objCharacterFocus.Rating;
							break;
						}
					}

					foreach (StackedFocus objStack in _objCharacter.StackedFoci)
					{
						if (objNode.Tag.ToString() == objStack.InternalId)
						{
							intFociTotal += objStack.TotalForce;
							break;
						}
					}
				}
			}

			if (intFociTotal > _objCharacter.MAG.TotalValue * 5)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_FocusMaximumForce"), LanguageManager.Instance.GetString("MessageTitle_FocusMaximum"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				e.Cancel = true;
				return;
			}

			if (intFociCount > _objCharacter.MAG.TotalValue)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_FocusMaximumNumber"), LanguageManager.Instance.GetString("MessageTitle_FocusMaximum"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				e.Cancel = true;
				return;
			}

			// If we've made it this far, everything is okay, so create a Karma Expense for the newly-bound Focus.
			bool blnFound = false;
			// Locate the Gear for the Focus.
			Gear objFocus = new Gear(_objCharacter);
			foreach (Gear objGear in _objCharacter.Gear)
			{
				if (objGear.InternalId == e.Node.Tag.ToString())
				{
					objFocus = objGear;
					blnFound = true;
					break;
				}
			}

			if (blnFound)
			{
				// Determine how much Karma the Focus will cost to bind.
				string strFocusName = objFocus.Name;
				if (objFocus.Extra != "")
					strFocusName += " (" + objFocus.Extra + ")";
				int intPosition = strFocusName.IndexOf("(");
				if (intPosition > -1)
					strFocusName = strFocusName.Substring(0, intPosition - 1);
				int intKarmaMultiplier = 0;
				switch (strFocusName)
				{
					case "Alchemical Focus":
                        intKarmaMultiplier = _objOptions.KarmaAlchemicalFocus;
						break;
					case "Sustaining Focus":
						intKarmaMultiplier = _objOptions.KarmaSustainingFocus;
						break;
					case "Counterspelling Focus":
						intKarmaMultiplier = _objOptions.KarmaCounterspellingFocus;
						break;
					case "Banishing Focus":
						intKarmaMultiplier = _objOptions.KarmaBanishingFocus;
						break;
					case "Binding Focus":
						intKarmaMultiplier = _objOptions.KarmaBindingFocus;
						break;
					case "Weapon Focus":
						intKarmaMultiplier = _objOptions.KarmaWeaponFocus;
						break;
					case "Ritual Spellcasting Focus":
						intKarmaMultiplier = _objOptions.KarmaRitualSpellcastingFocus;
						break;
                    case "Spellcasting Focus":
                        intKarmaMultiplier = _objOptions.KarmaSpellcastingFocus;
                        break;
                    case "Spell Shaping Focus":
                        intKarmaMultiplier = _objOptions.KarmaSpellShapingFocus;
                        break;
                    case "Summoning Focus":
						intKarmaMultiplier = _objOptions.KarmaSummoningFocus;
						break;
					case "Disenchanting Focus":
                        intKarmaMultiplier = _objOptions.KarmaDisenchantingFocus;
						break;
					case "Centering Focus":
						intKarmaMultiplier = _objOptions.KarmaCenteringFocus;
						break;
					case "Masking Focus":
						intKarmaMultiplier = _objOptions.KarmaMaskingFocus;
						break;
					case "Flexible Signature Focus":
						intKarmaMultiplier = _objOptions.KarmaFlexibleSignatureFocus;
						break;
					case "Power Focus":
						intKarmaMultiplier = _objOptions.KarmaPowerFocus;
						break;
					case "Qi Focus":
                        intKarmaMultiplier = _objOptions.KarmaQiFocus;
						break;
					default:
						intKarmaMultiplier = 1;
						break;
				}

				int intKarmaExpense = objFocus.Rating * intKarmaMultiplier;
				if (intKarmaExpense > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					e.Cancel = true;
					return;
				}

				if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseFocus").Replace("{0}", intKarmaExpense.ToString()).Replace("{1}", objFocus.DisplayNameShort)))
				{
					e.Cancel = true;
					return;
				}

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaExpense * -1, LanguageManager.Instance.GetString("String_ExpenseBound") + " " + objFocus.DisplayNameShort, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaExpense;

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.BindFocus, objFocus.InternalId);
				objExpense.Undo = objUndo;
			}
			else
			{
				// The Focus was not found in Gear, so this is a Stacked Focus.
				StackedFocus objStack = new StackedFocus(_objCharacter);
				foreach (StackedFocus objCharacterStack in _objCharacter.StackedFoci)
				{
					if (objCharacterStack.InternalId == e.Node.Tag.ToString())
					{
						objStack = objCharacterStack;
						break;
					}
				}

				int intKarmaExpense = objStack.BindingCost;
				if (intKarmaExpense > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					e.Cancel = true;
					return;
				}

				if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseFocus").Replace("{0}", intKarmaExpense.ToString()).Replace("{1}", LanguageManager.Instance.GetString("String_StackedFocus") + " " + objStack.Name)))
				{
					e.Cancel = true;
					return;
				}

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaExpense * -1, LanguageManager.Instance.GetString("String_ExpenseBound") + " " + LanguageManager.Instance.GetString("String_StackedFocus") + " " + objStack.Name, ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaExpense;

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.BindFocus, objStack.InternalId);
				objExpense.Undo = objUndo;
			}

			UpdateCharacterInfo();
		}

		private void cmdImproveInitiation_Click(object sender, EventArgs e)
		{
			if (_objCharacter.MAGEnabled)
			{
				// Make sure that the Initiate Grade is not attempting to go above the character's MAG Attribute.
				if (_objCharacter.InitiateGrade + 1 > _objCharacter.MAG.TotalValue)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotIncreaseInitiateGrade"), LanguageManager.Instance.GetString("MessageTitle_CannotIncreaseInitiateGrade"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				// Make sure the character has enough Karma.
				double dblMultiplier = 1.0;
				if (chkInitiationGroup.Checked)
					dblMultiplier -= 0.2;
				if (chkInitiationOrdeal.Checked)
					dblMultiplier -= 0.2;
				dblMultiplier = Math.Round(dblMultiplier, 2);

				int intKarmaExpense = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.InitiateGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

				if (intKarmaExpense > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_InitiateGrade")).Replace("{1}", (_objCharacter.InitiateGrade + 1).ToString()).Replace("{2}", intKarmaExpense.ToString())))
					return;

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaExpense * -1, LanguageManager.Instance.GetString("String_ExpenseInitiateGrade") + " " + _objCharacter.InitiateGrade.ToString() + " -> " + (_objCharacter.InitiateGrade + 1).ToString(), ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaExpense;

				// Create the Initiate Grade object.
				InitiationGrade objGrade = new InitiationGrade(_objCharacter);
				objGrade.Create(_objCharacter.InitiateGrade + 1, _objCharacter.RESEnabled, chkInitiationGroup.Checked, chkInitiationOrdeal.Checked);
				_objCharacter.InitiationGrades.Add(objGrade);

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.ImproveInitiateGrade, objGrade.InternalId);
				objExpense.Undo = objUndo;

				// Set the character's Initiate Grade.
				_objCharacter.InitiateGrade += 1;

				// Remove any existing Initiation Improvements.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Initiation, "Initiation");

				// Create the replacement Improvement.
				_objImprovementManager.CreateImprovement("MAG", Improvement.ImprovementSource.Initiation, "Initiation", Improvement.ImprovementType.Attribute, "", 0, 1, 0, _objCharacter.InitiateGrade);
				_objImprovementManager.Commit();

				// Update any Metamagic Improvements the character might have.
				foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
				{
					if (objMetamagic.Bonus != null)
					{
						// If the Bonus contains "Rating", remove the existing Improvement and create new ones.
						if (objMetamagic.Bonus.InnerXml.Contains("Rating"))
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId);
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Metamagic, objMetamagic.InternalId, objMetamagic.Bonus, false, _objCharacter.InitiateGrade, objMetamagic.DisplayNameShort);
						}
					}
				}

				lblInitiateGrade.Text = _objCharacter.InitiateGrade.ToString();

				int intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.InitiateGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

				string strInitTip = LanguageManager.Instance.GetString("Tip_ImproveInitiateGrade").Replace("{0}", (_objCharacter.InitiateGrade + 1).ToString()).Replace("{1}", intAmount.ToString());
				tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
			}
			else if (_objCharacter.RESEnabled)
			{
				// Make sure that the Initiate Grade is not attempting to go above the character's RES Attribute.
				if (_objCharacter.SubmersionGrade + 1 > _objCharacter.RES.TotalValue)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotIncreaseSubmersionGrade"), LanguageManager.Instance.GetString("MessageTitle_CannotIncreaseSubmersionGrade"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				// Make sure the character has enough Karma.
				double dblMultiplier = 1.0;
				if (chkInitiationGroup.Checked)
					dblMultiplier -= 0.2;
				if (chkInitiationOrdeal.Checked)
					dblMultiplier -= 0.2;
				dblMultiplier = Math.Round(dblMultiplier, 2);

				int intKarmaExpense = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.SubmersionGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

				if (intKarmaExpense > _objCharacter.Karma)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				if (!ConfirmKarmaExpense(LanguageManager.Instance.GetString("Message_ConfirmKarmaExpense").Replace("{0}", LanguageManager.Instance.GetString("String_SubmersionGrade")).Replace("{1}", (_objCharacter.SubmersionGrade + 1).ToString()).Replace("{2}", intKarmaExpense.ToString())))
					return;

				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intKarmaExpense * -1, LanguageManager.Instance.GetString("String_ExpenseSubmersionGrade") + " " + _objCharacter.SubmersionGrade.ToString() + " -> " + (_objCharacter.SubmersionGrade + 1).ToString(), ExpenseType.Karma, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Karma -= intKarmaExpense;

				// Create the Initiate Grade object.
				InitiationGrade objGrade = new InitiationGrade(_objCharacter);
				objGrade.Create(_objCharacter.SubmersionGrade + 1, _objCharacter.RESEnabled, chkInitiationGroup.Checked, chkInitiationOrdeal.Checked);
				_objCharacter.InitiationGrades.Add(objGrade);

				ExpenseUndo objUndo = new ExpenseUndo();
				objUndo.CreateKarma(KarmaExpenseType.ImproveInitiateGrade, objGrade.InternalId);
				objExpense.Undo = objUndo;

				// Set the character's Submersion Grade.
				_objCharacter.SubmersionGrade += 1;

				// Remove any existing Initiation Improvements.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Submersion, "Submersion");

				// Create the replacement Improvement.
				_objImprovementManager.CreateImprovement("RES", Improvement.ImprovementSource.Submersion, "Submersion", Improvement.ImprovementType.Attribute, "", 0, 1, 0, _objCharacter.SubmersionGrade);
				_objImprovementManager.Commit();

				// Update any Echo Improvements the character might have.
				foreach (Metamagic objMetamagic in _objCharacter.Metamagics)
				{
					if (objMetamagic.Bonus != null)
					{
						// If the Bonus contains "Rating", remove the existing Improvement and create new ones.
						if (objMetamagic.Bonus.InnerXml.Contains("Rating"))
						{
							_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId);
							_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Echo, objMetamagic.InternalId, objMetamagic.Bonus, false, _objCharacter.SubmersionGrade, objMetamagic.DisplayNameShort);
						}
					}
				}

				lblInitiateGrade.Text = _objCharacter.SubmersionGrade.ToString();

				int intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.SubmersionGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

				string strInitTip = LanguageManager.Instance.GetString("Tip_ImproveSubmersionGrade").Replace("{0}", (_objCharacter.SubmersionGrade + 1).ToString()).Replace("{1}", intAmount.ToString());
				tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
			}

			UpdateInitiationGradeList();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cboTradition_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_blnLoading || cboTradition.SelectedValue.ToString() == string.Empty)
				return;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("traditions.xml");

			XmlNode objXmlTradition = objXmlDocument.SelectSingleNode("/chummer/traditions/tradition[name = \"" + cboTradition.SelectedValue + "\"]");
			lblDrainAttributes.Text = objXmlTradition["drain"].InnerText;
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("BOD", LanguageManager.Instance.GetString("String_AttributeBODShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("AGI", LanguageManager.Instance.GetString("String_AttributeAGIShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("REA", LanguageManager.Instance.GetString("String_AttributeREAShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("STR", LanguageManager.Instance.GetString("String_AttributeSTRShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("CHA", LanguageManager.Instance.GetString("String_AttributeCHAShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("INT", LanguageManager.Instance.GetString("String_AttributeINTShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("LOG", LanguageManager.Instance.GetString("String_AttributeLOGShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("WIL", LanguageManager.Instance.GetString("String_AttributeWILShort"));
			lblDrainAttributes.Text = lblDrainAttributes.Text.Replace("MAG", LanguageManager.Instance.GetString("String_AttributeMAGShort"));
			_objCharacter.MagicTradition = cboTradition.SelectedValue.ToString();

			foreach (SpiritControl objSpiritControl in panSpirits.Controls)
				objSpiritControl.RebuildSpiritList(cboTradition.SelectedValue.ToString());

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Additional Sprites and Complex Forms Tab Control Events
		private void treComplexForms_AfterSelect(object sender, TreeViewEventArgs e)
		{
			RefreshSelectedComplexForm();
		}

		private void cboStream_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_blnLoading || cboStream.SelectedValue.ToString() == string.Empty)
				return;

			XmlDocument objXmlDocument = XmlManager.Instance.Load("streams.xml");

			XmlNode objXmlTradition = objXmlDocument.SelectSingleNode("/chummer/traditions/tradition[name = \"" + cboStream.SelectedValue + "\"]");
			lblFadingAttributes.Text = objXmlTradition["drain"].InnerText;
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("BOD", LanguageManager.Instance.GetString("String_AttributeBODShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("AGI", LanguageManager.Instance.GetString("String_AttributeAGIShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("REA", LanguageManager.Instance.GetString("String_AttributeREAShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("STR", LanguageManager.Instance.GetString("String_AttributeSTRShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("CHA", LanguageManager.Instance.GetString("String_AttributeCHAShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("INT", LanguageManager.Instance.GetString("String_AttributeINTShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("LOG", LanguageManager.Instance.GetString("String_AttributeLOGShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("WIL", LanguageManager.Instance.GetString("String_AttributeWILShort"));
			lblFadingAttributes.Text = lblFadingAttributes.Text.Replace("RES", LanguageManager.Instance.GetString("String_AttributeRESShort"));
			_objCharacter.TechnomancerStream = cboStream.SelectedValue.ToString();

			foreach (SpiritControl objSpriteControl in panSprites.Controls)
				objSpriteControl.RebuildSpiritList(cboStream.SelectedValue.ToString());

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treComplexForms_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteComplexForm_Click(sender, e);
			}
		}
		#endregion

		#region Additional Initiation Tab Control Events
		private void chkInitiationGroup_CheckedChanged(object sender, EventArgs e)
		{
			double dblMultiplier = 1.0;
			if (chkInitiationGroup.Checked)
				dblMultiplier -= 0.2;
			if (chkInitiationOrdeal.Checked)
				dblMultiplier -= 0.2;
			dblMultiplier = Math.Round(dblMultiplier, 2);

			int intAmount = 0;
			if (_objCharacter.MAGEnabled)
				intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.InitiateGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));
			else
				intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.SubmersionGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

			string strInitTip = "";
			if (_objCharacter.MAGEnabled)
				strInitTip = LanguageManager.Instance.GetString("Tip_ImproveInitiateGrade").Replace("{0}", (_objCharacter.InitiateGrade + 1).ToString()).Replace("{1}", intAmount.ToString());
			else
				strInitTip = LanguageManager.Instance.GetString("Tip_ImproveSubmersionGrade").Replace("{0}", (_objCharacter.SubmersionGrade + 1).ToString()).Replace("{1}", intAmount.ToString());

			tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
		}

		private void chkInitiationOrdeal_CheckedChanged(object sender, EventArgs e)
		{
			double dblMultiplier = 1.0;
			if (chkInitiationGroup.Checked)
				dblMultiplier -= 0.2;
			if (chkInitiationOrdeal.Checked)
				dblMultiplier -= 0.2;
			dblMultiplier = Math.Round(dblMultiplier, 2);

			int intAmount = 0;
			if (_objCharacter.MAGEnabled)
				intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.InitiateGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));
			else
				intAmount = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((10 + ((_objCharacter.SubmersionGrade + 1) * _objOptions.KarmaInitiation)), GlobalOptions.Instance.CultureInfo) * dblMultiplier));

			string strInitTip = "";
			if (_objCharacter.MAGEnabled)
				strInitTip = LanguageManager.Instance.GetString("Tip_ImproveInitiateGrade").Replace("{0}", (_objCharacter.InitiateGrade + 1).ToString()).Replace("{1}", intAmount.ToString());
			else
				strInitTip = LanguageManager.Instance.GetString("Tip_ImproveSubmersionGrade").Replace("{0}", (_objCharacter.SubmersionGrade + 1).ToString()).Replace("{1}", intAmount.ToString());

			tipTooltip.SetToolTip(cmdImproveInitiation, strInitTip);
		}

		private void treMetamagic_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Locate the selected Metamagic.
			Metamagic objMetamagic = _objFunctions.FindMetamagic(treMetamagic.SelectedNode.Tag.ToString(), _objCharacter.Metamagics);

			string strBook = _objOptions.LanguageBookShort(objMetamagic.Source);
			string strPage = objMetamagic.Page;
			lblMetamagicSource.Text = strBook + " " + strPage;
			tipTooltip.SetToolTip(lblMetamagicSource, _objOptions.BookFromCode(objMetamagic.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objMetamagic.Page);
		}

		private void chkJoinGroup_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh || _blnLoading)
				return;

			// Joining a Network does not cost Karma for Technomancers, so this only applies to Magicians/Adepts.
			if (_objCharacter.MAGEnabled)
			{
				if (chkJoinGroup.Checked)
				{
					int intKarmaExpense = _objOptions.KarmaJoinGroup;

					if (intKarmaExpense > _objCharacter.Karma)
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						_blnSkipRefresh = true;
						chkJoinGroup.Checked = false;
						_blnSkipRefresh = false;
						return;
					}

					string strMessage = "";
					string strExpense = "";
					if (_objCharacter.MAGEnabled)
					{
						strMessage = LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseJoinGroup");
						strExpense = LanguageManager.Instance.GetString("String_ExpenseJoinGroup");
					}
					else
					{
						strMessage = LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseJoinNetwork");
						strExpense = LanguageManager.Instance.GetString("String_ExpenseJoinNetwork");
					}

					if (!ConfirmKarmaExpense(strMessage.Replace("{0}", intKarmaExpense.ToString())))
					{
						_blnSkipRefresh = true;
						chkJoinGroup.Checked = false;
						_blnSkipRefresh = false;
						return;
					}

					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intKarmaExpense * -1, strExpense, ExpenseType.Karma, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Karma -= intKarmaExpense;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateKarma(KarmaExpenseType.JoinGroup, "");
					objExpense.Undo = objUndo;

					_objCharacter.GroupMember = chkJoinGroup.Checked;
					UpdateCharacterInfo();
				}
				else
				{
					int intKarmaExpense = _objOptions.KarmaLeaveGroup;

					if (intKarmaExpense > _objCharacter.Karma)
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughKarma"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughKarma"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						_blnSkipRefresh = true;
						chkJoinGroup.Checked = true;
						_blnSkipRefresh = false;
						return;
					}

					string strMessage = "";
					string strExpense = "";
					if (_objCharacter.MAGEnabled)
					{
						strMessage = LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseLeaveGroup");
						strExpense = LanguageManager.Instance.GetString("String_ExpenseLeaveGroup");
					}
					else
					{
						strMessage = LanguageManager.Instance.GetString("Message_ConfirmKarmaExpenseLeaveNetwork");
						strExpense = LanguageManager.Instance.GetString("String_ExpenseLeaveNetwork");
					}

					if (!ConfirmKarmaExpense(strMessage.Replace("{0}", intKarmaExpense.ToString())))
					{
						_blnSkipRefresh = true;
						chkJoinGroup.Checked = true;
						_blnSkipRefresh = false;
						return;
					}

					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intKarmaExpense * -1, strExpense, ExpenseType.Karma, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Karma -= intKarmaExpense;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateKarma(KarmaExpenseType.LeaveGroup, "");
					objExpense.Undo = objUndo;

					_objCharacter.GroupMember = chkJoinGroup.Checked;
					UpdateCharacterInfo();
				}
			}
			else
			{
				_objCharacter.GroupMember = chkJoinGroup.Checked;
				UpdateCharacterInfo();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtGroupName_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.GroupName = txtGroupName.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtGroupNotes_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.GroupNotes = txtGroupNotes.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Additional Critter Powers Tab Control Events
		private void treCritterPowers_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Look for the selected Critter Power.
			lblCritterPowerName.Text = "";
			lblCritterPowerCategory.Text = "";
			lblCritterPowerType.Text = "";
			lblCritterPowerAction.Text = "";
			lblCritterPowerRange.Text = "";
			lblCritterPowerDuration.Text = "";
			lblCritterPowerSource.Text = "";
			tipTooltip.SetToolTip(lblCritterPowerSource, null);
			lblCritterPowerPointCost.Visible = false;
			lblCritterPowerPointCostLabel.Visible = false;
			try
			{
				if (treCritterPowers.SelectedNode.Level > 0)
				{
					CritterPower objPower = _objFunctions.FindCritterPower(treCritterPowers.SelectedNode.Tag.ToString(), _objCharacter.CritterPowers);

					lblCritterPowerName.Text = objPower.DisplayName;
					lblCritterPowerCategory.Text = objPower.DisplayCategory;
					lblCritterPowerType.Text = objPower.DisplayType;
					lblCritterPowerAction.Text = objPower.DisplayAction;
					lblCritterPowerRange.Text = objPower.DisplayRange;
					lblCritterPowerDuration.Text = objPower.DisplayDuration;
					chkCritterPowerCount.Checked = objPower.CountTowardsLimit;
					string strBook = _objOptions.LanguageBookShort(objPower.Source);
					string strPage = objPower.Page;
					lblCritterPowerSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblCritterPowerSource, _objOptions.LanguageBookLong(objPower.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objPower.Page);
					if (objPower.PowerPoints > 0)
					{
						lblCritterPowerPointCost.Text = objPower.PowerPoints.ToString();
						lblCritterPowerPointCost.Visible = true;
						lblCritterPowerPointCostLabel.Visible = true;
					}
				}
			}
			catch
			{
			}
		}

		private void chkCritterPowerCount_CheckedChanged(object sender, EventArgs e)
		{
			try
			{
				if (treCritterPowers.SelectedNode.Level == 0)
					return;
			}
			catch
			{
				return;
			}

			// Locate the selected Critter Power.
			CritterPower objPower = _objFunctions.FindCritterPower(treCritterPowers.SelectedNode.Tag.ToString(), _objCharacter.CritterPowers);

			objPower.CountTowardsLimit = chkCritterPowerCount.Checked;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Additional Karma and Nuyen Tab Control Events
		private void lstKarma_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				ListViewItem objTest = lstKarma.SelectedItems[0];
			}
			catch
			{
				return;
			}

			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			ListViewItem objItem = lstKarma.SelectedItems[0];

			// Find the selected Karma Expense.
			foreach (ExpenseLogEntry objCharacterEntry in _objCharacter.ExpenseEntries)
			{
				if (objCharacterEntry.InternalId == objItem.SubItems[3].Text)
				{
					objEntry = objCharacterEntry;
					break;
				}
			}

			// If this is a manual entry, let the player modify the amount.
			int intOldAmount = objEntry.Amount;
			bool blnAllowEdit = false;
			try
			{
				if (objEntry.Undo.KarmaType == KarmaExpenseType.ManualAdd || objEntry.Undo.KarmaType == KarmaExpenseType.ManualSubtract)
					blnAllowEdit = true;
			}
			catch
			{
				return;
			}

			frmExpense frmEditExpense = new frmExpense();
			frmEditExpense.strReason = objEntry.Reason;
			frmEditExpense.Amount = objEntry.Amount;
			frmEditExpense.Refund = objEntry.Refund;
			frmEditExpense.SelectedDate = objEntry.Date;
			frmEditExpense.LockFields(blnAllowEdit);

			frmEditExpense.ShowDialog(this);

			if (frmEditExpense.DialogResult == DialogResult.Cancel)
				return;

			// If this is a manual entry, update the character's Karma total.
			int intNewAmount = frmEditExpense.Amount;
			if (blnAllowEdit && intOldAmount != intNewAmount)
			{
				objEntry.Amount = intNewAmount;
				_objCharacter.Karma += (intNewAmount - intOldAmount);
				UpdateCharacterInfo();
			}

			// Rename the Expense.
			objEntry.Reason = frmEditExpense.strReason;
			objEntry.Date = frmEditExpense.SelectedDate;

			PopulateExpenseList();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void lstNuyen_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				ListViewItem objTest = lstNuyen.SelectedItems[0];
			}
			catch
			{
				return;
			}

			ExpenseLogEntry objEntry = new ExpenseLogEntry();
			ListViewItem objItem = lstNuyen.SelectedItems[0];

			// Find the selected Nuyen Expense.
			foreach (ExpenseLogEntry objCharacterEntry in _objCharacter.ExpenseEntries)
			{
				if (objCharacterEntry.InternalId == objItem.SubItems[3].Text)
				{
					objEntry = objCharacterEntry;
					break;
				}
			}

			// If this is a manual entry, let the player modify the amount.
			int intOldAmount = objEntry.Amount;
			bool blnAllowEdit = false;
			try
			{
				if (objEntry.Undo.NuyenType == NuyenExpenseType.ManualAdd || objEntry.Undo.NuyenType == NuyenExpenseType.ManualSubtract)
					blnAllowEdit = true;
			}
			catch
			{
				return;
			}

			frmExpense frmEditExpense = new frmExpense();
			frmEditExpense.Mode = ExpenseType.Nuyen;
			frmEditExpense.strReason = objEntry.Reason;
			frmEditExpense.Amount = objEntry.Amount;
			frmEditExpense.Refund = objEntry.Refund;
			frmEditExpense.SelectedDate = objEntry.Date;
			frmEditExpense.LockFields(blnAllowEdit);

			frmEditExpense.ShowDialog(this);

			if (frmEditExpense.DialogResult == DialogResult.Cancel)
				return;

			// If this is a manual entry, update the character's Karma total.
			int intNewAmount = frmEditExpense.Amount;
			if (blnAllowEdit && intOldAmount != intNewAmount)
			{
				objEntry.Amount = intNewAmount;
				_objCharacter.Nuyen += (intNewAmount - intOldAmount);
				UpdateCharacterInfo();
			}

			// Rename the Expense.
			objEntry.Reason = frmEditExpense.strReason;
			objEntry.Date = frmEditExpense.SelectedDate;

			PopulateExpenseList();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void lstKarma_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (e.Column == _lvwKarmaColumnSorter.SortColumn)
			{
				if (_lvwKarmaColumnSorter.Order == SortOrder.Ascending)
					_lvwKarmaColumnSorter.Order = SortOrder.Descending;
				else
					_lvwKarmaColumnSorter.Order = SortOrder.Ascending;
			}
			else
			{
				_lvwKarmaColumnSorter.SortColumn = e.Column;
				_lvwKarmaColumnSorter.Order = SortOrder.Ascending;
			}
			lstKarma.Sort();
		}

		private void lstNuyen_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (e.Column == _lvwNuyenColumnSorter.SortColumn)
			{
				if (_lvwNuyenColumnSorter.Order == SortOrder.Ascending)
					_lvwNuyenColumnSorter.Order = SortOrder.Descending;
				else
					_lvwNuyenColumnSorter.Order = SortOrder.Ascending;
			}
			else
			{
				_lvwNuyenColumnSorter.SortColumn = e.Column;
				_lvwNuyenColumnSorter.Order = SortOrder.Ascending;
			}
			lstNuyen.Sort();
		}
		#endregion

		#region Additional Calendar Tab Control Events
		private void lstCalendar_DoubleClick(object sender, EventArgs e)
		{
			cmdEditWeek_Click(sender, e);
		}
		#endregion

		#region Additional Improvements Tab Control Events
		private void treImprovements_AfterSelect(object sender, TreeViewEventArgs e)
		{
			lblImprovementType.Text = "";
			lblImprovementValue.Text = "";

			if (treImprovements.SelectedNode.Level == 0)
			{
				cmdImprovementsEnableAll.Visible = true;
				cmdImprovementsDisableAll.Visible = true;
			}
			else
			{
				cmdImprovementsEnableAll.Visible = false;
				cmdImprovementsDisableAll.Visible = false;
			}

			_blnSkipRefresh = true;
			try
			{
				if (treImprovements.SelectedNode.Level == 0)
				{
					lblImprovementType.Text = "";
					lblImprovementValue.Text = "";
					chkImprovementActive.Checked = false;
				}
				else
				{
					Improvement objImprovement = new Improvement();
					foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
					{
						if (objCharacterImprovement.SourceName == treImprovements.SelectedNode.Tag.ToString())
						{
							objImprovement = objCharacterImprovement;
							break;
						}
					}

					// Get the human-readable name of the Improvement from the Improvements file.
					XmlDocument objXmlDocument = XmlManager.Instance.Load("improvements.xml");

					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/improvements/improvement[id = \"" + objImprovement.CustomId + "\"]");
					string strName = objNode["name"].InnerText;
					if (objNode["translate"] != null)
						strName = objNode["translate"].InnerText;

					// Build a string that contains the value(s) of the Improvement.
					string strValue = "";
					if (objImprovement.Value != 0)
						strValue += LanguageManager.Instance.GetString("Label_CreateImprovementValue") + " " + objImprovement.Value.ToString() + ", ";
					if (objImprovement.Minimum != 0)
						strValue += LanguageManager.Instance.GetString("Label_CreateImprovementMinimum") + " " + objImprovement.Minimum.ToString() + ", ";
					if (objImprovement.Maximum != 0)
						strValue += LanguageManager.Instance.GetString("Label_CreateImprovementMaximum") + " " + objImprovement.Maximum.ToString() + ", ";
					if (objImprovement.Augmented != 0)
						strValue += LanguageManager.Instance.GetString("Label_CreateImprovementAugmented") + " " + objImprovement.Augmented.ToString() + ", ";

					// Remove the trailing comma.
					if (strValue != "")
						strValue = strValue.Substring(0, strValue.Length - 2);

					lblImprovementType.Text = strName;
					lblImprovementValue.Text = strValue;
					chkImprovementActive.Checked = objImprovement.Enabled;
				}
			}
			catch
			{
			}
			_blnSkipRefresh = false;
		}

		private void treImprovements_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				if (treImprovements.SelectedNode.Level > 0)
				{
					Improvement objImprovement = new Improvement();
					foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
					{
						if (objCharacterImprovement.SourceName == treImprovements.SelectedNode.Tag.ToString())
						{
							objImprovement = objCharacterImprovement;
							break;
						}
					}

					// Edit the selected Improvement.
					frmCreateImprovement frmPickImprovement = new frmCreateImprovement(_objCharacter);
					frmPickImprovement.EditImprovementObject = objImprovement;
					frmPickImprovement.ShowDialog(this);

					if (frmPickImprovement.DialogResult != DialogResult.Cancel)
					{
						UpdateCharacterInfo();

						_blnIsDirty = true;
						UpdateWindowTitle();
					}
				}
			}
			catch
			{
			}
		}

		private void chkImprovementActive_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			try
			{
				if (treImprovements.SelectedNode.Level > 0)
				{
					Improvement objImprovement = new Improvement();
					foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
					{
						if (objCharacterImprovement.SourceName == treImprovements.SelectedNode.Tag.ToString())
						{
							objImprovement = objCharacterImprovement;
							break;
						}
					}

					objImprovement.Enabled = chkImprovementActive.Checked;

					UpdateCharacterInfo();

					_blnIsDirty = true;
					UpdateWindowTitle();
				}
			}
			catch
			{
			}
		}

		private void treImprovements_ItemDrag(object sender, ItemDragEventArgs e)
		{
			try
			{
				// Do not allow the root element to be moved.
				if (treImprovements.SelectedNode.Tag.ToString() == "Node_SelectedImprovements")
					return;
			}
			catch
			{
				return;
			}
			_intDragLevel = treImprovements.SelectedNode.Level;
			DoDragDrop(e.Item, DragDropEffects.Move);
		}

		private void treImprovements_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Move;
		}

		private void treImprovements_DragDrop(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode nodDestination = ((TreeView)sender).GetNodeAt(pt);

			int intNewIndex = 0;
			try
			{
				intNewIndex = nodDestination.Index;
			}
			catch
			{
				intNewIndex = treImprovements.Nodes[treImprovements.Nodes.Count - 1].Nodes.Count;
				nodDestination = treImprovements.Nodes[treImprovements.Nodes.Count - 1];
			}

			if (treImprovements.SelectedNode.Level == 1)
				_objController.MoveImprovementNode(intNewIndex, nodDestination, treImprovements);
			else
				_objController.MoveImprovementRoot(intNewIndex, nodDestination, treImprovements);

			// Clear the background color for all Nodes.
			_objFunctions.ClearNodeBackground(treImprovements, null);

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void treImprovements_DragOver(object sender, DragEventArgs e)
		{
			Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
			TreeNode objNode = ((TreeView)sender).GetNodeAt(pt);

			if (objNode == null)
				return;

			// Highlight the Node that we're currently dragging over, provided it is of the same level or higher.
			if (objNode.Level <= _intDragLevel)
				objNode.BackColor = SystemColors.ControlDark;

			// Clear the background colour for all other Nodes.
			_objFunctions.ClearNodeBackground(treImprovements, objNode);
		}

		private void cmdAddImprovementGroup_Click(object sender, EventArgs e)
		{
			// Add a new location to the Improvements Tree.
			frmSelectText frmPickText = new frmSelectText();
			frmPickText.Description = LanguageManager.Instance.GetString("String_AddLocation");
			frmPickText.ShowDialog(this);

			if (frmPickText.DialogResult == DialogResult.Cancel || frmPickText.SelectedValue == "")
				return;

			string strLocation = frmPickText.SelectedValue;
			_objCharacter.ImprovementGroups.Add(strLocation);

			TreeNode objLocation = new TreeNode();
			objLocation.Tag = strLocation;
			objLocation.Text = strLocation;
			objLocation.ContextMenuStrip = cmsImprovementLocation;
			treImprovements.Nodes.Add(objLocation);
			UpdateWindowTitle();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Character Info Tab Event
		private void txtSex_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Sex = txtSex.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtAge_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Age = txtAge.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtEyes_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Eyes = txtEyes.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtHair_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Hair = txtHair.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtHeight_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Height = txtHeight.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtWeight_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Weight = txtWeight.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtSkin_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Skin = txtSkin.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtDescription_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Description = txtDescription.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtBackground_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Background = txtBackground.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtConcept_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Concept = txtConcept.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtNotes_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Notes = txtNotes.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtPlayerName_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.PlayerName = txtPlayerName.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void txtAlias_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Alias = txtAlias.Text;
			_blnIsDirty = true;
			UpdateWindowTitle(false);
		}

		private void nudStreetCred_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.StreetCred = Convert.ToInt32(nudStreetCred.Value);
			_blnIsDirty = true;
			UpdateReputation();
			UpdateWindowTitle();
		}

		private void nudNotoriety_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.Notoriety = Convert.ToInt32(nudNotoriety.Value);
			_blnIsDirty = true;
			UpdateReputation();
			UpdateWindowTitle();
		}

		private void nudPublicAware_ValueChanged(object sender, EventArgs e)
		{
			_objCharacter.PublicAwareness = Convert.ToInt32(nudPublicAware.Value);
			_blnIsDirty = true;
			UpdateReputation();
			UpdateWindowTitle();
		}
		#endregion

		#region Notes Tab Events
		private void txtGameNotes_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.GameNotes = txtGameNotes.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Tree KeyDown Events
		private void treQualities_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteQuality_Click(sender, e);
			}
		}

		private void treSpells_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteSpell_Click(sender, e);
			}
		}

		private void treCyberware_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteCyberware_Click(sender, e);
			}
		}

		private void treLifestyles_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteLifestyle_Click(sender, e);
			}
		}

		private void treArmor_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteArmor_Click(sender, e);
			}
		}

		private void treWeapons_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteWeapon_Click(sender, e);
			}
		}

		private void treGear_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteGear_Click(sender, e);
			}
		}

		private void treVehicles_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteVehicle_Click(sender, e);
			}
		}

		private void treMartialArts_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteMartialArt_Click(sender, e);
			}
		}

		private void treCritterPowers_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteCritterPower_Click(sender, e);
			}
		}

		private void treMetamagic_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteMetamagic_Click(sender, e);
			}
		}

		private void treImprovements_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				cmdDeleteImprovement_Click(sender, e);
			}
		}
		#endregion

		#region Splitter Resize Events
		private void splitSkills_Panel1_Resize(object sender, EventArgs e)
		{
			panActiveSkills.Height = splitSkills.Panel1.Height - panActiveSkills.Top;
			panSkillGroups.Height = splitSkills.Panel1.Height - panSkillGroups.Top;
			panActiveSkills.Width = splitSkills.Panel1.Width - panActiveSkills.Left;
			panSkillGroups.Width = panActiveSkills.Left - 6 - panSkillGroups.Left;

			cmdAddExoticSkill.Left = panActiveSkills.Left + panActiveSkills.Width - cmdAddExoticSkill.Width - 3;
			cboSkillFilter.Left = cmdAddExoticSkill.Left - cboSkillFilter.Width - 6;
		}

		private void splitSkills_Panel2_Resize(object sender, EventArgs e)
		{
			panKnowledgeSkills.Width = splitSkills.Panel2.Width - 3;
			panKnowledgeSkills.Height = splitSkills.Panel2.Height - panKnowledgeSkills.Top;
		}

		private void splitContacts_Panel1_Resize(object sender, EventArgs e)
		{
			panContacts.Width = splitContacts.Panel1.Width - 3;
			panContacts.Height = splitContacts.Panel1.Height - panContacts.Top;
		}

		private void splitContacts_Panel2_Resize(object sender, EventArgs e)
		{
			panEnemies.Width = splitContacts.Panel2.Width - 3;
			panEnemies.Height = splitContacts.Panel2.Height - panEnemies.Top;
		}

		private void splitKarmaNuyen_Panel1_Resize(object sender, EventArgs e)
		{
			lstKarma.Width = splitKarmaNuyen.Panel1.Width;
			chtKarma.Width = splitKarmaNuyen.Panel1.Width;
			chtKarma.Height = 210;
			chtKarma.Top = splitKarmaNuyen.Panel1.Height - 6 - chtKarma.Height;
			lstKarma.Height = chtKarma.Top - 6 - lstKarma.Top;
			try
			{
				if (lstKarma.Width > 409)
				{
					lstKarma.Columns[2].Width = lstKarma.Width - 220;
				}
			}
			catch
			{
			}
		}

		private void splitKarmaNuyen_Panel2_Resize(object sender, EventArgs e)
		{
			lstNuyen.Width = splitKarmaNuyen.Panel2.Width;
			chtNuyen.Width = splitKarmaNuyen.Panel2.Width;
			chtNuyen.Height = 210;
			chtNuyen.Top = splitKarmaNuyen.Panel2.Height - 6 - chtNuyen.Height;
			lstNuyen.Height = chtNuyen.Top - 6 - lstNuyen.Top;
			try
			{
				if (lstNuyen.Width > 409)
				{
					lstNuyen.Columns[2].Width = lstNuyen.Width - 220;
				}
			}
			catch
			{
			}
		}
		#endregion

		#region Other Control Events
		private void txtCharacterName_TextChanged(object sender, EventArgs e)
		{
			_objCharacter.Name = txtCharacterName.Text;
			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdEdgeSpent_Click(object sender, EventArgs e)
		{
			int intEdgeUsed = 0;
			foreach (Improvement objImprovement in _objCharacter.Improvements)
			{
				if (objImprovement.ImproveType == Improvement.ImprovementType.Attribute && objImprovement.ImprovedName == "EDG" && objImprovement.ImproveSource == Improvement.ImprovementSource.EdgeUse && objImprovement.Enabled)
					intEdgeUsed += objImprovement.Augmented * objImprovement.Rating;
			}

			if (intEdgeUsed - 1 < _objCharacter.EDG.Value * -1)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotSpendEdge"), LanguageManager.Instance.GetString("MessageTitle_CannotSpendEdge"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.EdgeUse, "edgeuse");
			intEdgeUsed -= 1;

			_objImprovementManager.CreateImprovement("EDG", Improvement.ImprovementSource.EdgeUse, "edgeuse", Improvement.ImprovementType.Attribute, "", 0, 1, 0, 0, intEdgeUsed);
			_objImprovementManager.Commit();
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cmdEdgeGained_Click(object sender, EventArgs e)
		{
			int intEdgeUsed = 0;
			foreach (Improvement objImprovement in _objCharacter.Improvements)
			{
				if (objImprovement.ImproveType == Improvement.ImprovementType.Attribute && objImprovement.ImprovedName == "EDG" && objImprovement.ImproveSource == Improvement.ImprovementSource.EdgeUse && objImprovement.Enabled)
					intEdgeUsed += objImprovement.Augmented * objImprovement.Rating;
			}

			if (intEdgeUsed + 1 > 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_CannotRegainEdge"), LanguageManager.Instance.GetString("MessageTitle_CannotRegainEdge"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.EdgeUse, "edgeuse");
			intEdgeUsed += 1;

			if (intEdgeUsed < 0)
			{
				_objImprovementManager.CreateImprovement("EDG", Improvement.ImprovementSource.EdgeUse, "edgeuse", Improvement.ImprovementType.Attribute, "", 0, 1, 0, 0, intEdgeUsed);
				_objImprovementManager.Commit();
			}
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void cboSkillFilter_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Hide the Panel so it redraws faster.
			panActiveSkills.Visible = false;
			switch (cboSkillFilter.SelectedValue.ToString())
			{
				case "0":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						objSkillControl.Visible = true;
					}
					break;
				case "1":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Rating > 0)
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "2":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.TotalRating > 0)
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "3":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Rating == 0)
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "BOD":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "BOD")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "AGI":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "AGI")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "REA":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "REA")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "STR":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "STR")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "CHA":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "CHA")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "INT":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "INT")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "LOG":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "LOG")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "WIL":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "WIL")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "MAG":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "MAG")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				case "RES":
					foreach (SkillControl objSkillControl in panActiveSkills.Controls)
					{
						if (objSkillControl.SkillObject.Attribute == "RES")
							objSkillControl.Visible = true;
						else
							objSkillControl.Visible = false;
					}
					break;
				default:
					if (cboSkillFilter.SelectedValue.ToString().StartsWith("GROUP:"))
					{
						string strGroup = cboSkillFilter.SelectedValue.ToString().Replace("GROUP:", string.Empty);
						foreach (SkillControl objSkillControl in panActiveSkills.Controls)
						{
							if (objSkillControl.SkillGroup == strGroup)
								objSkillControl.Visible = true;
							else
								objSkillControl.Visible = false;
						}
					}
					else
					{
						foreach (SkillControl objSkillControl in panActiveSkills.Controls)
						{
							if (objSkillControl.SkillCategory == cboSkillFilter.SelectedValue.ToString())
								objSkillControl.Visible = true;
							else
								objSkillControl.Visible = false;
						}
					}
					break;
			}
			panActiveSkills.Visible = true;
		}

		private void tabCharacterTabs_SelectedIndexChanged(object sender, EventArgs e)
		{
			RefreshPasteStatus();
		}

		private void tabStreetGearTabs_SelectedIndexChanged(object sender, EventArgs e)
		{
			RefreshPasteStatus();
		}
		#endregion

		#region Clear Tab Contents
		/// <summary>
		/// Clear the contents of the Spells and Spirits Tab.
		/// </summary>
		private void ClearSpellTab()
		{
			_objController.ClearSpellTab(treSpells);

			// Remove the Spirits.
			panSpirits.Controls.Clear();

			_blnIsDirty = true;
			UpdateCharacterInfo();
		}

		/// <summary>
		/// Clear the contents of the Adept Powers Tab.
		/// </summary>
		private void ClearAdeptTab()
		{
			_objController.ClearAdeptTab();

			// Remove all of the Adept Powers from the panel.
			panPowers.Controls.Clear();

			_blnIsDirty = true;
			UpdateCharacterInfo();
		}

		/// <summary>
		/// Clear the contents of the Sprites and Complex Forms Tab.
		/// </summary>
		private void ClearTechnomancerTab()
		{
			_objController.ClearTechnomancerTab(treComplexForms);

			// Remove the Sprites.
			panSprites.Controls.Clear();

			_blnIsDirty = true;
			UpdateCharacterInfo();
		}

		/// <summary>
		/// Clear the conents of the Critter Powers Tab.
		/// </summary>
		private void ClearCritterTab()
		{
			_objController.ClearCritterTab(treCritterPowers);

			_blnIsDirty = true;
			UpdateCharacterInfo();
		}

		/// <summary>
		/// Clear the content of the Initiation Tab.
		/// </summary>
		private void ClearInitiationTab()
		{
			_objController.ClearInitiationTab(treMetamagic);
			UpdateInitiationGradeList();

			_blnIsDirty = true;
			UpdateCharacterInfo();
		}
		#endregion

		#region Condition Monitors
		private void chkPhysicalCM_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			int intFillCount = 0;

			CheckBox objCheck = (CheckBox)sender;
			if (objCheck.Checked)
			{
				// If this is being checked, make sure everything before it is checked off.
				_blnSkipRefresh = true;
				foreach (CheckBox objPhysicalCM in panPhysicalCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) < Convert.ToInt32(objCheck.Tag.ToString()))
						objPhysicalCM.Checked = true;

					if (objPhysicalCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}
			else
			{
				// If this is being unchecked, make sure everything after it is unchecked.
				_blnSkipRefresh = true;
				foreach (CheckBox objPhysicalCM in panPhysicalCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) > Convert.ToInt32(objCheck.Tag.ToString()))
						objPhysicalCM.Checked = false;

					if (objPhysicalCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}

			_objCharacter.PhysicalCMFilled = intFillCount;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkStunCM_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			int intFillCount = 0;

			CheckBox objCheck = (CheckBox)sender;
			if (objCheck.Checked)
			{
				// If this is being checked, make sure everything before it is checked off.
				_blnSkipRefresh = true;
				foreach (CheckBox objStunCM in panStunCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objStunCM.Tag.ToString()) < Convert.ToInt32(objCheck.Tag.ToString()))
						objStunCM.Checked = true;

					if (objStunCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}
			else
			{
				// If this is being unchecked, make sure everything after it is unchecked.
				_blnSkipRefresh = true;
				foreach (CheckBox objStunCM in panStunCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objStunCM.Tag.ToString()) > Convert.ToInt32(objCheck.Tag.ToString()))
						objStunCM.Checked = false;

					if (objStunCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}
			
			_objCharacter.StunCMFilled = intFillCount;
			
			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}

		private void chkVehicleCM_CheckedChanged(object sender, EventArgs e)
		{
			if (_blnSkipRefresh)
				return;

			// Locate the selected Vehicle.
			TreeNode objVehicleNode = new TreeNode();
			objVehicleNode = treVehicles.SelectedNode;
			if (treVehicles.SelectedNode.Level > 1)
			{
				while (objVehicleNode.Level > 1)
					objVehicleNode = objVehicleNode.Parent;
			}

			Vehicle objVehicle = new Vehicle(_objCharacter);
			foreach (Vehicle objCharacterVehicle in _objCharacter.Vehicles)
			{
				if (objCharacterVehicle.InternalId == objVehicleNode.Tag.ToString())
				{
					objVehicle = objCharacterVehicle;
					break;
				}
			}

			int intFillCount = 0;

			CheckBox objCheck = (CheckBox)sender;
			if (objCheck.Checked)
			{
				// If this is being checked, make sure everything before it is checked off.
				_blnSkipRefresh = true;
				foreach (CheckBox objVehicleCM in panVehicleCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objVehicleCM.Tag.ToString()) < Convert.ToInt32(objCheck.Tag.ToString()))
						objVehicleCM.Checked = true;

					if (objVehicleCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}
			else
			{
				// If this is being unchecked, make sure everything after it is unchecked.
				_blnSkipRefresh = true;
				foreach (CheckBox objVehicleCM in panVehicleCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objVehicleCM.Tag.ToString()) > Convert.ToInt32(objCheck.Tag.ToString()))
						objVehicleCM.Checked = false;

					if (objVehicleCM.Checked)
						intFillCount += 1;
				}
				_blnSkipRefresh = false;
			}

			objVehicle.PhysicalCMFilled = intFillCount;

			UpdateCharacterInfo();

			_blnIsDirty = true;
			UpdateWindowTitle();
		}
		#endregion

		#region Properties
		/// <summary>
		/// Character's name to use when loading them in a new tab.
		/// </summary>
		public string CharacterName
		{
			get
			{
				if (_objCharacter.Alias.Trim() != string.Empty)
					return _objCharacter.Alias;
				if (_objCharacter.Name.Trim() != string.Empty)
					return _objCharacter.Name;
				return LanguageManager.Instance.GetString("String_UnnamedCharacter");
			}
		}
		#endregion

		#region Sourcebook Label Events
		private void lblMetatypeSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblMetatypeSource.Text);
		}

		private void lblQualitySource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblQualitySource.Text);
		}

		private void lblMartialArtSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblMartialArtSource.Text);
		}

		private void lblSpellSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblSpellSource.Text);
		}

		private void lblComplexFormSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblComplexFormSource.Text);
		}

		private void lblCritterPowerSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblCritterPowerSource.Text);
		}

		private void lblMetamagicSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblMetamagicSource.Text);
		}

		private void lblCyberwareSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblCyberwareSource.Text);
		}

		private void lblLifestyleSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblLifestyleSource.Text);
		}

		private void lblArmorSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblArmorSource.Text);
		}

		private void lblWeaponSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblWeaponSource.Text);
		}

		private void lblGearSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblGearSource.Text);
		}

		private void lblVehicleSource_Click(object sender, EventArgs e)
		{
			CommonFunctions objCommon = new CommonFunctions(_objCharacter);
			_objFunctions.OpenPDF(lblVehicleSource.Text);
		}
		#endregion

		#region Custom Methods
		/// <summary>
		/// Let the application know that a Metatype has been selected.
		/// </summary>
		public void MetatypeSelected()
		{
			// Set the Minimum and Maximum values for each Attribute based on the selected MetaType.
			// Also update the Maximum and Augmented Maximum values displayed.
			_blnSkipUpdate = true;

			int intEssenceLoss = 0;
			if (!_objOptions.ESSLossReducesMaximumOnly && !_objCharacter.OverrideSpecialAttributeEssenceLoss)
				intEssenceLoss = _objCharacter.EssencePenalty;
			else
			{
				if (_objCharacter.MAGEnabled)
				{
					if (_objCharacter.MAG.Value > _objCharacter.MAG.TotalMaximum)
						intEssenceLoss = _objCharacter.MAG.Value - _objCharacter.MAG.TotalMaximum;
				}
				else if (_objCharacter.RESEnabled)
				{
					if (_objCharacter.RES.Value > _objCharacter.RES.TotalMaximum)
						intEssenceLoss = _objCharacter.RES.Value - _objCharacter.RES.TotalMaximum;
				}
			}

			lblBOD.Text = _objCharacter.BOD.Value.ToString();
			lblAGI.Text = _objCharacter.AGI.Value.ToString();
			lblREA.Text = _objCharacter.REA.Value.ToString();
			lblSTR.Text = _objCharacter.STR.Value.ToString();
			lblCHA.Text = _objCharacter.CHA.Value.ToString();
			lblINT.Text = _objCharacter.INT.Value.ToString();
			lblLOG.Text = _objCharacter.LOG.Value.ToString();
			lblWIL.Text = _objCharacter.WIL.Value.ToString();
			lblEDG.Text = _objCharacter.EDG.Value.ToString();
			lblMAG.Text = (_objCharacter.MAG.Value - intEssenceLoss).ToString();
			lblRES.Text = (_objCharacter.RES.Value - intEssenceLoss).ToString();

			_blnSkipUpdate = false;

			UpdateCharacterInfo();
		}

		/// <summary>
		/// Calculate the number of Adept Power Points used.
		/// </summary>
		private void CalculatePowerPoints()
		{
			decimal decPowerPoints = 0;

			foreach (PowerControl objPowerControl in panPowers.Controls)
			{
				decPowerPoints += objPowerControl.PowerPoints;
				objPowerControl.UpdatePointsPerLevel();
			}

			int intMAG = 0;
			if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
			{
				// If both Adept and Magician are enabled, this is a Mystic Adept, so use the MAG amount assigned to this portion.
				intMAG = _objCharacter.MAGAdept;
			}
			else
			{
				// The character is just an Adept, so use the full value.
				intMAG = _objCharacter.MAG.TotalValue;
			}

			// Add any Power Point Improvements to MAG.
			intMAG += _objImprovementManager.ValueOf(Improvement.ImprovementType.AdeptPowerPoints);

			string strRemain = (intMAG - decPowerPoints).ToString();
			while (strRemain.EndsWith("0") && strRemain.Length > 4)
				strRemain = strRemain.Substring(0, strRemain.Length - 1);

			lblPowerPoints.Text = String.Format("{1} ({0} " + LanguageManager.Instance.GetString("String_Remaining") + ")", strRemain, intMAG);
		}

		/// <summary>
		/// Update the Character information.
		/// </summary>
		public void UpdateCharacterInfo()
		{
			if (_blnLoading)
				return;

			if (!_blnSkipUpdate)
			{
				string strTip = "";
				_blnSkipUpdate = true;

				string strFormat;
				if (_objCharacter.Options.EssenceDecimals == 4)
					strFormat = "{0:0.0000}";
				else
					strFormat = "{0:0.00}";
				decimal decESS = _objCharacter.Essence;
				lblESSMax.Text = decESS.ToString();
				tssEssence.Text = string.Format(strFormat, decESS);

				lblCyberwareESS.Text = string.Format(strFormat, _objCharacter.CyberwareEssence);
				lblBiowareESS.Text = string.Format(strFormat, _objCharacter.BiowareEssence);
				lblEssenceHoleESS.Text = string.Format(strFormat, _objCharacter.EssenceHole);

				// Reduce a character's MAG and RES from Essence Loss.
				int intReduction = _objCharacter.ESS.MetatypeMaximum - Convert.ToInt32(Math.Floor(decESS));

				// Remove any Improvements from MAG and RES from Essence Loss.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.EssenceLoss, "Essence Loss");

				// Create the Essence Loss Improvements.
				if (intReduction > 0)
				{
					_objImprovementManager.CreateImprovement("MAG", Improvement.ImprovementSource.EssenceLoss, "Essence Loss", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intReduction * -1);
					_objImprovementManager.CreateImprovement("RES", Improvement.ImprovementSource.EssenceLoss, "Essence Loss", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intReduction * -1);
				}

				int intEssenceLoss = 0;
				if (!_objOptions.ESSLossReducesMaximumOnly && !_objCharacter.OverrideSpecialAttributeEssenceLoss)
					intEssenceLoss = _objCharacter.EssencePenalty;
				else
				{
					if (_objCharacter.MAGEnabled)
					{
						if (_objCharacter.MAG.Value > _objCharacter.MAG.TotalMaximum)
							intEssenceLoss = _objCharacter.MAG.Value - _objCharacter.MAG.TotalMaximum;
					}
					else if (_objCharacter.RESEnabled)
					{
						if (_objCharacter.RES.Value > _objCharacter.RES.TotalMaximum)
							intEssenceLoss = _objCharacter.RES.Value - _objCharacter.RES.TotalMaximum;
					}
				}

				// Update the Attribute information.
				lblBOD.Text = _objCharacter.BOD.Value.ToString();
				lblAGI.Text = _objCharacter.AGI.Value.ToString();
				lblREA.Text = _objCharacter.REA.Value.ToString();
				lblSTR.Text = _objCharacter.STR.Value.ToString();
				lblCHA.Text = _objCharacter.CHA.Value.ToString();
				lblINT.Text = _objCharacter.INT.Value.ToString();
				lblLOG.Text = _objCharacter.LOG.Value.ToString();
				lblWIL.Text = _objCharacter.WIL.Value.ToString();
				lblEDG.Text = _objCharacter.EDG.Value.ToString();
				if (_objCharacter.MAG.Value - intEssenceLoss < 0)
					lblMAG.Text = "0";
				else
					lblMAG.Text = (_objCharacter.MAG.Value - intEssenceLoss).ToString();
				if (_objCharacter.RES.Value - intEssenceLoss < 0)
					lblRES.Text = "0";
				else
					lblRES.Text = (_objCharacter.RES.Value - intEssenceLoss).ToString();

				// If the Attribute reaches 0, the character has burned out.
				if (_objCharacter.MAG.Value - intEssenceLoss < 1 && _objCharacter.MAGEnabled)
				{
					_objCharacter.MAG.Value = 0;
					_objCharacter.MAG.MetatypeMinimum = 0;
					_objCharacter.MAG.MetatypeMaximum = 0;
					_objCharacter.MAG.MetatypeAugmentedMaximum = 0;

					if (_objCharacter.MAGEnabled)
					{
						// Move all MAG-linked Active Skills to Knowledge Skills.
						List<Skill> lstNewSkills = new List<Skill>();
						foreach (Skill objSkill in _objCharacter.Skills)
						{
							if (objSkill.Attribute == "MAG" && objSkill.Rating > 0)
							{
								int i = panKnowledgeSkills.Controls.Count;
								Skill objKnowledge = new Skill(_objCharacter);

								SkillControl objSkillControl = new SkillControl();
								objKnowledge.Name = objSkill.Name;
								objSkillControl.SkillObject = objKnowledge;

								// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
								objSkillControl.RatingChanged += objKnowledgeSkill_RatingChanged;
								objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
								objSkillControl.DeleteSkill += objKnowledgeSkill_DeleteSkill;
								objSkillControl.SkillKarmaClicked += objKnowledgeSkill_KarmaClicked;
								objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

								objSkillControl.KnowledgeSkill = true;
								objSkillControl.AllowDelete = true;
								if (objSkill.Rating > 7)
									objSkillControl.SkillRatingMaximum = objSkill.Rating;
								else
									objSkillControl.SkillRatingMaximum = 6;
								objSkillControl.SkillRating = objSkill.Rating;
								objSkillControl.SkillCategory = "Professional";
								// Set the SkillControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
								objSkillControl.Location = new Point(0, objSkillControl.Height * i + panKnowledgeSkills.AutoScrollPosition.Y);
								panKnowledgeSkills.Controls.Add(objSkillControl);

								lstNewSkills.Add(objKnowledge);
							}
						}
						foreach (Skill objSkill in lstNewSkills)
							_objCharacter.Skills.Add(objSkill);
					}

					_objCharacter.MAGEnabled = false;
					_objCharacter.MagicianEnabled = false;
					_objCharacter.AdeptEnabled = false;
				}
				if (_objCharacter.RES.Value - intEssenceLoss < 1 && _objCharacter.RESEnabled)
				{
					_objCharacter.RES.Value = 0;
					_objCharacter.RES.MetatypeMinimum = 0;
					_objCharacter.RES.MetatypeMinimum = 0;
					_objCharacter.RES.MetatypeAugmentedMaximum = 0;

					if (_objCharacter.RESEnabled)
					{
						// Move all RES-linked Active Skills to Knowledge Skills.
						List<Skill> lstNewSkills = new List<Skill>();
						foreach (Skill objSkill in _objCharacter.Skills)
						{
							if (objSkill.Attribute == "RES" && objSkill.Rating > 0)
							{
								int i = panKnowledgeSkills.Controls.Count;
								Skill objKnowledge = new Skill(_objCharacter);

								SkillControl objSkillControl = new SkillControl();
								objKnowledge.Name = objSkill.Name;
								objSkillControl.SkillObject = objKnowledge;

								// Attach an EventHandler for the RatingChanged and SpecializationChanged Events.
								objSkillControl.RatingChanged += objKnowledgeSkill_RatingChanged;
								objSkillControl.SpecializationChanged += objSkill_SpecializationChanged;
								objSkillControl.DeleteSkill += objKnowledgeSkill_DeleteSkill;
								objSkillControl.SkillKarmaClicked += objKnowledgeSkill_KarmaClicked;
								objSkillControl.DiceRollerClicked += objSkill_DiceRollerClicked;

								objSkillControl.KnowledgeSkill = true;
								objSkillControl.AllowDelete = true;
								if (objSkill.Rating > 7)
									objSkillControl.SkillRatingMaximum = objSkill.Rating;
								else
									objSkillControl.SkillRatingMaximum = 6;
								objSkillControl.SkillRating = objSkill.Rating;
								objSkillControl.SkillCategory = "Professional";
								// Set the SkillControl's Location since scrolling the Panel causes it to actually change the child Controls' Locations.
								objSkillControl.Location = new Point(0, objSkillControl.Height * i + panKnowledgeSkills.AutoScrollPosition.Y);
								panKnowledgeSkills.Controls.Add(objSkillControl);

								lstNewSkills.Add(objKnowledge);
							}
						}
						foreach (Skill objSkill in lstNewSkills)
							_objCharacter.Skills.Add(objSkill);
					}

					_objCharacter.RESEnabled = false;
					_objCharacter.TechnomancerEnabled = false;
				}

				// If the character is an A.I., set the Edge MetatypeMaximum to their Rating.
				if (_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients")
					_objCharacter.EDG.MetatypeMaximum = _objCharacter.Rating;

				// If the character is Cyberzombie, adjust their Attributes based on their Essence.
				if (_objCharacter.MetatypeCategory == "Cyberzombie")
				{
					int intESSModifier = _objCharacter.EssencePenalty - Convert.ToInt32(_objCharacter.EssenceMaximum);
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes");
					_objImprovementManager.CreateImprovement("BOD", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("AGI", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("REA", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("STR", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("CHA", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("INT", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("LOG", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
					_objImprovementManager.CreateImprovement("WIL", Improvement.ImprovementSource.Cyberzombie, "Cyberzombie Attributes", Improvement.ImprovementType.Attribute, "", 0, 1, 0, intESSModifier);
				}

				// Update the Attribute Improvement Cost ToolTips.
				string strTooltip = "";
				if (!_objOptions.AlternateMetatypeAttributeKarma)
				{
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveBOD, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveAGI, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveREA, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveSTR, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveCHA, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveINT, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveLOG, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveWIL, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveEDG, strTooltip);
					if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
					{
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveMAG, strTooltip);
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveRES, strTooltip);
					}
					else
					{
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.MAG.Value - _objCharacter.EssencePenalty + 1).ToString()).Replace("{1}", ((_objCharacter.MAG.Value + _objCharacter.EssencePenalty - 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveMAG, strTooltip);
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.RES.Value - _objCharacter.EssencePenalty + 1).ToString()).Replace("{1}", ((_objCharacter.RES.Value + _objCharacter.EssencePenalty - 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveRES, strTooltip);
					}
				}
				else
				{
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.BOD.Value + _objCharacter.BOD.AttributeValueModifiers - _objCharacter.BOD.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveBOD, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.AGI.Value + _objCharacter.AGI.AttributeValueModifiers - _objCharacter.AGI.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveAGI, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.REA.Value + _objCharacter.REA.AttributeValueModifiers - _objCharacter.REA.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveREA, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.STR.Value + _objCharacter.STR.AttributeValueModifiers - _objCharacter.STR.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveSTR, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.CHA.Value + _objCharacter.CHA.AttributeValueModifiers - _objCharacter.CHA.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveCHA, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.INT.Value + _objCharacter.INT.AttributeValueModifiers - _objCharacter.INT.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveINT, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.LOG.Value + _objCharacter.LOG.AttributeValueModifiers - _objCharacter.LOG.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveLOG, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.WIL.Value + _objCharacter.WIL.AttributeValueModifiers - _objCharacter.WIL.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveWIL, strTooltip);
					strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.EDG.Value + _objCharacter.EDG.AttributeValueModifiers - _objCharacter.EDG.MetatypeMinimum + 2) * _objOptions.KarmaAttribute).ToString());
					tipTooltip.SetToolTip(cmdImproveEDG, strTooltip);
					if (!_objOptions.SpecialKarmaCostBasedOnShownValue)
					{
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.MAG.Value + _objCharacter.MAG.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveMAG, strTooltip);
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers + 1).ToString()).Replace("{1}", ((_objCharacter.RES.Value + _objCharacter.RES.AttributeValueModifiers + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveRES, strTooltip);
					}
					else
					{
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.MAG.Value - _objCharacter.EssencePenalty + 1).ToString()).Replace("{1}", ((_objCharacter.MAG.Value - _objCharacter.EssencePenalty + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveMAG, strTooltip);
						strTooltip = LanguageManager.Instance.GetString("Tip_ImproveItem").Replace("{0}", (_objCharacter.RES.Value - _objCharacter.EssencePenalty + 1).ToString()).Replace("{1}", ((_objCharacter.RES.Value - _objCharacter.EssencePenalty + 1) * _objOptions.KarmaAttribute).ToString());
						tipTooltip.SetToolTip(cmdImproveRES, strTooltip);
					}
				}

				// Disable any Attribute Karma buttons that have reached their Total Metatype Maximum.
				cmdImproveBOD.Enabled = !(_objCharacter.BOD.Value == _objCharacter.BOD.TotalMaximum);
				cmdImproveAGI.Enabled = !(_objCharacter.AGI.Value == _objCharacter.AGI.TotalMaximum);
				cmdImproveREA.Enabled = !(_objCharacter.REA.Value == _objCharacter.REA.TotalMaximum);
				cmdImproveSTR.Enabled = !(_objCharacter.STR.Value == _objCharacter.STR.TotalMaximum);
				cmdImproveCHA.Enabled = !(_objCharacter.CHA.Value == _objCharacter.CHA.TotalMaximum);
				cmdImproveINT.Enabled = !(_objCharacter.INT.Value == _objCharacter.INT.TotalMaximum);
				cmdImproveLOG.Enabled = !(_objCharacter.LOG.Value == _objCharacter.LOG.TotalMaximum);
				cmdImproveWIL.Enabled = !(_objCharacter.WIL.Value == _objCharacter.WIL.TotalMaximum);
				cmdImproveEDG.Enabled = !(_objCharacter.EDG.Value == _objCharacter.EDG.TotalMaximum);

				// Disable the Magic or Resonance Karma buttons if they have reached their current limits.
				if (_objCharacter.MAGEnabled)
					cmdImproveMAG.Enabled = !(_objCharacter.MAG.Value - intEssenceLoss >= _objCharacter.MAG.TotalMaximum);
				else
					cmdImproveMAG.Enabled = false;

				if (_objCharacter.RESEnabled)
					cmdImproveRES.Enabled = !(_objCharacter.RES.Value - intEssenceLoss >= _objCharacter.RES.TotalMaximum);
				else
					cmdImproveRES.Enabled = false;

				// Condition Monitor.
				double dblBOD = _objCharacter.BOD.TotalValue;
				double dblWIL = _objCharacter.WIL.TotalValue;
				int intCMPhysical = _objCharacter.PhysicalCM;
				int intCMStun = _objCharacter.StunCM;
				int intCMOverflow = _objCharacter.CMOverflow;

				// Update the Condition Monitor labels.
				lblCMPhysical.Text = intCMPhysical.ToString();
				lblCMStun.Text = intCMStun.ToString();
				string strCM = "8 + (BOD/2)(" + ((int)Math.Ceiling(dblBOD / 2)).ToString() + ")";
				if (_objImprovementManager.ValueOf(Improvement.ImprovementType.PhysicalCM) != 0)
					strCM += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.PhysicalCM).ToString() + ")";
				tipTooltip.SetToolTip(lblCMPhysical, strCM);
				strCM = "8 + (WIL/2)(" + ((int)Math.Ceiling(dblWIL / 2)).ToString() + ")";
				if (_objImprovementManager.ValueOf(Improvement.ImprovementType.StunCM) != 0)
					strCM += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.StunCM).ToString() + ")";
				tipTooltip.SetToolTip(lblCMStun, strCM);

				int intCMThreshold = _objCharacter.CMThreshold;
				int intPhysicalCMPenalty = 0;
				int intStunCMPenalty = 0;
				int intCMPenalty = 0;

				// Hide any unused Physical CM boxes.
				foreach (CheckBox objPhysicalCM in panPhysicalCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) <= intCMPhysical + intCMOverflow)
					{
						if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) <= _objCharacter.PhysicalCMFilled)
							objPhysicalCM.Checked = true;

						objPhysicalCM.Visible = true;
						
						if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) <= intCMPhysical)
						{
							// If this is within the Physical CM limits, act normally.
							objPhysicalCM.BackColor = SystemColors.Control;
							objPhysicalCM.UseVisualStyleBackColor = true;
							if ((Convert.ToInt32(objPhysicalCM.Tag.ToString()) - _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset)) % intCMThreshold == 0 && Convert.ToInt32(objPhysicalCM.Tag.ToString()) > _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset))
							{
								int intModifiers = ((Convert.ToInt32(objPhysicalCM.Tag.ToString()) - _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset)) / intCMThreshold) * -1;
								objPhysicalCM.Text = intModifiers.ToString();
								if (objPhysicalCM.Checked)
								{
									if (intModifiers < intPhysicalCMPenalty)
										intPhysicalCMPenalty = intModifiers;
								}
							}
							else
								objPhysicalCM.Text = "";
						}
						else if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) > intCMPhysical)
						{
							objPhysicalCM.BackColor = SystemColors.ControlDark;
							if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) == intCMPhysical + intCMOverflow)
								objPhysicalCM.Text = "D";
							else
								objPhysicalCM.Text = "";
						}
					}
					else
					{
						objPhysicalCM.Visible = false;
						objPhysicalCM.Text = "";
					}
				}

				// Hide any unused Stun CM boxes.
				foreach (CheckBox objStunCM in panStunCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objStunCM.Tag.ToString()) <= intCMStun)
					{
						if (Convert.ToInt32(objStunCM.Tag.ToString()) <= _objCharacter.StunCMFilled)
							objStunCM.Checked = true;

						objStunCM.Visible = true;
						if ((Convert.ToInt32(objStunCM.Tag.ToString()) - _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset)) % intCMThreshold == 0 && Convert.ToInt32(objStunCM.Tag.ToString()) > _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset))
						{
							int intModifiers = ((Convert.ToInt32(objStunCM.Tag.ToString()) - _objImprovementManager.ValueOf(Improvement.ImprovementType.CMThresholdOffset)) / intCMThreshold) * -1;
							objStunCM.Text = intModifiers.ToString();
							if (objStunCM.Checked)
							{
								if (intModifiers < intStunCMPenalty)
									intStunCMPenalty = intModifiers;
							}
						}
						else
							objStunCM.Text = "";
					}
					else
					{
						objStunCM.Visible = false;
						objStunCM.Text = "";
					}
				}

				// Reduce the CM Penalties to 0 if the character has Improvements to ignore them.
				if (_objCharacter.HasImprovement(Improvement.ImprovementType.IgnoreCMPenaltyStun, true))
					intStunCMPenalty = 0;
				if (_objCharacter.HasImprovement(Improvement.ImprovementType.IgnoreCMPenaltyPhysical, true))
					intPhysicalCMPenalty = 0;

				intCMPenalty = intPhysicalCMPenalty + intStunCMPenalty;
				lblCMPenalty.Text = intCMPenalty.ToString();

				// Discard any old Condition Monitor penalties.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ConditionMonitor, "");

				// Create the new Condition Monitor penalties.
				if (intCMPenalty < 0)
					_objImprovementManager.CreateImprovement("", Improvement.ImprovementSource.ConditionMonitor, "", Improvement.ImprovementType.ConditionMonitor, "", intCMPenalty);

				// Update the character's Skill information.
				foreach (SkillControl objSkillControl in panActiveSkills.Controls)
				{
					objSkillControl.SkillRatingMaximum = objSkillControl.SkillObject.RatingMaximum;
					objSkillControl.RefreshControl();
				}

				// Update the character's Knowledge Skill information.
				foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls)
				{
					objSkillControl.SkillRatingMaximum = objSkillControl.SkillObject.RatingMaximum;
					objSkillControl.RefreshControl();
				}

				// Armor Ratings.
				lblArmor.Text = _objCharacter.TotalArmorRating.ToString();
				lblCMArmor.Text = lblArmor.Text;
				string strArmorToolTip = "";
				strArmorToolTip = LanguageManager.Instance.GetString("Tip_Armor") + " (" + _objCharacter.ArmorRating.ToString() + ")";
                if (_objCharacter.ArmorRating != _objCharacter.TotalArmorRating)
					strArmorToolTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + (_objCharacter.TotalArmorRating - _objCharacter.ArmorRating).ToString() + ")";
				tipTooltip.SetToolTip(lblArmor, strArmorToolTip);

				// Remove any Improvements from Armor Encumbrance.
				_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.ArmorEncumbrance, "Armor Encumbrance");
				// Create the Armor Encumbrance Improvements.
				if (_objCharacter.ArmorEncumbrance < 0)
				{
					_objImprovementManager.CreateImprovement("AGI", Improvement.ImprovementSource.ArmorEncumbrance, "Armor Encumbrance", Improvement.ImprovementType.Attribute, "", 0, 1, 0, 0, _objCharacter.ArmorEncumbrance);
					_objImprovementManager.CreateImprovement("REA", Improvement.ImprovementSource.ArmorEncumbrance, "Armor Encumbrance", Improvement.ImprovementType.Attribute, "", 0, 1, 0, 0, _objCharacter.ArmorEncumbrance);
				}

                // Update the Attribute information.
				// Attribute: Body.
				lblBODMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.BOD.TotalMinimum, _objCharacter.BOD.TotalMaximum, _objCharacter.BOD.TotalAugmentedMaximum);
				if (_objCharacter.BOD.HasModifiers)
				{
					lblBODAug.Text = string.Format("({0})", _objCharacter.BOD.TotalValue);
					tipTooltip.SetToolTip(lblBODAug, _objCharacter.BOD.ToolTip());
				}
				else
				{
					lblBODAug.Text = "";
					tipTooltip.SetToolTip(lblBODAug, "");
				}

				// Attribute: Agility.
				lblAGIMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.AGI.TotalMinimum, _objCharacter.AGI.TotalMaximum, _objCharacter.AGI.TotalAugmentedMaximum);
				if (_objCharacter.AGI.HasModifiers)
				{
					lblAGIAug.Text = string.Format("({0})", _objCharacter.AGI.TotalValue);
					tipTooltip.SetToolTip(lblAGIAug, _objCharacter.AGI.ToolTip());
				}
				else
				{
					lblAGIAug.Text = "";
					tipTooltip.SetToolTip(lblAGIAug, "");
				}

				// Attribute: Reaction.
				lblREAMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.REA.TotalMinimum, _objCharacter.REA.TotalMaximum, _objCharacter.REA.TotalAugmentedMaximum);
				if (_objCharacter.REA.HasModifiers)
				{
					lblREAAug.Text = string.Format("({0})", _objCharacter.REA.TotalValue);
					tipTooltip.SetToolTip(lblREAAug, _objCharacter.REA.ToolTip());
				}
				else
				{
					lblREAAug.Text = "";
					tipTooltip.SetToolTip(lblREAAug, "");
				}

				// Attribute: Strength.
				lblSTRMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.STR.TotalMinimum, _objCharacter.STR.TotalMaximum, _objCharacter.STR.TotalAugmentedMaximum);
				if (_objCharacter.STR.HasModifiers)
				{
					lblSTRAug.Text = string.Format("({0})", _objCharacter.STR.TotalValue);
					tipTooltip.SetToolTip(lblSTRAug, _objCharacter.STR.ToolTip());
				}
				else
				{
					lblSTRAug.Text = "";
					tipTooltip.SetToolTip(lblSTRAug, "");
				}

				// Attribute: Charisma.
				lblCHAMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.CHA.TotalMinimum, _objCharacter.CHA.TotalMaximum, _objCharacter.CHA.TotalAugmentedMaximum);
				if (_objCharacter.CHA.HasModifiers)
				{
					lblCHAAug.Text = string.Format("({0})", _objCharacter.CHA.TotalValue);
					tipTooltip.SetToolTip(lblCHAAug, _objCharacter.CHA.ToolTip());
				}
				else
				{
					lblCHAAug.Text = "";
					tipTooltip.SetToolTip(lblCHAAug, "");
				}

				// Attribute: Intuition.
				lblINTMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.INT.TotalMinimum, _objCharacter.INT.TotalMaximum, _objCharacter.INT.TotalAugmentedMaximum);
				if (_objCharacter.INT.HasModifiers)
				{
					lblINTAug.Text = string.Format("({0})", _objCharacter.INT.TotalValue);
					tipTooltip.SetToolTip(lblINTAug, _objCharacter.INT.ToolTip());
				}
				else
				{
					lblINTAug.Text = "";
					tipTooltip.SetToolTip(lblINTAug, "");
				}

				// Attribute: Logic.
				lblLOGMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.LOG.TotalMinimum, _objCharacter.LOG.TotalMaximum, _objCharacter.LOG.TotalAugmentedMaximum);
				if (_objCharacter.LOG.HasModifiers)
				{
					lblLOGAug.Text = string.Format("({0})", _objCharacter.LOG.TotalValue);
					tipTooltip.SetToolTip(lblLOGAug, _objCharacter.LOG.ToolTip());
				}
				else
				{
					lblLOGAug.Text = "";
					tipTooltip.SetToolTip(lblLOGAug, "");
				}

				// Attribute: Willpower.
				lblWILMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.WIL.TotalMinimum, _objCharacter.WIL.TotalMaximum, _objCharacter.WIL.TotalAugmentedMaximum);
				if (_objCharacter.WIL.HasModifiers)
				{
					lblWILAug.Text = string.Format("({0})", _objCharacter.WIL.TotalValue);
					tipTooltip.SetToolTip(lblWILAug, _objCharacter.WIL.ToolTip());
				}
				else
				{
					lblWILAug.Text = "";
					tipTooltip.SetToolTip(lblWILAug, "");
				}

				// Attribute: Edge.
				lblEDGMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.EDG.TotalMinimum, _objCharacter.EDG.TotalMaximum, _objCharacter.EDG.TotalAugmentedMaximum);
				if (_objCharacter.EDG.HasModifiers)
				{
					lblEDGAug.Text = string.Format("({0})", _objCharacter.EDG.TotalValue);
					tipTooltip.SetToolTip(lblEDGAug, _objCharacter.EDG.ToolTip());
				}
				else
				{
					lblEDGAug.Text = "";
					tipTooltip.SetToolTip(lblEDGAug, "");
				}

				// Attribute: Magic.
				lblMAGMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.MAG.TotalMinimum, _objCharacter.MAG.TotalMaximum, _objCharacter.MAG.TotalAugmentedMaximum);
				if (_objCharacter.MAG.HasModifiers)
				{
					lblMAGAug.Text = string.Format("({0})", _objCharacter.MAG.TotalValue);
					tipTooltip.SetToolTip(lblMAGAug, _objCharacter.MAG.ToolTip());
				}
				else
				{
					lblMAGAug.Text = "";
					tipTooltip.SetToolTip(lblMAGAug, "");
				}

				// Attribute: Resonance.
				lblRESMetatype.Text = string.Format("{0} / {1} ({2})", _objCharacter.RES.TotalMinimum, _objCharacter.RES.TotalMaximum, _objCharacter.RES.TotalAugmentedMaximum);
				if (_objCharacter.RES.HasModifiers)
				{
					lblRESAug.Text = string.Format("({0})", _objCharacter.RES.TotalValue);
					tipTooltip.SetToolTip(lblRESAug, _objCharacter.RES.ToolTip());
				}
				else
				{
					lblRESAug.Text = "";
					tipTooltip.SetToolTip(lblRESAug, "");
				}

				// Update the MAG pseudo-Attributes if applicable.
				int intCharacterMAG = _objCharacter.MAG.TotalValue;
				if (_objCharacter.AdeptEnabled && _objCharacter.MagicianEnabled)
				{
					_objCharacter.MAGAdept = Convert.ToInt32(_objCharacter.MAG.TotalValue - nudMysticAdeptMAGMagician.Value);
					lblMysticAdeptMAGAdept.Text = _objCharacter.MAGAdept.ToString();
					intCharacterMAG = _objCharacter.MAGMagician;
				}

				// Update the maximum Force for all Spirits.
				foreach (SpiritControl objSpiritControl in panSpirits.Controls)
				{
					if (_objOptions.SpiritForceBasedOnTotalMAG)
						objSpiritControl.ForceMaximum = _objCharacter.MAG.TotalValue * 2;
					else
					{
						int intLocalMAG = intCharacterMAG;
						if (intLocalMAG == 0)
							intLocalMAG = 1;

						objSpiritControl.ForceMaximum = intLocalMAG * 2;
					}
					objSpiritControl.RebuildSpiritList(_objCharacter.MagicTradition);
				}

				// Update Adept Powers.
				int intMAG = _objCharacter.MAG.TotalValue;
				foreach (PowerControl objPowerControl in panPowers.Controls)
				{
					// Maximum Power Level for Mystic Adepts is based on their total MAG.
					objPowerControl.RefreshMaximum(_objCharacter.MAG.TotalValue);
					objPowerControl.RefreshTooltip();
				}

				// Update the Drain Attribute Value.
				if (_objCharacter.MAGEnabled && lblDrainAttributes.Text != "")
				{
					try
					{
						XmlDocument objXmlDocument = new XmlDocument();
						XPathNavigator nav = objXmlDocument.CreateNavigator();
						string strDrain = lblDrainAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), _objCharacter.BOD.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), _objCharacter.AGI.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), _objCharacter.REA.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), _objCharacter.STR.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), _objCharacter.CHA.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), _objCharacter.INT.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), _objCharacter.LOG.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), _objCharacter.WIL.TotalValue.ToString());
						strDrain = strDrain.Replace(LanguageManager.Instance.GetString("String_AttributeMAGShort"), _objCharacter.MAG.TotalValue.ToString());
						XPathExpression xprDrain = nav.Compile(strDrain);
						int intDrain = Convert.ToInt32(nav.Evaluate(xprDrain).ToString());
						intDrain += _objImprovementManager.ValueOf(Improvement.ImprovementType.DrainResistance);
						lblDrainAttributesValue.Text = intDrain.ToString();
					}
					catch
					{
					}
				}

				// Update the maximum Force for all Sprites.
				foreach (SpiritControl objSpiritControl in panSprites.Controls)
				{
					objSpiritControl.ForceMaximum = _objCharacter.RES.TotalValue * 2;
					objSpiritControl.RebuildSpiritList(_objCharacter.TechnomancerStream);
				}

				// Update the maximum value for the Complex Form Rating NUD when the alternate Complex Form cost option is on.
				if (_objCharacter.Options.AlternateComplexFormCost)
					nudComplexFormRating.Maximum = _objCharacter.RES.TotalValue * 2;

				// Update the Fading Attribute Value.
				if (_objCharacter.RESEnabled && lblFadingAttributes.Text != "")
				{
					try
					{
						XmlDocument objXmlDocument = new XmlDocument();
						XPathNavigator nav = objXmlDocument.CreateNavigator();
						string strFading = lblFadingAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), _objCharacter.BOD.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), _objCharacter.AGI.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), _objCharacter.REA.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), _objCharacter.STR.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), _objCharacter.CHA.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), _objCharacter.INT.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), _objCharacter.LOG.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), _objCharacter.WIL.TotalValue.ToString());
						strFading = strFading.Replace(LanguageManager.Instance.GetString("String_AttributeRESShort"), _objCharacter.RES.TotalValue.ToString());
						XPathExpression xprFading = nav.Compile(strFading);
						int intFading = Convert.ToInt32(nav.Evaluate(xprFading).ToString());
						intFading += _objImprovementManager.ValueOf(Improvement.ImprovementType.FadingResistance);
						lblFadingAttributesValue.Text = intFading.ToString();

						strTip = lblFadingAttributes.Text.Replace(LanguageManager.Instance.GetString("String_AttributeBODShort"), LanguageManager.Instance.GetString("String_AttributeBODShort") + " (" + _objCharacter.BOD.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeAGIShort"), LanguageManager.Instance.GetString("String_AttributeAGIShort") + " (" + _objCharacter.AGI.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeREAShort"), LanguageManager.Instance.GetString("String_AttributeREAShort") + " (" + _objCharacter.REA.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeSTRShort"), LanguageManager.Instance.GetString("String_AttributeSTRShort") + " (" + _objCharacter.STR.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeCHAShort"), LanguageManager.Instance.GetString("String_AttributeCHAShort") + " (" + _objCharacter.CHA.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeINTShort"), LanguageManager.Instance.GetString("String_AttributeINTShort") + " (" + _objCharacter.INT.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeLOGShort"), LanguageManager.Instance.GetString("String_AttributeLOGShort") + " (" + _objCharacter.LOG.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeWILShort"), LanguageManager.Instance.GetString("String_AttributeWILShort") + " (" + _objCharacter.WIL.TotalValue.ToString() + ")");
						strTip = strTip.Replace(LanguageManager.Instance.GetString("String_AttributeRESShort"), LanguageManager.Instance.GetString("String_AttributeRESShort") + " (" + _objCharacter.RES.TotalValue.ToString() + ")");
						tipTooltip.SetToolTip(lblFadingAttributesValue, strTip);
					}
					catch
					{
					}
				}

				// Update Living Persona values.
				if (_objCharacter.RESEnabled)
				{
					string strPersonaTip = "";
					int intFirewall = _objCharacter.WIL.TotalValue + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaFirewall);
					int intResponse = _objCharacter.INT.TotalValue + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaResponse);
					int intSignal = Convert.ToInt32(Math.Ceiling((Convert.ToDecimal(_objCharacter.RES.TotalValue, GlobalOptions.Instance.CultureInfo) / 2))) + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSignal);
					int intSystem = _objCharacter.LOG.TotalValue + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSystem);
					int intBiofeedback = _objCharacter.CHA.TotalValue + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaBiofeedback);

					// If this is a Technocritter, their Matrix Attributes always equal their RES.
					if (_objCharacter.MetatypeCategory == "Technocritters")
					{
						intFirewall = _objCharacter.RES.TotalValue;
						intSystem = _objCharacter.RES.TotalValue;
						intResponse = _objCharacter.RES.TotalValue;
						intSignal = _objCharacter.RES.TotalValue;
						intBiofeedback = _objCharacter.RES.TotalValue;
					}

					// Make sure none of the Attributes exceed the Technomancer's RES.
					intFirewall = Math.Min(intFirewall, _objCharacter.RES.TotalValue);
					intResponse = Math.Min(intResponse, _objCharacter.RES.TotalValue);
					intSignal = Math.Min(intSignal, _objCharacter.RES.TotalValue);
					intSystem = Math.Min(intSystem, _objCharacter.RES.TotalValue);

					lblLivingPersonaFirewall.Text = intFirewall.ToString();
					if (_objCharacter.MetatypeCategory != "Technocritters")
						strPersonaTip = "WIL (" + _objCharacter.WIL.TotalValue.ToString() + ")";
					else
						strPersonaTip = "RES (" + _objCharacter.RES.TotalValue.ToString() + ")";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaFirewall) != 0)
						strPersonaTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaFirewall).ToString() + ")";
					tipTooltip.SetToolTip(lblLivingPersonaFirewall, strPersonaTip);

					lblLivingPersonaResponse.Text = intResponse.ToString();
					if (_objCharacter.MetatypeCategory != "Technocritters")
						strPersonaTip = "INT (" + _objCharacter.INT.TotalValue.ToString() + ")";
					else
						strPersonaTip = "RES (" + _objCharacter.RES.TotalValue.ToString() + ")";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaResponse) != 0)
						strPersonaTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaResponse).ToString() + ")";
					tipTooltip.SetToolTip(lblLivingPersonaResponse, strPersonaTip);

					lblLivingPersonaSignal.Text = intSignal.ToString();
					if (_objCharacter.MetatypeCategory != "Technocritters")
						strPersonaTip = "RES/2 (" + Convert.ToInt32(Math.Ceiling((Convert.ToDecimal(_objCharacter.RES.TotalValue, GlobalOptions.Instance.CultureInfo) / 2))).ToString() + ")";
					else
						strPersonaTip = "RES (" + _objCharacter.RES.TotalValue.ToString() + ")";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSignal) != 0)
						strPersonaTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSignal).ToString() + ")";
					tipTooltip.SetToolTip(lblLivingPersonaSignal, strPersonaTip);

					lblLivingPersonaSystem.Text = intSystem.ToString();
					if (_objCharacter.MetatypeCategory != "Technocritters")
						strPersonaTip = "LOG (" + _objCharacter.LOG.TotalValue.ToString() + ")";
					else
						strPersonaTip = "RES (" + _objCharacter.RES.TotalValue.ToString() + ")";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSystem) != 0)
						strPersonaTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaSystem).ToString() + ")";
					tipTooltip.SetToolTip(lblLivingPersonaSystem, strPersonaTip);

					lblLivingPersonaBiofeedbackFilter.Text = intBiofeedback.ToString();
					if (_objCharacter.MetatypeCategory != "Technocritters")
						strPersonaTip = "CHA (" + _objCharacter.CHA.TotalValue.ToString() + ")";
					else
						strPersonaTip = "RES (" + _objCharacter.RES.TotalValue.ToString() + ")";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaBiofeedback) != 0)
						strPersonaTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaBiofeedback).ToString() + ")";
					tipTooltip.SetToolTip(lblLivingPersonaBiofeedbackFilter, strPersonaTip);
				}

				// Initiative.
				lblINI.Text = _objCharacter.Initiative;
				string strInit = "REA (" + _objCharacter.REA.Value.ToString() + ") + INT (" + _objCharacter.INT.Value.ToString() + ")";
				if (_objCharacter.INI.AttributeModifiers > 0 || _objImprovementManager.ValueOf(Improvement.ImprovementType.Initiative) > 0 || _objCharacter.INT.AttributeModifiers > 0 || _objCharacter.REA.AttributeModifiers > 0)
					strInit += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + (_objCharacter.INI.AttributeModifiers + _objImprovementManager.ValueOf(Improvement.ImprovementType.Initiative) + _objCharacter.INT.AttributeModifiers + _objCharacter.REA.AttributeModifiers).ToString() + ")";
				tipTooltip.SetToolTip(lblINI, strInit);

				// Initiative Passes.
				lblIP.Text = _objCharacter.InitiativePasses;
				string strIPTip = "";
				strIPTip = "1";
				if (Convert.ToInt32(_objImprovementManager.ValueOf(Improvement.ImprovementType.InitiativePass)) > 0)
					strIPTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.InitiativePass).ToString() + ")";
				tipTooltip.SetToolTip(lblIP, strIPTip);

				// Astral Initiative.
				if (_objCharacter.MAGEnabled)
				{
					lblAstralINI.Text = _objCharacter.AstralInitiative;
					lblAstralIP.Text = _objCharacter.AstralInitiativePasses;
					tipTooltip.SetToolTip(lblAstralINI, "INT (" + _objCharacter.INT.TotalValue.ToString() + ") x 2");
					tipTooltip.SetToolTip(lblAstralIP, "3");
				}

				// Matrix Initiative.
				// This is always calculated since characters can have a Matrix Initiative without actually being a Technomancer.
				int intCommlinkResponse = 0;

				// Retrieve the highest Response in case the Character has more than 1 Commlink.
				foreach (Commlink objCommlink in _objCharacter.Gear.OfType<Commlink>())
				{
					if (objCommlink.TotalDeviceRating > intCommlinkResponse)
                        intCommlinkResponse = objCommlink.TotalDeviceRating;
				}

				lblMatrixINI.Text = _objCharacter.MatrixInitiative;
				lblMatrixIP.Text = _objCharacter.MatrixInitiativePasses;
				if (!_objCharacter.TechnomancerEnabled)
				{
					tipTooltip.SetToolTip(lblMatrixINI, "INT (" + _objCharacter.INT.TotalValue.ToString() + ") + " + LanguageManager.Instance.GetString("Tip_CommlinkResponse") + " (" + intCommlinkResponse.ToString() + ")");
					strIPTip = "1";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.MatrixInitiativePass) > 0)
						strIPTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.MatrixInitiativePass).ToString() + ")";
					tipTooltip.SetToolTip(lblMatrixIP, strIPTip);
				}
				else
				{
					strInit = "INT x 2 (" + _objCharacter.INT.TotalValue.ToString() + ") + 1";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaResponse) > 0)
						strInit += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LivingPersonaResponse).ToString() + ")";
					tipTooltip.SetToolTip(lblMatrixINI, strInit);
					strIPTip = "3";
					if (_objImprovementManager.ValueOf(Improvement.ImprovementType.MatrixInitiativePass) > 0)
						strIPTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.MatrixInitiativePass).ToString() + ")";
					tipTooltip.SetToolTip(lblMatrixIP, strIPTip);
				}

				if (_objCharacter.AdeptEnabled)
					CalculatePowerPoints();
				if ((_objCharacter.Metatype == "Free Spirit" && !_objCharacter.IsCritter) || _objCharacter.MetatypeCategory.EndsWith("Spirits"))
				{
					lblCritterPowerPointsLabel.Visible = true;
					lblCritterPowerPoints.Visible = true;
					lblCritterPowerPoints.Text = _objController.CalculateFreeSpiritPowerPoints();
				}
				if (_objCharacter.IsFreeSprite)
				{
					lblCritterPowerPointsLabel.Visible = true;
					lblCritterPowerPoints.Visible = true;
					lblCritterPowerPoints.Text = _objController.CalculateFreeSpritePowerPoints();
				}

				// Update the Nuyen and Karma for the character.
				tssNuyen.Text = String.Format("{0:###,###,##0¥}", _objCharacter.Nuyen);
				lblRemainingNuyen.Text = String.Format("{0:###,###,##0¥}", _objCharacter.Nuyen);
				tssKarma.Text = _objCharacter.Karma.ToString();

				PopulateExpenseList();

				// Movement.
				lblMovement.Text = _objCharacter.Movement;
				lblSwim.Text = _objCharacter.Swim;
				lblFly.Text = _objCharacter.Fly;

				// Special Attribute-Only Test.
				lblComposure.Text = _objCharacter.Composure.ToString();
				strTip = "WIL (" + _objCharacter.WIL.TotalValue.ToString() + ") + CHA (" + _objCharacter.CHA.TotalValue.ToString() + ")";
				tipTooltip.SetToolTip(lblComposure, strTip);
				lblJudgeIntentions.Text = _objCharacter.JudgeIntentions.ToString();
				strTip = "INT (" + _objCharacter.INT.TotalValue.ToString() + ") + CHA (" + _objCharacter.CHA.TotalValue.ToString() + ")";
				if (_objImprovementManager.ValueOf(Improvement.ImprovementType.JudgeIntentions) != 0)
					strTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.JudgeIntentions).ToString() + ")";
				tipTooltip.SetToolTip(lblJudgeIntentions, strTip);
				lblLiftCarry.Text = _objCharacter.LiftAndCarry.ToString();
				strTip = "STR (" + _objCharacter.STR.TotalValue.ToString() + ") + BOD (" + _objCharacter.BOD.TotalValue.ToString() + ")";
				if (_objImprovementManager.ValueOf(Improvement.ImprovementType.LiftAndCarry) != 0)
					strTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.LiftAndCarry).ToString() + ")";
				strTip += "\n" + LanguageManager.Instance.GetString("Tip_LiftAndCarry").Replace("{0}", (_objCharacter.STR.TotalValue * 15).ToString()).Replace("{1}", (_objCharacter.STR.TotalValue * 10).ToString());
				tipTooltip.SetToolTip(lblLiftCarry, strTip);
				lblMemory.Text = _objCharacter.Memory.ToString();
				strTip = "LOG (" + _objCharacter.LOG.TotalValue.ToString() + ") + WIL (" + _objCharacter.WIL.TotalValue.ToString() + ")";
				if (_objImprovementManager.ValueOf(Improvement.ImprovementType.Memory) != 0)
					strTip += " + " + LanguageManager.Instance.GetString("Tip_Modifiers") + " (" + _objImprovementManager.ValueOf(Improvement.ImprovementType.Memory).ToString() + ")";
				tipTooltip.SetToolTip(lblMemory, strTip);

				// Update Initiate Grade if applicable.
				if (_objCharacter.MAGEnabled)
					lblInitiateGrade.Text = _objCharacter.InitiateGrade.ToString();
				if (_objCharacter.RESEnabled)
					lblInitiateGrade.Text = _objCharacter.SubmersionGrade.ToString();

				// Career Karma.
				lblCareerKarma.Text = String.Format("{0:###,###,##0}", _objCharacter.CareerKarma);

				// Career Nuyen.
				lblCareerNuyen.Text = String.Format("{0:###,###,##0¥}", _objCharacter.CareerNuyen);

				// Update A.I. Attributes.
				if (_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients")
				{
					lblRating.Text = _objCharacter.Rating.ToString();
					lblSystem.Text = _objCharacter.System.ToString();
					lblFirewall.Text = _objCharacter.Firewall.ToString();
				}

				// Update Damage Resistance Pool.
				lblCMDamageResistancePool.Text = (_objCharacter.BOD.TotalValue + _objImprovementManager.ValueOf(Improvement.ImprovementType.DamageResistance)).ToString();

				// Update EDG Remaining Info on the Condition Monitor tab.
				string strEDG = _objCharacter.EDG.TotalValue.ToString() + " " + LanguageManager.Instance.GetString("String_Of") + " " + _objCharacter.EDG.Value.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining");
				lblEDGInfo.Text = strEDG;

				foreach (SkillControl objSkillControl in panActiveSkills.Controls.OfType<SkillControl>())
					objSkillControl.RefreshControl();

				foreach (SkillControl objSkillControl in panKnowledgeSkills.Controls.OfType<SkillControl>())
					objSkillControl.RefreshControl();

				_blnSkipUpdate = false;

				_objImprovementManager.Commit();

				// If the Viewer window is open for this character, call its RefreshView method which updates it asynchronously
				if (_objCharacter.PrintWindow != null)
					_objCharacter.PrintWindow.RefreshView();

				cmdQuickenSpell.Visible = _objCharacter.HasImprovement(Improvement.ImprovementType.QuickeningMetamagic, true);
			}
			RefreshImprovements();
			UpdateReputation();
		}

		/// <summary>
		/// Refresh the information for the currently displayed piece of Cyberware.
		/// </summary>
		public void RefreshSelectedCyberware()
		{
			bool blnClear = false;
			try
			{
				if (treCyberware.SelectedNode.Level == 0)
					blnClear = true;
			}
			catch
			{
				blnClear = true;
			}
			if (blnClear)
			{
				lblCyberwareName.Text = "";
				lblCyberwareCategory.Text = "";
				lblCyberwareRating.Text = "";
				lblCyberwareAvail.Text = "";
				lblCyberwareCost.Text = "";
				lblCyberwareCapacity.Text = "";
				lblCyberwareEssence.Text = "";
				lblCyberwareSource.Text = "";
				tipTooltip.SetToolTip(lblCyberwareSource, null);
				return;
			}

			// Locate the selected piece of Cyberware.
			bool blnFound = false;
			Cyberware objCyberware = _objFunctions.FindCyberware(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware);
			if (objCyberware != null)
				blnFound = true;

			if (blnFound)
			{
				_blnSkipRefresh = true;
				lblCyberwareName.Text = objCyberware.DisplayNameShort;
				lblCyberwareCategory.Text = objCyberware.DisplayCategory;
				string strBook = _objOptions.LanguageBookShort(objCyberware.Source);
				string strPage = objCyberware.Page;
				lblCyberwareSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblCyberwareSource, _objOptions.LanguageBookLong(objCyberware.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objCyberware.Page);
				lblCyberwareRating.Text = objCyberware.Rating.ToString();

				lblCyberwareGrade.Text = objCyberware.Grade.DisplayName;

				_blnSkipRefresh = false;

				lblCyberwareAvail.Text = objCyberware.TotalAvail;
				lblCyberwareCost.Text = String.Format("{0:###,###,##0¥}", objCyberware.TotalCost);
				lblCyberwareCapacity.Text = objCyberware.CalculatedCapacity + " (" + objCyberware.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				lblCyberwareEssence.Text = objCyberware.CalculatedESS.ToString();
				UpdateCharacterInfo();
			}
			else
			{
				// Locate the selected piece of Gear.
				Cyberware objFoundCyberware = new Cyberware(_objCharacter);
				Gear objGear = _objFunctions.FindCyberwareGear(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware, out objFoundCyberware);

				_blnSkipRefresh = true;
				lblCyberwareName.Text = objGear.DisplayNameShort;
				lblCyberwareCategory.Text = objGear.DisplayCategory;
				lblCyberwareAvail.Text = objGear.TotalAvail(true);
				lblCyberwareCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
				lblCyberwareCapacity.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				lblCyberwareEssence.Text = "0";
				lblCyberwareGrade.Text = "";
				lblCyberwareRating.Text = objGear.Rating.ToString();
				string strBook = _objOptions.LanguageBookShort(objGear.Source);
				string strPage = objGear.Page;
				lblCyberwareSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblCyberwareSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);
			}
		}

		/// <summary>
		/// Refresh the information for the currently displayed Weapon.
		/// </summary>
		public void RefreshSelectedWeapon()
		{
			bool blnClear = false;
			try
			{
				if (treWeapons.SelectedNode.Level == 0)
					blnClear = true;
			}
			catch
			{
				blnClear = true;
			}
			if (blnClear)
			{
				lblWeaponName.Text = "";
				lblWeaponCategory.Text = "";
				lblWeaponAvail.Text = "";
				lblWeaponCost.Text = "";
				lblWeaponConceal.Text = "";
				lblWeaponDamage.Text = "";
				lblWeaponRC.Text = "";
				lblWeaponAP.Text = "";
				lblWeaponReach.Text = "";
				lblWeaponMode.Text = "";
				lblWeaponAmmo.Text = "";
				lblWeaponSource.Text = "";
				cboWeaponAmmo.Enabled = false;
				tipTooltip.SetToolTip(lblWeaponSource, null);
				chkWeaponAccessoryInstalled.Enabled = false;
				chkIncludedInWeapon.Enabled = false;
				chkIncludedInWeapon.Checked = false;

				// Disable the fire button.
				cmdFireWeapon.Enabled = false;
				cmdReloadWeapon.Enabled = false;
				cmdWeaponBuyAmmo.Enabled = false;
				cboWeaponAmmo.Enabled = false;

				// Hide Weapon Ranges.
				lblWeaponRangeShort.Text = "";
				lblWeaponRangeMedium.Text = "";
				lblWeaponRangeLong.Text = "";
				lblWeaponRangeExtreme.Text = "";
				return;
			}

			lblWeaponDicePool.Text = "";
			tipTooltip.SetToolTip(lblWeaponDicePool, "");
			cmdWeaponMoveToVehicle.Enabled = false;

			// Locate the selected Weapon.
			if (treWeapons.SelectedNode.Level == 1)
			{
				Weapon objWeapon = _objFunctions.FindWeapon(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
				if (objWeapon == null)
					return;

				_blnSkipRefresh = true;
				lblWeaponName.Text = objWeapon.DisplayNameShort;
				lblWeaponCategory.Text = objWeapon.DisplayCategory;
				string strBook = _objOptions.LanguageBookShort(objWeapon.Source);
				string strPage = objWeapon.Page;
				lblWeaponSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblWeaponSource, _objOptions.LanguageBookLong(objWeapon.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objWeapon.Page);
				chkWeaponAccessoryInstalled.Enabled = false;
				chkIncludedInWeapon.Enabled = false;
				chkIncludedInWeapon.Checked = false;

				// Do not allow Cyberware of Gear Weapons to be moved.
				if (!objWeapon.Category.StartsWith("Cyberware") && objWeapon.Category != "Gear")
				{
					if (_objCharacter.Vehicles.Count > 0)
						cmdWeaponMoveToVehicle.Enabled = true;
					else
						cmdWeaponMoveToVehicle.Enabled = false;
				}

				// Enable the fire button if the Weapon is Ranged.
				if (objWeapon.WeaponType == "Ranged" || (objWeapon.WeaponType == "Melee" && objWeapon.Ammo != "0"))
				{
					cmdFireWeapon.Enabled = true;
					cmdReloadWeapon.Enabled = true;
					cmdWeaponBuyAmmo.Enabled = true;
					lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
					//lblWeaponAmmoType.Text = "External Source";

					cmsAmmoSingleShot.Enabled = objWeapon.AllowMode("SS") || objWeapon.AllowMode("SA");
					cmsAmmoShortBurst.Enabled = objWeapon.AllowMode("BF") || objWeapon.AllowMode("FA");
					cmsAmmoLongBurst.Enabled = objWeapon.AllowMode("FA");
					cmsAmmoFullBurst.Enabled = objWeapon.AllowMode("FA");
					cmsAmmoSuppressiveFire.Enabled = objWeapon.AllowMode("FA");

					// Melee Weapons with Ammo are considered to be Single Shot.
					if (objWeapon.WeaponType == "Melee" && objWeapon.Ammo != "0")
						cmsAmmoSingleShot.Enabled = true;

					if (cmsAmmoFullBurst.Enabled)
						cmsAmmoFullBurst.Text = LanguageManager.Instance.GetString("String_FullBurst").Replace("{0}", objWeapon.FullBurst.ToString());
					if (cmsAmmoSuppressiveFire.Enabled)
						cmsAmmoSuppressiveFire.Text = LanguageManager.Instance.GetString("String_SuppressiveFire").Replace("{0}", objWeapon.Suppressive.ToString());

					List<ListItem> lstAmmo = new List<ListItem>();
					int intCurrentSlot = objWeapon.ActiveAmmoSlot;
					for (int i = 1; i <= objWeapon.AmmoSlots; i++)
					{
						Gear objGear = new Gear(_objCharacter);
						ListItem objAmmo = new ListItem();
						objWeapon.ActiveAmmoSlot = i;
						objGear = _objFunctions.FindGear(objWeapon.AmmoLoaded, _objCharacter.Gear);
						objAmmo.Value = i.ToString();

						string strPlugins = "";
						if (objGear != null)
						{
							foreach (Gear objChild in objGear.Children)
							{
								strPlugins += objChild.DisplayNameShort + ", ";
							}
						}
						// Remove the trailing comma.
						if (strPlugins != "")
							strPlugins = strPlugins.Substring(0, strPlugins.Length - 2);

						if (objGear == null)
						{
							if (objWeapon.AmmoRemaining == 0)
								objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_Empty");
							else
								objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_ExternalSource");
						}
						else
							objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + objGear.DisplayNameShort;
						if (strPlugins != "")
							objAmmo.Name += " [" + strPlugins + "]";
						lstAmmo.Add(objAmmo);
					}
					_blnSkipRefresh = true;
					objWeapon.ActiveAmmoSlot = intCurrentSlot;
					cboWeaponAmmo.Enabled = true;
					cboWeaponAmmo.ValueMember = "Value";
					cboWeaponAmmo.DisplayMember = "Name";
					cboWeaponAmmo.DataSource = lstAmmo;
					cboWeaponAmmo.SelectedValue = objWeapon.ActiveAmmoSlot.ToString();
					if (cboWeaponAmmo.SelectedIndex == -1)
						cboWeaponAmmo.SelectedIndex = 0;
					_blnSkipRefresh = false;
				}
				else
				{
					cmdFireWeapon.Enabled = false;
					cmdReloadWeapon.Enabled = false;
					cmdWeaponBuyAmmo.Enabled = false;
					lblWeaponAmmoRemaining.Text = "";
					cboWeaponAmmo.Enabled = false;
				}

				// If this is a Cyberweapon, grab the STR of the limb.
				int intUseSTR = 0;
				if (objWeapon.Category.StartsWith("Cyberware"))
				{
					foreach (Cyberware objCyberware in _objCharacter.Cyberware)
					{
						foreach (Cyberware objPlugin in objCyberware.Children)
						{
							if (objPlugin.WeaponID == objWeapon.InternalId)
							{
								intUseSTR = objCyberware.TotalStrength;
								break;
							}
						}
					}
				}

				// Show the Weapon Ranges.
				lblWeaponRangeShort.Text = objWeapon.RangeShort;
				lblWeaponRangeMedium.Text = objWeapon.RangeMedium;
				lblWeaponRangeLong.Text = objWeapon.RangeLong;
				lblWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

				_blnSkipRefresh = false;

				lblWeaponAvail.Text = objWeapon.TotalAvail;
				lblWeaponCost.Text = String.Format("{0:###,###,##0¥}", objWeapon.TotalCost);
				lblWeaponConceal.Text = objWeapon.CalculatedConcealability();
				lblWeaponDamage.Text = objWeapon.CalculatedDamage(intUseSTR);
				lblWeaponRC.Text = objWeapon.TotalRC;
				lblWeaponAP.Text = objWeapon.TotalAP;
				lblWeaponReach.Text = objWeapon.TotalReach.ToString();
				lblWeaponMode.Text = objWeapon.CalculatedMode;
				lblWeaponAmmo.Text = objWeapon.CalculatedAmmo();
				lblWeaponSlots.Text = "6 (" + objWeapon.SlotsRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				lblWeaponDicePool.Text = objWeapon.DicePool;
				tipTooltip.SetToolTip(lblWeaponDicePool, objWeapon.DicePoolTooltip);

				UpdateCharacterInfo();
			}
			else
			{
				// See if this is an Underbarrel Weapon.
				bool blnUnderbarrel = false;
				Weapon objWeapon = new Weapon(_objCharacter);
				foreach (Weapon objCharacterWeapon in _objCharacter.Weapons)
				{
					if (objCharacterWeapon.UnderbarrelWeapons.Count > 0)
					{
						foreach (Weapon objUnderbarrelWeapon in objCharacterWeapon.UnderbarrelWeapons)
						{
							if (objUnderbarrelWeapon.InternalId == treWeapons.SelectedNode.Tag.ToString())
							{
								objWeapon = objUnderbarrelWeapon;
								blnUnderbarrel = true;
								break;
							}
						}
					}
				}

				if (blnUnderbarrel)
				{
					cmdFireWeapon.Enabled = true;
					cmdReloadWeapon.Enabled = true;
					cmdWeaponBuyAmmo.Enabled = true;

					lblWeaponAvail.Text = objWeapon.TotalAvail;
					lblWeaponCost.Text = String.Format("{0:###,###,##0¥}", objWeapon.TotalCost);
					lblWeaponConceal.Text = "+4";
					lblWeaponDamage.Text = objWeapon.CalculatedDamage();
					lblWeaponRC.Text = objWeapon.TotalRC;
					lblWeaponAP.Text = objWeapon.TotalAP;
					lblWeaponReach.Text = objWeapon.TotalReach.ToString();
					lblWeaponMode.Text = objWeapon.CalculatedMode;
					lblWeaponAmmo.Text = objWeapon.CalculatedAmmo();
					lblWeaponSlots.Text = "6 (" + objWeapon.SlotsRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
					lblWeaponDicePool.Text = objWeapon.DicePool;
					tipTooltip.SetToolTip(lblWeaponDicePool, objWeapon.DicePoolTooltip);
					UpdateCharacterInfo();

					cmsAmmoSingleShot.Enabled = objWeapon.AllowMode("SS") || objWeapon.AllowMode("SA");
					cmsAmmoShortBurst.Enabled = objWeapon.AllowMode("BF") || objWeapon.AllowMode("FA");
					cmsAmmoLongBurst.Enabled = objWeapon.AllowMode("FA");
					cmsAmmoFullBurst.Enabled = objWeapon.AllowMode("FA");
					cmsAmmoSuppressiveFire.Enabled = objWeapon.AllowMode("FA");

					// Melee Weapons with Ammo are considered to be Single Shot.
					if (objWeapon.WeaponType == "Melee" && objWeapon.Ammo != "0")
						cmsAmmoSingleShot.Enabled = true;

					if (cmsAmmoFullBurst.Enabled)
						cmsAmmoFullBurst.Text = LanguageManager.Instance.GetString("String_FullBurst").Replace("{0}", objWeapon.FullBurst.ToString());
					if (cmsAmmoSuppressiveFire.Enabled)
						cmsAmmoSuppressiveFire.Text = LanguageManager.Instance.GetString("String_SuppressiveFire").Replace("{0}", objWeapon.Suppressive.ToString());

					List<ListItem> lstAmmo = new List<ListItem>();
					int intCurrentSlot = objWeapon.ActiveAmmoSlot;
					for (int i = 1; i <= objWeapon.AmmoSlots; i++)
					{
						Gear objGear = new Gear(_objCharacter);
						ListItem objAmmo = new ListItem();
						objWeapon.ActiveAmmoSlot = i;
						objGear = _objFunctions.FindGear(objWeapon.AmmoLoaded, _objCharacter.Gear);
						objAmmo.Value = i.ToString();

						string strPlugins = "";
						if (objGear != null)
						{
							foreach (Gear objChild in objGear.Children)
							{
								strPlugins += objChild.DisplayNameShort + ", ";
							}
						}
						// Remove the trailing comma.
						if (strPlugins != string.Empty)
							strPlugins = strPlugins.Substring(0, strPlugins.Length - 2);

						if (objGear == null)
							objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_Empty");
						else
							objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + objGear.DisplayNameShort;
						if (strPlugins != "")
							objAmmo.Name += " [" + strPlugins + "]";
						lstAmmo.Add(objAmmo);
					}
					_blnSkipRefresh = true;
					chkWeaponAccessoryInstalled.Enabled = true;
					chkWeaponAccessoryInstalled.Checked = objWeapon.Installed;
					chkIncludedInWeapon.Enabled = false;
					chkIncludedInWeapon.Checked = objWeapon.IncludedInWeapon;
					objWeapon.ActiveAmmoSlot = intCurrentSlot;
					cboWeaponAmmo.Enabled = true;
					cboWeaponAmmo.ValueMember = "Value";
					cboWeaponAmmo.DisplayMember = "Name";
					cboWeaponAmmo.DataSource = lstAmmo;
					cboWeaponAmmo.SelectedValue = objWeapon.ActiveAmmoSlot.ToString();
					if (cboWeaponAmmo.SelectedIndex == -1)
						cboWeaponAmmo.SelectedIndex = 0;

					// Show the Weapon Ranges.
					lblWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();
					lblWeaponRangeShort.Text = objWeapon.RangeShort;
					lblWeaponRangeMedium.Text = objWeapon.RangeMedium;
					lblWeaponRangeLong.Text = objWeapon.RangeLong;
					lblWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

					_blnSkipRefresh = false;
				}
				else
				{
					cmdFireWeapon.Enabled = false;
					cmdReloadWeapon.Enabled = false;
					cmdWeaponBuyAmmo.Enabled = false;
					cboWeaponAmmo.Enabled = false;

					bool blnAccessory = false;
					Weapon objSelectedWeapon = new Weapon(_objCharacter);
					WeaponAccessory objSelectedAccessory = _objFunctions.FindWeaponAccessory(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
					if (objSelectedAccessory != null)
					{
						blnAccessory = true;
						objSelectedWeapon = objSelectedAccessory.Parent;
					}

					if (blnAccessory)
					{
						lblWeaponName.Text = objSelectedAccessory.DisplayNameShort;
						lblWeaponCategory.Text = LanguageManager.Instance.GetString("String_WeaponAccessory");
						lblWeaponAvail.Text = objSelectedAccessory.TotalAvail;
						lblWeaponCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objSelectedAccessory.TotalCost));
						lblWeaponConceal.Text = objSelectedAccessory.Concealability.ToString();
						lblWeaponDamage.Text = "";
						lblWeaponRC.Text = objSelectedAccessory.RC;
						lblWeaponAP.Text = "";
						lblWeaponReach.Text = "";
						lblWeaponMode.Text = "";
						lblWeaponAmmo.Text = "";

						string[] strMounts = objSelectedAccessory.Mount.Split('/');
						string strMount = "";
						foreach (string strCurrentMount in strMounts)
						{
							if (strCurrentMount != "")
								strMount += LanguageManager.Instance.GetString("String_Mount" + strCurrentMount) + "/";
						}
						// Remove the trailing /
						if (strMount != "" && strMount.Contains('/'))
							strMount = strMount.Substring(0, strMount.Length - 1);

						lblWeaponSlots.Text = strMount;
						string strBook = _objOptions.LanguageBookShort(objSelectedAccessory.Source);
						string strPage = objSelectedAccessory.Page;
						lblWeaponSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblWeaponSource, _objOptions.LanguageBookLong(objSelectedAccessory.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSelectedAccessory.Page);
						chkWeaponAccessoryInstalled.Enabled = true;
						chkWeaponAccessoryInstalled.Checked = objSelectedAccessory.Installed;
						chkIncludedInWeapon.Enabled = _objOptions.AllowEditPartOfBaseWeapon;
						chkIncludedInWeapon.Checked = objSelectedAccessory.IncludedInWeapon;
					}
					else
					{
						bool blnMod = false;
						WeaponMod objSelectedMod = _objFunctions.FindWeaponMod(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons);
						if (objSelectedMod != null)
						{
							blnMod = true;
							objSelectedWeapon = objSelectedMod.Parent;
						}

						if (blnMod)
						{
							lblWeaponName.Text = objSelectedMod.DisplayNameShort;
							lblWeaponCategory.Text = LanguageManager.Instance.GetString("String_WeaponModification");
							lblWeaponAvail.Text = objSelectedMod.TotalAvail;
							lblWeaponCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objSelectedMod.TotalCost));
							lblWeaponConceal.Text = objSelectedMod.Concealability.ToString();
							lblWeaponDamage.Text = "";
							lblWeaponRC.Text = objSelectedMod.RC;
							lblWeaponAP.Text = "";
							lblWeaponReach.Text = "";
							lblWeaponMode.Text = "";
							lblWeaponAmmo.Text = "";
							lblWeaponSlots.Text = objSelectedMod.Slots.ToString();
							string strBook = _objOptions.LanguageBookShort(objSelectedMod.Source);
							string strPage = objSelectedMod.Page;
							lblWeaponSource.Text = strBook + " " + strPage;
							tipTooltip.SetToolTip(lblWeaponSource, _objOptions.LanguageBookLong(objSelectedMod.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSelectedMod.Page);
							chkWeaponAccessoryInstalled.Enabled = true;
							chkWeaponAccessoryInstalled.Checked = objSelectedMod.Installed;
							chkIncludedInWeapon.Enabled = _objOptions.AllowEditPartOfBaseWeapon;
							chkIncludedInWeapon.Checked = objSelectedMod.IncludedInWeapon;
						}
						else
						{
							// Find the selected Gear.
							_blnSkipRefresh = true;
							WeaponAccessory objAccessory = new WeaponAccessory(_objCharacter);
							Gear objGear = _objFunctions.FindWeaponGear(treWeapons.SelectedNode.Tag.ToString(), _objCharacter.Weapons, out objAccessory);
							lblWeaponName.Text = objGear.DisplayNameShort;
							lblWeaponCategory.Text = objGear.DisplayCategory;
							lblWeaponAvail.Text = objGear.TotalAvail(true);
							lblWeaponCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
							lblWeaponConceal.Text = "";
							lblWeaponDamage.Text = "";
							lblWeaponRC.Text = "";
							lblWeaponAP.Text = "";
							lblWeaponReach.Text = "";
							lblWeaponMode.Text = "";
							lblWeaponAmmo.Text = "";
							lblWeaponSlots.Text = "";
							string strBook = _objOptions.LanguageBookShort(objGear.Source);
							string strPage = objGear.Page;
							lblWeaponSource.Text = strBook + " " + strPage;
							tipTooltip.SetToolTip(lblWeaponSource, _objOptions.BookFromCode(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);
							chkWeaponAccessoryInstalled.Enabled = true;
							chkWeaponAccessoryInstalled.Checked = objGear.Equipped;
							chkIncludedInWeapon.Enabled = false;
							chkIncludedInWeapon.Checked = false;
							_blnSkipRefresh = true;
						}
					}

					// Show the Weapon Ranges.
					lblWeaponRangeShort.Text = objSelectedWeapon.RangeShort;
					lblWeaponRangeMedium.Text = objSelectedWeapon.RangeMedium;
					lblWeaponRangeLong.Text = objSelectedWeapon.RangeLong;
					lblWeaponRangeExtreme.Text = objSelectedWeapon.RangeExtreme;
				}
			}
		}

		/// <summary>
		/// Refresh the information for the currently displayed Armor.
		/// </summary>
		public void RefreshSelectedArmor()
		{
			if (treArmor.SelectedNode.Level == 0)
			{
				lblArmorEquipped.Text = "";
				foreach (Armor objArmor in _objCharacter.Armor)
				{
					if (objArmor.Equipped && (objArmor.Location == treArmor.SelectedNode.Text || objArmor.Location == string.Empty && treArmor.SelectedNode == treArmor.Nodes[0]))
						lblArmorEquipped.Text += objArmor.DisplayName + " (" + objArmor.TotalArmor.ToString() +  ")\n";
				}
				if (lblArmorEquipped.Text == string.Empty)
					lblArmorEquipped.Text = LanguageManager.Instance.GetString("String_None");
				
				lblArmorEquipped.Visible = true;

				_blnSkipRefresh = true;
				chkIncludedInArmor.Enabled = false;
				chkIncludedInArmor.Checked = false;
				_blnSkipRefresh = false;
			}
			else
				lblArmorEquipped.Visible = false;

			if (treArmor.SelectedNode.Level == 1)
			{
				_blnSkipRefresh = true;

				// Loclate the selected Armor
				Armor objArmor = _objFunctions.FindArmor(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
				if (objArmor == null)
					return;

				lblArmor.Text = objArmor.TotalArmor.ToString();
				lblArmorAvail.Text = objArmor.TotalAvail;
				lblArmorCapacity.Text = objArmor.CalculatedCapacity + " (" + objArmor.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				lblArmorRating.Text = "";
				lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objArmor.TotalCost);
				string strBook = _objOptions.LanguageBookShort(objArmor.Source);
				string strPage = objArmor.Page;
				lblArmorSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblArmorSource, _objOptions.LanguageBookLong(objArmor.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objArmor.Page);
				chkArmorEquipped.Enabled = true;
				chkArmorEquipped.Checked = objArmor.Equipped;
				chkIncludedInArmor.Enabled = false;
				chkIncludedInArmor.Checked = false;

				_blnSkipRefresh = false;

				_blnSkipRefresh = true;
				chkIncludedInArmor.Enabled = false;
				chkIncludedInArmor.Checked = false;
				_blnSkipRefresh = false;
			}
			else if (treArmor.SelectedNode.Level == 2)
			{
				bool blnIsMod = false;
				Armor objSelectedArmor = new Armor(_objCharacter);
				ArmorMod objSelectedMod = _objFunctions.FindArmorMod(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor);
				if (objSelectedMod != null)
				{
					blnIsMod = true;
					objSelectedArmor = objSelectedMod.Parent;
				}

				if (blnIsMod)
				{
					lblArmor.Text = objSelectedMod.Armor.ToString();
					lblArmorAvail.Text = objSelectedMod.TotalAvail;
					if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.Standard)
						lblArmorCapacity.Text = objSelectedMod.CalculatedCapacity;
					else if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.Zero)
						lblArmorCapacity.Text = "[0]";
					else if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.PerRating)
					{
						if (objSelectedMod.Rating > 0)
							lblArmorCapacity.Text = "[" + objSelectedMod.Rating.ToString() + "]";
						else
							lblArmorCapacity.Text = "[1]";
					}
					lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objSelectedMod.TotalCost);

					string strBook = _objOptions.LanguageBookShort(objSelectedMod.Source);
					string strPage = objSelectedMod.Page;
					lblArmorSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblArmorSource, _objOptions.LanguageBookLong(objSelectedMod.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSelectedMod.Page);
					chkArmorEquipped.Enabled = true;
					chkArmorEquipped.Checked = objSelectedMod.Equipped;
					lblArmorRating.Text = objSelectedMod.Rating.ToString();

					_blnSkipRefresh = true;
					chkIncludedInArmor.Enabled = true;
					chkIncludedInArmor.Checked = objSelectedMod.IncludedInArmor;
					_blnSkipRefresh = false;
				}
				else
				{
					Gear objSelectedGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objSelectedArmor);

					lblArmor.Text = "";
					lblArmorAvail.Text = objSelectedGear.TotalAvail(true);
					if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.Standard)
						lblArmorCapacity.Text = objSelectedGear.CalculatedCapacity;
					else if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.Zero)
						lblArmorCapacity.Text = "[0]";
					else if (objSelectedArmor.CapacityDisplayStyle == CapacityStyle.PerRating)
					{
						if (objSelectedGear.Rating > 0)
							lblArmorCapacity.Text = "[" + objSelectedGear.Rating.ToString() + "]";
						else
							lblArmorCapacity.Text = "[1]";
					}
					try
					{
						lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objSelectedGear.TotalCost);
					}
					catch
					{
						lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objSelectedGear.Cost);
					}
					string strBook = _objOptions.LanguageBookShort(objSelectedGear.Source);
					string strPage = objSelectedGear.Page;
					lblArmorSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblArmorSource, _objOptions.LanguageBookLong(objSelectedGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSelectedGear.Page);
					chkArmorEquipped.Enabled = true;
					chkArmorEquipped.Checked = objSelectedGear.Equipped;
					lblArmorRating.Text = objSelectedGear.Rating.ToString();
				}
			}
			else if (treArmor.SelectedNode.Level > 2)
			{
				Armor objSelectedArmor = new Armor(_objCharacter);
				Gear objSelectedGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objSelectedArmor);

				lblArmor.Text = "";
				lblArmorAvail.Text = objSelectedGear.TotalAvail(true);
				lblArmorCapacity.Text = objSelectedGear.CalculatedArmorCapacity;
				try
				{
					lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objSelectedGear.TotalCost);
				}
				catch
				{
					lblArmorCost.Text = String.Format("{0:###,###,##0¥}", objSelectedGear.Cost);
				}
				string strBook = _objOptions.LanguageBookShort(objSelectedGear.Source);
				string strPage = objSelectedGear.Page;
				lblArmorSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblArmorSource, _objOptions.LanguageBookLong(objSelectedGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objSelectedGear.Page);
				chkArmorEquipped.Enabled = true;
				chkArmorEquipped.Checked = objSelectedGear.Equipped;
				lblArmorRating.Text = objSelectedGear.Rating.ToString();
			}
			else
			{
				lblArmor.Text = "";
				lblArmorAvail.Text = "";
				lblArmorCost.Text = "";
				lblArmorSource.Text = "";
				tipTooltip.SetToolTip(lblArmorSource, null);
				lblArmorRating.Text = "";
				chkArmorEquipped.Enabled = false;
			}
		}

		/// <summary>
		/// Refresh the information for the currently displayed Gear.
		/// </summary>
		public void RefreshSelectedGear()
		{
			bool blnClear = false;
			try
			{
				if (treGear.SelectedNode.Level == 0)
					blnClear = true;
			}
			catch
			{
				blnClear = true;
			}
			if (blnClear)
			{
				lblGearRating.Text = "";
				lblGearQty.Text = "";
				cmdGearIncreaseQty.Enabled = false;
				cmdGearReduceQty.Enabled = false;
				chkGearEquipped.Text = LanguageManager.Instance.GetString("Checkbox_Equipped");
				chkGearEquipped.Visible = false;
				chkActiveCommlink.Visible = false;
				cmdGearSplitQty.Enabled = false;
				cmdGearMergeQty.Enabled = false;
				cmdGearMoveToVehicle.Enabled = false;
				return;
			}
			cmdGearIncreaseQty.Enabled = false;
			chkGearHomeNode.Visible = false;

			if (treGear.SelectedNode.Level > 0)
			{
				Gear objGear = new Gear(_objCharacter);
				objGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);

				lblGearName.Text = objGear.DisplayNameShort;
				lblGearCategory.Text = objGear.DisplayCategory;
				lblGearAvail.Text = objGear.TotalAvail(true);
				try
				{
					lblGearCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
				}
				catch
				{
					lblGearCost.Text = objGear.Cost;
				}
				lblGearCapacity.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				string strBook = _objOptions.LanguageBookShort(objGear.Source);
				string strPage = objGear.Page;
				lblGearSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblGearSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

				if (objGear.Category == "Ammunition")
					cmdGearIncreaseQty.Enabled = true;

				if (objGear.GetType() == typeof(Commlink))
				{
					Commlink objCommlink = (Commlink)objGear;
                    lblGearResponse.Text = objCommlink.TotalDeviceRating.ToString();
					_blnSkipRefresh = true;
					chkActiveCommlink.Checked = objCommlink.IsActive;
					_blnSkipRefresh = false;

					if (objCommlink.Category != "Commlink Upgrade")
						chkActiveCommlink.Visible = true;
				}
				else
				{
					lblGearResponse.Text = objGear.DeviceRating.ToString();
					chkActiveCommlink.Visible = false;
				}

				if (objGear.MaxRating > 0)
					lblGearRating.Text = objGear.Rating.ToString();
				else
					lblGearRating.Text = "";

				try
				{
					lblGearQty.Text = objGear.Quantity.ToString();
				}
				catch
				{
				}

				if (treGear.SelectedNode.Level == 1)
				{
					_blnSkipRefresh = true;
					lblGearQty.Text = objGear.Quantity.ToString();
					chkGearEquipped.Visible = true;
					chkGearEquipped.Checked = objGear.Equipped;

					_blnSkipRefresh = false;
				}
				else
				{
					lblGearQty.Text = "1";
					chkGearEquipped.Visible = true;
					chkGearEquipped.Checked = objGear.Equipped;

					// If this is a Program, determine if its parent Gear (if any) is a Commlink. If so, show the Equipped checkbox.
					if (objGear.IsProgram && _objOptions.CalculateCommlinkResponse)
					{
						Gear objParent = new Gear(_objCharacter);
						objParent = objGear.Parent;
						if (objParent.Category != string.Empty)
						{
							if (objParent.Category == "Commlink" || objParent.Category == "Nexus")
								chkGearEquipped.Text = LanguageManager.Instance.GetString("Checkbox_SoftwareRunning");
						}
					}
				}

				// Show the Weapon Bonus information if it's available.
				if (objGear.WeaponBonus != null)
				{
					lblGearDamageLabel.Visible = true;
					lblGearDamage.Visible = true;
					lblGearAPLabel.Visible = true;
					lblGearAP.Visible = true;
					lblGearDamage.Text = objGear.WeaponBonusDamage();
					lblGearAP.Text = objGear.WeaponBonusAP;
				}
				else
				{
					lblGearDamageLabel.Visible = false;
					lblGearDamage.Visible = false;
					lblGearAPLabel.Visible = false;
					lblGearAP.Visible = false;
				}

				cmdGearReduceQty.Enabled = true;

				if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && (objGear.GetType() == typeof(Commlink) || objGear.Category == "Nexus"))
				{
					chkGearHomeNode.Visible = true;
					chkGearHomeNode.Checked = objGear.HomeNode;
				}

				treGear.SelectedNode.Text = objGear.DisplayName;
			}

			// Enable or disable the Split/Merge buttons as needed.
			if (treGear.SelectedNode.Level == 1)
			{
				cmdGearSplitQty.Enabled = true;
				cmdGearMergeQty.Enabled = true;
				if (_objCharacter.Vehicles.Count > 0)
					cmdGearMoveToVehicle.Enabled = true;
				else
					cmdGearMoveToVehicle.Enabled = false;
			}
			else
			{
				cmdGearSplitQty.Enabled = false;
				cmdGearMergeQty.Enabled = false;
				cmdGearMoveToVehicle.Enabled = false;
			}
		}

		/// <summary>
		/// Update the Window title to show the Character's name and unsaved changes status.
		/// </summary>
		private void UpdateWindowTitle(bool blnCanSkip = true)
		{
			if (this.Text.EndsWith("*") && blnCanSkip)
				return;

			this.Text = "";
			if (txtAlias.Text != "")
				this.Text += txtAlias.Text + " - ";
			this.Text += LanguageManager.Instance.GetString("Title_CareerMode");
			this.Text += " (" + _objCharacter.Options.Name + ")";
			if (_blnIsDirty)
				this.Text += "*";
		}

		/// <summary>
		/// Save the Character.
		/// </summary>
		private bool SaveCharacter()
		{
			bool blnSaved = false;

			// If the Character does not have a file name, trigger the Save As menu item instead.
			if (_objCharacter.FileName == "")
				blnSaved = SaveCharacterAs();
			else
			{
				_objCharacter.Save();
				_blnIsDirty = false;
				blnSaved = true;
				GlobalOptions.Instance.AddToMRUList(_objCharacter.FileName);
			}
			UpdateWindowTitle(false);

			return blnSaved;
		}

		/// <summary>
		/// Save the Character using the Save As dialogue box.
		/// </summary>
		private bool SaveCharacterAs()
		{
			bool blnSaved = false;

			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "Chummer5 Files (*.chum5)|*.chum5|All Files (*.*)|*.*";

			string strShowFileName = "";
			string[] strFile = _objCharacter.FileName.Split(Path.DirectorySeparatorChar);
			strShowFileName = strFile[strFile.Length - 1];

			if (strShowFileName == "")
				strShowFileName = _objCharacter.Alias;

			saveFileDialog.FileName = strShowFileName;

			if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				string strFileName = saveFileDialog.FileName;
				_objCharacter.FileName = strFileName;
				_objCharacter.Save();
				_blnIsDirty = false;
				blnSaved = true;
				GlobalOptions.Instance.AddToMRUList(_objCharacter.FileName);
			}
			UpdateWindowTitle(false);

			return blnSaved;
		}

		/// <summary>
		/// Open the Select Cyberware window and handle adding to the Tree and Character.
		/// </summary>
		private bool PickCyberware(Improvement.ImprovementSource objSource = Improvement.ImprovementSource.Cyberware)
		{
			// Determine the lowest whole number for the character's current Essence.
			decimal decStartingESS = Math.Floor(_objCharacter.Essence);

			Cyberware objSelectedCyberware = new Cyberware(_objCharacter);
			int intNode = 0;
			if (objSource == Improvement.ImprovementSource.Bioware)
				intNode = 1;

			// Attempt to locate the selected piece of Cyberware.
			try
			{
				if (treCyberware.SelectedNode.Level > 0)
					objSelectedCyberware = _objFunctions.FindCyberware(treCyberware.SelectedNode.Tag.ToString(), _objCharacter.Cyberware);
			}
			catch
			{
			}

			frmSelectCyberware frmPickCyberware = new frmSelectCyberware(_objCharacter, true);
			double dblMultiplier = 1;
			// Apply the character's Cyberware Essence cost multiplier if applicable.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.CyberwareEssCost) != 0 && objSource == Improvement.ImprovementSource.Cyberware)
			{
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.ImproveType == Improvement.ImprovementType.CyberwareEssCost && objImprovement.Enabled)
						dblMultiplier -= (1 - (Convert.ToDouble(objImprovement.Value, GlobalOptions.Instance.CultureInfo) / 100));
				}
				frmPickCyberware.CharacterESSMultiplier = dblMultiplier;
			}

			// Apply the character's Bioware Essence cost multiplier if applicable.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.BiowareEssCost) != 0 && objSource == Improvement.ImprovementSource.Bioware)
			{
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.ImproveType == Improvement.ImprovementType.BiowareEssCost && objImprovement.Enabled)
						dblMultiplier -= (1 - (Convert.ToDouble(objImprovement.Value, GlobalOptions.Instance.CultureInfo) / 100));
				}
				frmPickCyberware.CharacterESSMultiplier = dblMultiplier;
			}

			// Apply the character's Basic Bioware Essence cost multiplier if applicable.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.BasicBiowareEssCost) != 0 && objSource == Improvement.ImprovementSource.Bioware)
			{
				double dblBasicMultiplier = 1;
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.ImproveType == Improvement.ImprovementType.BasicBiowareEssCost && objImprovement.Enabled)
						dblBasicMultiplier -= (1 - (Convert.ToDouble(objImprovement.Value, GlobalOptions.Instance.CultureInfo) / 100));
				}
				frmPickCyberware.BasicBiowareESSMultiplier = dblBasicMultiplier;
			}

			// Genetech Cost multiplier.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.GenetechCostMultiplier) != 0 && objSource == Improvement.ImprovementSource.Bioware)
			{
				dblMultiplier = 1;
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.ImproveType == Improvement.ImprovementType.GenetechCostMultiplier && objImprovement.Enabled)
						dblMultiplier -= (1 - (Convert.ToDouble(objImprovement.Value, GlobalOptions.Instance.CultureInfo) / 100));
				}
				frmPickCyberware.GenetechCostMultiplier = dblMultiplier;
			}

			// Transgenics Cost multiplier.
			if (_objImprovementManager.ValueOf(Improvement.ImprovementType.TransgenicsBiowareCost) != 0 && objSource == Improvement.ImprovementSource.Bioware)
			{
				dblMultiplier = 1;
				foreach (Improvement objImprovement in _objCharacter.Improvements)
				{
					if (objImprovement.ImproveType == Improvement.ImprovementType.TransgenicsBiowareCost && objImprovement.Enabled)
						dblMultiplier -= (1 - (Convert.ToDouble(objImprovement.Value, GlobalOptions.Instance.CultureInfo) / 100));
				}
				frmPickCyberware.TransgenicsBiowareCostMultiplier = dblMultiplier;
			}

			try
			{
				if (treCyberware.SelectedNode.Level > 0)
				{
					frmPickCyberware.SetGrade = lblCyberwareGrade.Text;
					frmPickCyberware.LockGrade();
					// If the Cyberware has a Capacity with no brackets (meaning it grants Capacity), show only Subsystems (those that conume Capacity).
					if (!objSelectedCyberware.Capacity.Contains('['))
					{
						frmPickCyberware.ShowOnlySubsystems = true;
						frmPickCyberware.Subsystems = objSelectedCyberware.Subsytems;
						frmPickCyberware.MaximumCapacity = objSelectedCyberware.CapacityRemaining;

						// Do not allow the user to add a new piece of Cyberware if its Capacity has been reached.
						if (_objOptions.EnforceCapacity && objSelectedCyberware.CapacityRemaining < 0)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return false;
						}
					}
				}
			}
			catch
			{
			}

			if (objSource == Improvement.ImprovementSource.Bioware)
				frmPickCyberware.WindowMode = frmSelectCyberware.Mode.Bioware;

			frmPickCyberware.AllowModularPlugins = objSelectedCyberware.AllowModularPlugins;

			frmPickCyberware.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickCyberware.DialogResult == DialogResult.Cancel)
				return false;

			// Open the Cyberware XML file and locate the selected piece.
			XmlDocument objXmlDocument = new XmlDocument();
			if (objSource == Improvement.ImprovementSource.Bioware)
				objXmlDocument = XmlManager.Instance.Load("bioware.xml");
			else
				objXmlDocument = XmlManager.Instance.Load("cyberware.xml");

			XmlNode objXmlCyberware;
			if (objSource == Improvement.ImprovementSource.Bioware)
				objXmlCyberware = objXmlDocument.SelectSingleNode("/chummer/biowares/bioware[name = \"" + frmPickCyberware.SelectedCyberware + "\"]");
			else
				objXmlCyberware = objXmlDocument.SelectSingleNode("/chummer/cyberwares/cyberware[name = \"" + frmPickCyberware.SelectedCyberware + "\"]");

			// Create the Cyberware object.
			Cyberware objCyberware = new Cyberware(_objCharacter);
			List<Weapon> objWeapons = new List<Weapon>();
			TreeNode objNode = new TreeNode();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			objCyberware.Create(objXmlCyberware, _objCharacter, frmPickCyberware.SelectedGrade, objSource, frmPickCyberware.SelectedRating, objNode, objWeapons, objWeaponNodes);
			if (objCyberware.InternalId == Guid.Empty.ToString())
				return false;

			// Force the item to be Transgenic if selected.
			if (frmPickCyberware.ForceTransgenic)
				objCyberware.Category = "Genetech: Transgenics";

			// Apply the ESS discount if applicable.
			if (_objOptions.AllowCyberwareESSDiscounts)
				objCyberware.ESSDiscount = frmPickCyberware.SelectedESSDiscount;

			int intCost = objCyberware.TotalCost;

			// Multiply the cost if applicable.
			if (objCyberware.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objCyberware.TotalAvail.EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickCyberware.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					// Remove any Improvements created by the Cyberware.
					_objImprovementManager.RemoveImprovements(objCyberware.SourceType, objCyberware.InternalId);
					return frmPickCyberware.AddAgain;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					string strEntry = "";
					if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
						strEntry = LanguageManager.Instance.GetString("String_ExpensePurchaseCyberware");
					else
						strEntry = LanguageManager.Instance.GetString("String_ExpensePurchaseBioware");
					objExpense.Create(intCost * -1, strEntry + " " + objCyberware.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					ExpenseUndo objUndo = new ExpenseUndo();
					objUndo.CreateNuyen(NuyenExpenseType.AddCyberware, objCyberware.InternalId);
					objExpense.Undo = objUndo;
				}
			}

			try
			{
				if (treCyberware.SelectedNode.Level > 0)
				{
					treCyberware.SelectedNode.Nodes.Add(objNode);
					treCyberware.SelectedNode.Expand();
					objSelectedCyberware.Children.Add(objCyberware);
					objCyberware.Parent = objSelectedCyberware;
				}
				else
				{
					treCyberware.Nodes[intNode].Nodes.Add(objNode);
					treCyberware.Nodes[intNode].Expand();
					_objCharacter.Cyberware.Add(objCyberware);
				}
			}
			catch
			{
				treCyberware.Nodes[intNode].Nodes.Add(objNode);
				treCyberware.Nodes[intNode].Expand();
				_objCharacter.Cyberware.Add(objCyberware);
			}

			// Select the node that was just added.
			if (objSource == Improvement.ImprovementSource.Cyberware)
				objNode.ContextMenuStrip = cmsCyberware;
			else if (objSource == Improvement.ImprovementSource.Bioware)
				objNode.ContextMenuStrip = cmsBioware;

			foreach (Weapon objWeapon in objWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			// Create the Weapon Node if one exists.
			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeapon;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			_objFunctions.SortTree(treCyberware);
			treCyberware.SelectedNode = objNode;
			UpdateCharacterInfo();
			RefreshSelectedCyberware();
			PopulateGearList();

			if (frmPickCyberware.DialogResult != DialogResult.Cancel)
			{
				_blnIsDirty = true;
				UpdateWindowTitle();
			}

			return frmPickCyberware.AddAgain;
		}

		/// <summary>
		/// Select a piece of Gear to be added to the character.
		/// </summary>
		/// <param name="blnAmmoOnly">Whether or not only Ammunition should be shown in the window.</param>
		/// <param name="objStackGear">Whether or not the selected item should stack with a matching item on the character.</param>
		/// <param name="strForceItemValue">Force the user to select an item with the passed name..</param>
		private bool PickGear(bool blnAmmoOnly = false, Gear objStackGear = null, string strForceItemValue = "")
		{
			bool blnNullParent = false;
			Gear objSelectedGear = new Gear(_objCharacter);
			if (treGear.SelectedNode != null)
				objSelectedGear = _objFunctions.FindGear(treGear.SelectedNode.Tag.ToString(), _objCharacter.Gear);
			if (objSelectedGear == null)
			{
				objSelectedGear = new Gear(_objCharacter);
				blnNullParent = true;
			}

			ExpenseUndo objUndo = new ExpenseUndo();

			// Open the Gear XML file and locate the selected Gear.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSelectedGear.Name + "\" and category = \"" + objSelectedGear.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true, objSelectedGear.ChildAvailModifier, objSelectedGear.ChildCostMultiplier);
			try
			{
				if (treGear.SelectedNode.Level > 0)
				{
					if (objXmlGear.InnerXml.Contains("<addoncategory>"))
					{
						string strCategories = "";
						foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
							strCategories += objXmlCategory.InnerText + ",";
						// Remove the trailing comma.
						strCategories = strCategories.Substring(0, strCategories.Length - 1);
						frmPickGear.AddCategory(strCategories);
					}

					if (frmPickGear.AllowedCategories != "")
						frmPickGear.AllowedCategories += objSelectedGear.Category + ",";

					// If the Gear has a Capacity with no brackets (meaning it grants Capacity), show only Subsystems (those that conume Capacity).
					if (!objSelectedGear.Capacity.Contains('['))
					{
						frmPickGear.MaximumCapacity = objSelectedGear.CapacityRemaining;

						// Do not allow the user to add a new piece of Gear if its Capacity has been reached.
						if (_objOptions.EnforceCapacity && objSelectedGear.CapacityRemaining < 0)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return false;
						}
					}

					if (objSelectedGear.Category == "Commlink")
					{
						Commlink objCommlink = (Commlink)objSelectedGear;
						frmPickGear.CommlinkResponse = objCommlink.DeviceRating;

						// If a Commlink has just been added, see if the character already has one. If not, make it the active Commlink.
						if (_objFunctions.FindCharacterCommlinks(_objCharacter.Gear).Count == 0)
							objCommlink.IsActive = true;
					}
				}
			}
			catch
			{
			}

			if (blnAmmoOnly)
			{
				frmPickGear.AllowedCategories = "Ammunition";
				frmPickGear.SelectedGear = objSelectedGear.Name;
			}

			frmPickGear.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return false;

			TreeNode objNode = new TreeNode();

			// Open the Cyberware XML file and locate the selected piece.
			objXmlDocument = XmlManager.Instance.Load("gear.xml");
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			Gear objNewGear = new Gear(_objCharacter);
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objCommlink.DisplayName;

					objNewGear = objCommlink;
					break;
				default:
					string strForceValue = "";
					if (blnAmmoOnly)
					{
						strForceValue = objSelectedGear.Extra;
						try
						{
							treGear.SelectedNode = treGear.SelectedNode.Parent;
						}
						catch
						{
						}
					}
					if (strForceItemValue != "")
						strForceValue = strForceItemValue;
					Gear objGear = new Gear(_objCharacter);
					objGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, strForceValue, frmPickGear.Hacked, frmPickGear.InherentProgram, true, true, frmPickGear.Aerodynamic);
					objGear.Quantity = frmPickGear.SelectedQty;
					objNode.Text = objGear.DisplayName;

					objNewGear = objGear;
					break;
			}

			objNewGear.Parent = objSelectedGear;
			if (blnNullParent)
				objNewGear.Parent = null;

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return false;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objNewGear.Cost = (Convert.ToDouble(objNewGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();

			// Reduce the cost to 10% for Hacked programs.
			if (frmPickGear.Hacked)
			{
				if (objNewGear.Cost != "")
					objNewGear.Cost = "(" + objNewGear.Cost + ") * 0.1";
				if (objNewGear.Cost3 != "")
					objNewGear.Cost3 = "(" + objNewGear.Cost3 + ") * 0.1";
				if (objNewGear.Cost6 != "")
					objNewGear.Cost6 = "(" + objNewGear.Cost6 + ") * 0.1";
				if (objNewGear.Cost10 != "")
					objNewGear.Cost10 = "(" + objNewGear.Cost10 + ") * 0.1";
				if (objNewGear.Extra == "")
					objNewGear.Extra = LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
				else
					objNewGear.Extra += ", " + LanguageManager.Instance.GetString("Label_SelectGear_Hacked");
			}

			int intCost = 0;
			if (objNewGear.Cost.Contains("Gear Cost"))
			{
				XPathNavigator nav = objXmlDocument.CreateNavigator();
				string strCost = objNewGear.Cost.Replace("Gear Cost", objSelectedGear.CalculatedCost.ToString());
				XPathExpression xprCost = nav.Compile(strCost);
				intCost = Convert.ToInt32(nav.Evaluate(xprCost).ToString());
			}
			else
			{
				intCost = Convert.ToInt32(objNewGear.TotalCost);
			}

			bool blnMatchFound = false;
			Gear objStackWith = new Gear(_objCharacter);
			// See if the character already has the item on them if they chose to stack.
			if (frmPickGear.Stack)
			{
				if (objStackGear != null)
				{
					blnMatchFound = true;
					objStackWith = objStackGear;
				}
				else
				{
					foreach (Gear objCharacterGear in _objCharacter.Gear)
					{
						if (objCharacterGear.Name == objNewGear.Name && objCharacterGear.Category == objNewGear.Category && objCharacterGear.Rating == objNewGear.Rating && objCharacterGear.Extra == objNewGear.Extra)
						{
							blnMatchFound = true;
							objStackWith = objCharacterGear;

							break;
						}
					}
				}
			}
			
			if (blnMatchFound)
			{
				// If a match was found, we need to use the cost of a single item in the stack which can include plugins.
				foreach (Gear objPlugin in objStackWith.Children)
					intCost += (objPlugin.TotalCost * frmPickGear.SelectedQty);
			}
			if (!blnNullParent && !blnAmmoOnly)
				intCost *= objSelectedGear.Quantity;

			// Apply a markup if applicable.
			if (frmPickGear.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickGear.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Do not allow the user to add a new piece of Cyberware if its Capacity has been reached.
			// This is wrapped in a try statement since the character may not have a piece of Gear selected and has clicked the Buy Additional Ammo button for a Weapon.
			try
			{
				if (!blnMatchFound && treGear.SelectedNode.Level > 0)
				{
					if (_objOptions.EnforceCapacity && objSelectedGear.CapacityRemaining - objNewGear.PluginCapacity < 0)
					{
						MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
						return false;
					}
				}
			}
			catch
			{
			}

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					// Remove any Improvements created by the Gear.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objNewGear.InternalId);
					return frmPickGear.AddAgain;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseGear") + " " + objNewGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					objUndo.CreateNuyen(NuyenExpenseType.AddGear, objNewGear.InternalId, objNewGear.Quantity);
					objExpense.Undo = objUndo;
				}
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return false;

			if (blnMatchFound)
			{
				// A match was found, so increase the quantity instead.
				objStackWith.Quantity += objNewGear.Quantity;
				blnMatchFound = true;

				if (objUndo.ObjectId != "")
					objUndo.ObjectId = objStackWith.InternalId;

				foreach (TreeNode objGearNode in treGear.Nodes[0].Nodes)
				{
					if (objStackWith.InternalId == objGearNode.Tag.ToString())
					{
						objGearNode.Text = objStackWith.DisplayName;
						treGear.SelectedNode = objGearNode;
						break;
					}
				}
			}

			// Add the Gear.
			if (!blnMatchFound)
			{
				// Create any Weapons that came with this Gear.
				foreach (Weapon objWeapon in objWeapons)
					_objCharacter.Weapons.Add(objWeapon);

				foreach (TreeNode objWeaponNode in objWeaponNodes)
				{
					objWeaponNode.ContextMenuStrip = cmsWeapon;
					treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
					treWeapons.Nodes[0].Expand();
				}

				try
				{
					if (treGear.SelectedNode.Level > 0)
					{
						objNode.ContextMenuStrip = cmsGear;
						treGear.SelectedNode.Nodes.Add(objNode);
						treGear.SelectedNode.Expand();
						objSelectedGear.Children.Add(objNewGear);
					}
					else
					{
						objNode.ContextMenuStrip = cmsGear;
						treGear.Nodes[0].Nodes.Add(objNode);
						treGear.Nodes[0].Expand();
						_objCharacter.Gear.Add(objNewGear);
					}
				}
				catch
				{
					treGear.Nodes[0].Nodes.Add(objNode);
					treGear.Nodes[0].Expand();
					_objCharacter.Gear.Add(objNewGear);
				}

				// Select the node that was just added.
				lblGearQty.Text = objNewGear.Quantity.ToString();
				if (objNode.Level < 2)
					treGear.SelectedNode = objNode;
			}

			UpdateCharacterInfo();
			RefreshSelectedGear();

			if (frmPickGear.DialogResult != DialogResult.Cancel)
			{
				_blnIsDirty = true;
				UpdateWindowTitle();
			}

			return frmPickGear.AddAgain;
		}

		/// <summary>
		/// Select a piece of Gear and add it to a piece of Armor.
		/// </summary>
		/// <param name="blnShowArmorCapacityOnly">Whether or not only items that consume capacity should be shown.</param>
		private bool PickArmorGear(bool blnShowArmorCapacityOnly = false)
		{
			bool blnNullParent = true;
			Gear objSelectedGear = new Gear(_objCharacter);
			Armor objSelectedArmor = new Armor(_objCharacter);
			ExpenseUndo objUndo = new ExpenseUndo();

			foreach (Armor objArmor in _objCharacter.Armor)
			{
				if (objArmor.InternalId == treArmor.SelectedNode.Tag.ToString())
					objSelectedArmor = objArmor;
			}

			if (treArmor.SelectedNode.Level > 1)
			{
				objSelectedGear = _objFunctions.FindArmorGear(treArmor.SelectedNode.Tag.ToString(), _objCharacter.Armor, out objSelectedArmor);
				if (objSelectedGear != null)
					blnNullParent = false;
			}

			// Open the Gear XML file and locate the selected Gear.
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");

			XmlNode objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + objSelectedGear.Name + "\" and category = \"" + objSelectedGear.Category + "\"]");

			frmSelectGear frmPickGear = new frmSelectGear(_objCharacter, true);
			frmPickGear.EnableStack = false;
			frmPickGear.ShowArmorCapacityOnly = blnShowArmorCapacityOnly;
			frmPickGear.CapacityDisplayStyle = objSelectedArmor.CapacityDisplayStyle;
			try
			{
				if (treArmor.SelectedNode.Level > 1)
				{
					if (objXmlGear.InnerXml.Contains("<addoncategory>"))
					{
						string strCategories = "";
						foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
							strCategories += objXmlCategory.InnerText + ",";
						// Remove the trailing comma.
						strCategories = strCategories.Substring(0, strCategories.Length - 1);
						frmPickGear.AddCategory(strCategories);
					}

					if (frmPickGear.AllowedCategories != "")
						frmPickGear.AllowedCategories += objSelectedGear.Category + ",";

					// If the Gear has a Capacity with no brackets (meaning it grants Capacity), show only Subsystems (those that conume Capacity).
					if (!objSelectedGear.Capacity.Contains('['))
					{
						frmPickGear.MaximumCapacity = objSelectedGear.CapacityRemaining;

						// Do not allow the user to add a new piece of Gear if its Capacity has been reached.
						if (_objOptions.EnforceCapacity && objSelectedGear.CapacityRemaining < 0)
						{
							MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
							return false;
						}
					}

					if (objSelectedGear.Category == "Commlink")
					{
						Commlink objCommlink = (Commlink)objSelectedGear;
						frmPickGear.CommlinkResponse = objCommlink.DeviceRating;
					}
				}
				else if (treArmor.SelectedNode.Level == 1)
				{
					// Open the Armor XML file and locate the selected Gear.
					objXmlDocument = XmlManager.Instance.Load("armor.xml");
					objXmlGear = objXmlDocument.SelectSingleNode("/chummer/armors/armor[name = \"" + objSelectedArmor.Name + "\"]");

					if (objXmlGear.InnerXml.Contains("<addoncategory>"))
					{
						string strCategories = "";
						foreach (XmlNode objXmlCategory in objXmlGear.SelectNodes("addoncategory"))
							strCategories += objXmlCategory.InnerText + ",";
						// Remove the trailing comma.
						strCategories = strCategories.Substring(0, strCategories.Length - 1);
						frmPickGear.AddCategory(strCategories);
					}
				}
			}
			catch
			{
			}

			frmPickGear.ShowDialog(this);

			// Make sure the dialogue window was not canceled.
			if (frmPickGear.DialogResult == DialogResult.Cancel)
				return false;

			TreeNode objNode = new TreeNode();

			// Open the Cyberware XML file and locate the selected piece.
			objXmlDocument = XmlManager.Instance.Load("gear.xml");
			objXmlGear = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"" + frmPickGear.SelectedGear + "\" and category = \"" + frmPickGear.SelectedCategory + "\"]");

			// Create the new piece of Gear.
			Gear objNewGear = new Gear(_objCharacter);
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();

			switch (frmPickGear.SelectedCategory)
			{
				case "Commlink":
				case "Commlink Upgrade":
					Commlink objCommlink = new Commlink(_objCharacter);
					objCommlink.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating);
					objCommlink.Quantity = frmPickGear.SelectedQty;

					objNewGear = objCommlink;
					break;
				default:
					Gear objGear = new Gear(_objCharacter);
					objGear.Create(objXmlGear, _objCharacter, objNode, frmPickGear.SelectedRating, objWeapons, objWeaponNodes, "", false, false, true, true, frmPickGear.Aerodynamic);
					objGear.Quantity = frmPickGear.SelectedQty;

					objNewGear = objGear;
					break;
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return false;

			if (!blnNullParent)
				objNewGear.Parent = objSelectedGear;

			// Reduce the cost for Do It Yourself components.
			if (frmPickGear.DoItYourself)
				objNewGear.Cost = (Convert.ToDouble(objNewGear.Cost, GlobalOptions.Instance.CultureInfo) * 0.5).ToString();

			// Apply a markup if applicable.
			int intCost = objNewGear.TotalCost;
			if (frmPickGear.Markup != 0)
			{
				double dblCost = Convert.ToDouble(intCost, GlobalOptions.Instance.CultureInfo);
				dblCost *= 1 + (Convert.ToDouble(frmPickGear.Markup, GlobalOptions.Instance.CultureInfo) / 100.0);
				intCost = Convert.ToInt32(dblCost);
			}

			// Multiply the cost if applicable.
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailRestricted")) && _objOptions.MultiplyRestrictedCost)
				intCost *= _objOptions.RestrictedCostMultiplier;
			if (objNewGear.TotalAvail().EndsWith(LanguageManager.Instance.GetString("String_AvailForbidden")) && _objOptions.MultiplyForbiddenCost)
				intCost *= _objOptions.ForbiddenCostMultiplier;

			// Do not allow the user to add new Gear if the Armor's Capacity has been reached.
			if (_objOptions.EnforceCapacity)
			{
				objSelectedArmor.Gear.Add(objSelectedGear);
				if (objSelectedArmor.CapacityRemaining < 0)
				{
					objSelectedArmor.Gear.Remove(objSelectedGear);
					MessageBox.Show(LanguageManager.Instance.GetString("Message_CapacityReached"), LanguageManager.Instance.GetString("MessageTitle_CapacityReached"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return frmPickGear.AddAgain;
				}
				else
					objSelectedArmor.Gear.Remove(objSelectedGear);
			}

			// Check the item's Cost and make sure the character can afford it.
			if (!frmPickGear.FreeCost)
			{
				if (intCost > _objCharacter.Nuyen)
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					// Remove any Improvements created by the Gear.
					_objImprovementManager.RemoveImprovements(Improvement.ImprovementSource.Gear, objNewGear.InternalId);
					return frmPickGear.AddAgain;
				}
				else
				{
					// Create the Expense Log Entry.
					ExpenseLogEntry objExpense = new ExpenseLogEntry();
					objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseArmorGear") + " " + objNewGear.DisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
					_objCharacter.ExpenseEntries.Add(objExpense);
					_objCharacter.Nuyen -= intCost;

					objUndo.CreateNuyen(NuyenExpenseType.AddArmorGear, objNewGear.InternalId, objNewGear.Quantity);
					objExpense.Undo = objUndo;
				}
			}

			if (objNewGear.InternalId == Guid.Empty.ToString())
				return false;

			// Create any Weapons that came with this Gear.
			foreach (Weapon objWeapon in objWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				objWeaponNode.ContextMenuStrip = cmsWeapon;
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			bool blnMatchFound = false;
			// If this is Ammunition, see if the character already has it on them.
			if (objNewGear.Category == "Ammunition")
			{
				foreach (Gear objCharacterGear in _objCharacter.Gear)
				{
					if (objCharacterGear.Name == objNewGear.Name && objCharacterGear.Category == objNewGear.Category && objCharacterGear.Rating == objNewGear.Rating && objCharacterGear.Extra == objNewGear.Extra)
					{
						// A match was found, so increase the quantity instead.
						objCharacterGear.Quantity += objNewGear.Quantity;
						blnMatchFound = true;

						if (objUndo.ObjectId != "")
							objUndo.ObjectId = objCharacterGear.InternalId;

						foreach (TreeNode objGearNode in treGear.Nodes[0].Nodes)
						{
							if (objCharacterGear.InternalId == objGearNode.Tag.ToString())
							{
								objGearNode.Text = objCharacterGear.DisplayName;
								treArmor.SelectedNode = objGearNode;
								break;
							}
						}

						break;
					}
				}
			}

			// Add the Gear.
			if (!blnMatchFound)
			{
				if (objSelectedGear.Name == string.Empty)
				{
					objNode.ContextMenuStrip = cmsArmorGear;
					treArmor.SelectedNode.Nodes.Add(objNode);
					treArmor.SelectedNode.Expand();
					objSelectedArmor.Gear.Add(objNewGear);
				}
				else
				{
					objNode.ContextMenuStrip = cmsArmorGear;
					treArmor.SelectedNode.Nodes.Add(objNode);
					treArmor.SelectedNode.Expand();
					objSelectedGear.Children.Add(objNewGear);
				}

				// Select the node that was just added.
				treArmor.SelectedNode = objNode;
			}

			UpdateCharacterInfo();
			RefreshSelectedArmor();

			_blnIsDirty = true;
			UpdateWindowTitle();

			return frmPickGear.AddAgain;
		}

		/// <summary>
		/// Refresh the currently-selected Lifestyle.
		/// </summary>
		private void RefreshSelectedLifestyle()
		{
			bool blnClear = false;
			try
			{
				if (treLifestyles.SelectedNode.Level == 0)
					blnClear = true;
			}
			catch
			{
				blnClear = true;
			}
			if (blnClear)
			{
				lblLifestyleCost.Text = "";
				lblLifestyleMonths.Text = "";
				lblLifestyleSource.Text = "";
				tipTooltip.SetToolTip(lblLifestyleSource, null);
				lblLifestyleComforts.Text = "";
				lblLifestyleEntertainment.Text = "";
				lblLifestyleNecessities.Text = "";
				lblLifestyleNeighborhood.Text = "";
				lblLifestyleSecurity.Text = "";
				lblLifestyleQualities.Text = "";
				cmdIncreaseLifestyleMonths.Enabled = false;
				cmdDecreaseLifestyleMonths.Enabled = false;
			}

			if (treLifestyles.SelectedNode.Level > 0)
			{
				_blnSkipRefresh = true;

				// Locate the selected Lifestyle.
				Lifestyle objLifestyle = _objFunctions.FindLifestyle(treLifestyles.SelectedNode.Tag.ToString(), _objCharacter.Lifestyles);
				if (objLifestyle == null)
					return;

				decimal decMultiplier = 1.0m;
				decimal decModifier = Convert.ToDecimal(_objImprovementManager.ValueOf(Improvement.ImprovementType.LifestyleCost), GlobalOptions.Instance.CultureInfo);
				if (objLifestyle.StyleType == LifestyleType.Standard)
					decModifier += Convert.ToDecimal(_objImprovementManager.ValueOf(Improvement.ImprovementType.BasicLifestyleCost), GlobalOptions.Instance.CultureInfo);
				decMultiplier = 1.0m + Convert.ToDecimal(decModifier / 100, GlobalOptions.Instance.CultureInfo);

				lblLifestyleCost.Text = String.Format("{0:###,###,##0¥}", objLifestyle.TotalMonthlyCost);
				lblLifestyleMonths.Text = objLifestyle.Months.ToString();
				string strBook = _objOptions.LanguageBookShort(objLifestyle.Source);
				string strPage = objLifestyle.Page;
				lblLifestyleSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblLifestyleSource, _objOptions.LanguageBookLong(objLifestyle.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objLifestyle.Page);
				cmdIncreaseLifestyleMonths.Enabled = true;
				cmdDecreaseLifestyleMonths.Enabled = true;

				// Change the Cost/Month label.
				if (objLifestyle.StyleType == LifestyleType.Safehouse)
					lblLifestyleCostLabel.Text = LanguageManager.Instance.GetString("Label_SelectLifestyle_CostPerWeek");
				else
					lblLifestyleCostLabel.Text = LanguageManager.Instance.GetString("Label_SelectLifestyle_CostPerMonth");

				if (objLifestyle.BaseLifestyle != "")
				{
					XmlDocument objXmlDocument = XmlManager.Instance.Load("lifestyles.xml");
					string strLifestyle = "";
					string strQualities = "";

					lblLifestyleQualities.Text = "";
					XmlNode objNode = objXmlDocument.SelectSingleNode("/chummer/lifestyles/lifestyle[name = \"" + objLifestyle.BaseLifestyle + "\"]");
					if (objNode["translate"] != null)
                        strLifestyle = objNode["translate"].InnerText;
					else
                        strLifestyle = objNode["name"].InnerText;

					foreach (string strQuality in objLifestyle.Qualities)
					{
						string strQualityName = strQuality.Substring(0, strQuality.IndexOf('[') - 1);
						objNode = objXmlDocument.SelectSingleNode("/chummer/qualities/quality[name = \"" + strQualityName + "\"]");
						if (objNode["translate"] != null)
							strQualities += objNode["translate"].InnerText;
						else
							strQualities += objNode["name"].InnerText;
						strQualities += " [" + objNode["lp"].InnerText + "LP]\n";
					}

                    lblLifestyleComforts.Text = strLifestyle;
					lblLifestyleQualities.Text += strQualities;
				}
				else
				{
					lblLifestyleComforts.Text = "";
					lblLifestyleEntertainment.Text = "";
					lblLifestyleNecessities.Text = "";
					lblLifestyleNeighborhood.Text = "";
					lblLifestyleSecurity.Text = "";
					lblLifestyleQualities.Text = "";
				}

				_blnSkipRefresh = false;
			}
		}

		/// <summary>
		/// Refresh the currently-selected Vehicle.
		/// </summary>
		private void RefreshSelectedVehicle()
		{
			bool blnClear = false;
			try
			{
				if (treVehicles.SelectedNode.Level == 0)
					blnClear = true;
			}
			catch
			{
				blnClear = true;
			}
			if (blnClear)
			{
				lblVehicleWeaponAmmoRemaining.Text = "";
				lblVehicleWeaponName.Text = "";
				lblVehicleWeaponCategory.Text = "";
				lblVehicleWeaponAP.Text = "";
				lblVehicleWeaponDamage.Text = "";
				lblVehicleWeaponMode.Text = "";
				lblVehicleWeaponAmmo.Text = "";
				cmdFireVehicleWeapon.Enabled = false;
				cmdReloadVehicleWeapon.Enabled = false;

				lblVehicleWeaponRangeShort.Text = "";
				lblVehicleWeaponRangeMedium.Text = "";
				lblVehicleWeaponRangeLong.Text = "";
				lblVehicleWeaponRangeExtreme.Text = "";

				lblVehicleGearQty.Text = "";
				cmdVehicleGearReduceQty.Enabled = false;
				cboVehicleWeaponAmmo.Enabled = false;
				return;
			}
			chkVehicleHomeNode.Visible = false;
			cmdVehicleMoveToInventory.Enabled = false;

			if (treVehicles.SelectedNode.Level != 0)
			{
				// Locate the selected Vehicle.
				TreeNode objVehicleNode = new TreeNode();
				objVehicleNode = treVehicles.SelectedNode;
				if (treVehicles.SelectedNode.Level > 1)
				{
					while (objVehicleNode.Level > 1)
						objVehicleNode = objVehicleNode.Parent;
				}

				Vehicle objVehicle = _objFunctions.FindVehicle(objVehicleNode.Tag.ToString(), _objCharacter.Vehicles);
				if (objVehicle == null)
					return;

				// Hide any unused Physical CM boxes.
				panVehicleCM.Visible = true;
				_blnSkipRefresh = true;
				foreach (CheckBox objPhysicalCM in panVehicleCM.Controls.OfType<CheckBox>())
				{
					if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) <= objVehicle.PhysicalCM)
					{
						if (Convert.ToInt32(objPhysicalCM.Tag.ToString()) <= objVehicle.PhysicalCMFilled)
							objPhysicalCM.Checked = true;
						else
							objPhysicalCM.Checked = false;

						objPhysicalCM.Visible = true;
					}
					else
					{
						objPhysicalCM.Checked = false;
						objPhysicalCM.Visible = false;
						objPhysicalCM.Text = "";
					}
				}
				_blnSkipRefresh = false;
			}

			// Locate the selected Vehicle.
			if (treVehicles.SelectedNode.Level == 1)
			{
				Vehicle objVehicle = _objFunctions.FindVehicle(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
				if (objVehicle == null)
					return;

				_blnSkipRefresh = true;
				lblVehicleRatingLabel.Text = LanguageManager.Instance.GetString("Label_Rating");
				lblVehicleRating.Text = "";
				_blnSkipRefresh = false;

				lblVehicleName.Text = objVehicle.DisplayNameShort;
				lblVehicleCategory.Text = objVehicle.DisplayCategory;
				lblVehicleAvail.Text = objVehicle.CalculatedAvail;
				lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objVehicle.TotalCost);
				lblVehicleHandling.Text = objVehicle.TotalHandling.ToString();
				lblVehicleAccel.Text = objVehicle.TotalAccel;
				lblVehicleSpeed.Text = objVehicle.TotalSpeed.ToString();
				lblVehicleDevice.Text = objVehicle.DeviceRating.ToString();
				lblVehiclePilot.Text = objVehicle.Pilot.ToString();
				lblVehicleBody.Text = objVehicle.TotalBody.ToString();
				lblVehicleArmor.Text = objVehicle.TotalArmor.ToString();
				if (_objOptions.UseCalculatedVehicleSensorRatings)
					lblVehicleSensor.Text = objVehicle.CalculatedSensor.ToString() + " (" + LanguageManager.Instance.GetString("Label_Signal") + " " + objVehicle.SensorSignal.ToString() + ")";
				else
					lblVehicleSensor.Text = objVehicle.Sensor.ToString() + " (" + LanguageManager.Instance.GetString("Label_Signal") + " " + objVehicle.SensorSignal.ToString() + ")";
				lblVehicleSlots.Text = objVehicle.Slots.ToString() + " (" + (objVehicle.Slots - objVehicle.SlotsUsed).ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				string strBook = _objOptions.LanguageBookShort(objVehicle.Source);
				string strPage = objVehicle.Page;
				lblVehicleSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objVehicle.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objVehicle.Page);
				chkVehicleWeaponAccessoryInstalled.Enabled = false;
				chkVehicleIncludedInWeapon.Checked = false;

				if (_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients")
				{
					chkVehicleHomeNode.Visible = true;
					chkVehicleHomeNode.Checked = objVehicle.HomeNode;
				}

				UpdateCharacterInfo();
			}
			else if (treVehicles.SelectedNode.Level == 2)
			{
				panVehicleCM.Visible = true;
				bool blnVehicleMod = false;

				// If this is a Vehicle Location, don't do anything.
				foreach (Vehicle objVehicle in _objCharacter.Vehicles)
				{
					if (objVehicle.InternalId == treVehicles.SelectedNode.Parent.Tag.ToString())
					{
						foreach (string strLocation in objVehicle.Locations)
						{
							if (strLocation == treVehicles.SelectedNode.Tag.ToString())
							{
								lblVehicleName.Text = "";
								lblVehicleCategory.Text = "";
								lblVehicleSource.Text = "";
								lblVehicleHandling.Text = "";
								lblVehicleAccel.Text = "";
								lblVehicleSpeed.Text = "";
								lblVehicleDevice.Text = "";
								lblVehiclePilot.Text = "";
								lblVehicleBody.Text = "";
								lblVehicleArmor.Text = "";
								lblVehicleSensor.Text = "";
								lblVehicleFirewall.Text = "";
								lblVehicleSignal.Text = "";
								lblVehicleResponse.Text = "";
								lblVehicleSystem.Text = "";
								lblVehicleAvail.Text = "";
								lblVehicleCost.Text = "";
								lblVehicleSlots.Text = "";
								return;
							}
						}
					}
				}

				// Locate the selected VehicleMod.
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				VehicleMod objMod = _objFunctions.FindVehicleMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);
				if (objMod != null)
					blnVehicleMod = true;

				if (blnVehicleMod)
				{
					if (objMod.MaxRating != "qty")
					{
						if (Convert.ToInt32(objMod.MaxRating) > 0)
						{
							_blnSkipRefresh = true;
							lblVehicleRatingLabel.Text = LanguageManager.Instance.GetString("Label_Rating");
							lblVehicleRating.Text = objMod.Rating.ToString();
							_blnSkipRefresh = false;
						}
						else
						{
							_blnSkipRefresh = true;
							lblVehicleRatingLabel.Text = LanguageManager.Instance.GetString("Label_Rating");
							lblVehicleRating.Text = "";
							_blnSkipRefresh = false;
						}
					}
					else
					{
						_blnSkipRefresh = true;
						lblVehicleRatingLabel.Text = LanguageManager.Instance.GetString("Label_Qty");
						lblVehicleRating.Text = objMod.Rating.ToString();
						_blnSkipRefresh = false;
					}

					lblVehicleName.Text = objMod.DisplayNameShort;
					lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleModification");
					lblVehicleAvail.Text = objMod.TotalAvail;
					lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objMod.TotalCost);
					lblVehicleHandling.Text = "";
					lblVehicleAccel.Text = "";
					lblVehicleSpeed.Text = "";
					lblVehicleDevice.Text = "";
					lblVehiclePilot.Text = "";
					lblVehicleBody.Text = "";
					lblVehicleArmor.Text = "";
					lblVehicleSensor.Text = "";
					lblVehicleFirewall.Text = "";
					lblVehicleSignal.Text = "";
					lblVehicleResponse.Text = "";
					lblVehicleSystem.Text = "";
					lblVehicleSlots.Text = objMod.CalculatedSlots.ToString();
					string strBook = _objOptions.LanguageBookShort(objMod.Source);
					string strPage = objMod.Page;
					lblVehicleSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objMod.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objMod.Page);
				}
				else
				{
					bool blnFound = false;
					// If it's not a Vehicle Mod then it must be a Sensor.
					Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);
					if (objGear != null)
						blnFound = true;

					if (blnFound)
					{
						lblVehicleRating.Text = "";
						if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							lblVehicleGearQty.Text = objGear.Quantity.ToString();
							cmdVehicleGearReduceQty.Enabled = true;

							if (objGear.Rating > 0)
								lblVehicleRating.Text = objGear.Rating.ToString();
						}

						lblVehicleName.Text = objGear.DisplayNameShort;
						lblVehicleCategory.Text = objGear.DisplayCategory;
						lblVehicleAvail.Text = objGear.TotalAvail(true);
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";
						lblVehicleSlots.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
						string strBook = _objOptions.LanguageBookShort(objGear.Source);
						string strPage = objGear.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

						cmdVehicleMoveToInventory.Enabled = true;

						if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && objGear.GetType() == typeof(Commlink))
						{
							chkVehicleHomeNode.Visible = true;
							chkVehicleHomeNode.Checked = objGear.HomeNode;
						}
					}
					else
					{
						// Look for the selected Vehicle Weapon.
						Weapon objWeapon = new Weapon(_objCharacter);
						Vehicle objCurrentVehicle = new Vehicle(_objCharacter);

						foreach (Vehicle objVehicle in _objCharacter.Vehicles)
						{
							objWeapon = _objFunctions.FindWeapon(treVehicles.SelectedNode.Tag.ToString(), objVehicle.Weapons);
							if (objWeapon != null)
							{
								objCurrentVehicle = objVehicle;
								break;
							}
						}

						lblVehicleWeaponName.Text = objWeapon.DisplayNameShort;
						lblVehicleWeaponCategory.Text = objWeapon.DisplayCategory;
						lblVehicleWeaponDamage.Text = objWeapon.CalculatedDamage();
						lblVehicleWeaponAP.Text = objWeapon.TotalAP;
						lblVehicleWeaponAmmo.Text = objWeapon.CalculatedAmmo();
						lblVehicleWeaponMode.Text = objWeapon.CalculatedMode;
						if (objWeapon.WeaponType == "Ranged" || (objWeapon.WeaponType == "Melee" && objWeapon.Ammo != "0"))
						{
							cmdFireVehicleWeapon.Enabled = true;
							cmdReloadVehicleWeapon.Enabled = true;
							lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();

							cmsVehicleAmmoSingleShot.Enabled = objWeapon.AllowMode("SS") || objWeapon.AllowMode("SA");
							cmsVehicleAmmoShortBurst.Enabled = objWeapon.AllowMode("BF");
							cmsVehicleAmmoLongBurst.Enabled = objWeapon.AllowMode("FA");
							cmsVehicleAmmoFullBurst.Enabled = objWeapon.AllowMode("FA");
							cmsVehicleAmmoSuppressiveFire.Enabled = objWeapon.AllowMode("FA");

							// Melee Weapons with Ammo are considered to be Single Shot.
							if (objWeapon.WeaponType == "Melee" && objWeapon.Ammo != "0")
								cmsVehicleAmmoSingleShot.Enabled = true;

							if (cmsVehicleAmmoFullBurst.Enabled)
								cmsVehicleAmmoFullBurst.Text = LanguageManager.Instance.GetString("String_FullBurst").Replace("{0}", objWeapon.FullBurst.ToString());
							if (cmsVehicleAmmoSuppressiveFire.Enabled)
								cmsVehicleAmmoSuppressiveFire.Text = LanguageManager.Instance.GetString("String_SuppressiveFire").Replace("{0}", objWeapon.Suppressive.ToString());

							List<ListItem> lstAmmo = new List<ListItem>();
							int intCurrentSlot = objWeapon.ActiveAmmoSlot;
							for (int i = 1; i <= objWeapon.AmmoSlots; i++)
							{
								Gear objVehicleGear = new Gear(_objCharacter);
								ListItem objAmmo = new ListItem();
								objWeapon.ActiveAmmoSlot = i;
								objVehicleGear = _objFunctions.FindGear(objWeapon.AmmoLoaded, objCurrentVehicle.Gear);
								objAmmo.Value = i.ToString();

								string strPlugins = "";
								foreach (Vehicle objVehicle in _objCharacter.Vehicles)
								{
									foreach (Gear objCurrentAmmo in objVehicle.Gear)
									{
										if (objCurrentAmmo.InternalId == objWeapon.AmmoLoaded)
										{
											foreach (Gear objChild in objCurrentAmmo.Children)
											{
												strPlugins += objChild.DisplayNameShort + ", ";
											}
										}
									}
								}
								// Remove the trailing comma.
								if (strPlugins != "")
									strPlugins = strPlugins.Substring(0, strPlugins.Length - 2);

								if (objVehicleGear == null)
								{
									if (objWeapon.AmmoRemaining == 0)
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_Empty");
									else
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_ExternalSource");
								}
								else
									objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + objVehicleGear.DisplayNameShort;

								if (strPlugins != "")
									objAmmo.Name += " [" + strPlugins + "]";
								lstAmmo.Add(objAmmo);
							}
							_blnSkipRefresh = true;
							objWeapon.ActiveAmmoSlot = intCurrentSlot;
							cboVehicleWeaponAmmo.Enabled = true;
							cboVehicleWeaponAmmo.ValueMember = "Value";
							cboVehicleWeaponAmmo.DisplayMember = "Name";
							cboVehicleWeaponAmmo.DataSource = lstAmmo;
							cboVehicleWeaponAmmo.SelectedValue = objWeapon.ActiveAmmoSlot.ToString();
							if (cboVehicleWeaponAmmo.SelectedIndex == -1)
								cboVehicleWeaponAmmo.SelectedIndex = 0;
							_blnSkipRefresh = false;
						}

						lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
						lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
						lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
						lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

						lblVehicleName.Text = objWeapon.DisplayNameShort;
						lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeapon");
						lblVehicleAvail.Text = objWeapon.TotalAvail;
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objWeapon.TotalCost);
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";
						lblVehicleSlots.Text = "6 (" + objWeapon.SlotsRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
						string strBook = _objOptions.LanguageBookShort(objWeapon.Source);
						string strPage = objWeapon.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objWeapon.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objWeapon.Page);

						cmdVehicleMoveToInventory.Enabled = true;

						// Determine the Dice Pool size.
						int intPilot = objCurrentVehicle.Pilot;
						int intAutosoft = 0;
						bool blnAutosoftFound = false;
						foreach (Gear objAutosoft in objCurrentVehicle.Gear)
						{
							if (objAutosoft.Category.StartsWith("Autosofts") && objAutosoft.Name == "Targeting")
							{
								if (!blnAutosoftFound)
								{
									if (objAutosoft.Rating > intAutosoft)
										intAutosoft = objAutosoft.Rating;
									if (objAutosoft.Extra == objWeapon.DisplayCategory)
									{
										intAutosoft = objAutosoft.Rating;
										blnAutosoftFound = true;
									}
								}
							}
						}
						if (intAutosoft == 0)
							intPilot -= 1;
						lblVehicleWeaponDicePool.Text = (intPilot + intAutosoft).ToString();
					}
				}
				if (blnVehicleMod)
				{
					chkVehicleWeaponAccessoryInstalled.Enabled = true;
					chkVehicleWeaponAccessoryInstalled.Checked = objMod.Installed;
				}
				else
					chkVehicleWeaponAccessoryInstalled.Enabled = false;
				chkVehicleIncludedInWeapon.Checked = false;
			}
			else if (treVehicles.SelectedNode.Level == 3)
			{
				panVehicleCM.Visible = true;
				bool blnSensorPlugin = false;
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);
				if (objGear != null)
					blnSensorPlugin = true;

				if (blnSensorPlugin)
				{
					lblVehicleRating.Text = "";
					if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
					{
						lblVehicleGearQty.Text = objGear.Quantity.ToString();
						cmdVehicleGearReduceQty.Enabled = true;

						if (objGear.Rating > 0)
							lblVehicleRating.Text = objGear.Rating.ToString();
					}

					lblVehicleName.Text = objGear.DisplayNameShort;
					lblVehicleCategory.Text = objGear.DisplayCategory;
					lblVehicleAvail.Text = objGear.TotalAvail(true);
					lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
					lblVehicleHandling.Text = "";
					lblVehicleAccel.Text = "";
					lblVehicleSpeed.Text = "";
					lblVehicleDevice.Text = "";
					lblVehiclePilot.Text = "";
					lblVehicleBody.Text = "";
					lblVehicleArmor.Text = "";
					lblVehicleSensor.Text = "";
					lblVehicleFirewall.Text = "";
					lblVehicleSignal.Text = "";
					lblVehicleResponse.Text = "";
					lblVehicleSystem.Text = "";
					lblVehicleSlots.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
					string strBook = _objOptions.LanguageBookShort(objGear.Source);
					string strPage = objGear.Page;
					lblVehicleSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

					if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && objGear.GetType() == typeof(Commlink))
					{
						chkVehicleHomeNode.Visible = true;
						chkVehicleHomeNode.Checked = objGear.HomeNode;
					}
				}
				else
				{
					// Look for the selected Vehicle Weapon.
					Weapon objWeapon = new Weapon(_objCharacter);
					Vehicle objCurrentVehicle = new Vehicle(_objCharacter);
					bool blnWeapon = false;

					objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objCurrentVehicle);
					if (objWeapon != null)
						blnWeapon = true;

					if (blnWeapon)
					{
						lblVehicleWeaponName.Text = objWeapon.DisplayNameShort;
						lblVehicleWeaponCategory.Text = objWeapon.DisplayCategory;
						lblVehicleWeaponDamage.Text = objWeapon.CalculatedDamage();
						lblVehicleWeaponAP.Text = objWeapon.TotalAP;
						lblVehicleWeaponAmmo.Text = objWeapon.CalculatedAmmo();
						lblVehicleWeaponMode.Text = objWeapon.CalculatedMode;
						if (objWeapon.WeaponType == "Ranged")
						{
							cmdFireVehicleWeapon.Enabled = true;
							cmdReloadVehicleWeapon.Enabled = true;
							lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();

							cmsVehicleAmmoSingleShot.Enabled = objWeapon.AllowMode("SS") || objWeapon.AllowMode("SA");
							cmsVehicleAmmoShortBurst.Enabled = objWeapon.AllowMode("BF");
							cmsVehicleAmmoLongBurst.Enabled = objWeapon.AllowMode("FA");
							cmsVehicleAmmoFullBurst.Enabled = objWeapon.AllowMode("FA");
							cmsVehicleAmmoSuppressiveFire.Enabled = objWeapon.AllowMode("FA");
							if (cmsVehicleAmmoFullBurst.Enabled)
								cmsVehicleAmmoFullBurst.Text = LanguageManager.Instance.GetString("String_FullBurst").Replace("{0}", objWeapon.FullBurst.ToString());
							if (cmsVehicleAmmoSuppressiveFire.Enabled)
								cmsVehicleAmmoSuppressiveFire.Text = LanguageManager.Instance.GetString("String_SuppressiveFire").Replace("{0}", objWeapon.Suppressive.ToString());

							List<ListItem> lstAmmo = new List<ListItem>();
							int intCurrentSlot = objWeapon.ActiveAmmoSlot;
							for (int i = 1; i <= objWeapon.AmmoSlots; i++)
							{
								Gear objVehicleGear = new Gear(_objCharacter);
								ListItem objAmmo = new ListItem();
								objWeapon.ActiveAmmoSlot = i;
								objVehicleGear = _objFunctions.FindGear(objWeapon.AmmoLoaded, objCurrentVehicle.Gear);
								objAmmo.Value = i.ToString();

								string strPlugins = "";
								foreach (Vehicle objVehicle in _objCharacter.Vehicles)
								{
									foreach (Gear objCurrentAmmo in objVehicle.Gear)
									{
										if (objCurrentAmmo.InternalId == objWeapon.AmmoLoaded)
										{
											foreach (Gear objChild in objCurrentAmmo.Children)
											{
												strPlugins += objChild.DisplayNameShort + ", ";
											}
										}
									}
								}
								// Remove the trailing comma.
								if (strPlugins != "")
									strPlugins = strPlugins.Substring(0, strPlugins.Length - 2);

								if (objVehicleGear == null)
								{
									if (objWeapon.AmmoRemaining == 0)
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_Empty");
									else
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_ExternalSource");
								}
								else
									objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + objVehicleGear.DisplayNameShort;
								if (strPlugins != "")
									objAmmo.Name += " [" + strPlugins + "]";
								lstAmmo.Add(objAmmo);
							}
							_blnSkipRefresh = true;
							objWeapon.ActiveAmmoSlot = intCurrentSlot;
							cboVehicleWeaponAmmo.Enabled = true;
							cboVehicleWeaponAmmo.ValueMember = "Value";
							cboVehicleWeaponAmmo.DisplayMember = "Name";
							cboVehicleWeaponAmmo.DataSource = lstAmmo;
							cboVehicleWeaponAmmo.SelectedValue = objWeapon.ActiveAmmoSlot.ToString();
							if (cboVehicleWeaponAmmo.SelectedIndex == -1)
								cboVehicleWeaponAmmo.SelectedIndex = 0;
							_blnSkipRefresh = false;
						}

						lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
						lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
						lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
						lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

						lblVehicleName.Text = objWeapon.DisplayNameShort;
						lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeapon");
						lblVehicleAvail.Text = objWeapon.TotalAvail;
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objWeapon.TotalCost);
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";
						lblVehicleSlots.Text = "6 (" + objWeapon.SlotsRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
						string strBook = _objOptions.LanguageBookShort(objWeapon.Source);
						string strPage = objWeapon.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objWeapon.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objWeapon.Page);

						cmdVehicleMoveToInventory.Enabled = true;

						// Determine the Dice Pool size.
						int intPilot = objCurrentVehicle.Pilot;
						int intAutosoft = 0;
						bool blnAutosoftFound = false;
						foreach (Gear objAutosoft in objCurrentVehicle.Gear)
						{
							if (objAutosoft.Category.StartsWith("Autosofts") && objAutosoft.Name == "Targeting")
							{
								if (!blnAutosoftFound)
								{
									if (objAutosoft.Rating > intAutosoft)
										intAutosoft = objAutosoft.Rating;
									if (objAutosoft.Extra == objWeapon.DisplayCategory)
									{
										intAutosoft = objAutosoft.Rating;
										blnAutosoftFound = true;
									}
								}
							}
						}
						if (intAutosoft == 0)
							intPilot -= 1;
						lblVehicleWeaponDicePool.Text = (intPilot + intAutosoft).ToString();
					}
					else
					{
						bool blnCyberware = false;
						// See if this is a piece of Cyberware.
						Cyberware objCyberware = _objFunctions.FindVehicleCyberware(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
						if (objCyberware != null)
							blnCyberware = true;

						if (blnCyberware)
						{
							_blnSkipRefresh = true;
							lblVehicleName.Text = objCyberware.DisplayNameShort;
							lblVehicleRatingLabel.Text = LanguageManager.Instance.GetString("Label_Rating");
							lblVehicleRating.Text = objCyberware.Rating.ToString();
							_blnSkipRefresh = false;

							lblVehicleName.Text = objCyberware.DisplayNameShort;
							lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleModification");
							lblVehicleAvail.Text = objCyberware.TotalAvail;
							lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objCyberware.TotalCost);
							lblVehicleHandling.Text = "";
							lblVehicleAccel.Text = "";
							lblVehicleSpeed.Text = "";
							lblVehicleDevice.Text = "";
							lblVehiclePilot.Text = "";
							lblVehicleBody.Text = "";
							lblVehicleArmor.Text = "";
							lblVehicleSensor.Text = "";
							lblVehicleFirewall.Text = "";
							lblVehicleSignal.Text = "";
							lblVehicleResponse.Text = "";
							lblVehicleSystem.Text = "";
							lblVehicleSlots.Text = "";
							string strBook = _objOptions.LanguageBookShort(objCyberware.Source);
							string strPage = objCyberware.Page;
							lblVehicleSource.Text = strBook + " " + strPage;
							tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objCyberware.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objCyberware.Page);
						}
					}
				}
				chkVehicleWeaponAccessoryInstalled.Enabled = false;
				chkVehicleIncludedInWeapon.Checked = false;
			}
			else if (treVehicles.SelectedNode.Level == 4)
			{
				panVehicleCM.Visible = true;
				bool blnSensorPlugin = false;
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);
				if (objGear != null)
					blnSensorPlugin = true;

				if (blnSensorPlugin)
				{
					lblVehicleRating.Text = "";
					if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
					{
						lblVehicleGearQty.Text = objGear.Quantity.ToString();
						cmdVehicleGearReduceQty.Enabled = true;

						if (objGear.Rating > 0)
							lblVehicleRating.Text = objGear.Rating.ToString();
					}

					lblVehicleName.Text = objGear.DisplayNameShort;
					lblVehicleCategory.Text = objGear.DisplayCategory;
					lblVehicleAvail.Text = objGear.TotalAvail(true);
					lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
					lblVehicleHandling.Text = "";
					lblVehicleAccel.Text = "";
					lblVehicleSpeed.Text = "";
					lblVehicleDevice.Text = "";
					lblVehiclePilot.Text = "";
					lblVehicleBody.Text = "";
					lblVehicleArmor.Text = "";
					lblVehicleSensor.Text = "";
					lblVehicleFirewall.Text = "";
					lblVehicleSignal.Text = "";
					lblVehicleResponse.Text = "";
					lblVehicleSystem.Text = "";
					lblVehicleSlots.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
					string strBook = _objOptions.LanguageBookShort(objGear.Source);
					string strPage = objGear.Page;
					lblVehicleSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

					if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && objGear.GetType() == typeof(Commlink))
					{
						chkVehicleHomeNode.Visible = true;
						chkVehicleHomeNode.Checked = objGear.HomeNode;
					}
				}
				else
				{
					bool blnAccessory = false;

					// Locate the the Selected Vehicle Weapon Accessory of Modification.
					Weapon objWeapon = new Weapon(_objCharacter);
					WeaponAccessory objAccessory = _objFunctions.FindVehicleWeaponAccessory(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
					if (objAccessory != null)
					{
						objWeapon = objAccessory.Parent;
						blnAccessory = true;
					}

					if (blnAccessory)
					{
						lblVehicleName.Text = objAccessory.DisplayNameShort;
						lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeaponAccessory");
						lblVehicleAvail.Text = objAccessory.TotalAvail;
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objAccessory.TotalCost));
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";

						string[] strMounts = objAccessory.Mount.Split('/');
						string strMount = "";
						foreach (string strCurrentMount in strMounts)
						{
							if (strCurrentMount != "")
								strMount += LanguageManager.Instance.GetString("String_Mount" + strCurrentMount) + "/";
						}
						// Remove the trailing /
						if (strMount != "" && strMount.Contains('/'))
							strMount = strMount.Substring(0, strMount.Length - 1);

						lblVehicleSlots.Text = strMount;
						string strBook = _objOptions.LanguageBookShort(objAccessory.Source);
						string strPage = objAccessory.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objAccessory.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objAccessory.Page);
						chkVehicleWeaponAccessoryInstalled.Enabled = true;
						chkVehicleWeaponAccessoryInstalled.Checked = objAccessory.Installed;
						chkVehicleIncludedInWeapon.Checked = objAccessory.IncludedInWeapon;

						lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
						lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
						lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
						lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

						cmdFireVehicleWeapon.Enabled = false;
						cmdReloadVehicleWeapon.Enabled = false;
						cboVehicleWeaponAmmo.Enabled = false;
					}
					else
					{
						bool blnMod = false;
						// Locate the selected Vehicle Weapon Modification.
						WeaponMod objMod = _objFunctions.FindVehicleWeaponMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
						if (objMod != null)
						{
							objWeapon = objMod.Parent;
							blnMod = true;
						}

						if (blnMod)
						{
							lblVehicleName.Text = objMod.DisplayNameShort;
							lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeaponModification");
							lblVehicleAvail.Text = objMod.TotalAvail;
							lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objMod.TotalCost));
							lblVehicleHandling.Text = "";
							lblVehicleAccel.Text = "";
							lblVehicleSpeed.Text = "";
							lblVehicleDevice.Text = "";
							lblVehiclePilot.Text = "";
							lblVehicleBody.Text = "";
							lblVehicleArmor.Text = "";
							lblVehicleSensor.Text = "";
							lblVehicleFirewall.Text = "";
							lblVehicleSignal.Text = "";
							lblVehicleResponse.Text = "";
							lblVehicleSystem.Text = "";
							lblVehicleSlots.Text = objMod.Slots.ToString();
							string strBook = _objOptions.LanguageBookShort(objMod.Source);
							string strPage = objMod.Page;
							lblVehicleSource.Text = strBook + " " + strPage;
							tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objMod.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objMod.Page);
							chkVehicleWeaponAccessoryInstalled.Enabled = true;
							chkVehicleWeaponAccessoryInstalled.Checked = objMod.Installed;
							chkVehicleIncludedInWeapon.Checked = objMod.IncludedInWeapon;

							lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
							lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
							lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
							lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

							cmdFireVehicleWeapon.Enabled = false;
							cmdReloadVehicleWeapon.Enabled = false;
							cboVehicleWeaponAmmo.Enabled = false;
						}
						else
						{
							// If it's none of these, it must be an Underbarrel Weapon.
							Vehicle objCurrentVehicle = new Vehicle(_objCharacter);
							objWeapon = _objFunctions.FindVehicleWeapon(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objCurrentVehicle);

							lblVehicleWeaponName.Text = objWeapon.DisplayNameShort;
							lblVehicleWeaponCategory.Text = objWeapon.DisplayCategory;
							lblVehicleWeaponDamage.Text = objWeapon.CalculatedDamage();
							lblVehicleWeaponAP.Text = objWeapon.TotalAP;
							lblVehicleWeaponAmmo.Text = objWeapon.CalculatedAmmo();
							lblVehicleWeaponMode.Text = objWeapon.CalculatedMode;
							if (objWeapon.WeaponType == "Ranged")
							{
								cmdFireVehicleWeapon.Enabled = true;
								cmdReloadVehicleWeapon.Enabled = true;
								lblVehicleWeaponAmmoRemaining.Text = objWeapon.AmmoRemaining.ToString();

								cmsVehicleAmmoSingleShot.Enabled = objWeapon.AllowMode("SS") || objWeapon.AllowMode("SA");
								cmsVehicleAmmoShortBurst.Enabled = objWeapon.AllowMode("BF");
								cmsVehicleAmmoLongBurst.Enabled = objWeapon.AllowMode("FA");
								cmsVehicleAmmoFullBurst.Enabled = objWeapon.AllowMode("FA");
								cmsVehicleAmmoSuppressiveFire.Enabled = objWeapon.AllowMode("FA");
								if (cmsVehicleAmmoFullBurst.Enabled)
									cmsVehicleAmmoFullBurst.Text = LanguageManager.Instance.GetString("String_FullBurst").Replace("{0}", objWeapon.FullBurst.ToString());
								if (cmsVehicleAmmoSuppressiveFire.Enabled)
									cmsVehicleAmmoSuppressiveFire.Text = LanguageManager.Instance.GetString("String_SuppressiveFire").Replace("{0}", objWeapon.Suppressive.ToString());
							}

							List<ListItem> lstAmmo = new List<ListItem>();
							int intCurrentSlot = objWeapon.ActiveAmmoSlot;
							for (int i = 1; i <= objWeapon.AmmoSlots; i++)
							{
								Gear objVehicleGear = new Gear(_objCharacter);
								ListItem objAmmo = new ListItem();
								objWeapon.ActiveAmmoSlot = i;
								objVehicleGear = _objFunctions.FindGear(objWeapon.AmmoLoaded, objCurrentVehicle.Gear);
								objAmmo.Value = i.ToString();

								string strPlugins = "";
								foreach (Vehicle objVehicle in _objCharacter.Vehicles)
								{
									foreach (Gear objCurrentAmmo in objVehicle.Gear)
									{
										if (objCurrentAmmo.InternalId == objWeapon.AmmoLoaded)
										{
											foreach (Gear objChild in objCurrentAmmo.Children)
											{
												strPlugins += objChild.DisplayNameShort + ", ";
											}
										}
									}
								}
								// Remove the trailing comma.
								if (strPlugins != "")
									strPlugins = strPlugins.Substring(0, strPlugins.Length - 2);

								if (objVehicleGear == null)
								{
									if (objWeapon.AmmoRemaining == 0)
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_Empty");
									else
										objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + LanguageManager.Instance.GetString("String_ExternalSource");
								}
								else
									objAmmo.Name = LanguageManager.Instance.GetString("String_SlotNumber").Replace("{0}", i.ToString()) + " " + objVehicleGear.DisplayNameShort;

								if (strPlugins != "")
									objAmmo.Name += " [" + strPlugins + "]";
								lstAmmo.Add(objAmmo);
							}
							_blnSkipRefresh = true;
							objWeapon.ActiveAmmoSlot = intCurrentSlot;
							cboVehicleWeaponAmmo.Enabled = true;
							cboVehicleWeaponAmmo.ValueMember = "Value";
							cboVehicleWeaponAmmo.DisplayMember = "Name";
							cboVehicleWeaponAmmo.DataSource = lstAmmo;
							cboVehicleWeaponAmmo.SelectedValue = objWeapon.ActiveAmmoSlot.ToString();
							if (cboVehicleWeaponAmmo.SelectedIndex == -1)
								cboVehicleWeaponAmmo.SelectedIndex = 0;
							_blnSkipRefresh = false;

							lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
							lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
							lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
							lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

							lblVehicleName.Text = objWeapon.DisplayNameShort;
							lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeapon");
							lblVehicleAvail.Text = objWeapon.TotalAvail;
							lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objWeapon.TotalCost);
							lblVehicleHandling.Text = "";
							lblVehicleAccel.Text = "";
							lblVehicleSpeed.Text = "";
							lblVehicleDevice.Text = "";
							lblVehiclePilot.Text = "";
							lblVehicleBody.Text = "";
							lblVehicleArmor.Text = "";
							lblVehicleSensor.Text = "";
							lblVehicleFirewall.Text = "";
							lblVehicleSignal.Text = "";
							lblVehicleResponse.Text = "";
							lblVehicleSystem.Text = "";
							lblVehicleSlots.Text = "6 (" + objWeapon.SlotsRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
							string strBook = _objOptions.LanguageBookShort(objWeapon.Source);
							string strPage = objWeapon.Page;
							lblVehicleSource.Text = strBook + " " + strPage;
							_blnSkipRefresh = true;
							chkVehicleWeaponAccessoryInstalled.Enabled = true;
							chkVehicleWeaponAccessoryInstalled.Checked = objWeapon.Installed;
							_blnSkipRefresh = false;
							tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objWeapon.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objWeapon.Page);

							// Determine the Dice Pool size.
							int intPilot = objCurrentVehicle.Pilot;
							int intAutosoft = 0;
							bool blnAutosoftFound = false;
							foreach (Gear objAutosoft in objCurrentVehicle.Gear)
							{
								if (objAutosoft.Category.StartsWith("Autosofts") && objAutosoft.Name == "Targeting")
								{
									if (!blnAutosoftFound)
									{
										if (objAutosoft.Rating > intAutosoft)
											intAutosoft = objAutosoft.Rating;
										if (objAutosoft.Extra == objWeapon.DisplayCategory)
										{
											intAutosoft = objAutosoft.Rating;
											blnAutosoftFound = true;
										}
									}
								}
							}
							if (intAutosoft == 0)
								intPilot -= 1;
							lblVehicleWeaponDicePool.Text = (intPilot + intAutosoft).ToString();
						}
					}
				}
			}
			else if (treVehicles.SelectedNode.Level == 5)
			{
				panVehicleCM.Visible = true;
				bool blnFound = false;

				// Locate the the Selected Vehicle Underbarrel Weapon Accessory or Modification.
				Weapon objWeapon = new Weapon(_objCharacter);
				WeaponAccessory objAccessory = _objFunctions.FindVehicleWeaponAccessory(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
				if (objAccessory != null)
				{
					blnFound = true;
					objWeapon = objAccessory.Parent;
				}

				if (blnFound)
				{
					lblVehicleName.Text = objAccessory.DisplayNameShort;
					lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeaponAccessory");
					lblVehicleAvail.Text = objAccessory.TotalAvail;
					lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objAccessory.TotalCost));
					lblVehicleHandling.Text = "";
					lblVehicleAccel.Text = "";
					lblVehicleSpeed.Text = "";
					lblVehicleDevice.Text = "";
					lblVehiclePilot.Text = "";
					lblVehicleBody.Text = "";
					lblVehicleArmor.Text = "";
					lblVehicleSensor.Text = "";
					lblVehicleFirewall.Text = "";
					lblVehicleSignal.Text = "";
					lblVehicleResponse.Text = "";
					lblVehicleSystem.Text = "";

					string[] strMounts = objAccessory.Mount.Split('/');
					string strMount = "";
					foreach (string strCurrentMount in strMounts)
					{
						if (strCurrentMount != "")
							strMount += LanguageManager.Instance.GetString("String_Mount" + strCurrentMount) + "/";
					}
					// Remove the trailing /
					if (strMount != "" && strMount.Contains('/'))
						strMount = strMount.Substring(0, strMount.Length - 1);

					lblVehicleSlots.Text = strMount;
					string strBook = _objOptions.LanguageBookShort(objAccessory.Source);
					string strPage = objAccessory.Page;
					lblVehicleSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objAccessory.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objAccessory.Page);
					chkVehicleWeaponAccessoryInstalled.Enabled = true;
					chkVehicleWeaponAccessoryInstalled.Checked = objAccessory.Installed;
					chkVehicleIncludedInWeapon.Checked = objAccessory.IncludedInWeapon;

					lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
					lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
					lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
					lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

					cmdFireVehicleWeapon.Enabled = false;
					cmdReloadVehicleWeapon.Enabled = false;
					cboVehicleWeaponAmmo.Enabled = false;
				}
				else
				{
					// Locate the selected Vehicle Weapon Modification.
					WeaponMod objMod = _objFunctions.FindVehicleWeaponMod(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles);
					if (objMod != null)
					{
						blnFound = true;
						objWeapon = objMod.Parent;
					}

					if (blnFound)
					{
						lblVehicleName.Text = objMod.DisplayNameShort;
						lblVehicleCategory.Text = LanguageManager.Instance.GetString("String_VehicleWeaponModification");
						lblVehicleAvail.Text = objMod.TotalAvail;
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", Convert.ToInt32(objMod.TotalCost));
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";
						lblVehicleSlots.Text = objMod.Slots.ToString();
						string strBook = _objOptions.LanguageBookShort(objMod.Source);
						string strPage = objMod.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objMod.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objMod.Page);
						chkVehicleWeaponAccessoryInstalled.Enabled = true;
						chkVehicleWeaponAccessoryInstalled.Checked = objMod.Installed;
						chkVehicleIncludedInWeapon.Checked = objMod.IncludedInWeapon;

						lblVehicleWeaponRangeShort.Text = objWeapon.RangeShort;
						lblVehicleWeaponRangeMedium.Text = objWeapon.RangeMedium;
						lblVehicleWeaponRangeLong.Text = objWeapon.RangeLong;
						lblVehicleWeaponRangeExtreme.Text = objWeapon.RangeExtreme;

						cmdFireVehicleWeapon.Enabled = false;
						cmdReloadVehicleWeapon.Enabled = false;
						cboVehicleWeaponAmmo.Enabled = false;
					}
					else
					{
						panVehicleCM.Visible = true;
						Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
						Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);

						lblVehicleRating.Text = "";
						if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
						{
							lblVehicleGearQty.Text = objGear.Quantity.ToString();
							cmdVehicleGearReduceQty.Enabled = true;

							if (objGear.Rating > 0)
								lblVehicleRating.Text = objGear.Rating.ToString();
						}

						lblVehicleName.Text = objGear.DisplayNameShort;
						lblVehicleCategory.Text = objGear.DisplayCategory;
						lblVehicleAvail.Text = objGear.TotalAvail(true);
						lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
						lblVehicleHandling.Text = "";
						lblVehicleAccel.Text = "";
						lblVehicleSpeed.Text = "";
						lblVehicleDevice.Text = "";
						lblVehiclePilot.Text = "";
						lblVehicleBody.Text = "";
						lblVehicleArmor.Text = "";
						lblVehicleSensor.Text = "";
						lblVehicleFirewall.Text = "";
						lblVehicleSignal.Text = "";
						lblVehicleResponse.Text = "";
						lblVehicleSystem.Text = "";
						lblVehicleSlots.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
						string strBook = _objOptions.LanguageBookShort(objGear.Source);
						string strPage = objGear.Page;
						lblVehicleSource.Text = strBook + " " + strPage;
						tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

						if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && objGear.GetType() == typeof(Commlink))
						{
							chkVehicleHomeNode.Visible = true;
							chkVehicleHomeNode.Checked = objGear.HomeNode;
						}
					}
				}
			}
			else if (treVehicles.SelectedNode.Level > 5)
			{
				panVehicleCM.Visible = true;
				Vehicle objSelectedVehicle = new Vehicle(_objCharacter);
				Gear objGear = _objFunctions.FindVehicleGear(treVehicles.SelectedNode.Tag.ToString(), _objCharacter.Vehicles, out objSelectedVehicle);

				lblVehicleRating.Text = "";
				if (objGear.InternalId == treVehicles.SelectedNode.Tag.ToString())
				{
					lblVehicleGearQty.Text = objGear.Quantity.ToString();
					cmdVehicleGearReduceQty.Enabled = true;

					if (objGear.Rating > 0)
						lblVehicleRating.Text = objGear.Rating.ToString();
				}

				lblVehicleName.Text = objGear.DisplayNameShort;
				lblVehicleCategory.Text = objGear.DisplayCategory;
				lblVehicleAvail.Text = objGear.TotalAvail(true);
				lblVehicleCost.Text = String.Format("{0:###,###,##0¥}", objGear.TotalCost);
				lblVehicleHandling.Text = "";
				lblVehicleAccel.Text = "";
				lblVehicleSpeed.Text = "";
				lblVehicleDevice.Text = "";
				lblVehiclePilot.Text = "";
				lblVehicleBody.Text = "";
				lblVehicleArmor.Text = "";
				lblVehicleSensor.Text = "";
				lblVehicleFirewall.Text = "";
				lblVehicleSignal.Text = "";
				lblVehicleResponse.Text = "";
				lblVehicleSystem.Text = "";
				lblVehicleSlots.Text = objGear.CalculatedCapacity + " (" + objGear.CapacityRemaining.ToString() + " " + LanguageManager.Instance.GetString("String_Remaining") + ")";
				string strBook = _objOptions.LanguageBookShort(objGear.Source);
				string strPage = objGear.Page;
				lblVehicleSource.Text = strBook + " " + strPage;
				tipTooltip.SetToolTip(lblVehicleSource, _objOptions.LanguageBookLong(objGear.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objGear.Page);

				if ((_objCharacter.Metatype.EndsWith("A.I.") || _objCharacter.MetatypeCategory == "Technocritters" || _objCharacter.MetatypeCategory == "Protosapients") && objGear.GetType() == typeof(Commlink))
				{
					chkVehicleHomeNode.Visible = true;
					chkVehicleHomeNode.Checked = objGear.HomeNode;
				}
			}
			else
				panVehicleCM.Visible = false;
		}

		/// <summary>
		/// Populate the Expense Log Lists.
		/// </summary>
		public void PopulateExpenseList()
		{
			lstKarma.Items.Clear();
			lstNuyen.Items.Clear();
			lstKarma.ContextMenuStrip = null;
			lstNuyen.ContextMenuStrip = null;
			foreach (ExpenseLogEntry objExpense in _objCharacter.ExpenseEntries)
			{
				ListViewItem objItem = new ListViewItem();
				objItem.Text = objExpense.Date.ToShortDateString() + " " + objExpense.Date.ToShortTimeString();
				ListViewItem.ListViewSubItem objAmountItem = new ListViewItem.ListViewSubItem();
				objAmountItem.Text = objExpense.Amount.ToString();
				ListViewItem.ListViewSubItem objReasonItem = new ListViewItem.ListViewSubItem();
				objReasonItem.Text = objExpense.Reason;
				ListViewItem.ListViewSubItem objInternalIdItem = new ListViewItem.ListViewSubItem();
				objInternalIdItem.Text = objExpense.InternalId;

				if (objExpense.Type == ExpenseType.Nuyen)
				{
					objAmountItem.Text = string.Format("{0:###,###,##0¥}", objExpense.Amount);
				}

				objItem.SubItems.Add(objAmountItem);
				objItem.SubItems.Add(objReasonItem);
				objItem.SubItems.Add(objInternalIdItem);

				if (objExpense.Type == ExpenseType.Nuyen)
				{
					lstNuyen.Items.Add(objItem);
					if (objExpense.Undo != null)
						lstNuyen.ContextMenuStrip = cmsUndoNuyenExpense;
				}
				else
				{
					lstKarma.Items.Add(objItem);
					if (objExpense.Undo != null)
						lstKarma.ContextMenuStrip = cmsUndoKarmaExpense;
				}
			}
			lstKarma.Sort();
			lstNuyen.Sort();

			// Charting test for Expenses.
			chtKarma.Series.Clear();
			chtNuyen.Series.Clear();

			// Setup the series used for charts.
			Series objKarmaSeries = new Series
			{
				Name = "Series1",
				Color = System.Drawing.Color.Blue,
				IsVisibleInLegend = false,
				IsXValueIndexed = true,
				ChartType = SeriesChartType.Area
			};
			Series objNuyenSeries = new Series
			{
				Name = "Series1",
				Color = System.Drawing.Color.Green,
				IsVisibleInLegend = false,
				IsXValueIndexed = true,
				ChartType = SeriesChartType.Area
			};

			// Configure the Karma chart.
			chtKarma.Series.Add(objKarmaSeries);
			chtKarma.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
			chtKarma.ChartAreas[0].AxisY.Title = "Karma Remaining";
			chtKarma.ChartAreas[0].AxisX.Minimum = 1;

			// Configure the Nuyen chart.
			chtNuyen.Series.Add(objNuyenSeries);
			chtNuyen.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
			chtNuyen.ChartAreas[0].AxisY.Title = "Nuyen Remaining";
			chtNuyen.ChartAreas[0].AxisX.Minimum = 1;

			int intKarmaX = 0;
			int intNuyenX = 0;
			int intKarmaValue = 0;
			int intNuyenValue = 0;
			foreach (ExpenseLogEntry objExpense in _objCharacter.ExpenseEntries)
			{
				if (objExpense.Type == ExpenseType.Karma)
				{
					intKarmaX++;
					intKarmaValue += objExpense.Amount;
					objKarmaSeries.Points.AddXY(intKarmaX, intKarmaValue);
				}
				else
				{
					intNuyenX++;
					intNuyenValue += objExpense.Amount;
					objNuyenSeries.Points.AddXY(intNuyenX, intNuyenValue);
				}
			}
			chtKarma.ChartAreas[0].AxisX.Maximum = intKarmaX;
			chtNuyen.ChartAreas[0].AxisX.Maximum = intNuyenX;
			//chtKarma.ChartAreas[0].AxisX.MaximumAutoSize = 100;
			chtKarma.Invalidate();
			chtNuyen.Invalidate();
		}

		/// <summary>
		/// Populate the Calendar List.
		/// </summary>
		public void PopulateCalendar()
		{
			lstCalendar.Items.Clear();
			for (int i = _objCharacter.Calendar.Count - 1; i >= 0; i--)
			{
				CalendarWeek objWeek = _objCharacter.Calendar[i];
				ListViewItem objItem = new ListViewItem();
				objItem.Text = objWeek.DisplayName;
				ListViewItem.ListViewSubItem objNoteItem = new ListViewItem.ListViewSubItem();
				objNoteItem.Text = objWeek.Notes;
				ListViewItem.ListViewSubItem objInternalIdItem = new ListViewItem.ListViewSubItem();
				objInternalIdItem.Text = objWeek.InternalId;

				objItem.SubItems.Add(objNoteItem);
				objItem.SubItems.Add(objInternalIdItem);

				lstCalendar.Items.Add(objItem);
			}
		}

		/// <summary>
		/// Verify that the user wants to spend their Karma and did not accidentally click the button.
		/// </summary>
		public bool ConfirmKarmaExpense(string strMessage)
		{
			if (!_objOptions.ConfirmKarmaExpense)
				return true;
			else
			{
				if (MessageBox.Show(strMessage, LanguageManager.Instance.GetString("MessageTitle_ConfirmKarmaExpense"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					return false;
				else
					return true;
			}
		}

		/// <summary>
		/// Dummy method to trap the Options MRUChanged Event.
		/// </summary>
		public void PopulateMRU()
		{
		}

		/// <summary>
		/// Update the contents of the Initiation Grade list.
		/// </summary>
		public void UpdateInitiationGradeList()
		{
			lstInitiation.Items.Clear();
			foreach (InitiationGrade objGrade in _objCharacter.InitiationGrades)
				lstInitiation.Items.Add(objGrade.Text);
		}

		/// <summary>
		/// Update the character's Mentor Spirit/Paragon information.
		/// </summary>
		private void UpdateMentorSpirits()
		{
			MentorSpirit objMentor = _objController.MentorInformation(MainController.MentorType.Mentor);
			MentorSpirit objParagon = _objController.MentorInformation(MainController.MentorType.Paragon);

			if (objMentor == null)
			{
				lblMentorSpiritLabel.Visible = false;
				lblMentorSpirit.Visible = false;
				lblMentorSpiritInformation.Visible = false;
			}
			else
			{
				lblMentorSpiritLabel.Visible = true;
				lblMentorSpirit.Visible = true;
				lblMentorSpiritInformation.Visible = true;
				lblMentorSpirit.Text = objMentor.Name;
				lblMentorSpiritInformation.Text = objMentor.Advantages;
			}

			if (objParagon == null)
			{
				lblParagonLabel.Visible = false;
				lblParagon.Visible = false;
				lblParagonInformation.Visible = false;
			}
			else
			{
				lblParagonLabel.Visible = true;
				lblParagon.Visible = true;
				lblParagonInformation.Visible = true;
				lblParagon.Text = objParagon.Name;
				lblParagonInformation.Text = objParagon.Advantages;
			}
		}

		/// <summary>
		/// Set the ToolTips from the Language file.
		/// </summary>
		private void SetTooltips()
		{
			// Common Tab.
			tipTooltip.SetToolTip(lblAttributesBase, LanguageManager.Instance.GetString("Tip_CommonAttributesBase"));
			tipTooltip.SetToolTip(lblAttributesAug, LanguageManager.Instance.GetString("Tip_CommonAttributesAug"));
			tipTooltip.SetToolTip(lblAttributesMetatype, LanguageManager.Instance.GetString("Tip_CommonAttributesMetatypeLimits"));
			tipTooltip.SetToolTip(lblRatingLabel, LanguageManager.Instance.GetString("Tip_CommonAIRating"));
			tipTooltip.SetToolTip(lblSystemLabel, LanguageManager.Instance.GetString("Tip_CommonAISystem"));
			tipTooltip.SetToolTip(lblFirewallLabel, LanguageManager.Instance.GetString("Tip_CommonAIFirewall"));
			tipTooltip.SetToolTip(lblResponseLabel, LanguageManager.Instance.GetString("Tip_CommonAIResponse"));
			tipTooltip.SetToolTip(lblSignalLabel, LanguageManager.Instance.GetString("Tip_CommonAISignal"));
			tipTooltip.SetToolTip(lblContacts, LanguageManager.Instance.GetString("Tip_CommonContacts"));
			tipTooltip.SetToolTip(lblEnemies, LanguageManager.Instance.GetString("Tip_CommonEnemies"));
			tipTooltip.SetToolTip(cmdBurnEdge, LanguageManager.Instance.GetString("Tip_CommonBurnEdge"));
			// Spells Tab.
			tipTooltip.SetToolTip(cmdRollSpell, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			tipTooltip.SetToolTip(cmdRollDrain, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			// Complex Forms Tab.
			tipTooltip.SetToolTip(lblLivingPersonaResponseLabel, LanguageManager.Instance.GetString("Tip_TechnomancerResponse"));
			tipTooltip.SetToolTip(lblLivingPersonaSignalLabel, LanguageManager.Instance.GetString("Tip_TechnomancerSignal"));
			tipTooltip.SetToolTip(lblLivingPersonaSystemLabel, LanguageManager.Instance.GetString("Tip_TechnomancerSystem"));
			tipTooltip.SetToolTip(lblLivingPersonaFirewallLabel, LanguageManager.Instance.GetString("Tip_TechnomancerFirewall"));
			tipTooltip.SetToolTip(lblLivingPersonaBiofeedbackFilterLabel, LanguageManager.Instance.GetString("Tip_TechnomancerBiofeedbackFilter"));
			tipTooltip.SetToolTip(cmdRollComplexForm, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			tipTooltip.SetToolTip(cmdRollFading, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			// Lifestyle Tab.
			tipTooltip.SetToolTip(cmdIncreaseLifestyleMonths, LanguageManager.Instance.GetString("Tab_IncreaseLifestyleMonths"));
			tipTooltip.SetToolTip(cmdDecreaseLifestyleMonths, LanguageManager.Instance.GetString("Tab_DecreaseLifestyleMonths"));
			// Armor Tab.
			tipTooltip.SetToolTip(chkArmorEquipped, LanguageManager.Instance.GetString("Tip_ArmorEquipped"));
			// tipTooltip.SetToolTip(cmdArmorIncrease, LanguageManager.Instance.GetString("Tip_ArmorDegradationAPlus"));
			// tipTooltip.SetToolTip(cmdArmorDecrease, LanguageManager.Instance.GetString("Tip_ArmorDegradationAMinus"));
			// Weapon Tab.
			tipTooltip.SetToolTip(chkWeaponAccessoryInstalled, LanguageManager.Instance.GetString("Tip_WeaponInstalled"));
			tipTooltip.SetToolTip(cmdWeaponBuyAmmo, LanguageManager.Instance.GetString("Tip_BuyAmmo"));
			tipTooltip.SetToolTip(cmdWeaponMoveToVehicle, LanguageManager.Instance.GetString("Tip_TransferToVehicle"));
			tipTooltip.SetToolTip(cmdRollWeapon, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			// Gear Tab.
			tipTooltip.SetToolTip(cmdGearIncreaseQty, LanguageManager.Instance.GetString("Tip_IncreaseGearQty"));
			tipTooltip.SetToolTip(cmdGearReduceQty, LanguageManager.Instance.GetString("Tip_DecreaseGearQty"));
			tipTooltip.SetToolTip(cmdGearSplitQty, LanguageManager.Instance.GetString("Tip_SplitGearQty"));
			tipTooltip.SetToolTip(cmdGearMergeQty, LanguageManager.Instance.GetString("Tip_MergeGearQty"));
			tipTooltip.SetToolTip(cmdGearMoveToVehicle, LanguageManager.Instance.GetString("Tip_TransferToVehicle"));
			tipTooltip.SetToolTip(chkActiveCommlink, LanguageManager.Instance.GetString("Tip_ActiveCommlink"));
			// Vehicles Tab.
			tipTooltip.SetToolTip(chkVehicleWeaponAccessoryInstalled, LanguageManager.Instance.GetString("Tip_WeaponInstalled"));
			tipTooltip.SetToolTip(cmdVehicleGearReduceQty, LanguageManager.Instance.GetString("Tip_DecreaseGearQty"));
			tipTooltip.SetToolTip(cmdVehicleMoveToInventory, LanguageManager.Instance.GetString("Tip_TransferToInventory"));
			tipTooltip.SetToolTip(cmdRollVehicleWeapon, LanguageManager.Instance.GetString("Tip_DiceRoller"));
			// Other Info Tab.
			tipTooltip.SetToolTip(lblCMPhysicalLabel, LanguageManager.Instance.GetString("Tip_OtherCMPhysical"));
			tipTooltip.SetToolTip(lblCMStunLabel, LanguageManager.Instance.GetString("Tip_OtherCMStun"));
			tipTooltip.SetToolTip(lblINILabel, LanguageManager.Instance.GetString("Tip_OtherInitiative"));
			tipTooltip.SetToolTip(lblIPLabel, LanguageManager.Instance.GetString("Tip_OtherInitiativePasses"));
			tipTooltip.SetToolTip(lblMatrixINILabel, LanguageManager.Instance.GetString("Tip_OtherMatrixInitiative"));
			tipTooltip.SetToolTip(lblMatrixIPLabel, LanguageManager.Instance.GetString("Tip_OtherMatrixInitiativePasses"));
			tipTooltip.SetToolTip(lblAstralINILabel, LanguageManager.Instance.GetString("Tip_OtherAstralInitiative"));
			tipTooltip.SetToolTip(lblAstralIPLabel, LanguageManager.Instance.GetString("Tip_OtherAstralInitiativePasses"));
			tipTooltip.SetToolTip(lblArmorLabel, LanguageManager.Instance.GetString("Tip_OtherArmor"));
			tipTooltip.SetToolTip(lblESS, LanguageManager.Instance.GetString("Tip_OtherEssence"));
			tipTooltip.SetToolTip(lblRemainingNuyenLabel, LanguageManager.Instance.GetString("Tip_OtherNuyen"));
			tipTooltip.SetToolTip(lblCareerKarmaLabel, LanguageManager.Instance.GetString("Tip_OtherCareerKarma"));
			tipTooltip.SetToolTip(lblMovementLabel, LanguageManager.Instance.GetString("Tip_OtherMovement"));
			tipTooltip.SetToolTip(lblSwimLabel, LanguageManager.Instance.GetString("Tip_OtherSwim"));
			tipTooltip.SetToolTip(lblFlyLabel, LanguageManager.Instance.GetString("Tip_OtherFly"));
			tipTooltip.SetToolTip(lblComposureLabel, LanguageManager.Instance.GetString("Tip_OtherComposure"));
			tipTooltip.SetToolTip(lblJudgeIntentionsLabel, LanguageManager.Instance.GetString("Tip_OtherJudgeIntentions"));
			tipTooltip.SetToolTip(lblLiftCarryLabel, LanguageManager.Instance.GetString("Tip_OtherLiftAndCarry"));
			tipTooltip.SetToolTip(lblMemoryLabel, LanguageManager.Instance.GetString("Tip_OtherMemory"));
			// Condition Monitor Tab.
			tipTooltip.SetToolTip(lblCMPenaltyLabel, LanguageManager.Instance.GetString("Tip_CMCMPenalty"));
			tipTooltip.SetToolTip(lblCMArmorLabel, LanguageManager.Instance.GetString("Tip_OtherArmor"));
			tipTooltip.SetToolTip(lblCMDamageResistancePoolLabel, LanguageManager.Instance.GetString("Tip_CMDamageResistance"));
			tipTooltip.SetToolTip(cmdEdgeGained, LanguageManager.Instance.GetString("Tip_CMRegainEdge"));
			tipTooltip.SetToolTip(cmdEdgeSpent, LanguageManager.Instance.GetString("Tip_CMSpendEdge"));
			// Common Info Tab.
			tipTooltip.SetToolTip(lblStreetCred, LanguageManager.Instance.GetString("Tip_StreetCred"));
			tipTooltip.SetToolTip(lblNotoriety, LanguageManager.Instance.GetString("Tip_Notoriety"));
			tipTooltip.SetToolTip(lblPublicAware, LanguageManager.Instance.GetString("Tip_PublicAwareness"));
			tipTooltip.SetToolTip(cmdBurnStreetCred, LanguageManager.Instance.GetString("Tip_BurnStreetCred"));

			// Attribute Labels.
			lblBODLabel.Text = LanguageManager.Instance.GetString("String_AttributeBODLong") + " (" + LanguageManager.Instance.GetString("String_AttributeBODShort") + ")";
			lblAGILabel.Text = LanguageManager.Instance.GetString("String_AttributeAGILong") + " (" + LanguageManager.Instance.GetString("String_AttributeAGIShort") + ")";
			lblREALabel.Text = LanguageManager.Instance.GetString("String_AttributeREALong") + " (" + LanguageManager.Instance.GetString("String_AttributeREAShort") + ")";
			lblSTRLabel.Text = LanguageManager.Instance.GetString("String_AttributeSTRLong") + " (" + LanguageManager.Instance.GetString("String_AttributeSTRShort") + ")";
			lblCHALabel.Text = LanguageManager.Instance.GetString("String_AttributeCHALong") + " (" + LanguageManager.Instance.GetString("String_AttributeCHAShort") + ")";
			lblINTLabel.Text = LanguageManager.Instance.GetString("String_AttributeINTLong") + " (" + LanguageManager.Instance.GetString("String_AttributeINTShort") + ")";
			lblLOGLabel.Text = LanguageManager.Instance.GetString("String_AttributeLOGLong") + " (" + LanguageManager.Instance.GetString("String_AttributeLOGShort") + ")";
			lblWILLabel.Text = LanguageManager.Instance.GetString("String_AttributeWILLong") + " (" + LanguageManager.Instance.GetString("String_AttributeWILShort") + ")";
			lblEDGLabel.Text = LanguageManager.Instance.GetString("String_AttributeEDGLong") + " (" + LanguageManager.Instance.GetString("String_AttributeEDGShort") + ")";
			lblMAGLabel.Text = LanguageManager.Instance.GetString("String_AttributeMAGLong") + " (" + LanguageManager.Instance.GetString("String_AttributeMAGShort") + ")";
			lblRESLabel.Text = LanguageManager.Instance.GetString("String_AttributeRESLong") + " (" + LanguageManager.Instance.GetString("String_AttributeRESShort") + ")";

			// Reposition controls based on their new sizes.
			// Common Tab.
			txtAlias.Left = lblAlias.Left + lblAlias.Width + 6;
			txtAlias.Width = lblMetatypeLabel.Left - 6 - txtAlias.Left;
			cmdSwapQuality.Left = cmdAddQuality.Left + cmdAddQuality.Width + 6;
			cmdDeleteQuality.Left = cmdSwapQuality.Left + cmdSwapQuality.Width + 6;
			// Skills Tab.
			cboSkillFilter.Left = cmdAddExoticSkill.Left - 6 - cboSkillFilter.Width;
			// Martial Arts Tab.
			cmdAddManeuver.Left = cmdAddMartialArt.Left + cmdAddMartialArt.Width + 6;
			cmdDeleteMartialArt.Left = cmdAddManeuver.Left + cmdAddManeuver.Width + 6;
			// Magician Tab.
			cmdDeleteSpell.Left = cmdAddSpell.Left + cmdAddSpell.Width + 6;
			// Technomancer Tab.
			cmdDeleteComplexForm.Left = cmdAddComplexForm.Left + cmdAddComplexForm.Width + 6;
			// Critter Powers Tab.
			cmdDeleteCritterPower.Left = cmdAddCritterPower.Left + cmdAddCritterPower.Width + 6;
			// Initiation Tab.
			cmdDeleteMetamagic.Left = cmdAddMetamagic.Left + cmdAddMetamagic.Width + 6;
			// Cyberware Tab.
			cmdAddBioware.Left = cmdAddCyberware.Left + cmdAddCyberware.Width + 6;
			cmdDeleteCyberware.Left = cmdAddBioware.Left + cmdAddBioware.Width + 6;
			// Lifestyle Tab.
			cmdDeleteLifestyle.Left = cmdAddLifestyle.Left + cmdAddLifestyle.Width + 6;
			// Armor Tab.
			cmdDeleteArmor.Left = cmdAddArmor.Left + cmdAddArmor.Width + 6;
			cmdAddArmorBundle.Left = cmdDeleteArmor.Left + cmdDeleteArmor.Width + 6;
			cmdArmorEquipAll.Left = chkArmorEquipped.Left + chkArmorEquipped.Width + 6;
			cmdArmorUnEquipAll.Left = cmdArmorEquipAll.Left + cmdArmorEquipAll.Width + 6;
			// Weapons Tab.
			cmdDeleteWeapon.Left = cmdAddWeapon.Left + cmdAddWeapon.Width + 6;
			cmdAddWeaponLocation.Left = cmdDeleteWeapon.Left + cmdDeleteWeapon.Width + 6;
			// Gear Tab.
			cmdDeleteGear.Left = cmdAddGear.Left + cmdAddGear.Width + 6;
			cmdAddLocation.Left = cmdDeleteGear.Left + cmdDeleteGear.Width + 6;
			// Vehicle Tab.
			cmdDeleteVehicle.Left = cmdAddVehicle.Left + cmdAddVehicle.Width + 6;
			cmdAddVehicleLocation.Left = cmdDeleteVehicle.Left + cmdDeleteVehicle.Width + 6;
			// Expense Tab.
			cmdKarmaSpent.Left = cmdKarmaGained.Left + cmdKarmaGained.Width + 6;
			cmdNuyenSpent.Left = cmdNuyenGained.Left + cmdNuyenGained.Width + 6;
			// Improvements Tab.
			cmdImprovementsEnableAll.Left = chkImprovementActive.Left + chkImprovementActive.Width + 6;
			cmdImprovementsDisableAll.Left = cmdImprovementsEnableAll.Left + cmdImprovementsEnableAll.Width + 6;
		}

		/// <summary>
		/// Refresh the list of Improvements.
		/// </summary>
		private void RefreshImprovements()
		{
			treImprovements.Nodes.Clear();

			TreeNode objRoot = new TreeNode();
			objRoot.Tag = null;
			objRoot.Text = LanguageManager.Instance.GetString("Node_SelectedImprovements");
			treImprovements.Nodes.Add(objRoot);

			// Populate the Locations.
			foreach (string strGroup in _objCharacter.ImprovementGroups)
			{
				TreeNode objGroup = new TreeNode();
				objGroup.Tag = strGroup;
				objGroup.Text = strGroup;
				objGroup.ContextMenuStrip = cmsImprovementLocation;
				treImprovements.Nodes.Add(objGroup);
			}

			List<ListItem> lstImprovements = new List<ListItem>();
			foreach (Improvement objImprovement in _objCharacter.Improvements)
			{
				if (objImprovement.ImproveSource == Improvement.ImprovementSource.Custom)
				{
					string strName = "000000";
					strName = strName.Substring(0, 6 - objImprovement.SortOrder.ToString().Length) + objImprovement.SortOrder.ToString();
					ListItem objItem = new ListItem();
					objItem.Value = objImprovement.SourceName;
					objItem.Name = strName;
					lstImprovements.Add(objItem);
				}
			}

			// Populate the Improvements TreeView.
			int i = -1;
			foreach (ListItem objItem in lstImprovements)
			{
				i++;
				Improvement objImprovement = new Improvement();
				foreach (Improvement objCharacterImprovement in _objCharacter.Improvements)
				{
					if (objCharacterImprovement.SourceName == objItem.Value)
					{
						objImprovement = objCharacterImprovement;
						break;
					}
				}

				TreeNode nodImprovement = new TreeNode();
				nodImprovement.Tag = objImprovement.SourceName;
				nodImprovement.Text = objImprovement.CustomName;
				if (objImprovement.Notes != string.Empty)
				{
					if (objImprovement.Enabled)
						nodImprovement.ForeColor = Color.SaddleBrown;
					else
						nodImprovement.ForeColor = Color.SandyBrown;
				}
				else
				{
					if (objImprovement.Enabled)
						nodImprovement.ForeColor = SystemColors.WindowText;
					else
						nodImprovement.ForeColor = SystemColors.GrayText;
				}
				nodImprovement.ToolTipText = objImprovement.Notes;
				nodImprovement.ContextMenuStrip = cmsImprovement;

				TreeNode objParent = new TreeNode();
				if (objImprovement.CustomGroup == "")
					objParent = treImprovements.Nodes[0];
				else
				{
					foreach (TreeNode objFind in treImprovements.Nodes)
					{
						if (objFind.Text == objImprovement.CustomGroup)
						{
							objParent = objFind;
							break;
						}
					}
				}

				objParent.Nodes.Add(nodImprovement);
				objParent.Expand();
			}

			// Sort the list of Custom Improvements in alphabetical order based on their Custom Name within each Group.
			_objFunctions.SortTree(treImprovements);
		}

		private void MoveControls()
		{
			int intWidth = 0;

			// Common tab.
			lblAlias.Left = Math.Max(288, cmdDeleteQuality.Left + cmdDeleteQuality.Width + 6);
			txtAlias.Left = lblAlias.Left + lblAlias.Width + 6;
			txtAlias.Width = lblMetatypeLabel.Left - txtAlias.Left - 6;

			intWidth = Math.Max(lblRatingLabel.Width, lblSystemLabel.Width);
			intWidth = Math.Max(intWidth, lblFirewallLabel.Width);
			intWidth = Math.Max(intWidth, lblResponseLabel.Width);
			intWidth = Math.Max(intWidth, lblSignalLabel.Width);

			lblRating.Left = lblRatingLabel.Left + intWidth + 6;
			lblSystem.Left = lblSystemLabel.Left + intWidth + 6;
			lblFirewall.Left = lblFirewallLabel.Left + intWidth + 6;
			nudResponse.Left = lblResponseLabel.Left + intWidth + 6;
			nudSignal.Left = lblSignalLabel.Left + intWidth + 6;

			// Skills tab.

			// Martial Arts tab.
			intWidth = Math.Max(lblMartialArtsRatingLabel.Width, lblMartialArtSourceLabel.Width);
			lblMartialArtsRating.Left = lblMartialArtsRatingLabel.Left + intWidth + 6;
			cmdImproveMartialArtsRating.Left = lblMartialArtsRating.Left + lblMartialArtsRating.Width + 6;
			lblMartialArtSource.Left = lblMartialArtSourceLabel.Left + intWidth + 6;

			// Spells and Spirits tab.
			intWidth = Math.Max(lblSpellDescriptorsLabel.Width, lblSpellCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblSpellRangeLabel.Width);
			intWidth = Math.Max(intWidth, lblSpellDurationLabel.Width);
			intWidth = Math.Max(intWidth, lblSpellSourceLabel.Width);
			intWidth = Math.Max(intWidth, lblSpellDicePoolLabel.Width);

			lblSpellDescriptors.Left = lblSpellDescriptorsLabel.Left + intWidth + 6;
			lblSpellCategory.Left = lblSpellCategoryLabel.Left + intWidth + 6;
			lblSpellRange.Left = lblSpellRangeLabel.Left + intWidth + 6;
			lblSpellDuration.Left = lblSpellDurationLabel.Left + intWidth + 6;
			lblSpellSource.Left = lblSpellSourceLabel.Left + intWidth + 6;
			lblSpellDicePool.Left = lblSpellDicePoolLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblSpellTypeLabel.Width, lblSpellDamageLabel.Width);
			intWidth = Math.Max(intWidth, lblSpellDVLabel.Width);
			lblSpellTypeLabel.Left = lblSpellCategoryLabel.Left + 179;
			lblSpellType.Left = lblSpellTypeLabel.Left + intWidth + 6;
			lblSpellDamageLabel.Left = lblSpellRangeLabel.Left + 179;
			lblSpellDamage.Left = lblSpellDamageLabel.Left + intWidth + 6;
			lblSpellDVLabel.Left = lblSpellDurationLabel.Left + 179;
			lblSpellDV.Left = lblSpellDVLabel.Left + intWidth + 6;
			cmdQuickenSpell.Left = lblSpellDVLabel.Left;

			intWidth = Math.Max(lblTraditionLabel.Width, lblDrainAttributesLabel.Width);
			intWidth = Math.Max(intWidth, lblMentorSpiritLabel.Width);
			cboTradition.Left = lblTraditionLabel.Left + intWidth + 6;
			lblDrainAttributes.Left = lblDrainAttributesLabel.Left + intWidth + 6;
			lblDrainAttributesValue.Left = lblDrainAttributes.Left + 91;
			lblMentorSpirit.Left = lblMentorSpiritLabel.Left + intWidth + 6;

			cmdRollSpell.Left = lblSpellDicePool.Left + lblSpellDicePool.Width + 6;
			cmdRollDrain.Left = lblDrainAttributesValue.Left + lblDrainAttributesValue.Width + 6;
			cmdRollSpell.Visible = _objOptions.AllowSkillDiceRolling;
			cmdRollDrain.Visible = _objOptions.AllowSkillDiceRolling;

			// Adept Powers tab.
			lblPowerPoints.Left = lblPowerPointsLabel.Left + lblPowerPointsLabel.Width + 6;

			// Sprites and Complex Forms tab.
			intWidth = Math.Max(lblComplexFormRatingLabel.Width, lblComplexFormSkillLabel.Width);
			intWidth = Math.Max(intWidth, lblComplexFormSourceLabel.Width);
			intWidth = Math.Max(intWidth, lblComplexFormDicePoolLabel.Width);
			lblComplexFormRating.Left = lblComplexFormRatingLabel.Left + intWidth + 6;
			cmdImproveComplexForm.Left = lblComplexFormRating.Left + lblComplexFormRating.Width + 6;
			lblComplexFormSkill.Left = lblComplexFormSkillLabel.Left + intWidth + 6;
			lblComplexFormSource.Left = lblComplexFormSourceLabel.Left + intWidth + 6;
			cboComplexFormSkill.Left = lblComplexFormDicePoolLabel.Left + intWidth + 6;
			if (!_objOptions.AlternateMatrixAttribute)
			{
				cboComplexFormAttribute.Enabled = false;
				cboComplexFormAttribute.Visible = false;
				lblComplexFormDicePool.Left = cboComplexFormSkill.Left + cboComplexFormSkill.Width + 6;
			}
			else
			{
				cboComplexFormAttribute.Visible = true;
				cboComplexFormAttribute.Left = cboComplexFormSkill.Left + cboComplexFormSkill.Width + 6;
				lblComplexFormDicePool.Left = cboComplexFormAttribute.Left + cboComplexFormAttribute.Width + 6;
			}

			intWidth = Math.Max(lblStreamLabel.Width, lblFadingAttributesLabel.Width);
			intWidth = Math.Max(intWidth, lblParagonLabel.Width);
			cboStream.Left = lblStreamLabel.Left + intWidth + 6;
			lblFadingAttributes.Left = lblFadingAttributesLabel.Left + intWidth + 6;
			lblFadingAttributesValue.Left = lblFadingAttributes.Left + 91;
			lblParagon.Left = lblParagonLabel.Left + intWidth + 6;

			lblLivingPersonaResponse.Left = lblLivingPersonaResponseLabel.Left + lblLivingPersonaResponseLabel.Width + 6;
			lblLivingPersonaSignalLabel.Left = lblLivingPersonaResponse.Left + 35;
			lblLivingPersonaSignal.Left = lblLivingPersonaSignalLabel.Left + lblLivingPersonaSignalLabel.Width + 6;
			lblLivingPersonaSystemLabel.Left = lblLivingPersonaSignal.Left + 35;
			lblLivingPersonaSystem.Left = lblLivingPersonaSystemLabel.Left + lblLivingPersonaSystemLabel.Width + 6;
			lblLivingPersonaFirewallLabel.Left = lblLivingPersonaSystem.Left + 35;
			lblLivingPersonaFirewall.Left = lblLivingPersonaFirewallLabel.Left + lblLivingPersonaFirewallLabel.Width + 6;
			lblLivingPersonaBiofeedbackFilterLabel.Left = lblLivingPersonaFirewall.Left + 35;
			lblLivingPersonaBiofeedbackFilter.Left = lblLivingPersonaBiofeedbackFilterLabel.Left + lblLivingPersonaBiofeedbackFilterLabel.Width + 6;

			if (_objOptions.AlternateComplexFormCost)
			{
				nudComplexFormRating.Visible = true;
				lblComplexFormRating.Visible = false;
				cmdImproveComplexForm.Visible = false;
			}

			cmdRollComplexForm.Left = lblComplexFormDicePool.Left + lblComplexFormDicePool.Width + 6;
			cmdRollFading.Left = lblFadingAttributesValue.Left + lblFadingAttributesValue.Width + 6;
			cmdRollComplexForm.Visible = _objOptions.AllowSkillDiceRolling;
			cmdRollFading.Visible = _objOptions.AllowSkillDiceRolling;

			// Critter Powers tab.
			lblCritterPowerPointsLabel.Left = cmdDeleteCritterPower.Left + cmdDeleteCritterPower.Width + 16;
			lblCritterPowerPoints.Left = lblCritterPowerPointsLabel.Left + lblCritterPowerPointsLabel.Width + 6;

			intWidth = Math.Max(lblCritterPowerNameLabel.Width, lblCritterPowerCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerTypeLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerActionLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerRangeLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerDurationLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerSourceLabel.Width);
			intWidth = Math.Max(intWidth, lblCritterPowerPointCostLabel.Width);

			lblCritterPowerName.Left = lblCritterPowerNameLabel.Left + intWidth + 6;
			lblCritterPowerCategory.Left = lblCritterPowerCategoryLabel.Left + intWidth + 6;
			lblCritterPowerType.Left = lblCritterPowerTypeLabel.Left + intWidth + 6;
			lblCritterPowerAction.Left = lblCritterPowerActionLabel.Left + intWidth + 6;
			lblCritterPowerRange.Left = lblCritterPowerRangeLabel.Left + intWidth + 6;
			lblCritterPowerDuration.Left = lblCritterPowerDurationLabel.Left + intWidth + 6;
			lblCritterPowerSource.Left = lblCritterPowerSourceLabel.Left + intWidth + 6;
			lblCritterPowerPointCost.Left = lblCritterPowerPointCostLabel.Left + intWidth + 6;

			// Initiation and Submersion tab.

			// Cyberware and Bioware tab.
			intWidth = Math.Max(lblCyberwareNameLabel.Width, lblCyberwareCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblCyberwareGradeLabel.Width);
			intWidth = Math.Max(intWidth, lblCyberwareEssenceLabel.Width);
			intWidth = Math.Max(intWidth, lblCyberwareAvailLabel.Width);
			intWidth = Math.Max(intWidth, lblCyberwareSourceLabel.Width);

			lblCyberwareName.Left = lblCyberwareNameLabel.Left + intWidth + 6;
			lblCyberwareCategory.Left = lblCyberwareCategoryLabel.Left + intWidth + 6;
			lblCyberwareGrade.Left = lblCyberwareGradeLabel.Left + intWidth + 6;
			lblCyberwareEssence.Left = lblCyberwareEssenceLabel.Left + intWidth + 6;
			lblCyberwareAvail.Left = lblCyberwareAvailLabel.Left + intWidth + 6;
			lblCyberwareSource.Left = lblCyberwareSourceLabel.Left + intWidth + 6;

			intWidth = lblEssenceHoleESSLabel.Width;
			lblCyberwareESS.Left = lblEssenceHoleESSLabel.Left + intWidth + 6;
			lblBiowareESS.Left = lblEssenceHoleESSLabel.Left + intWidth + 6;
			lblEssenceHoleESS.Left = lblEssenceHoleESSLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblCyberwareRatingLabel.Width, lblCyberwareCapacityLabel.Width);
			intWidth = Math.Max(intWidth, lblCyberwareCostLabel.Width);

			lblCyberwareRatingLabel.Left = lblCyberwareName.Left + 208;
			lblCyberwareRating.Left = lblCyberwareRatingLabel.Left + intWidth + 6;
			lblCyberwareCapacityLabel.Left = lblCyberwareName.Left + 208;
			lblCyberwareCapacity.Left = lblCyberwareCapacityLabel.Left + intWidth + 6;
			lblCyberwareCostLabel.Left = lblCyberwareName.Left + 208;
			lblCyberwareCost.Left = lblCyberwareCostLabel.Left + intWidth + 6;

			// Street Gear tab.
			// Lifestyles tab.
			lblLifestyleCost.Left = lblLifestyleCostLabel.Left + lblLifestyleCostLabel.Width + 6;
			lblLifestyleSource.Left = lblLifestyleSourceLabel.Left + lblLifestyleSourceLabel.Width + 6;

			intWidth = Math.Max(lblLifestyleComfortsLabel.Width, lblLifestyleEntertainmentLabel.Width);
			intWidth = Math.Max(intWidth, lblLifestyleNecessitiesLabel.Width);
			intWidth = Math.Max(intWidth, lblLifestyleNeighborhoodLabel.Width);
			intWidth = Math.Max(intWidth, lblLifestyleSecurityLabel.Width);

			lblLifestyleComforts.Left = lblLifestyleComfortsLabel.Left + intWidth + 6;
			lblLifestyleEntertainment.Left = lblLifestyleEntertainmentLabel.Left + intWidth + 6;
			lblLifestyleNecessities.Left = lblLifestyleNecessitiesLabel.Left + intWidth + 6;
			lblLifestyleNeighborhood.Left = lblLifestyleNeighborhoodLabel.Left + intWidth + 6;
			lblLifestyleSecurity.Left = lblLifestyleSecurityLabel.Left + intWidth + 6;

			lblLifestyleQualitiesLabel.Left = lblLifestyleComforts.Left + 132;
			lblLifestyleQualities.Left = lblLifestyleQualitiesLabel.Left + 14;
			lblLifestyleQualities.Width = tabLifestyle.Width - lblLifestyleQualities.Left - 10;

			// Armor tab.
			intWidth = lblArmorLabel.Width;
			intWidth = Math.Max(intWidth, lblArmorRatingLabel.Width);
			intWidth = Math.Max(intWidth, lblArmorCapacityLabel.Width);
			intWidth = Math.Max(intWidth, lblArmorSourceLabel.Width);

			lblArmor.Left = lblArmorLabel.Left + intWidth + 6;
			lblArmorRating.Left = lblArmorRatingLabel.Left + intWidth + 6;
			lblArmorCapacity.Left = lblArmorCapacityLabel.Left + intWidth + 6;
			lblArmorSource.Left = lblArmorSourceLabel.Left + intWidth + 6;

			lblArmorAvailLabel.Left = lblArmorRating.Left + lblArmorRating.Width + 6;
			lblArmorAvail.Left = lblArmorAvailLabel.Left + lblArmorAvailLabel.Width + 6;

			lblArmorCostLabel.Left = lblArmorAvail.Left + lblArmorAvail.Width + 6;
			lblArmorCost.Left = lblArmorCostLabel.Left + lblArmorCostLabel.Width + 6;

			cmdArmorIncrease.Left = lblArmor.Left + 45;
			cmdArmorDecrease.Left = cmdArmorIncrease.Left + cmdArmorIncrease.Width + 6;

			// Weapons tab.
			lblWeaponName.Left = lblWeaponNameLabel.Left + lblWeaponNameLabel.Width + 6;
			lblWeaponCategory.Left = lblWeaponCategoryLabel.Left + lblWeaponCategoryLabel.Width + 6;

			intWidth = Math.Max(lblWeaponNameLabel.Width, lblWeaponCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponDamageLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponReachLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponAvailLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponSlotsLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponSourceLabel.Width);

			lblWeaponName.Left = lblWeaponNameLabel.Left + intWidth + 6;
			lblWeaponCategory.Left = lblWeaponCategoryLabel.Left + intWidth + 6;
			lblWeaponDamage.Left = lblWeaponDamageLabel.Left + intWidth + 6;
			lblWeaponReach.Left = lblWeaponReachLabel.Left + intWidth + 6;
			lblWeaponAvail.Left = lblWeaponAvailLabel.Left + intWidth + 6;
			lblWeaponSlots.Left = lblWeaponSlotsLabel.Left + intWidth + 6;
			lblWeaponSource.Left = lblWeaponSourceLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblWeaponRCLabel.Width, lblWeaponModeLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponCostLabel.Width);

			lblWeaponRCLabel.Left = lblWeaponDamageLabel.Left + 176;
			lblWeaponRC.Left = lblWeaponRCLabel.Left + intWidth + 6;
			lblWeaponModeLabel.Left = lblWeaponDamageLabel.Left + 176;
			lblWeaponMode.Left = lblWeaponModeLabel.Left + intWidth + 6;
			lblWeaponCostLabel.Left = lblWeaponDamageLabel.Left + 176;
			lblWeaponCost.Left = lblWeaponCostLabel.Left + intWidth + 6;
			chkIncludedInWeapon.Left = lblWeaponDamageLabel.Left + 176;
			cmdWeaponMoveToVehicle.Left = lblWeaponDamageLabel.Left + 176;

			intWidth = Math.Max(lblWeaponAPLabel.Width, lblWeaponAmmoLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponConcealLabel.Width);

			lblWeaponAPLabel.Left = lblWeaponRC.Left + 95;
			lblWeaponAP.Left = lblWeaponAPLabel.Left + intWidth + 6;
			lblWeaponAmmoLabel.Left = lblWeaponRC.Left + 95;
			lblWeaponAmmo.Left = lblWeaponAmmoLabel.Left + intWidth + 6;
			lblWeaponConcealLabel.Left = lblWeaponRC.Left + 95;
			lblWeaponConceal.Left = lblWeaponConcealLabel.Left + intWidth + 6;
			chkWeaponAccessoryInstalled.Left = lblWeaponRC.Left + 95;

			intWidth = Math.Max(lblWeaponAmmoRemainingLabel.Width, lblWeaponAmmoTypeLabel.Width);
			intWidth = Math.Max(intWidth, lblWeaponDicePoolLabel.Width);

			lblWeaponAmmoRemaining.Left = lblWeaponAmmoRemainingLabel.Left + intWidth + 6;
			cboWeaponAmmo.Left = lblWeaponAmmoTypeLabel.Left + intWidth + 6;
			lblWeaponDicePool.Left = lblWeaponDicePoolLabel.Left + intWidth + 6;

			cmdFireWeapon.Left = lblWeaponAmmoRemaining.Left + 123;
			cmdReloadWeapon.Left = cmdFireWeapon.Left + cmdFireWeapon.Width + 6;
			cmdWeaponBuyAmmo.Left = cmdReloadWeapon.Left + cmdReloadWeapon.Width + 6;

			cmdRollWeapon.Left = lblWeaponDicePool.Left + lblWeaponDicePool.Width + 6;
			cmdRollWeapon.Visible = _objOptions.AllowSkillDiceRolling;

			// Gear tab.
			intWidth = Math.Max(lblGearNameLabel.Width, lblGearCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblGearRatingLabel.Width);
			intWidth = Math.Max(intWidth, lblGearCapacityLabel.Width);
			intWidth = Math.Max(intWidth, lblGearQtyLabel.Width);

			chkCommlinks.Left = cmdAddLocation.Left + cmdAddLocation.Width + 16;

			lblGearName.Left = lblGearNameLabel.Left + intWidth + 6;
			lblGearCategory.Left = lblGearCategoryLabel.Left + intWidth + 6;
			lblGearRating.Left = lblGearRatingLabel.Left + intWidth + 6;
			lblGearCapacity.Left = lblGearCapacityLabel.Left + intWidth + 6;
			lblGearQty.Left = lblGearQtyLabel.Left + intWidth + 6;

			lblGearAvailLabel.Left = lblGearRating.Left + 52;
			lblGearAvail.Left = lblGearAvailLabel.Left + lblGearAvailLabel.Width + 6;
			lblGearCostLabel.Left = lblGearAvail.Left + 75;
			lblGearCost.Left = lblGearCostLabel.Left + lblGearCostLabel.Width + 6;

			cmdGearIncreaseQty.Left = lblGearQty.Left + 57;
			cmdGearReduceQty.Left = cmdGearIncreaseQty.Left + cmdGearIncreaseQty.Width + 6;
			cmdGearSplitQty.Left = cmdGearReduceQty.Left + 79;
			cmdGearMergeQty.Left = cmdGearSplitQty.Left + cmdGearSplitQty.Width + 6;
			cmdGearMoveToVehicle.Left = cmdGearMergeQty.Left + 56;

			intWidth = Math.Max(lblGearResponseLabel.Width, lblGearDamageLabel.Width);
			lblGearResponse.Left = lblGearResponseLabel.Left + intWidth + 6;
			lblGearDamage.Left = lblGearDamageLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblGearSignalLabel.Width, lblGearAPLabel.Width);
			lblGearSignalLabel.Left = lblGearResponse.Left + lblGearResponse.Width + 16;
			lblGearSignal.Left = lblGearSignalLabel.Left + intWidth + 6;
			lblGearAPLabel.Left = lblGearResponse.Left + lblGearResponse.Width + 16;
			lblGearAP.Left = lblGearAPLabel.Left + intWidth + 6;

			lblGearSystemLabel.Left = lblGearSignal.Left + lblGearSignal.Width + 16;
			lblGearSystem.Left = lblGearSystemLabel.Left + lblGearSystemLabel.Width + 6;
			lblGearFirewallLabel.Left = lblGearSystem.Left + lblGearSystem.Width + 16;
			lblGearFirewall.Left = lblGearFirewallLabel.Left + lblGearFirewallLabel.Width + 6;

			lblGearSource.Left = lblGearSourceLabel.Left + lblGearSourceLabel.Width + 6;
			chkGearHomeNode.Left = chkGearEquipped.Left + chkGearEquipped.Width + 16;

			// Vehicles and Drones tab.
			intWidth = Math.Max(lblVehicleNameLabel.Width, lblVehicleCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleHandlingLabel.Width);
			intWidth = Math.Max(intWidth, lblVehiclePilotLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleFirewallLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleAvailLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleRatingLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleGearQtyLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleSourceLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponNameLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponCategoryLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponDamageLabel.Width);

			lblVehicleName.Left = lblVehicleNameLabel.Left + intWidth + 6;
			lblVehicleCategory.Left = lblVehicleCategoryLabel.Left + intWidth + 6;
			lblVehicleHandling.Left = lblVehicleHandlingLabel.Left + intWidth + 6;
			lblVehiclePilot.Left = lblVehiclePilotLabel.Left + intWidth + 6;
			lblVehicleFirewall.Left = lblVehicleFirewallLabel.Left + intWidth + 6;
			lblVehicleAvail.Left = lblVehicleAvailLabel.Left + intWidth + 6;
			lblVehicleRating.Left = lblVehicleRatingLabel.Left + intWidth + 6;
			lblVehicleGearQty.Left = lblVehicleGearQtyLabel.Left + intWidth + 6;
			lblVehicleSource.Left = lblVehicleSourceLabel.Left + intWidth + 6;
			lblVehicleWeaponName.Left = lblVehicleWeaponNameLabel.Left + intWidth + 6;
			lblVehicleWeaponCategory.Left = lblVehicleWeaponCategoryLabel.Left + intWidth + 6;
			lblVehicleWeaponDamage.Left = lblVehicleWeaponDamageLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblVehicleAccelLabel.Width, lblVehicleBodyLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleSignalLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleCostLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponAPLabel.Width);

			lblVehicleAccelLabel.Left = lblVehicleHandling.Left + 47;
			lblVehicleAccel.Left = lblVehicleAccelLabel.Left + intWidth + 6;
			lblVehicleBodyLabel.Left = lblVehicleHandling.Left + 47;
			lblVehicleBody.Left = lblVehicleBodyLabel.Left + intWidth + 6;
			lblVehicleSignalLabel.Left = lblVehicleHandling.Left + 47;
			lblVehicleSignal.Left = lblVehicleSignalLabel.Left + intWidth + 6;
			lblVehicleCostLabel.Left = lblVehicleHandling.Left + 47;
			lblVehicleCost.Left = lblVehicleCostLabel.Left + intWidth + 6;
			lblVehicleWeaponAPLabel.Left = lblVehicleHandling.Left + 47;
			lblVehicleWeaponAP.Left = lblVehicleWeaponAPLabel.Left + intWidth + 6;

			cmdVehicleGearReduceQty.Left = lblVehicleGearQty.Left + 50;
			chkVehicleIncludedInWeapon.Left = lblVehicleAccel.Left;
			chkVehicleHomeNode.Left = lblVehicleAccel.Left;
			cmdVehicleMoveToInventory.Left = lblVehicleAccel.Left;

			intWidth = Math.Max(lblVehicleSpeedLabel.Width, lblVehicleArmorLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleResponseLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponModeLabel.Width);

			lblVehicleSpeedLabel.Left = lblVehicleAccel.Left + 53;
			lblVehicleSpeed.Left = lblVehicleSpeedLabel.Left + intWidth + 6;
			lblVehicleArmorLabel.Left = lblVehicleAccel.Left + 53;
			lblVehicleArmor.Left = lblVehicleArmorLabel.Left + intWidth + 6;
			lblVehicleResponseLabel.Left = lblVehicleAccel.Left + 53;
			lblVehicleResponse.Left = lblVehicleResponseLabel.Left + intWidth + 6;
			lblVehicleWeaponModeLabel.Left = lblVehicleAccel.Left + 53;
			lblVehicleWeaponMode.Left = lblVehicleWeaponModeLabel.Left + intWidth + 6;

			intWidth = Math.Max(lblVehicleDeviceLabel.Width, lblVehicleSensorLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleSystemLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponAmmoLabel.Width);

			lblVehicleDeviceLabel.Left = lblVehicleSpeed.Left + 35;
			lblVehicleDevice.Left = lblVehicleDeviceLabel.Left + intWidth + 6;
			lblVehicleSensorLabel.Left = lblVehicleSpeed.Left + 35;
			lblVehicleSensor.Left = lblVehicleSensorLabel.Left + intWidth + 6;
			lblVehicleSystemLabel.Left = lblVehicleSpeed.Left + 35;
			lblVehicleSystem.Left = lblVehicleSystemLabel.Left + intWidth + 6;
			lblVehicleWeaponAmmoLabel.Left = lblVehicleSpeed.Left + 35;
			lblVehicleWeaponAmmo.Left = lblVehicleWeaponAmmoLabel.Left + intWidth + 6;

			lblVehicleSlotsLabel.Left = lblVehicleCost.Left + 94;
			lblVehicleSlots.Left = lblVehicleSlotsLabel.Left + lblVehicleSlotsLabel.Width + 6;
			chkVehicleWeaponAccessoryInstalled.Left = lblVehicleDeviceLabel.Left;

			intWidth = Math.Max(lblVehicleWeaponAmmoRemainingLabel.Width, lblVehicleWeaponAmmoTypeLabel.Width);
			intWidth = Math.Max(intWidth, lblVehicleWeaponDicePoolLabel.Width);

			lblVehicleWeaponAmmoRemaining.Left = lblVehicleWeaponAmmoRemainingLabel.Left + intWidth + 6;
			cboVehicleWeaponAmmo.Left = lblVehicleWeaponAmmoTypeLabel.Left + intWidth + 6;
			lblVehicleWeaponDicePool.Left = lblVehicleWeaponDicePoolLabel.Left + intWidth + 6;

			cmdFireVehicleWeapon.Left = lblVehicleWeaponAmmoRemaining.Left + 123;
			cmdReloadVehicleWeapon.Left = cmdFireVehicleWeapon.Left + cmdFireVehicleWeapon.Width + 6;

			cmdRollVehicleWeapon.Left = lblVehicleWeaponDicePool.Left + lblVehicleWeaponDicePool.Width + 6;
			cmdRollVehicleWeapon.Visible = _objOptions.AllowSkillDiceRolling;

			// Character Info.
			intWidth = Math.Max(lblSex.Width, lblHeight.Width);
			txtSex.Left = lblSex.Left + intWidth + 6;
			txtSex.Width = lblAge.Left - txtSex.Left - 16;
			txtHeight.Left = lblHeight.Left + intWidth + 6;
			txtHeight.Width = lblWeight.Left - txtHeight.Left - 16;

			intWidth = Math.Max(lblAge.Width, lblWeight.Width);
			txtAge.Left = lblAge.Left + intWidth + 6;
			txtAge.Width = lblEyes.Left - txtAge.Left - 16;
			txtWeight.Left = lblWeight.Left + intWidth + 6;
			txtWeight.Width = lblSkin.Left - txtWeight.Left - 16;

			intWidth = Math.Max(lblEyes.Width, lblSkin.Width);
			txtEyes.Left = lblEyes.Left + intWidth + 6;
			txtEyes.Width = lblHair.Left - txtEyes.Left - 16;
			txtSkin.Left = lblSkin.Left + intWidth + 6;
			txtSkin.Width = lblCharacterName.Left - txtSkin.Left - 16;

			intWidth = Math.Max(lblHair.Width, lblCharacterName.Width);
			txtHair.Left = lblHair.Left + intWidth + 6;
			txtHair.Width = lblPlayerName.Left - txtHair.Left - 16;
			txtCharacterName.Left = lblCharacterName.Left + intWidth + 6;
			txtCharacterName.Width = lblPlayerName.Left - txtCharacterName.Left - 16;

			txtPlayerName.Left = lblPlayerName.Left + lblPlayerName.Width + 6;
			txtPlayerName.Width = tabCharacterInfo.Width - txtPlayerName.Left - 16;

			intWidth = Math.Max(lblStreetCred.Width, lblNotoriety.Width);
			intWidth = Math.Max(intWidth, lblPublicAware.Width);

			nudStreetCred.Left = lblStreetCred.Left + intWidth + 6;
			nudNotoriety.Left = lblNotoriety.Left + intWidth + 6;
			nudPublicAware.Left = lblPublicAware.Left + intWidth + 6;
			lblStreetCredTotal.Left = nudStreetCred.Left + nudStreetCred.Width + 6;
			lblNotorietyTotal.Left = nudNotoriety.Left + nudNotoriety.Width + 6;
			lblPublicAwareTotal.Left = nudPublicAware.Left + nudPublicAware.Width + 6;

			// Expense Tab.
			cmdKarmaSpent.Left = cmdKarmaGained.Left + cmdKarmaGained.Width + 6;
			cmdKarmaEdit.Left = cmdKarmaSpent.Left + cmdKarmaSpent.Width + 6;
			cmdNuyenSpent.Left = cmdNuyenGained.Left + cmdNuyenGained.Width + 6;
			cmdNuyenEdit.Left = cmdNuyenSpent.Left + cmdNuyenSpent.Width + 6;

			// Calendar Tab.
			cmdEditWeek.Left = cmdAddWeek.Left + cmdAddWeek.Width + 6;

			// Improvements tab.
			cmdEditImprovement.Left = cmdAddImprovement.Left + cmdAddImprovement.Width + 6;
			cmdDeleteImprovement.Left = cmdEditImprovement.Left + cmdEditImprovement.Width + 6;
			lblImprovementType.Left = lblImprovementTypeLabel.Left + lblImprovementTypeLabel.Width + 6;

			// Other Info tab.
			intWidth = Math.Max(lblCMPhysicalLabel.Width, lblCMStunLabel.Width);
			intWidth = Math.Max(intWidth, lblINILabel.Width);
			intWidth = Math.Max(intWidth, lblIPLabel.Width);
			intWidth = Math.Max(intWidth, lblMatrixINILabel.Width);
			intWidth = Math.Max(intWidth, lblMatrixIPLabel.Width);
			intWidth = Math.Max(intWidth, lblAstralINILabel.Width);
			intWidth = Math.Max(intWidth, lblAstralIPLabel.Width);
			intWidth = Math.Max(intWidth, lblArmorLabel.Width);
			intWidth = Math.Max(intWidth, lblESS.Width);
			intWidth = Math.Max(intWidth, lblRemainingNuyenLabel.Width);
			intWidth = Math.Max(intWidth, lblCareerKarmaLabel.Width);
			intWidth = Math.Max(intWidth, lblCareerNuyenLabel.Width);
			intWidth = Math.Max(intWidth, lblComposureLabel.Width);
			intWidth = Math.Max(intWidth, lblJudgeIntentionsLabel.Width);
			intWidth = Math.Max(intWidth, lblLiftCarryLabel.Width);
			intWidth = Math.Max(intWidth, lblMemoryLabel.Width);
			intWidth = Math.Max(intWidth, lblMovementLabel.Width);
			intWidth = Math.Max(intWidth, lblSwimLabel.Width);
			intWidth = Math.Max(intWidth, lblFlyLabel.Width);

			lblCMPhysical.Left = lblPhysicalCMLabel.Left + intWidth + 6;
			lblCMStun.Left = lblCMPhysical.Left;
			lblINI.Left = lblCMPhysical.Left;
			lblIP.Left = lblCMPhysical.Left;
			lblMatrixINI.Left = lblCMPhysical.Left;
			lblMatrixIP.Left = lblCMPhysical.Left;
			lblAstralINI.Left = lblCMPhysical.Left;
			lblAstralIP.Left = lblCMPhysical.Left;
			lblArmor.Left = lblCMPhysical.Left;
			lblESSMax.Left = lblCMPhysical.Left;
			lblRemainingNuyen.Left = lblCMPhysical.Left;
			lblCareerKarma.Left = lblCMPhysical.Left;
			lblCareerNuyen.Left = lblCMPhysical.Left;
			lblComposure.Left = lblCMPhysical.Left;
			lblJudgeIntentions.Left = lblCMPhysical.Left;
			lblLiftCarry.Left = lblCMPhysical.Left;
			lblMemory.Left = lblCMPhysical.Left;
			lblMovement.Left = lblCMPhysical.Left;
			lblSwim.Left = lblCMPhysical.Left;
			lblFly.Left = lblCMPhysical.Left;

			// Condition Monitor tab.
			intWidth = Math.Max(lblCMPenaltyLabel.Width, lblCMArmorLabel.Width);
			intWidth = Math.Max(intWidth, lblCMDamageResistancePoolLabel.Width);

			lblCMPenalty.Left = lblCMPenaltyLabel.Left + intWidth + 6;
			lblCMArmor.Left = lblCMPenalty.Left;
			lblCMDamageResistancePool.Left = lblCMPenalty.Left;
		}

		/// <summary>
		/// Change the size of a Vehicle's Sensor
		/// </summary>
		/// <param name="objVehicle">Vehicle to modify.</param>
		/// <param name="blnIncrease">True if the Sensor should increase in size, False if it should decrease.</param>
		private void ChangeVehicleSensor(Vehicle objVehicle, bool blnIncrease)
		{
			XmlDocument objXmlDocument = XmlManager.Instance.Load("gear.xml");
			XmlNode objNewNode;
			bool blnFound = false;

			Gear objSensor = new Gear(_objCharacter);
			Gear objNewSensor = new Gear(_objCharacter);

			TreeNode objTreeNode = new TreeNode();
			List<Weapon> lstWeapons = new List<Weapon>();
			List<TreeNode> lstWeaponNodes = new List<TreeNode>();
			foreach (Gear objCurrentGear in objVehicle.Gear)
			{
				if (objCurrentGear.Name == "Microdrone Sensor")
				{
					if (blnIncrease)
					{
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Minidrone Sensor\" and category = \"Sensors\"]");
						objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
						objSensor = objCurrentGear;
						blnFound = true;
					}
					break;
				}
				else if (objCurrentGear.Name == "Minidrone Sensor")
				{
					if (blnIncrease)
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Small Drone Sensor\" and category = \"Sensors\"]");
					else
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Microdrone Sensor\" and category = \"Sensors\"]");
					objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
					objSensor = objCurrentGear;
					blnFound = true;
					break;
				}
				else if (objCurrentGear.Name == "Small Drone Sensor")
				{
					if (blnIncrease)
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Medium Drone Sensor\" and category = \"Sensors\"]");
					else
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Minidrone Sensor\" and category = \"Sensors\"]");
					objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
					objSensor = objCurrentGear;
					blnFound = true;
					break;
				}
				else if (objCurrentGear.Name == "Medium Drone Sensor")
				{
					if (blnIncrease)
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Large Drone Sensor\" and category = \"Sensors\"]");
					else
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Small Drone Sensor\" and category = \"Sensors\"]");
					objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
					objSensor = objCurrentGear;
					blnFound = true;
					break;
				}
				else if (objCurrentGear.Name == "Large Drone Sensor")
				{
					if (blnIncrease)
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Vehicle Sensor\" and category = \"Sensors\"]");
					else
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Medium Drone Sensor\" and category = \"Sensors\"]");
					objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
					objSensor = objCurrentGear;
					blnFound = true;
					break;
				}
				else if (objCurrentGear.Name == "Vehicle Sensor")
				{
					if (blnIncrease)
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Extra-Large Vehicle Sensor\" and category = \"Sensors\"]");
					else
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Large Drone Sensor\" and category = \"Sensors\"]");
					objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
					objSensor = objCurrentGear;
					blnFound = true;
					break;
				}
				else if (objCurrentGear.Name == "Extra-Large Vehicle Sensor")
				{
					if (!blnIncrease)
					{
						objNewNode = objXmlDocument.SelectSingleNode("/chummer/gears/gear[name = \"Vehicle Sensor\" and category = \"Sensors\"]");
						objNewSensor.Create(objNewNode, _objCharacter, objTreeNode, 0, lstWeapons, lstWeaponNodes);
						objSensor = objCurrentGear;
						blnFound = true;
					}
					break;
				}
			}

			// If the item was found, update the Vehicle Sensor information.
			if (blnFound)
			{
				objSensor.Name = objNewSensor.Name;
				objSensor.Rating = objNewSensor.Rating;
				objSensor.Capacity = objNewSensor.Capacity;
				objSensor.DeviceRating = objNewSensor.DeviceRating;
				objSensor.Avail = objNewSensor.Avail;
				objSensor.Cost = objNewSensor.Cost;
				objSensor.Source = objNewSensor.Source;
				objSensor.Page = objNewSensor.Page;

				// Update the name of the item in the TreeView.
				TreeNode objNode = _objFunctions.FindNode(objSensor.InternalId, treVehicles);
				objNode.Text = objSensor.DisplayNameShort;
			}
		}

		/// <summary>
		/// Update the Reputation fields.
		/// </summary>
		private void UpdateReputation()
		{
			lblStreetCredTotal.Text = " + " + _objCharacter.CalculatedStreetCred.ToString() + " = " + _objCharacter.TotalStreetCred.ToString();
			lblNotorietyTotal.Text = " + " + _objCharacter.CalculatedNotoriety.ToString() + " = " + _objCharacter.TotalNotoriety.ToString();
			lblPublicAwareTotal.Text = " + " + _objCharacter.CalculatedPublicAwareness.ToString() + " = " + _objCharacter.TotalPublicAwareness.ToString();
			cmdBurnStreetCred.Left = lblStreetCredTotal.Left + lblStreetCredTotal.Width + 6;
			cmdBurnStreetCred.Enabled = _objCharacter.TotalStreetCred >= 2;

			tipTooltip.SetToolTip(lblStreetCredTotal, _objCharacter.StreetCredTooltip);
			tipTooltip.SetToolTip(lblNotorietyTotal, _objCharacter.NotorietyTooltip);
			tipTooltip.SetToolTip(lblPublicAwareTotal, _objCharacter.PublicAwarenessTooltip);
		}

		/// <summary>
		/// Copy the Improvements from a piece of Armor on one character to another.
		/// </summary>
		/// <param name="objSource">Source character.</param>
		/// <param name="objDestination">Destination character.</param>
		/// <param name="objArmor">Armor to copy.</param>
		private void CopyArmorImprovements(Character objSource, Character objDestination, Armor objArmor)
		{
			foreach (Improvement objImproevment in objSource.Improvements)
			{
				if (objImproevment.SourceName == objArmor.InternalId)
				{
					objDestination.Improvements.Add(objImproevment);
				}
			}
			// Look through any Armor Mods and add the Improvements as well.
			foreach (ArmorMod objMod in objArmor.ArmorMods)
			{
				foreach (Improvement objImproevment in objSource.Improvements)
				{
					if (objImproevment.SourceName == objMod.InternalId)
					{
						objDestination.Improvements.Add(objImproevment);
					}
				}
			}
			// Look through any children and add their Improvements as well.
			foreach (Gear objChild in objArmor.Gear)
				CopyGearImprovements(objSource, objDestination, objChild);
		}

		/// <summary>
		/// Copy the Improvements from a piece of Gear on one character to another.
		/// </summary>
		/// <param name="objSource">Source character.</param>
		/// <param name="objDestination">Destination character.</param>
		/// <param name="objGear">Gear to copy.</param>
		private void CopyGearImprovements(Character objSource, Character objDestination, Gear objGear)
		{
			foreach (Improvement objImproevment in objSource.Improvements)
			{
				if (objImproevment.SourceName == objGear.InternalId)
				{
					objDestination.Improvements.Add(objImproevment);
				}
			}
			// Look through any children and add their Improvements as well.
			foreach (Gear objChild in objGear.Children)
				CopyGearImprovements(objSource, objDestination, objChild);
		}

		/// <summary>
		/// Copy the Improvements from a piece of Cyberware on one character to another.
		/// </summary>
		/// <param name="objSource">Source character.</param>
		/// <param name="objDestination">Destination character.</param>
		/// <param name="objCyberware">Cyberware to copy.</param>
		private void CopyCyberwareImprovements(Character objSource, Character objDestination, Cyberware objCyberware)
		{
			foreach (Improvement objImproevment in objSource.Improvements)
			{
				if (objImproevment.SourceName == objCyberware.InternalId)
				{
					objDestination.Improvements.Add(objImproevment);
				}
			}
			// Look through any children and add their Improvements as well.
			foreach (Cyberware objChild in objCyberware.Children)
				CopyCyberwareImprovements(objSource, objDestination, objChild);
		}

		/// <summary>
		/// Recursive method to add a Gear's Improvements to a character when moving them from a Vehicle.
		/// </summary>
		/// <param name="objGear">Gear to create Improvements for.
		/// </param>
		private void AddGearImprovements(Gear objGear)
		{
			string strForce = "";
			if (objGear.Bonus != null)
			{
				if (objGear.Extra != string.Empty)
					strForce = objGear.Extra;
				_objImprovementManager.ForcedValue = strForce;
				_objImprovementManager.CreateImprovements(Improvement.ImprovementSource.Gear, objGear.InternalId, objGear.Bonus, true, objGear.Rating, objGear.DisplayNameShort);
			}
			foreach (Gear objChild in objGear.Children)
				AddGearImprovements(objChild);
		}

		/// <summary>
		/// Enable/Disable the Paste Menu and ToolStrip items as appropriate.
		/// </summary>
		private void RefreshPasteStatus()
		{
		}

		/// <summary>
		/// Refresh the information for the currently selected Complex Form.
		/// </summary>
		private void RefreshSelectedComplexForm()
		{
			if (_blnSkipRefresh)
				return;

			try
			{
				if (treComplexForms.SelectedNode.Level == 1)
				{
					// Locate the Program that is selected in the tree.
                    ComplexForm objProgram = _objFunctions.FindComplexForm(treComplexForms.SelectedNode.Tag.ToString(), _objCharacter.ComplexForms);

					string strBook = _objOptions.LanguageBookShort(objProgram.Source);
					string strPage = objProgram.Page;
					lblComplexFormSource.Text = strBook + " " + strPage;
					tipTooltip.SetToolTip(lblComplexFormSource, _objOptions.LanguageBookLong(objProgram.Source) + " " + LanguageManager.Instance.GetString("String_Page") + " " + objProgram.Page);
				}
				else
				{
					cmdImproveComplexForm.Enabled = false;
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Populate the TreeView that contains all of the character's Gear.
		/// </summary>
		private void PopulateGearList()
		{
			// Populate Gear.
			// Create the root node.
			treGear.Nodes.Clear();
			TreeNode objRoot = new TreeNode();
			objRoot.Tag = "Node_SelectedGear";
			objRoot.Text = LanguageManager.Instance.GetString("Node_SelectedGear");
			treGear.Nodes.Add(objRoot);

			// Start by populating Locations.
			foreach (string strLocation in _objCharacter.Locations)
			{
				TreeNode objLocation = new TreeNode();
				objLocation.Tag = strLocation;
				objLocation.Text = strLocation;
				objLocation.ContextMenuStrip = cmsGearLocation;
				treGear.Nodes.Add(objLocation);
			}

			// Add Locations for the character's bits that can hold Commlinks.
			// Populate the list of Commlink Locations.
			foreach (Cyberware objCyberware in _objCharacter.Cyberware)
			{
				if (objCyberware.AllowGear != null)
				{
					if (objCyberware.AllowGear["gearcategory"] != null)
					{
						if (objCyberware.AllowGear["gearcategory"].InnerText == "Commlink")
						{
							TreeNode objNode = new TreeNode();
							objNode.Tag = objCyberware.InternalId.ToString();
							objNode.Text = objCyberware.DisplayCategory + ": " + objCyberware.DisplayName;
							treGear.Nodes.Add(objNode);
						}
					}
				}
				foreach (Cyberware objPlugin in objCyberware.Children)
				{
					if (objPlugin.AllowGear != null)
					{
						if (objPlugin.AllowGear["gearcategory"] != null)
						{
							TreeNode objNode = new TreeNode();
							objNode.Tag = objPlugin.InternalId.ToString();
							objNode.Text = objPlugin.DisplayCategory + ": " + objPlugin.DisplayName;
							treGear.Nodes.Add(objNode);
						}
					}
				}
			}
			foreach (Weapon objWeapon in _objCharacter.Weapons)
			{
				foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
				{
					if (objAccessory.AllowGear != null)
					{
						if (objAccessory.AllowGear["gearcategory"] != null)
						{
							if (objAccessory.AllowGear["gearcategory"].InnerText == "Commlink")
							{
								TreeNode objNode = new TreeNode();
								objNode.Tag = objAccessory.InternalId.ToString();
								objNode.Text = objWeapon.DisplayName + ": " + objAccessory.DisplayName;
								treGear.Nodes.Add(objNode);
							}
						}
					}
				}
				foreach (Weapon objUnderbarrel in objWeapon.UnderbarrelWeapons)
				{
					foreach (WeaponAccessory objUnderbarrelAccessory in objUnderbarrel.WeaponAccessories)
					{
						if (objUnderbarrelAccessory.AllowGear != null)
						{
							if (objUnderbarrelAccessory.AllowGear["gearcategory"] != null)
							{
								if (objUnderbarrelAccessory.AllowGear["gearcategory"].InnerText == "Commlink")
								{
									TreeNode objNode = new TreeNode();
									objNode.Tag = objUnderbarrelAccessory.InternalId.ToString();
									objNode.Text = objUnderbarrel.DisplayName + ": " + objUnderbarrelAccessory.DisplayName;
									treGear.Nodes.Add(objNode);
								}
							}
						}
					}
				}
			}

			foreach (Gear objGear in _objCharacter.Gear)
			{
				TreeNode objNode = new TreeNode();
				objNode.Text = objGear.DisplayName;
				objNode.Tag = objGear.InternalId;
				if (objGear.Notes != string.Empty)
					objNode.ForeColor = Color.SaddleBrown;
				objNode.ToolTipText = objGear.Notes;

				_objFunctions.BuildGearTree(objGear, objNode, cmsGear);

				objNode.ContextMenuStrip = cmsGear;

				TreeNode objParent = new TreeNode();
				if (objGear.Location == "")
					objParent = treGear.Nodes[0];
				else
				{
					foreach (TreeNode objFind in treGear.Nodes)
					{
						if (objFind.Text == objGear.Location)
						{
							objParent = objFind;
							break;
						}
					}
				}
				objParent.Nodes.Add(objNode);
				objParent.Expand();
			}
		}

		/// <summary>
		/// Populate the TreeView that contains all of the character's Cyberware and Bioware.
		/// </summary>
		private void PopulateCyberware()
		{
			// Populate Cyberware.
			foreach (Cyberware objCyberware in _objCharacter.Cyberware)
			{
				if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
				{
					_objFunctions.BuildCyberwareTree(objCyberware, treCyberware.Nodes[0], cmsCyberware, cmsCyberwareGear);
				}
			}

			// Populate Bioware.
			foreach (Cyberware objCyberware in _objCharacter.Cyberware)
			{
				if (objCyberware.SourceType == Improvement.ImprovementSource.Bioware)
				{
					_objFunctions.BuildCyberwareTree(objCyberware, treCyberware.Nodes[1], cmsBioware, cmsCyberwareGear);
				}
			}
		}

		/// <summary>
		/// Change the active Commlink for the Character.
		/// </summary>
		/// <param name="objActiveCommlink"></param>
		private void ChangeActiveCommlink(Commlink objActiveCommlink)
		{
			List<Commlink> lstCommlinks = _objFunctions.FindCharacterCommlinks(_objCharacter.Gear);

			foreach (Commlink objCommlink in lstCommlinks)
			{
				if (objCommlink.InternalId != objActiveCommlink.InternalId)
					objCommlink.IsActive = false;
			}
		}

		/// <summary>
		/// Create Cyberware from a Cyberware Suite.
		/// </summary>
		/// <param name="objXmlNode">XmlNode for the Cyberware to add.</param>
		/// <param name="objGrade">CyberwareGrade to add the item as.</param>
		/// <param name="intRating">Rating of the Cyberware.</param>
		/// <param name="blnAddToCharacter">Whether or not the Cyberware should be added directly to the character.</param>
		/// <param name="objParent">Parent Cyberware if the item is not being added directly to the character.</param>
		private TreeNode CreateSuiteCyberware(XmlNode objXmlItem, XmlNode objXmlNode, Grade objGrade, int intRating, bool blnAddToCharacter, Improvement.ImprovementSource objSource, string strType, Cyberware objParent = null)
		{
			// Create the Cyberware object.
			List<Weapon> objWeapons = new List<Weapon>();
			List<TreeNode> objWeaponNodes = new List<TreeNode>();
			TreeNode objNode = new TreeNode();
			Cyberware objCyberware = new Cyberware(_objCharacter);
			string strForced = "";

			if (objXmlItem["name"].Attributes["select"] != null)
				strForced = objXmlItem["name"].Attributes["select"].InnerText;

			objCyberware.Create(objXmlNode, _objCharacter, objGrade, objSource, intRating, objNode, objWeapons, objWeaponNodes, true, true, strForced);
			objCyberware.Suite = true;

			foreach (Weapon objWeapon in objWeapons)
				_objCharacter.Weapons.Add(objWeapon);

			foreach (TreeNode objWeaponNode in objWeaponNodes)
			{
				treWeapons.Nodes[0].Nodes.Add(objWeaponNode);
				treWeapons.Nodes[0].Expand();
			}

			if (blnAddToCharacter)
				_objCharacter.Cyberware.Add(objCyberware);
			else
				objParent.Children.Add(objCyberware);

			foreach (XmlNode objXmlChild in objXmlItem.SelectNodes(strType + "s/" + strType))
			{
				XmlDocument objXmlDocument = XmlManager.Instance.Load(strType + ".xml");
				XmlNode objXmlChildCyberware = objXmlDocument.SelectSingleNode("/chummer/" + strType + "s/" + strType + "[name = \"" + objXmlChild["name"].InnerText + "\"]");
				TreeNode objChildNode = new TreeNode();
				int intChildRating = 0;

				if (objXmlChild["rating"] != null)
					intChildRating = Convert.ToInt32(objXmlChild["rating"].InnerText);

				objChildNode = CreateSuiteCyberware(objXmlChild, objXmlChildCyberware, objGrade, intChildRating, false, objSource, strType, objCyberware);
				objNode.Nodes.Add(objChildNode);
				objNode.Expand();
			}

			return objNode;
		}

		private void AddCyberwareSuite(Improvement.ImprovementSource objSource)
		{
			frmSelectCyberwareSuite frmPickCyberwareSuite = new frmSelectCyberwareSuite(objSource, _objCharacter);
			frmPickCyberwareSuite.ShowDialog(this);

			if (frmPickCyberwareSuite.DialogResult == DialogResult.Cancel)
				return;

			int intCost = frmPickCyberwareSuite.TotalCost;
			if (intCost > _objCharacter.Nuyen)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_NotEnoughNuyen"), LanguageManager.Instance.GetString("MessageTitle_NotEnoughNuyen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			else
			{
				// Create the Expense Log Entry.
				ExpenseLogEntry objExpense = new ExpenseLogEntry();
				objExpense.Create(intCost * -1, LanguageManager.Instance.GetString("String_ExpensePurchaseCyberwareSuite") + " " + frmPickCyberwareSuite.SelectedSuite, ExpenseType.Nuyen, DateTime.Now);
				_objCharacter.ExpenseEntries.Add(objExpense);
				_objCharacter.Nuyen -= intCost;
			}

			string strType = "";
			int intParentNode = 0;
			if (objSource == Improvement.ImprovementSource.Cyberware)
			{
				strType = "cyberware";
				intParentNode = 0;
			}
			else
			{
				strType = "bioware";
				intParentNode = 1;
			}
			XmlDocument objXmlDocument = XmlManager.Instance.Load(strType + ".xml");

			XmlNode objXmlSuite = objXmlDocument.SelectSingleNode("/chummer/suites/suite[name = \"" + frmPickCyberwareSuite.SelectedSuite + "\"]");
			Cyberware objTemp = new Cyberware(_objCharacter);
			Grade objGrade = new Grade();
			objGrade = objTemp.ConvertToCyberwareGrade(objXmlSuite["grade"].InnerText, objSource);

			// Run through each of the items in the Suite and add them to the character.
			foreach (XmlNode objXmlItem in objXmlSuite.SelectNodes(strType + "s/" + strType))
			{
				XmlNode objXmlCyberware = objXmlDocument.SelectSingleNode("/chummer/" + strType + "s/" + strType + "[name = \"" + objXmlItem["name"].InnerText + "\"]");
				TreeNode objNode = new TreeNode();
				int intRating = 0;

				if (objXmlItem["rating"] != null)
					intRating = Convert.ToInt32(objXmlItem["rating"].InnerText);

				objNode = CreateSuiteCyberware(objXmlItem, objXmlCyberware, objGrade, intRating, true, objSource, strType, null);

				objNode.Expand();
				treCyberware.Nodes[intParentNode].Nodes.Add(objNode);
				treCyberware.Nodes[intParentNode].Expand();
			}

			_blnIsDirty = true;
			UpdateWindowTitle();
			UpdateCharacterInfo();
		}
		#endregion
	}
}