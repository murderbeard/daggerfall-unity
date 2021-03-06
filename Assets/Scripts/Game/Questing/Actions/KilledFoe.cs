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
using System.Collections;
using System.Text.RegularExpressions;
using System;

namespace DaggerfallWorkshop.Game.Questing.Actions
{
    public class KilledFoe : ActionTemplate
    {
        Symbol foeSymbol;
        int killsRequired;
        int sayingID;

        public override string Pattern
        {
            get { return @"killed (?<kills>\d+) (?<aFoe>[a-zA-Z0-9_.-]+) (saying (?<sayingID>\d+))|killed (?<kills>\d+) (?<aFoe>[a-zA-Z0-9_.-]+)|killed (?<aFoe>[a-zA-Z0-9_.-]+)"; }
        }

        public KilledFoe(Quest parentQuest)
            : base(parentQuest)
        {
            IsTriggerCondition = true;
        }

        public override IQuestAction CreateNew(string source, Quest parentQuest)
        {
            base.CreateNew(source, parentQuest);

            // Source must match pattern
            Match match = Test(source);
            if (!match.Success)
                return null;

            // Factory new action
            KilledFoe action = new KilledFoe(parentQuest);
            action.foeSymbol = new Symbol(match.Groups["aFoe"].Value);
            action.killsRequired = Parser.ParseInt(match.Groups["kills"].Value);
            action.sayingID = Parser.ParseInt(match.Groups["sayingID"].Value);

            // Kills required must be 1 or more
            if (action.killsRequired < 1)
                action.killsRequired = 1;

            return action;
        }

        public override bool CheckTrigger(Task caller)
        {
            // Get related Foe resource
            Foe foe = ParentQuest.GetFoe(foeSymbol);
            if (foe == null)
                return false;

            // Check total kills recorded on this Foe
            if (foe.KillCount >= killsRequired)
            {
                // Popup saying message
                if (sayingID != 0)
                    ParentQuest.ShowMessagePopup(sayingID);

                return true;
            }

            return false;
        }
    }
}