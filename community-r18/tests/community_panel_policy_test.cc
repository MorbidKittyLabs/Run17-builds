#include "community_panel_policy.h"
#include <cassert>
struct Fleet { void* Hull=nullptr; };
struct Bar {int count=0,index=-1;void RequestSelect(int i){++count;index=i;}};
struct Manager {Fleet fleets[8]{};int viewed=0,last=-1;Fleet* GetFleetPlayerData(int i){return &fleets[i];}void RequestViewFleet(Fleet* f,bool locate){assert(locate);++viewed;last=static_cast<int>(f-fleets);}};
int main(){using namespace community_panel;
 assert(Visible(true,true,true,false));
 assert(!Visible(false,true,true,false));assert(!Visible(true,false,true,false));
 assert(!Visible(true,true,false,false));assert(!Visible(true,true,true,true));
 assert(BlockingCanvas("gift_claim_canvas"));assert(BlockingCanvas("event_detail"));
 assert(!BlockingCanvas("building_details"));assert(!BlockingCanvas("ship_management"));assert(!BlockingCanvas("chat_canvas"));assert(!BlockingCanvas("navigation_system"));
 Bar bar;Manager manager;manager.fleets[2].Hull=&manager;
 assert(SelectAndLocate(2,&bar,&manager));assert(bar.count==1&&bar.index==2&&manager.viewed==1&&manager.last==2);
 assert(!SelectAndLocate(1,&bar,&manager));assert(!SelectAndLocate(-1,&bar,&manager));assert(!SelectAndLocate(8,&bar,&manager));
 assert(!SelectAndLocate(2,static_cast<Bar*>(nullptr),&manager));assert(!SelectAndLocate(2,&bar,static_cast<Manager*>(nullptr)));
 assert(bar.count==1&&manager.viewed==1);
 assert(SelectAndLocate(2,&bar,&manager,0));assert(bar.index==0&&manager.last==2);
}
