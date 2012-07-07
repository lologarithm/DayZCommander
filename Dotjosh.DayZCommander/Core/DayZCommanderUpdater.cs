﻿using System;
using System.ComponentModel;
using System.Deployment.Application;
using System.Reflection;
using System.Xml;
using NLog;

namespace Dotjosh.DayZCommander.Core
{
	public class DayZCommanderUpdater : BindableBase
	{
		private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
		private bool _restartToApplyUpdate;

		public bool RestartToApplyUpdate
		{
			get { return _restartToApplyUpdate; }
			set
			{
				_restartToApplyUpdate = value;
				PropertyHasChanged("RestartToApplyUpdate");
			}
		}

		public string CurrentVersion
		{
			get
			{
				var xmlDoc = new XmlDocument();
				var asmCurrent = Assembly.GetExecutingAssembly();
				string executePath = new Uri(asmCurrent.GetName().CodeBase).LocalPath;

				xmlDoc.Load(executePath + ".manifest");
				string retval = string.Empty;
				if (xmlDoc.HasChildNodes)
				{
					retval = xmlDoc.ChildNodes[1].ChildNodes[0].Attributes.GetNamedItem("version").Value;
				}
				return new Version(retval).ToString();
			
			}
		}

		public void StartCheckingForUpdates()
		{
			HandleExceptionsAsWarnings(() =>
			{
				if(!ApplicationDeployment.IsNetworkDeployed)
					return;

				ApplicationDeployment.CurrentDeployment.CheckForUpdateCompleted += CheckForUpdates_Completed;
				ApplicationDeployment.CurrentDeployment.UpdateCompleted += UpdateCompleted;

				//Start a timer to check for updates every 2 hours
				var t = new System.Timers.Timer();
				t.Interval = TimeSpan.FromHours(2).TotalMilliseconds;
				t.Elapsed += (sender, args) => CheckForUpdates();
				t.Start();

				//But still check now
				CheckForUpdates();
			});
		}

		private void CheckForUpdates()
		{
			HandleExceptionsAsWarnings(() =>
				ApplicationDeployment.CurrentDeployment.CheckForUpdateAsync()
			);
		}

		private void CheckForUpdates_Completed(object sender, CheckForUpdateCompletedEventArgs args)
		{
			HandleExceptionsAsWarnings(() =>
			{
				if(args.UpdateAvailable)
				{
					ApplicationDeployment.CurrentDeployment.UpdateAsync();
				}
			});
		}

		private void UpdateCompleted(object sender, AsyncCompletedEventArgs e)
		{
			RestartToApplyUpdate = true;
		}

		private void HandleExceptionsAsWarnings(Action action)
		{
			try
			{
				action();
			}
			catch(Exception ex)
			{
				_logger.Warn(ex);
			}
		}
	}
}