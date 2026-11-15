// Exercise the actual production handler with game APIs replaced by a fixture.
// Timers remain real; clearing next_update simulates each permitted update tick
// without waiting between manual-request regression cases.
#include "fake_game.h"
#include "tactical_policy.h"
namespace tactical_fixture {
inline std::set<int64_t> elites;
inline std::map<int64_t,double> eta;
inline bool queue_busy=false,model_combat=false;
inline int busy_calls=0,busy_on_call=0;
}
#include "patches/parts/hostile_action.cc"
#include <cassert>
#include <iostream>

SectionManager* Hub::get_SectionManager() { return &fake_game::sections; }
NavigationSectionManager* NavigationSectionManager::Instance() { return &fake_game::navigation_sections; }
bool NavigationManager::CanSelectPoi(int64_t id) { return fake_game::visible.contains(id); }
bool NavigationManager::SelectPoi(int64_t id)
{
  if (fake_game::failed_selections.contains(id)) return false;
  fake_game::selected.push_back(id);
  fake_game::battle.TargetFleetDeployedData=&fake_game::models.at(id);
  return true;
}
void ScanEngageButtonsWidget::OnEngageButtonClicked()
{
  ++fake_game::attacks;
  fake_game::local.fleet->CurrentState=FleetState::Impulsing;
}
std::vector<int64_t> VisibleRegularHostileIds()
{
  return {fake_game::visible.begin(),fake_game::visible.end()};
}
bool TryVisibleFleetSystemPosition(int64_t id,float& x,float& y,float& z)
{
  const auto it=fake_game::positions.find(id);
  if(it==fake_game::positions.end()) return false;
  x=static_cast<float>(it->second.x); y=static_cast<float>(it->second.y); z=static_cast<float>(it->second.z);
  return true;
}
bool TryVisibleFleetPosition(int64_t,float&,float&,float&)
{
  ++fake_game::rendered_position_reads;
  return false;
}
bool VisibleHostileKnownDestroyed(int64_t id) { return fake_game::models.at(id).destroyed; }
namespace hostile_audit {
void Record(const char*,int64_t,uint64_t,const char*) {}
bool Blocked(int64_t id,uint64_t) { return fake_game::blocked.contains(id); }
Motion CurrentMotion(int64_t id) { return fake_game::motions.at(id); }
bool Skip(int64_t id,uint64_t own) { return Blocked(id,own)||CurrentMotion(id)!=Motion::Moving; }
void Cooldown(int64_t id,uint64_t,const char*,int64_t) { fake_game::blocked.insert(id); }
bool KnownDestroyed(FleetDeployedData* target) { return target->destroyed; }
uint64_t Selected(int64_t,FleetPlayerData*,double) { return ++fake_game::attempts; }
Attack ObserveButton(ScanEngageButtonsWidget* button,FleetDeployedData*,FleetPlayerData*,uint64_t)
{
  return button->attack;
}
void Dispatched(int64_t,FleetPlayerData*,uint64_t) {}
void TacticalDispatched(int64_t,FleetPlayerData*) {}
bool TacticalBusy(FleetPlayerData* own,std::string& reason,bool) {
  using namespace tactical_fixture;
  const bool combat=model_combat || own->CurrentState==FleetState::Battling;
  reason=combat?"combat_in_progress":"manual_queue_pending";
  return combat || queue_busy || (++busy_calls==busy_on_call);
}
int64_t TacticalChoose(const std::vector<RankedTarget>& candidates,int64_t current,double distance2,uint64_t) {
  std::vector<tactical::Score> scores;
  const auto add=[&](int64_t id,double d){const auto it=tactical_fixture::eta.find(id);
    scores.push_back({id,d,tactical_fixture::elites.contains(id),it!=tactical_fixture::eta.end(),it!=tactical_fixture::eta.end()?std::optional<double>(it->second):std::nullopt});};
  for(const auto& c:candidates)add(c.id,c.distance2);
  if(current && distance2>=0)add(current,distance2);
  return tactical::Choose(scores,current).id;
}
}
namespace {
using namespace fake_game;
void Add(int64_t id,double x,hostile_audit::Motion motion=hostile_audit::Motion::Moving)
{
  models[id]={id,&hull,false}; positions[id]={x,0,0}; motions[id]=motion; visible.insert(id);
}
void Reset()
{
  ResetHeldSelectAttackHostile();
  tactical_fixture::elites.clear();tactical_fixture::eta.clear();
  tactical_fixture::queue_busy=tactical_fixture::model_combat=false;
  tactical_fixture::busy_calls=tactical_fixture::busy_on_call=0;
  models.clear(); positions.clear(); motions.clear(); visible.clear(); blocked.clear(); failed_selections.clear();
  selected.clear(); attempts=0; attacks=rendered_position_reads=0;
  own={1,FleetState::IdleInSpace}; local.fleet=&own; bar._fleetPanelController=&local;
  battle={}; buttons={&battle,true,hostile_audit::Attack::Usable};
  visibility={VisibilityState::Visible}; viewer={&visibility,&buttons};
  ObjectFinder<PreScanTargetWidget>::objects={&viewer};
  sections.CurrentSection=SectionID::Navigation_System;
  navigation_sections.SNavigationManager=&navigation;
  positions[1]={0,0,0};
}
void Tick(bool press)
{
  if(auto it=pursuits.find(local.fleet->Id);it!=pursuits.end())it->second.next_update=0;
  UpdateSelectAttackHostile(&bar,press);
}
}
int main() {
  Reset();Add(10,10);Add(20,30);
  for(int i=0;i<1000;++i)Tick(false);
  assert(selected.empty() && attacks==0);
  Tick(true);Tick(false);assert(attacks==1 && selected==std::vector<int64_t>{10});
  own.CurrentState=FleetState::IdleInSpace;
  for(int i=0;i<1000;++i)Tick(false);
  assert(attacks==1 && selected.size()==1);
  Tick(true);Tick(false);assert(attacks==2);
  Reset();Add(10,10);buttons.attack=hostile_audit::Attack::Disabled;
  Tick(true);Tick(false);assert(attacks==0);
  Reset();Add(10,10);Tick(true);CancelSelectAttackHostile("F7");Tick(false);assert(attacks==0);
  Reset();Add(10,10);Tick(true);
  FleetPlayerData second{2,FleetState::IdleInSpace};positions[2]={40,0,0};local.fleet=&second;
  Tick(false);local.fleet=&own;Tick(false);assert(attacks==0);
  Reset();Add(10,10);tactical_fixture::queue_busy=true;Tick(true);
  tactical_fixture::queue_busy=false;Tick(false);assert(attacks==0 && selected.empty());
  Reset();Add(10,10);own.CurrentState=FleetState::Battling;Tick(true);
  own.CurrentState=FleetState::IdleInSpace;Tick(false);assert(attacks==0 && selected.empty());
  Reset();Add(10,10);Tick(true);sections.CurrentSection=SectionID::Other;
  Tick(false);assert(attacks==0);
  Reset();Add(10,10);
  auto held_tick=[](bool press,bool held){if(auto it=pursuits.find(local.fleet->Id);it!=pursuits.end())it->second.next_update=0;UpdateHeldSelectAttackHostile(&bar,press,held,true);};
  held_tick(true,true);held_tick(false,false);assert(attacks==1);
  own.CurrentState=FleetState::IdleInSpace;
  for(int i=0;i<100;++i){held_tick(false,false);}
  assert(attacks==1);
  held_tick(true,true);held_tick(false,true);assert(attacks==2);own.CurrentState=FleetState::IdleInSpace;
  held_gate.began=NowMs()-1000;held_gate.last=NowMs()-600;
  held_tick(false,true);held_tick(false,true);assert(attacks==3);own.CurrentState=FleetState::IdleInSpace;
  held_gate.last=NowMs()-600;held_tick(false,true);held_tick(false,false);
  for(int i=0;i<100;++i){held_tick(false,false);}
  assert(attacks==3);
  std::cout<<"PASS: tap dispatch, held repeat, release cancellation, ship/view changes, queue and combat guards\n";
}
