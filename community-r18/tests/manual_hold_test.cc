#include "manual_hold_policy.h"
#include <cassert>
int main(){using manual_hold::Gate;Gate g;
 assert(!g.Tick(false,true,true,true,0).request); // No action from a key held before this context.
 assert(g.Tick(true,true,true,true,10).request);
 assert(!g.Tick(false,false,true,true,80).cancel); // A tap's one pending attack may finish.
 assert(!g.Tick(false,false,true,true,1000).request);
 assert(g.Tick(true,true,true,true,2000).request);
 assert(!g.Tick(false,true,true,true,2300).request);
 assert(!g.Tick(false,true,true,false,2500).request); // Combat/queue/pending request.
 assert(g.Tick(false,true,true,true,2600).request);
 assert(!g.Tick(false,true,true,true,2601).request);
 assert(g.Tick(false,false,true,true,2700).cancel);
 assert(!g.Tick(false,false,true,true,5000).request);
 assert(g.Tick(true,true,true,true,6000).request);
 assert(g.Tick(false,true,false,true,6500).cancel);
 assert(!g.Tick(false,true,true,true,7000).request); // Context changes require a fresh press.
}
