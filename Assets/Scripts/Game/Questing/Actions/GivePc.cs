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
using System.Text.RegularExpressions;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;


namespace DaggerfallWorkshop.Game.Questing.Actions
{
    /// <summary>
    /// Give a quest Item to player. This has three formats:
    ///  * "give pc anItem" - Displays QuestComplete success message and opens loot window with reward. Could probably be called "give quest reward anItem".
    ///  * "give pc nothing" - Also displays QuestComplete success message but does not open loot window as no reward.
    ///  * "give pc anItem notify nnnn" - Places item directly into player's inventory and says message ID nnnn.
    /// </summary>
    public class GivePc : ActionTemplate
    {
        Symbol itemSymbol;
        int textId;
        bool isNothing;

        DaggerfallLoot rewardLoot = null;

        public override string Pattern
        {
            get { return @"give pc (?<anItem>[a-zA-Z0-9_.]+) notify (?<id>\d+)|give pc (?<anItem>[a-zA-Z0-9_.]+)|give pc (?<nothing>nothing)"; }
        }

        public GivePc(Quest parentQuest)
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
            GivePc action = new GivePc(parentQuest);
            action.itemSymbol = new Symbol(match.Groups["anItem"].Value);
            action.textId = Parser.ParseInt(match.Groups["id"].Value);
            if (!string.IsNullOrEmpty(match.Groups["nothing"].Value))
                action.isNothing = true;
            else
                action.isNothing = false;

            return action;
        }

        public override void Update(Task caller)
        {
            base.Update(caller);

            // Handle giving player nothing
            // This also shows QuestComplete message but does not give player loot
            if (isNothing)
            {
                OfferToPlayerWithQuestComplete(null);
                SetComplete();
                return;
            }

            // Attempt to get Item resource
            Item item = ParentQuest.GetItem(itemSymbol);
            if (item == null)
                throw new Exception(string.Format("Could not find Item resource symbol {0}", itemSymbol));

            // Give quest item to player based on command format
            if (textId != 0)
                DirectToPlayerWithNotify(item);
            else
                OfferToPlayerWithQuestComplete(item);
            
            SetComplete();
        }

        void OfferToPlayerWithQuestComplete(Item item)
        {
            // Show quest complete message
            DaggerfallMessageBox messageBox = ParentQuest.ShowMessagePopup((int)QuestMachine.QuestMessages.QuestComplete);

            // If no item for reward then we are done
            if (item == null)
                return;

            // Create a dropped loot container window for player to loot their reward
            rewardLoot = GameObjectHelper.CreateDroppedLootContainer(GameManager.Instance.PlayerObject, DaggerfallUnity.NextUID);
            rewardLoot.Items.AddItem(item.DaggerfallUnityItem);

            // Schedule loot window to open when player dismisses message
            messageBox.OnClose += QuestCompleteMessage_OnClose;
        }

        void DirectToPlayerWithNotify(Item item)
        {
            // Give player item and show notify message
            GameManager.Instance.PlayerEntity.Items.AddItem(item.DaggerfallUnityItem, Items.ItemCollection.AddPosition.Front);
            ParentQuest.ShowMessagePopup(textId);
        }

        private void QuestCompleteMessage_OnClose()
        {
            // Open loot reward container once QuestComplete dismissed
            if (rewardLoot != null)
            {
                DaggerfallUI.Instance.InventoryWindow.LootTarget = rewardLoot;
                DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenInventoryWindow);
            }
        }
    }
}