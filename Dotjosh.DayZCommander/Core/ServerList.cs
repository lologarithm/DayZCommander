﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Dotjosh.DayZCommander.Core
{
	public class ServerList : BindableBase
	{
		private int _processedServersCount;
		private ObservableCollection<Server> _items;
		private bool _isUpdating;

		public ServerList()
		{
			Items = new ObservableCollection<Server>();
		}

		public ObservableCollection<Server> Items
		{
			get { return _items; }
			private set
			{
				_items = value;
				PropertyHasChanged("Items");
				App.Events.Publish(new ServersAdded(_items));
			}
		}

		public int UnprocessedServersCount
		{
			get { return Items.Count - ProcessedServersCount; }
		}

		public int ProcessedServersCount
		{
			get { return _processedServersCount; }
			set
			{
				_processedServersCount = value;
				PropertyHasChanged("ProcessedServersCount", "UnprocessedServersCount");
			}
		}

		public void GetAndUpdateAll()
		{
			GetAll(
				() => UpdateAll() 
				);
		}

		public void GetAll(Action uiThreadOnComplete)
		{
			Task.Factory.StartNew(() =>
			                    {
			                        var servers = GetAllSync();
									Execute.OnUiThread(() =>
														{
															Items = new ObservableCollection<Server>(servers);
															uiThreadOnComplete();
														});

			                    });
		}

		private List<Server> GetAllSync()
		{
			ExecuteGSList("-u");
			return ExecuteGSList("-n arma2oapc -f \"mod LIKE '%@dayz%'\"")
				.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
				.Select(line => new Server(
				                	line.Substring(0, 15).Trim(),
				                	line.Substring(16).Trim().TryInt()
				                	)
				)
				.ToList();
		}

		public void UpdateAll()
		{
			if(_isUpdating)
				return;

			_isUpdating = true;
			ProcessedServersCount = 0;

			//In an array to prevent modified closure access
			int[] processed = {0};

			Task.Factory.StartNew(() =>
			{
				try
				{
					while(processed[0] <= Items.Count)
					{
						Execute.OnUiThread(() =>
						{
							ProcessedServersCount = processed[0];
						});
						if(processed[0] == Items.Count)
						{
							_isUpdating = false;
							Execute.OnUiThread(() =>
							{
								ProcessedServersCount = Items.Count;
							});
							break;
						}
						Thread.Sleep(50);
					}
				}
				finally
				{
					Execute.OnUiThread(() =>
					{
						ProcessedServersCount = Items.Count;
					});
					_isUpdating = false;
				}
			});


			Task.Factory.StartNew(() =>
			{
				for(var index = 0; index < Items.Count; index++)
				{
					var server = Items[index];
					new Thread(() =>
					{
						try
						{
							server.Update();
						}
						finally
						{
							processed[0]++;
						}
					}).Start();

					if(index % 50 == 0)
					{
						Thread.Sleep(100);
					}
				}
			});
		}

		private static string ExecuteGSList(string arguments)
		{
			var currentDirectoryUri = new Uri( Path.GetDirectoryName(
				Assembly.GetExecutingAssembly().GetName().CodeBase));

			var currentDirectory = currentDirectoryUri.AbsolutePath;

			var p = new Process
			{
				StartInfo =
					{
						UseShellExecute = false,
						CreateNoWindow = true,
						WindowStyle = ProcessWindowStyle.Hidden,
						RedirectStandardOutput = true,
						FileName = Path.Combine(currentDirectory, @"GSList\gslist.exe"),
						Arguments = arguments
					}
			};
			p.Start();
			string output = p.StandardOutput.ReadToEnd();
			p.WaitForExit();
			return output;
		}
	}
}