﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2017 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Questing
{
    /// <summary>
    /// Contains live state of quests in play.
    /// Quests are instantiated from text source and executed inside quest machine.
    /// Each quest is assigned a unique id (UID) as its possible for same question to be instantiated multiple times,
    /// such as a basic fetch quest to two different dungeons. The name of quest cannot not be used for unique identification.
    /// Child resources generally will not care about quest UID, but this is used by quest machine.
    /// </summary>
    public class Quest : IDisposable
    {
        #region Fields

        // Quest object collections
        Dictionary<int, Message> messages = new Dictionary<int, Message>();
        Dictionary<string, Task> tasks = new Dictionary<string, Task>();

        // Clock, Place, Person, Foe, etc. all share a common resource dictionary
        Dictionary<string, QuestResource> resources = new Dictionary<string, QuestResource>();

        ulong uid;
        bool questComplete = false;
        Dictionary<int, LogEntry> activeLogMessages = new Dictionary<int, LogEntry>();

        string questName;
        string displayName;
        DaggerfallDateTime questStartTime;

        Person lastPersonReferenced = null;
        bool questBreak = false;

        #endregion

        #region Structures

        /// <summary>
        /// Stores active log messages as ID only.
        /// This allows text to be linked/unlinked into log without duplication of text.
        /// Actual quest UI can decide how to present these messages to user.
        /// </summary>
        public struct LogEntry
        {
            public int stepID;
            public int messageID;
        }

        /// <summary>
        /// Basic information about a single task
        /// </summary>
        public struct TaskState
        {
            public Task.TaskType type;
            public Symbol symbol;
            public bool set;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets quest UID assigned at create time.
        /// </summary>
        public ulong UID
        {
            get { return uid; }
        }

        /// <summary>
        /// True when quest has completed and will be deleted from quest machine.
        /// </summary>
        public bool QuestComplete
        {
            get { return questComplete; }
        }


        /// <summary>
        /// Short quest name read from source.
        /// e.g. "_BRISIEN"
        /// </summary>
        public string QuestName
        {
            get { return questName; }
            set { questName = value; }
        }

        /// <summary>
        /// Optional display name for future quest journal.
        /// e.g. "Lady Brisienna's Letter"
        /// </summary>
        public string DisplayName
        {
            get { return displayName; }
            set { displayName = value; }
        }

        /// <summary>
        /// Gets world time of quest when started.
        /// </summary>
        public DaggerfallDateTime QuestStartTime
        {
            get { return questStartTime; }
        }

        /// <summary>
        /// Gets or sets last Person resource encountered during macro expand.
        /// This will be used to resolve pronoun, god, oath, etc.
        /// Can return null so caller should have a fail-over plan.
        /// </summary>
        public Person LastPersonReferenced
        {
            get { return lastPersonReferenced; }
            set { lastPersonReferenced = value; }
        }

        /// <summary>
        /// Allows other classes working on this quest to break execution.
        /// This allows for "say" messages and other popups to happen in correct order.
        /// Flag will be lowered automatically.
        /// </summary>
        public bool QuestBreak
        {
            get { return questBreak; }
            set { questBreak = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Quest()
        {
            uid = DaggerfallUnity.NextUID;
            questStartTime = new DaggerfallDateTime(DaggerfallUnity.Instance.WorldTime.Now);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update quest.
        /// </summary>
        public void Update()
        {
            // Do nothing if complete
            // Now waiting to be removed from quest machine
            if (questComplete)
                return;

            // Tick resources
            foreach(QuestResource resource in resources.Values)
            {
                resource.Tick(this);
            }

            // Update tasks
            foreach(Task task in tasks.Values)
            {
                // Handle quest break or completion
                if (questBreak || questComplete)
                {
                    questBreak = false;
                    return;
                }

                task.Update();
            }
        }

        public void EndQuest()
        {
            questComplete = true;
        }

        public void SetTask(Symbol symbol)
        {
            Task task = GetTask(symbol);
            if (task != null)
                task.Set();
        }

        public void UnsetTask(Symbol symbol)
        {
            Task task = GetTask(symbol);
            if (task != null)
                task.Unset();
        }

        public TaskState[] GetTaskStates()
        {
            List<TaskState> states = new List<TaskState>();
            foreach(Task task in tasks.Values)
            {
                TaskState state = new TaskState();
                state.type = task.Type;
                state.symbol = task.Symbol;
                state.set = task.IsSet;
                states.Add(state);
            }

            return states.ToArray();
        }

        public void Dispose()
        {
            // Dispose of quest resources
            foreach(QuestResource resource in resources.Values)
            {
                resource.Dispose();
            }
        }

        #endregion

        #region Log Message Methods

        /// <summary>
        /// Adds quest log message for quest at step position.
        /// Quests can only have 0-9 steps and cannot log the same step more than once at a time.
        /// </summary>
        /// <param name="stepID">StepID to key this message.</param>
        /// <param name="messageID">MessageID to display for this step.</param>
        public void AddLogStep(int stepID, int messageID)
        {
            // Replacing existing log step if it exists
            if (activeLogMessages.ContainsKey(stepID))
            {
                activeLogMessages.Remove(stepID);
            }

            // Add the step to active log messages
            LogEntry entry = new LogEntry();
            entry.stepID = stepID;
            entry.messageID = messageID;
            activeLogMessages.Add(stepID, entry);
        }

        /// <summary>
        /// Removes quest log message for step position.
        /// </summary>
        /// <param name="stepID">StepID to remove from quest log.</param>
        public void RemoveLogStep(int stepID)
        {
            // Remove the step
            if (activeLogMessages.ContainsKey(stepID))
            {
                activeLogMessages.Remove(stepID);
            }
        }

        /// <summary>
        /// Gets the active log messages associated with this quest.
        /// Usually only one log step is active at a time.
        /// This allows log messages to be displayed however desired (as list, verbose, sorted, etc.).
        /// </summary>
        /// <returns>LogEntry array.</returns>
        public LogEntry[] GetLogMessages()
        {
            // Create an array of active log messages
            LogEntry[] logs = new LogEntry[activeLogMessages.Count];
            int count = 0;
            foreach (LogEntry log in activeLogMessages.Values)
            {
                logs[count] = log;
                count++;
            }

            return logs;
        }

        ///// <summary>
        ///// Quick test of grabbing log message before UI is ready.
        ///// To be removed.
        ///// </summary>
        //public void TestLogMessages()
        //{
        //    LogEntry[] logs = GetLogMessages();
        //    for (int i = 0; i < logs.Length; i++)
        //    {
        //        Message message = GetMessage(logs[i].messageID);
        //        if (message != null)
        //        {
        //            // Get message tokens
        //            DaggerfallConnect.Arena2.TextFile.Token[] tokens = message.GetTextTokens();

        //            UserInterfaceWindows.DaggerfallMessageBox messageBox = new UserInterfaceWindows.DaggerfallMessageBox(DaggerfallUI.UIManager);
        //            messageBox.SetTextTokens(tokens);
        //            messageBox.ClickAnywhereToClose = true;
        //            messageBox.AllowCancel = true;
        //            messageBox.ParentPanel.BackgroundColor = Color.clear;
        //            messageBox.Show();
        //        }
        //    }
        //}

        #endregion

        #region Resource Query Methods

        public Message GetMessage(int messageID)
        {
            if (messages.ContainsKey(messageID))
                return messages[messageID];
            else
                return null;
        }

        public Task GetTask(Symbol symbol)
        {
            if (symbol != null && tasks.ContainsKey(symbol.Name))
                return tasks[symbol.Name];
            else
                return null;
        }

        public Clock GetClock(Symbol symbol)
        {
            return GetResource(symbol) as Clock;
        }

        public Place GetPlace(Symbol symbol)
        {
            return GetResource(symbol) as Place;
        }

        public Person GetPerson(Symbol symbol)
        {
            return GetResource(symbol) as Person;
        }

        public Item GetItem(Symbol symbol)
        {
            return GetResource(symbol) as Item;
        }

        public Foe GetFoe(Symbol symbol)
        {
            return GetResource(symbol) as Foe;
        }

        public QuestResource GetResource(string name)
        {
            if (!string.IsNullOrEmpty(name) && resources.ContainsKey(name))
                return resources[name];
            else
                return null;
        }

        public QuestResource GetResource(Symbol symbol)
        {
            if (symbol != null && resources.ContainsKey(symbol.Name))
                return resources[symbol.Name];
            else
                return null;
        }

        public QuestResource[] GetAllResources(Type resourceType)
        {
            List<QuestResource> foundResources = new List<QuestResource>();

            foreach (var kvp in resources)
            {
                if (kvp.Value.GetType() == resourceType)
                {
                    foundResources.Add(kvp.Value);
                }
            }

            return foundResources.ToArray();
        }

        public DaggerfallMessageBox ShowMessagePopup(int id)
        {
            // Get message resource
            Message message = GetMessage(id);
            if (message == null)
                return null;

            // Get message tokens
            TextFile.Token[] tokens = message.GetTextTokens();

            // Present popup message
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetTextTokens(tokens);
            messageBox.ClickAnywhereToClose = true;
            messageBox.AllowCancel = true;
            messageBox.ParentPanel.BackgroundColor = Color.clear;
            messageBox.Show();

            // Set a quest break so popup will display immediately
            questBreak = true;

            return messageBox;
        }

        #endregion

        #region Resource Allocation Methods

        public void AddMessage(int messageID, Message message)
        {
            messages.Add(messageID, message);
        }

        public void AddResource(QuestResource resource)
        {
            if (resource.Symbol == null || string.IsNullOrEmpty(resource.Symbol.Name))
            {
                throw new Exception("QuestResource must have a named symbol.");
            }

            if (resources.ContainsKey(resource.Symbol.Name))
            {
                throw new Exception(string.Format("Duplicate QuestResource symbol name found: {0}", resource.Symbol));
            }

            resources.Add(resource.Symbol.Name, resource);
        }

        public void AddTask(Task task)
        {
            if (!tasks.ContainsKey(task.Symbol.Name))
                tasks.Add(task.Symbol.Name, task);
            else
            {
                //task w/ this symbol already exists, add actions to it
                var existingTask = tasks[task.Symbol.Name];
                existingTask.CopyQuestActions(task);
            }
        }

        #endregion

        #region Private Methods
        #endregion
    }
}