#include "pursuit_observer_policy.h"
#include <cassert>
using namespace hostile_audit::pursuit_observer;
int main(){
 assert(Turned({1,0,0},{0,1,0}));assert(!Turned({1,0,0},{1,.01,0}));assert(!Turned({0,0,0},{1,0,0}));
 Observer p;Observation o{0,10,10,1,Point{0,0,0},Point{1,0,0},true,false};
 assert(p.Update(o).acquired);
 o.now=200;o.course=Point{0,1,0};assert(p.Update(o).course_changed);
 o.now=400;o.target=0;o.pursued_count=0;assert(p.Update(o).link_cleared);
 for(int t=600;t<1400;t+=200){o.now=t;assert(!p.Update(o).dormant_candidate);}
 o.now=1400;assert(p.Update(o).dormant_candidate);o.now=1600;assert(!p.Update(o).dormant_candidate);
 // Observation gaps, ship changes, missing fields and motion cannot certify dormancy.
 o.now=2300;assert(!p.Update(o).dormant_candidate);
 p.Reset();o={0,10,10,1,Point{},Point{1,0,0},true,false};p.Update(o);
 o.target=0;o.pursued_count.reset();for(int t=200;t<2000;t+=200){o.now=t;assert(!p.Update(o).dormant_candidate);}
 o.pursued_count=0;o.target.reset();for(int t=2000;t<4000;t+=200){o.now=t;assert(!p.Update(o).dormant_candidate);}
 o.target=0;o.battling.reset();for(int t=4000;t<6000;t+=200){o.now=t;assert(!p.Update(o).dormant_candidate);}
 o.battling=false;for(int t=6000;t<8000;t+=200){o.now=t;o.position=Point{double(t),0,0};assert(!p.Update(o).dormant_candidate);}
 // Never-linked patrols, ongoing combat and a non-idle own ship cannot produce success candidates.
 Observer patrol;Observation q{0,10,0,0,Point{},Point{1,0,0},true,false};
 for(int t=0;t<2000;t+=200){q.now=t;assert(!patrol.Update(q).dormant_candidate);}
 Observer busy;q={0,10,10,1,Point{},Point{1,0,0},true,false};busy.Update(q);
 q.target=0;q.pursued_count=0;q.battling=true;
 for(int t=200;t<2000;t+=200){q.now=t;assert(!busy.Update(q).dormant_candidate);}
 q.battling=false;q.own_idle=false;
 for(int t=2000;t<4000;t+=200){q.now=t;assert(!busy.Update(q).dormant_candidate);}
 o.own=11;o.now=8200;assert(!p.Update(o).dormant_candidate);
}

