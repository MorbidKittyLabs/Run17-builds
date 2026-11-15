#include "community_panel_policy.h"
#include <cassert>
int main(){using namespace community_panel;
 assert(Visible(true,true,true,false));
 assert(!Visible(false,true,true,false));assert(!Visible(true,false,true,false));
 assert(!Visible(true,true,false,false));assert(!Visible(true,true,true,true));
 assert(BlockingCanvas("gift_claim_canvas"));assert(BlockingCanvas("event_detail"));
 assert(BlockingCanvas("building_details"));assert(!BlockingCanvas("navigation_system"));
}
