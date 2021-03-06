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

using System;
using UnityEngine;
using System.Text.RegularExpressions;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility;

namespace DaggerfallWorkshop.Game.Questing
{
    /// <summary>
    /// A clock is an alarm that that executes a task with the same symbol name.
    /// Clock must be started and stopped by quest actions.
    /// Clock runs down in game-time (default is 12x real-time).
    /// This also means timer is paused when game is paused.
    /// 
    /// Notes:
    ///  * Range values are unknown
    ///  * Flag value is unknown.
    ///  * Flag 1 is most common. Have observed 1, 2, 9, 12, 17, 18 in canonical quests.
    ///  * Suspect (flag & 16) = "set by distance". e.g. "Clock _qtime_ 00:00 0 flag 17 range 0 2".
    ///  * In classic, this sort of clock definition is used when player must travel long distances.
    /// </summary>
    public class Clock : QuestResource
    {
        const float returnTripMultiplier = 2.5f;

        DaggerfallDateTime lastWorldTimeSample;
        int startingTimeInSeconds;
        int remainingTimeInSeconds;
        int flag = 0;
        int minRange = 0;
        int maxRange = 0;
        bool clockEnabled = false;
        bool clockFinished = false;

        #region Properties

        public bool Enabled
        {
            get { return clockEnabled; }
            set { clockEnabled = value; }
        }

        public bool Finished
        {
            get { return clockFinished; }
        }

        public int Flag
        {
            get { return flag; }
            set { flag = value; }
        }

        public int MinRange
        {
            get { return minRange; }
            set { minRange = value; }
        }

        public int MaxRange
        {
            get { return maxRange; }
            set { maxRange = value; }
        }

        public int StartingTimeInSeconds
        {
            get { return startingTimeInSeconds; }
        }

        public int RemainingTimeInSeconds
        {
            get { return remainingTimeInSeconds; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="parentQuest">Parent quest.</param>
        public Clock(Quest parentQuest)
            : base(parentQuest)
        {
        }

        /// <summary>
        /// Construct a clock from QBN input.
        /// </summary>
        /// <param name="parentQuest">Parent quest.</param>
        /// <param name="line">Clock definition line from QBN.</param>
        public Clock(Quest parentQuest, string line)
            : base(parentQuest)
        {
            SetResource(line);
        }

        #endregion

        #region Overrides

        public override void SetResource(string line)
        {
            base.SetResource(line);

            string declMatchStr = @"(Clock|clock) (?<symbol>[a-zA-Z0-9_.-]+)";

            string optionsMatchStr = @"(?<ddhhmm>)\d+.\d+:\d+|" +
                                     @"(?<hhmm>)\d+:\d+|" +
                                     @"(?<mm>)\d+|" +
                                     @"flag (?<flag>\d+)|" +
                                     @"range (?<minRange>\d+) (?<maxRange>\d+)";

            // Try to match source line with pattern
            Match match = Regex.Match(line, declMatchStr);
            if (match.Success)
            {
                // Seed random
                //UnityEngine.Random.InitState(Time.renderedFrameCount);

                // Store symbol for quest system
                Symbol = new Symbol(match.Groups["symbol"].Value);

                // Split options from declaration
                string optionsLine = line.Substring(match.Length);

                // Match all options
                // TODO: Work out meaning of "flag" and "range" values
                int timeValue0 = -1;
                int timeValue1 = -1;
                int currentTimeValue = 0;
                MatchCollection options = Regex.Matches(optionsLine, optionsMatchStr);
                foreach (Match option in options)
                {
                    // Match any possible time value syntax
                    Group ddhhmmGroup = option.Groups["ddhhmm"];
                    Group hhmmGroup = option.Groups["hhmm"];
                    Group mmGroup = option.Groups["mm"];
                    if (ddhhmmGroup.Success || hhmmGroup.Success | mmGroup.Success)
                    {
                        // Get time value
                        int timeValue = MatchTimeValue(option.Value);

                        // Assign time value
                        if (currentTimeValue == 0)
                        {
                            timeValue0 = timeValue;
                            currentTimeValue++;
                        }
                        else if (currentTimeValue == 1)
                        {
                            timeValue1 = timeValue;
                            currentTimeValue++;
                        }
                        else
                        {
                            throw new Exception("Clock cannot specify more than 2 time values.");
                        }
                    }

                    // Unknown flag value
                    Group flagGroup = option.Groups["flag"];
                    if (flagGroup.Success)
                        flag = Parser.ParseInt(flagGroup.Value);

                    // Unknown minRange value
                    Group minRangeGroup = option.Groups["minRange"];
                    if (minRangeGroup.Success)
                        minRange = Parser.ParseInt(minRangeGroup.Value);

                    // Unknown maxRange value
                    Group maxRangeGroup = option.Groups["maxRange"];
                    if (maxRangeGroup.Success)
                        maxRange = Parser.ParseInt(maxRangeGroup.Value);
                }

                // Set total clock time based on values
                int clockTimeInSeconds = 0;
                if (currentTimeValue == 0)
                {
                    // No time value specifed: "clock _symbol_"
                    // Clock timer starts at a random value between 1 week and 1 minute
                    // TODO: Work out the actual range Daggerfall uses here
                    int minSeconds = GetTimeInSeconds(0, 0, 1);
                    int maxSeconds = GetTimeInSeconds(7, 0, 0);
                    clockTimeInSeconds = FromRange(minSeconds, maxSeconds);
                }
                else if (currentTimeValue == 1)
                {
                    // One time value specified: "clock _symbol_ dd.hh:mm"
                    // Clock timer starts at this value
                    clockTimeInSeconds = timeValue0;
                }
                else if (currentTimeValue == 2)
                {
                    // Two time values specified: "clock _symbol_ dd.hh:mm dd.hh:mm"
                    // Clock timer starts at a random point between timeValue0 (min) and timeValue1 (max)
                    // But second value must be greater than first to be a valid range
                    if (timeValue1 > timeValue0)
                        clockTimeInSeconds = FromRange(timeValue0, timeValue1);
                    else
                        clockTimeInSeconds = timeValue0;
                }

                // Flag & 16 with a 0.0:0 clock seems to indicate clock should be
                // set to 2.5x cautious travel time of first Place resource specificed in quest script
                // Quests using this configuration will usually end once timer elapsed
                if ((flag & 16) == 16 && timeValue0 == 0)
                {
                    clockTimeInSeconds = GetTravelTimeInSeconds();
                }

                // Set timer value in seconds
                InitialiseTimer(clockTimeInSeconds);
            }
        }

        public override bool ExpandMacro(MacroTypes macro, out string textOut)
        {
            textOut = string.Empty;
            switch(macro)
            {
                case MacroTypes.DetailsMacro:
                    textOut = GetDaysRemainingString();
                    return true;

                default:
                    return false;
            }
        }

        public override void Tick(Quest caller)
        {
            // Exit if not enabled or finished
            if (!clockEnabled || clockFinished)
                return;

            // Get how much time has passed in whole seconds
            DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now.Clone();
            ulong difference = now.ToSeconds() - lastWorldTimeSample.ToSeconds();

            // Remove time passed from time remaining
            remainingTimeInSeconds -= (int)difference;

            // Check if time is up
            if (remainingTimeInSeconds <= 0)
            {
                TriggerTask();
                clockEnabled = false;
                clockFinished = true;
                remainingTimeInSeconds = 0;
            }

            // Update last time sample to now
            lastWorldTimeSample = now;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts timer if not already running or complete.
        /// </summary>
        public void StartTimer()
        {
            if (!clockFinished)
            {
                clockEnabled = true;
                lastWorldTimeSample = DaggerfallUnity.Instance.WorldTime.Now.Clone();
            }
        }

        /// <summary>
        /// Stops clock from running if not already complete.
        /// </summary>
        public void StopTimer()
        {
            if (!clockFinished)
            {
                clockEnabled = false;
            }
        }

        public string GetDaysRemainingString()
        {
            TimeSpan time = TimeSpan.FromSeconds(remainingTimeInSeconds);

            return time.Days.ToString();
        }

        public string GetRemainingTimeString()
        {
            TimeSpan time = TimeSpan.FromSeconds(remainingTimeInSeconds);

            return string.Format("{0}.{1}:{2}", time.Days, time.Hours, time.Minutes);
        }

        #endregion

        #region Private Methods

        int MatchTimeValue(string line)
        {
            string matchStr = @"(?<days>\d+).(?<hours>\d+):(?<minutes>\d+)|(?<hours>\d+):(?<minutes>\d+)|(?<minutes>\d+)";

            Match match = Regex.Match(line, matchStr);
            if (match.Success)
            {
                int days = Parser.ParseInt(match.Groups["days"].Value);
                int hours = Parser.ParseInt(match.Groups["hours"].Value);
                int minutes = Parser.ParseInt(match.Groups["minutes"].Value);

                return GetTimeInSeconds(days, hours, minutes);
            }

            return 0;
        }

        int GetTimeInSeconds(int days, int hours, int minutes)
        {
            return (days * 86400) + (hours * 3600) + (minutes * 60);
        }

        int FromRange(int minSeconds, int maxSeconds)
        {
            return UnityEngine.Random.Range(minSeconds, maxSeconds);
        }

        void InitialiseTimer(int clockTimeInSeconds)
        {
            startingTimeInSeconds = clockTimeInSeconds;
            remainingTimeInSeconds = clockTimeInSeconds;
        }

        void TriggerTask()
        {
            // Attempt to get task with same symbol name as this timer
            Task task = ParentQuest.GetTask(Symbol);
            if (task != null)
            {
                task.Set();
            }
            else
            {
                Debug.LogFormat("Clock timer {0} completed but could not find a task with same name.", Symbol.Name);
            }
        }

        int GetTravelTimeInSeconds()
        {
            // First Place resource should be main location of quest
            // This seems to hold true in all quests looked at in detail so far
            QuestResource[] placeResources = ParentQuest.GetAllResources(typeof(Place));
            if (placeResources == null || placeResources.Length == 0)
            {
                Debug.LogError("Clock wants a travel time but quest has no Place resources.");
                return 0;
            }

            // Get location from place resource
            DFLocation location;
            Place place = (Place)placeResources[0];
            if (!DaggerfallUnity.Instance.ContentReader.GetLocation(place.SiteDetails.regionName, place.SiteDetails.locationName, out location))
            {
                Debug.LogErrorFormat("Could not find Quest Place {0}/{1}", place.SiteDetails.regionName, place.SiteDetails.locationName);
                return 0;
            }

            // Get end position in map pixel coordinates
            DFPosition endPos = MapsFile.WorldCoordToMapPixel(location.Exterior.RecordElement.Header.X, location.Exterior.RecordElement.Header.Y);

            // Create a path to location
            // Use the most cautious time possible allowing for player to camp out or stop at inns along the way
            TravelTimeCalculator travelTimeCalculator = new TravelTimeCalculator();
            travelTimeCalculator.GeneratePath(endPos);
            travelTimeCalculator.CalculateTravelTimeTotal(true, false, false, true, false);

            // Get travel time in total days across land and water
            // Allow for return trip travel time plus some fudge for side goals and quest process itself
            // How Daggerfall actually calcutes travel time is currently unknown
            // This should be fair and realistic in most circumstances while still creating time pressures
            // Modify "returnTripMultiplier" to make calcualted quest time harder/easier
            int travelTimeDaysLand = 0;
            int travelTimeDaysWater = 0;
            int travelTimeDaysTotal = 0;
            if (travelTimeCalculator.TravelTimeTotalLand > 0)
                travelTimeDaysLand = (int)((travelTimeCalculator.TravelTimeTotalLand / 60 / 24) + 0.5);
            if (travelTimeCalculator.TravelTimeTotalWater > 0)
                travelTimeDaysWater = (int)((travelTimeCalculator.TravelTimeTotalWater / 60 / 24) + 0.5);
            travelTimeDaysTotal = (int)((travelTimeDaysLand + travelTimeDaysWater) * returnTripMultiplier);

            return GetTimeInSeconds(travelTimeDaysTotal, 0, 0);
        }

        #endregion
    }
}