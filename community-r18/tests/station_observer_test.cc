#include "station_observer_policy.h"
#include <cassert>
#include <stdexcept>
#include <vector>
int main() {
  using fleet_diagnostics::diagnostics::ObserveAndForward;
  std::vector<int> order;
  ObserveAndForward([&]{order.push_back(1);},[&]{order.push_back(2);});
  assert((order==std::vector<int>{1,2}));
  int forwarded=0;
  ObserveAndForward([]{throw std::runtime_error("logger failure");},[&]{++forwarded;});
  assert(forwarded==1);
  bool escaped=false;
  try {ObserveAndForward([]{},[&]{++forwarded;throw std::runtime_error("original failure");});}
  catch(const std::runtime_error&){escaped=true;}
  assert(escaped && forwarded==2);
}

