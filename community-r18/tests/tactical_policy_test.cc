#include "tactical_policy.h"
#include <cassert>
#include <limits>
using namespace hostile_audit::tactical;
int main(){
 assert(Elite("91902","Hull_L40_Explorer_Klg_Assassin_G4"));
 assert(!Elite("13003","Hull_L40_Destroyer_Klg"));
 assert(!Elite("91902","unverified_other_family"));
 assert(!Elite("999","Hull_L40_Explorer_Klg_Assassin_G4"));
 assert(Elite("unknown","unknown",true));
 assert(std::abs(*Intercept({100,0,0},{-10,0,0},10)-5)<1e-6);
 assert(std::abs(*Intercept({100,0,0},{5,0,0},10)-20)<1e-6);
 assert(!Intercept({100,0,0},{20,0,0},10));
 assert(!Intercept({1000,0,0},{0,0,0},1));
 assert(!Intercept({100,0,0},{0,0,0},0));
 assert(!Intercept({NAN,0,0},{0,0,0},10));
 assert(std::abs(*Intercept({100,0,0},{0,10,0},20)-std::sqrt(10000./300))<1e-6);
 Motion motion;for(int i=0;i<5;i++)motion.Add(i*250,{i*2.5,0,0});
 assert(std::abs(motion.Velocity(1000)->x-10)<1e-6);assert(!motion.Velocity(1800));
 Motion turn;turn.Add(0,{0,0,0});turn.Add(250,{10,0,0});turn.Add(500,{0,0,0});turn.Add(750,{10,0,0});assert(!turn.Velocity(750));
 std::vector<Score> rows={{1,1,false,false,{}},{2,100,true,false,{}}};
 assert(Choose(rows,1).id==2);assert(Choose(rows,2).id==2);
 rows={{1,100,true,true,20},{2,400,true,true,5}};assert(Choose(rows,1).id==2);assert(Choose(rows,1).intercept_mode);
 rows[1].eta=19;assert(Choose(rows,1).id==1);
 rows[1].trajectory_known=false;assert(!Choose(rows,1).intercept_mode);assert(Choose(rows,1).id==1);
 rows={{1,100,false,false,{}},{2,25,false,false,{}}};assert(Choose(rows,1).id==2);
 QueueLatch latch;assert(!latch.Busy(0,false,true,0));assert(latch.Busy(1,false,true,1));
 assert(latch.Busy(100,false,false,0));assert(latch.Busy(200,true,false,0));
 assert(latch.Busy(300,false,true,0));assert(latch.Busy(1049,false,true,0));assert(!latch.Busy(1050,false,true,0));
 assert(latch.Busy(1100,false,true,{}));assert(!latch.Busy(1200,false,true,{},true));
}

