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
    public class DropFace : ActionTemplate
    {
        Symbol personSymbol;

        public override string Pattern
        {
            get { return @"drop (?<anNPC>[a-zA-Z0-9_.-]+) face"; }
        }

        public DropFace(Quest parentQuest)
            : base(parentQuest)
        {
        }

        public override IQuestAction CreateNew(string source, Quest parentQuest)
        {
            base.CreateNew(source, parentQuest);

            // Source must match pattern
            Match match = Test(source);
            if (!match.Success)
                return null;

            // Factory new action
            DropFace action = new DropFace(parentQuest);
            action.personSymbol = new Symbol(match.Groups["anNPC"].Value);

            return action;
        }

        public override void Update(Task caller)
        {
            base.Update(caller);

            // Get related Person resource
            Person person = ParentQuest.GetPerson(personSymbol);
            if (person == null)
                return;

            // Drop face from HUD
            DaggerfallUI.Instance.DaggerfallHUD.EscortingFaces.DropFace(person);

            SetComplete();
        }
    }
}