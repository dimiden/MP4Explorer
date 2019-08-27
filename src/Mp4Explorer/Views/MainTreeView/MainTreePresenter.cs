﻿//===============================================================================
// Copyright © 2009 CM Streaming Technologies.
// All rights reserved.
// http://www.cmstream.net
//===============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CMStream.Mp4;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Presentation.Commands;
using Microsoft.Win32;

namespace Mp4Explorer
{
    /// <summary>
    /// 
    /// </summary>
    public class MainTreePresenter : IMainTreePresenter
    {
        #region Fields

        /// <summary>
        /// 
        /// </summary>
        private readonly IMainService service;

        /// <summary>
        /// 
        /// </summary>
        private readonly IMainController controller;

        /// <summary>
        /// 
        /// </summary>
        private readonly GlobalCommandsProxy commandsProxy;

        /// <summary>
        /// 
        /// </summary>
        private Mp4File file;

		private string lastFileName;

        /// <summary>
        /// 
        /// </summary>
        private Mp4Stream stream;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="view"></param>
        /// <param name="service"></param>
        /// <param name="controller"></param>
        /// <param name="commandsProxy"></param>
        public MainTreePresenter(IMainTreeView view, IMainService service, IMainController controller, GlobalCommandsProxy commandsProxy)
        {
            this.service = service;
            this.controller = controller;
            this.commandsProxy = commandsProxy;

            this.View = view;
            this.View.NodeSelected += View_NodeSelected;
			this.View.Presenter = this;

            this.OpenCommand = new DelegateCommand<object>(this.Open);
			this.RefreshCommand = new DelegateCommand<object>(this.Refresh);
			this.ExpandAllCommand = new DelegateCommand<object>(this.ExpandAll);
            
            this.commandsProxy.OpenCommand.RegisterCommand(this.OpenCommand);
			this.commandsProxy.RefreshCommand.RegisterCommand(this.RefreshCommand);
			this.commandsProxy.ExpandAllCommand.RegisterCommand(this.ExpandAllCommand);
        }

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public IMainTreeView View { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DelegateCommand<object> OpenCommand { get; private set; }

		public DelegateCommand<object> RefreshCommand { get; private set; }
		public DelegateCommand<object> ExpandAllCommand { get; private set; }

        #endregion

        #region Private methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void View_NodeSelected(object sender, DataEventArgs<TreeViewItem> e)
        {
            if (e.Value != null && e.Value is Mp4TreeNode)
            {
                Mp4Box box = ((Mp4TreeNode)e.Value).Box;

                controller.OnBoxSelected(box);
            }
            else
            {
                controller.ShowFile(file);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private void Open(object obj)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();

                openFileDialog.Filter = "MP4 Files (*.mp4,*.m4a,*.ismv,*.mov)|*.mp4;*.m4a;*.ismv;*.mov|All Files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() == true)
                {
					OpenFile(openFileDialog.FileName);
                }
            }
            catch
            {
                MessageBox.Show("Error loading mp4 file", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void Refresh(object obj)
		{
			if (lastFileName != null)
			{
				OpenFile(lastFileName);
			}
		}

		private void ExpandRecursive(TreeViewItem item)
		{
			if(item != null)
			{
				item.IsExpanded = true;

				foreach(TreeViewItem subItem in item.Items)
				{
					ExpandRecursive(subItem);
				}
			}
		}

		private void ExpandAll(object obj)
		{
			ExpandRecursive(this.View.RootNode);
		}

		public void OpenFile(string fileName)
		{
			Stream fileStream;

			try
			{
				fileStream = File.OpenRead(fileName);
			}
			catch (FileNotFoundException)
			{
				MessageBox.Show("Could not open file: " + fileName);
				return;
			}

			if (stream != null)
			{
				stream.Close();
			}

			lastFileName = fileName;

			Window window = Window.GetWindow(View.TreeView);

			window.Title = "Mp4 Explorer - " + Path.GetFileName(fileName) + " (" + fileName + ")";

			BackgroundWorker backgroundWorker = new BackgroundWorker();

			Exception exceptionError = null;

			backgroundWorker.DoWork += delegate(object s, DoWorkEventArgs args)
			{
				try
				{
					stream = new Mp4Stream(fileStream);
					file = new Mp4File(stream);

					List<Mp4Box> unknowBoxes = file.Boxes.FindAll(b => b is Mp4UnknownBox);

					if (unknowBoxes.Count > 1)
						throw new Exception("Too many unknow boxes.");

					View.Dispatcher.Invoke(new Action(delegate
					{
						this.View.RootNode = BuildTree(file, new FileInfo(fileName).Name);

						controller.RemoveViews();
					}));
				}
				catch (Exception ex)
				{
					exceptionError = ex;
				}
			};

			ProgressWindow progressWindow = new ProgressWindow();

			progressWindow.Worker = backgroundWorker;

			progressWindow.ShowDialog();

			if (exceptionError != null)
				throw exceptionError;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private TreeViewItem BuildTree(Mp4File file, string name)
        {
            ImageTreeViewItem root = new ImageTreeViewItem() { Text = name, ImageSource = "/Mp4Explorer;component/Images/movie_16.ico" };

            foreach (Mp4Box box in file.Boxes)
            {
                root.Items.Add(new Mp4TreeNode(box));
            }

            return root;
        }

        #endregion
    }
}
