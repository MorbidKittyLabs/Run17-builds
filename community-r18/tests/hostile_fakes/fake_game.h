#pragma once
#include "patches/parts/hostile_target_policy.h"
#include <cstdint>
#include <map>
#include <set>
#include <vector>

enum class FleetState { Unknown, IdleInSpace, Impulsing, Battling };
enum class SectionID { Navigation_System, Other };
enum class VisibilityState { Visible, Show, Hide };
enum class DeployedFleetType { Marauder, Player };
enum class HullType { Regular, ArmadaTarget };
struct HullSpec { HullType Type = HullType::Regular; };
struct FleetPlayerData { uint64_t Id = 1; FleetState CurrentState = FleetState::IdleInSpace; };
struct FleetDeployedData {
  int64_t id = 0;
  HullSpec* Hull = nullptr;
  bool destroyed = false;
  int64_t GetId64() { return id; }
  bool TryGetFleetType(DeployedFleetType& type) { type=DeployedFleetType::Marauder; return true; }
};
struct BattleContext { FleetDeployedData* TargetFleetDeployedData = nullptr; };
struct ScanEngageButtonsWidget {
  BattleContext* Context = nullptr;
  bool isActiveAndEnabled = true;
  hostile_audit::Attack attack = hostile_audit::Attack::Usable;
  void OnEngageButtonClicked();
};
struct VisibilityController { VisibilityState _state = VisibilityState::Visible; };
struct PreScanTargetWidget {
  VisibilityController* _visibilityController = nullptr;
  ScanEngageButtonsWidget* _scanEngageButtonsWidget = nullptr;
};
struct FleetLocalViewController { FleetPlayerData* fleet = nullptr; };
struct FleetBarViewController { FleetLocalViewController* _fleetPanelController = nullptr; };
struct SectionManager { SectionID CurrentSection = SectionID::Navigation_System; };
struct NavigationManager {
  bool CanSelectPoi(int64_t id);
  bool SelectPoi(int64_t id);
};
struct NavigationSectionManager {
  NavigationManager* SNavigationManager = nullptr;
  static NavigationSectionManager* Instance();
};
struct Hub { static SectionManager* get_SectionManager(); };
template<typename T> struct ObjectFinder {
  inline static std::vector<T*> objects;
  static std::vector<T*> GetAll() { return objects; }
};
namespace fake_game {
inline HullSpec hull;
inline std::map<int64_t,FleetDeployedData> models;
inline std::map<int64_t,hostile_audit::Position> positions;
inline std::map<int64_t,hostile_audit::Motion> motions;
inline std::set<int64_t> visible, blocked, failed_selections;
inline std::vector<int64_t> selected;
inline uint64_t attempts = 0;
inline int attacks = 0, rendered_position_reads = 0;
inline FleetPlayerData own;
inline FleetLocalViewController local;
inline FleetBarViewController bar;
inline BattleContext battle;
inline ScanEngageButtonsWidget buttons;
inline VisibilityController visibility;
inline PreScanTargetWidget viewer;
inline SectionManager sections;
inline NavigationManager navigation;
inline NavigationSectionManager navigation_sections;
}

