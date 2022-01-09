using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using SandBox;
using TaleWorlds.Engine;
using TaleWorlds.ObjectSystem;
using StoryMode.Missions;
using TaleWorlds.GauntletUI;

namespace SmallEvents
{
    public class MySubModule : MBSubModuleBase
    {
        public static CampaignGameStarter campaignGameStarter;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
        }

        
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            var campaign = game.GameType as Campaign;
            if(campaign != null)
            {
                campaignGameStarter = (CampaignGameStarter)gameStarterObject;
                campaignGameStarter.AddBehavior(new LetterQuestCampaignBehavior());
                campaignGameStarter.AddBehavior(new StopFightQuestCampaignBehavior());

            }
            
        }

        
    }

    

    public class LetterQuestCampaignBehavior : CampaignBehaviorBase
    {

        //General properties
        private Agent _questTarget;
        private Agent _questGiver;
        
        private QuestGivable _questGivable = QuestGivable.NotCheckedYet;
        private QuestState _questState = QuestState.QuestNotEncountered;

        //Quest flag properties
        enum QuestState
        {
            QuestNotEncountered,
            QuestAccepted,
            QuestDeclined,
            QuestInDeliveryProcess,
            QuestDelivered,
            QuestDeliveredProcess,
            QuestBeforeFinishedByAccept,
            QuestFinishedByAccept,
            QuestFinishedByDecline
        }

        enum QuestGivable
        {
            NotCheckedYet,
            NotGivable,
            Givable
        }

        

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
            
        }
        
        public override void SyncData(IDataStore dataStore)
        {
        }
        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
                    AddDialogs(campaignGameStarter);
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if(party == MobileParty.MainParty && !(_questState == QuestState.QuestFinishedByAccept || _questState == QuestState.QuestFinishedByDecline)) //If quest finished successfully or declined, mission does not show up again.
            {
                _questTarget = null;
                _questGiver = null;
                _questState = QuestState.QuestNotEncountered;
                _questGivable = QuestGivable.NotCheckedYet;
               
            }
        }

        private void OnMissionEnded(IMission mission)
        {
            var village = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationVillageCenter);
            var city = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationCenter);

            if (CampaignMission.Current.Location == village || CampaignMission.Current.Location == city)
            {
                _questTarget = null;
                _questGiver = null;
                _questState = QuestState.QuestNotEncountered;
                _questGivable = QuestGivable.NotCheckedYet;
            }
        }

        private void AddDialogs(CampaignGameStarter campaignGameStarter)
        {
           
            //Conversation for when the quest is being given.
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_1", "start", "next_conversation_token", "{=*}Hey, can you do a small favor for me? I have been trying to find someone who can send a letter to someone I like. Can you do this for me in exchange for 50 golds?", CanTakeLetterQuest, () => {/* has no consequence */},priority: 2000);
            campaignGameStarter.AddPlayerLine("letter_quest_conversation_player_id_1", "next_conversation_token", "final_conversation_token", "{=*}Why not, Where can I find her?", CanTakeLetterQuest, LetterQuestAccepted);
            campaignGameStarter.AddPlayerLine("letter_quest_conversation_player_id_2", "next_conversation_token", "final_conversation_token", "{=*}I have no time for your whims.", CanTakeLetterQuest, LetterQuestDeclined);
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_2", "final_conversation_token", "close_window", "{=*}Thank you so much. Actually, she is the only {LETTER_QUEST_GIRL_TYPE} around here. I'm sure you can find her easily.", LetterQuestAcceptedContinue, LetterQuestAcceptedContinueConsequence);
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_3", "final_conversation_token", "close_window", "{=*}I hope you get the chance to realize some day, that this is not a whim. Goodbye.", LetterQuestDeclinedContinue, LetterQuestDeclinedContinueConsequence);

            //Conversation for when the quest is being delivered
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_4", "start", "next_conversation_token", "{=*}What is it?", CanGiveLetterQuest, () => {/* has no consequence */}, priority: 2000);
            campaignGameStarter.AddPlayerLine("letter_quest_conversation_player_id_3", "next_conversation_token", "final_conversation_token", "{=*}A guy wanted me to give this letter to you.", CanGiveLetterQuest, DeliverLetterQuest);
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_5", "final_conversation_token", "close_window", "{=*}Thank you.", LetterQuestDeliverFinish, LetterQuestDeliverFinishConsequence);


            //Conversation for when the quest is being finished
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_4", "start", "next_conversation_token", "{=*}Have you given the letter yet?", CanDisplayFinishSection, () => {/* has no consequence */}, priority: 2000);
            campaignGameStarter.AddPlayerLine("letter_quest_conversation_player_id_3", "next_conversation_token", "final_conversation_token", "{=*}Yes.", CanFinishLetterQuest, FinishingLetterQuest);
            campaignGameStarter.AddPlayerLine("letter_quest_conversation_player_id_4", "next_conversation_token", "close_window", "{=*}No, not yet.", LetterQuestNotYetDelivered, () => {/* has no consequence */});
            campaignGameStarter.AddDialogLine("letter_quest_conversation_id_5", "final_conversation_token", "close_window", "{=*}Thank you.", LetterQuestFinish, GiveQuestAward);
        }

        private bool LetterQuestNotYetDelivered()
        {
            if (( _questState == QuestState.QuestDeliveredProcess || _questState == QuestState.QuestInDeliveryProcess)&& _questGiver == (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0])
            {

                return true;
            }
            else
            {
                return false;
            }
        }

        private void LetterQuestDeliverFinishConsequence()
        {
            InformationManager.DisplayMessage(new InformationMessage("Quest completed. Talk to the letter giver for your award."));
        }

        private bool CanDisplayFinishSection()
        {
            if( (_questState == QuestState.QuestInDeliveryProcess || _questState == QuestState.QuestDeliveredProcess) && _questGiver == (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0])
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CanFinishLetterQuest()
        {
            if(_questState == QuestState.QuestDeliveredProcess && _questGiver == (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0])
            {
                
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool LetterQuestFinish()
        {
            if(_questState == QuestState.QuestBeforeFinishedByAccept)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void GiveQuestAward()
        {
            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 50); //giving 50 golds.
            
        }

        private void FinishingLetterQuest()
        {
            _questState = QuestState.QuestBeforeFinishedByAccept;
        }

        private bool CanTakeLetterQuest()
        {

            

            if(_questGivable == QuestGivable.NotCheckedYet)
            {
                CheckQuestPossible();
            }


            if (_questState == QuestState.QuestNotEncountered && !(_questGivable == QuestGivable.NotGivable)) //if quest is not encountered and it is givable
            {
                if (((Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0]).IsHuman && !(((Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0]).IsFemale) && !(((Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0]).IsHero)) //Here, we dont let Hero characters give the mission. It can be changed.
                {
                    return true;
                } 
            }
            
            
            return false;
            
                
        }

        //Checks if there is a female with a unique character type
        private void CheckQuestPossible()
        {
            IReadOnlyList<Agent> agentsList = Mission.Current.Agents;

            var femaleAgentDictionary = new Dictionary<string, Agent>();

            foreach (var agent in agentsList)
            {
                if (agent.IsHuman && agent.IsFemale && !(agent.IsHero) && !(agent.IsMainAgent))
                {
                    

                    if(femaleAgentDictionary.ContainsKey(agent.Character.Name.ToString()))
                    {
                        femaleAgentDictionary[agent.Character.Name.ToString()] = null;
                    }
                    else
                    {
                        femaleAgentDictionary.Add(agent.Character.Name.ToString(), agent);
                    }
                }

            }

            foreach (KeyValuePair<string, Agent> pair in femaleAgentDictionary)
            {
                if(pair.Value != null)
                {
                    _questTarget = pair.Value;
                    
                }
                
            }

            if(_questTarget == null)
            {
                
                _questGivable = QuestGivable.NotGivable;
            }
            else
            {
                //There will be a %20 chance of taking the quest.
                //Random rand = new Random();
                //int chance = rand.Next(1, 101);

                //if(chance < 21) //%20
                //{
                    _questGivable = QuestGivable.Givable;
                //}
                //else //%80
                //{
                    //_questGivable = QuestGivable.NotGivable;
                //}
                
            }

        }

        private bool LetterQuestDeliverFinish()
        {   
            if (_questState == QuestState.QuestDelivered)
            {
                _questState = QuestState.QuestDeliveredProcess;
                return true;
            }
            else
            {
                return false;
            }
                
        }

        private void DeliverLetterQuest()
        {
            _questState = QuestState.QuestDelivered;
        }
        private bool CanGiveLetterQuest()
        {
            if (_questState == QuestState.QuestInDeliveryProcess && _questTarget == (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0])
            {
                return true;
            }
            else
            {
                return false;
            }
                
        }


        private bool LetterQuestAcceptedContinue()
        {
            if (_questState == QuestState.QuestAccepted)
            {
                _questState = QuestState.QuestInDeliveryProcess;
                return true;
            }
            else
            {
                return false;
            }
                
        }

        private bool LetterQuestDeclinedContinue()
        {
            if (_questState == QuestState.QuestDeclined)
            {
                return true;
            }
            else
            {
                return false;
            }
                
        }

        private void LetterQuestAcceptedContinueConsequence()
        {
            _questState = QuestState.QuestInDeliveryProcess;
        }

        private void LetterQuestDeclinedContinueConsequence()
        {
            _questState = QuestState.QuestFinishedByDecline;
            
        }

        private void LetterQuestAccepted()
        {
            _questState = QuestState.QuestAccepted;
            _questGiver = (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0];
            
            MBTextManager.SetTextVariable("LETTER_QUEST_GIRL_TYPE", _questTarget.Name); //Girl type
        }
        
        private void LetterQuestDeclined()
        {
            _questState = QuestState.QuestDeclined;
        }
        

        
        
    }

    public class StopFightQuestCampaignBehavior : CampaignBehaviorBase
    {

        //General properties
        private Agent _questAgent1;
        private Agent _questAgent2;
        private QuestGivable _questGivable = QuestGivable.notCheckedYet;
        private QuestState _questState = QuestState.notTalkedYet;
        private IReadOnlyList<Agent> _agentsList;
        private bool _agentListAcquired = false;

        ActionIndexCache _questAgent1ActCh0;
        ActionIndexCache _questAgent1ActCh1;

        ActionIndexCache _questAgent2ActCh0;
        ActionIndexCache _questAgent2ActCh1;
        //Quest flag properties
        enum QuestGivable
        {
            notCheckedYet,
            givable,
            notGivable
        }

        enum QuestState
        {
            notTalkedYet,
            talked,
            finished
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
            CampaignEvents.MissionTickEvent.AddNonSerializedListener(this, OnMissionTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddDialogs(campaignGameStarter);
        }

        
        private void OnMissionEnded(IMission mission)
        {
            var village = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationVillageCenter);
            var city = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationCenter);

            if (CampaignMission.Current.Location == village || CampaignMission.Current.Location == city)
            {
                _questAgent1 = null;
                _questAgent2 = null;
                _agentListAcquired = false;
                _agentsList = null;
                _questGivable = QuestGivable.notCheckedYet;
            }
        }
        private void OnMissionTick(float number)
        {
            
            if(!_agentListAcquired && _questState != QuestState.finished && _questGivable == QuestGivable.notCheckedYet)
            {
                
                var village = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationVillageCenter);
                var city = PlayerEncounter.LocationEncounter.Settlement.LocationComplex.GetLocationWithId(CampaignData.LocationCenter);

                if (CampaignMission.Current.Location == village || CampaignMission.Current.Location == city)
                {
                    
                    _agentsList = Mission.Current.Agents;
                    if(_agentsList != null && _agentsList.Count != 0)
                    {
                        _agentListAcquired = true;
                    }
                    else
                    {
                        return;
                    }    
                    
                    for (var i = 0; i < _agentsList.Count; i++)
                    {

                        Agent agentToCompare1 = _agentsList[i];
                        if (agentToCompare1.IsHuman && !(agentToCompare1.IsFemale) && !(agentToCompare1.IsHero) && !(agentToCompare1.IsMainAgent) && (agentToCompare1.Age > 18) /*&& !(agentToCompare1.GetCurrentVelocity().IsNonZero())*/)
                        {
                            
                            for (var k = i + 1; k < _agentsList.Count; k++)
                            {
                                var agentToCompare2 = _agentsList[k];

                                if (agentToCompare2.IsHuman && !(agentToCompare2.IsFemale) && !(agentToCompare2.IsHero) && !(agentToCompare2.IsMainAgent) && (agentToCompare2.Age > 18))
                                {
                                    
                                    if (agentToCompare1.Position.Distance(agentToCompare2.Position) < 3) //less than 3 meters
                                    {
                                        _questAgent1 = agentToCompare1;
                                        _questAgent2 = agentToCompare2;

                                        MBTextManager.SetTextVariable("FIGHTSTOPQUEST_QUESTAGENT1_TYPE", _questAgent1.Name);
                                        MBTextManager.SetTextVariable("FIGHTSTOPQUEST_QUESTAGENT2_TYPE", _questAgent2.Name);

                                        _questAgent1ActCh0 = _questAgent1.GetCurrentAction(0);
                                        _questAgent1ActCh1 = _questAgent1.GetCurrentAction(1);

                                        _questAgent2ActCh0 = _questAgent2.GetCurrentAction(0);
                                        _questAgent2ActCh1 = _questAgent2.GetCurrentAction(1);

                                        _questAgent1.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().AddBehavior<StopFightQuestBehavior>();
                                        _questAgent2.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().AddBehavior<StopFightQuestBehavior>();
                                        
                                        _questAgent1.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().SetScriptedBehavior<StopFightQuestBehavior>();
                                        _questAgent2.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().SetScriptedBehavior<StopFightQuestBehavior>();

                                        _questAgent1.SetLookToPointOfInterest(_questAgent2.GetEyeGlobalPosition());
                                        _questAgent2.SetLookToPointOfInterest(_questAgent1.GetEyeGlobalPosition());

                                        
                                        _questAgent2.SetLookAgent(_questAgent1);
                                        _questAgent1.SetLookAgent(_questAgent2);

                                        
                                        _questGivable = QuestGivable.givable;
                                        break;
                                    }
                                    
                                    
                                }
                            }
                        }

                        if (_questAgent1 != null && _questAgent2 != null)
                        {
                            break;
                        }
                    }


                }

                if(_questGivable == QuestGivable.notCheckedYet && _agentListAcquired == true)
                {
                    _questGivable = QuestGivable.notGivable;
                }
                
            }

            if(_questAgent1 != null && _questAgent2 != null && _questState == QuestState.notTalkedYet)
            {
                _questAgent2.SetLookAgent(_questAgent1);
                _questAgent1.SetLookAgent(_questAgent2);
            }

            
            
            
        }
        private void AddDialogs(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddDialogLine("fight_stop_quest_conversation_id_1", "start", "next_conversation_token", "{=*}This {FIGHTSTOPQUEST_QUESTAGENT2_TYPE} needs a lesson, by my own hands.", IsFighter1, () => {/* has no consequence */}, priority: 2001);
            campaignGameStarter.AddDialogLine("fight_stop_quest_conversation_id_1", "start", "next_conversation_token", "{=*}This {FIGHTSTOPQUEST_QUESTAGENT1_TYPE} needs a lesson, by my own hands.", IsFighter2, () => {/* has no consequence */}, priority: 2001);

            campaignGameStarter.AddPlayerLine("fight_stop_quest_conversation_player_id_1", "next_conversation_token", "final_conversation_token", "{=*}Giving someone a lesson is not a duty of yours. Step away, both of you.", IsOneOfFighters, FightQuestStateChange);
            campaignGameStarter.AddPlayerLine("fight_stop_quest_conversation_player_id_2", "next_conversation_token", "final_conversation_token", "{=*}Fighting with each other does not change anything. Mind your own business.", IsOneOfFighters, FightQuestStateChange);
            campaignGameStarter.AddDialogLine("fight_stop_quest_conversation_id_2", "final_conversation_token", "close_window", "{=*}I guess you are right. We overreacted, sorry.", IsQuestGoingToFinish, FinishQuest);
            
        }

        private void FinishQuest()
        {
            _questState = QuestState.finished;


            Campaign.Current.PlayerTraitDeveloper.AddTraitXp(DefaultTraits.Honor, 30); //giving award here
            _questAgent1.ResetLookAgent();
            _questAgent2.ResetLookAgent();
            
            _questAgent1.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().RemoveBehavior<StopFightQuestBehavior>();
            _questAgent2.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().RemoveBehavior<StopFightQuestBehavior>();

            _questAgent1.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().DisableScriptedBehavior();
            _questAgent2.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>().DisableScriptedBehavior();            
        }

        private bool IsFighter1()
        {
            if( (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent1 && _questState == QuestState.notTalkedYet && _questGivable == QuestGivable.givable)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
       
        private bool IsQuestGoingToFinish()
        {
            if (((Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent1 || (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent2) && _questState == QuestState.talked)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        private bool IsFighter2()
        {
            if ( (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent2 && _questState == QuestState.notTalkedYet && _questGivable == QuestGivable.givable)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        private bool IsOneOfFighters()
        {
            if(((Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent1 || (Agent)MissionConversationHandler.Current.ConversationManager.ConversationAgents[0] == _questAgent2) && _questState == QuestState.notTalkedYet)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void FightQuestStateChange()
        {
            _questState = QuestState.talked;
        }
    }

    

}
