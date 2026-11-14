#include "patches/parts/hostile_target_policy.h"
#include <cassert>
#include <iostream>
using namespace hostile_audit;
int main()
{
  MotionWindow still, slow, orbit;
  for (int64_t t = 0; t <= 10000; t += 250) {
    still.Add(t, {1,2,3});
    slow.Add(t, {1 + t * 0.0000002,2,3});
    orbit.Add(t, {std::sin(t / 1000.0) * 0.1, std::cos(t / 1000.0) * 0.1, 0});
    if (t < 10000) assert(still.State() == Motion::Unknown);
  }
  assert(still.State() == Motion::Stationary);
  assert(slow.State() == Motion::Moving);
  assert(orbit.State() == Motion::Moving);
  still.Add(10250, {1.01,2,3});
  assert(still.State() == Motion::Moving); // movement immediately restores eligibility
  still.Add(13000, {1.01,2,3});
  assert(still.State() == Motion::Unknown); // lost view/sample gap
  still.Add(13250, {NAN,2,3});
  assert(still.State() == Motion::Unknown);

  // A new target has no permission until current movement is established.
  MotionWindow moving_only;
  assert(!moving_only.RecentlyMoving(0));
  moving_only.Add(0, {1,2,3});
  assert(!moving_only.RecentlyMoving(0));
  moving_only.Add(250, {1.00005,2,3});
  assert(!moving_only.RecentlyMoving(250));
  moving_only.Add(500, {1.00010,2,3});
  assert(moving_only.RecentlyMoving(500)); // very slow drift, below old 0.001 threshold
  assert(moving_only.RecentlyMoving(800));
  assert(!moving_only.RecentlyMoving(801)); // stale proof cannot authorize an attack
  assert(!moving_only.RecentlyMoving(499)); // reversed clock
  moving_only.Add(750, {1.00010,2,3}); // pursuit hostile parks
  assert(!moving_only.RecentlyMoving(750)); // revoke on first unchanged sample
  moving_only.Add(1000, {1.00010,2,3});
  assert(!moving_only.RecentlyMoving(1000));
  moving_only.Add(1250, {1.00015,2,3});
  assert(!moving_only.RecentlyMoving(1250)); // one changed sample is insufficient
  moving_only.Add(1500, {1.00020,2,3});
  assert(moving_only.RecentlyMoving(1500));
  moving_only.Add(3500, {1.00030,2,3});
  assert(!moving_only.RecentlyMoving(3500)); // view/sample gap resets proof
  moving_only.Add(3750, {NAN,2,3});
  assert(!moving_only.RecentlyMoving(3750));

  // Network sample repeats revoke attack permission without proving the ship
  // has parked. Only sustained fresh, unchanged positions release pursuit.
  MotionWindow repeated_sample;
  repeated_sample.Add(0,{0,0,0});
  repeated_sample.Add(250,{0.001,0,0});
  repeated_sample.Add(500,{0.002,0,0});
  assert(repeated_sample.Current(500)==Motion::Moving);
  repeated_sample.Add(750,{0.002,0,0});
  assert(repeated_sample.Current(750)==Motion::Unknown);
  assert(!repeated_sample.RecentlyMoving(750));
  repeated_sample.Add(1000,{0.003,0,0});
  repeated_sample.Add(1250,{0.004,0,0});
  assert(repeated_sample.Current(1250)==Motion::Moving);
  for(int64_t t=1500;t<=2750;t+=250) {
    repeated_sample.Add(t,{0.004,0,0});
    assert(!repeated_sample.RecentlyMoving(t));
    assert(repeated_sample.Current(t)==(t<2750 ? Motion::Unknown : Motion::Stationary));
  }

  MotionWindow parked, jitter, was_moving;
  for (int64_t t=0; t<=10000; t+=250) {
    parked.Add(t,{0,0,0});
    jitter.Add(t,{t % 500 ? 0.00001 : -0.00001,0,0});
    was_moving.Add(t,{t * 0.00001,0,0});
    assert(!parked.RecentlyMoving(t)); // includes warm-up; button color is irrelevant
    assert(!jitter.RecentlyMoving(t)); // alternating label jitter is not coherent movement
  }
  assert(was_moving.RecentlyMoving(10000));
  was_moving.Add(10250,{0.1,0,0});
  assert(was_moving.State()==Motion::Moving); // old window still remembers movement
  assert(!was_moving.RecentlyMoving(10250)); // that old movement cannot authorize F6

  // Regression: both logs stopped when matching cards were observed at 849
  // and 1469 ms. Those cards must be evaluated before the normal timeout.
  EngageWindow delayed;
  delayed.Begin(10000);
  assert(delayed.Decide(10849, ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(delayed.Decide(11469, ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(delayed.Decide(13100, ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(delayed.Decide(16001, ProbeStatus::Ready)==EngageDecision::Stale);
  assert(delayed.Decide(12999, ProbeStatus::MissingPanel)==EngageDecision::Wait);
  assert(delayed.Decide(13000, ProbeStatus::MissingPanel)==EngageDecision::Timeout);
  assert(delayed.Decide(11000, ProbeStatus::Stationary)==EngageDecision::Reject);
  assert(delayed.Decide(11000, ProbeStatus::Invalid)==EngageDecision::Reject);
  assert(delayed.Decide(11000, ProbeStatus::UnknownButton)==EngageDecision::Wait);
  assert(delayed.Decide(13000, ProbeStatus::UnknownButton)==EngageDecision::Timeout);
  assert(delayed.Decide(13000, ProbeStatus::DisabledButton)==EngageDecision::Timeout);
  assert(delayed.Decide(13000, ProbeStatus::UnknownMotion)==EngageDecision::Wait);
  assert(delayed.Decide(16001, ProbeStatus::UnknownMotion)==EngageDecision::Stale);
  assert(delayed.Due(10000));
  assert(!delayed.Due(10050));
  assert(delayed.Due(10125));

  // Four independent request timers under staggered client updates.
  EngageWindow accounts[4];
  for (int i=0;i<4;++i) accounts[i].Begin(10000+i*500);
  assert(accounts[0].Decide(10849,ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(accounts[1].Decide(11469,ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(accounts[2].Decide(14000,ProbeStatus::DisabledButton)==EngageDecision::Timeout);
  assert(accounts[3].Decide(14500,ProbeStatus::Ready)==EngageDecision::Dispatch);
  assert(accounts[0].Decide(17000,ProbeStatus::Ready)==EngageDecision::Stale);

  TargetCooldowns retry;
  retry.Put(1,123,1000,5000);
  assert(retry.Contains(1,123,1001));
  assert(!retry.Contains(2,123,1001)); // another selected ship remains independent
  assert(!retry.Contains(1,124,1001)); // next target remains eligible
  assert(!retry.Contains(1,123,6000)); // self-recovery without restart
  retry.Put(1,123,10000,30000);
  retry.Put(1,123,10100,1000);
  assert(retry.Contains(1,123,39999)); // short update cannot erase a longer cooldown
  retry.Erase(1,123);
  assert(!retry.Contains(1,123,39999));

  TargetCooldowns backoff;
  assert(backoff.Put(1,999,0,30000)==30000);
  assert(backoff.Put(1,999,30000,30000)==60000);
  assert(backoff.Put(1,999,90000,30000)==120000);
  assert(backoff.Put(1,999,210000,30000)==240000);
  assert(backoff.Put(1,999,450000,30000)==300000);
  assert(backoff.Put(1,999,1100000,30000)==30000); // forget strikes after a long absence

  EngagementProgress watch;
  watch.Begin(0);
  assert(!watch.Failed(1000,true,false));
  watch.Begin(3000); // repeated dispatch must not reset failure detection
  assert(watch.Failed(6000,true,false));
  assert(!watch.Failed(7000,true,false));
  watch.Begin(10000);
  assert(!watch.Failed(17000,false,false)); // valid travel is never a generic failure
  assert(!watch.Failed(20000,true,false)); // allow idle-to-battle transition at arrival
  assert(!watch.Failed(21500,true,true)); // observed combat confirms engagement
  assert(!watch.Failed(24000,true,false));
  watch.Begin(30000);
  assert(!watch.Failed(31000,true,false));
  watch.Reset(); // unknown state/view/selected-ship gap cannot accumulate evidence
  assert(!watch.Failed(50000,true,false));

  MotionWindow low_fps;
  low_fps.Add(0,{0,0,0}); low_fps.Add(1000,{0.001,0,0}); low_fps.Add(2000,{0.002,0,0});
  assert(low_fps.Current(2000)==Motion::Moving);
  low_fps.Add(3000,{0.002,0,0});
  assert(low_fps.Current(3000)==Motion::Unknown);
  low_fps.Add(4000,{0.002,0,0});
  assert(low_fps.Current(4000)==Motion::Stationary);
  assert(low_fps.Current(4301)==Motion::Unknown);

  StallEvidence evidence;
  for (uint64_t i=1; i<=6; ++i)
    assert(!evidence.Observe(i, i*3000, 0, Attack::Usable, true, true, Motion::Stationary, false));
  assert(evidence.Count() == 0); // confirmed live targets may repeat 5 or 6 times
  for (int i=0; i<100; ++i)
    assert(!evidence.Observe(1, i*100, 0, Attack::Disabled, true, true, Motion::Stationary, false));
  assert(evidence.Count() == 1); // repeated polling is one attempt
  evidence.Clear();
  assert(!evidence.Observe(1, 1000, 0.00034455, Attack::Disabled, true, true, Motion::Stationary, false));
  assert(!evidence.Observe(2, 4000, 0.00034455, Attack::Disabled, true, true, Motion::Stationary, false));
  assert(evidence.Observe(3, 7000, 0.00034455, Attack::Disabled, true, true, Motion::Stationary, false));
  evidence.Clear();
  for (uint64_t i=1; i<=6; ++i) {
    assert(!evidence.Observe(i, i*3000, 0, Attack::Unknown, true, true, Motion::Stationary, false));
    assert(!evidence.Observe(i, i*3000, 0, Attack::Disabled, true, false, Motion::Stationary, false));
    assert(!evidence.Observe(i, i*3000, 0, Attack::Disabled, false, true, Motion::Stationary, false));
    assert(!evidence.Observe(i, i*3000, 0, Attack::Disabled, true, true, Motion::Unknown, false));
    assert(!evidence.Observe(i, i*3000, 0, Attack::Disabled, true, true, Motion::Moving, false));
    assert(!evidence.Observe(i, i*3000, 0, Attack::Disabled, true, true, Motion::Stationary, true));
    assert(!evidence.Observe(i, i*3000, 1, Attack::Disabled, true, true, Motion::Stationary, false));
  }
  assert(evidence.Count() == 0);
  assert(!evidence.Observe(50, 30000, 0, Attack::Disabled, true, true, Motion::Stationary, false));
  assert(!evidence.Observe(51, 50000, 0, Attack::Disabled, true, true, Motion::Stationary, false));
  assert(evidence.Count() == 1); // separated visits cannot accumulate stale failure evidence
  evidence.Clear();
  assert(!evidence.Observe(1, 1000, 0, Attack::Disabled, true, true, Motion::Unknown, false));
  assert(!evidence.Observe(2, 4000, 0, Attack::Disabled, true, true, Motion::Unknown, false));
  assert(!evidence.Observe(3, 7000, 0, Attack::Disabled, true, true, Motion::Unknown, false));
  assert(evidence.Count() == 3);
  assert(evidence.Observe(3, 11000, 0, Attack::Disabled, true, true, Motion::Stationary, false));
  // Sampling may complete the stationary window, but does not create extra attempts.
  std::cout << "target audit policy: all cases passed\n";
}

