﻿// Copyright (c) 2020 Pryaxis & Orion Contributors
// 
// This file is part of Orion.
// 
// Orion is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Orion is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Orion.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Ninject;
using Orion.Core;
using Orion.Core.Framework;
using Orion.Launcher.Properties;
using Serilog;

namespace Orion.Launcher.Extensions
{
    internal sealed class OrionExtensionManager : IExtensionManager, IDisposable
    {
        private readonly ILogger _log;
        private readonly IKernel _kernel = new StandardKernel();

        private readonly ISet<Type> _serviceInterfaceTypes = new HashSet<Type>();
        private readonly IDictionary<Type, ISet<Type>> _serviceBindingTypes = new Dictionary<Type, ISet<Type>>();
        private readonly ISet<Type> _pluginTypes = new HashSet<Type>();

        private readonly Dictionary<string, OrionExtension> _plugins = new Dictionary<string, OrionExtension>();

        public OrionExtensionManager(IServer server, ILogger log)
        {
            Debug.Assert(log != null);

            _log = log;

            _kernel.Bind<IServer>().ToConstant(server).InSingletonScope();

            // Bind `Ilogger` so that extensions retrieve extension-specific logs.
            _kernel
                .Bind<ILogger>()
                .ToMethod(ctx =>
                {
                    var type = ctx.Request.Target.Member.ReflectedType;
                    Debug.Assert(type != null);

                    var name =
                        type.GetCustomAttribute<BindingAttribute>()?.Name ??
                        type.GetCustomAttribute<PluginAttribute>()!.Name;
                    return log.ForContext("Name", name);
                })
                .InTransientScope();
        }

        public IReadOnlyDictionary<string, OrionExtension> Plugins => _plugins;

        public void Dispose()
        {
            _kernel.Dispose();
        }

        public void Load(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            LoadServiceInterfaceTypes();
            LoadServiceBindingTypes();
            LoadPluginTypes();

            void LoadServiceInterfaceTypes()
            {
                _serviceInterfaceTypes.UnionWith(
                    assembly.ExportedTypes
                        .Where(t => t.IsInterface)
                        .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null));
            }

            void LoadServiceBindingTypes()
            {
                foreach (var bindingType in assembly.DefinedTypes
                    .Where(t => !t.IsAbstract)
                    .Where(t => t.GetCustomAttribute<BindingAttribute>() != null))
                {
                    foreach (var interfaceType in bindingType
                        .GetInterfaces()
                        .Where(_serviceInterfaceTypes.Contains))
                    {
                        if (!_serviceBindingTypes.TryGetValue(interfaceType, out var types))
                        {
                            types = new HashSet<Type>();
                            _serviceBindingTypes[interfaceType] = types;
                        }

                        types.Add(bindingType);
                    }
                }
            }

            void LoadPluginTypes()
            {
                foreach (var pluginType in assembly.ExportedTypes
                    .Where(t => !t.IsAbstract)
                    .Where(t => t.GetCustomAttribute<PluginAttribute>() != null))
                {
                    _pluginTypes.Add(pluginType);

                    var pluginName = pluginType.GetCustomAttribute<PluginAttribute>()!.Name;
                    _log.Information(Resources.LoadedPlugin, pluginName);
                }
            }
        }

        public void Initialize()
        {
            InitializeServiceBindings();
            InitializePlugins();

            // Clear out the loaded types so that a second `Initialize` call won't perform redundant initialization
            // logic.
            _serviceInterfaceTypes.Clear();
            _serviceBindingTypes.Clear();
            _pluginTypes.Clear();
            
            void InitializeServiceBindings()
            {
                foreach (var (interfaceType, bindingTypes) in _serviceBindingTypes)
                {
                    var bindingType = bindingTypes
                        .OrderByDescending(t => t.GetCustomAttribute<BindingAttribute>()!.Priority)
                        .FirstOrDefault();
                    if (bindingType is null)
                    {
                        // We didn't find a binding for `interfaceType`, so continue.
                        continue;
                    }

                    switch (interfaceType.GetCustomAttribute<ServiceAttribute>()!.Scope)
                    {
                    case ServiceScope.Singleton:
                        _kernel.Bind(interfaceType).To(bindingType).InSingletonScope();

                        // Eagerly request singleton-scoped serivces so that an instance always exists.
                        _ = _kernel.Get(interfaceType);
                        break;

                    case ServiceScope.Transient:
                        _kernel.Bind(interfaceType).To(bindingType).InTransientScope();
                        break;

                    default:
                        // Not localized because this string is developer-facing.
                        throw new InvalidOperationException("Invalid service scope");
                    }
                }
            }

            void InitializePlugins()
            {
                // Initialize the plugin bindings to allow plugin dependencies.
                foreach (var pluginType in _pluginTypes)
                {
                    _kernel.Bind(pluginType).ToSelf().InSingletonScope();
                }

                // Initialize the plugins.
                foreach (var pluginType in _pluginTypes)
                {
                    var attribute = pluginType.GetCustomAttribute<PluginAttribute>()!;
                    var pluginName = attribute.Name;
                    var pluginVersion = pluginType.Assembly.GetName().Version;
                    var pluginAuthor = attribute.Author;
                    _log.Information(Resources.InitializedPlugin, pluginName, pluginVersion, pluginAuthor);

                    var plugin = (OrionExtension)_kernel.Get(pluginType);
                    _plugins[pluginName] = plugin;
                }
            }
        }
    }
}
