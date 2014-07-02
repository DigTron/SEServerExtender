﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using SEModAPIExtensions.Plugin;

using SEModAPIInternal.Support;

using VRage.Common.Utils;

namespace SEModAPIInternal.API.Common
{
	public class PluginManager
	{
		#region "Attributes"

		private static PluginManager m_instance;

		private Dictionary<Guid, Object> m_plugins;
		private bool m_initialized;

		#endregion

		#region "Constructors and Initializers"

		protected PluginManager()
		{
			m_instance = this;

			m_plugins = new Dictionary<Guid, Object>();
			m_initialized = false;

			Console.WriteLine("Finished loading PluginManager");
		}

		public static PluginManager GetInstance()
		{
			if (m_instance == null)
			{
				m_instance = new PluginManager();
			}
			return (PluginManager)m_instance;
		}

		#endregion

		#region "Properties"

		public bool Initialized
		{
			get { return m_initialized; }
		}

		#endregion

		#region "Methods"

		private bool CompareInterfaces(Type type, Object criteria)
		{
			if (type.ToString() == criteria.ToString())
				return true;
			else
				return false;
		}

		public void LoadPlugins(string basePath = "")
		{
			Console.WriteLine("Loading plugins ...");

			try
			{
				string modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineersDedicated", "Mods");
				if (!Directory.Exists(modsPath))
					return;

				string[] subDirectories = Directory.GetDirectories(modsPath);
				foreach (string path in subDirectories)
				{
					string[] files = Directory.GetFiles(path);
					foreach (string file in files)
					{
						try
						{
							FileInfo fileInfo = new FileInfo(file);
							if (fileInfo.Name != "plugin.dll")
								continue;

							//Load the assembly
							Assembly pluginAssembly = Assembly.UnsafeLoadFrom(file);

							//Get the assembly GUID
							GuidAttribute guid = (GuidAttribute)pluginAssembly.GetCustomAttributes(typeof(GuidAttribute), true)[0];
							Guid guidValue = new Guid(guid.Value);

							//Look through the exported types to find the one that implements ModPlugin
							Type[] types = pluginAssembly.GetExportedTypes();
							foreach (Type type in types)
							{
								//Check that we don't have an entry already for this GUID
								if (m_plugins.ContainsKey(guidValue))
									break;

								if (type.BaseType == null)
									continue;

								Type[] filteredTypes = type.BaseType.GetInterfaces();
								foreach (Type interfaceType in filteredTypes)
								{
									if (interfaceType.FullName == typeof(IPlugin).FullName)
									{
										try
										{
											//Create an instance of the plugin object
											var pluginObject = Activator.CreateInstance(type);

											//And add it to the dictionary
											m_plugins.Add(guidValue, pluginObject);

											break;
										}
										catch (Exception ex)
										{
											Console.WriteLine(ex.ToString());
										}
									}
								}
							}

							break;
						}
						catch (Exception ex)
						{
							Console.WriteLine(ex.ToString());
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}

			Console.WriteLine("Finished loading plugins");
		}

		public void Init()
		{
			Console.WriteLine("Initializing plugins ...");

			foreach (var plugin in m_plugins.Values)
			{
				try
				{
					MethodInfo initMethod = plugin.GetType().GetMethod("Init");
					initMethod.Invoke(plugin, new object[] { });
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLine(ex);
				}
			}

			Console.WriteLine("Finished initializing plugins");

			m_initialized = true;
		}

		public void Update()
		{
			if (!SandboxGameAssemblyWrapper.GetInstance().IsGameStarted())
				return;

			foreach (var plugin in m_plugins.Values)
			{
				try
				{
					MethodInfo updateMethod = plugin.GetType().GetMethod("Update");
					updateMethod.Invoke(plugin, new object[] { });
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLine(ex);
				}
			}
		}

		#endregion
	}
}