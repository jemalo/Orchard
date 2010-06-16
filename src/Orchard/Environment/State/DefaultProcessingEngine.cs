﻿using System;
using System.Collections.Generic;
using System.Linq;
using Orchard.Environment.Configuration;
using Orchard.Environment.ShellBuilders;
using Orchard.Environment.Descriptor.Models;
using Orchard.Events;
using Orchard.Logging;

namespace Orchard.Environment.State {
    public class DefaultProcessingEngine : Component, IProcessingEngine {
        private readonly IShellContextFactory _shellContextFactory;
        private readonly IList<Entry> _entries = new List<Entry>();


        public DefaultProcessingEngine(
            IShellContextFactory shellContextFactory) {
            _shellContextFactory = shellContextFactory;
        }

        public string AddTask(ShellSettings shellSettings, ShellDescriptor shellDescriptor, string eventName, Dictionary<string, object> parameters) {
            var entry = new Entry {
                ShellSettings = shellSettings,
                ShellDescriptor = shellDescriptor,
                MessageName = eventName,
                EventData = parameters,
                TaskId = Guid.NewGuid().ToString("n"),
                ProcessId = Guid.NewGuid().ToString("n"),
            };
            Logger.Information("Adding event {0} to process {1} for shell {2}", 
                eventName, 
                entry.ProcessId,
                shellSettings.Name);
            lock (_entries) {
                _entries.Add(entry);
                return entry.ProcessId;
            }
        }


        public class Entry {
            public string ProcessId { get; set; }
            public string TaskId { get; set; }

            public ShellSettings ShellSettings { get; set; }
            public ShellDescriptor ShellDescriptor { get; set; }
            public string MessageName { get; set; }
            public Dictionary<string, object> EventData { get; set; }
        }


        public bool AreTasksPending() {
            lock (_entries)
                return _entries.Any();
        }

        public void ExecuteNextTask() {
            Entry entry;
            lock (_entries) {
                if (!_entries.Any())
                    return;
                entry = _entries.First();
                _entries.Remove(entry);
            }
            Execute(entry);
        }

        private void Execute(Entry entry) {
            var shellContext = _shellContextFactory.CreateDescribedContext(entry.ShellSettings, entry.ShellDescriptor);
            using (shellContext.LifetimeScope) {
                using (var standaloneEnvironment = new StandaloneEnvironment(shellContext.LifetimeScope)) {
                    var eventBus = standaloneEnvironment.Resolve<IEventBus>();

                    Logger.Information("Executing event {0} in process {1} for shell {2}", 
                        entry.MessageName,
                        entry.ProcessId, 
                        entry.ShellSettings.Name);
                    eventBus.Notify(entry.MessageName, entry.EventData);
                }
            }
        }
    }
}
